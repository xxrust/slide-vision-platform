using System;
using System.IO;
using Newtonsoft.Json;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 3D定标配置（校准位）
    /// </summary>
    public class ThreeDCalibrationConfig
    {
        private const string ConfigFileName = "ThreeDCalibrationConfig.json";

        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", ConfigFileName);

        /// <summary>
        /// 校准位（未配置时为null）
        /// </summary>
        public float? CalibrationPosition { get; set; }

        /// <summary>
        /// 微调步进（gap）
        /// </summary>
        public float Gap { get; set; } = 0.1f;

        public void Save()
        {
            try
            {
                string configDir = Path.GetDirectoryName(ConfigFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
                LogManager.Info($"3D定标配置已保存: CalibrationPosition={CalibrationPosition}, Gap={Gap}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存3D定标配置失败: {ex.Message}");
            }
        }

        public static ThreeDCalibrationConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonConvert.DeserializeObject<ThreeDCalibrationConfig>(json) ?? new ThreeDCalibrationConfig();
                    return config;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载3D定标配置失败: {ex.Message}");
            }

            return new ThreeDCalibrationConfig();
        }
    }
}

