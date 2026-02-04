using System;
using WpfApp2.Models;

namespace WpfApp2.ThreeD
{
    /// <summary>
    /// 3D runtime settings stored in main process. This is Keyence-agnostic and can be used even when 3D is disabled.
    /// </summary>
    public static class ThreeDSettings
    {
        public static Detection3DParameters CurrentDetection3DParams { get; private set; } = new Detection3DParameters();

        // Mirrors the old flag used to split 3D callback behavior (image test vs production).
        public static bool IsInImageTestMode { get; set; }

        /// <summary>
        /// 3D shielding switch for "run without 3D dongle / SDK".
        /// Set env var `SLIDE_DISABLE_3D=1` (or true/yes) to force-disable 3D in main process.
        /// </summary>
        public static bool Is3DShielded
        {
            get
            {
                return IsEnvTrue("SLIDE_DISABLE_3D") || IsEnvTrue("SLIDE_3D_SHIELD");
            }
        }

        public static bool Is3DDetectionEnabledEffective =>
            (CurrentDetection3DParams?.Enable3DDetection ?? false) && !Is3DShielded;

        public static void LoadFromTemplate(TemplateParameters templateParams)
        {
            if (templateParams?.Detection3DParams == null)
            {
                CurrentDetection3DParams = new Detection3DParameters();
                return;
            }

            CurrentDetection3DParams = new Detection3DParameters
            {
                Enable3DDetection = templateParams.Detection3DParams.Enable3DDetection,
                ProjectName = templateParams.Detection3DParams.ProjectName,
                ProjectFolder = templateParams.Detection3DParams.ProjectFolder,
                HeightImagePath = templateParams.Detection3DParams.HeightImagePath,
                ReCompile = templateParams.Detection3DParams.ReCompile
            };
        }

        public static void ApplyToTemplate(TemplateParameters templateParams)
        {
            if (templateParams == null)
            {
                return;
            }

            templateParams.Detection3DParams = new Detection3DParameters
            {
                Enable3DDetection = CurrentDetection3DParams.Enable3DDetection,
                ProjectName = CurrentDetection3DParams.ProjectName,
                ProjectFolder = CurrentDetection3DParams.ProjectFolder,
                HeightImagePath = CurrentDetection3DParams.HeightImagePath,
                ReCompile = CurrentDetection3DParams.ReCompile
            };
        }

        private static bool IsEnvTrue(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) return false;

            value = value.Trim();
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
