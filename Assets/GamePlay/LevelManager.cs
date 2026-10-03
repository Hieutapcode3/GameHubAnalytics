using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameHub.Analytics;

namespace GamePlay
{
    public enum LevelState
    {
        NotStarted,
        Playing,
        Won,
        Lost
    }

    /// <summary>
    /// Quản lý vòng đời Level và tự động bắn event lên Firebase Firestore thông qua GameHub Analytics.
    /// Hỗ trợ:
    ///   - Load level (1, 2, 3...)
    ///   - Bắt đầu màn (LogStartMission)
    ///   - Thắng màn (LogCompleteMission) -> Mở Panel Win -> Next Level / Restart
    ///   - Thua màn (LogFailMission) -> Mở Panel Lose -> Restart
    ///   - Restart màn (LogRetryMission)
    ///   - Lấy thống kê của Player cho màn hiện tại (WinRate, Best Time...)
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Settings")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private bool autoStartOnAwake = true;

        [Header("Runtime State")]
        [SerializeField] private LevelState state = LevelState.NotStarted;
        [SerializeField] private float playTime = 0f;
        [SerializeField] private string lastStatusMessage = "Sẵn sàng";

        // Callbacks cho UI lắng nghe
        public event Action<int> OnLevelLoaded;
        public event Action<float> OnLevelWon;
        public event Action<float> OnLevelLost;
        public event Action<int> OnLevelRestarted;
        public event Action<PlayerMissionStats> OnStatsUpdated;

        // Thống kê cá nhân của level hiện tại
        public PlayerMissionStats CurrentStats { get; private set; }

        public int CurrentLevel => currentLevel;
        public LevelState State => state;
        public float PlayTime => playTime;
        public string LastStatusMessage => lastStatusMessage;
        public string MissionId => $"level_{currentLevel:D2}";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Khởi tạo Analytics nếu chưa chạy
            if (AnalyticsManager.Instance != null && !AnalyticsManager.Instance.IsInitialized)
            {
                AnalyticsManager.Instance.Initialize();
            }

            if (autoStartOnAwake)
            {
                LoadLevel(currentLevel);
            }
        }

        private void Update()
        {
            if (state == LevelState.Playing)
            {
                playTime += Time.deltaTime;
            }
        }

        /// <summary>
        /// Tải màn chơi và bắn event bắt đầu màn chơi lên Firestore.
        /// </summary>
        public void LoadLevel(int level)
        {
            currentLevel = Mathf.Max(1, level);
            playTime = 0f;
            state = LevelState.Playing;

            string mId = MissionId;
            lastStatusMessage = $"Đang chơi {mId}...";

            // Bắn event bắt đầu lên Firebase
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogStartMission(mId);

                // Lấy thống kê cũ của màn này về để hiển thị lên UI
                AnalyticsManager.Instance.GetPlayerMissionStats(mId, stats =>
                {
                    CurrentStats = stats;
                    OnStatsUpdated?.Invoke(stats);
                });
            }

            OnLevelLoaded?.Invoke(currentLevel);
            Debug.Log($"<color=#2EA3FF>[GamePlay]</color> 🚀 Bắt đầu màn: <b>{mId}</b>");
        }

        /// <summary>
        /// Bấm nút Thắng (Win) -> Bắn event hoàn thành lên Firestore -> Hiện Panel Win.
        /// </summary>
        public void WinLevel()
        {
            if (state != LevelState.Playing) return;

            state = LevelState.Won;
            string mId = MissionId;
            float finalTime = playTime;

            lastStatusMessage = $"🎉 CHIẾN THẮNG {mId} ({finalTime:F1}s)";

            // Bắn event thắng lên Firebase
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogCompleteMission(mId, finalTime);

                // Cập nhật lại stats sau khi ghi
                AnalyticsManager.Instance.GetPlayerMissionStats(mId, stats =>
                {
                    CurrentStats = stats;
                    OnStatsUpdated?.Invoke(stats);
                });
            }

            OnLevelWon?.Invoke(finalTime);
            Debug.Log($"<color=#4EFC85>[GamePlay]</color> 🏆 Hoàn thành: <b>{mId}</b> - Thời gian: {finalTime:F1}s");
        }

        /// <summary>
        /// Bấm nút Thua (Lose) -> Bắn event thất bại lên Firestore -> Hiện Panel Lose.
        /// </summary>
        public void LoseLevel()
        {
            if (state != LevelState.Playing) return;

            state = LevelState.Lost;
            string mId = MissionId;
            float finalTime = playTime;

            lastStatusMessage = $"💀 THẤT BẠI {mId} ({finalTime:F1}s)";

            // Bắn event thua lên Firebase
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogFailMission(mId, finalTime);

                // Cập nhật lại stats sau khi ghi
                AnalyticsManager.Instance.GetPlayerMissionStats(mId, stats =>
                {
                    CurrentStats = stats;
                    OnStatsUpdated?.Invoke(stats);
                });
            }

            OnLevelLost?.Invoke(finalTime);
            Debug.Log($"<color=#FF5252>[GamePlay]</color> ❌ Thất bại: <b>{mId}</b> - Thời gian: {finalTime:F1}s");
        }

        /// <summary>
        /// Chuyển sang màn tiếp theo (Level + 1).
        /// </summary>
        public void NextLevel()
        {
            LoadLevel(currentLevel + 1);
        }

        /// <summary>
        /// Chơi lại màn hiện tại -> Bắn event retry lên Firestore.
        /// </summary>
        public void RestartLevel()
        {
            string mId = MissionId;
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogRetryMission(mId);
            }

            OnLevelRestarted?.Invoke(currentLevel);
            LoadLevel(currentLevel);
        }

        /// <summary>
        /// Thoát màn chơi giữa chừng.
        /// </summary>
        public void QuitLevel()
        {
            if (state == LevelState.Playing)
            {
                string mId = MissionId;
                if (AnalyticsManager.Instance != null)
                {
                    AnalyticsManager.Instance.LogQuitMission(mId, playTime);
                }
            }

            state = LevelState.NotStarted;
            playTime = 0f;
            lastStatusMessage = "Đã thoát màn chơi";
        }
    }
}
