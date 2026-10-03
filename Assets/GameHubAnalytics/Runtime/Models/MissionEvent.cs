using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GameHub.Analytics
{
    /// <summary>
    /// Data model đại diện cho một analytics event trong game.
    /// Hỗ trợ serialize thành định dạng Firestore REST API JSON.
    /// </summary>
    [Serializable]
    public class MissionEvent
    {
        // ─────────────────────────────────────────────────────────
        //  Core Fields
        // ─────────────────────────────────────────────────────────

        /// <summary>Loại sự kiện: start_mission, complete_mission, fail_mission, v.v.</summary>
        public string eventType;

        /// <summary>ID của mission (ví dụ: "level_01", "boss_stage_3")</summary>
        public string missionId;

        /// <summary>Thời điểm xảy ra event (ISO 8601 UTC)</summary>
        public string timestamp;

        /// <summary>ID người chơi (auto-generated hoặc custom)</summary>
        public string playerId;

        /// <summary>Session ID duy nhất cho mỗi lần mở game</summary>
        public string sessionId;

        /// <summary>Nền tảng: Android, iOS, Windows, Editor, v.v.</summary>
        public string platform;

        // ─────────────────────────────────────────────────────────
        //  Performance Fields
        // ─────────────────────────────────────────────────────────

        /// <summary>Thời gian chơi của event này (giây)</summary>
        public float playTime;

        /// <summary>Số lần chơi lại mission này trong session hiện tại</summary>
        public int retryCount;

        // ─────────────────────────────────────────────────────────
        //  Extension
        // ─────────────────────────────────────────────────────────

        /// <summary>Data tùy chỉnh mở rộng (key-value pairs)</summary>
        public Dictionary<string, object> customData;

        // ─────────────────────────────────────────────────────────
        //  Constructors
        // ─────────────────────────────────────────────────────────

        public MissionEvent() { }

        /// <summary>
        /// Tạo event với đầy đủ context tự động (timestamp, platform, sessionId).
        /// </summary>
        public MissionEvent(string eventType, string missionId)
        {
            this.eventType  = eventType;
            this.missionId  = missionId;
            this.timestamp  = DateTime.UtcNow.ToString("o"); // ISO 8601
            this.platform   = PlatformHelper.GetPlatformName();
            this.sessionId  = SessionManager.SessionId;
            this.customData = new Dictionary<string, object>();
        }

        // ─────────────────────────────────────────────────────────
        //  Firestore Serialization
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Chuyển MissionEvent thành Firestore REST document JSON.
        /// Format: { "fields": { "fieldName": { "stringValue": "..." }, ... } }
        /// </summary>
        public string ToFirestoreJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"fields\":{");

            AppendStringField(sb, "eventType",  eventType);  sb.Append(",");
            AppendStringField(sb, "missionId",  missionId);  sb.Append(",");
            AppendStringField(sb, "timestamp",  timestamp);  sb.Append(",");
            AppendStringField(sb, "playerId",   playerId);   sb.Append(",");
            AppendStringField(sb, "sessionId",  sessionId);  sb.Append(",");
            AppendStringField(sb, "platform",   platform);   sb.Append(",");
            AppendDoubleField(sb, "playTime",   playTime);   sb.Append(",");
            AppendIntField   (sb, "retryCount", retryCount);

            if (customData != null && customData.Count > 0)
            {
                sb.Append(",");
                AppendMapField(sb, "customData", customData);
            }

            sb.Append("}}");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────
        //  Private Helpers
        // ─────────────────────────────────────────────────────────

        private static void AppendStringField(StringBuilder sb, string key, string value)
        {
            sb.Append($"\"{key}\":{{\"stringValue\":\"{EscapeJson(value ?? "")}\"}}");
        }

        private static void AppendDoubleField(StringBuilder sb, string key, double value)
        {
            sb.Append($"\"{key}\":{{\"doubleValue\":{value.ToString("G", CultureInfo.InvariantCulture)}}}");
        }

        private static void AppendIntField(StringBuilder sb, string key, long value)
        {
            sb.Append($"\"{key}\":{{\"integerValue\":\"{value}\"}}");
        }

        private static void AppendMapField(StringBuilder sb, string key, Dictionary<string, object> map)
        {
            sb.Append($"\"{key}\":{{\"mapValue\":{{\"fields\":{{");
            bool first = true;
            foreach (var kvp in map)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kvp.Key}\":{ValueToFirestoreField(kvp.Value)}");
                first = false;
            }
            sb.Append("}}}}}");
        }

        private static string ValueToFirestoreField(object value)
        {
            if (value == null)   return "{\"nullValue\":null}";
            if (value is bool b) return $"{{\"booleanValue\":{b.ToString().ToLower()}}}";
            if (value is int  i) return $"{{\"integerValue\":\"{i}\"}}";
            if (value is long l) return $"{{\"integerValue\":\"{l}\"}}";
            if (value is float  f) return $"{{\"doubleValue\":{f.ToString("G", CultureInfo.InvariantCulture)}}}";
            if (value is double d) return $"{{\"doubleValue\":{d.ToString("G", CultureInfo.InvariantCulture)}}}";
            return $"{{\"stringValue\":\"{EscapeJson(value.ToString())}\"}}";
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
