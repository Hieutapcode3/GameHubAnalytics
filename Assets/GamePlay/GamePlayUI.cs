using UnityEngine;
using GameHub.Analytics;

namespace GamePlay
{
    /// <summary>
    /// Giao diện điều khiển Gameplay & Test Analytics được tối ưu 100% cho màn hình dọc (Portrait Mobile):
    ///   - Hệ thống Canvas ảo tỉ lệ chuẩn màn dọc (Reference Width = 640px) tự động co giãn trên mọi độ phân giải.
    ///   - HUD: Xem Level hiện tại, Đồng hồ đếm giây, Lưới nút chọn màn 1-6 xếp 2 dòng gọn gàng.
    ///   - Action Buttons: Nút WIN và LOSE to bản xếp dọc, tối ưu cho thao tác bấm 1 ngón tay trên điện thoại.
    ///   - Result Panel (Win/Lose Modal): Popup canh giữa màn hình dọc với thống kê Firebase và các nút Next/Restart.
    ///   - Bottom Status Bar: Hiển thị ID máy test và trạng thái gửi dữ liệu Firestore.
    /// </summary>
    [RequireComponent(typeof(LevelManager))]
    public class GamePlayUI : MonoBehaviour
    {
        private LevelManager _manager;

        [Header("Tùy Chỉnh Kích Thước")]
        [Tooltip("Hệ số phóng to/thu nhỏ giao diện")]
        [SerializeField] private float uiScale = 1.0f;

        // Texture nền mờ cho popup
        private Texture2D _backdropTex;

        // Styles cache
        private bool _stylesReady = false;
        private GUIStyle _hudBoxStyle;
        private GUIStyle _levelTitleStyle;
        private GUIStyle _timerStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _levelSelectBtnStyle;
        private GUIStyle _winButtonStyle;
        private GUIStyle _loseButtonStyle;
        private GUIStyle _restartButtonStyle;
        private GUIStyle _panelBgStyle;
        private GUIStyle _panelTitleWinStyle;
        private GUIStyle _panelTitleLoseStyle;
        private GUIStyle _actionButtonGreen;
        private GUIStyle _actionButtonYellow;
        private GUIStyle _actionButtonRed;
        private GUIStyle _actionButtonGrey;
        private GUIStyle _statsLabelStyle;
        private GUIStyle _miniLabelStyle;
        private GUIStyle _statusBoxStyle;

        private void Awake()
        {
            _manager = GetComponent<LevelManager>();
        }

        private void OnGUI()
        {
            if (_manager == null) _manager = LevelManager.Instance;
            if (_manager == null) return;

            InitStyles();

            // ── Tự động tính toán ma trận tỉ lệ theo màn hình dọc (Reference Width = 640px) ──
            float refWidth = 640f;
            float scale = (Screen.width / refWidth) * Mathf.Max(0.5f, uiScale);

            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float virtualW = refWidth;
            float virtualH = Screen.height / scale;

            // 1. HUD trên cùng
            DrawTopHUD(virtualW, virtualH);

            // 2. Các nút Gameplay chính (Win / Lose) khi đang chơi
            if (_manager.State == LevelState.Playing)
            {
                DrawGameplayButtons(virtualW, virtualH);
            }

            // 3. Panel Kết Quả (Popup khi Thắng hoặc Thua)
            if (_manager.State == LevelState.Won || _manager.State == LevelState.Lost)
            {
                DrawResultPanel(virtualW, virtualH);
            }

            // 4. Thanh trạng thái ở đáy màn hình
            DrawBottomStatusBar(virtualW, virtualH);

            GUI.matrix = oldMatrix;
        }

        // ─────────────────────────────────────────────────────────
        //  1. Top HUD (Màn Dọc)
        // ─────────────────────────────────────────────────────────

        private void DrawTopHUD(float virtualW, float virtualH)
        {
            float pad = 20f;
            float hudW = virtualW - (pad * 2f);
            float hudH = 220f;

            GUILayout.BeginArea(new Rect(pad, 20f, hudW, hudH), _hudBoxStyle);
            GUILayout.BeginVertical();

            // Tiêu đề Level to rõ ràng
            GUILayout.Label($"🎮 MÀN CHƠI: LEVEL {_manager.CurrentLevel:D2}", _levelTitleStyle);
            GUILayout.Label($"⏱️ Thời gian: {_manager.PlayTime:F1}s", _timerStyle);

            GUILayout.Space(8);
            GUILayout.Label("Chọn màn chơi nhanh:", _sectionHeaderStyle);

            // Lưới chọn màn chơi: 2 hàng x 3 cột (tối ưu cho chiều rộng màn hình dọc)
            float btnW = (hudW - 40f) / 3f;
            float btnH = 40f;

            // Hàng 1: Màn 1, 2, 3
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 3; i++)
                DrawLevelButton(i, btnW, btnH);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Hàng 2: Màn 4, 5, 6
            GUILayout.BeginHorizontal();
            for (int i = 4; i <= 6; i++)
                DrawLevelButton(i, btnW, btnH);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawLevelButton(int level, float width, float height)
        {
            bool isCurrent = _manager.CurrentLevel == level;
            GUI.backgroundColor = isCurrent ? new Color(0.2f, 0.8f, 1f) : new Color(0.9f, 0.9f, 0.95f);
            if (GUILayout.Button($"Màn {level}", _levelSelectBtnStyle, GUILayout.Width(width), GUILayout.Height(height)))
            {
                _manager.LoadLevel(level);
            }
            GUI.backgroundColor = Color.white;
        }

        // ─────────────────────────────────────────────────────────
        //  2. Gameplay Buttons (Màn Dọc - Xếp Dọc Dễ Bấm Ngón Cái)
        // ─────────────────────────────────────────────────────────

        private void DrawGameplayButtons(float virtualW, float virtualH)
        {
            float pad = 25f;
            float areaW = virtualW - (pad * 2f);
            float areaH = 340f;
            float posY = virtualH * 0.40f;

            GUILayout.BeginArea(new Rect(pad, posY, areaW, areaH));
            GUILayout.BeginVertical();

            GUILayout.Label("CHỌN KẾT QUẢ ĐỂ BẮN EVENT LÊN FIREBASE:", _sectionHeaderStyle);
            GUILayout.Space(12);

            // Nút Thắng (Vivid Green) - To bản, nằm vừa tầm với ngón tay cái
            GUI.backgroundColor = new Color(0.2f, 0.88f, 0.38f);
            if (GUILayout.Button("🏆 BẤM ĐỂ THẮNG\n(WIN LEVEL)", _winButtonStyle, GUILayout.Height(95)))
            {
                _manager.WinLevel();
            }

            GUILayout.Space(16);

            // Nút Thua (Vivid Red)
            GUI.backgroundColor = new Color(0.98f, 0.3f, 0.3f);
            if (GUILayout.Button("💀 BẤM ĐỂ THUA\n(LOSE LEVEL)", _loseButtonStyle, GUILayout.Height(95)))
            {
                _manager.LoseLevel();
            }

            GUILayout.Space(14);

            // Nút Chơi Lại (Restart)
            GUI.backgroundColor = new Color(1f, 0.78f, 0.28f);
            if (GUILayout.Button("🔄 CHƠI LẠI MÀN NÀY (RESTART)", _restartButtonStyle, GUILayout.Height(52)))
            {
                _manager.RestartLevel();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ─────────────────────────────────────────────────────────
        //  3. Result Panel Modal (Màn Dọc Canh Giữa)
        // ─────────────────────────────────────────────────────────

        private void DrawResultPanel(float virtualW, float virtualH)
        {
            // Nền tối mờ toàn màn hình
            if (_backdropTex == null)
            {
                _backdropTex = new Texture2D(1, 1);
                _backdropTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.82f));
                _backdropTex.Apply();
            }
            GUI.DrawTexture(new Rect(0, 0, virtualW, virtualH), _backdropTex);

            // Modal Card canh giữa
            float panelW = virtualW - 50f;
            float panelH = 500f;
            float posX = 25f;
            float posY = Mathf.Max(30f, (virtualH - panelH) * 0.5f);

            GUILayout.BeginArea(new Rect(posX, posY, panelW, panelH), _panelBgStyle);
            GUILayout.BeginVertical();

            bool isWon = _manager.State == LevelState.Won;

            GUILayout.Space(10);

            // Tiêu đề & Thông điệp
            if (isWon)
            {
                GUILayout.Label("🎉 CHIẾN THẮNG!", _panelTitleWinStyle);
                GUILayout.Label($"Bạn đã vượt qua {_manager.MissionId} thành công!", _sectionHeaderStyle);
            }
            else
            {
                GUILayout.Label("💀 THẤT BẠI!", _panelTitleLoseStyle);
                GUILayout.Label($"Bạn đã bị hạ gục ở {_manager.MissionId}!", _sectionHeaderStyle);
            }

            GUILayout.Space(14);

            // Khung Thống Kê Chi Tiết
            GUILayout.BeginVertical("box");
            GUILayout.Space(6);
            GUILayout.Label($"⏱️ Thời gian màn này: <b>{_manager.PlayTime:F1} giây</b>", _statsLabelStyle);
            GUILayout.Space(4);

            if (_manager.CurrentStats != null)
            {
                var s = _manager.CurrentStats;
                GUILayout.Label($"📊 Tỉ lệ thắng của bạn: <b>{s.WinRate:F1}%</b>", _statsLabelStyle);
                GUILayout.Label($"🎯 Lịch sử: Thắng <b>{s.completed}</b> | Thua <b>{s.failed}</b> (Tổng: {s.started})", _statsLabelStyle);
                if (s.bestTime > 0)
                    GUILayout.Label($"⚡ Kỷ lục nhanh nhất: <b>{s.bestTime:F1}s</b>", _statsLabelStyle);
            }
            else
            {
                bool isEditorFilter = Application.isEditor && (AnalyticsManager.Instance?.Config?.mobileOnlyForWinLose ?? true);
                if (isEditorFilter)
                {
                    GUILayout.Label("<color=#FFB74D>⚡ Unity Editor: Thắng/Thua được giữ nội bộ (Chỉ gửi Firebase trên Mobile/Giả lập)</color>", _miniLabelStyle);
                }
                else
                {
                    GUILayout.Label("⏳ Đang đồng bộ số liệu từ Firebase Firestore...", _miniLabelStyle);
                }
            }
            GUILayout.Space(6);
            GUILayout.EndVertical();

            GUILayout.Space(20);

            // Nút bấm hành động xếp dọc lớn để dễ ấn trên màn hình điện thoại
            if (isWon)
            {
                // Nút Chơi Tiếp (Màn sau)
                GUI.backgroundColor = new Color(0.2f, 0.88f, 0.38f);
                if (GUILayout.Button("▶️ MÀN TIẾP THEO (NEXT LEVEL)", _actionButtonGreen, GUILayout.Height(65)))
                {
                    _manager.NextLevel();
                }

                GUILayout.Space(10);

                // Nút Chơi Lại
                GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
                if (GUILayout.Button("🔄 CHƠI LẠI MÀN NÀY", _actionButtonYellow, GUILayout.Height(55)))
                {
                    _manager.RestartLevel();
                }
            }
            else
            {
                // Nút Thử Lại
                GUI.backgroundColor = new Color(0.98f, 0.35f, 0.35f);
                if (GUILayout.Button("🔄 THỬ LẠI MÀN NÀY (RESTART)", _actionButtonRed, GUILayout.Height(65)))
                {
                    _manager.RestartLevel();
                }

                GUILayout.Space(10);

                // Nút Bỏ Qua
                GUI.backgroundColor = new Color(0.7f, 0.72f, 0.78f);
                if (GUILayout.Button("⏭️ BỎ QUA SANG MÀN TIẾP", _actionButtonGrey, GUILayout.Height(55)))
                {
                    _manager.NextLevel();
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ─────────────────────────────────────────────────────────
        //  4. Bottom Status Bar (Màn Dọc)
        // ─────────────────────────────────────────────────────────

        private void DrawBottomStatusBar(float virtualW, float virtualH)
        {
            float pad = 20f;
            float barW = virtualW - (pad * 2f);
            float barH = 80f;
            float posY = virtualH - barH - 15f;

            GUILayout.BeginArea(new Rect(pad, posY, barW, barH), _statusBoxStyle);
            GUILayout.BeginVertical();

            string playerId = AnalyticsManager.Instance != null ? AnalyticsManager.Instance.GetPlayerId() : "Chưa khởi tạo";
            int queueCount  = AnalyticsManager.Instance != null ? AnalyticsManager.Instance.PendingQueueCount : 0;

            bool isEditor = Application.isEditor;
            bool filterOn = AnalyticsManager.Instance != null && AnalyticsManager.Instance.Config != null && AnalyticsManager.Instance.Config.mobileOnlyForWinLose;
            string platformTag = isEditor 
                ? (filterOn ? "<color=#FFA726>[Editor: Win/Lose Firebase OFF]</color>" : "<color=#42A5F5>[Editor: Live]</color>")
                : "<color=#66BB6A>[Mobile/Emulator: Live Firebase]</color>";

            GUI.color = Color.white;
            GUILayout.Label($"🔥 Device: {playerId}  {platformTag}", _miniLabelStyle);

            GUILayout.Label($"📦 Offline Queue: {queueCount} | {_manager.LastStatusMessage}", _miniLabelStyle);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ─────────────────────────────────────────────────────────
        //  Styles Initialization
        // ─────────────────────────────────────────────────────────

        private void InitStyles()
        {
            if (_stylesReady) return;

            _hudBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(16, 16, 14, 14)
            };

            _statusBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(14, 14, 10, 10)
            };

            _levelTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.35f, 1f, 0.75f) }
            };

            _sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.85f, 0.88f, 0.95f) }
            };

            _levelSelectBtnStyle = new GUIStyle("button")
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _winButtonStyle = new GUIStyle("button")
            {
                fontSize  = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _loseButtonStyle = new GUIStyle("button")
            {
                fontSize  = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _restartButtonStyle = new GUIStyle("button")
            {
                fontSize  = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _panelBgStyle = new GUIStyle("box")
            {
                padding = new RectOffset(24, 24, 20, 20)
            };

            _panelTitleWinStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.2f, 1f, 0.45f) }
            };

            _panelTitleLoseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 0.32f, 0.32f) }
            };

            _actionButtonGreen = new GUIStyle("button")
            {
                fontSize  = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _actionButtonYellow = new GUIStyle("button")
            {
                fontSize  = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _actionButtonRed = new GUIStyle("button")
            {
                fontSize  = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _actionButtonGrey = new GUIStyle("button")
            {
                fontSize  = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _statsLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 15,
                richText  = true,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = Color.white }
            };

            _miniLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal   = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            _stylesReady = true;
        }
    }
}
