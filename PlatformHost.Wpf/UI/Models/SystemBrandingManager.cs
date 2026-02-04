using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace WpfApp2.UI.Models
{
    public sealed class SystemBrandingConfig
    {
        public string SystemName { get; set; } = "点胶检测系统";
        public string ModuleName { get; set; } = string.Empty;
    }

    public static class SystemBrandingManager
    {
        private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "SystemBranding.json");
        private static SystemBrandingConfig _config;
        private static string _resolvedName;

        public static string GetSystemName()
        {
            EnsureLoaded();
            return _resolvedName;
        }

        private static void EnsureLoaded()
        {
            if (_resolvedName != null)
            {
                return;
            }

            _config = LoadInternal();
            _resolvedName = ResolveSystemName(_config);
        }

        private static SystemBrandingConfig LoadInternal()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    return new SystemBrandingConfig();
                }

                var json = File.ReadAllText(ConfigFile);
                return JsonConvert.DeserializeObject<SystemBrandingConfig>(json) ?? new SystemBrandingConfig();
            }
            catch
            {
                return new SystemBrandingConfig();
            }
        }

        private static string ResolveSystemName(SystemBrandingConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config?.ModuleName))
            {
                var moduleName = config.ModuleName.Trim();
                var module = ModuleRegistry.AllModules.FirstOrDefault(m =>
                    string.Equals(m.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.DisplayName, moduleName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.StepType.ToString(), moduleName, StringComparison.OrdinalIgnoreCase));

                return module?.DisplayName ?? moduleName;
            }

            if (!string.IsNullOrWhiteSpace(config?.SystemName))
            {
                return config.SystemName.Trim();
            }

            return "点胶检测系统";
        }
    }
}
