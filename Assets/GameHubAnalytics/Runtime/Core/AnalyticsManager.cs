using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameHub.Analytics
{
    [DisallowMultipleComponent]
    public class AnalyticsManager : MonoBehaviour
    {
        private static AnalyticsManager _instance;

        public static AnalyticsManager Instance
        {
            get
            {
                if (_instance == null)
                    CreateInstance();
                return _instance;
            }
        }

        private AnalyticsConfig   _config;
        private FirestoreClient   _client;
        private EventQueue        _queue;
        private PlayerDataManager _playerData;
        private string            _currentPlayerId;
        private bool             _initialized;
        private Coroutine        _retryCoroutine;

        public static event Action<MissionEvent> OnEventLogged;
        public static event Action<bool> OnConnectionStatusChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_retryCoroutine != null)
                StopCoroutine(_retryCoroutine);
            if (_instance == this)
                _instance = null;
        }

        private static void CreateInstance()
        {
            var go = new GameObject("[GameHub Analytics]");
            _instance = go.AddComponent<AnalyticsManager>();
            _instance.AutoInitialize();
        }

        private void AutoInitialize()
        {
            var config = Resources.Load<AnalyticsConfig>("AnalyticsConfig");
            if (config == null)
            {
                Debug.LogWarning("[Analytics] 'AnalyticsConfig' not found in Resources folder.");
                return;
            }
            Initialize(config);
        }

        public void Initialize(string gameIdOverride = null)
        {
            var config = Resources.Load<AnalyticsConfig>("AnalyticsConfig");
            if (config == null)
            {
                Debug.LogError("[Analytics] AnalyticsConfig not found in Resources.");
                return;
            }
            if (!string.IsNullOrEmpty(gameIdOverride))
                config.gameId = gameIdOverride;

            Initialize(config);
        }

        public void Initialize(AnalyticsConfig config)
        {
            if (_initialized)
            {
                Debug.LogWarning("[Analytics] Already initialized.");
                return;
            }

            _config          = config;
            _client          = new FirestoreClient(config);
            _queue           = new EventQueue(config);
            _currentPlayerId = LoadOrCreatePlayerId();
            _playerData      = new PlayerDataManager(config, _currentPlayerId);
            _initialized     = true;

            Debug.Log($"[Analytics] Initialized | game={config.gameId} | player={_currentPlayerId} | session={SessionManager.SessionId}");

            if (!(_config != null && _config.disableAllInEditor && Application.isEditor))
            {
                StartCoroutine(_playerData.UpsertProfile());
            }

            if (_queue.HasPending)
            {
                Debug.Log($"[Analytics] Found {_queue.Count} pending event(s). Starting retry loop...");
                StartRetryLoop();
            }
        }

        public void LogStartMission(string missionId, int retryCount = 0)
        {
            if (_config != null && (_config.mobileOnlyForMissionEvents || _config.disableAllInEditor) && Application.isEditor)
            {
                if (_config.enableDebugLog)
                    Debug.Log($"<color=#FF9800>[Analytics]</color> [Platform Filter] Skipping start mission '{missionId}' event in Editor.");
                return;
            }

            var evt = CreateEvent("start_mission", missionId);
            evt.retryCount = retryCount;
            DispatchEvent(evt);

            if (_playerData != null)
                StartCoroutine(_playerData.IncrementStarted(missionId));
        }

        public void LogCompleteMission(string missionId, float playTime,
                                       Dictionary<string, object> extraData = null)
        {
            if (_config != null && (_config.mobileOnlyForWinLose || _config.mobileOnlyForMissionEvents || _config.disableAllInEditor) && Application.isEditor)
            {
                if (_config.enableDebugLog)
                    Debug.Log($"<color=#FF9800>[Analytics]</color> [Platform Filter] Skipping complete mission '{missionId}' event in Editor.");
                return;
            }

            var evt = CreateEvent("complete_mission", missionId);
            evt.playTime = playTime;
            if (extraData != null) evt.customData = extraData;
            DispatchEvent(evt);

            if (_playerData != null)
                StartCoroutine(_playerData.IncrementCompleted(missionId, playTime));
        }

        public void LogFailMission(string missionId, float playTime,
                                   Dictionary<string, object> extraData = null)
        {
            if (_config != null && (_config.mobileOnlyForWinLose || _config.mobileOnlyForMissionEvents || _config.disableAllInEditor) && Application.isEditor)
            {
                if (_config.enableDebugLog)
                    Debug.Log($"<color=#FF9800>[Analytics]</color> [Platform Filter] Skipping fail mission '{missionId}' event in Editor.");
                return;
            }

            var evt = CreateEvent("fail_mission", missionId);
            evt.playTime = playTime;
            if (extraData != null) evt.customData = extraData;
            DispatchEvent(evt);

            if (_playerData != null)
                StartCoroutine(_playerData.IncrementFailed(missionId, playTime));
        }

        public void LogRetryMission(string missionId)
        {
            if (_config != null && (_config.mobileOnlyForMissionEvents || _config.disableAllInEditor) && Application.isEditor)
            {
                if (_config.enableDebugLog)
                    Debug.Log($"<color=#FF9800>[Analytics]</color> [Platform Filter] Skipping retry mission '{missionId}' event in Editor.");
                return;
            }
            DispatchEvent(CreateEvent("retry_mission", missionId));
        }

        public void LogQuitMission(string missionId, float playTime)
        {
            if (_config != null && (_config.mobileOnlyForMissionEvents || _config.disableAllInEditor) && Application.isEditor)
            {
                if (_config.enableDebugLog)
                    Debug.Log($"<color=#FF9800>[Analytics]</color> [Platform Filter] Skipping quit mission '{missionId}' event in Editor.");
                return;
            }
            var evt = CreateEvent("quit_mission", missionId);
            evt.playTime = playTime;
            DispatchEvent(evt);
        }

        public void LogCustomEvent(string eventName,
                                   string missionId = "global",
                                   Dictionary<string, object> customData = null)
        {
            var evt = CreateEvent(eventName, missionId);
            if (customData != null) evt.customData = customData;
            DispatchEvent(evt);
        }

        public void SetPlayerId(string playerId)
        {
            _currentPlayerId = playerId;
            PlayerPrefs.SetString("GH_Analytics_PlayerId", playerId);
            PlayerPrefs.Save();
            Debug.Log($"[Analytics] Player ID set: {playerId}");
        }

        public string GetPlayerId() => _currentPlayerId;

        public string GetSessionId() => SessionManager.SessionId;

        public (float duration, int eventCount) GetSessionStats()
            => (SessionManager.SessionDuration, SessionManager.TotalEventsLogged);

        public void TestConnection(Action<bool, string> onComplete)
        {
            if (!CheckInitialized()) return;
            StartCoroutine(_client.TestConnection((success, msg) =>
            {
                OnConnectionStatusChanged?.Invoke(success);
                onComplete?.Invoke(success, msg);
            }));
        }

        public void GetPlayerMissionStats(string missionId, Action<PlayerMissionStats> onComplete)
        {
            if (!CheckInitialized()) { onComplete?.Invoke(null); return; }
            StartCoroutine(_playerData.GetMissionStats(missionId, onComplete));
        }

        public PlayerMissionStats GetCachedMissionStats(string missionId)
        {
            return _playerData?.GetCachedStats(missionId);
        }

        public void RefreshPlayerStats(string missionId = null)
        {
            _playerData?.InvalidateCache(missionId);
        }

        public void ClearQueue() => _queue?.Clear();

        public int PendingQueueCount => _queue?.Count ?? 0;

        public AnalyticsConfig Config => _config;

        public bool IsInitialized => _initialized;

        public void SetEnabled(bool enabled)
        {
            if (_config != null) _config.isEnabled = enabled;
        }

        private MissionEvent CreateEvent(string eventType, string missionId)
        {
            var evt       = new MissionEvent(eventType, missionId);
            evt.playerId  = _currentPlayerId;
            return evt;
        }

        private void DispatchEvent(MissionEvent evt)
        {
            if (!CheckInitialized()) return;

            if (!_config.isEnabled)
            {
                Debug.Log("[Analytics] Analytics disabled (isEnabled=false). Event dropped.");
                return;
            }

            if (_config.disableAllInEditor && Application.isEditor)
            {
                if (_config.enableDebugLog)
                    Debug.Log($"<color=#FF9800>[Analytics]</color> [Platform Filter] Skipping event '{evt.eventType}' because disableAllInEditor=true in Editor.");
                return;
            }

            SessionManager.IncrementEventCount();
            OnEventLogged?.Invoke(evt);
            StartCoroutine(SendEventCoroutine(evt));
        }

        private IEnumerator SendEventCoroutine(MissionEvent evt)
        {
            bool success = false;
            yield return StartCoroutine(_client.PostEvent(evt, (ok, _) => success = ok));

            if (!success)
            {
                _queue.Enqueue(evt);
                StartRetryLoop();
                OnConnectionStatusChanged?.Invoke(false);
            }
            else
            {
                OnConnectionStatusChanged?.Invoke(true);
            }
        }

        private void StartRetryLoop()
        {
            if (_retryCoroutine == null)
                _retryCoroutine = StartCoroutine(RetryQueueCoroutine());
        }

        private IEnumerator RetryQueueCoroutine()
        {
            while (_queue.HasPending)
            {
                yield return new WaitForSeconds(_config.retryIntervalSeconds);

                if (!_queue.HasPending) break;

                Debug.Log($"[Analytics] Retrying {_queue.Count} queued event(s)...");

                int batchSize = _queue.Count;

                for (int i = 0; i < batchSize && _queue.HasPending; i++)
                {
                    if (!_queue.TryPeek(out string missionId, out string json)) break;

                    bool sent = false;
                    yield return StartCoroutine(_client.PostRawJson(missionId, json, (ok, _) => sent = ok));

                    if (sent)
                    {
                        _queue.Dequeue();
                        Debug.Log($"[Analytics] Retry OK. Remaining: {_queue.Count}");
                    }
                    else
                    {
                        Debug.Log("[Analytics] Retry failed. Retrying later.");
                        break;
                    }
                }
            }

            _retryCoroutine = null;
            if (!_queue.HasPending)
                Debug.Log("[Analytics] Queue cleared. All events sent.");
        }

        private string LoadOrCreatePlayerId()
        {
            string saved = PlayerPrefs.GetString("GH_Analytics_PlayerId", "");
            if (!string.IsNullOrEmpty(saved)) return saved;

            string deviceId = SystemInfo.deviceUniqueIdentifier;
            string newId    = "player_" + (deviceId.Length >= 8 ? deviceId.Substring(0, 8) : deviceId);

            PlayerPrefs.SetString("GH_Analytics_PlayerId", newId);
            PlayerPrefs.Save();
            return newId;
        }

        private bool CheckInitialized()
        {
            if (_initialized) return true;
            Debug.LogWarning("[Analytics] AnalyticsManager is not initialized. Call Initialize() first.");
            return false;
        }
    }
}
