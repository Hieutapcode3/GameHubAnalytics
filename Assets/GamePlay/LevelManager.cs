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

    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Settings")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private bool autoStartOnAwake = true;

        [Header("Runtime State")]
        [SerializeField] private LevelState state = LevelState.NotStarted;
        [SerializeField] private float playTime = 0f;
        [SerializeField] private string lastStatusMessage = "Ready";

        public event Action<int> OnLevelLoaded;
        public event Action<float> OnLevelWon;
        public event Action<float> OnLevelLost;
        public event Action<int> OnLevelRestarted;
        public event Action<PlayerMissionStats> OnStatsUpdated;

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

        public void LoadLevel(int level)
        {
            currentLevel = Mathf.Max(1, level);
            playTime = 0f;
            state = LevelState.Playing;

            string mId = MissionId;
            bool isEditor = Application.isEditor;
            bool skipFirebase = isEditor && (AnalyticsManager.Instance?.Config != null &&
                (AnalyticsManager.Instance.Config.mobileOnlyForMissionEvents || AnalyticsManager.Instance.Config.disableAllInEditor));

            lastStatusMessage = skipFirebase 
                ? $"Playing {mId}... [Editor: Skip Firebase]" 
                : $"Playing {mId}...";

            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogStartMission(mId);

                AnalyticsManager.Instance.GetPlayerMissionStats(mId, stats =>
                {
                    CurrentStats = stats;
                    OnStatsUpdated?.Invoke(stats);
                });
            }

            OnLevelLoaded?.Invoke(currentLevel);
        }

        public void WinLevel()
        {
            if (state != LevelState.Playing) return;

            state = LevelState.Won;
            string mId = MissionId;
            float finalTime = playTime;

            bool isEditor = Application.isEditor;
            bool mobileOnly = AnalyticsManager.Instance != null && AnalyticsManager.Instance.Config != null &&
                (AnalyticsManager.Instance.Config.mobileOnlyForWinLose || AnalyticsManager.Instance.Config.mobileOnlyForMissionEvents || AnalyticsManager.Instance.Config.disableAllInEditor);
            bool skippedFirebase = isEditor && mobileOnly;

            if (skippedFirebase)
            {
                lastStatusMessage = $"🎉 WIN {mId} ({finalTime:F1}s) [Editor: Skip Firebase]";
            }
            else
            {
                lastStatusMessage = $"🎉 WIN {mId} ({finalTime:F1}s) -> Sending to Firebase";
            }

            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogCompleteMission(mId, finalTime);

                if (!skippedFirebase)
                {
                    AnalyticsManager.Instance.GetPlayerMissionStats(mId, stats =>
                    {
                        CurrentStats = stats;
                        OnStatsUpdated?.Invoke(stats);
                    });
                }
            }

            OnLevelWon?.Invoke(finalTime);
        }

        public void LoseLevel()
        {
            if (state != LevelState.Playing) return;

            state = LevelState.Lost;
            string mId = MissionId;
            float finalTime = playTime;

            bool isEditor = Application.isEditor;
            bool mobileOnly = AnalyticsManager.Instance != null && AnalyticsManager.Instance.Config != null &&
                (AnalyticsManager.Instance.Config.mobileOnlyForWinLose || AnalyticsManager.Instance.Config.mobileOnlyForMissionEvents || AnalyticsManager.Instance.Config.disableAllInEditor);
            bool skippedFirebase = isEditor && mobileOnly;

            if (skippedFirebase)
            {
                lastStatusMessage = $"💀 LOSE {mId} ({finalTime:F1}s) [Editor: Skip Firebase]";
            }
            else
            {
                lastStatusMessage = $"💀 LOSE {mId} ({finalTime:F1}s) -> Sending to Firebase";
            }

            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.LogFailMission(mId, finalTime);

                if (!skippedFirebase)
                {
                    AnalyticsManager.Instance.GetPlayerMissionStats(mId, stats =>
                    {
                        CurrentStats = stats;
                        OnStatsUpdated?.Invoke(stats);
                    });
                }
            }

            OnLevelLost?.Invoke(finalTime);
        }

        public void NextLevel()
        {
            LoadLevel(currentLevel + 1);
        }

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
            lastStatusMessage = "Level quit";
        }
    }
}
