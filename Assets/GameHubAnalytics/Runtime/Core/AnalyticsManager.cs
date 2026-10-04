using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameHub.Analytics
{
    /// <summary>
    /// Singleton chính của GameHub Analytics Package.
    /// 
    /// Cách dùng:
    ///   AnalyticsManager.Instance.LogStartMission("level_01");
    ///   AnalyticsManager.Instance.LogCompleteMission("level_01", 45.5f);
    /// 
    /// Config được tự động load từ Resources/AnalyticsConfig.asset
    /// </summary>
    [DisallowMultipleComponent]
    public class AnalyticsManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────
        //  Fields
        // ─────────────────────────────────────────────────────────

        private AnalyticsConfig   _config;
        private FirestoreClient   _client;
        private EventQueue        _queue;
        private PlayerDataManager _playerData;
        private string            _currentPlayerId;
        private bool             _initialized;
        private Coroutine        _retryCoroutine;

        // ─────────────────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────────────────

        /// <summary>Fired mỗi khi một event được log (trước khi gửi lên Firebase).</summary>
        public static event Action<MissionEvent> OnEventLogged;

        /// <summary>Fired khi trạng thái kết nối Firebase thay đổi.</summary>
        public static event Action<bool> OnConnectionStatusChanged;

        // ─────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────

        private static void CreateInstance()
        {
            var go = new GameObject("[GameHub Analytics]");
            _instance = go.AddComponent<AnalyticsManager>();
            _instance.AutoInitialize();
        }

        /// <summary>Tự động load AnalyticsConfig từ Resources và khởi tạo.</summary>
        private void AutoInitialize()
        {
            var config = Resources.Load<AnalyticsConfig>("AnalyticsConfig");
            if (config == null)
            {
                Debug.LogWarning("[Analytics] Không tìm thấy 'AnalyticsConfig' trong Resources.\n" +
                                 "Tạo file: Right-click > Create > GameHub > Analytics Config\n" +
                                 "Đặt vào: Assets/Resources/AnalyticsConfig.asset");
                return;
            }
            Initialize(config);
        }

        /// <summary>
        /// Khởi tạo analytics với config từ Resources.
        /// Gọi hàm này nếu bạn muốn khởi tạo sớm hoặc override gameId.
        /// </summary>
        /// <param name="gameIdOverride">Override gameId trong config (tuỳ chọn)</param>
        public void Initialize(string gameIdOverride = null)
        {
            var config = Resources.Load<AnalyticsConfig>("AnalyticsConfig");
            if (config == null)
            {
                Debug.LogError("[Analytics] Không tìm thấy AnalyticsConfig trong Resources.");
                return;
            }
            if (!string.IsNullOrEmpty(gameIdOverride))
                config.gameId = gameIdOverride;

            Initialize(config);
        }

        /// <summary>Khởi tạo analytics với config tùy chỉnh.</summary>
        public void Initialize(AnalyticsConfig config)
        {
            if (_initialized)
            {
                Debug.LogWarning("[Analytics] Đã khởi tạo rồi, bỏ qua.");
                return;
            }

            _config          = config;
            _client          = new FirestoreClient(config);
            _queue           = new EventQueue(config);
            _currentPlayerId = LoadOrCreatePlayerId();
            _playerData      = new PlayerDataManager(config, _currentPlayerId);
            _initialized     = true;

            Debug.Log($"[Analytics] ✓ Initialized | game={config.gameId} | " +
                      $"player={_currentPlayerId} | session={SessionManager.SessionId}");

            // Upsert player profile lên Firestore (async, không block)
            StartCoroutine(_playerData.UpsertProfile());

            // Nếu có events đang chờ trong queue → bắt đầu retry
            if (_queue.HasPending)
            {
                Debug.Log($"[Analytics] Found {_queue.Count} pending event(s). Starting retry loop...");
                StartRetryLoop();
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Public API — Mission Events
        // ─────────────────────────────────────────────────────────

        /// <summary>Log sự kiện bắt đầu mission.</summary>
        public void LogStartMission(string missionId, int retryCount = 0)
        {
            var evt = CreateEvent("start_mission", missionId);
            evt.retryCount = retryCount;
            DispatchEvent(evt);

            // Tăng counter started cho player này
            if (_playerData != null)
                StartCoroutine(_playerData.IncrementStarted(missionId));
        }

        /// <summary>Log sự kiện hoàn thành mission thành công.</summary>
        public void LogCompleteMission(string missionId, float playTime,
                                       Dictionary<string, object> extraData = null)
        {
            // Nếu bật chế độ chỉ gửi trên mobile/giả lập và đang chạy trong Unity Editor -> Bỏ qua gửi Firebase
            if (_config != null && _config.mobileOnlyForWinLose && Application.isEditor)
            {
                if (_config.enableDebugLog)
                    Debug.Log($"<color=#FF9800>[Analytics]</color> ℹ️ [Platform Filter] Bỏ qua gửi sự kiện THẮNG (Complete) '{missionId}' lên Firebase vì đang chạy trong Unity Editor (chỉ gửi trên thiết bị mobile thật hoặc máy giả lập).");
                return;
            }

            var evt = CreateEvent("complete_mission", missionId);
            evt.playTime = playTime;
            if (extraData != null) evt.customData = extraData;
            DispatchEvent(evt);

            // Tăng counter completed + cập nhật bestTime nếu là record
            if (_playerData != null)
                StartCoroutine(_playerData.IncrementCompleted(missionId, playTime));
        }

        /// <summary>Log sự kiện thất bại mission.</summary>
        public void LogFailMission(string missionId, float playTime,
                                   Dictionary<string, object> extraData = null)
        {
            // Nếu bật chế độ chỉ gửi trên mobile/giả lập và đang chạy trong Unity Editor -> Bỏ qua gửi Firebase
            if (_config != null && _config.mobileOnlyForWinLose && Application.isEditor)
            {
                if (_config.enableDebugLog)
                    Debug.Log($"<color=#FF9800>[Analytics]</color> ℹ️ [Platform Filter] Bỏ qua gửi sự kiện THUA (Fail) '{missionId}' lên Firebase vì đang chạy trong Unity Editor (chỉ gửi trên thiết bị mobile thật hoặc máy giả lập).");
                return;
            }

            var evt = CreateEvent("fail_mission", missionId);
            evt.playTime = playTime;
            if (extraData != null) evt.customData = extraData;
            DispatchEvent(evt);

            // Tăng counter failed + cộng totalPlayTime
            if (_playerData != null)
                StartCoroutine(_playerData.IncrementFailed(missionId, playTime));
        }

        /// <summary>Log sự kiện chơi lại mission.</summary>
        public void LogRetryMission(string missionId)
            => DispatchEvent(CreateEvent("retry_mission", missionId));

        /// <summary>Log sự kiện thoát mission giữa chừng.</summary>
        public void LogQuitMission(string missionId, float playTime)
        {
            var evt = CreateEvent("quit_mission", missionId);
            evt.playTime = playTime;
            DispatchEvent(evt);
        }

        // ─────────────────────────────────────────────────────────
        //  Public API — Custom Events
        // ─────────────────────────────────────────────────────────

        /// <summary>Log sự kiện tùy chỉnh với data mở rộng.</summary>
        public void LogCustomEvent(string eventName,
                                   string missionId = "global",
                                   Dictionary<string, object> customData = null)
        {
            var evt = CreateEvent(eventName, missionId);
            if (customData != null) evt.customData = customData;
            DispatchEvent(evt);
        }

        // ─────────────────────────────────────────────────────────
        //  Public API — Player & Session
        // ─────────────────────────────────────────────────────────

        /// <summary>Đặt Player ID tùy chỉnh (override auto-generated ID).</summary>
        public void SetPlayerId(string playerId)
        {
            _currentPlayerId = playerId;
            PlayerPrefs.SetString("GH_Analytics_PlayerId", playerId);
            PlayerPrefs.Save();
            Debug.Log($"[Analytics] Player ID set: {playerId}");
        }

        /// <summary>Lấy Player ID hiện tại.</summary>
        public string GetPlayerId() => _currentPlayerId;

        /// <summary>Lấy Session ID hiện tại.</summary>
        public string GetSessionId() => SessionManager.SessionId;

        /// <summary>Lấy thống kê session hiện tại.</summary>
        public (float duration, int eventCount) GetSessionStats()
            => (SessionManager.SessionDuration, SessionManager.TotalEventsLogged);

        // ─────────────────────────────────────────────────────────
        //  Public API — Utilities
        // ─────────────────────────────────────────────────────────

        /// <summary>Kiểm tra kết nối Firebase.</summary>
        public void TestConnection(Action<bool, string> onComplete)
        {
            if (!CheckInitialized()) return;
            StartCoroutine(_client.TestConnection((success, msg) =>
            {
                OnConnectionStatusChanged?.Invoke(success);
                onComplete?.Invoke(success, msg);
            }));
        }

        // ─────────────────────────────────────────────────────────
        //  Public API — Player Stats
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Đọc thống kê của player hiện tại cho một mission từ Firestore.
        /// Kết quả được cache, lần sau gọi sẽ trả về ngay.
        /// </summary>
        public void GetPlayerMissionStats(string missionId, Action<PlayerMissionStats> onComplete)
        {
            if (!CheckInitialized()) { onComplete?.Invoke(null); return; }
            StartCoroutine(_playerData.GetMissionStats(missionId, onComplete));
        }

        /// <summary>
        /// Đọc stats từ local cache (không tốn network).
        /// Trả về null nếu chưa fetch lần nào.
        /// </summary>
        public PlayerMissionStats GetCachedMissionStats(string missionId)
        {
            return _playerData?.GetCachedStats(missionId);
        }

        /// <summary>
        /// Xóa cache stats để lần sau sẽ fetch mới từ Firestore.
        /// </summary>
        public void RefreshPlayerStats(string missionId = null)
        {
            _playerData?.InvalidateCache(missionId);
        }

        /// <summary>Xóa toàn bộ event queue đang chờ.</summary>
        public void ClearQueue() => _queue?.Clear();

        /// <summary>Số event đang chờ trong offline queue.</summary>
        public int PendingQueueCount => _queue?.Count ?? 0;

        /// <summary>Cấu hình AnalyticsConfig đang được sử dụng.</summary>
        public AnalyticsConfig Config => _config;

        /// <summary>Kiểm tra xem AnalyticsManager đã được khởi tạo thành công chưa.</summary>
        public bool IsInitialized => _initialized;

        /// <summary>Bật/tắt analytics trong runtime.</summary>
        public void SetEnabled(bool enabled)
        {
            if (_config != null) _config.isEnabled = enabled;
        }

        // ─────────────────────────────────────────────────────────
        //  Private — Event Dispatch
        // ─────────────────────────────────────────────────────────

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
                Debug.Log("[Analytics] Analytics tắt (isEnabled=false). Event bị bỏ qua.");
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
                // Ghi vào offline queue để retry sau
                _queue.Enqueue(evt);
                StartRetryLoop();
                OnConnectionStatusChanged?.Invoke(false);
            }
            else
            {
                OnConnectionStatusChanged?.Invoke(true);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Private — Retry Loop
        // ─────────────────────────────────────────────────────────

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

                int batchSize = _queue.Count; // chỉ retry số event đang có, không retry mãi

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
                        // Lỗi → dừng batch này, đợi đến interval tiếp theo
                        Debug.Log("[Analytics] Retry failed. Sẽ thử lại sau.");
                        break;
                    }
                }
            }

            _retryCoroutine = null;
            if (!_queue.HasPending)
                Debug.Log("[Analytics] Queue cleared. All events sent.");
        }

        // ─────────────────────────────────────────────────────────
        //  Private — Helpers
        // ─────────────────────────────────────────────────────────

        private string LoadOrCreatePlayerId()
        {
            string saved = PlayerPrefs.GetString("GH_Analytics_PlayerId", "");
            if (!string.IsNullOrEmpty(saved)) return saved;

            // Auto-generate dựa trên device unique ID (8 ký tự đầu)
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            string newId    = "player_" + (deviceId.Length >= 8 ? deviceId.Substring(0, 8) : deviceId);

            PlayerPrefs.SetString("GH_Analytics_PlayerId", newId);
            PlayerPrefs.Save();
            return newId;
        }

        private bool CheckInitialized()
        {
            if (_initialized) return true;
            Debug.LogWarning("[Analytics] AnalyticsManager chưa khởi tạo. Gọi Initialize() trước.");
            return false;
        }
    }
}
