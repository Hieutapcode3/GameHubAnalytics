using System;

namespace GameHub.Analytics
{
    [Serializable]
    public class PlayerMissionStats
    {
        public int started;
        public int completed;
        public int failed;
        public float bestTime;
        public float totalPlayTime;
        public string lastPlayed;

        public float WinRate => (completed + failed) > 0 ? (float)completed / (completed + failed) * 100f : 0f;
        public float CompleteRate => started > 0 ? (float)completed / started * 100f : 0f;
        public float CompletedRate => CompleteRate;
        public int TotalAttempts => started;

        public override string ToString()
        {
            return $"[Started={started} | Completed={completed} | Failed={failed} | " +
                   $"WinRate={WinRate:F1}% | CompleteRate={CompleteRate:F1}% | BestTime={bestTime:F1}s]";
        }
    }

    [Serializable]
    public class PlayerProfile
    {
        public string playerId;
        public string platform;
        public string deviceModel;
        public string osVersion;
        public string firstSeen;
        public string lastSeen;
        public int totalSessions;
    }
}
