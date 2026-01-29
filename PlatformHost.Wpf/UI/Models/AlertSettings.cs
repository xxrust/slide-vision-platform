using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 告警设置模型，支持多策略组合与项目绑定
    /// </summary>
    public class AlertSettings
    {
        private const string ConfigFileName = "AlertSettings.json";

        public bool IsEnabled { get; set; } = false;
        public int StatisticsCycle { get; set; } = 100;
        public int MinSampleSize { get; set; } = 50;
        public bool EnableIOOutput { get; set; } = false;

        /// <summary>
        /// 默认策略组合标识
        /// </summary>
        public string DefaultProfileId { get; set; } = string.Empty;

        /// <summary>
        /// 策略组合列表
        /// </summary>
        public List<AlertStrategyProfile> StrategyProfiles { get; set; } = new List<AlertStrategyProfile>();

        /// <summary>
        /// 项目与策略组合绑定关系（Key: 项目名, Value: ProfileId）
        /// </summary>
        public Dictionary<string, string> ItemProfileBindings { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [JsonIgnore]
        public IReadOnlyList<AlertStrategyProfile> Profiles
        {
            get
            {
                EnsureProfileList();
                return StrategyProfiles;
            }
        }

        /// <summary>
        /// 获取指定项目绑定的策略组合，不存在则返回默认组合
        /// </summary>
        public AlertStrategyProfile GetProfileForItem(string itemName)
        {
            EnsureProfileList();
            var profileId = string.Empty;

            if (!string.IsNullOrWhiteSpace(itemName) && ItemProfileBindings != null)
            {
                if (ItemProfileBindings.TryGetValue(itemName, out var mappedId))
                {
                    profileId = mappedId;
                }
            }

            var profile = GetProfileById(profileId);
            return profile ?? GetDefaultProfile();
        }

        /// <summary>
        /// 获取默认策略组合，若不存在则创建
        /// </summary>
        public AlertStrategyProfile GetDefaultProfile()
        {
            EnsureProfileList();

            var profile = GetProfileById(DefaultProfileId);
            if (profile != null)
            {
                foreach (var p in StrategyProfiles)
                {
                    p.IsDefault = string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase);
                }
                return profile;
            }

            if (StrategyProfiles.Count > 0)
            {
                profile = StrategyProfiles[0];
                DefaultProfileId = profile.Id;
                foreach (var p in StrategyProfiles)
                {
                    p.IsDefault = string.Equals(p.Id, DefaultProfileId, StringComparison.OrdinalIgnoreCase);
                }
                return profile;
            }

            profile = AlertStrategyProfile.CreateDefault();
            profile.IsDefault = true;
            StrategyProfiles.Add(profile);
            DefaultProfileId = profile.Id;
            return profile;
        }
        /// <summary>
        /// 绑定项目到指定策略组合。profileId为空时解绑
        /// </summary>
        public void BindItemToProfile(string itemName, string profileId)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return;
            }

            EnsureBindingDictionary();

            if (string.IsNullOrWhiteSpace(profileId))
            {
                ItemProfileBindings.Remove(itemName);
                return;
            }

            var profile = GetProfileById(profileId);
            if (profile == null)
            {
                return;
            }

            ItemProfileBindings[itemName] = profile.Id;
        }

        /// <summary>
        /// 解绑指定项目
        /// </summary>
        public void UnbindItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return;
            }

            EnsureBindingDictionary();
            ItemProfileBindings.Remove(itemName);
        }

        /// <summary>
        /// 保存设置到配置文件
        /// </summary>
        public void Save()
        {
            EnsureProfileConsistency();

            var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            Directory.CreateDirectory(configDir);

            var filePath = Path.Combine(configDir, ConfigFileName);
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 从配置文件加载设置
        /// </summary>
        public static AlertSettings Load()
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", ConfigFileName);
                if (!File.Exists(filePath))
                {
                    var defaults = CreateWithDefaultProfile();
                    defaults.Save();
                    return defaults;
                }

                var json = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<AlertSettings>(json) ?? CreateWithDefaultProfile();
                settings.EnsureProfileConsistency();
                return settings;
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载告警设置失败: {ex.Message}");
                return CreateWithDefaultProfile();
            }
        }

        /// <summary>
        /// 深拷贝，供界面编辑时使用
        /// </summary>
        public AlertSettings Clone()
        {
            EnsureProfileConsistency();

            var clonedProfiles = StrategyProfiles
                .Select(p => p.Clone(withNewId: false))
                .ToList();

            return new AlertSettings
            {
                IsEnabled = IsEnabled,
                StatisticsCycle = StatisticsCycle,
                MinSampleSize = MinSampleSize,
                EnableIOOutput = EnableIOOutput,
                DefaultProfileId = DefaultProfileId,
                StrategyProfiles = clonedProfiles,
                ItemProfileBindings = new Dictionary<string, string>(ItemProfileBindings, StringComparer.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// 兼容旧版属性：数量分析开关
        /// </summary>
        public bool EnableCountAnalysis
        {
            get => GetDefaultProfile().EnableCountAnalysis;
            set => GetDefaultProfile().EnableCountAnalysis = value;
        }

        /// <summary>
        /// 兼容旧版属性：数量分析阈值
        /// </summary>
        public int OutOfRangeThreshold
        {
            get => GetDefaultProfile().OutOfRangeThreshold;
            set => GetDefaultProfile().OutOfRangeThreshold = value;
        }

        public bool EnableProcessCapabilityAnalysis
        {
            get => GetDefaultProfile().EnableProcessCapabilityAnalysis;
            set => GetDefaultProfile().EnableProcessCapabilityAnalysis = value;
        }

        public double CAThreshold
        {
            get => GetDefaultProfile().CAThreshold;
            set => GetDefaultProfile().CAThreshold = value;
        }

        public double CPThreshold
        {
            get => GetDefaultProfile().CPThreshold;
            set => GetDefaultProfile().CPThreshold = value;
        }

        public double CPKThreshold
        {
            get => GetDefaultProfile().CPKThreshold;
            set => GetDefaultProfile().CPKThreshold = value;
        }

        public bool EnableConsecutiveNGAnalysis
        {
            get => GetDefaultProfile().EnableConsecutiveNGAnalysis;
            set => GetDefaultProfile().EnableConsecutiveNGAnalysis = value;
        }

        public int ConsecutiveNGThreshold
        {
            get => GetDefaultProfile().ConsecutiveNGThreshold;
            set => GetDefaultProfile().ConsecutiveNGThreshold = value;
        }

        /// <summary>
        /// 确保策略组合列表与绑定合法
        /// </summary>
        public void EnsureProfileConsistency()
        {
            EnsureProfileList();
            EnsureBindingDictionary();

            foreach (var profile in StrategyProfiles)
            {
                profile.Normalize();
            }

            if (string.IsNullOrWhiteSpace(DefaultProfileId) || GetProfileById(DefaultProfileId) == null)
            {
                DefaultProfileId = StrategyProfiles.First().Id;
            }

            foreach (var profile in StrategyProfiles)
            {
                profile.IsDefault = string.Equals(profile.Id, DefaultProfileId, StringComparison.OrdinalIgnoreCase);
            }

            var validIds = new HashSet<string>(StrategyProfiles.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
            var keysToRemove = ItemProfileBindings.Where(p => !validIds.Contains(p.Value)).Select(p => p.Key).ToList();
            foreach (var key in keysToRemove)
            {
                ItemProfileBindings.Remove(key);
            }
        }

        /// <summary>
        /// 返回当前配置中启用的策略类型集合
        /// </summary>
        public HashSet<AlertType> GetEnabledAlertTypes()
        {
            var result = new HashSet<AlertType>();
            foreach (var profile in Profiles)
            {
                if (profile.EnableCountAnalysis)
                {
                    result.Add(AlertType.CountBased);
                }
                if (profile.EnableProcessCapabilityAnalysis)
                {
                    result.Add(AlertType.StatisticalAnalysis);
                }
                if (profile.EnableConsecutiveNGAnalysis)
                {
                    result.Add(AlertType.ConsecutiveNG);
                }
            }
            return result;
        }

        private static AlertSettings CreateWithDefaultProfile()
        {
            var settings = new AlertSettings();
            settings.GetDefaultProfile();
            return settings;
        }

        private void EnsureProfileList()
        {
            if (StrategyProfiles == null)
            {
                StrategyProfiles = new List<AlertStrategyProfile>();
            }

            if (StrategyProfiles.Count == 0)
            {
                StrategyProfiles.Add(AlertStrategyProfile.CreateDefault());
            }
        }

        private void EnsureBindingDictionary()
        {
            if (ItemProfileBindings == null)
            {
                ItemProfileBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else if (ItemProfileBindings.Comparer != StringComparer.OrdinalIgnoreCase)
            {
                ItemProfileBindings = new Dictionary<string, string>(ItemProfileBindings, StringComparer.OrdinalIgnoreCase);
            }
        }

        private AlertStrategyProfile GetProfileById(string profileId)
        {
            if (StrategyProfiles == null || string.IsNullOrWhiteSpace(profileId))
            {
                return null;
            }

            return StrategyProfiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
        }

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            EnsureProfileConsistency();
        }
    }

    /// <summary>
    /// 告警策略组合
    /// </summary>
    public class AlertStrategyProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "策略组合";

        // 策略1：数量分析
        public bool EnableCountAnalysis { get; set; } = true;
        public int OutOfRangeThreshold { get; set; } = 10;

        // 策略2：过程能力分析
        public bool EnableProcessCapabilityAnalysis { get; set; } = false;
        public double CAThreshold { get; set; } = 0.2;
        public double CPThreshold { get; set; } = 1.33;
        public double CPKThreshold { get; set; } = 1.33;

        // 策略3：连续NG分析
        public bool EnableConsecutiveNGAnalysis { get; set; } = false;
        public int ConsecutiveNGThreshold { get; set; } = 3;

        [JsonIgnore]
        public bool IsDefault { get; set; } = false;

        [JsonIgnore]
        public bool HasAnyStrategyEnabled => EnableCountAnalysis || EnableProcessCapabilityAnalysis || EnableConsecutiveNGAnalysis;

        public AlertStrategyProfile Clone(bool withNewId = true)
        {
            return new AlertStrategyProfile
            {
                Id = withNewId ? Guid.NewGuid().ToString("N") : Id,
                Name = Name,
                EnableCountAnalysis = EnableCountAnalysis,
                OutOfRangeThreshold = OutOfRangeThreshold,
                EnableProcessCapabilityAnalysis = EnableProcessCapabilityAnalysis,
                CAThreshold = CAThreshold,
                CPThreshold = CPThreshold,
                CPKThreshold = CPKThreshold,
                EnableConsecutiveNGAnalysis = EnableConsecutiveNGAnalysis,
                ConsecutiveNGThreshold = ConsecutiveNGThreshold,
                IsDefault = withNewId ? false : IsDefault
            };
        }

        public static AlertStrategyProfile CreateDefault(string name = null)
        {
            return new AlertStrategyProfile
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name) ? "默认策略组合" : name.Trim()
            };
        }

        public void Normalize()
        {
            if (OutOfRangeThreshold <= 0)
            {
                OutOfRangeThreshold = 1;
            }

            if (ConsecutiveNGThreshold <= 0)
            {
                ConsecutiveNGThreshold = 1;
            }

            if (CAThreshold < 0)
            {
                CAThreshold = 0;
            }

            if (CPThreshold <= 0)
            {
                CPThreshold = 0.01;
            }

            if (CPKThreshold <= 0)
            {
                CPKThreshold = 0.01;
            }
        }
    }

    /// <summary>
    /// 告警类型枚举
    /// </summary>
    public enum AlertType
    {
        CountBased,
        StatisticalAnalysis,
        ConsecutiveNG
    }
}
