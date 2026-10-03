using UnityEngine;
using GameHub.Analytics;

namespace GamePlay
{
    /// <summary>
    /// Giao diện điều khiển Gameplay & Test Analytics trực quan:
    ///   - HUD: Xem Level hiện tại, Đồng hồ đếm thời gian, Chọn Level nhanh.
    ///   - Action Buttons: Nút WIN và LOSE để test bắn event.
    ///   - Result Panel (Win/Lose Modal): Bật lên khi thắng hoặc thua, hiển thị stats, nút Next và Restart.
    ///   - Thống kê thời gian thực từ Firebase Firestore (Win Rate %, Kỷ lục...).
    /// </summary>
    [RequireComponent(typeof(LevelManager))]
    public class GamePlayUI : MonoBehaviour
    {
        private LevelManager _manager;

        // Tùy chỉnh hiển thị
        [Header("UI Scale")]
        [SerializeField] private float uiScale = 1.0f;

        // Styles cache
        private bool _stylesReady = false;
        private GUIStyle _hudBoxStyle;
        private GUIStyle _levelTitleStyle;
        private GUIStyle _timerStyle;
        private GUIStyle _winButtonStyle;
        private GUIStyle _loseButtonStyle;
        private GUIStyle _panelBgStyle;
        private GUIStyle _panelTitleWinStyle;
        private GUIStyle _panelTitleLoseStyle;
        private GUIStyle _actionButtonStyle;
        private GUIStyle _statsLabelStyle;
        private GUIStyle _miniLabelStyle;

        private void Awake()
        {
            _manager = GetComponent<LevelManager>();
        }

        private void OnGUI()
        {
            if (_manager == null) _manager = LevelManager.Instance;
            if (_manager == null) return;

            InitStyles();

            // Lưu ma trận GUI cũ và áp dụng scale nếu màn hình lớn/nhỏ
            var oldMatrix = GUI.matrix;
            if (Screen.width < 800)
            {
                float factor = Screen.width / 800f;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(factor, factor, 1f));
            }

            // 1. HUD Trên cùng
            DrawTopHUD();

            // 2. Các nút Gameplay chính (Win / Lose) khi đang chơi
            if (_manager.State == LevelState.Playing)
            {
                DrawGameplayButtons();
            }

            // 3. Panel Kết Quả (Popup khi Thắng hoặc Thua)
            if (_manager.State == LevelState.Won || _manager.State == LevelState.Lost)
            {
                DrawResultPanel();
            }

            // 4. Thanh trạng thái Analytics ở đáy màn hình
            DrawBottomStatusBar();

            GUI.matrix = oldMatrix;
        }

        private void DrawTopHUD()
        {
            float screenW = Screen.width < 800 ? 800 : Screen.width;

            // Khung Header
            GUILayout.BeginArea(new Rect(15, 15, screenW - 30, 110), _hudBoxStyle);
            GUILayout.BeginHorizontal();

            // Thông tin Level & Timer
            GUILayout.BeginVertical(GUILayout.Width(260));
            GUILayout.Label($"🎮 MÀN CHƠI: LEVEL {_manager.CurrentLevel:D2}", _levelTitleStyle);
            GUILayout.Label($"⏱️ Thời gian chơi: {_manager.PlayTime:F1}s", _timerStyle);
            GUILayout.EndVertical();

            // Level Selector nhanh (Level 1 .. 5)
            GUILayout.BeginVertical();
            GUILayout.Label("Chọn màn chơi nhanh:", _miniLabelStyle);
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 6; i++)
            {
                bool isCurrent = _manager.CurrentLevel == i;
                GUI.backgroundColor = isCurrent ? new Color(0.3f, 0.8f, 1f) : Color.white;
                if (GUILayout.Button($"Màn {i}", GUILayout.Height(32), GUILayout.Width(75)))
                {
                    _manager.LoadLevel(i);
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Nút Restart nhanh trên HUD
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
            if (GUILayout.Button("🔄 Restart", GUILayout.Height(50), GUILayout.Width(90)))
            {
                _manager.RestartLevel();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawGameplayButtons()
        {
            float screenW = Screen.width < 800 ? 800 : Screen.width;
            float screenH = Screen.height < 600 ? 600 : Screen.height;

            float areaW = 540;
            float areaH = 120;
            float posX = (screenW - areaW) * 0.5f;
            float posY = screenH * 0.45f;

            GUILayout.BeginArea(new Rect(posX, posY, areaW, areaH));
            GUILayout.BeginHorizontal();

            // Nút Thắng (Win)
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
            if (GUILayout.Button("🏆 BẤM ĐỂ THẮNG\n(WIN LEVEL)", _winButtonStyle, GUILayout.Width(255), GUILayout.Height(100)))
            {
                _manager.WinLevel();
            }

            GUILayout.Space(25);

            // Nút Thua (Lose)
            GUI.backgroundColor = new Color(0.95f, 0.3f, 0.3f);
            if (GUILayout.Button("💀 BẤM ĐỂ THUA\n(LOSE LEVEL)", _loseButtonStyle, GUILayout.Width(255), GUILayout.Height(100)))
            {
                _manager.LoseLevel();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawResultPanel()
        {
            float screenW = Screen.width < 800 ? 800 : Screen.width;
            float screenH = Screen.height < 600 ? 600 : Screen.height;

            // Nền làm mờ toàn màn hình
            GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none);

            // Khung Panel Modal
            float panelW = 460;
            float panelH = 340;
            float posX = (screenW - panelW) * 0.5f;
            float posY = (screenH - panelH) * 0.5f;

            GUILayout.BeginArea(new Rect(posX, posY, panelW, panelH), _panelBgStyle);
            GUILayout.BeginVertical();

            bool isWon = _manager.State == LevelState.Won;

            // Tiêu đề Panel
            if (isWon)
            {
                GUILayout.Label("🎉 CHIẾN THẮNG!", _panelTitleWinStyle);
                GUILayout.Label($"Bạn đã vượt qua {_manager.MissionId} thành công!", _statsLabelStyle);
            }
            else
            {
                GUILayout.Label("💀 THẤT BẠI!", _panelTitleLoseStyle);
                GUILayout.Label($"Bạn đã bị hạ gục ở {_manager.MissionId}!", _statsLabelStyle);
            }

            GUILayout.Space(12);

            // Thống kê kết quả
            GUILayout.BeginVertical("box");
            GUILayout.Label($"⏱️ Thời gian màn này: <b>{_manager.PlayTime:F1} giây</b>", _statsLabelStyle);

            if (_manager.CurrentStats != null)
            {
                var s = _manager.CurrentStats;
                GUILayout.Label($"📊 Tỉ lệ thắng của bạn: <b>{s.WinRate:F1}%</b> (Thắng: {s.completed} | Thua: {s.failed})", _statsLabelStyle);
                if (s.bestTime > 0)
                    GUILayout.Label($"⚡ Kỷ lục thời gian tốt nhất: <b>{s.bestTime:F1}s</b>", _statsLabelStyle);
            }
            else
            {
                GUILayout.Label("⏳ Đang đồng bộ thống kê từ Firebase Firestore...", _miniLabelStyle);
            }
            GUILayout.EndVertical();

            GUILayout.Space(16);

            // Các nút hành động Next / Restart
            GUILayout.BeginHorizontal();

            if (isWon)
            {
                // Nút Chơi Tiếp (Next Level)
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
                if (GUILayout.Button("▶️ MÀN TIẾP THEO", _actionButtonStyle, GUILayout.Height(52)))
                {
                    _manager.NextLevel();
                }

                GUILayout.Space(10);

                // Nút Chơi Lại (Restart)
                GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
                if (GUILayout.Button("🔄 CHƠI LẠI", _actionButtonStyle, GUILayout.Height(52)))
                {
                    _manager.RestartLevel();
                }
            }
            else
            {
                // Nút Thử Lại (Restart)
                GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
                if (GUILayout.Button("🔄 THỬ LẠI MÀN NÀY", _actionButtonStyle, GUILayout.Height(52)))
                {
                    _manager.RestartLevel();
                }

                GUILayout.Space(10);

                // Nút Bỏ Qua (Skip to next)
                GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f);
                if (GUILayout.Button("⏭️ BỎ QUA", _actionButtonStyle, GUILayout.Height(52)))
                {
                    _manager.NextLevel();
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawBottomStatusBar()
        {
            float screenW = Screen.width < 800 ? 800 : Screen.width;
            float screenH = Screen.height < 600 ? 600 : Screen.height;

            string playerId = AnalyticsManager.Instance != null ? AnalyticsManager.Instance.GetPlayerId() : "Chưa khởi tạo";
            int queueCount  = AnalyticsManager.Instance != null ? AnalyticsManager.Instance.PendingQueueCount : 0;

            GUILayout.BeginArea(new Rect(15, screenH - 45, screenW - 30, 35), "box");
            GUILayout.BeginHorizontal();

            GUI.color = new Color(0.4f, 1f, 0.6f);
            GUILayout.Label($"🔥 Firebase Player: {playerId}", _miniLabelStyle);
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Hàng đợi offline: {queueCount} | Trạng thái: {_manager.LastStatusMessage}", _miniLabelStyle);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (_stylesReady) return;

            _hudBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(15, 15, 12, 12)
            };

            _levelTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 1f, 0.7f) }
            };

            _winButtonStyle = new GUIStyle("button")
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _loseButtonStyle = new GUIStyle("button")
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _panelBgStyle = new GUIStyle("box")
            {
                padding = new RectOffset(25, 25, 20, 20)
            };

            _panelTitleWinStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 1f, 0.4f) }
            };

            _panelTitleLoseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.3f, 0.3f) }
            };

            _actionButtonStyle = new GUIStyle("button")
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _statsLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true,
                normal = { textColor = Color.white }
            };

            _miniLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _stylesReady = true;
        }
    }
}
