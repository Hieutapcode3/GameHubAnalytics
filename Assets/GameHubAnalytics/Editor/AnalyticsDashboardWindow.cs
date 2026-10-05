using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GameHub.Analytics.Editor
{
    public class AnalyticsDashboardWindow : EditorWindow
    {
        // ---------------------------------------------------------
        //  Menu Item
        // ---------------------------------------------------------

        [MenuItem("GameHub/Analytics/📊 Open Dashboard", priority = 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<AnalyticsDashboardWindow>("📊 Analytics Dashboard");
            window.minSize = new Vector2(720, 520);
        }

        // ---------------------------------------------------------
        //  Data Models
        // ---------------------------------------------------------

        public class MissionRow
        {
            public string missionId;
            public int    started;
            public int    completed;
            public int    failed;
            public float  totalPlayTime;
            public float  bestTime;
            public int    playerCount;
            public int    completedPlayers;

            public float WinRate             => (completed + failed) > 0 ? (float)completed / (completed + failed) * 100f : 0f;
            public float CompleteRate        => playerCount > 0 ? (float)completedPlayers / playerCount * 100f : 0f;
            public float AttemptCompleteRate => started > 0 ? (float)completed / started * 100f : 0f;
            public float AvgPlayTime         => completed > 0 ? totalPlayTime / completed : (started > 0 ? totalPlayTime / started : 0f);
        }

        public class PlayerRow
        {
            public string playerId;
            public string missionId;
            public int    started;
            public int    completed;
            public int    failed;
            public float  bestTime;
            public float  totalPlayTime;
            public string lastPlayed;

            public float WinRate      => (completed + failed) > 0 ? (float)completed / (completed + failed) * 100f : 0f;
            public float CompleteRate => started > 0 ? (float)completed / started * 100f : 0f;
        }

        public class EventRow
        {
            public string timestamp;
            public string eventType;
            public string missionId;
            public string playerId;
            public float  playTime;
            public string platform;
            public string deviceModel;
        }

        private enum TabView
        {
            Missions = 0,
            Players  = 1,
            Events   = 2
        }

        // ---------------------------------------------------------
        //  State
        // ---------------------------------------------------------

        private AnalyticsConfig _config;
        private Vector2         _scrollPos;
        private string          _statusMsg = "";
        private bool            _statusIsError;
        private bool            _isFetching;

        // View options
        private TabView _activeTab       = TabView.Missions;
        private bool    _isUsingRealData = false;
        private string  _filterText      = "";
        private int     _sortColumn      = 4; // default sort by WinRate
        private bool    _sortAsc         = false;
        private bool    _groupByPlayer   = true;
        private readonly HashSet<string> _collapsedPlayerIds = new HashSet<string>();

        // Data containers
        private readonly List<MissionRow> _missionRows = new List<MissionRow>();
        private readonly List<PlayerRow>  _playerRows  = new List<PlayerRow>();
        private readonly List<EventRow>   _eventRows   = new List<EventRow>();

        // Styles
        private bool     _stylesReady;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _tableHeaderStyle;
        private GUIStyle _tableCellStyle;
        private GUIStyle _tableCellBold;

        // ---------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------

        private void OnEnable()
        {
            LoadConfig();
            _stylesReady = false;

            if (_missionRows.Count == 0)
                LoadSampleData();
        }

        private void LoadConfig()
        {
            _config = Resources.Load<AnalyticsConfig>("AnalyticsConfig");
        }

        // ---------------------------------------------------------
        //  GUI Layout
        // ---------------------------------------------------------

        private void OnGUI()
        {
            InitStyles();

            // --------------------------------------------------------- 1. Header ---------------------------------------------------------
            DrawHeader();

            // --------------------------------------------------------- 2. Config Check ---------------------------------------------------------
            if (_config == null)
            {
                DrawNoConfigState();
                return;
            }

            // --------------------------------------------------------- 3. Main Action Toolbar ---------------------------------------------------------
            DrawMainToolbar();

            // --------------------------------------------------------- 4. Sub Toolbar (Tabs, Filter, Export) ---------------------------------------------------------
            DrawSubToolbar();

            // --------------------------------------------------------- 5. Status Notice ---------------------------------------------------------
            if (!string.IsNullOrEmpty(_statusMsg))
            {
                var msgType = _statusIsError ? MessageType.Error : MessageType.Info;
                EditorGUILayout.HelpBox(_statusMsg, msgType);
                EditorGUILayout.Space(2);
            }

            // --------------------------------------------------------- 6. Live Session Info (Play Mode) ---------------------------------------------------------
            if (Application.isPlaying && AnalyticsManager.Instance != null)
                DrawLiveSessionBar();

            // --------------------------------------------------------- 7. Data Grid Table ---------------------------------------------------------
            switch (_activeTab)
            {
                case TabView.Missions:
                    DrawMissionsTable();
                    break;
                case TabView.Players:
                    DrawPlayersTable();
                    break;
                case TabView.Events:
                    DrawEventsTable();
                    break;
            }

            // --------------------------------------------------------- 8. Footer ---------------------------------------------------------
            DrawFooter();
        }

        // ---------------------------------------------------------
        //  Header & Toolbars
        // ---------------------------------------------------------

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(0, 0, position.width, 54), new Color(0.12f, 0.13f, 0.18f));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("📊  GameHub Analytics — Firestore Dashboard", _titleStyle);

            string projInfo = _config != null && _config.IsValid
                ? $"Project: {_config.projectId}  |  Game: {_config.gameId}  |  Trạng thái: Kết nối hợp lệ"
                : "Chưa cấu hình Firebase hoặc thiếu API Key / Project ID";
            EditorGUILayout.LabelField(projInfo, _subtitleStyle);

            EditorGUILayout.Space(6);
            EditorGUILayout.EndVertical();
        }

        private void DrawMainToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Fetch Real Data button
            GUI.enabled = !_isFetching && _config != null && _config.IsValid;
            string fetchLabel = _isFetching ? "⏳ Đang tải Firestore..." : "🔄 Lấy Dữ Liệu Thật (Fetch)";
            if (GUILayout.Button(fetchLabel, EditorStyles.toolbarButton, GUILayout.Width(170)))
                FetchRealFirestoreData();
            GUI.enabled = true;

            // Sample data toggle
            if (GUILayout.Button(_isUsingRealData ? "👁️ Xem Dữ Liệu Mẫu" : "● Đang Xem Dữ Liệu Mẫu", EditorStyles.toolbarButton, GUILayout.Width(150)))
            {
                if (_isUsingRealData)
                {
                    LoadSampleData();
                    _statusMsg = "ℹ️ Đã chuyển sang chế độ xem dữ liệu mẫu thử nghiệm.";
                    _statusIsError = false;
                }
            }

            // Export to Excel (.xlsx) with multi-sheet support
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("📊 Xuất Excel (.xlsx)", EditorStyles.toolbarButton, GUILayout.Width(135)))
                ExportToExcel();
            GUI.backgroundColor = Color.white;

            // Export to CSV
            if (GUILayout.Button("📄 CSV", EditorStyles.toolbarButton, GUILayout.Width(55)))
                ExportToCsv();

            // Export to Google Sheets
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("📑 Dán vào Google Sheets", EditorStyles.toolbarButton, GUILayout.Width(160)))
                ExportToGoogleSheets();
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();

            // Link to Config & Firebase
            if (GUILayout.Button("⚙️ Config", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                Selection.activeObject = _config;
                EditorGUIUtility.PingObject(_config);
            }

            if (GUILayout.Button("🌐 Console", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                if (_config != null && !string.IsNullOrEmpty(_config.projectId))
                    Application.OpenURL($"https://console.firebase.google.com/project/{_config.projectId}/firestore");
                else
                    Application.OpenURL("https://console.firebase.google.com");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSubToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Tab Buttons
            string[] tabNames = { "🎯 Theo Màn Chơi (Missions)", "📱 Theo Người Chơi (Players)", "⚡ Lịch Sử Sự Kiện (Events)" };
            int currentTab = (int)_activeTab;
            int newTab = GUILayout.Toolbar(currentTab, tabNames, EditorStyles.toolbarButton, GUILayout.Width(450));
            if (newTab != currentTab)
            {
                _activeTab = (TabView)newTab;
                _sortColumn = _activeTab == TabView.Missions ? 4 : 0;
                _sortAsc = false;
            }

            GUILayout.FlexibleSpace();

            // Data indicator badge
            string badgeText = _isUsingRealData ? "● LIVE FIRESTORE DATA" : "● DỮ LIỆU MẪU (DEMO)";
            GUI.color = _isUsingRealData ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.75f, 0.2f);
            EditorGUILayout.LabelField(badgeText, EditorStyles.miniLabel, GUILayout.Width(150));
            GUI.color = Color.white;

            // Search filter
            EditorGUILayout.LabelField("Lọc:", GUILayout.Width(30));
            _filterText = EditorGUILayout.TextField(_filterText, EditorStyles.toolbarSearchField, GUILayout.Width(120));
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
                _filterText = "";

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void DrawLiveSessionBar()
        {
            var (duration, eventCount) = AnalyticsManager.Instance.GetSessionStats();
            string sessionId = AnalyticsManager.Instance.GetSessionId();
            string playerId  = AnalyticsManager.Instance.GetPlayerId();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.color = new Color(0.3f, 1f, 0.5f);
            EditorGUILayout.LabelField($"● PLAY MODE LIVE  |  Player: {playerId}  |  Session: {sessionId}  |  " +
                                       $"Thời gian: {duration:F0}s  |  Đã ghi: {eventCount} sự kiện", EditorStyles.miniBoldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        // ---------------------------------------------------------
        //  Tables Rendering
        // ---------------------------------------------------------

        private void DrawMissionsTable()
        {
            float[] widths = { 150f, 70f, 85f, 70f, 95f, 95f, 100f, 90f, 80f };
            string[] headers = { "Màn Chơi", "Bắt Đầu", "Vượt Qua", "Thất Bại", "Win Rate %", "Hoàn Thành %", "Thời Gian TB", "Kỷ Lục", "Người Chơi" };

            DrawTableHeader(headers, widths);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            var filtered = GetFilteredMissions();

            if (filtered.Count == 0)
            {
                DrawEmptyTableNotice("Không có dữ liệu màn chơi phù hợp với bộ lọc.");
            }
            else
            {
                bool alt = false;
                foreach (var r in filtered)
                {
                    var bg = alt ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.22f, 0.22f, 0.22f);
                    var rowRect = EditorGUILayout.BeginHorizontal();
                    EditorGUI.DrawRect(rowRect, bg);

                    EditorGUILayout.LabelField(r.missionId, _tableCellBold, GUILayout.Width(widths[0]));
                    EditorGUILayout.LabelField(r.started.ToString(), _tableCellStyle, GUILayout.Width(widths[1]));
                    EditorGUILayout.LabelField(r.completed.ToString(), _tableCellStyle, GUILayout.Width(widths[2]));
                    EditorGUILayout.LabelField(r.failed.ToString(), _tableCellStyle, GUILayout.Width(widths[3]));

                    // Win rate with color coding
                    DrawWinRateCell(r.WinRate, widths[4]);

                    EditorGUILayout.LabelField($"{r.CompleteRate:F1}%", _tableCellStyle, GUILayout.Width(widths[5]));
                    EditorGUILayout.LabelField(FormatPlayTime(r.AvgPlayTime), _tableCellStyle, GUILayout.Width(widths[6]));
                    EditorGUILayout.LabelField(r.bestTime > 0 ? FormatPlayTime(r.bestTime) : "-", _tableCellStyle, GUILayout.Width(widths[7]));
                    EditorGUILayout.LabelField(r.playerCount.ToString(), _tableCellStyle, GUILayout.Width(widths[8]));

                    EditorGUILayout.EndHorizontal();
                    alt = !alt;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPlayersTable()
        {
            var filtered = GetFilteredPlayers();

            if (filtered.Count == 0)
            {
                DrawEmptyTableNotice("Chưa có thống kê người chơi nào.");
                return;
            }

            var groups = filtered.GroupBy(x => x.playerId).ToList();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _groupByPlayer = GUILayout.Toggle(_groupByPlayer, "  📁 Tách theo từng Player ID", EditorStyles.toolbarButton, GUILayout.Width(170));
            GUILayout.Label($"|  {groups.Count} người chơi ({filtered.Count} lượt lưu)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (_groupByPlayer)
            {
                if (GUILayout.Button("Mở rộng tất cả", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    _collapsedPlayerIds.Clear();

                if (GUILayout.Button("Thu gọn tất cả", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    foreach (var g in groups)
                        _collapsedPlayerIds.Add(g.Key);
                }
            }
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_groupByPlayer)
            {
                DrawGroupedPlayersTable(groups);
            }
            else
            {
                DrawFlatPlayersTable(filtered);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGroupedPlayersTable(List<IGrouping<string, PlayerRow>> groups)
        {
            float[] widths = { 150f, 65f, 65f, 65f, 90f, 95f, 85f, 105f, 150f };
            string[] headers = { "Màn Chơi", "Bắt Đầu", "Thắng", "Thua", "Win Rate %", "Hoàn Thành %", "Kỷ Lục", "Thời Lượng", "Lần Chơi Cuối" };

            foreach (var group in groups)
            {
                string pId = group.Key;
                bool isCollapsed = _collapsedPlayerIds.Contains(pId);

                int totalMissions = group.Count();
                int totalStarted = 0;
                int totalCompleted = 0;
                int totalFailed = 0;
                float totalTime = 0f;

                foreach (var r in group)
                {
                    totalStarted += r.started;
                    totalCompleted += r.completed;
                    totalFailed += r.failed;
                    totalTime += r.totalPlayTime;
                }

                float winRate = (totalCompleted + totalFailed) > 0 ? (float)totalCompleted / (totalCompleted + totalFailed) * 100f : 0f;
                float completeRate = totalStarted > 0 ? (float)totalCompleted / totalStarted * 100f : 0f;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                string foldoutIcon = isCollapsed ? "▶" : "▼";
                if (GUILayout.Button($" {foldoutIcon}  📱 {pId}", EditorStyles.boldLabel))
                {
                    if (isCollapsed) _collapsedPlayerIds.Remove(pId);
                    else _collapsedPlayerIds.Add(pId);
                }

                GUILayout.FlexibleSpace();

                GUI.color = new Color(0.7f, 0.9f, 1f);
                GUILayout.Label($"Màn chơi: {totalMissions}", EditorStyles.miniBoldLabel);
                GUI.color = Color.white;
                GUILayout.Label("|", EditorStyles.miniLabel);

                GUI.color = winRate >= 70f ? new Color(0.4f, 1f, 0.4f) : (winRate >= 40f ? new Color(1f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f));
                GUILayout.Label($"Win: {winRate:F1}%", EditorStyles.miniBoldLabel);
                GUI.color = Color.white;
                GUILayout.Label("|", EditorStyles.miniLabel);

                GUILayout.Label($"Hoàn thành: {completeRate:F1}%", EditorStyles.miniLabel);
                GUILayout.Label("|", EditorStyles.miniLabel);

                GUILayout.Label($"Thắng {totalCompleted} / Thua {totalFailed} (Tổng: {totalStarted})", EditorStyles.miniLabel);
                GUILayout.Label("|", EditorStyles.miniLabel);

                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUILayout.Label($"Tổng thời lượng: {FormatPlayTime(totalTime)}", EditorStyles.miniBoldLabel);
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();

                if (!isCollapsed)
                {
                    EditorGUILayout.Space(2);
                    DrawTableHeader(headers, widths);

                    bool alt = false;
                    foreach (var r in group)
                    {
                        var bg = alt ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.22f, 0.22f, 0.22f);
                        var rowRect = EditorGUILayout.BeginHorizontal();
                        EditorGUI.DrawRect(rowRect, bg);

                        EditorGUILayout.LabelField(r.missionId, _tableCellBold, GUILayout.Width(widths[0]));
                        EditorGUILayout.LabelField(r.started.ToString(), _tableCellStyle, GUILayout.Width(widths[1]));
                        EditorGUILayout.LabelField(r.completed.ToString(), _tableCellStyle, GUILayout.Width(widths[2]));
                        EditorGUILayout.LabelField(r.failed.ToString(), _tableCellStyle, GUILayout.Width(widths[3]));

                        DrawWinRateCell(r.WinRate, widths[4]);
                        EditorGUILayout.LabelField($"{r.CompleteRate:F1}%", _tableCellStyle, GUILayout.Width(widths[5]));

                        EditorGUILayout.LabelField(r.bestTime > 0 ? FormatPlayTime(r.bestTime) : "-", _tableCellStyle, GUILayout.Width(widths[6]));
                        EditorGUILayout.LabelField(FormatPlayTime(r.totalPlayTime), _tableCellStyle, GUILayout.Width(widths[7]));
                        EditorGUILayout.LabelField(FormatTimestamp(r.lastPlayed), _tableCellStyle, GUILayout.Width(widths[8]));

                        EditorGUILayout.EndHorizontal();
                        alt = !alt;
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
        }

        private void DrawFlatPlayersTable(List<PlayerRow> filtered)
        {
            float[] widths = { 170f, 110f, 65f, 65f, 65f, 90f, 95f, 80f, 95f, 140f };
            string[] headers = { "Player ID (Thiết bị)", "Màn Chơi", "Bắt Đầu", "Thắng", "Thua", "Win Rate %", "Hoàn Thành %", "Kỷ Lục", "Thời Lượng", "Lần Chơi Cuối" };

            DrawTableHeader(headers, widths);

            bool alt = false;
            foreach (var r in filtered)
            {
                var bg = alt ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.22f, 0.22f, 0.22f);
                var rowRect = EditorGUILayout.BeginHorizontal();
                EditorGUI.DrawRect(rowRect, bg);

                EditorGUILayout.LabelField(r.playerId, _tableCellBold, GUILayout.Width(widths[0]));
                EditorGUILayout.LabelField(r.missionId, _tableCellStyle, GUILayout.Width(widths[1]));
                EditorGUILayout.LabelField(r.started.ToString(), _tableCellStyle, GUILayout.Width(widths[2]));
                EditorGUILayout.LabelField(r.completed.ToString(), _tableCellStyle, GUILayout.Width(widths[3]));
                EditorGUILayout.LabelField(r.failed.ToString(), _tableCellStyle, GUILayout.Width(widths[4]));

                DrawWinRateCell(r.WinRate, widths[5]);
                EditorGUILayout.LabelField($"{r.CompleteRate:F1}%", _tableCellStyle, GUILayout.Width(widths[6]));

                EditorGUILayout.LabelField(r.bestTime > 0 ? FormatPlayTime(r.bestTime) : "-", _tableCellStyle, GUILayout.Width(widths[7]));
                EditorGUILayout.LabelField(FormatPlayTime(r.totalPlayTime), _tableCellStyle, GUILayout.Width(widths[8]));
                EditorGUILayout.LabelField(FormatTimestamp(r.lastPlayed), _tableCellStyle, GUILayout.Width(widths[9]));

                EditorGUILayout.EndHorizontal();
                alt = !alt;
            }
        }

        private void DrawEventsTable()
        {
            float[] widths = { 150f, 130f, 110f, 160f, 85f, 85f, 130f };
            string[] headers = { "Thời Gian", "Sự Kiện", "Màn Chơi", "Player ID", "Thời Lượng", "Nền Tảng", "Thiết Bị" };

            DrawTableHeader(headers, widths);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            var filtered = GetFilteredEvents();

            if (filtered.Count == 0)
            {
                DrawEmptyTableNotice("Chưa có log sự kiện nào được ghi nhận.");
            }
            else
            {
                bool alt = false;
                foreach (var r in filtered)
                {
                    var bg = alt ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.22f, 0.22f, 0.22f);
                    var rowRect = EditorGUILayout.BeginHorizontal();
                    EditorGUI.DrawRect(rowRect, bg);

                    EditorGUILayout.LabelField(FormatTimestamp(r.timestamp), _tableCellStyle, GUILayout.Width(widths[0]));
                    EditorGUILayout.LabelField(r.eventType, _tableCellBold, GUILayout.Width(widths[1]));
                    EditorGUILayout.LabelField(r.missionId, _tableCellStyle, GUILayout.Width(widths[2]));
                    EditorGUILayout.LabelField(r.playerId, _tableCellStyle, GUILayout.Width(widths[3]));
                    EditorGUILayout.LabelField(r.playTime > 0 ? $"{r.playTime:F1}s" : "-", _tableCellStyle, GUILayout.Width(widths[4]));
                    EditorGUILayout.LabelField(r.platform, _tableCellStyle, GUILayout.Width(widths[5]));
                    EditorGUILayout.LabelField(r.deviceModel, _tableCellStyle, GUILayout.Width(widths[6]));

                    EditorGUILayout.EndHorizontal();
                    alt = !alt;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTableHeader(string[] headers, float[] widths)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (int col = 0; col < headers.Length; col++)
            {
                bool sorted = _sortColumn == col;
                string label = sorted ? headers[col] + (_sortAsc ? " ▲" : " ▼") : headers[col];

                if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(widths[col])))
                {
                    if (_sortColumn == col) _sortAsc = !_sortAsc;
                    else { _sortColumn = col; _sortAsc = false; }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWinRateCell(float wr, float width)
        {
            GUI.color = wr >= 70f ? new Color(0.4f, 1f, 0.4f)
                      : wr >= 40f ? new Color(1f, 1f, 0.4f)
                                  : new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField($"{wr:F1}%", _tableCellBold, GUILayout.Width(width));
            GUI.color = Color.white;
        }

        private void DrawEmptyTableNotice(string msg)
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"ℹ️ {msg}", EditorStyles.wordWrappedLabel, GUILayout.Width(400));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoConfigState()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox(
                "❌ Không tìm thấy AnalyticsConfig trong Resources!\n\n" +
                "Vui lòng vào menu: GameHub > Analytics > ⚙️ Create Analytics Config\n" +
                "và điền Web API Key + Project ID từ Firebase Console.",
                MessageType.Error);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string countMsg = _activeTab switch
            {
                TabView.Missions => $"Tổng số màn chơi: {_missionRows.Count}",
                TabView.Players  => $"Tổng số bản ghi player: {_playerRows.Count}",
                TabView.Events   => $"Tổng số sự kiện: {_eventRows.Count}",
                _                => ""
            };

            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            EditorGUILayout.LabelField(countMsg, EditorStyles.miniLabel);
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("❓ Hướng dẫn đồng bộ Google Sheets", EditorStyles.toolbarButton, GUILayout.Width(220)))
                ShowGoogleSheetsGuide();

            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------
        //  Firestore Fetching (REST runQuery)
        // ---------------------------------------------------------

        private void FetchRealFirestoreData()
        {
            LoadConfig();
            if (_config == null || !_config.IsValid)
            {
                _statusMsg = "❌ Cấu hình Firebase chưa đầy đủ (thiếu apiKey hoặc projectId).";
                _statusIsError = true;
                return;
            }

            _isFetching = true;
            _statusMsg = "⏳ Đang kết nối và tải toàn bộ dữ liệu từ Firestore...";
            _statusIsError = false;
            Repaint();

            // Run collectionGroup query for missions
            string url = $"{_config.FirestoreBaseUrl}:runQuery?key={_config.CleanApiKey}";
            string bodyMissions = "{\"structuredQuery\":{\"from\":[{\"collectionId\":\"missions\",\"allDescendants\":true}]}}";

            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyMissions);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;

            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                _isFetching = false;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _statusMsg = $"❌ Lỗi khi tải dữ liệu từ Firestore: {request.error}\nChi tiết: {request.downloadHandler.text}";
                    _statusIsError = true;
                    Repaint();
                    return;
                }

                string json = request.downloadHandler.text;
                ParseFirestoreMissionStats(json);

                // Fetch raw events as well
                FetchRealEvents();
            };
        }

        private void FetchRealEvents()
        {
            string url = $"{_config.FirestoreBaseUrl}:runQuery?key={_config.CleanApiKey}";
            string bodyEvents = "{\"structuredQuery\":{\"from\":[{\"collectionId\":\"events\",\"allDescendants\":true}],\"limit\":200}}";

            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyEvents);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;

            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    ParseFirestoreEvents(request.downloadHandler.text);
                }

                _isUsingRealData = true;
                _statusMsg = $"✅ Đã tải dữ liệu thành công từ Firestore! ({_playerRows.Count} bản ghi player, {_missionRows.Count} màn chơi, {_eventRows.Count} sự kiện)";
                _statusIsError = false;
                Repaint();
            };
        }

        private void ParseFirestoreMissionStats(string json)
        {
            _playerRows.Clear();
            _missionRows.Clear();

            var missionAggMap = new Dictionary<string, MissionRow>();

            int searchIdx = 0;
            while (true)
            {
                int docIdx = json.IndexOf("\"document\":", searchIdx, StringComparison.Ordinal);
                if (docIdx < 0) break;

                int nextDoc = json.IndexOf("\"document\":", docIdx + 11, StringComparison.Ordinal);
                string docBlock = nextDoc > 0 ? json.Substring(docIdx, nextDoc - docIdx) : json.Substring(docIdx);
                searchIdx = docIdx + 11;

                string docName = ExtractDocName(docBlock);
                if (string.IsNullOrEmpty(docName)) continue;

                // Format: projects/.../databases/(default)/documents/players/{playerId}/missions/{missionId}
                string playerId = ExtractBetween(docName, "/players/", "/missions/");
                string missionId = "";
                int mIdx = docName.IndexOf("/missions/", StringComparison.Ordinal);
                if (mIdx >= 0)
                {
                    missionId = docName.Substring(mIdx + 10);
                    int slash = missionId.IndexOf('/');
                    if (slash > 0) missionId = missionId.Substring(0, slash);
                }

                if (string.IsNullOrEmpty(missionId)) continue;
                if (string.IsNullOrEmpty(playerId)) playerId = "Unknown";

                var pRow = new PlayerRow
                {
                    playerId      = playerId,
                    missionId     = missionId,
                    started       = ExtractInt(docBlock, "started"),
                    completed     = ExtractInt(docBlock, "completed"),
                    failed        = ExtractInt(docBlock, "failed"),
                    bestTime      = ExtractFloat(docBlock, "bestTime"),
                    totalPlayTime = ExtractFloat(docBlock, "totalPlayTime"),
                    lastPlayed    = ExtractFieldValue(docBlock, "lastPlayed")
                };
                if (string.IsNullOrEmpty(pRow.lastPlayed))
                    pRow.lastPlayed = ExtractFieldValue(docBlock, "lastPlayed_str");

                _playerRows.Add(pRow);

                // Aggregate into MissionRow
                if (!missionAggMap.TryGetValue(missionId, out var mRow))
                {
                    mRow = new MissionRow { missionId = missionId, playerCount = 0, completedPlayers = 0, bestTime = 0f };
                    missionAggMap[missionId] = mRow;
                }

                mRow.started       += pRow.started;
                mRow.completed     += pRow.completed;
                mRow.failed        += pRow.failed;
                mRow.totalPlayTime += pRow.totalPlayTime;
                mRow.playerCount++;
                if (pRow.completed > 0)
                    mRow.completedPlayers++;

                if (pRow.bestTime > 0f)
                {
                    if (mRow.bestTime <= 0f || pRow.bestTime < mRow.bestTime)
                        mRow.bestTime = pRow.bestTime;
                }
            }

            _missionRows.AddRange(missionAggMap.Values);
        }

        private void ParseFirestoreEvents(string json)
        {
            _eventRows.Clear();

            int searchIdx = 0;
            while (true)
            {
                int docIdx = json.IndexOf("\"document\":", searchIdx, StringComparison.Ordinal);
                if (docIdx < 0) break;

                int nextDoc = json.IndexOf("\"document\":", docIdx + 11, StringComparison.Ordinal);
                string docBlock = nextDoc > 0 ? json.Substring(docIdx, nextDoc - docIdx) : json.Substring(docIdx);
                searchIdx = docIdx + 11;

                string eventType = ExtractFieldValue(docBlock, "eventType");
                if (string.IsNullOrEmpty(eventType)) continue;

                var ev = new EventRow
                {
                    eventType   = eventType,
                    missionId   = ExtractFieldValue(docBlock, "missionId"),
                    playerId    = ExtractFieldValue(docBlock, "playerId"),
                    timestamp   = ExtractFieldValue(docBlock, "timestamp"),
                    playTime    = ExtractFloat(docBlock, "playTime"),
                    platform    = ExtractFieldValue(docBlock, "platform"),
                    deviceModel = ExtractFieldValue(docBlock, "deviceModel")
                };
                _eventRows.Add(ev);
            }
        }

        // ---------------------------------------------------------
        //  Export Features: Excel (.csv) & Google Sheets
        // ---------------------------------------------------------

        private void ExportToExcel()
        {
            string defaultName = $"GameHub_Analytics_{_activeTab}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string path = EditorUtility.SaveFilePanel("Xuất file Excel (.xlsx)", "", defaultName, "xlsx");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var sheets = new List<XlsxSheet>();

                switch (_activeTab)
                {
                    case TabView.Missions:
                    {
                        var sheet = new XlsxSheet
                        {
                            Name = "Màn Chơi",
                            Headers = new List<string> { "Màn Chơi", "Số Lượt Bắt Đầu", "Hoàn Thành (Thắng)", "Thất Bại (Thua)", "Tỉ Lệ Thắng (%)", "Tỉ Lệ Hoàn Thành (%)", "Thời Gian TB", "Kỷ Lục", "Số Thiết Bị Test" }
                        };
                        foreach (var r in GetFilteredMissions())
                        {
                            sheet.Rows.Add(new List<string>
                            {
                                r.missionId,
                                r.started.ToString(),
                                r.completed.ToString(),
                                r.failed.ToString(),
                                $"{r.WinRate:F1}%",
                                $"{r.CompleteRate:F1}%",
                                FormatPlayTime(r.AvgPlayTime),
                                r.bestTime > 0 ? FormatPlayTime(r.bestTime) : "-",
                                r.playerCount.ToString()
                            });
                        }
                        sheets.Add(sheet);
                        break;
                    }

                    case TabView.Players:
                    {
                        var filtered = GetFilteredPlayers();
                        var groups = filtered.GroupBy(x => x.playerId).ToList();

                        // Sheet 1: Overview
                        var overviewSheet = new XlsxSheet
                        {
                            Name = "Tổng Hợp",
                            Headers = new List<string> { "Player ID (Thiết Bị)", "Số Màn Chơi", "Bắt Đầu", "Thắng", "Thua", "Win Rate TB (%)", "Hoàn Thành TB (%)", "Tổng Thời Lượng", "Lần Chơi Cuối" }
                        };

                        foreach (var g in groups)
                        {
                            int totalStarted = g.Sum(x => x.started);
                            int totalCompleted = g.Sum(x => x.completed);
                            int totalFailed = g.Sum(x => x.failed);
                            float totalTime = g.Sum(x => x.totalPlayTime);
                            float winRate = (totalCompleted + totalFailed) > 0 ? (float)totalCompleted / (totalCompleted + totalFailed) * 100f : 0f;
                            float completeRate = totalStarted > 0 ? (float)totalCompleted / totalStarted * 100f : 0f;

                            overviewSheet.Rows.Add(new List<string>
                            {
                                g.Key,
                                g.Count().ToString(),
                                totalStarted.ToString(),
                                totalCompleted.ToString(),
                                totalFailed.ToString(),
                                $"{winRate:F1}%",
                                $"{completeRate:F1}%",
                                FormatPlayTime(totalTime),
                                g.Max(x => x.lastPlayed)
                            });
                        }
                        sheets.Add(overviewSheet);

                        // Individual sheet for each Player
                        foreach (var g in groups)
                        {
                            var playerSheet = new XlsxSheet
                            {
                                Name = g.Key,
                                Headers = new List<string> { "Màn Chơi", "Bắt Đầu", "Thắng", "Thua", "Win Rate (%)", "Hoàn Thành (%)", "Kỷ Lục", "Thời Lượng", "Lần Chơi Cuối" }
                            };

                            foreach (var r in g)
                            {
                                playerSheet.Rows.Add(new List<string>
                                {
                                    r.missionId,
                                    r.started.ToString(),
                                    r.completed.ToString(),
                                    r.failed.ToString(),
                                    $"{r.WinRate:F1}%",
                                    $"{r.CompleteRate:F1}%",
                                    r.bestTime > 0 ? FormatPlayTime(r.bestTime) : "-",
                                    FormatPlayTime(r.totalPlayTime),
                                    FormatTimestamp(r.lastPlayed)
                                });
                            }
                            sheets.Add(playerSheet);
                        }
                        break;
                    }

                    case TabView.Events:
                    {
                        var sheet = new XlsxSheet
                        {
                            Name = "Sự Kiện",
                            Headers = new List<string> { "Thời Gian", "Tên Sự Kiện", "Màn Chơi", "Player ID", "Thời Lượng (giây)", "Nền Tảng", "Tên Thiết Bị" }
                        };
                        foreach (var r in GetFilteredEvents())
                        {
                            sheet.Rows.Add(new List<string>
                            {
                                r.timestamp,
                                r.eventType,
                                r.missionId,
                                r.playerId,
                                r.playTime.ToString("F1", CultureInfo.InvariantCulture),
                                r.platform,
                                r.deviceModel
                            });
                        }
                        sheets.Add(sheet);
                        break;
                    }
                }

                SaveXlsxFile(path, sheets);

                bool openNow = EditorUtility.DisplayDialog(
                    "Xuất File Excel (.xlsx) Thành Công! 🎉",
                    $"Đã lưu file thành công tại:\n{path}\n\n" +
                    (_activeTab == TabView.Players 
                        ? $"✨ Đã chia thành {sheets.Count} trang tính (1 sheet 'Tổng Hợp' + {sheets.Count - 1} sheet riêng cho từng Player)!\n\n" +
                          "• Khi mở bằng Excel hoặc Google Sheets, mỗi Player nằm ở một Trang tính (Sheet Tab) riêng biệt.\n"
                        : "") +
                    "Bạn có muốn mở file này ngay không?",
                    "Mở File",
                    "Mở Thư Mục Chứa");

                if (openNow)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                }
                else
                {
                    EditorUtility.RevealInFinder(path);
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Lỗi Xuất File Excel", $"Không thể lưu file: {ex.Message}", "OK");
            }
        }

        private void ExportToCsv()
        {
            string defaultName = $"GameHub_Analytics_{_activeTab}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string path = EditorUtility.SaveFilePanel("Xuất dữ liệu Analytics ra Excel (.csv)", "", defaultName, "csv");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var sb = new StringBuilder();

                switch (_activeTab)
                {
                    case TabView.Missions:
                        sb.AppendLine("Màn Chơi,Số Lượt Bắt Đầu,Hoàn Thành (Thắng),Thất Bại (Thua),Tỉ Lệ Thắng (%),Tỉ Lệ Hoàn Thành (%),Thời Gian TB (giây),Kỷ Lục (giây),Số Thiết Bị Test");
                        foreach (var r in GetFilteredMissions())
                        {
                            sb.AppendLine($"\"{EscapeCsv(r.missionId)}\",{r.started},{r.completed},{r.failed},{r.WinRate:F1}%,{r.CompleteRate:F1}%,{r.AvgPlayTime:F1},{r.bestTime:F1},{r.playerCount}");
                        }
                        break;

                    case TabView.Players:
                        sb.AppendLine("Player ID (Thiết Bị),Màn Chơi,Số Lần Chơi,Thắng,Thua,Win Rate (%),Hoàn Thành (%),Kỷ Lục (giây),Tổng Thời Lượng (giây),Lần Chơi Cuối");
                        foreach (var r in GetFilteredPlayers())
                        {
                            sb.AppendLine($"\"{EscapeCsv(r.playerId)}\",\"{EscapeCsv(r.missionId)}\",{r.started},{r.completed},{r.failed},{r.WinRate:F1}%,{r.CompleteRate:F1}%,{r.bestTime:F1},{r.totalPlayTime:F1},\"{EscapeCsv(r.lastPlayed)}\"");
                        }
                        break;

                    case TabView.Events:
                        sb.AppendLine("Thời Gian,Tên Sự Kiện,Màn Chơi,Player ID,Thời Lượng (giây),Nền Tảng,Tên Thiết Bị");
                        foreach (var r in GetFilteredEvents())
                        {
                            sb.AppendLine($"\"{EscapeCsv(r.timestamp)}\",\"{EscapeCsv(r.eventType)}\",\"{EscapeCsv(r.missionId)}\",\"{EscapeCsv(r.playerId)}\",{r.playTime:F1},\"{EscapeCsv(r.platform)}\",\"{EscapeCsv(r.deviceModel)}\"");
                        }
                        break;
                }

                // Write with UTF-8 BOM for Excel compatibility
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));

                bool openNow = EditorUtility.DisplayDialog(
                    "Xuất Excel Thành Công! 🎉",
                    $"Đã lưu file thành công tại:\n{path}\n\nBạn có muốn mở file này bằng Excel ngay không?",
                    "Mở Bằng Excel",
                    "Đóng");

                if (openNow)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Lỗi Xuất File", $"Không thể lưu file: {ex.Message}", "OK");
            }
        }

        private void ExportToGoogleSheets()
        {
            var sb = new StringBuilder();

            switch (_activeTab)
            {
                case TabView.Missions:
                    sb.AppendLine("Màn Chơi\tSố Lượt Bắt Đầu\tHoàn Thành (Thắng)\tThất Bại (Thua)\tTỉ Lệ Thắng (%)\tTỉ Lệ Hoàn Thành (%)\tThời Gian TB\tKỷ Lục\tSố Thiết Bị Test");
                    foreach (var r in GetFilteredMissions())
                        sb.AppendLine($"{r.missionId}\t{r.started}\t{r.completed}\t{r.failed}\t{r.WinRate:F1}%\t{r.CompleteRate:F1}%\t{FormatPlayTime(r.AvgPlayTime)}\t{(r.bestTime > 0 ? FormatPlayTime(r.bestTime) : "-")}\t{r.playerCount}");
                    break;

                case TabView.Players:
                {
                    var groups = GetFilteredPlayers().GroupBy(x => x.playerId).ToList();
                    sb.AppendLine("=== BẢNG TỔNG HỢP TẤT CẢ NGƯỜI CHƠI ===");
                    sb.AppendLine("Player ID (Thiết Bị)\tSố Màn Chơi\tBắt Đầu\tThắng\tThua\tWin Rate TB (%)\tHoàn Thành TB (%)\tTổng Thời Lượng\tLần Chơi Cuối");
                    foreach (var g in groups)
                    {
                        int totalStarted = g.Sum(x => x.started);
                        int totalCompleted = g.Sum(x => x.completed);
                        int totalFailed = g.Sum(x => x.failed);
                        float totalTime = g.Sum(x => x.totalPlayTime);
                        float winRate = (totalCompleted + totalFailed) > 0 ? (float)totalCompleted / (totalCompleted + totalFailed) * 100f : 0f;
                        float completeRate = totalStarted > 0 ? (float)totalCompleted / totalStarted * 100f : 0f;
                        sb.AppendLine($"{g.Key}\t{g.Count()}\t{totalStarted}\t{totalCompleted}\t{totalFailed}\t{winRate:F1}%\t{completeRate:F1}%\t{FormatPlayTime(totalTime)}\t{g.Max(x => x.lastPlayed)}");
                    }
                    sb.AppendLine();

                    foreach (var g in groups)
                    {
                        sb.AppendLine($"=== 📱 PLAYER: {g.Key} ===");
                        sb.AppendLine("Màn Chơi\tBắt Đầu\tThắng\tThua\tWin Rate (%)\tHoàn Thành (%)\tKỷ Lục\tThời Lượng\tLần Chơi Cuối");
                        foreach (var r in g)
                        {
                            sb.AppendLine($"{r.missionId}\t{r.started}\t{r.completed}\t{r.failed}\t{r.WinRate:F1}%\t{r.CompleteRate:F1}%\t{(r.bestTime > 0 ? FormatPlayTime(r.bestTime) : "-")}\t{FormatPlayTime(r.totalPlayTime)}\t{r.lastPlayed}");
                        }
                        sb.AppendLine();
                    }
                    break;
                }

                case TabView.Events:
                    sb.AppendLine("Thời Gian\tTên Sự Kiện\tMàn Chơi\tPlayer ID\tThời Lượng (giây)\tNền Tảng\tTên Thiết Bị");
                    foreach (var r in GetFilteredEvents())
                        sb.AppendLine($"{r.timestamp}\t{r.eventType}\t{r.missionId}\t{r.playerId}\t{r.playTime:F1}\t{r.platform}\t{r.deviceModel}");
                    break;
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();

            string helpTip = _activeTab == TabView.Players
                ? "💡 MẸO TRANG TÍNH RIÊNG (SHEETS):\nĐể mỗi Player nằm ở một Trang tính (Tab) riêng biệt trên Google Sheets:\n• Hãy dùng nút '📊 Xuất Excel (.xlsx)'.\n• Trên Google Sheets, chọn Tệp > Nhập (File > Import) và tải file .xlsx lên.\n\n"
                : "";

            bool openSheets = EditorUtility.DisplayDialog(
                "Đã Copy Dữ Liệu Bảng Tính! 📋",
                $"{helpTip}Dữ liệu đã được copy vào Clipboard của bạn.\n\nBạn có muốn mở Google Sheets (sheets.new) để dán (Ctrl + V) ngay không?",
                "Mở Google Sheets (sheets.new)",
                "Chỉ Copy, Đóng");

            if (openSheets)
            {
                Application.OpenURL("https://sheets.new");
            }
        }

        private class XlsxSheet
        {
            public string Name;
            public List<string> Headers = new List<string>();
            public List<List<string>> Rows = new List<List<string>>();
        }

        private static void SaveXlsxFile(string filePath, List<XlsxSheet> sheets)
        {
            if (File.Exists(filePath)) File.Delete(filePath);

            using (var fs = File.Create(filePath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                // [Content_Types].xml
                var sbCt = new StringBuilder();
                sbCt.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sbCt.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
                sbCt.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
                sbCt.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
                sbCt.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
                sbCt.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
                for (int i = 0; i < sheets.Count; i++)
                    sbCt.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                sbCt.Append("</Types>");
                WriteZipEntry(zip, "[Content_Types].xml", sbCt.ToString());

                // _rels/.rels
                string rootRels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>";
                WriteZipEntry(zip, "_rels/.rels", rootRels);

                // xl/_rels/workbook.xml.rels
                var sbWbRels = new StringBuilder();
                sbWbRels.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sbWbRels.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                sbWbRels.Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
                for (int i = 0; i < sheets.Count; i++)
                    sbWbRels.Append($"<Relationship Id=\"rId{i + 2}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                sbWbRels.Append("</Relationships>");
                WriteZipEntry(zip, "xl/_rels/workbook.xml.rels", sbWbRels.ToString());

                // xl/styles.xml
                string styles = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                    "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
                    "<borders count=\"1\"><border/></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
                    "</styleSheet>";
                WriteZipEntry(zip, "xl/styles.xml", styles);

                // xl/workbook.xml
                var sbWb = new StringBuilder();
                sbWb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sbWb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
                sbWb.Append("<sheets>");
                var existingNames = new HashSet<string>();
                for (int i = 0; i < sheets.Count; i++)
                {
                    string safeName = SanitizeSheetName(sheets[i].Name, existingNames);
                    sbWb.Append($"<sheet name=\"{EscapeXml(safeName)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 2}\"/>");
                }
                sbWb.Append("</sheets>");
                sbWb.Append("</workbook>");
                WriteZipEntry(zip, "xl/workbook.xml", sbWb.ToString());

                // xl/worksheets/sheet{i+1}.xml
                for (int i = 0; i < sheets.Count; i++)
                {
                    var sheet = sheets[i];
                    var sbSheet = new StringBuilder();
                    sbSheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                    sbSheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                    sbSheet.Append("<sheetData>");

                    int rowNum = 1;
                    if (sheet.Headers != null && sheet.Headers.Count > 0)
                    {
                        sbSheet.Append($"<row r=\"{rowNum}\">");
                        for (int col = 0; col < sheet.Headers.Count; col++)
                        {
                            string cellRef = GetCellRef(col + 1, rowNum);
                            sbSheet.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{EscapeXml(sheet.Headers[col])}</t></is></c>");
                        }
                        sbSheet.Append("</row>");
                        rowNum++;
                    }

                    foreach (var row in sheet.Rows)
                    {
                        sbSheet.Append($"<row r=\"{rowNum}\">");
                        for (int col = 0; col < row.Count; col++)
                        {
                            string cellRef = GetCellRef(col + 1, rowNum);
                            string val = row[col];
                            if (string.IsNullOrEmpty(val)) continue;

                            if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double numVal)
                                && !val.Contains("%") && !val.Contains("s") && !val.Contains("m") && !val.Contains("h"))
                            {
                                sbSheet.Append($"<c r=\"{cellRef}\"><v>{numVal.ToString(CultureInfo.InvariantCulture)}</v></c>");
                            }
                            else
                            {
                                sbSheet.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{EscapeXml(val)}</t></is></c>");
                            }
                        }
                        sbSheet.Append("</row>");
                        rowNum++;
                    }

                    sbSheet.Append("</sheetData>");
                    sbSheet.Append("</worksheet>");
                    WriteZipEntry(zip, $"xl/worksheets/sheet{i + 1}.xml", sbSheet.ToString());
                }
            }
        }

        private static void WriteZipEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Fastest);
            using (var stream = entry.Open())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private static string SanitizeSheetName(string name, HashSet<string> existingNames)
        {
            if (string.IsNullOrEmpty(name)) name = "Sheet";
            char[] invalid = { '\\', '/', '?', '*', '[', ']', ':' };
            foreach (var c in invalid) name = name.Replace(c, '_');
            if (name.Length > 31) name = name.Substring(0, 31);

            string unique = name;
            int counter = 1;
            while (existingNames.Contains(unique.ToLowerInvariant()))
            {
                string suffix = $"_{counter++}";
                int maxBaseLen = 31 - suffix.Length;
                string baseName = name.Length > maxBaseLen ? name.Substring(0, maxBaseLen) : name;
                unique = baseName + suffix;
            }
            existingNames.Add(unique.ToLowerInvariant());
            return unique;
        }

        private static string GetCellRef(int colIndex, int rowIndex)
        {
            string colLetter = "";
            while (colIndex > 0)
            {
                int rem = (colIndex - 1) % 26;
                colLetter = (char)('A' + rem) + colLetter;
                colIndex = (colIndex - 1) / 26;
            }
            return $"{colLetter}{rowIndex}";
        }

        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
        }

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\"", "\"\"");
        }

        private void ShowGoogleSheetsGuide()
        {
            string guide =
                "=== CÁCH DÙNG GOOGLE SHEETS VỚI GAMEHUB ANALYTICS ===\n\n" +
                "Cách 1 (Nhanh nhất): Bấm nút '📑 Dán vào Google Sheets' -> Chọn Mở sheets.new -> Bấm ô A1 và nhấn Ctrl + V.\n\n" +
                "Cách 2: Bấm '📊 Xuất Excel (.csv)' -> Mở Google Sheets -> Tệp (File) -> Mở (Open) -> Tải lên (Upload) file .csv vừa lưu.\n\n" +
                "Cách 3: Dùng Google Apps Script kết nối trực tiếp Firestore REST API tự động cập nhật.";

            EditorUtility.DisplayDialog("Hướng Dẫn Google Sheets", guide, "Đã Hiểu");
        }

        // ---------------------------------------------------------
        //  Data Filter & Sort
        // ---------------------------------------------------------

        private List<MissionRow> GetFilteredMissions()
        {
            var list = new List<MissionRow>(_missionRows);
            if (!string.IsNullOrEmpty(_filterText))
                list.RemoveAll(x => !x.missionId.ToLower().Contains(_filterText.ToLower()));

            list.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    0 => string.Compare(a.missionId, b.missionId, StringComparison.OrdinalIgnoreCase),
                    1 => a.started.CompareTo(b.started),
                    2 => a.completed.CompareTo(b.completed),
                    3 => a.failed.CompareTo(b.failed),
                    4 => a.WinRate.CompareTo(b.WinRate),
                    5 => a.CompleteRate.CompareTo(b.CompleteRate),
                    6 => a.AvgPlayTime.CompareTo(b.AvgPlayTime),
                    7 => a.bestTime.CompareTo(b.bestTime),
                    8 => a.playerCount.CompareTo(b.playerCount),
                    _ => 0
                };
                return _sortAsc ? cmp : -cmp;
            });

            return list;
        }

        private List<PlayerRow> GetFilteredPlayers()
        {
            var list = new List<PlayerRow>(_playerRows);
            if (!string.IsNullOrEmpty(_filterText))
            {
                string f = _filterText.ToLower();
                list.RemoveAll(x => !x.playerId.ToLower().Contains(f) && !x.missionId.ToLower().Contains(f));
            }

            list.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    0 => string.Compare(a.playerId, b.playerId, StringComparison.OrdinalIgnoreCase),
                    1 => string.Compare(a.missionId, b.missionId, StringComparison.OrdinalIgnoreCase),
                    2 => a.started.CompareTo(b.started),
                    3 => a.completed.CompareTo(b.completed),
                    4 => a.failed.CompareTo(b.failed),
                    5 => a.WinRate.CompareTo(b.WinRate),
                    6 => a.CompleteRate.CompareTo(b.CompleteRate),
                    7 => a.bestTime.CompareTo(b.bestTime),
                    8 => a.totalPlayTime.CompareTo(b.totalPlayTime),
                    _ => 0
                };
                return _sortAsc ? cmp : -cmp;
            });

            return list;
        }

        private List<EventRow> GetFilteredEvents()
        {
            var list = new List<EventRow>(_eventRows);
            if (!string.IsNullOrEmpty(_filterText))
            {
                string f = _filterText.ToLower();
                list.RemoveAll(x => !x.eventType.ToLower().Contains(f) &&
                                    !x.missionId.ToLower().Contains(f) &&
                                    !x.playerId.ToLower().Contains(f));
            }

            list.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    0 => string.Compare(a.timestamp, b.timestamp, StringComparison.OrdinalIgnoreCase),
                    1 => string.Compare(a.eventType, b.eventType, StringComparison.OrdinalIgnoreCase),
                    2 => string.Compare(a.missionId, b.missionId, StringComparison.OrdinalIgnoreCase),
                    3 => string.Compare(a.playerId, b.playerId, StringComparison.OrdinalIgnoreCase),
                    4 => a.playTime.CompareTo(b.playTime),
                    _ => 0
                };
                return _sortAsc ? cmp : -cmp;
            });

            return list;
        }

        // ---------------------------------------------------------
        //  Sample Data Fallback
        // ---------------------------------------------------------

        private void LoadSampleData()
        {
            _isUsingRealData = false;
            _missionRows.Clear();
            _playerRows.Clear();
            _eventRows.Clear();

            _missionRows.AddRange(new[]
            {
                new MissionRow { missionId="level_01", started=150, completed=120, failed=30, totalPlayTime=5424f, bestTime=35.2f, playerCount=15, completedPlayers=14 },
                new MissionRow { missionId="level_02", started=130, completed=85,  failed=45, totalPlayTime=5329f, bestTime=48.6f, playerCount=14, completedPlayers=12 },
                new MissionRow { missionId="level_03", started=90,  completed=40,  failed=50, totalPlayTime=3124f, bestTime=65.0f, playerCount=12, completedPlayers=9 },
                new MissionRow { missionId="boss_01",  started=60,  completed=22,  failed=38, totalPlayTime=2651f, bestTime=98.4f, playerCount=10, completedPlayers=6 },
                new MissionRow { missionId="level_04", started=45,  completed=38,  failed=7,  totalPlayTime=1360f, bestTime=28.1f, playerCount=8,  completedPlayers=7 },
                new MissionRow { missionId="challenge_01", started=30, completed=8, failed=22, totalPlayTime=2859f, bestTime=89.5f, playerCount=6, completedPlayers=3 }
            });

            _playerRows.AddRange(new[]
            {
                new PlayerRow { playerId="device_sam_s23_01", missionId="level_01", started=12, completed=10, failed=2, bestTime=35.2f, totalPlayTime=420f, lastPlayed=DateTime.UtcNow.AddMinutes(-15).ToString("o") },
                new PlayerRow { playerId="device_sam_s23_01", missionId="level_02", started=8,  completed=5,  failed=3, bestTime=52.1f, totalPlayTime=390f, lastPlayed=DateTime.UtcNow.AddMinutes(-10).ToString("o") },
                new PlayerRow { playerId="device_pixel7_02",  missionId="level_01", started=15, completed=14, failed=1, bestTime=38.4f, totalPlayTime=550f, lastPlayed=DateTime.UtcNow.AddHours(-1).ToString("o") },
                new PlayerRow { playerId="device_pixel7_02",  missionId="boss_01",  started=6,  completed=2,  failed=4, bestTime=110.2f, totalPlayTime=680f, lastPlayed=DateTime.UtcNow.AddMinutes(-40).ToString("o") }
            });

            _eventRows.AddRange(new[]
            {
                new EventRow { timestamp=DateTime.UtcNow.AddMinutes(-5).ToString("o"),  eventType="mission_complete", missionId="level_01", playerId="device_sam_s23_01", playTime=42.5f, platform="Android", deviceModel="Samsung Galaxy S23" },
                new EventRow { timestamp=DateTime.UtcNow.AddMinutes(-12).ToString("o"), eventType="mission_start",    missionId="level_01", playerId="device_sam_s23_01", playTime=0f,    platform="Android", deviceModel="Samsung Galaxy S23" },
                new EventRow { timestamp=DateTime.UtcNow.AddMinutes(-20).ToString("o"), eventType="mission_fail",     missionId="boss_01",  playerId="device_pixel7_02",  playTime=35.0f, platform="Android", deviceModel="Google Pixel 7" }
            });
        }

        // ---------------------------------------------------------
        //  JSON Extraction Utilities
        // ---------------------------------------------------------

        private static string ExtractDocName(string docBlock)
        {
            int idx = docBlock.IndexOf("\"name\"", StringComparison.Ordinal);
            if (idx < 0) return "";
            int colon = docBlock.IndexOf(':', idx + 6);
            if (colon < 0) return "";
            int q1 = docBlock.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            int q2 = docBlock.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return docBlock.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static string ExtractFieldValue(string docBlock, string fieldName)
        {
            int fIdx = docBlock.IndexOf($"\"{fieldName}\"", StringComparison.Ordinal);
            if (fIdx < 0) return "";

            int colon = docBlock.IndexOf(':', fIdx + fieldName.Length + 2);
            if (colon < 0) return "";

            int openBrace = docBlock.IndexOf('{', colon);
            if (openBrace < 0) return "";

            int closeBrace = -1;
            bool inQuotes = false;
            for (int i = openBrace + 1; i < docBlock.Length; i++)
            {
                char c = docBlock[i];
                if (c == '\\' && i + 1 < docBlock.Length)
                {
                    i++;
                    continue;
                }
                if (c == '"') inQuotes = !inQuotes;
                else if (c == '}' && !inQuotes)
                {
                    closeBrace = i;
                    break;
                }
            }
            if (closeBrace < 0) return "";

            string fieldBlock = docBlock.Substring(openBrace, closeBrace - openBrace + 1);
            int typeColon = fieldBlock.IndexOf(':');
            if (typeColon < 0) return "";

            int q1 = fieldBlock.IndexOf('"', typeColon + 1);
            if (q1 >= 0)
            {
                int q2 = fieldBlock.IndexOf('"', q1 + 1);
                if (q2 > q1) return fieldBlock.Substring(q1 + 1, q2 - q1 - 1);
            }

            int brace = fieldBlock.IndexOf('}', typeColon + 1);
            if (brace > typeColon)
            {
                return fieldBlock.Substring(typeColon + 1, brace - typeColon - 1).Trim();
            }

            return "";
        }

        private static string FormatPlayTime(float seconds)
        {
            if (seconds <= 0f) return "-";
            if (seconds < 60f) return $"{seconds:F1}s";
            int totalSec = Mathf.RoundToInt(seconds);
            int h = totalSec / 3600;
            int m = (totalSec % 3600) / 60;
            int s = totalSec % 60;
            if (h > 0)
                return $"{h}h {m:D2}m {s:D2}s";
            return $"{m}m {s:D2}s";
        }

        private static string ExtractJsonString(string block, string field)
        {
            return ExtractFieldValue(block, field);
        }

        private static int ExtractInt(string block, string field)
        {
            string val = ExtractFieldValue(block, field);
            return int.TryParse(val, out int res) ? res : 0;
        }

        private static float ExtractFloat(string block, string field)
        {
            string val = ExtractFieldValue(block, field);
            return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float res) ? res : 0f;
        }

        private static string ExtractBetween(string source, string startPattern, string endPattern)
        {
            int s = source.IndexOf(startPattern, StringComparison.Ordinal);
            if (s < 0) return "";
            s += startPattern.Length;
            int e = source.IndexOf(endPattern, s, StringComparison.Ordinal);
            if (e < 0) return "";
            return source.Substring(s, e - s);
        }

        private static string FormatTimestamp(string iso)
        {
            if (DateTime.TryParse(iso, null, DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            return iso;
        }

        // ---------------------------------------------------------
        //  Style Initializer
        // ---------------------------------------------------------

        private void InitStyles()
        {
            if (_stylesReady) return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 14,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = Color.white }
            };

            _subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.75f, 0.75f, 0.75f) }
            };

            _tableCellStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize  = 11
            };

            _tableCellBold = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize  = 11
            };

            _stylesReady = true;
        }
    }
}
