using System;
using System.IO;
using Newtonsoft.Json;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 远程模板与LOT号来源配置
    /// </summary>
    public class RemoteSourceConfig
    {
        private static readonly string ConfigFileName = "RemoteSourceConfig.json";
        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", ConfigFileName);

        /// <summary>
        /// 是否启用远程来源监控
        /// </summary>
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// LOT号文件路径（例如: \\\\192.168.1.100\\shared\\lot.txt 或 ..\\..\\lot.txt）
        /// </summary>
        public string LotFilePath { get; set; } = "";

        /// <summary>
        /// 模板名文件路径（例如: \\\\192.168.1.100\\shared\\template.txt 或 ..\\..\\template.txt）
        /// </summary>
        public string TemplateFilePath { get; set; } = "";

        /// <summary>
        /// 文件检测间隔（毫秒），默认5000ms
        /// </summary>
        public int CheckIntervalMs { get; set; } = 5000;

        /// <summary>
        /// 上次读取的LOT号（用于检测变更）
        /// </summary>
        [JsonIgnore]
        public string LastLotValue { get; set; } = "";

        /// <summary>
        /// 上次读取的模板名（用于检测变更）
        /// </summary>
        [JsonIgnore]
        public string LastTemplateName { get; set; } = "";

        /// <summary>
        /// 保存配置到文件
        /// </summary>
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
                LogManager.Info($"远程来源配置已保存: 启用={IsEnabled}, LOT路径={LotFilePath}, 模板路径={TemplateFilePath}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存远程来源配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static RemoteSourceConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonConvert.DeserializeObject<RemoteSourceConfig>(json);
                    LogManager.Info($"远程来源配置已加载: 启用={config.IsEnabled}, LOT路径={config.LotFilePath}, 模板路径={config.TemplateFilePath}");
                    return config ?? new RemoteSourceConfig();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载远程来源配置失败: {ex.Message}");
            }

            return new RemoteSourceConfig();
        }
    }
}
