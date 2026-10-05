using UnityEditor;
using UnityEngine;
using System.IO;

namespace GameHub.Analytics.Editor
{
    public static class PackageExporter
    {
        private const string PACKAGE_ASSETS_PATH = "Assets/GameHubAnalytics";
        private const string PACKAGE_NAME        = "GameHubAnalytics";
        private const string PACKAGE_VERSION     = "1.0.0";

        [MenuItem("GameHub/Analytics/📦 Export .unitypackage")]
        public static void ExportPackage()
        {
            if (!AssetDatabase.IsValidFolder(PACKAGE_ASSETS_PATH))
            {
                EditorUtility.DisplayDialog(
                    "Export Failed",
                    $"Không tìm thấy thư mục '{PACKAGE_ASSETS_PATH}'.\n" +
                    "Đảm bảo package đã được tạo đúng cấu trúc.",
                    "OK");
                return;
            }

            // Default save path: Desktop
            string defaultDir  = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            string defaultName = $"{PACKAGE_NAME}_v{PACKAGE_VERSION}.unitypackage";

            string savePath = EditorUtility.SaveFilePanel(
                "Xuất GameHub Analytics Package",
                defaultDir,
                defaultName,
                "unitypackage");

            if (string.IsNullOrEmpty(savePath)) return;

            try
            {
                AssetDatabase.ExportPackage(
                    PACKAGE_ASSETS_PATH,
                    savePath,
                    ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

                Debug.Log($"[Analytics] ✅ Package exported → {savePath}");

                bool openFolder = EditorUtility.DisplayDialog(
                    "Export Thành Công! ✅",
                    $"Package đã xuất:\n{Path.GetFileName(savePath)}\n\n" +
                    $"Phiên bản: v{PACKAGE_VERSION}\n" +
                    $"Thư mục: {Path.GetDirectoryName(savePath)}\n\n" +
                    "Mở thư mục chứa file không?",
                    "Mở thư mục", "Đóng");

                if (openFolder)
                    EditorUtility.RevealInFinder(savePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Analytics] Export thất bại: {e.Message}");
                EditorUtility.DisplayDialog("Export Thất Bại", $"Lỗi: {e.Message}", "OK");
            }
        }

        [MenuItem("GameHub/Analytics/⚙️ Create Analytics Config")]
        public static void CreateAnalyticsConfig()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            string assetPath = "Assets/Resources/AnalyticsConfig.asset";

            if (AssetDatabase.LoadAssetAtPath<AnalyticsConfig>(assetPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "File đã tồn tại",
                    $"AnalyticsConfig.asset đã tồn tại tại:\n{assetPath}\n\nBạn có muốn chọn nó không?",
                    "Chọn file", "Hủy");

                if (overwrite)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<AnalyticsConfig>(assetPath);
                    Selection.activeObject = existing;
                    EditorGUIUtility.PingObject(existing);
                }
                return;
            }

            var config = ScriptableObject.CreateInstance<AnalyticsConfig>();
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log($"[Analytics] ✅ AnalyticsConfig created at {assetPath}");
            EditorUtility.DisplayDialog(
                "Tạo Thành Công! ✅",
                $"AnalyticsConfig.asset đã tạo tại:\n{assetPath}\n\n" +
                "Điền API Key và Project ID từ Firebase Console để bắt đầu.",
                "OK");
        }

        [MenuItem("GameHub/Analytics/ℹ️ About")]
        public static void ShowAbout()
        {
            EditorUtility.DisplayDialog(
                "GameHub Analytics  v" + PACKAGE_VERSION,
                "Package analytics nội bộ kết nối Firebase Firestore.\n\n" +
                "Tính năng:\n" +
                "  • Log mission events (start / complete / fail)\n" +
                "  • Offline queue với auto-retry\n" +
                "  • Editor Dashboard xem thống kê\n" +
                "  • Không cần Firebase Unity SDK\n\n" +
                "Kết nối: Firebase Firestore REST API\n" +
                "Liên hệ: GameHub Team",
                "OK");
        }
    }
}
