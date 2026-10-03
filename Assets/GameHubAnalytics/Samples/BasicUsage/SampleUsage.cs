using System.Collections.Generic;
using UnityEngine;
using GameHub.Analytics;

/// <summary>
/// Demo cách tích hợp GameHub Analytics vào game.
/// 
/// Hướng dẫn:
///   1. Đảm bảo đã có file Resources/AnalyticsConfig.asset với API Key và Project ID
///   2. Gắn script này vào một GameObject trong scene
///   3. Nhấn Play và dùng Context Menu (right-click component) để gửi events
/// </summary>
public class SampleUsage : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────

    [Header("Test Settings")]
    [Tooltip("ID của mission để test")]
    public string testMissionId = "level_01";

    [Header("Debug Info (readonly)")]
    [SerializeField] private string _currentPlayerId;
    [SerializeField] private string _currentSessionId;

    // ─────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────

    private float _missionStartTime;
    private bool  _missionActive;

    // ─────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        // Khởi tạo analytics — tự load config từ Resources/AnalyticsConfig
        AnalyticsManager.Instance.Initialize();

        // Lấy IDs để hiển thị trong Inspector
        _currentPlayerId  = AnalyticsManager.Instance.GetPlayerId();
        _currentSessionId = AnalyticsManager.Instance.GetSessionId();

        // Lắng nghe event callbacks
        AnalyticsManager.OnEventLogged             += OnEventLogged;
        AnalyticsManager.OnConnectionStatusChanged += OnConnectionStatusChanged;

        // ── Device Info ─────────────────────────────────────────
        Debug.Log($"[Sample] ✅ Analytics ready");
        Debug.Log($"[Sample] 📱 Device ID (Player ID): {_currentPlayerId}");
        Debug.Log($"[Sample] 🔌 Device Model: {SystemInfo.deviceModel}");
        Debug.Log($"[Sample] 💻 OS: {SystemInfo.operatingSystem}");
        Debug.Log($"[Sample] 🔑 Session: {_currentSessionId}");
        Debug.Log("[Sample] Right-click component trong Inspector để test các events.");

        // Tự động load stats của player cho mission này khi Start
        LoadMyStats();
    }


    private void OnDestroy()
    {
        AnalyticsManager.OnEventLogged             -= OnEventLogged;
        AnalyticsManager.OnConnectionStatusChanged -= OnConnectionStatusChanged;
    }

    // ─────────────────────────────────────────────────────────────
    //  Test Methods (Right-click trong Inspector để gọi)
    // ─────────────────────────────────────────────────────────────

    [ContextMenu("▶  Start Mission")]
    public void StartMission()
    {
        _missionStartTime = Time.realtimeSinceStartup;
        _missionActive    = true;

        AnalyticsManager.Instance.LogStartMission(testMissionId);
        Debug.Log($"[Sample] Mission started: {testMissionId}");
    }

    [ContextMenu("✅  Complete Mission")]
    public void CompleteMission()
    {
        if (!CheckMissionActive()) return;

        float playTime = Time.realtimeSinceStartup - _missionStartTime;
        _missionActive = false;

        AnalyticsManager.Instance.LogCompleteMission(testMissionId, playTime,
            new Dictionary<string, object>
            {
                { "stars",  3       },
                { "score",  9500    },
                { "is_perfect", false }
            });

        Debug.Log($"[Sample] Mission completed! Play time: {playTime:F2}s");
    }

    [ContextMenu("❌  Fail Mission")]
    public void FailMission()
    {
        if (!CheckMissionActive()) return;

        float playTime = Time.realtimeSinceStartup - _missionStartTime;
        _missionActive = false;

        AnalyticsManager.Instance.LogFailMission(testMissionId, playTime,
            new Dictionary<string, object>
            {
                { "death_cause", "enemy" },
                { "health_remaining", 0  }
            });

        Debug.Log($"[Sample] Mission failed. Play time: {playTime:F2}s");
    }

    [ContextMenu("🔄  Retry Mission")]
    public void RetryMission()
    {
        AnalyticsManager.Instance.LogRetryMission(testMissionId);
        Debug.Log("[Sample] Mission retry logged.");
        StartMission(); // Bắt đầu lại timer
    }

    [ContextMenu("🚪  Quit Mission")]
    public void QuitMission()
    {
        if (!CheckMissionActive()) return;

        float playTime = Time.realtimeSinceStartup - _missionStartTime;
        _missionActive = false;

        AnalyticsManager.Instance.LogQuitMission(testMissionId, playTime);
        Debug.Log($"[Sample] Mission quit at {playTime:F2}s");
    }

    [ContextMenu("🎯  Log Custom Event (Boss Defeated)")]
    public void LogCustomEvent()
    {
        AnalyticsManager.Instance.LogCustomEvent(
            "boss_defeated",
            testMissionId,
            new Dictionary<string, object>
            {
                { "boss_name",   "Dragon King" },
                { "player_lvl",  12            },
                { "kill_time",   87.5f         },
                { "no_hit",      false         }
            });
        Debug.Log("[Sample] Custom event 'boss_defeated' logged.");
    }

    [ContextMenu("🔗  Test Firebase Connection")]
    public void TestConnection()
    {
        Debug.Log("[Sample] Testing Firebase connection...");
        AnalyticsManager.Instance.TestConnection((success, message) =>
        {
            string icon = success ? "✅" : "❌";
            Debug.Log($"[Sample] {icon} {message}");
        });
    }

    [ContextMenu("📋  Print Session Stats")]
    public void PrintSessionStats()
    {
        var (duration, count) = AnalyticsManager.Instance.GetSessionStats();
        Debug.Log($"[Sample] Session Stats:\n" +
                  $"  Duration:  {duration:F1}s\n" +
                  $"  Events:    {count}\n" +
                  $"  Queued:    {AnalyticsManager.Instance.PendingQueueCount}\n" +
                  $"  PlayerID:  {AnalyticsManager.Instance.GetPlayerId()}\n" +
                  $"  SessionID: {AnalyticsManager.Instance.GetSessionId()}");
    }

    [ContextMenu("📊  Get My Mission Stats")]
    public void GetMyMissionStats()
    {
        Debug.Log($"[Sample] Fetching stats for player={AnalyticsManager.Instance.GetPlayerId()} | mission={testMissionId}...");

        AnalyticsManager.Instance.GetPlayerMissionStats(testMissionId, stats =>
        {
            if (stats == null)
            {
                Debug.LogWarning("[Sample] Không lấy được stats (kiểm tra kết nối và config).");
                return;
            }

            Debug.Log($"[Sample] 📊 Stats cho mission '{testMissionId}':\n" +
                      $"  ├─ Started:       {stats.started}\n" +
                      $"  ├─ Completed:     {stats.completed}\n" +
                      $"  ├─ Failed:        {stats.failed}\n" +
                      $"  ├─ Win Rate:      {stats.WinRate:F1}%\n" +
                      $"  ├─ Best Time:     {stats.bestTime:F1}s\n" +
                      $"  └─ Total Time:    {stats.totalPlayTime:F1}s");
        });
    }

    /// <summary>Load stats ngay khi Start — dùng cache cho lần sau.</summary>
    private void LoadMyStats()
    {
        AnalyticsManager.Instance.GetPlayerMissionStats(testMissionId, stats =>
        {
            if (stats == null) return;
            Debug.Log($"[Sample] 📥 Stats loaded for '{testMissionId}': {stats}");
        });
    }

    // ─────────────────────────────────────────────────────────────
    //  Event Callbacks
    // ─────────────────────────────────────────────────────────────

    private void OnEventLogged(MissionEvent evt)
    {
        Debug.Log($"[Sample] 📊 Event → type={evt.eventType} | " +
                  $"mission={evt.missionId} | platform={evt.platform}");

        // Update inspector display
        _currentSessionId = evt.sessionId;
    }

    private void OnConnectionStatusChanged(bool connected)
    {
        Debug.Log($"[Sample] 🔗 Firebase connection: {(connected ? "✅ Online" : "❌ Offline")}");
    }

    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    private bool CheckMissionActive()
    {
        if (_missionActive) return true;
        Debug.LogWarning("[Sample] Mission chưa bắt đầu! Gọi 'Start Mission' trước.");
        return false;
    }
}
