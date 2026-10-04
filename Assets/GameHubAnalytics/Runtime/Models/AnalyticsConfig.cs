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
        //  Platform Filter Settings
        // ─────────────────────────────────────────────────────────

        [Header("Platform Filter")]
        [Tooltip("Chỉ gửi các sự kiện gameplay màn chơi (Start, Win, Lose, Retry, Quit) lên Firebase khi chạy trên thiết bị di động thật hoặc máy giả lập (Bỏ qua khi chạy trong Unity Editor).")]
        public bool mobileOnlyForMissionEvents = true;

        [Tooltip("Chỉ gửi sự kiện Thắng/Thua (Complete/Fail) lên Firebase khi chạy trên thiết bị di động thật hoặc máy giả lập (Bỏ qua khi chạy trong Unity Editor).")]
        public bool mobileOnlyForWinLose = true;

        [Tooltip("Tắt hoàn toàn việc gửi dữ liệu lên Firebase khi chạy trong Unity Editor (bao gồm cả Profile, Session và Custom Events).")]
        public bool disableAllInEditor = false;

        // ─────────────────────────────────────────────────────────
        //  Computed Properties (Auto-Trimmed to prevent space errors)
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra xem ứng dụng hiện tại có đang chạy trên thiết bị di động thật hoặc máy giả lập (Android, iOS) hay không.
        /// Luôn trả về false khi đang chạy trong Unity Editor.
        /// </summary>
        public static bool IsMobileOrEmulator
        {
            get
            {
#if UNITY_EDITOR
                return false;
#else
                return Application.isMobilePlatform ||
                       Application.platform == RuntimePlatform.Android ||
                       Application.platform == RuntimePlatform.IPhonePlayer;
#endif
            }
        }

        public string CleanProjectId => projectId?.Trim() ?? "";
        public string CleanApiKey    => apiKey?.Trim() ?? "";
        public string CleanGameId    => gameId?.Trim() ?? "";

        /// <summary>Base URL của Firestore REST API cho project này.</summary>
        public string FirestoreBaseUrl =>
            $"https://firestore.googleapis.com/v1/projects/{CleanProjectId}/databases/(default)/documents";

        /// <summary>Kiểm tra config đã được điền đầy đủ các trường bắt buộc chưa.</summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(projectId) &&
            !string.IsNullOrWhiteSpace(gameId);

        private void OnValidate()
        {
            if (apiKey != null) apiKey = apiKey.Trim();
            if (projectId != null) projectId = projectId.Trim();
            if (gameId != null) gameId = gameId.Trim();
        }
    }
}
