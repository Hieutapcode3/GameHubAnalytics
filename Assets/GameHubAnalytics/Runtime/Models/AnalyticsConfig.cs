using System.Text.RegularExpressions;
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
        [Tooltip("Unique identifier for this game in Firestore. All data is stored under analytics/{gameId}/ so many games can share one Firebase project. Empty = Product Name.")]
        public string gameId = "";

        [Header("Behavior")]
        [Tooltip("Master switch. When false: nothing is sent to or read from Firebase and the name / star rating popups are not shown. Firebase is never used from the Unity Editor either way.")]
        public bool isEnabled = true;

        [Tooltip("Show debug logs in Unity Console.")]
        public bool enableDebugLog = true;

        [Header("Popups")]
        [Tooltip("Ask the player's name (NamePanel) on first launch. Ignored when isEnabled is false.")]
        public bool askPlayerName = true;

        [Tooltip("Ask a 1-5 star rating (RatingPanel) after winning a level. Ignored when isEnabled is false.")]
        public bool askStarRating = true;

        [Tooltip("Maximum queue capacity for offline events.")]
        [Range(10, 500)]
        public int maxQueueSize = 100;

        [Tooltip("Retry interval in seconds when disconnected.")]
        [Range(5f, 300f)]
        public float retryIntervalSeconds = 30f;

        [Tooltip("HTTP request timeout in seconds.")]
        [Range(5f, 60f)]
        public float requestTimeoutSeconds = 10f;

        public string CleanProjectId => projectId?.Trim() ?? "";
        public string CleanApiKey    => apiKey?.Trim() ?? "";
        // Used as a Firestore doc id inside URLs, so only keep URL/path-safe chars
        public string CleanGameId    => Regex.Replace(string.IsNullOrWhiteSpace(gameId) ? Application.productName : gameId.Trim(), "[^A-Za-z0-9_-]", "-");

        /// <summary>Per-game root document; every game in the same Firebase project gets its own subtree.</summary>
        public string GameRoot => $"analytics/{CleanGameId}";

        public string FirestoreBaseUrl =>
            $"https://firestore.googleapis.com/v1/projects/{CleanProjectId}/databases/(default)/documents";

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(projectId);

        private void OnValidate()
        {
            if (apiKey != null) apiKey = apiKey.Trim();
            if (projectId != null) projectId = projectId.Trim();
            if (gameId != null) gameId = gameId.Trim();
        }
    }
}
