using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GamePlay;

namespace GamePlay.Editor
{
    /// <summary>
    /// Công cụ hỗ trợ tạo và thiết lập GamePlayController trong Scene với 1 click.
    /// </summary>
    public static class GamePlaySceneSetup
    {
        [MenuItem("GameHub/🎮 Setup GamePlay In Current Scene", priority = 1)]
        public static void SetupCurrentScene()
        {
            var existing = Object.FindAnyObjectByType<LevelManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                EditorUtility.DisplayDialog("GamePlay Đã Có Sẵn",
                    $"GamePlayController đã có sẵn trong Scene ({existing.gameObject.name})!\n\n" +
                    "Bạn chỉ cần bấm nút ▶️ Play để test.", "OK");
                return;
            }

            var go = new GameObject("GamePlayController");
            go.AddComponent<LevelManager>();
            go.AddComponent<GamePlayUI>();

            Undo.RegisterCreatedObjectUndo(go, "Create GamePlayController");
            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorSceneManager.SaveScene(go.scene);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            Debug.Log("<color=#2EA3FF>[GamePlay]</color> ✅ Đã tạo GamePlayController thành công trong Scene!");
            EditorUtility.DisplayDialog("Setup GamePlay Thành Công! 🎮",
                "Đã tạo thành công 'GamePlayController' trong Scene!\n\n" +
                "Các tính năng có sẵn khi bạn bấm ▶️ Play:\n" +
                "1. Tự động kết nối GameHub Analytics & Firebase\n" +
                "2. HUD hiển thị Màn chơi, Timer, Nút chọn level nhanh\n" +
                "3. Nút WIN và LOSE để test bắn sự kiện hoàn thành/thất bại\n" +
                "4. Panel Kết Quả (Popup) hiển thị thông số và các nút 'Next Level' / 'Restart'",
                "Tuyệt Vời");
        }
    }
}
