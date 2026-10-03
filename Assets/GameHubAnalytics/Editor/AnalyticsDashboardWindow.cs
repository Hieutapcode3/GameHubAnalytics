using UnityEditor;
using UnityEngine;

namespace GameHub.Analytics.Editor
{
    /// <summary>
    /// Editor Window hiển thị thống kê analytics từ Firebase Firestore.
    /// Mở bằng menu: GameHub > Analytics > Open Dashboard
    /// </summary>
    public class AnalyticsDashboardWindow : EditorWindow
    {
        // ─────────────────────────────────────────────────────────
        //  Menu Item
        // ─────────────────────────────────────────────────────────

        [MenuItem("GameHub/Analytics/📊 Open Dashboard")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnalyticsDashboardWindow>("📊 Analytics Dashboard");
            window.minSize = new Vector2(640, 480);
        }

        // ─────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────

        private AnalyticsConfig _config;
        private Vector2         _scrollPos;
        private string          _statusMsg = "";
        private bool            _stylesReady;
        private GUIStyle        _titleStyle;
        private GUIStyle        _subtitleStyle;
        private Texture2D       _headerBg;
        private Texture2D       _rowBg;
        private Texture2D       _altRowBg;

        // Filter
        private string _filterMission = "";
        private int    _sortColumn    = 3; // default sort by WinRate
        private bool   _sortAsc       = false;

        // ─────────────────────────────────────────────────────────
        //  Sample Data (Phase 3: replace với Firestore fetch)
        // ─────────────────────────────────────────────────────────

        private struct MissionStats
        {
            public string missionId;
            public int    started;
            public int    completed;
            public int    failed;
            public float  avgPlayTime;

            public float WinRate      => started > 0 ? (float)completed / started * 100f : 0f;
            public float CompleteRate => started > 0 ? (float)completed / started * 100f : 0f;
        }

        private readonly MissionStats[] _sampleData =
        {
            new MissionStats { missionId="level_01", started=150, completed=120, failed=30, avgPlayTime=45.2f },
            new MissionStats { missionId="level_02", started=130, completed=85,  failed=45, avgPlayTime=62.7f },
            new MissionStats { missionId="level_03", started=90,  completed=40,  failed=50, avgPlayTime=78.1f },
            new MissionStats { missionId="boss_01",  started=60,  completed=22,  failed=38, avgPlayTime=120.5f},
            new MissionStats { missionId="level_04", started=45,  completed=38,  failed=7,  avgPlayTime=35.8f },
            new MissionStats { missionId="challenge_01", started=30, completed=8, failed=22, avgPlayTime=95.3f},
        };

        // ─────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _config = Resources.Load<AnalyticsConfig>("AnalyticsConfig");
            _stylesReady = false;
        }

        // ─────────────────────────────────────────────────────────
        //  GUI
        // ─────────────────────────────────────────────────────────

        private void OnGUI()
        {
            InitStyles();

            // ── Header ──────────────────────────────────────────
            DrawHeader();

            // ── Config Check ─────────────────────────────────────
            if (_config == null)
            {
                DrawNoConfigState();
                return;
            }

            // ── Toolbar ──────────────────────────────────────────
            DrawToolbar();

            // ── Status ───────────────────────────────────────────
            if (!string.IsNullOrEmpty(_statusMsg))
            {
                EditorGUILayout.HelpBox(_statusMsg, MessageType.Info);
                EditorGUILayout.Space(4);
            }

            // ── Session Info (Play Mode only) ────────────────────
            if (Application.isPlaying && AnalyticsManager.Instance != null)
                DrawSessionInfo();

            // ── Stats Table ──────────────────────────────────────
            DrawStatsTable();

            // ── Footer ───────────────────────────────────────────
            DrawFooter();
        }

        // ─────────────────────────────────────────────────────────
        //  Draw Sections
        // ─────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            // Header background
            var headerRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(0, 0, position.width, 56), new Color(0.13f, 0.13f, 0.17f));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("📊  GameHub Analytics Dashboard", _titleStyle);

            string gameInfo = _config != null
                ? $"Game: {_config.gameId}  |  Project: {_config.projectId}"
                : "No config loaded";
            EditorGUILayout.LabelField(gameInfo, _subtitleStyle);

            EditorGUILayout.Space(6);
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
                RefreshData();

            if (GUILayout.Button("⚙️ Config", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                Selection.activeObject = _config;
                EditorGUIUtility.PingObject(_config);
            }

            if (GUILayout.Button("🌐 Firestore", EditorStyles.toolbarButton, GUILayout.Width(80)))
                Application.OpenURL($"https://console.firebase.google.com/project/{_config.projectId}/firestore");

            GUILayout.FlexibleSpace();

            // Filter field
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));
            _filterMission = EditorGUILayout.TextField(_filterMission, EditorStyles.toolbarSearchField, GUILayout.Width(150));
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
                _filterMission = "";

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawSessionInfo()
        {
            var (duration, eventCount) = AnalyticsManager.Instance.GetSessionStats();
            string sessionId = AnalyticsManager.Instance.GetSessionId();
            int    queued    = AnalyticsManager.Instance.PendingQueueCount;

            EditorGUILayout.BeginHorizontal();
            GUI.color = new Color(0.6f, 1f, 0.6f);
            EditorGUILayout.LabelField($"● LIVE SESSION  |  ID: {sessionId}  |  " +
                                       $"Duration: {duration:F0}s  |  Events: {eventCount}  |  " +
                                       $"Queue: {queued}", EditorStyles.miniLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawStatsTable()
        {
            // Column headers
            float[] widths = { 130f, 65f, 75f, 65f, 80f, 90f };
            string[] headers = { "Mission ID", "Started", "Completed", "Failed", "Win Rate", "Avg Time (s)" };

            // Header row
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

            // Data rows
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            bool alt = false;
            foreach (var stats in GetFilteredSortedData())
            {
                var rowBg = alt ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.2f, 0.2f, 0.2f);
                var rowRect = EditorGUILayout.BeginHorizontal();
                EditorGUI.DrawRect(rowRect, rowBg);

                EditorGUILayout.LabelField(stats.missionId,          GUILayout.Width(widths[0]));
                EditorGUILayout.LabelField(stats.started.ToString(), GUILayout.Width(widths[1]));
                EditorGUILayout.LabelField(stats.completed.ToString(),GUILayout.Width(widths[2]));
                EditorGUILayout.LabelField(stats.failed.ToString(),  GUILayout.Width(widths[3]));

                // Win Rate with color
                float wr = stats.WinRate;
                GUI.color = wr >= 70f ? new Color(0.4f, 1f, 0.4f)
                          : wr >= 40f ? new Color(1f,   1f, 0.4f)
                                      : new Color(1f,   0.4f, 0.4f);
                EditorGUILayout.LabelField($"{wr:F1}%", GUILayout.Width(widths[4]));
                GUI.color = Color.white;

                EditorGUILayout.LabelField(stats.avgPlayTime.ToString("F1"), GUILayout.Width(widths[5]));
                EditorGUILayout.EndHorizontal();

                alt = !alt;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawNoConfigState()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox(
                "❌  Không tìm thấy AnalyticsConfig trong Resources!\n\n" +
                "Tạo file: Right-click trong Project Window\n" +
                "→ Create > GameHub > Analytics Config\n" +
                "Đặt tên 'AnalyticsConfig' và đặt vào thư mục 'Resources'.",
                MessageType.Error);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("➕  Tạo AnalyticsConfig tự động", GUILayout.Height(36)))
                CreateConfigAsset();
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.BeginHorizontal();
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField(
                "ℹ️  Dữ liệu mẫu (Phase 1). Phase 3 sẽ fetch data thật từ Firestore.",
                EditorStyles.miniLabel);
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("v1.0.0", EditorStyles.miniLabel, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────────────────
        //  Data Helpers
        // ─────────────────────────────────────────────────────────

        private System.Collections.Generic.IEnumerable<MissionStats> GetFilteredSortedData()
        {
            var data = new System.Collections.Generic.List<MissionStats>(_sampleData);

            // Filter
            if (!string.IsNullOrEmpty(_filterMission))
                data.RemoveAll(s => !s.missionId.ToLower().Contains(_filterMission.ToLower()));

            // Sort
            data.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    0 => string.Compare(a.missionId, b.missionId),
                    1 => a.started.CompareTo(b.started),
                    2 => a.completed.CompareTo(b.completed),
                    3 => a.failed.CompareTo(b.failed),
                    4 => a.WinRate.CompareTo(b.WinRate),
                    5 => a.avgPlayTime.CompareTo(b.avgPlayTime),
                    _ => 0
                };
                return _sortAsc ? cmp : -cmp;
            });

            return data;
        }

        private void RefreshData()
        {
            if (_config == null || !_config.IsValid)
            {
                _statusMsg = "❌  Config chưa hợp lệ.";
                return;
            }
            _statusMsg = "🔄  Đang tải... (Firestore fetch sẽ được implement trong Phase 3)";
            Repaint();
        }

        private void CreateConfigAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var config = CreateInstance<AnalyticsConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Resources/AnalyticsConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _config = config;
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        // ─────────────────────────────────────────────────────────
        //  Style Initialization
        // ─────────────────────────────────────────────────────────

        private void InitStyles()
        {
            if (_stylesReady) return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = Color.white }
            };

            _subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _stylesReady = true;
        }
    }
}
