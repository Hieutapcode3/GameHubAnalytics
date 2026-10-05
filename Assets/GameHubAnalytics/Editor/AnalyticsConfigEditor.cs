using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GameHub.Analytics.Editor
{
    [CustomEditor(typeof(AnalyticsConfig))]
    public class AnalyticsConfigEditor : UnityEditor.Editor
    {
        private string _statusMessage  = "";
        private bool   _statusIsError  = false;
        private bool   _statusIsGood   = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var config = (AnalyticsConfig)target;

            EditorGUILayout.Space(6);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 14,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("🎮 GameHub Analytics", titleStyle);
            EditorGUILayout.LabelField("Firebase Firestore — REST API", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
            EditorGUILayout.Space(6);

            if (!config.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "⚠️  Cấu hình chưa đầy đủ!\n" +
                    "Hãy điền API Key và Project ID từ Firebase Console.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "✅  Cấu hình hợp lệ. Sẵn sàng kết nối Firebase Firestore.",
                    MessageType.Info);
            }
            EditorGUILayout.Space(4);

            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("🔧  Công cụ kiểm tra", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = config.IsValid;
            if (GUILayout.Button("🔗  Test Connection", GUILayout.Height(32)))
                TestConnectionAsync(config);
            GUI.enabled = true;

            if (GUILayout.Button("📤  Send Test Event", GUILayout.Height(32)))
                SendTestEvent(config);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📊  Open Dashboard", GUILayout.Height(28)))
                AnalyticsDashboardWindow.ShowWindow();

            if (GUILayout.Button("🌐  Firebase Console", GUILayout.Height(28)))
                Application.OpenURL("https://console.firebase.google.com");

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(6);
                var msgType = _statusIsError ? MessageType.Error
                            : _statusIsGood  ? MessageType.Info
                                             : MessageType.Warning;
                EditorGUILayout.HelpBox(_statusMessage, msgType);
            }

            if (config.IsValid)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("📁  Firestore Path Preview", EditorStyles.boldLabel);

                var pathStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    fontStyle = FontStyle.Italic
                };
                EditorGUILayout.LabelField(
                    $"analytics/{config.gameId}/missions/{{missionId}}/events/{{autoId}}",
                    pathStyle);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void TestConnectionAsync(AnalyticsConfig config)
        {
            _statusMessage = "⏳  Đang kiểm tra kết nối...";
            _statusIsError = false;
            _statusIsGood  = false;
            Repaint();

            string url = $"{config.FirestoreBaseUrl}/analytics?key={config.apiKey}&pageSize=1";
            var request = UnityWebRequest.Get(url);
            request.timeout = 10;

            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                bool ok = request.result == UnityWebRequest.Result.Success
                       || request.responseCode == 404;

                _statusIsGood  = ok;
                _statusIsError = !ok;
                _statusMessage = ok
                    ? $"✅  Kết nối thành công! (HTTP {request.responseCode})\nProject: {config.projectId}"
                    : $"❌  Kết nối thất bại: {request.error} (HTTP {request.responseCode})";

                request.Dispose();
                Repaint();
            };
        }

        private void SendTestEvent(AnalyticsConfig config)
        {
            if (!Application.isPlaying)
            {
                _statusMessage = "⚠️  Vào Play Mode trước để gửi test event.";
                _statusIsError = false;
                _statusIsGood  = false;
                Repaint();
                return;
            }

            if (!config.IsValid)
            {
                _statusMessage = "❌  Config chưa hợp lệ. Điền API Key và Project ID trước.";
                _statusIsError = true;
                Repaint();
                return;
            }

            AnalyticsManager.Instance.Initialize();
            AnalyticsManager.Instance.LogCustomEvent(
                "editor_test",
                "test_mission",
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "source",    "unity_editor"                        },
                    { "timestamp", System.DateTime.UtcNow.ToString("o")  }
                }
            );

            _statusMessage = "📤  Test event đã gửi! Xem Console để biết kết quả.";
            _statusIsGood  = true;
            _statusIsError = false;
            Repaint();
        }
    }
}
