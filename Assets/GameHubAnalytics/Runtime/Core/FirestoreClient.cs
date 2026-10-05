using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameHub.Analytics
{
    public class FirestoreClient
    {
        private readonly AnalyticsConfig _config;

        public FirestoreClient(AnalyticsConfig config)
        {
            _config = config;
        }

        public IEnumerator PostEvent(MissionEvent missionEvent, Action<bool, string> onComplete = null)
        {
            if (!_config.IsValid)
            {
                LogError("AnalyticsConfig is not properly configured (missing apiKey or projectId).");
                onComplete?.Invoke(false, "Config invalid");
                yield break;
            }

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

        public IEnumerator TestConnection(Action<bool, string> onComplete)
        {
            if (!_config.IsValid)
            {
                onComplete?.Invoke(false, "Config invalid: missing apiKey or projectId.");
                yield break;
            }

            string url = $"{_config.FirestoreBaseUrl}/analytics?key={_config.apiKey}&pageSize=1";
            Log($"Testing connection → {_config.projectId}");

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                bool connected = request.result == UnityWebRequest.Result.Success
                              || request.responseCode == 404;

                string msg = connected
                    ? $"Firebase connection successful (HTTP {request.responseCode})"
                    : $"Firebase connection failed: {request.error} (HTTP {request.responseCode})";

                Log(msg);
                onComplete?.Invoke(connected, msg);
            }
        }

        private void Log(string msg)
        {
            if (_config.enableDebugLog)
                Debug.Log($"[Analytics] {msg}");
        }

        private void LogError(string msg) =>
            Debug.LogError($"[Analytics] {msg}");
    }
}
