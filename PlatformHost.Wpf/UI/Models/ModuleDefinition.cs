using System;
using System.Collections.Generic;
using System.Windows.Media;
using WpfApp2.Models;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// Module category used by the template configuration UI.
    /// </summary>
    public enum ModuleType
    {
        None,
        ImageSource,
        ImageEnhance,
        FeatureMatch,
        SaveImage,
        BlobFind,
        LineFind,
        CircleFind,
        FlawModuleC
    }

    /// <summary>
    /// Unified module definition. Keeps step metadata, parameters, mappings, and UI helpers in one place.
    /// </summary>
    public class ModuleDefinition
    {
        // Basic info
        public StepType StepType { get; set; }
        public string DisplayName { get; set; }
        public bool IsSpecialStep { get; set; } = false;

        // Module mapping
        public string ModuleName { get; set; } = string.Empty;
        public string ModulePath { get; set; } = string.Empty;
        public ModuleType ModuleType { get; set; } = ModuleType.None;
        public Action SetupAction { get; set; }

        // Parameters
        public List<ModuleParameter> InputParameters { get; set; } = new List<ModuleParameter>();
        public List<ModuleParameter> OutputParameters { get; set; } = new List<ModuleParameter>();

        // Actions
        public List<ModuleAction> Actions { get; set; } = new List<ModuleAction>();

        // UI
        public List<string> Labels { get; set; } = new List<string>();

        // Special handler (e.g., template save)
        public Action<ModuleDefinition> SpecialHandler { get; set; }

        public StepConfiguration ToStepConfiguration()
        {
            var config = new StepConfiguration
            {
                StepType = StepType,
                DisplayName = DisplayName,
                ModuleName = ModuleName,
                IsSpecialStep = IsSpecialStep,
                Labels = Labels ?? new List<string>()
            };

            config.InputParameters = new List<ParameterConfig>();
            foreach (var param in InputParameters ?? new List<ModuleParameter>())
            {
                config.InputParameters.Add(param.ToParameterConfig());
            }

            config.OutputParameters = new List<ParameterConfig>();
            foreach (var param in OutputParameters ?? new List<ModuleParameter>())
            {
                config.OutputParameters.Add(param.ToParameterConfig());
            }

            config.Actions = new List<ActionConfig>();
            foreach (var action in Actions ?? new List<ModuleAction>())
            {
                config.Actions.Add(action.ToActionConfig());
            }

            return config;
        }

        public Dictionary<string, string> GetParameterMappings()
        {
            var mappings = new Dictionary<string, string>();
            foreach (var param in InputParameters ?? new List<ModuleParameter>())
            {
                if (!string.IsNullOrEmpty(param.GlobalVariableName))
                {
                    mappings[param.Name] = param.GlobalVariableName;
                }
            }
            return mappings;
        }

        public Dictionary<string, Func<string, string>> GetParameterConversions()
        {
            var conversions = new Dictionary<string, Func<string, string>>();
            foreach (var param in InputParameters ?? new List<ModuleParameter>())
            {
                if (param.ConversionFunc != null)
                {
                    conversions[param.Name] = param.ConversionFunc;
                }
            }
            return conversions;
        }
    }

    public class ModuleParameter
    {
        public string Name { get; set; }
        public string DefaultValue { get; set; }
        public ParamType Type { get; set; }
        public bool IsReadOnly { get; set; } = false;
        public string Group { get; set; } = string.Empty;
        public string GlobalVariableName { get; set; }
        public Func<string, string> ConversionFunc { get; set; }

        public ParameterConfig ToParameterConfig()
        {
            return new ParameterConfig
            {
                Name = Name,
                DefaultValue = DefaultValue,
                Type = Type,
                IsReadOnly = IsReadOnly,
                Group = Group
            };
        }
    }

    public class ModuleAction
    {
        public string Name { get; set; }
        public System.Windows.RoutedEventHandler Handler { get; set; }
        public Brush BackgroundColor { get; set; } = new SolidColorBrush(Colors.Blue);
        public Brush ForegroundColor { get; set; } = new SolidColorBrush(Colors.White);

        public ActionConfig ToActionConfig()
        {
            return new ActionConfig
            {
                Name = Name,
                Handler = Handler,
                BackgroundColor = BackgroundColor,
                ForegroundColor = ForegroundColor
            };
        }
    }
}
