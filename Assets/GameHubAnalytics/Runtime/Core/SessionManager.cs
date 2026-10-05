using System;
using UnityEngine;

namespace GameHub.Analytics
{
    public static class SessionManager
    {
        private static string _sessionId;
        private static float  _sessionStartTime;
        private static int    _totalEventsInSession;

        public static string SessionId
        {
            get
            {
                if (string.IsNullOrEmpty(_sessionId))
                    InitSession();
                return _sessionId;
            }
        }

        public static float SessionDuration =>
            Time.realtimeSinceStartup - _sessionStartTime;

        public static int TotalEventsLogged => _totalEventsInSession;

        public static void ResetSession()
        {
            _sessionId = null;
            _totalEventsInSession = 0;
            InitSession();
        }

        internal static void IncrementEventCount()
        {
            _totalEventsInSession++;
        }

        private static void InitSession()
        {
            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 16);
            _sessionStartTime = Time.realtimeSinceStartup;
            _totalEventsInSession = 0;
        }
    }
}
