using System;
using UnityEngine;

namespace GameHub.Analytics
{
    /// <summary>
    /// Quản lý Session ID và thông tin session hiện tại.
    /// Session được tạo mới tự động mỗi khi khởi động game.
    /// </summary>
    public static class SessionManager
    {
        private static string _sessionId;
        private static float  _sessionStartTime;
        private static int    _totalEventsInSession;

        // ─────────────────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────────────────

        /// <summary>Session ID duy nhất (16 ký tự hex) cho lần chạy game này.</summary>
        public static string SessionId
        {
            get
            {
                if (string.IsNullOrEmpty(_sessionId))
                    InitSession();
                return _sessionId;
            }
        }

        /// <summary>Thời gian session tính từ lúc khởi tạo (giây, realtime).</summary>
        public static float SessionDuration =>
            Time.realtimeSinceStartup - _sessionStartTime;

        /// <summary>Tổng số event đã gửi trong session này.</summary>
        public static int TotalEventsLogged => _totalEventsInSession;

        // ─────────────────────────────────────────────────────────
        //  Methods
        // ─────────────────────────────────────────────────────────

        /// <summary>Reset và tạo session ID mới (dùng khi cần tạo session mới trong cùng một run).</summary>
        public static void ResetSession()
        {
            _sessionId = null;
            _totalEventsInSession = 0;
            InitSession();
        }

        /// <summary>Gọi khi một event được ghi nhận (để đếm tổng số events).</summary>
        internal static void IncrementEventCount()
        {
            _totalEventsInSession++;
        }

        // ─────────────────────────────────────────────────────────
        //  Private
        // ─────────────────────────────────────────────────────────

        private static void InitSession()
        {
            // Tạo session ID ngắn gọn: 16 ký tự hex từ GUID
            _sessionId        = Guid.NewGuid().ToString("N").Substring(0, 16);
            _sessionStartTime = Time.realtimeSinceStartup;
            _totalEventsInSession = 0;
        }
    }
}
