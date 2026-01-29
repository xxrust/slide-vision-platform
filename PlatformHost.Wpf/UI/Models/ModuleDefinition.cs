using System;
using System.Collections.Generic;
using System.Windows.Media;
using WpfApp2.Models;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// VM模块类型枚举
    /// </summary>
    public enum VmModuleType
    {
        None,           // 无模块（特殊步骤如模板命名）
        ImageSource,    // 图像源模块
        ImageEnhance,   // 图像增强模块
        FeatureMatch,   // 特征匹配模块
        SaveImage,      // 保存图像模块
        BlobFind,       // 斑点检测模块
        LineFind,       // 直线查找模块
        CircleFind,     // 圆形查找模块
        FlawModuleC     // 图像分割CPU
    }

    /// <summary>
    /// 统一的模块定义类 - 将所有模块相关配置集中在一起
    /// 实现高内聚：每个模块的所有配置（步骤、参数、映射、转换、VM模块）都在一个定义中
    /// </summary>
    public class ModuleDefinition
    {
        #region 基本信息

        /// <summary>
        /// 步骤类型（唯一标识）
        /// </summary>
        public StepType StepType { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 是否为特殊步骤（如模板命名、3D配置等）
        /// </summary>
        public bool IsSpecialStep { get; set; } = false;

        #endregion

        #region VM模块配置

        /// <summary>
        /// VM模块显示名称（用于UI显示和模块切换）
        /// </summary>
        public string VmModuleName { get; set; } = "";

        /// <summary>
        /// VM中的实际模块路径（如"校准.PKG增亮"）
        /// </summary>
        public string VmModulePath { get; set; } = "";

        /// <summary>
        /// VM模块类型
        /// </summary>
        public VmModuleType VmModuleType { get; set; } = VmModuleType.None;

        /// <summary>
        /// VM模块额外的设置操作（可选）
        /// </summary>
        public Action SetupAction { get; set; }

        #endregion

        #region 参数配置

        /// <summary>
        /// 输入参数列表
        /// </summary>
        public List<ModuleParameter> InputParameters { get; set; } = new List<ModuleParameter>();

        /// <summary>
        /// 输出参数列表（只读参数）
        /// </summary>
        public List<ModuleParameter> OutputParameters { get; set; } = new List<ModuleParameter>();

        #endregion

        #region 操作按钮配置

        /// <summary>
        /// 操作按钮列表
        /// </summary>
        public List<ModuleAction> Actions { get; set; } = new List<ModuleAction>();

        #endregion

        #region UI配置

        /// <summary>
        /// 说明标签列表
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        #endregion

        #region 特殊处理器

        /// <summary>
        /// 特殊步骤的处理器（如模板保存）
        /// </summary>
        public Action<ModuleDefinition> SpecialHandler { get; set; }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 转换为StepConfiguration（向后兼容）
        /// </summary>
        public StepConfiguration ToStepConfiguration()
        {
            var config = new StepConfiguration
            {
                StepType = this.StepType,
                DisplayName = this.DisplayName,
                VmModuleName = this.VmModuleName,
                IsSpecialStep = this.IsSpecialStep,
                Labels = this.Labels ?? new List<string>()
            };

            // 转换输入参数
            config.InputParameters = new List<ParameterConfig>();
            foreach (var param in InputParameters ?? new List<ModuleParameter>())
            {
                config.InputParameters.Add(param.ToParameterConfig());
            }

            // 转换输出参数
            config.OutputParameters = new List<ParameterConfig>();
            foreach (var param in OutputParameters ?? new List<ModuleParameter>())
            {
                config.OutputParameters.Add(param.ToParameterConfig());
            }

            // 转换操作按钮
            config.Actions = new List<ActionConfig>();
            foreach (var action in Actions ?? new List<ModuleAction>())
            {
                config.Actions.Add(action.ToActionConfig());
            }

            return config;
        }

        /// <summary>
        /// 获取参数到全局变量的映射字典
        /// </summary>
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

        /// <summary>
        /// 获取参数单位转换函数字典
        /// </summary>
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

        #endregion
    }

    /// <summary>
    /// 模块参数定义 - 包含参数的所有配置信息
    /// </summary>
    public class ModuleParameter
    {
        /// <summary>
        /// 参数名称（UI显示）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// 参数类型
        /// </summary>
        public ParamType Type { get; set; }

        /// <summary>
        /// 是否只读
        /// </summary>
        public bool IsReadOnly { get; set; } = false;

        /// <summary>
        /// 参数分组（用于UI分组显示）
        /// </summary>
        public string Group { get; set; } = "";

        /// <summary>
        /// 对应的VM全局变量名称（为空则不映射到全局变量）
        /// </summary>
        public string GlobalVariableName { get; set; }

        /// <summary>
        /// 单位转换函数（为空则不进行转换）
        /// 输入UI值，输出转换后的值
        /// </summary>
        public Func<string, string> ConversionFunc { get; set; }

        /// <summary>
        /// 转换为ParameterConfig（向后兼容）
        /// </summary>
        public ParameterConfig ToParameterConfig()
        {
            return new ParameterConfig
            {
                Name = this.Name,
                DefaultValue = this.DefaultValue,
                Type = this.Type,
                IsReadOnly = this.IsReadOnly,
                Group = this.Group
            };
        }
    }

    /// <summary>
    /// 模块操作按钮定义
    /// </summary>
    public class ModuleAction
    {
        /// <summary>
        /// 按钮名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 按钮点击处理器（在运行时设置）
        /// </summary>
        public System.Windows.RoutedEventHandler Handler { get; set; }

        /// <summary>
        /// 背景色
        /// </summary>
        public Brush BackgroundColor { get; set; } = new SolidColorBrush(Colors.Blue);

        /// <summary>
        /// 前景色
        /// </summary>
        public Brush ForegroundColor { get; set; } = new SolidColorBrush(Colors.White);

        /// <summary>
        /// 转换为ActionConfig（向后兼容）
        /// </summary>
        public ActionConfig ToActionConfig()
        {
            return new ActionConfig
            {
                Name = this.Name,
                Handler = this.Handler,
                BackgroundColor = this.BackgroundColor,
                ForegroundColor = this.ForegroundColor
            };
        }
    }
}
