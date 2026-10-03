using System;

namespace GameHub.Analytics
{
    /// <summary>
    /// Thống kê gameplay của một player cho một mission cụ thể.
    /// Được lưu tại: Firestore > players/{playerId}/missions/{missionId}
    /// </summary>
    [Serializable]
    public class PlayerMissionStats
    {
        // ─────────────────────────────────────────────────────────
        //  Firestore Fields
        // ─────────────────────────────────────────────────────────

        /// <summary>Tổng số lần bắt đầu mission</summary>
        public int started;

        /// <summary>Tổng số lần hoàn thành thành công</summary>
        public int completed;

        /// <summary>Tổng số lần thất bại</summary>
        public int failed;

        /// <summary>Thời gian hoàn thành nhanh nhất (giây)</summary>
        public float bestTime;

        /// <summary>Tổng thời gian chơi mission này (giây)</summary>
        public float totalPlayTime;

        /// <summary>Lần chơi gần nhất (ISO 8601)</summary>
        public string lastPlayed;

        // ─────────────────────────────────────────────────────────
        //  Computed Properties
        // ─────────────────────────────────────────────────────────

        /// <summary>Tỷ lệ thắng (%) = completed / started * 100</summary>
        public float WinRate => started > 0 ? (float)completed / started * 100f : 0f;

        /// <summary>Tỷ lệ hoàn thành (%) giống WinRate cho mission-based game</summary>
        public float CompletedRate => WinRate;

        /// <summary>Tổng số lần thử (started)</summary>
        public int TotalAttempts => started;

        // ─────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────

        public override string ToString()
        {
            return $"[Started={started} | Completed={completed} | Failed={failed} | " +
                   $"WinRate={WinRate:F1}% | BestTime={bestTime:F1}s]";
        }
    }

    /// <summary>
    /// Thông tin profile của một player.
    /// Được lưu tại: Firestore > players/{playerId}
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        /// <summary>Player ID (từ device hardware)</summary>
        public string playerId;

        /// <summary>Nền tảng: Android, iOS, Windows_Editor, v.v.</summary>
        public string platform;

        /// <summary>Mô hình thiết bị (device model)</summary>
        public string deviceModel;

        /// <summary>Hệ điều hành</summary>
        public string osVersion;

        /// <summary>Lần đầu tiên mở game (ISO 8601)</summary>
        public string firstSeen;

        /// <summary>Lần cuối cùng mở game (ISO 8601)</summary>
        public string lastSeen;

        /// <summary>Tổng số session đã chơi</summary>
        public int totalSessions;
    }
}
