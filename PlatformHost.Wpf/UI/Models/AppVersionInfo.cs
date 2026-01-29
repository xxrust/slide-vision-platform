using System;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 应用版本信息（统一管理，避免各处版本号不一致）
    /// </summary>
    public static class AppVersionInfo
    {
        /// <summary>
        /// 软件版本号（显示用）
        /// </summary>
        public const string SoftwareVersion = "V3.1.0";

        /// <summary>
        /// 启动界面英文标题
        /// </summary>
        public const string SplashProductName = "Glue Inspection System";

        public static string GetSoftwareVersion()
        {
            return SoftwareVersion;
        }

        public static string GetSplashVersionText()
        {
            return $"{SplashProductName} {SoftwareVersion}";
        }
    }
}
