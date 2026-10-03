using UnityEngine;

namespace GameHub.Analytics
{
    /// <summary>
    /// ScriptableObject chứa toàn bộ cấu hình kết nối Firebase cho GameHub Analytics.
    /// 
    /// Tạo instance: Right-click trong Project > Create > GameHub > Analytics Config
    /// Đặt tên file là "AnalyticsConfig" và đặt vào thư mục Resources/
    /// </summary>
    [CreateAssetMenu(fileName = "AnalyticsConfig", menuName = "GameHub/Analytics Config")]
    public class AnalyticsConfig : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────
        //  Firebase Settings
        // ─────────────────────────────────────────────────────────

        [Header("Firebase Settings")]
        [Tooltip("Firebase Web API Key\nLấy tại: Firebase Console > Project Settings > General > Web API Key")]
        public string apiKey = "";

        [Tooltip("Firebase Project ID\nLấy tại: Firebase Console > Project Settings > General > Project ID")]
        public string projectId = "";

        // ─────────────────────────────────────────────────────────
        //  Game Settings
        // ─────────────────────────────────────────────────────────

        [Header("Game Settings")]
        [Tooltip("Unique identifier của game này trên Firestore (dùng để phân loại data theo game)")]
        public string gameId = "my-game";

        // ─────────────────────────────────────────────────────────
        //  Behavior Settings
        // ─────────────────────────────────────────────────────────

        [Header("Behavior")]
        [Tooltip("Bật/tắt analytics toàn bộ package. Khi false, mọi event đều bị bỏ qua.")]
        public bool isEnabled = true;

        [Tooltip("Hiện debug log trong Console. Nên tắt khi build production.")]
        public bool enableDebugLog = true;

        [Tooltip("Số lượng event tối đa được giữ trong queue khi offline (tránh tràn bộ nhớ)")]
        [Range(10, 500)]
        public int maxQueueSize = 100;

        [Tooltip("Thời gian (giây) giữa mỗi lần thử gửi lại khi mất kết nối")]
        [Range(5f, 300f)]
        public float retryIntervalSeconds = 30f;

        [Tooltip("Timeout (giây) cho mỗi HTTP request")]
        [Range(5f, 60f)]
        public float requestTimeoutSeconds = 10f;

        // ─────────────────────────────────────────────────────────
        //  Computed Properties
        // ─────────────────────────────────────────────────────────

        /// <summary>Base URL của Firestore REST API cho project này.</summary>
        public string FirestoreBaseUrl =>
            $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents";

        /// <summary>Kiểm tra config đã được điền đầy đủ các trường bắt buộc chưa.</summary>
        public bool IsValid =>
            !string.IsNullOrEmpty(apiKey) &&
            !string.IsNullOrEmpty(projectId) &&
            !string.IsNullOrEmpty(gameId);
    }
}
