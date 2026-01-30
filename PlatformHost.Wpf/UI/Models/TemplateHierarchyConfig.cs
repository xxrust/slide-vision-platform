using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace WpfApp2.Models
{
    public sealed class TemplateHierarchyConfig
    {
        private const string ConfigFileName = "TemplateHierarchy.json";
        private static readonly Lazy<TemplateHierarchyConfig> _instance = new Lazy<TemplateHierarchyConfig>(LoadOrCreate, true);

        public static TemplateHierarchyConfig Instance => _instance.Value;

        [JsonProperty(Order = 1)]
        public int Version { get; set; } = 1;

        [JsonProperty(Order = 2)]
        public string DefaultProfileId { get; set; }

        [JsonProperty(Order = 3)]
        public List<TemplateProfileDefinition> Profiles { get; set; } = new List<TemplateProfileDefinition>();

        [JsonProperty(Order = 4)]
        public List<LegacyProfileMapping> LegacyMappings { get; set; } = new List<LegacyProfileMapping>();

        public static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", ConfigFileName);

        public TemplateProfileDefinition ResolveProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = DefaultProfileId;
            }

            var profile = Profiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
            if (profile != null)
            {
                return profile;
            }

            return Profiles.FirstOrDefault();
        }

        public string ResolveProfileId(SampleType sampleType, CoatingType coatingType)
        {
            var sampleTypeName = sampleType.ToString();
            var coatingTypeName = coatingType.ToString();

            var mapping = LegacyMappings.FirstOrDefault(m =>
                string.Equals(m.SampleType, sampleTypeName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.CoatingType, coatingTypeName, StringComparison.OrdinalIgnoreCase));

            if (mapping != null && !string.IsNullOrWhiteSpace(mapping.ProfileId))
            {
                return mapping.ProfileId;
            }

            return DefaultProfileId;
        }

        private static TemplateHierarchyConfig LoadOrCreate()
        {
            try
            {
                var path = ConfigPath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var config = JsonConvert.DeserializeObject<TemplateHierarchyConfig>(json);
                    return Normalize(config);
                }
            }
            catch
            {
                // Ignore and fall back to default.
            }

            var fallback = CreateDefault();
            TrySave(fallback);
            return fallback;
        }

        private static TemplateHierarchyConfig Normalize(TemplateHierarchyConfig config)
        {
            if (config == null)
            {
                return CreateDefault();
            }

            if (config.Profiles == null)
            {
                config.Profiles = new List<TemplateProfileDefinition>();
            }

            if (config.LegacyMappings == null)
            {
                config.LegacyMappings = new List<LegacyProfileMapping>();
            }

            if (string.IsNullOrWhiteSpace(config.DefaultProfileId))
            {
                config.DefaultProfileId = config.Profiles.FirstOrDefault()?.Id;
            }

            return config;
        }

        private static bool TrySave(TemplateHierarchyConfig config)
        {
            try
            {
                var targetPath = ConfigPath;
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(targetPath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static TemplateHierarchyConfig CreateDefault()
        {
            return new TemplateHierarchyConfig
            {
                Version = 1,
                DefaultProfileId = "profile-standard",
                Profiles = new List<TemplateProfileDefinition>
                {
                    new TemplateProfileDefinition
                    {
                        Id = "profile-basic",
                        DisplayName = "Basic Template",
                        Description = "Minimal configuration flow for quick verification.",
                        Steps = new List<string>
                        {
                            StepType.ImageSelection.ToString(),
                            StepType.DemoSetup.ToString(),
                            StepType.DemoSummary.ToString(),
                            StepType.TemplateName.ToString()
                        },
                        GlobalVariables = new Dictionary<string, string>
                        {
                            { "PROFILE", "basic" },
                            { "PIPELINE", "quick" }
                        },
                        DefaultTemplateName = "Template-Basic",
                        MeasurementOutputCount = 1
                    },
                    new TemplateProfileDefinition
                    {
                        Id = "profile-standard",
                        DisplayName = "Standard Template",
                        Description = "Standard configuration with calculation stage.",
                        Steps = new List<string>
                        {
                            StepType.ImageSelection.ToString(),
                            StepType.DemoSetup.ToString(),
                            StepType.DemoCalculation.ToString(),
                            StepType.DemoSummary.ToString(),
                            StepType.TemplateName.ToString()
                        },
                        GlobalVariables = new Dictionary<string, string>
                        {
                            { "PROFILE", "standard" },
                            { "PIPELINE", "default" }
                        },
                        DefaultTemplateName = "Template-Standard",
                        MeasurementOutputCount = 1
                    },
                    new TemplateProfileDefinition
                    {
                        Id = "profile-3d",
                        DisplayName = "3D Template",
                        Description = "Standard configuration with 3D setup enabled.",
                        Steps = new List<string>
                        {
                            StepType.ImageSelection.ToString(),
                            StepType.DemoSetup.ToString(),
                            StepType.DemoCalculation.ToString(),
                            StepType.ThreeDConfiguration.ToString(),
                            StepType.DemoSummary.ToString(),
                            StepType.TemplateName.ToString()
                        },
                        GlobalVariables = new Dictionary<string, string>
                        {
                            { "PROFILE", "3d" },
                            { "PIPELINE", "extended" }
                        },
                        DefaultTemplateName = "Template-3D",
                        MeasurementOutputCount = 1
                    }
                },
                LegacyMappings = new List<LegacyProfileMapping>()
            };
        }
    }

    public sealed class TemplateProfileDefinition
    {
        [JsonProperty(Order = 1)]
        public string Id { get; set; }

        [JsonProperty(Order = 2)]
        public string DisplayName { get; set; }

        [JsonProperty(Order = 3)]
        public string Description { get; set; }

        [JsonProperty(Order = 4)]
        public List<string> Steps { get; set; } = new List<string>();

        [JsonProperty(Order = 5)]
        public Dictionary<string, string> GlobalVariables { get; set; } = new Dictionary<string, string>();

        [JsonProperty(Order = 6)]
        public string DefaultTemplateName { get; set; }

        [JsonProperty(Order = 7)]
        public int MeasurementOutputCount { get; set; } = 1;

        public List<StepType> GetStepTypes()
        {
            var stepTypes = new List<StepType>();
            if (Steps == null)
            {
                return stepTypes;
            }

            foreach (var raw in Steps)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                if (StepTypeLegacyMap.TryParse(raw, out StepType stepType))
                {
                    stepTypes.Add(stepType);
                }
            }

            return stepTypes;
        }
    }

    public sealed class LegacyProfileMapping
    {
        [JsonProperty("SampleType", Order = 1)]
        public string SampleType { get; set; }

        [JsonProperty("CoatingType", Order = 2)]
        public string CoatingType { get; set; }

        [JsonProperty(Order = 3)]
        public string ProfileId { get; set; }
    }
}
