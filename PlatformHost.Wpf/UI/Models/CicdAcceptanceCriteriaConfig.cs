using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace WpfApp2.UI.Models
{
    public sealed class CicdItemToleranceConfig
    {
        [JsonProperty(Order = 1)]
        public string ItemName { get; set; } = string.Empty;

        [JsonProperty(Order = 2)]
        public double ToleranceAbs { get; set; } = 0.0;

        [JsonProperty(Order = 3)]
        public double ToleranceRatio { get; set; } = 0.0;
    }

    public sealed class CicdAcceptanceCriteriaStandard
    {
        [JsonProperty(Order = 1)]
        public string Name { get; set; } = "默认标准";

        [JsonProperty(Order = 2)]
        public int AllowedOkNgMismatchCount { get; set; } = 0;

        [JsonProperty(Order = 3)]
        public int AllowedDefectTypeMismatchCount { get; set; } = 0;

        [JsonProperty(Order = 4)]
        public int AllowedNumericRangeMismatchCount { get; set; } = 0;

        [JsonProperty(Order = 5)]
        public double DefaultNumericToleranceAbs { get; set; } = 0.0;

        [JsonProperty(Order = 6)]
        public double DefaultNumericToleranceRatio { get; set; } = 0.0;

        [JsonProperty(Order = 7)]
        public List<CicdItemToleranceConfig> ItemTolerances { get; set; } = new List<CicdItemToleranceConfig>();

        [JsonIgnore]
        public double NumericRangeToleranceAbs
        {
            get { return DefaultNumericToleranceAbs; }
            set { DefaultNumericToleranceAbs = value; }
        }

        [JsonIgnore]
        public double NumericRangeToleranceRatio
        {
            get { return DefaultNumericToleranceRatio; }
            set { DefaultNumericToleranceRatio = value; }
        }

        // === 兼容旧字段（CICD v1）===
        [JsonProperty("NumericRangeToleranceAbs", Order = 98, NullValueHandling = NullValueHandling.Ignore)]
        public double? LegacyNumericRangeToleranceAbs
        {
            get { return null; }
            set
            {
                if (value.HasValue)
                {
                    DefaultNumericToleranceAbs = value.Value;
                }
            }
        }

        [JsonProperty("NumericRangeToleranceRatio", Order = 99, NullValueHandling = NullValueHandling.Ignore)]
        public double? LegacyNumericRangeToleranceRatio
        {
            get { return null; }
            set
            {
                if (value.HasValue)
                {
                    DefaultNumericToleranceRatio = value.Value;
                }
            }
        }
    }

    public sealed class CicdTemplateCriteriaConfig
    {
        [JsonProperty(Order = 1)]
        public string TemplateName { get; set; } = string.Empty;

        [JsonProperty(Order = 2)]
        public string BoundStandardName { get; set; } = "默认标准";

        [JsonProperty(Order = 3)]
        public string ActiveStandardName { get; set; } = null;

        [JsonProperty(Order = 4)]
        public List<CicdAcceptanceCriteriaStandard> Standards { get; set; } = null;
    }

    public sealed class CicdAcceptanceCriteriaConfig
    {
        [JsonProperty(Order = 1)]
        public List<CicdAcceptanceCriteriaStandard> Standards { get; set; } = new List<CicdAcceptanceCriteriaStandard>();

        [JsonProperty(Order = 2)]
        public List<CicdTemplateCriteriaConfig> Templates { get; set; } = new List<CicdTemplateCriteriaConfig>();
    }

    public static class CicdAcceptanceCriteriaConfigManager
    {
        private const string ConfigFileName = "CicdAcceptanceCriteriaConfig.json";

        private static readonly string _baseConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string _baseConfigFilePath = Path.Combine(_baseConfigDir, ConfigFileName);

        private static readonly string _userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfApp2",
            "Config");
        private static readonly string _userConfigFilePath = Path.Combine(_userConfigDir, ConfigFileName);

        private static readonly Lazy<bool> _isBaseConfigDirWritable = new Lazy<bool>(() => CanWriteToDirectory(_baseConfigDir), LazyThreadSafetyMode.ExecutionAndPublication);

        public static CicdAcceptanceCriteriaConfig Load()
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
                var configFromFile = JsonConvert.DeserializeObject<CicdAcceptanceCriteriaConfig>(json);
                return Normalize(configFromFile);
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载CICD验收标准配置失败: {ex.Message}");
                return CreateDefault();
            }
        }

        public static bool TrySave(CicdAcceptanceCriteriaConfig config, out string savedPath, out string errorMessage)
        {
            savedPath = null;
            errorMessage = null;
            try
            {
                var normalized = Normalize(config);
                var targetPath = ResolveConfigPathForSave();
                var dir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonConvert.SerializeObject(normalized, Formatting.Indented);
                File.WriteAllText(targetPath, json, Encoding.UTF8);
                savedPath = targetPath;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                LogManager.Error($"保存CICD验收标准配置失败: {ex.Message}");
                return false;
            }
        }

        public static CicdTemplateCriteriaConfig GetOrCreateTemplateConfig(CicdAcceptanceCriteriaConfig config, string templateName)
        {
            if (config == null)
            {
                config = CreateDefault();
            }

            if (templateName == null)
            {
                templateName = string.Empty;
            }

            var template = config.Templates.FirstOrDefault(t => string.Equals(t.TemplateName, templateName, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                return template;
            }

            template = new CicdTemplateCriteriaConfig
            {
                TemplateName = templateName,
                BoundStandardName = "默认标准"
            };

            config.Templates.Add(template);
            return template;
        }

        public static CicdAcceptanceCriteriaStandard GetBoundStandard(string templateName)
        {
            var config = Load();
            var template = GetOrCreateTemplateConfig(config, templateName);
            EnsureDefaultStandardExists(config);

            string standardName = template.BoundStandardName ?? template.ActiveStandardName ?? "默认标准";
            var standard = config.Standards.FirstOrDefault(s => s != null && string.Equals(s.Name, standardName, StringComparison.OrdinalIgnoreCase))
                           ?? config.Standards.FirstOrDefault(s => s != null && string.Equals(s.Name, "默认标准", StringComparison.OrdinalIgnoreCase))
                           ?? config.Standards.FirstOrDefault(s => s != null);

            if (standard == null)
            {
                standard = CreateDefaultStandard();
                config.Standards.Add(standard);
            }

            template.BoundStandardName = standard.Name ?? "默认标准";
            TrySave(config, out _, out _);
            return standard;
        }

        // 兼容旧调用（等价于BoundStandard）
        public static CicdAcceptanceCriteriaStandard GetActiveStandard(string templateName)
        {
            return GetBoundStandard(templateName);
        }

        private static CicdAcceptanceCriteriaConfig CreateDefault()
        {
            return new CicdAcceptanceCriteriaConfig
            {
                Standards = new List<CicdAcceptanceCriteriaStandard> { CreateDefaultStandard() },
                Templates = new List<CicdTemplateCriteriaConfig>()
            };
        }

        private static CicdAcceptanceCriteriaStandard CreateDefaultStandard()
        {
            return new CicdAcceptanceCriteriaStandard
            {
                Name = "默认标准",
                AllowedOkNgMismatchCount = 0,
                AllowedDefectTypeMismatchCount = 0,
                AllowedNumericRangeMismatchCount = 0,
                DefaultNumericToleranceAbs = 0.0,
                DefaultNumericToleranceRatio = 0.0,
                ItemTolerances = new List<CicdItemToleranceConfig>()
            };
        }

        private static CicdAcceptanceCriteriaConfig Normalize(CicdAcceptanceCriteriaConfig config)
        {
            if (config == null)
            {
                return CreateDefault();
            }

            if (config.Standards == null)
            {
                config.Standards = new List<CicdAcceptanceCriteriaStandard>();
            }

            if (config.Templates == null)
            {
                config.Templates = new List<CicdTemplateCriteriaConfig>();
            }

            // 兼容旧结构：Templates[].Standards/ActiveStandardName
            MigrateLegacyTemplateStandardsToGlobal(config);
            EnsureDefaultStandardExists(config);

            // 清理全局标准
            foreach (var standard in config.Standards)
            {
                NormalizeStandard(standard);
            }

            foreach (var template in config.Templates)
            {
                if (template == null)
                {
                    continue;
                }

                if (template.TemplateName == null)
                {
                    template.TemplateName = string.Empty;
                }

                if (template.BoundStandardName == null)
                {
                    template.BoundStandardName = template.ActiveStandardName ?? "默认标准";
                }

                // 如果模板绑定的标准不存在，回退到默认标准
                if (!config.Standards.Any(s => s != null && string.Equals(s.Name, template.BoundStandardName, StringComparison.OrdinalIgnoreCase)))
                {
                    template.BoundStandardName = "默认标准";
                }
            }

            return config;
        }

        private static void NormalizeStandard(CicdAcceptanceCriteriaStandard standard)
        {
            if (standard == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(standard.Name))
            {
                standard.Name = "默认标准";
            }

            if (standard.DefaultNumericToleranceRatio < 0)
            {
                standard.DefaultNumericToleranceRatio = 0;
            }

            if (standard.ItemTolerances == null)
            {
                standard.ItemTolerances = new List<CicdItemToleranceConfig>();
            }

            // 去重：按ItemName保留最后一条
            var map = new Dictionary<string, CicdItemToleranceConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in standard.ItemTolerances)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemName))
                {
                    continue;
                }

                item.ItemName = item.ItemName.Trim();
                if (item.ToleranceRatio < 0)
                {
                    item.ToleranceRatio = 0;
                }

                map[item.ItemName] = item;
            }

            standard.ItemTolerances = map.Values
                .OrderBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void EnsureDefaultStandardExists(CicdAcceptanceCriteriaConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (config.Standards == null)
            {
                config.Standards = new List<CicdAcceptanceCriteriaStandard>();
            }

            if (!config.Standards.Any(s => s != null && string.Equals(s.Name, "默认标准", StringComparison.OrdinalIgnoreCase)))
            {
                config.Standards.Insert(0, CreateDefaultStandard());
            }
        }

        private static void MigrateLegacyTemplateStandardsToGlobal(CicdAcceptanceCriteriaConfig config)
        {
            if (config == null || config.Templates == null)
            {
                return;
            }

            // 如果已经有全局标准且模板已经使用BoundStandardName，说明不需要迁移
            bool hasNew = config.Standards != null && config.Standards.Count > 0;
            bool hasLegacy = config.Templates.Any(t => t != null && t.Standards != null && t.Standards.Count > 0);
            if (!hasLegacy)
            {
                return;
            }

            if (config.Standards == null)
            {
                config.Standards = new List<CicdAcceptanceCriteriaStandard>();
            }

            var globalNames = new HashSet<string>(config.Standards.Where(s => s != null).Select(s => s.Name ?? ""), StringComparer.OrdinalIgnoreCase);

            foreach (var template in config.Templates)
            {
                if (template == null || template.Standards == null || template.Standards.Count == 0)
                {
                    continue;
                }

                var legacyStandards = template.Standards.Where(s => s != null).ToList();
                if (legacyStandards.Count == 0)
                {
                    continue;
                }

                string templatePrefix = string.IsNullOrWhiteSpace(template.TemplateName) ? "Template" : template.TemplateName.Trim();
                var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var legacy in legacyStandards)
                {
                    string baseName = string.IsNullOrWhiteSpace(legacy.Name) ? "默认标准" : legacy.Name.Trim();
                    string candidate = baseName;
                    if (globalNames.Contains(candidate))
                    {
                        candidate = $"{templatePrefix}-{baseName}";
                    }

                    string unique = candidate;
                    int idx = 1;
                    while (globalNames.Contains(unique))
                    {
                        unique = $"{candidate}_{idx++}";
                    }

                    var cloned = new CicdAcceptanceCriteriaStandard
                    {
                        Name = unique,
                        AllowedOkNgMismatchCount = legacy.AllowedOkNgMismatchCount,
                        AllowedDefectTypeMismatchCount = legacy.AllowedDefectTypeMismatchCount,
                        AllowedNumericRangeMismatchCount = legacy.AllowedNumericRangeMismatchCount,
                        DefaultNumericToleranceAbs = legacy.DefaultNumericToleranceAbs,
                        DefaultNumericToleranceRatio = legacy.DefaultNumericToleranceRatio,
                        ItemTolerances = legacy.ItemTolerances != null ? legacy.ItemTolerances.ToList() : new List<CicdItemToleranceConfig>()
                    };

                    NormalizeStandard(cloned);
                    config.Standards.Add(cloned);
                    globalNames.Add(cloned.Name ?? "");
                    nameMap[baseName] = cloned.Name ?? baseName;
                }

                string activeLegacy = template.ActiveStandardName ?? "";
                if (string.IsNullOrWhiteSpace(activeLegacy))
                {
                    activeLegacy = legacyStandards[0].Name ?? "默认标准";
                }

                if (nameMap.TryGetValue(activeLegacy, out string mapped))
                {
                    template.BoundStandardName = mapped;
                }
                else
                {
                    template.BoundStandardName = nameMap.Values.FirstOrDefault() ?? "默认标准";
                }

                // 清空旧字段，避免继续膨胀
                template.Standards = null;
            }
        }

        private static string ResolveConfigPathForLoad()
        {
            if (File.Exists(_userConfigFilePath))
            {
                return _userConfigFilePath;
            }

            return _baseConfigFilePath;
        }

        private static string ResolveConfigPathForSave()
        {
            return _isBaseConfigDirWritable.Value ? _baseConfigFilePath : _userConfigFilePath;
        }

        private static bool CanWriteToDirectory(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string testFile = Path.Combine(dir, $".write_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test", Encoding.UTF8);
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
