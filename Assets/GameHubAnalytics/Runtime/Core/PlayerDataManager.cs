using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameHub.Analytics
{
    /// <summary>
    /// Quản lý đọc và ghi dữ liệu player lên Firebase Firestore.
    /// 
    /// Firestore paths:
    ///   players/{playerId}                          ← PlayerProfile
    ///   players/{playerId}/missions/{missionId}     ← PlayerMissionStats
    /// 
    /// Dùng atomic increment (Firestore commit API) để tránh race condition.
    /// </summary>
    public class PlayerDataManager
    {
        // ─────────────────────────────────────────────────────────
        //  Fields
        // ─────────────────────────────────────────────────────────

        private readonly AnalyticsConfig _config;
        private readonly string _playerId;

        // Cache local để tránh đọc Firestore liên tục
        // Key: missionId, Value: stats
        private readonly System.Collections.Generic.Dictionary<string, PlayerMissionStats> _statsCache
            = new System.Collections.Generic.Dictionary<string, PlayerMissionStats>();

        // ─────────────────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────────────────

        public PlayerDataManager(AnalyticsConfig config, string playerId)
        {
            _config   = config;
            _playerId = playerId;
        }

        // ─────────────────────────────────────────────────────────
        //  Public API — Write
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Ghi PlayerProfile lên Firestore khi lần đầu mở game.
        /// Dùng PATCH để chỉ update lastSeen nếu profile đã tồn tại.
        /// </summary>
        public IEnumerator UpsertProfile(Action<bool> onComplete = null)
        {
            string docPath  = $"players/{_playerId}";
            string url      = $"{_config.FirestoreBaseUrl}/{docPath}?key={_config.apiKey}" +
                              $"&updateMask.fieldPaths=platform" +
                              $"&updateMask.fieldPaths=deviceModel" +
                              $"&updateMask.fieldPaths=osVersion" +
                              $"&updateMask.fieldPaths=lastSeen" +
                              $"&updateMask.fieldPaths=playerId";

            // Kiểm tra xem profile đã tồn tại chưa (để set firstSeen)
            bool isNew = false;
            yield return CheckDocumentExists(docPath, exists => isNew = !exists);

            if (isNew)
                url += "&updateMask.fieldPaths=firstSeen&updateMask.fieldPaths=totalSessions";

            string body = BuildProfileJson(isNew);

            Log($"Upserting profile → player={_playerId} | isNew={isNew}");

            using (var request = new UnityWebRequest($"{_config.FirestoreBaseUrl}/{docPath}?key={_config.apiKey}", "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.RoundToInt(_config.requestTimeoutSeconds);

                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success;
                if (ok)
                    Log($"✓ Profile upserted: {_playerId}");
                else
                    LogError($"✗ Profile upsert failed: {request.error}");

                onComplete?.Invoke(ok);
            }
        }

        /// <summary>
        /// Tăng counter "started" cho mission này (atomic increment).
        /// Gọi khi: LogStartMission()
        /// </summary>
        public IEnumerator IncrementStarted(string missionId, Action<bool> onComplete = null)
        {
            yield return CommitIncrement(missionId,
                ("started", 1),
                ("lastPlayed_str", DateTime.UtcNow.ToString("o")),
                onComplete: onComplete);
        }

        /// <summary>
        /// Tăng counter "completed" và cập nhật bestTime (nếu là record mới).
        /// Gọi khi: LogCompleteMission()
        /// </summary>
        public IEnumerator IncrementCompleted(string missionId, float playTime, Action<bool> onComplete = null)
        {
            // Đọc bestTime hiện tại từ cache hoặc Firestore
            float currentBest = float.MaxValue;
            if (_statsCache.TryGetValue(missionId, out var cached))
                currentBest = cached.bestTime > 0 ? cached.bestTime : float.MaxValue;

            bool isNewBest = playTime < currentBest;

            yield return CommitCompletionIncrement(missionId, playTime, isNewBest, onComplete);
        }

        /// <summary>
        /// Tăng counter "failed" và cộng totalPlayTime.
        /// Gọi khi: LogFailMission()
        /// </summary>
        public IEnumerator IncrementFailed(string missionId, float playTime, Action<bool> onComplete = null)
        {
            yield return CommitIncrement(missionId,
                ("failed",        1),
                ("totalPlayTime", playTime),
                onComplete: onComplete);
        }

        // ─────────────────────────────────────────────────────────
        //  Public API — Read
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Đọc thống kê mission của player từ Firestore.
        /// Kết quả được cache local.
        /// </summary>
        public IEnumerator GetMissionStats(string missionId, Action<PlayerMissionStats> onComplete)
        {
            // Trả cache ngay nếu có
            if (_statsCache.TryGetValue(missionId, out var cached))
            {
                onComplete?.Invoke(cached);
                yield break;
            }

            string url = $"{_config.FirestoreBaseUrl}/players/{_playerId}/missions/{missionId}?key={_config.apiKey}";
            Log($"Fetching stats → player={_playerId} | mission={missionId}");

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.RoundToInt(_config.requestTimeoutSeconds);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var stats = ParseMissionStats(request.downloadHandler.text);
                    _statsCache[missionId] = stats;
                    Log($"✓ Stats loaded: {stats}");
                    onComplete?.Invoke(stats);
                }
                else if (request.responseCode == 404)
                {
                    // Document chưa tồn tại → trả về stats rỗng
                    var empty = new PlayerMissionStats();
                    _statsCache[missionId] = empty;
                    onComplete?.Invoke(empty);
                }
                else
                {
                    LogError($"✗ Get stats failed: {request.error}");
                    onComplete?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// Lấy stats từ cache local (không gọi network).
        /// Trả về null nếu chưa có trong cache.
        /// </summary>
        public PlayerMissionStats GetCachedStats(string missionId)
        {
            return _statsCache.TryGetValue(missionId, out var stats) ? stats : null;
        }

        /// <summary>Xóa cache để force refresh từ Firestore lần sau.</summary>
        public void InvalidateCache(string missionId = null)
        {
            if (missionId == null)
                _statsCache.Clear();
            else
                _statsCache.Remove(missionId);
        }

        // ─────────────────────────────────────────────────────────
        //  Firestore Atomic Operations
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Dùng Firestore commit API để tăng counter atomic (thread-safe).
        /// Tham số: tuple (fieldPath, incrementValue)
        /// </summary>
        private IEnumerator CommitIncrement(string missionId,
            (string field, object value) increment1,
            (string field, object value) increment2 = default,
            Action<bool> onComplete = null)
        {
            string docPath = $"projects/{_config.projectId}/databases/(default)/documents" +
                             $"/players/{_playerId}/missions/{missionId}";

            string url  = $"https://firestore.googleapis.com/v1/projects/{_config.projectId}" +
                          $"/databases/(default)/documents:commit?key={_config.apiKey}";
            string body = BuildCommitBody(docPath, increment1, increment2);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.RoundToInt(_config.requestTimeoutSeconds);

                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success;
                if (ok)
                {
                    // Cập nhật cache local
                    UpdateLocalCache(missionId, increment1, increment2);
                    Log($"✓ Increment OK: {missionId}.{increment1.field}+={increment1.value}");
                }
                else
                {
                    LogError($"✗ Increment failed [{missionId}.{increment1.field}]: {request.error}\n{request.downloadHandler?.text}");
                }

                onComplete?.Invoke(ok);
            }
        }

        /// <summary>
        /// Commit hoàn thành mission: tăng completed + totalPlayTime + cập nhật bestTime nếu là record.
        /// </summary>
        private IEnumerator CommitCompletionIncrement(string missionId, float playTime, bool isNewBest,
            Action<bool> onComplete = null)
        {
            string docPath = $"projects/{_config.projectId}/databases/(default)/documents" +
                             $"/players/{_playerId}/missions/{missionId}";

            string url  = $"https://firestore.googleapis.com/v1/projects/{_config.projectId}" +
                          $"/databases/(default)/documents:commit?key={_config.apiKey}";
            string body = BuildCompletionCommitBody(docPath, playTime, isNewBest);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.RoundToInt(_config.requestTimeoutSeconds);

                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success;
                if (ok)
                {
                    // Cập nhật cache
                    if (_statsCache.TryGetValue(missionId, out var s))
                    {
                        s.completed++;
                        s.totalPlayTime += playTime;
                        if (isNewBest) s.bestTime = playTime;
                        _statsCache[missionId] = s;
                    }
                    Log($"✓ Completion recorded: {missionId} | time={playTime:F1}s | newBest={isNewBest}");
                }
                else
                {
                    LogError($"✗ Completion increment failed: {request.error}");
                }

                onComplete?.Invoke(ok);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  JSON Builders
        // ─────────────────────────────────────────────────────────

        private string BuildProfileJson(bool includeFirstSeen)
        {
            var sb = new StringBuilder();
            sb.Append("{\"fields\":{");
            sb.Append($"\"playerId\":{{\"stringValue\":\"{_playerId}\"}},");
            sb.Append($"\"platform\":{{\"stringValue\":\"{PlatformHelper.GetPlatformName()}\"}},");
            sb.Append($"\"deviceModel\":{{\"stringValue\":\"{EscapeJson(SystemInfo.deviceModel)}\"}},");
            sb.Append($"\"osVersion\":{{\"stringValue\":\"{EscapeJson(SystemInfo.operatingSystem)}\"}},");
            sb.Append($"\"lastSeen\":{{\"stringValue\":\"{DateTime.UtcNow:o}\"}}");

            if (includeFirstSeen)
            {
                sb.Append($",\"firstSeen\":{{\"stringValue\":\"{DateTime.UtcNow:o}\"}}");
                sb.Append($",\"totalSessions\":{{\"integerValue\":\"1\"}}");
            }

            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>Tạo Firestore commit body cho atomic increment.</summary>
        private string BuildCommitBody(string docPath,
            (string field, object value) inc1,
            (string field, object value) inc2 = default)
        {
            var sb = new StringBuilder();
            sb.Append("{\"writes\":[{\"transform\":{");
            sb.Append($"\"document\":\"{docPath}\",");
            sb.Append("\"fieldTransforms\":[");

            // Transform 1
            sb.Append(BuildFieldTransform(inc1.field, inc1.value));

            // Transform 2 (nếu có)
            if (!string.IsNullOrEmpty(inc2.field))
            {
                sb.Append(",");
                sb.Append(BuildFieldTransform(inc2.field, inc2.value));
            }

            // Luôn update lastPlayed
            sb.Append($",{{\"fieldPath\":\"lastPlayed\",\"setToServerValue\":\"REQUEST_TIME\"}}");

            sb.Append("]}}]}");
            return sb.ToString();
        }

        private string BuildCompletionCommitBody(string docPath, float playTime, bool setNewBest)
        {
            var sb = new StringBuilder();
            sb.Append("{\"writes\":[{\"transform\":{");
            sb.Append($"\"document\":\"{docPath}\",");
            sb.Append("\"fieldTransforms\":[");

            // Tăng completed
            sb.Append("{\"fieldPath\":\"completed\",\"increment\":{\"integerValue\":\"1\"}}");

            // Cộng thêm totalPlayTime
            sb.Append($",{{\"fieldPath\":\"totalPlayTime\",\"increment\":{{\"doubleValue\":{playTime.ToString("G", CultureInfo.InvariantCulture)}}}}}");

            // lastPlayed server time
            sb.Append(",{\"fieldPath\":\"lastPlayed\",\"setToServerValue\":\"REQUEST_TIME\"}");

            sb.Append("]}}");

            // Nếu là best time → thêm write riêng để set bestTime (không dùng increment vì cần set giá trị mới)
            if (setNewBest)
            {
                sb.Append($",{{\"update\":{{\"name\":\"{docPath}\"," +
                          $"\"fields\":{{\"bestTime\":{{\"doubleValue\":{playTime.ToString("G", CultureInfo.InvariantCulture)}}}}}}}}," +
                          $"\"updateMask\":{{\"fieldPaths\":[\"bestTime\"]}}}}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static string BuildFieldTransform(string field, object value)
        {
            if (value is int iv)
                return $"{{\"fieldPath\":\"{field}\",\"increment\":{{\"integerValue\":\"{iv}\"}}}}";
            if (value is float fv)
                return $"{{\"fieldPath\":\"{field}\",\"increment\":{{\"doubleValue\":{fv.ToString("G", CultureInfo.InvariantCulture)}}}}}";
            if (value is double dv)
                return $"{{\"fieldPath\":\"{field}\",\"increment\":{{\"doubleValue\":{dv.ToString("G", CultureInfo.InvariantCulture)}}}}}";
            // String → dùng set thay vì increment
            return $"{{\"fieldPath\":\"{field}\",\"setToServerValue\":\"REQUEST_TIME\"}}";
        }

        // ─────────────────────────────────────────────────────────
        //  Firestore Response Parser
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Parse Firestore document JSON thành PlayerMissionStats.
        /// Firestore format: { "fields": { "fieldName": { "integerValue": "5" }, ... } }
        /// </summary>
        private static PlayerMissionStats ParseMissionStats(string json)
        {
            var stats = new PlayerMissionStats();
            if (string.IsNullOrEmpty(json)) return stats;

            stats.started       = ExtractInt(json,    "started");
            stats.completed     = ExtractInt(json,    "completed");
            stats.failed        = ExtractInt(json,    "failed");
            stats.bestTime      = ExtractFloat(json,  "bestTime");
            stats.totalPlayTime = ExtractFloat(json,  "totalPlayTime");
            stats.lastPlayed    = ExtractString(json, "lastPlayed");

            return stats;
        }

        private static int ExtractInt(string json, string field)
        {
            string pattern = $"\"{field}\":{{\"integerValue\":\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return 0;
            idx += pattern.Length;
            int end = json.IndexOf("\"", idx, StringComparison.Ordinal);
            return end > idx && int.TryParse(json.Substring(idx, end - idx), out int v) ? v : 0;
        }

        private static float ExtractFloat(string json, string field)
        {
            // Thử doubleValue trước
            string pattern = $"\"{field}\":{{\"doubleValue\":";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return 0f;
            idx += pattern.Length;
            int end = json.IndexOfAny(new[] { ',', '}' }, idx);
            return end > idx && float.TryParse(json.Substring(idx, end - idx),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private static string ExtractString(string json, string field)
        {
            string pattern = $"\"{field}\":{{\"stringValue\":\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return "";
            idx += pattern.Length;
            int end = json.IndexOf("\"", idx, StringComparison.Ordinal);
            return end > idx ? json.Substring(idx, end - idx) : "";
        }

        // ─────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────

        private IEnumerator CheckDocumentExists(string docPath, Action<bool> onResult)
        {
            string url = $"{_config.FirestoreBaseUrl}/{docPath}?key={_config.apiKey}&mask.fieldPaths=playerId";
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();
                onResult?.Invoke(request.result == UnityWebRequest.Result.Success);
            }
        }

        private void UpdateLocalCache(string missionId,
            (string field, object value) inc1,
            (string field, object value) inc2)
        {
            if (!_statsCache.TryGetValue(missionId, out var stats))
                stats = new PlayerMissionStats();

            ApplyIncrement(ref stats, inc1.field, inc1.value);
            if (!string.IsNullOrEmpty(inc2.field))
                ApplyIncrement(ref stats, inc2.field, inc2.value);

            _statsCache[missionId] = stats;
        }

        private static void ApplyIncrement(ref PlayerMissionStats stats, string field, object value)
        {
            switch (field)
            {
                case "started":       stats.started++;                                        break;
                case "completed":     stats.completed++;                                       break;
                case "failed":        stats.failed++;                                          break;
                case "totalPlayTime": stats.totalPlayTime += Convert.ToSingle(value);          break;
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private void Log(string msg)
        {
            if (_config.enableDebugLog)
                Debug.Log($"[PlayerData] {msg}");
        }

        private void LogError(string msg) =>
            Debug.LogError($"[PlayerData] {msg}");
    }
}
