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
        private bool             _enabled;
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
            _enabled         = config.isEnabled;

            Debug.Log($"[Analytics] Initialized | game={config.CleanGameId} | player={_currentPlayerId} | session={SessionManager.SessionId}");

            if (!CanSend) return;

            StartCoroutine(_playerData.UpsertProfile());

            if (_queue.HasPending)
            {
                Debug.Log($"[Analytics] Found {_queue.Count} pending event(s). Starting retry loop...");
                StartRetryLoop();
            }
        }

        public void LogStartMission(string missionId, int retryCount = 0)
        {
            if (!CanSendOrLog($"start_mission {missionId}")) return;

            var evt = CreateEvent("start_mission", missionId);
            evt.retryCount = retryCount;
            DispatchEvent(evt);

            if (_playerData != null)
                StartCoroutine(_playerData.IncrementStarted(missionId));
        }

        public void LogCompleteMission(string missionId, float playTime,
                                       Dictionary<string, object> extraData = null)
        {
            if (!CanSendOrLog($"complete_mission {missionId}")) return;

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
            if (!CanSendOrLog($"fail_mission {missionId}")) return;

            var evt = CreateEvent("fail_mission", missionId);
            evt.playTime = playTime;
            if (extraData != null) evt.customData = extraData;
            DispatchEvent(evt);

            if (_playerData != null)
                StartCoroutine(_playerData.IncrementFailed(missionId, playTime));
        }

        public void LogRetryMission(string missionId)
        {
            if (!CanSendOrLog($"retry_mission {missionId}")) return;
            DispatchEvent(CreateEvent("retry_mission", missionId));
        }

        public void LogQuitMission(string missionId, float playTime)
        {
            if (!CanSendOrLog($"quit_mission {missionId}")) return;
            var evt = CreateEvent("quit_mission", missionId);
            evt.playTime = playTime;
            DispatchEvent(evt);
        }

        public void LogCustomEvent(string eventName,
                                   string missionId = "global",
                                   Dictionary<string, object> customData = null)
        {
            if (!CanSendOrLog(eventName)) return;

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

        private const string PlayerNameKey = "GH_Analytics_PlayerName";

        public string PlayerName => PlayerPrefs.GetString(PlayerNameKey, "");

        /// <summary>Local cache first, then Firestore (playerId is device-derived, so it survives reinstall). "" = not set, null = network error.</summary>
        public void GetPlayerName(Action<string> onComplete)
        {
            if (!string.IsNullOrEmpty(PlayerName)) { onComplete?.Invoke(PlayerName); return; }
            if (!CanSend) { onComplete?.Invoke(""); return; }

            StartCoroutine(_playerData.FetchPlayerName(name =>
            {
                if (!string.IsNullOrEmpty(name))
                {
                    PlayerPrefs.SetString(PlayerNameKey, name);
                    PlayerPrefs.Save();
                }
                onComplete?.Invoke(name);
            }));
        }

        public void SetPlayerName(string playerName)
        {
            PlayerPrefs.SetString(PlayerNameKey, playerName);
            PlayerPrefs.Save();
            if (!CanSendOrLog("set_player_name")) return;
            StartCoroutine(_playerData.SavePlayerName(playerName));
        }

        public void LogRateMission(string missionId, int stars)
        {
            if (!CanSendOrLog($"rate_mission {missionId}")) return;

            stars = Mathf.Clamp(stars, 1, 5);
            LogCustomEvent("rate_mission", missionId, new Dictionary<string, object> { { "stars", stars } });

            // Also keep the latest rating on the player's mission doc so the dashboard can read it
            // (the event list is capped, the mission docs are not).
            StartCoroutine(_playerData.SaveMissionRating(missionId, stars));
        }

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
            if (!CanSend) { onComplete?.Invoke(null); return; }
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

        /// <summary>Config isEnabled (or SetEnabled). False = no Firebase and no name / rating popups.</summary>
        public bool IsEnabled => _initialized && _enabled;

        /// <summary>Show the NamePanel: enabled and config askPlayerName.</summary>
        public bool AskPlayerName => IsEnabled && _config.askPlayerName;

        /// <summary>Show the RatingPanel after a win: enabled and config askStarRating.</summary>
        public bool AskStarRating => IsEnabled && _config.askStarRating;

        /// <summary>True when Firebase is actually used: enabled and not running in the Unity Editor.</summary>
        public bool CanSend => IsEnabled && !Application.isEditor;

        /// <summary>Runtime only; does not change the AnalyticsConfig asset.</summary>
        public void SetEnabled(bool enabled) => _enabled = enabled;

        // Every Firebase write goes through here, so disabling or running in the Editor sends nothing.
        private bool CanSendOrLog(string what)
        {
            if (CanSend) return true;
            if (_config != null && _config.enableDebugLog)
                Debug.Log($"<color=#FF9800>[Analytics]</color> Skipped '{what}' ({(IsEnabled ? "Unity Editor" : "isEnabled = false")})");
            return false;
        }

        private MissionEvent CreateEvent(string eventType, string missionId)
        {
            var evt       = new MissionEvent(eventType, missionId);
            evt.playerId  = _currentPlayerId;
            return evt;
        }

        private void DispatchEvent(MissionEvent evt)
        {
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
                if (!CanSend) continue; // SetEnabled(false) at runtime: keep the queue, send nothing

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
