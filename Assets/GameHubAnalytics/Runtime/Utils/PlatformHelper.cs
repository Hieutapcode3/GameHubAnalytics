using UnityEngine;

namespace GameHub.Analytics
{
    /// <summary>
    /// Utility tự động detect platform đang chạy và trả về tên chuỗi thân thiện.
    /// </summary>
    public static class PlatformHelper
    {
        /// <summary>Trả về tên platform hiện tại dưới dạng string.</summary>
        public static string GetPlatformName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return "Android";

                case RuntimePlatform.IPhonePlayer:
                    return "iOS";

                case RuntimePlatform.WindowsPlayer:
                    return "Windows";
                case RuntimePlatform.WindowsEditor:
                    return "Windows_Editor";

                case RuntimePlatform.OSXPlayer:
                    return "macOS";
                case RuntimePlatform.OSXEditor:
                    return "macOS_Editor";

                case RuntimePlatform.LinuxPlayer:
                    return "Linux";
                case RuntimePlatform.LinuxEditor:
                    return "Linux_Editor";

                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";

                case RuntimePlatform.PS4:
                    return "PS4";
                case RuntimePlatform.PS5:
                    return "PS5";

                case RuntimePlatform.XboxOne:
                    return "XboxOne";

                case RuntimePlatform.Switch:
                    return "Switch";

                default:
                    return Application.platform.ToString();
            }
        }

        /// <summary>Kiểm tra đang chạy trong Unity Editor không.</summary>
        public static bool IsEditor =>
            Application.platform == RuntimePlatform.WindowsEditor ||
            Application.platform == RuntimePlatform.OSXEditor     ||
            Application.platform == RuntimePlatform.LinuxEditor;

        /// <summary>Kiểm tra đang chạy trên mobile không.</summary>
        public static bool IsMobile =>
            Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer;
    }
}
