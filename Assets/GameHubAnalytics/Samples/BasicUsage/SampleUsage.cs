using System.Collections.Generic;
using UnityEngine;
using GameHub.Analytics;

public class SampleUsage : MonoBehaviour
{
    [Header("Test Settings")]
    [Tooltip("Mission ID for testing")]
    public string testMissionId = "level_01";

    [Header("Debug Info (readonly)")]
    [SerializeField] private string _currentPlayerId;
    [SerializeField] private string _currentSessionId;

    private float _missionStartTime;
    private bool  _missionActive;

    private void Start()
    {
        AnalyticsManager.Instance.Initialize();

        _currentPlayerId  = AnalyticsManager.Instance.GetPlayerId();
        _currentSessionId = AnalyticsManager.Instance.GetSessionId();

        AnalyticsManager.OnEventLogged             += OnEventLogged;
        AnalyticsManager.OnConnectionStatusChanged += OnConnectionStatusChanged;

        Debug.Log("[Sample] Analytics ready");
        Debug.Log($"[Sample] Device ID (Player ID): {_currentPlayerId}");
        Debug.Log($"[Sample] Device Model: {SystemInfo.deviceModel}");
        Debug.Log($"[Sample] OS: {SystemInfo.operatingSystem}");
        Debug.Log($"[Sample] Session: {_currentSessionId}");

        LoadMyStats();
    }

    private void OnDestroy()
    {
        AnalyticsManager.OnEventLogged             -= OnEventLogged;
        AnalyticsManager.OnConnectionStatusChanged -= OnConnectionStatusChanged;
    }

    [ContextMenu("Start Mission")]
    public void StartMission()
    {
        _missionStartTime = Time.realtimeSinceStartup;
        _missionActive    = true;

        AnalyticsManager.Instance.LogStartMission(testMissionId);
        Debug.Log($"[Sample] Mission started: {testMissionId}");
    }

    [ContextMenu("Complete Mission")]
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

    [ContextMenu("Fail Mission")]
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

    [ContextMenu("Retry Mission")]
    public void RetryMission()
    {
        AnalyticsManager.Instance.LogRetryMission(testMissionId);
        Debug.Log("[Sample] Mission retry logged.");
        StartMission();
    }

    [ContextMenu("Quit Mission")]
    public void QuitMission()
    {
        if (!CheckMissionActive()) return;

        float playTime = Time.realtimeSinceStartup - _missionStartTime;
        _missionActive = false;

        AnalyticsManager.Instance.LogQuitMission(testMissionId, playTime);
        Debug.Log($"[Sample] Mission quit at {playTime:F2}s");
    }

    [ContextMenu("Log Custom Event (Boss Defeated)")]
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

    [ContextMenu("Test Firebase Connection")]
    public void TestConnection()
    {
        Debug.Log("[Sample] Testing Firebase connection...");
        AnalyticsManager.Instance.TestConnection((success, message) =>
        {
            string icon = success ? "✅" : "❌";
            Debug.Log($"[Sample] {icon} {message}");
        });
    }

    [ContextMenu("Print Session Stats")]
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

    [ContextMenu("Get My Mission Stats")]
    public void GetMyMissionStats()
    {
        Debug.Log($"[Sample] Fetching stats for player={AnalyticsManager.Instance.GetPlayerId()} | mission={testMissionId}...");

        AnalyticsManager.Instance.GetPlayerMissionStats(testMissionId, stats =>
        {
            if (stats == null)
            {
                Debug.LogWarning("[Sample] Failed to fetch stats. Check connection and config.");
                return;
            }

            Debug.Log($"[Sample] Stats for mission '{testMissionId}':\n" +
                      $"  Started:       {stats.started}\n" +
                      $"  Completed:     {stats.completed}\n" +
                      $"  Failed:        {stats.failed}\n" +
                      $"  Win Rate:      {stats.WinRate:F1}%\n" +
                      $"  Best Time:     {stats.bestTime:F1}s\n" +
                      $"  Total Time:    {stats.totalPlayTime:F1}s");
        });
    }

    private void LoadMyStats()
    {
        AnalyticsManager.Instance.GetPlayerMissionStats(testMissionId, stats =>
        {
            if (stats == null) return;
            Debug.Log($"[Sample] Stats loaded for '{testMissionId}': {stats}");
        });
    }

    private void OnEventLogged(MissionEvent evt)
    {
        Debug.Log($"[Sample] Event → type={evt.eventType} | mission={evt.missionId} | platform={evt.platform}");
        _currentSessionId = evt.sessionId;
    }

    private void OnConnectionStatusChanged(bool connected)
    {
        Debug.Log($"[Sample] Firebase connection: {(connected ? "Online" : "Offline")}");
    }

    private bool CheckMissionActive()
    {
        if (_missionActive) return true;
        Debug.LogWarning("[Sample] Mission not started. Call StartMission first.");
        return false;
    }
}
