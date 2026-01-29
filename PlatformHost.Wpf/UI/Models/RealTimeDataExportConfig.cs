using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WpfApp2.UI.Models
{
    public enum RealTimeDataExportMode
    {
        Default = 0,
        Custom = 1
    }

    public enum RealTimeDataExportColumnKind
    {
        Meta = 0,
        Item = 1
    }

    public enum RealTimeDataExportMetaField
    {
        ImageNumber = 0,
        Timestamp = 1,
        LotNumber = 2,
        DefectType = 3,
        Result = 4
    }

    public enum RealTimeDataExportItemField
    {
        Value = 0,
        LowerLimit = 1,
        UpperLimit = 2,
        IsOutOfRange = 3
    }

    public sealed class RealTimeDataExportColumn
    {
        [JsonProperty(Order = 1)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RealTimeDataExportColumnKind Kind { get; set; }

        [JsonProperty(Order = 2)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RealTimeDataExportMetaField? MetaField { get; set; }

        [JsonProperty(Order = 3)]
        public string ItemName { get; set; }

        [JsonProperty(Order = 4)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RealTimeDataExportItemField? ItemField { get; set; }
    }

    public sealed class RealTimeDataExportTemplate
    {
        [JsonProperty(Order = 1)]
        public string Name { get; set; }

        [JsonProperty(Order = 2)]
        public List<RealTimeDataExportColumn> Columns { get; set; } = new List<RealTimeDataExportColumn>();
    }

    public sealed class RealTimeDataExportConfig
    {
        [JsonProperty(Order = 1)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RealTimeDataExportMode Mode { get; set; } = RealTimeDataExportMode.Default;

        [JsonProperty(Order = 2)]
        public string ActiveTemplateName { get; set; } = "自定义模板1";

        [JsonProperty(Order = 3)]
        public List<RealTimeDataExportTemplate> Templates { get; set; } = new List<RealTimeDataExportTemplate>();
    }

    public static class RealTimeDataExportConfigManager
    {
        private const string ConfigFileName = "RealTimeDataExportConfig.json";

        private static readonly string _baseConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string _baseConfigFilePath = Path.Combine(_baseConfigDir, ConfigFileName);

        private static readonly string _userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfApp2",
            "Config");
        private static readonly string _userConfigFilePath = Path.Combine(_userConfigDir, ConfigFileName);

        private static readonly Lazy<bool> _isBaseConfigDirWritable = new Lazy<bool>(() => CanWriteToDirectory(_baseConfigDir), LazyThreadSafetyMode.ExecutionAndPublication);

        public static string ConfigFilePath => ResolveConfigPathForSave();

        public static RealTimeDataExportConfig Load()
        {
            try
            {
                var configPath = ResolveConfigPathForLoad();
                if (!File.Exists(configPath))
                {
                    var config = CreateDefault();
                    TrySave(config, out _, out _);
                    return config;
                }

                var json = File.ReadAllText(configPath, Encoding.UTF8);
                var configFromFile = JsonConvert.DeserializeObject<RealTimeDataExportConfig>(json);
                var normalized = Normalize(configFromFile);
                return normalized;
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载实时数据导出配置失败: {ex.Message}");
                return CreateDefault();
            }
        }

        public static void Save(RealTimeDataExportConfig config)
        {
            TrySave(config, out _, out _);
        }

        public static bool TrySave(RealTimeDataExportConfig config, out string savedPath, out string errorMessage)
        {
            savedPath = null;
            errorMessage = null;
            try
            {
                var normalized = Normalize(config);
                var targetPath = ResolveConfigPathForSave();
                var configDir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                var json = JsonConvert.SerializeObject(normalized, Formatting.Indented);
                File.WriteAllText(targetPath, json, Encoding.UTF8);
                savedPath = targetPath;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                LogManager.Error($"保存实时数据导出配置失败: {ex.Message}");
                return false;
            }
        }

        public static RealTimeDataExportTemplate GetActiveTemplate(RealTimeDataExportConfig config)
        {
            if (config == null)
            {
                return null;
            }

            var activeName = config.ActiveTemplateName ?? string.Empty;
            var template = config.Templates?.FirstOrDefault(t => string.Equals(t.Name, activeName, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                return template;
            }

            return config.Templates?.FirstOrDefault();
        }

        public static RealTimeDataExportConfig CreateDefault()
        {
            return new RealTimeDataExportConfig
            {
                Mode = RealTimeDataExportMode.Default,
                ActiveTemplateName = "自定义模板1",
                Templates = new List<RealTimeDataExportTemplate>
                {
                    new RealTimeDataExportTemplate
                    {
                        Name = "自定义模板1",
                        Columns = new List<RealTimeDataExportColumn>
                        {
                            new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.ImageNumber },
                            new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Timestamp },
                            new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.LotNumber },
                            new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.DefectType },
                            new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Result }
                        }
                    }
                }
            };
        }

        private static RealTimeDataExportConfig Normalize(RealTimeDataExportConfig config)
        {
            if (config == null)
            {
                return CreateDefault();
            }

            if (config.Templates == null)
            {
                config.Templates = new List<RealTimeDataExportTemplate>();
            }

            foreach (var template in config.Templates)
            {
                if (template.Columns == null)
                {
                    template.Columns = new List<RealTimeDataExportColumn>();
                }
            }

            if (string.IsNullOrWhiteSpace(config.ActiveTemplateName))
            {
                config.ActiveTemplateName = config.Templates.FirstOrDefault()?.Name ?? "自定义模板1";
            }

            if (config.Templates.Count == 0)
            {
                config.Templates = CreateDefault().Templates;
            }

            return config;
        }

        private static string ResolveConfigPathForLoad()
        {
            if (_isBaseConfigDirWritable.Value)
            {
                return _baseConfigFilePath;
            }

            if (File.Exists(_userConfigFilePath))
            {
                return _userConfigFilePath;
            }

            if (File.Exists(_baseConfigFilePath))
            {
                TryMigrateBaseConfigToUserConfig();
                if (File.Exists(_userConfigFilePath))
                {
                    return _userConfigFilePath;
                }

                return _baseConfigFilePath;
            }

            return _userConfigFilePath;
        }

        private static string ResolveConfigPathForSave()
        {
            return _isBaseConfigDirWritable.Value ? _baseConfigFilePath : _userConfigFilePath;
        }

        private static void TryMigrateBaseConfigToUserConfig()
        {
            try
            {
                if (!Directory.Exists(_userConfigDir))
                {
                    Directory.CreateDirectory(_userConfigDir);
                }

                File.Copy(_baseConfigFilePath, _userConfigFilePath, true);
            }
            catch (Exception ex)
            {
                LogManager.Error($"迁移实时数据导出配置失败: {ex.Message}");
            }
        }

        private static bool CanWriteToDirectory(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var testFilePath = Path.Combine(directoryPath, $".__writable_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFilePath, "test", Encoding.UTF8);
                File.Delete(testFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
