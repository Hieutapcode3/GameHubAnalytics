using UnityEngine;

namespace GameHub.Analytics
{
    [CreateAssetMenu(fileName = "AnalyticsConfig", menuName = "GameHub/Analytics Config")]
    public class AnalyticsConfig : ScriptableObject
    {
        [Header("Firebase Settings")]
        [Tooltip("Firebase Web API Key from Firebase Console > Project Settings > General")]
        public string apiKey = "";

        [Tooltip("Firebase Project ID from Firebase Console > Project Settings > General")]
        public string projectId = "";

        [Header("Game Settings")]
        [Tooltip("Unique identifier for this game in Firestore")]
        public string gameId = "my-game";

        [Header("Behavior")]
        [Tooltip("Master switch for analytics. When false, events are ignored.")]
        public bool isEnabled = true;

        [Tooltip("Show debug logs in Unity Console.")]
        public bool enableDebugLog = true;

        [Tooltip("Maximum queue capacity for offline events.")]
        [Range(10, 500)]
        public int maxQueueSize = 100;

        [Tooltip("Retry interval in seconds when disconnected.")]
        [Range(5f, 300f)]
        public float retryIntervalSeconds = 30f;

        [Tooltip("HTTP request timeout in seconds.")]
        [Range(5f, 60f)]
        public float requestTimeoutSeconds = 10f;

        [Header("Platform Filter")]
        [Tooltip("Send mission events (Start, Win, Lose, Retry, Quit) to Firebase only on real mobile devices or emulators.")]
        public bool mobileOnlyForMissionEvents = true;

        [Tooltip("Send Win/Lose events to Firebase only on real mobile devices or emulators.")]
        public bool mobileOnlyForWinLose = true;

        [Tooltip("Disable all Firebase event transmissions when running in Unity Editor.")]
        public bool disableAllInEditor = false;

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

        public string FirestoreBaseUrl =>
            $"https://firestore.googleapis.com/v1/projects/{CleanProjectId}/databases/(default)/documents";

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
