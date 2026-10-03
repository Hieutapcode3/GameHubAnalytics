using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameHub.Analytics
{
    /// <summary>
    /// Wrapper giao tiếp với Firebase Firestore qua REST API.
    /// Không cần Firebase Unity SDK — chỉ dùng UnityWebRequest có sẵn trong Unity.
    /// 
    /// Firestore document path:
    ///   analytics/{gameId}/missions/{missionId}/events/{autoId}
    /// </summary>
    public class FirestoreClient
    {
        private readonly AnalyticsConfig _config;

        public FirestoreClient(AnalyticsConfig config)
        {
            _config = config;
        }

        // ─────────────────────────────────────────────────────────
        //  Write
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Ghi một MissionEvent lên Firestore (auto-generated document ID).
        /// Gọi bằng StartCoroutine từ MonoBehaviour.
        /// </summary>
        /// <param name="missionEvent">Event cần ghi</param>
        /// <param name="onComplete">Callback (success, responseText)</param>
        public IEnumerator PostEvent(MissionEvent missionEvent, Action<bool, string> onComplete = null)
        {
            if (!_config.IsValid)
            {
                LogError("AnalyticsConfig chưa được cấu hình đầy đủ (thiếu apiKey hoặc projectId).");
                onComplete?.Invoke(false, "Config invalid");
                yield break;
            }

            // Firestore path: POST vào collection để tự gen document ID
            string collectionPath = $"analytics/{_config.gameId}/missions/{missionEvent.missionId}/events";
            string url  = $"{_config.FirestoreBaseUrl}/{collectionPath}?key={_config.apiKey}";
            string body = missionEvent.ToFirestoreJson();

            Log($"→ Sending [{missionEvent.eventType}] mission={missionEvent.missionId}");

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.RoundToInt(_config.requestTimeoutSeconds);

                yield return request.SendWebRequest();

                bool   success  = request.result == UnityWebRequest.Result.Success;
                string response = request.downloadHandler?.text ?? "";

                if (success)
                    Log($"✓ Event sent: [{missionEvent.eventType}] HTTP {request.responseCode}");
                else
                    LogError($"✗ Send failed: {request.error} (HTTP {request.responseCode})\nResponse: {response}");

                onComplete?.Invoke(success, response);
            }
        }

        /// <summary>
        /// Ghi một raw Firestore JSON string lên collection (dùng khi retry từ queue).
        /// </summary>
        /// <param name="missionId">Mission ID để xác định collection path</param>
        /// <param name="firestoreJson">JSON đã serialize sẵn</param>
        /// <param name="onComplete">Callback (success, responseText)</param>
        public IEnumerator PostRawJson(string missionId, string firestoreJson, Action<bool, string> onComplete = null)
        {
            string collectionPath = $"analytics/{_config.gameId}/missions/{missionId}/events";
            string url = $"{_config.FirestoreBaseUrl}/{collectionPath}?key={_config.apiKey}";

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(firestoreJson);
                request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.RoundToInt(_config.requestTimeoutSeconds);

                yield return request.SendWebRequest();

                bool success = request.result == UnityWebRequest.Result.Success;
                onComplete?.Invoke(success, request.downloadHandler?.text ?? "");
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Connection Test
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra kết nối đến Firebase bằng cách GET document analytics root.
        /// HTTP 200 hoặc 404 đều được tính là kết nối thành công.
        /// </summary>
        public IEnumerator TestConnection(Action<bool, string> onComplete)
        {
            if (!_config.IsValid)
            {
                onComplete?.Invoke(false, "Config invalid: thiếu apiKey hoặc projectId.");
                yield break;
            }

            string url = $"{_config.FirestoreBaseUrl}/analytics?key={_config.apiKey}&pageSize=1";
            Log($"Testing connection → {_config.projectId}");

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                // 200 = OK, 404 = collection chưa có nhưng project OK
                bool connected = request.result == UnityWebRequest.Result.Success
                              || request.responseCode == 404;

                string msg = connected
                    ? $"Kết nối Firebase thành công (HTTP {request.responseCode})"
                    : $"Kết nối thất bại: {request.error} (HTTP {request.responseCode})";

                Log(msg);
                onComplete?.Invoke(connected, msg);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Logging
        // ─────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            if (_config.enableDebugLog)
                Debug.Log($"[Analytics] {msg}");
        }

        private void LogError(string msg) =>
            Debug.LogError($"[Analytics] {msg}");
    }
}
