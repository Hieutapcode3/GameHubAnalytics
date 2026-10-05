using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GameHub.Analytics
{
    [Serializable]
    public class MissionEvent
    {
        public string eventType;
        public string missionId;
        public string timestamp;
        public string playerId;
        public string sessionId;
        public string platform;
        public float playTime;
        public int retryCount;
        public Dictionary<string, object> customData;

        public MissionEvent() { }

        public MissionEvent(string eventType, string missionId)
        {
            this.eventType  = eventType;
            this.missionId  = missionId;
            this.timestamp  = DateTime.UtcNow.ToString("o");
            this.platform   = PlatformHelper.GetPlatformName();
            this.sessionId  = SessionManager.SessionId;
            this.customData = new Dictionary<string, object>();
        }

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
