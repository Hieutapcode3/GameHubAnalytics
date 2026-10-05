using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameHub.Analytics
{
    public class PlayerDataManager
    {
        private readonly AnalyticsConfig _config;
        private readonly string _playerId;

        private readonly System.Collections.Generic.Dictionary<string, PlayerMissionStats> _statsCache
            = new System.Collections.Generic.Dictionary<string, PlayerMissionStats>();

        public PlayerDataManager(AnalyticsConfig config, string playerId)
        {
            _config   = config;
            _playerId = playerId;
        }

        public IEnumerator UpsertProfile(Action<bool> onComplete = null)
        {
            string docPath  = $"players/{_playerId}";
            string url      = $"{_config.FirestoreBaseUrl}/{docPath}?key={_config.apiKey}" +
                              $"&updateMask.fieldPaths=platform" +
                              $"&updateMask.fieldPaths=deviceModel" +
                              $"&updateMask.fieldPaths=osVersion" +
                              $"&updateMask.fieldPaths=lastSeen" +
                              $"&updateMask.fieldPaths=playerId";

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

        public IEnumerator IncrementStarted(string missionId, Action<bool> onComplete = null)
        {
            yield return CommitIncrement(missionId,
                ("started", 1),
                ("lastPlayed_str", DateTime.UtcNow.ToString("o")),
                onComplete: onComplete);
        }

        public IEnumerator IncrementCompleted(string missionId, float playTime, Action<bool> onComplete = null)
        {
            float currentBest = float.MaxValue;
            if (_statsCache.TryGetValue(missionId, out var cached))
                currentBest = cached.bestTime > 0 ? cached.bestTime : float.MaxValue;

            bool isNewBest = playTime < currentBest;

            yield return CommitCompletionIncrement(missionId, playTime, isNewBest, onComplete);
        }

        public IEnumerator IncrementFailed(string missionId, float playTime, Action<bool> onComplete = null)
        {
            yield return CommitIncrement(missionId,
                ("failed",        1),
                ("totalPlayTime", playTime),
                onComplete: onComplete);
        }

        public IEnumerator GetMissionStats(string missionId, Action<PlayerMissionStats> onComplete)
        {
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

        public PlayerMissionStats GetCachedStats(string missionId)
        {
            return _statsCache.TryGetValue(missionId, out var stats) ? stats : null;
        }

        public void InvalidateCache(string missionId = null)
        {
            if (missionId == null)
                _statsCache.Clear();
            else
                _statsCache.Remove(missionId);
        }

        private IEnumerator CommitIncrement(string missionId,
            (string field, object value) increment1,
            (string field, object value) increment2 = default,
            Action<bool> onComplete = null)
        {
            string docPath = $"projects/{_config.CleanProjectId}/databases/(default)/documents" +
                             $"/players/{_playerId}/missions/{missionId}";

            string url  = $"https://firestore.googleapis.com/v1/projects/{_config.CleanProjectId}" +
                          $"/databases/(default)/documents:commit?key={_config.CleanApiKey}";
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

        private IEnumerator CommitCompletionIncrement(string missionId, float playTime, bool isNewBest,
            Action<bool> onComplete = null)
        {
            string docPath = $"projects/{_config.CleanProjectId}/databases/(default)/documents" +
                             $"/players/{_playerId}/missions/{missionId}";

            string url  = $"https://firestore.googleapis.com/v1/projects/{_config.CleanProjectId}" +
                          $"/databases/(default)/documents:commit?key={_config.CleanApiKey}";
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

        private string BuildCommitBody(string docPath,
            (string field, object value) inc1,
            (string field, object value) inc2 = default)
        {
            var sb = new StringBuilder();
            sb.Append("{\"writes\":[{\"transform\":{");
            sb.Append($"\"document\":\"{docPath}\",");
            sb.Append("\"fieldTransforms\":[");

            sb.Append(BuildFieldTransform(inc1.field, inc1.value));

            if (!string.IsNullOrEmpty(inc2.field))
            {
                sb.Append(",");
                sb.Append(BuildFieldTransform(inc2.field, inc2.value));
            }

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

            sb.Append("{\"fieldPath\":\"completed\",\"increment\":{\"integerValue\":\"1\"}}");

            string playTimeStr = playTime.ToString("G", CultureInfo.InvariantCulture);

            sb.Append(",{\"fieldPath\":\"totalPlayTime\",\"increment\":{\"doubleValue\":");
            sb.Append(playTimeStr);
            sb.Append("}}");

            sb.Append(",{\"fieldPath\":\"lastPlayed\",\"setToServerValue\":\"REQUEST_TIME\"}");

            sb.Append("]}}");

            if (setNewBest)
            {
                sb.Append(",{\"update\":{\"name\":\"");
                sb.Append(docPath);
                sb.Append("\",\"fields\":{\"bestTime\":{\"doubleValue\":");
                sb.Append(playTimeStr);
                sb.Append("}}},\"updateMask\":{\"fieldPaths\":[\"bestTime\"]}}");
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
            return $"{{\"fieldPath\":\"{field}\",\"setToServerValue\":\"REQUEST_TIME\"}}";
        }

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

        private static string ExtractFieldValue(string json, string field)
        {
            int fIdx = json.IndexOf($"\"{field}\"", StringComparison.Ordinal);
            if (fIdx < 0) return "";

            int colon = json.IndexOf(':', fIdx + field.Length + 2);
            if (colon < 0) return "";

            int openBrace = json.IndexOf('{', colon);
            if (openBrace < 0) return "";

            int closeBrace = -1;
            bool inQuotes = false;
            for (int i = openBrace + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    i++;
                    continue;
                }
                if (c == '"') inQuotes = !inQuotes;
                else if (c == '}' && !inQuotes)
                {
                    closeBrace = i;
                    break;
                }
            }
            if (closeBrace < 0) return "";

            string block = json.Substring(openBrace, closeBrace - openBrace + 1);
            int valColon = block.IndexOf(':');
            if (valColon < 0) return "";

            int q1 = block.IndexOf('"', valColon + 1);
            if (q1 >= 0)
            {
                int q2 = block.IndexOf('"', q1 + 1);
                if (q2 > q1) return block.Substring(q1 + 1, q2 - q1 - 1);
            }

            int endBrace = block.IndexOf('}', valColon + 1);
            if (endBrace > valColon)
                return block.Substring(valColon + 1, endBrace - valColon - 1).Trim();

            return "";
        }

        private static int ExtractInt(string json, string field)
        {
            string val = ExtractFieldValue(json, field);
            return int.TryParse(val, out int v) ? v : 0;
        }

        private static float ExtractFloat(string json, string field)
        {
            string val = ExtractFieldValue(json, field);
            return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private static string ExtractString(string json, string field)
        {
            return ExtractFieldValue(json, field);
        }

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
