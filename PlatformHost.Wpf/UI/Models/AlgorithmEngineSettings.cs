using System;
using System.IO;
using GlueInspect.Algorithm.Contracts;
using Newtonsoft.Json;

namespace WpfApp2.UI.Models
{
    public sealed class AlgorithmEngineSettings
    {
        public string PreferredEngineId { get; set; } = AlgorithmEngineIds.Vm;
    }

    public static class AlgorithmEngineSettingsManager
    {
        private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "AlgorithmEngine.json");
        private static AlgorithmEngineSettings _cached;

        public static string PreferredEngineId
        {
            get
            {
                EnsureLoaded();
                return _cached?.PreferredEngineId ?? AlgorithmEngineIds.Vm;
            }
        }

        public static void UpdatePreferredEngine(string engineId)
        {
            EnsureLoaded();
            _cached.PreferredEngineId = NormalizeEngineId(engineId);
            SaveInternal(_cached);
        }

        public static void Reload()
        {
            _cached = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_cached != null)
            {
                return;
            }

            _cached = LoadInternal();
        }

        private static AlgorithmEngineSettings LoadInternal()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    var settings = JsonConvert.DeserializeObject<AlgorithmEngineSettings>(json) ?? new AlgorithmEngineSettings();
                    settings.PreferredEngineId = NormalizeEngineId(settings.PreferredEngineId);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"Failed to read algorithm engine config: {ex.Message}", "Config");
            }

            return new AlgorithmEngineSettings();
        }

        private static void SaveInternal(AlgorithmEngineSettings settings)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                LogManager.Warning($"Failed to save algorithm engine config: {ex.Message}", "Config");
            }
        }

        private static string NormalizeEngineId(string engineId)
        {
            return string.IsNullOrWhiteSpace(engineId) ? AlgorithmEngineIds.Vm : engineId;
        }
    }
}
