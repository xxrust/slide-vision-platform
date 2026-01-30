using System.Windows;
using System.Windows.Controls;
using IMVSHPFeatureMatchModuCs;
using static WpfApp2.UI.Page1;
using static WpfApp2.UI.Page2;
using VM.Core;
using VM.PlatformSDKCS;
using System.Windows.Media;
using WpfApp2.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Linq;
using ScottPlot.WPF;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Data;
using VMControls.WPF.Release;
using GlobalCameraModuleCs;
using System.Windows.Threading;
using Path = System.IO.Path;
using WpfApp2.UI;
using WpfApp2.UI.Models;
using WpfApp2.UI.Controls;
using VmModuleType = WpfApp2.UI.Models.VmModuleType;
using WpfApp2.SMTGPIO;
using WpfApp2.ThreeD;
using System;
using GlueInspect.Algorithm.Contracts;
using WpfApp2.Algorithms;
using WpfApp2.Rendering;

namespace WpfApp2.UI
{
    /// <summary>
    /// 步骤配置工厂类，根据样品类型提供对应的配置流程
    /// </summary>
    public static class StepConfigurationFactory
    {
        /// <summary>
        /// 根据样品类型获取对应的步骤配置列表
        /// 【重构】- 现在委托给 ModuleRegistry.GetStepConfigurations
        /// </summary>
        /// <param name="sampleType">样品类型</param>
        /// <param name="coatingType">涂布类型</param>
        /// <returns>该类型对应的步骤配置列表</returns>
        public static List<StepConfiguration> GetStepConfigurations(SampleType sampleType, CoatingType coatingType = CoatingType.Single)
        {
            // 直接使用 ModuleRegistry 获取配置
            return ModuleRegistry.GetStepConfigurations(sampleType, coatingType);
        }

        /// <summary>
        /// 镀膜图PKG定位步骤集合（飞拍作为BLK图像时使用）
        /// 【重构】- 现在委托给 ModuleRegistry.GetCoatingPkgConfigurations
        /// </summary>
        public static List<StepConfiguration> GetCoatingPkgConfigurations()
        {
            return ModuleRegistry.GetCoatingPkgConfigurations();
        }
    }



    /// <summary>
    /// 参数类型枚举
    /// </summary>
    public enum ParamType
    {
        Text,      // 普通文本
        Number,    // 数字
        FilePath,  // 文件路径
        FolderPath,// 文件夹路径
        Boolean,   // 布尔值（复选框）
        Label      // 标签（只显示）
    }

    /// <summary>
    /// 参数配置类
    /// </summary>
    public class ParameterConfig
    {
        public string Name { get; set; }
        public string DefaultValue { get; set; }
        public ParamType Type { get; set; }
        public bool IsReadOnly { get; set; } = false;
        public string Group { get; set; } = ""; // 参数分组，空字符串表示不分组
    }

    /// <summary>
    /// 参数分组配置类，用于实现树状结构的参数组织
    /// </summary>
    public class ParameterGroup
    {
        public string Name { get; set; }
        public List<ParameterConfig> Parameters { get; set; } = new List<ParameterConfig>();
        public List<ParameterGroup> SubGroups { get; set; } = new List<ParameterGroup>();
        public bool IsExpanded { get; set; } = true; // 是否展开
    }





    /// <summary>
    /// 操作按钮配置类
    /// </summary>
    public class ActionConfig
    {
        public string Name { get; set; }
        public RoutedEventHandler Handler { get; set; }
        public Brush BackgroundColor { get; set; } = new SolidColorBrush(Colors.Blue);
        public Brush ForegroundColor { get; set; } = new SolidColorBrush(Colors.White);
    }

    /// <summary>
    /// 步骤组类，用于管理多个连续步骤的组合显示
    /// </summary>
    public class StepGroup
    {
        public string GroupName { get; set; }
        public List<int> StepIndices { get; set; } = new List<int>();
        public bool IsExpanded { get; set; } = false;
        public bool ContainsStep(int stepIndex) => StepIndices.Contains(stepIndex);
        public int StartIndex => StepIndices.Count > 0 ? StepIndices.Min() : -1;
        public int EndIndex => StepIndices.Count > 0 ? StepIndices.Max() : -1;
    }

    /// <summary>
    /// 步骤配置类
    /// </summary>
    public class StepConfiguration
    {
        public StepType StepType { get; set; }
        public string DisplayName { get; set; }
        public string VmModuleName { get; set; }
        public List<ParameterConfig> InputParameters { get; set; } = new List<ParameterConfig>();
        public List<ParameterConfig> OutputParameters { get; set; } = new List<ParameterConfig>();
        public List<ActionConfig> Actions { get; set; } = new List<ActionConfig>();
        public List<string> Labels { get; set; } = new List<string>();
        public bool IsSpecialStep { get; set; } = false;
        public Action<StepConfiguration> SpecialHandler { get; set; }
    }

    public partial class TemplateConfigPage : Page
    {
        /// <summary>
        /// 静态标志位，确保事件处理器只绑定一次
        /// </summary>
        private static bool _isEventHandlerBound = false;

        /// <summary>
        /// 防止10009流程告警弹窗重复弹出（同一时刻只允许一个）
        /// </summary>
        private static bool _isProcess10009WarningDialogShowing = false;

        /// <summary>
        /// 静态锁对象，确保线程安全
        /// </summary>
        private static readonly object _eventBindingLock = new object();

        /// <summary>
        /// 静态标志位，防止重复加载VM解决方案
        /// 确保VM解决方案在整个应用程序生命周期中只加载一次
        /// </summary>
        private static bool _isVmSolutionLoaded = false;

        /// <summary>
        /// 静态实例引用，用于其他页面访问
        /// </summary>
        public static TemplateConfigPage Instance { get; private set; }

        // ==================== 性能调试系统 ====================
        /// <summary>
        /// 简洁性能监控
        /// </summary>
        private static System.Diagnostics.Stopwatch _simpleTimer = new System.Diagnostics.Stopwatch();
        private static int _detectionCount = 0;
        private static readonly object _performanceLock = new object();

        /// <summary>
        /// 简洁性能监控 - 检测运行状态
        /// </summary>
        private static bool _isDetectionRunning = false;

        /// <summary>
        /// 简洁性能监控 - 开始检测（只有在没有运行时才启动）
        /// </summary>
        public static void StartDetectionTimer()
        {
            lock (_performanceLock)
            {
                if (!_isDetectionRunning)
                {
                    _detectionCount++;
                    _simpleTimer.Restart();
                    _isDetectionRunning = true;
                    
                    // 使用多种方式确保日志被输出
                    string message = $"🚀 开始第{_detectionCount}次检测";
                    Instance?.LogMessage(message, LogLevel.Info);
                    LogManager.Info(message, "检测性能");
                    
                    // 额外的调试信息
                    Instance?.LogMessage($"[调试] 检测状态已设置为运行中，计数器: {_detectionCount}", LogLevel.Info);
                }
                else
                {
                    string warning = $"⚠️ 检测已在运行中，跳过启动 (当前第{_detectionCount}次)";
                    Instance?.LogMessage(warning, LogLevel.Warning);
                    LogManager.Warning(warning, "检测性能");
                }
            }
        }

        /// <summary>
        /// 简洁性能监控 - 结束检测并输出耗时（只有在运行时才结束）
        /// </summary>
        private static void StopDetectionTimer()
        {
            lock (_performanceLock)
            {
                if (_isDetectionRunning)
                {
                    _simpleTimer.Stop();
                    _isDetectionRunning = false;
                    long elapsed = _simpleTimer.ElapsedMilliseconds;
                    
                    string message = $"⏱️ 第{_detectionCount}次检测完成，耗时: {elapsed}ms";
                    if (elapsed > 3000)
                    {
                        message += " ⚠️ (超过3秒)";
                        Instance?.LogMessage(message, LogLevel.Warning);
                    }
                    else
                    {
                        Instance?.LogMessage(message, LogLevel.Info);
                    }
                    
                    // 同时输出到界面日志
                    LogManager.Info(message, "检测性能");
                    
                    // 额外的调试信息
                    Instance?.LogMessage($"[调试] 检测状态已重置为停止", LogLevel.Info);
                }
                else
                {
                    string warning = $"⚠️ 尝试停止检测，但检测未运行 (当前计数: {_detectionCount})";
                    Instance?.LogMessage(warning, LogLevel.Warning);
                    
                    // 增加调试信息，帮助诊断问题
                    Instance?.LogMessage($"[调试] 当前状态 - 运行中: {_isDetectionRunning}, 计数器: {_detectionCount}, 定时器运行中: {_simpleTimer.IsRunning}", LogLevel.Info);
                }
            }
        }

        private int currentStep = 0;
        private int laststep = 0; // 上一步的索引

        /// <summary>
        /// 控制是否需要弹出历史数据清理确认对话框
        /// true: 需要弹出确认对话框（软件启动后首次加载模板或重新选择模板时）
        /// false: 静默处理，不弹出对话框（执行操作、保存模板等日常操作时）
        /// </summary>
        private bool _shouldPromptForDataCleanup = true;

        /// <summary>
        /// 用户对数据清理的选择记录
        /// null: 用户尚未做出选择
        /// true: 用户选择清理无效数据
        /// false: 用户选择不清理无效数据（默认行为，不影响检测）
        /// </summary>
        private bool? _userDataCleanupChoice = false;

        /// <summary>
        /// 当前样品类型
        /// </summary>
        private SampleType currentSampleType = SampleType.Other;

        /// <summary>
        /// 当前模板文件路径（用于相机参数等需要精确指向当前模板的场景）
        /// </summary>
        public string CurrentTemplateFilePath { get; private set; } = string.Empty;

        public string CurrentAlgorithmEngineId => AlgorithmEngineSettingsManager.PreferredEngineId;

        /// <summary>
        /// 当前涂布类型
        /// </summary>
        private CoatingType currentCoatingType = CoatingType.Single;

        /// <summary>
        /// 飞拍是否作为BLK图像使用
        /// </summary>
        private bool isFlyingCameraAsBlk = false;

        /// <summary>
        /// 被隐藏但允许保留参数的步骤类型列表
        /// </summary>
        private readonly HashSet<StepType> hiddenStepTypes = new HashSet<StepType>();

        /// <summary>
        /// 集中化的步骤配置，替代原来分散的硬编码配置
        /// </summary>
        private List<StepConfiguration> stepConfigurations = new List<StepConfiguration>();

        /// <summary>
        /// 步骤分组配置列表
        /// </summary>
        private List<StepGroup> stepGroups = new List<StepGroup>();

        // 图像渲染器（项目级配置）
        private IImageRenderer _imageRenderer;
        private ImageRendererContext _imageRendererContext;

        /// <summary>
        /// 按钮缓存，用于避免重新创建按钮
        /// </summary>
        private Dictionary<int, Button> stepButtonCache = new Dictionary<int, Button>();
        
        /// <summary>
        /// 组按钮缓存
        /// </summary>
        private Dictionary<StepGroup, Button> groupButtonCache = new Dictionary<StepGroup, Button>();
        
        /// <summary>
        /// 上次显示的按钮结构，用于比较是否需要重新生成面板
        /// </summary>
        private string lastButtonStructure = "";

        /// <summary>
        /// 获取步骤名称数组（向后兼容）
        /// </summary>
        private string[] stepNames => stepConfigurations.Select(s => s.DisplayName).ToArray();

        /// <summary>
        /// 统计缺陷类型的字典，键为缺陷类型名称，值为该类型的数量
        /// </summary>
        /// <summary>
        /// 静态统计数据管理器 - 确保在页面切换时数据不丢失
        /// </summary>
        public static class StatisticsManager
        {
            public static Dictionary<string, int> DefectTypeCounter { get; } = new Dictionary<string, int>();
            public static int TotalCount { get; set; } = 0;
            public static int OkCount { get; set; } = 0;
            public static double YieldRate { get; set; } = 0.0;
            
            public static void ClearAll()
            {
                DefectTypeCounter.Clear();
                TotalCount = 0;
                OkCount = 0;
                YieldRate = 0.0;
            }
        }

        /// <summary>
        /// 缺陷类型计数器（用于饼图统计）- 代理到静态管理器
        /// </summary>
        private Dictionary<string, int> defectTypeCounter => StatisticsManager.DefectTypeCounter;

        /// <summary>
        /// 总检测数量 - 代理到静态管理器
        /// </summary>
        private int totalCount
        {
            get => StatisticsManager.TotalCount;
            set => StatisticsManager.TotalCount = value;
        }

        /// <summary>
        /// 良品数量 - 代理到静态管理器
        /// </summary>
        private int okCount
        {
            get => StatisticsManager.OkCount;
            set => StatisticsManager.OkCount = value;
        }

        /// <summary>
        /// 良率（百分比）- 代理到静态管理器
        /// </summary>
        private double yieldRate
        {
            get => StatisticsManager.YieldRate;
            set => StatisticsManager.YieldRate = value;
        }

        /// <summary>
        /// 参数映射表，将UI参数名称映射到VM平台的全局变量名
        /// 【已迁移到ModuleRegistry】- 现在从ModuleRegistry.GetAllParameterMappings()获取
        /// </summary>
        private Dictionary<string, string> parameterToGlobalVariableMap => ModuleRegistry.GetAllParameterMappings();

        /// <summary>
        /// 参数单位转换表，用于指示需要将UI参数转换成像素单位的参数名称及其转换方法
        /// 【已迁移到ModuleRegistry】- 现在从ModuleRegistry.GetAllParameterConversions()获取
        /// </summary>
        private Dictionary<string, Func<string, string>> parameterConversionMap => ModuleRegistry.GetAllParameterConversions();

        /// <summary>
        /// 清理模板中的无效步骤数据
        /// </summary>
        /// <param name="showConfirmDialog">是否显示确认对话框，如果为false则静默清理</param>
        private void CleanupTemplateData(bool showConfirmDialog = true)
        {
            try
            {
                // 获取当前有效的步骤索引列表
                var validStepIndices = stepConfigurations.Select((config, index) => index).ToList();

                // 检测将要删除的数据
                var invalidInputKeys = inputParameterControls.Keys.Where(stepIndex => !validStepIndices.Contains(stepIndex)).ToList();
                var validStepTypes = new HashSet<StepType>(stepConfigurations.Select(s => s.StepType));
                validStepTypes.UnionWith(hiddenStepTypes);
                var invalidTemplateKeys = currentTemplate.InputParameters.Keys.Where(stepType => !validStepTypes.Contains(stepType)).ToList();

                // 如果没有无效数据，直接返回
                if (invalidInputKeys.Count == 0 && invalidTemplateKeys.Count == 0)
                {
                    return;
                }

                // 根据用户的历史选择和当前设置决定是否处理无效数据
                bool shouldCleanData = false;

                if (showConfirmDialog && _shouldPromptForDataCleanup)
                {
                    // 需要弹出对话框询问用户
                    if (ConfirmDataCleanup(invalidInputKeys, invalidTemplateKeys))
                    {
                        // 用户选择清理
                        _userDataCleanupChoice = true;
                        shouldCleanData = true;
                        LogManager.Info("用户选择清理无效历史数据", "模板配置");
                    }
                    else
                    {
                        // 用户选择不清理，记住这个选择
                        _userDataCleanupChoice = false;
                        shouldCleanData = false;
                        LogManager.Info("用户选择保留无效历史数据（默认行为，不影响检测）", "模板配置");
                    }

                    // 标记用户已经做过选择，后续不再弹出对话框
                    _shouldPromptForDataCleanup = false;
                }
                else if (!showConfirmDialog)
                {
                    // 静默模式，使用用户之前的选择或默认不清理
                    shouldCleanData = _userDataCleanupChoice ?? false;
                    if (shouldCleanData)
                    {
                        LogManager.Info($"根据用户之前的选择，静默清理无效历史数据: {invalidInputKeys.Count}个输入控件, {invalidTemplateKeys.Count}个模板步骤", "模板配置");
                    }
                    else
                    {
                        LogManager.Info($"根据用户选择或默认行为，保留无效历史数据: {invalidInputKeys.Count}个输入控件, {invalidTemplateKeys.Count}个模板步骤", "模板配置");
                    }
                }

                // 如果决定不清理数据，则直接返回
                if (!shouldCleanData)
                {
                    return;
                }

                // 执行清理操作
                foreach (var invalidKey in invalidInputKeys)
                {
                    inputParameterControls.Remove(invalidKey);
                }

                foreach (var invalidKey in invalidTemplateKeys)
                {
                    currentTemplate.InputParameters.Remove(invalidKey);
                }

                LogManager.Info($"数据清理完成，移除了 {invalidInputKeys.Count} 个控件步骤和 {invalidTemplateKeys.Count} 个模板步骤的历史数据", "模板配置");
            }
            catch (Exception ex)
            {
                LogManager.Error($"清理模板数据失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 确认数据清理操作，显示详细的删除内容给用户确认
        /// </summary>
        /// <param name="invalidInputKeys">无效的输入控件键</param>
        /// <param name="invalidTemplateKeys">无效的模板参数键</param>
        /// <returns>用户是否确认删除</returns>
        private bool ConfirmDataCleanup(List<int> invalidInputKeys, List<StepType> invalidTemplateKeys)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("检测到以下无效的历史数据，这些数据可能来自旧版本的配置或已删除的步骤：");
            messageBuilder.AppendLine("您可以先拍照留存，这些参数可能需要重新设置，只是版本更迭，无法自动迁移：");
            messageBuilder.AppendLine();

            if (invalidInputKeys.Count > 0)
            {
                messageBuilder.AppendLine("【输入控件中的无效步骤】:");
                foreach (var key in invalidInputKeys)
                {
                    messageBuilder.AppendLine($"  • 步骤索引 {key} (已不存在于当前配置中)");
                }
                messageBuilder.AppendLine();
            }

            if (invalidTemplateKeys.Count > 0)
            {
                messageBuilder.AppendLine("【模板参数中的无效步骤】:");
                foreach (var stepType in invalidTemplateKeys)
                {
                    var paramCount = currentTemplate.InputParameters.ContainsKey(stepType) ?
                        currentTemplate.InputParameters[stepType].Count : 0;
                    messageBuilder.AppendLine($"  • {stepType} (包含 {paramCount} 个参数)");

                    // 显示所有参数详情
                    if (currentTemplate.InputParameters.ContainsKey(stepType))
                    {
                        var parameters = currentTemplate.InputParameters[stepType];
                        foreach (var param in parameters)
                        {
                            messageBuilder.AppendLine($"    - {param.Key}: {param.Value}");
                        }
                    }
                }
                messageBuilder.AppendLine();
            }

            messageBuilder.AppendLine("这些数据将被永久删除。");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("是否继续删除这些无效数据？");
            messageBuilder.AppendLine("• 点击'删除'：删除这些历史数据，保持配置整洁");
            messageBuilder.AppendLine("• 点击'保留'：保留这些数据（不会影响检测功能）");

            var result = ScrollableMessageWindow.Show(
                messageBuilder.ToString(),
                "确认删除无效历史数据",
                true,
                "删除",
                "保留");

            return result == MessageBoxResult.OK;
        }

        /// <summary>
        /// 重置数据清理提示状态，用于加载新模板时
        /// </summary>
        private void ResetDataCleanupPromptForNewTemplate()
        {
            _shouldPromptForDataCleanup = true;
            // 注意：不重置 _userDataCleanupChoice，保持用户的历史选择
            LogManager.Info("重置数据清理提示状态（新模板加载）", "模板配置");
        }

        /// <summary>
        /// 验证步骤索引是否有效
        /// </summary>
        /// <param name="stepIndex">步骤索引</param>
        /// <returns>是否有效</returns>
        private bool IsValidStepIndex(int stepIndex)
        {
            return stepIndex >= 0 && stepIndex < stepConfigurations.Count;
        }

        /// <summary>
        /// 验证步骤类型是否有效
        /// </summary>
        /// <param name="stepType">步骤类型</param>
        /// <returns>是否有效</returns>
        private bool IsValidStepType(StepType stepType)
        {
            return stepConfigurations.Any(config => config.StepType == stepType);
        }

        /// <summary>
        /// 获取步骤显示名称（安全版本）
        /// </summary>
        /// <param name="stepIndex">步骤索引</param>
        /// <returns>步骤显示名称</returns>
        private string GetSafeStepName(int stepIndex)
        {
            if (IsValidStepIndex(stepIndex))
            {
                return stepNames[stepIndex];
            }
            return $"未知步骤[索引{stepIndex}]";
        }

        /// <summary>
        /// 根据StepType获取步骤显示名称（安全版本）
        /// </summary>
        /// <param name="stepType">步骤类型</param>
        /// <returns>步骤显示名称</returns>
        private string GetSafeStepNameByType(StepType stepType)
        {
            var config = stepConfigurations.FirstOrDefault(c => c.StepType == stepType);
            return config?.DisplayName ?? $"未知步骤[{stepType}]";
        }

        /// <summary>
        /// 将模板中所有已保存的参数应用到VM全局变量（改进版 - 只处理有效步骤）
        /// </summary>
        public void ApplyParametersToGlobalVariables()
        {
            try
            {
                // 根据当前样品类型设置TYPE全局变量
                try
                {
                    int typeValue = (int)currentSampleType;
                    AlgorithmGlobalVariables.Set("TYPE", typeValue.ToString());
                    LogManager.Info($"设置样品类型全局变量 TYPE = {typeValue} ({currentSampleType})", "模板配置");
                }
                catch (Exception ex)
                {
                    string errorMsg = $"设置样品类型全局变量失败: {ex.Message}";
                    LogManager.Error(errorMsg, "模板配置");
                    MessageBox.Show(errorMsg);
                }

                // 根据当前涂布类型设置涂布数目全局变量
                try
                {
                    int coatingCount = (int)currentCoatingType;
                    AlgorithmGlobalVariables.Set("涂布数目", coatingCount.ToString());
                    LogManager.Info($"设置涂布数目全局变量 涂布数目 = {coatingCount} ({currentCoatingType})", "模板配置");
                }
                catch (Exception ex)
                {
                    string errorMsg = $"设置涂布数目全局变量失败: {ex.Message}";
                    LogManager.Error(errorMsg, "全局变量设置");
                    MessageBox.Show(errorMsg);
                }

                // 设置构建缓存全局变量
                try
                {
                    AlgorithmGlobalVariables.Set("构建缓存", "1");
                    LogManager.Info($"设置构建缓存全局变量 构建缓存 = 1", "模板配置");
                }
                catch (Exception ex)
                {
                    string errorMsg = $"设置构建缓存全局变量失败: {ex.Message}";
                    LogManager.Error(errorMsg, "全局变量设置");
                    MessageBox.Show(errorMsg);
                }

                StringBuilder errorLog = new StringBuilder();

                // 先清理无效的历史数据（静默清理，不弹出对话框）
                CleanupTemplateData(false);

                // 只遍历当前有效的步骤参数
                foreach (var stepEntry in currentTemplate.InputParameters)
                {
                    StepType stepType = stepEntry.Key;
                    var stepParameters = stepEntry.Value;

                    // 验证步骤类型是否有效
                    if (!IsValidStepType(stepType))
                    {
                        LogMessage($"跳过无效步骤类型: {stepType}（可能是历史数据）", LogLevel.Warning);
                        continue;
                    }

                    string stepName = GetSafeStepNameByType(stepType);

                    foreach (var paramPair in stepParameters)
                    {
                        string paramName = paramPair.Key;
                        string paramValue = paramPair.Value;
                        string processedValue = paramValue; // 保存转换后的值，用于错误报告

                        try
                        {
                            // 如果参数名称在映射表中有对应的全局变量名
                            if (parameterToGlobalVariableMap.TryGetValue(paramName, out string globalVarName))
                            {
                                // 检查是否需要进行单位转换
                                if (parameterConversionMap.TryGetValue(paramName, out var conversionFunc))
                                {
                                    try
                                    {
                                        processedValue = conversionFunc(paramValue);
                                    }
                                    catch (Exception ex)
                                    {
                                        // 记录转换错误
                                        string errorMsg = $"步骤{stepType} ({stepName}) 参数 '{paramName}' 值 '{paramValue}' 转换失败: {ex.Message}";
                                        errorLog.AppendLine(errorMsg);
                                        PageManager.Page1Instance?.LogUpdate(errorMsg);
                                        continue; // 跳过这个参数，继续处理其他参数
                                    }
                                }

                                // 尝试设置全局变量
                                try
                                {
                                    AlgorithmGlobalVariables.Set(globalVarName, processedValue);
                                }
                                catch (Exception ex)
                                {
                                    // 记录设置全局变量错误
                                    string errorMsg = $"步骤{stepType} ({stepName}) 设置全局变量 '{globalVarName}' 值 '{processedValue}' 失败: {ex.Message}";
                                    errorLog.AppendLine(errorMsg);
                                    MessageBox.Show(errorMsg);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 捕获每个参数处理过程中的任何其他错误
                            string errorMsg = $"处理步骤{stepType} ({stepName}) 参数 '{paramName}' 时出错: {ex.Message}";
                            errorLog.AppendLine(errorMsg);
                            LogManager.Error(errorMsg, "参数处理");
                        }
                    }
                }

                // 同时也保存并应用当前步骤最新的参数（可能还未保存到字典中）
                if (inputParameterControls.ContainsKey(currentStep))
                {
                    string currentStepName = GetSafeStepName(currentStep);
                    
                    foreach (var pair in inputParameterControls[currentStep])
                    {
                        string paramName = pair.Key;
                        string paramValue = pair.Value.Text;
                        string processedValue = paramValue; // 保存转换后的值，用于错误报告

                        try
                        {
                            // 如果参数名称在映射表中有对应的全局变量名
                            if (parameterToGlobalVariableMap.TryGetValue(paramName, out string globalVarName))
                            {
                                // 检查是否需要进行单位转换
                                if (parameterConversionMap.TryGetValue(paramName, out var conversionFunc))
                                {
                                    try
                                    {
                                        processedValue = conversionFunc(paramValue);
                                    }
                                    catch (Exception ex)
                                    {
                                        // 记录转换错误
                                        string errorMsg = $"当前步骤{currentStep} ({currentStepName}) 参数 '{paramName}' 值 '{paramValue}' 转换失败: {ex.Message}";
                                        errorLog.AppendLine(errorMsg);
                                        LogManager.Error(errorMsg, "参数转换");
                                        continue; // 跳过这个参数，继续处理其他参数
                                    }
                                }

                                // 尝试设置全局变量
                                try
                                {
                                    AlgorithmGlobalVariables.Set(globalVarName, processedValue);
                                    LogManager.Info($"设置全局变量 '{globalVarName}' = '{processedValue}' (当前编辑步骤)", "参数应用");
                                }
                                catch (Exception ex)
                                {
                                    // 记录设置全局变量错误
                                    string errorMsg = $"当前步骤{currentStep} ({currentStepName}) 设置全局变量 '{globalVarName}' 值 '{processedValue}' 失败: {ex.Message}";
                                    errorLog.AppendLine(errorMsg);
                                    LogManager.Error(errorMsg, "参数应用");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 捕获每个参数处理过程中的任何其他错误
                            string errorMsg = $"处理当前步骤{currentStep} ({currentStepName}) 参数 '{paramName}' 时出错: {ex.Message}";
                            errorLog.AppendLine(errorMsg);
                            PageManager.Page1Instance?.LogUpdate(errorMsg);
                        }
                    }
                }

                // 🔧 暂时屏蔽：划痕检测模型文件路径处理
                // 由于配置模板路径仍有问题，暂时恢复到最简单状态并屏蔽
                /*
                // 特殊处理：划痕检测模型文件路径
                if (currentTemplate.InputParameters.ContainsKey(StepType.ScratchDetection) &&
                    currentTemplate.InputParameters[StepType.ScratchDetection].ContainsKey("模型文件路径"))
                {
                    try
                    {
                        HandleScratchDetectionModelPath(errorLog);
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = $"处理划痕检测模型文件路径时出错: {ex.Message}";
                        errorLog.AppendLine(errorMsg);
                        LogManager.Error(errorMsg, "文件路径处理");
                    }
                }
                */

                // 如果收集到任何错误，显示详细的错误报告
                if (errorLog.Length > 0)
                {
                    string errorReport = "应用参数到全局变量时发生以下错误:\n\n" + errorLog.ToString();

                    // 显示错误报告对话框
                    MessageBox.Show(errorReport, "参数应用错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // 处理方法级别的异常
                string errorMessage = $"应用参数到全局变量时出错: {ex.Message}\n{ex.StackTrace}";
                MessageBox.Show(errorMessage, "参数应用错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }





        // 当前的模板参数
        private TemplateParameters currentTemplate = new TemplateParameters();

        // 算法引擎选择控件
        private ComboBox _algorithmEngineComboBox;
        private TextBlock _algorithmEngineHintText;

        // 输入参数控件字典，用于快速访问和更新参数值
        private Dictionary<int, Dictionary<string, TextBox>> inputParameterControls = new Dictionary<int, Dictionary<string, TextBox>>();

        // 未保存变更追踪
        private bool hasUnsavedChanges = false;
        private DateTime lastSaveTime = DateTime.MinValue;

        // 3D视图控件
        private System.Windows.Forms.Control _threeDViewHostChild = new System.Windows.Forms.Panel();

        // 🔧 暂时屏蔽：跟踪上次加载的划痕检测模型文件路径，避免重复加载
        // private string _lastLoadedScratchModelPath = string.Empty;

        /// <summary>
        /// 默认构造函数（使用默认样品类型）
        /// </summary>
        public TemplateConfigPage()
        {
            // 使用默认的样品类型初始化
            InitializeWithSampleType(SampleType.Other);
        }

        /// <summary>
        /// 带样品类型参数的构造函数
        /// </summary>
        /// <param name="sampleType">样品类型</param>
        public TemplateConfigPage(SampleType sampleType)
        {
            InitializeWithSampleType(sampleType);
        }

        /// <summary>
        /// 带样品类型与算法引擎参数的构造函数
        /// </summary>
        public TemplateConfigPage(SampleType sampleType, string algorithmEngineId)
        {
            InitializeWithSampleType(sampleType, CoatingType.Single, algorithmEngineId);
        }

        /// <summary>
        /// 带样品类型和涂布类型参数的构造函数
        /// </summary>
        /// <param name="sampleType">样品类型</param>
        /// <param name="coatingType">涂布类型</param>
        public TemplateConfigPage(SampleType sampleType, CoatingType coatingType)
        {
            InitializeWithSampleType(sampleType, coatingType);
        }

        /// <summary>
        /// 带样品类型、涂布类型与算法引擎参数的构造函数
        /// </summary>
        public TemplateConfigPage(SampleType sampleType, CoatingType coatingType, string algorithmEngineId)
        {
            InitializeWithSampleType(sampleType, coatingType, algorithmEngineId);
        }

        /// <summary>
        /// 使用指定样品类型初始化页面
        /// </summary>
        /// <param name="sampleType">样品类型</param>
        /// <param name="coatingType">涂布类型</param>
        private void InitializeWithSampleType(SampleType sampleType, CoatingType coatingType = CoatingType.Single, string algorithmEngineId = null)
        {
            // 保存样品类型和涂布类型
            currentSampleType = sampleType;
            currentCoatingType = coatingType;

            var preferredEngineId = AlgorithmEngineSettingsManager.PreferredEngineId;
            currentTemplate.AlgorithmEngineId = preferredEngineId;

            // 更新相机选择状态，支持飞拍作为BLK的布局调整
            UpdateCameraSelectionFlags();

            // 初始化步骤配置（基于样品类型和涂布类型）
            stepConfigurations = InitializeStepConfigurations(sampleType, coatingType);
            ApplyCameraSpecificStepLayout();

            // 初始化时标记为已保存状态（新建模板未修改）
            hasUnsavedChanges = false;
            lastSaveTime = DateTime.MinValue;

            InitializeComponent();
            
            // 设置静态实例引用
            Instance = this;

            InitializeImageRenderer();

            // 动态生成步骤按钮
            InitializeStepGroups(); // 初始化步骤分组
            GenerateStepButtons();

            // 注意：统计数据已在构造函数中初始化，这里不再重复初始化

            // 创建Templates目录
            string templatesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            if (!Directory.Exists(templatesDir))
                Directory.CreateDirectory(templatesDir);

            // 设置当前模板的初始属性
            currentTemplate.SampleType = sampleType; // 设置样品类型
            currentTemplate.CreatedTime = DateTime.Now;
            currentTemplate.LastModifiedTime = DateTime.Now;

            // 只在第一次时加载VM解决方案，避免重复加载
            TryAutoLoadVmSolution();
            
            // 设置样品类型全局变量
            SetSampleTypeGlobalVariable();
            
            // 初始化配置页面的DataGrid
            InitializeConfigDataGrid();
            
            // 更新当前模板名称显示
            UpdateCurrentTemplateNameDisplay();
            
            // 初始化3D视图控件
            Initialize3DView();
        }

        /// <summary>
        /// 设置样品类型和涂布类型到VM全局变量
        /// </summary>
        private void SetSampleTypeGlobalVariable()
        {
            try
            {
                if (!_isVmSolutionLoaded)
                {
                    LogManager.Info("VM未加载，改为写入算法全局变量（OpenCV/ONNX）", "初始化");
                }

                // 设置样品类型 TYPE
                int typeValue = (int)currentSampleType;
                AlgorithmGlobalVariables.Set("TYPE", typeValue.ToString());
                LogManager.Info($"初始化时设置样品类型全局变量 TYPE = {typeValue} ({currentSampleType})", "初始化");

                // 设置涂布数目
                int coatingCount = (int)currentCoatingType;
                AlgorithmGlobalVariables.Set("涂布数目", coatingCount.ToString());
                LogManager.Info($"初始化时设置涂布数目全局变量 涂布数目 = {coatingCount} ({currentCoatingType})", "初始化");
            }
            catch (Exception ex)
            {
                string errorMsg = $"设置样品类型或涂布类型全局变量失败: {ex.Message}";
                LogManager.Error(errorMsg, "初始化");
                MessageBox.Show(errorMsg);
            }
        }

        private void InitializeImageRenderer()
        {
            try
            {
                _imageRendererContext = new ImageRendererContext
                {
                    VmRender1 = VmRender1,
                    VmRender2_1 = VmRender2_1,
                    VmRender2_2 = VmRender2_2,
                    PreviewImage1 = PreviewImage1,
                    PreviewImage2_1 = PreviewImage2_1,
                    PreviewImage2_2 = PreviewImage2_2
                };

                _imageRenderer = ImageRendererManager.ResolveRenderer(_imageRendererContext);
                _imageRenderer?.Bind(_imageRendererContext);
            }
            catch (Exception ex)
            {
                LogManager.Warning($"初始化图像渲染器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据样品类型初始化步骤配置
        /// </summary>
        /// <param name="sampleType">样品类型</param>
        /// <param name="coatingType">涂布类型</param>
        /// <returns>配置好的步骤列表</returns>
        private List<StepConfiguration> InitializeStepConfigurations(SampleType sampleType, CoatingType coatingType = CoatingType.Single)
        {
            // 使用 ModuleRegistry 获取配置（新的高内聚方式）
            var configurations = ModuleRegistry.GetStepConfigurations(sampleType, coatingType);
            
            // 设置事件处理器（因为工厂中的Handler为null）
            AttachActionHandlers(configurations);

            // 设置特殊处理器
            foreach (var config in configurations)
            {
                if (config.IsSpecialStep && config.StepType == StepType.TemplateName)
                {
                    config.SpecialHandler = templateConfig =>
                    {
                        // 模板命名步骤的特殊处理逻辑
                        if (inputParameterControls.ContainsKey(currentStep))
                        {
                            if (inputParameterControls[currentStep].ContainsKey("模板名称"))
                                inputParameterControls[currentStep]["模板名称"].Text = currentTemplate.TemplateName ?? "";
                            if (inputParameterControls[currentStep].ContainsKey("备注"))
                                inputParameterControls[currentStep]["备注"].Text = currentTemplate.Remark ?? "";
                            if (inputParameterControls[currentStep].ContainsKey("启用3D检测"))
                            {
                                // 保存3D检测启用状态到模板
                                if (bool.TryParse(inputParameterControls[currentStep]["启用3D检测"].Text, out bool enable3D))
                                {
                                    if (currentTemplate.Detection3DParams == null)
                                        currentTemplate.Detection3DParams = new Detection3DParameters();
                                    
                                    currentTemplate.Detection3DParams.Enable3DDetection = enable3D;
                                    
                                    // 同时更新内存中的3D参数
                                    ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = enable3D;
                                }
                            }
                        }
                    };
                }
            }

            return configurations;
        }

        /// <summary>
        /// 补齐步骤动作的事件处理器
        /// </summary>
        private void AttachActionHandlers(IEnumerable<StepConfiguration> configurations)
        {
            foreach (var config in configurations)
            {
                foreach (var action in config.Actions)
                {
                    switch (action.Name)
                    {
                        case "浏览图片":
                            action.Handler = BrowseImage_Click;
                            break;
                        case "浏览模板":
                            action.Handler = BrowseMatchTemplate_Click;
                            break;
                        case "浏览BLK模板":
                            action.Handler = BrowseBlkMatchTemplate_Click;
                            break;
                        case "浏览镀膜模板":
                            action.Handler = BrowseCoatingMatchTemplate_Click;
                            break;
                        case "沿用PKG模板":
                            action.Handler = UsePkgTemplateForCoating_Click;
                            break;
                        case "3D检测":
                            action.Handler = Open3DDetection_Click;
                            break;
                        case "保存模板":
                            action.Handler = SaveTemplate_Click;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 更新相机选择状态（飞拍是否作为BLK图像）
        /// </summary>
        private void UpdateCameraSelectionFlags()
        {
            try
            {
                bool flyingAsBlk = false;

                // 仅使用当前模板中的相机配置，避免继承上一次相机界面的状态
                if (currentTemplate?.CameraParams != null)
                {
                    flyingAsBlk = currentTemplate.CameraParams.LidImageSelection == 1;
                }

                if (isFlyingCameraAsBlk != flyingAsBlk)
                {
                    LogManager.Info($"更新相机配置：飞拍作为BLK图像 = {(flyingAsBlk ? "是" : "否")}", "模板配置");
                }

                isFlyingCameraAsBlk = flyingAsBlk;
            }
            catch (Exception ex)
            {
                LogManager.Warning($"更新相机配置状态失败: {ex.Message}", "模板配置");
                isFlyingCameraAsBlk = false;
            }
        }

        /// <summary>
        /// 根据相机选择调整布局：飞拍作为BLK时替换银面定位为镀膜图PKG定位
        /// </summary>
        private void ApplyCameraSpecificStepLayout()
        {
            hiddenStepTypes.Clear();

            if (!isFlyingCameraAsBlk)
            {
                return;
            }

            int replaceIndex = stepConfigurations.FindIndex(c => c.StepType == StepType.CoatingChipEnhance);
            if (replaceIndex >= 0)
            {
                hiddenStepTypes.Add(StepType.CoatingChipEnhance);
                stepConfigurations.RemoveAt(replaceIndex);

                var coatingPkgSteps = ModuleRegistry.GetCoatingPkgConfigurations();
                stepConfigurations.InsertRange(replaceIndex, coatingPkgSteps);
                AttachActionHandlers(coatingPkgSteps);

                LogManager.Info("飞拍作为BLK图像：已用“镀膜图PKG定位”替换“银面图晶片定位”", "模板配置");
            }
            else
            {
                LogManager.Warning("未找到“银面图晶片定位”步骤，无法替换为“镀膜图PKG定位”", "模板配置");
            }
        }

        /// <summary>
        /// 返回按钮点击事件，带保存检测
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否有未保存的更改
            if (HasUnsavedChanges())
            {
                // 显示保存确认对话框
                var result = ShowUnsavedChangesDialog();
                
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        // 用户选择保存
                        SaveTemplate_Click(null, null);
                        // 保存成功后返回
                        NavigateBack();
                        break;
                    case MessageBoxResult.No:
                        // 用户选择不保存，直接返回
                        NavigateBack();
                        break;
                    case MessageBoxResult.Cancel:
                        // 用户取消，不做任何操作
                        return;
                }
            }
            else
            {
                // 没有未保存的更改，直接返回
                NavigateBack();
            }
        }

        /// <summary>
        /// 执行导航返回操作
        /// </summary>
        private void NavigateBack()
        {
            try
            {
                // 🔧 关键修复：使用统一的状态重置机制
                WpfApp2.UI.Page1.PageManager.ResetDetectionManagerOnPageReturn("模板配置页面");
                
                // 🔧 关键修复：使用正确的导航方式返回主页面（与HardwareConfigPage保持一致）
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow != null)
                {
                    mainWindow.ContentC.Content = mainWindow.frame1; // 返回Page1的正确容器
                    LogMessage("已返回主界面");
                }
                else
                {
                    LogMessage("无法找到主窗口", LogLevel.Error);
                }
                                    }
                                    catch (Exception ex)
                                    {
                LogMessage($"导航到主页面失败: {ex.Message}", LogLevel.Error);
            }
        }



        /// <summary>
        /// 检查是否有未保存的更改
        /// </summary>
        /// <returns>如果有未保存的更改返回true，否则返回false</returns>
        private bool HasUnsavedChanges()
        {
            // 只检查标志位，不检查控件值
            // 控件有值不代表未保存，只有用户点击"执行"后才标记为未保存
            return hasUnsavedChanges;
        }

        /// <summary>
        /// 显示未保存更改的确认对话框
        /// </summary>
        /// <returns>用户的选择结果</returns>
        private MessageBoxResult ShowUnsavedChangesDialog()
        {
            return MessageBox.Show(
                "检测到未保存的配置更改。\n\n是否要保存当前配置？\n\n• 点击\"是\"保存配置并退出\n• 点击\"否\"放弃更改并退出\n• 点击\"取消\"继续编辑",
                "保存配置确认",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes
            );
        }

        /// <summary>
        /// 标记配置已发生更改
        /// </summary>
        private void MarkAsChanged()
        {
            hasUnsavedChanges = true;
            LogManager.Info("配置已标记为未保存状态", "配置管理");
        }

        /// <summary>
        /// 标记配置已保存
        /// </summary>
        private void MarkAsSaved()
        {
            hasUnsavedChanges = false;
            lastSaveTime = DateTime.Now;
            LogManager.Info("配置已标记为已保存状态", "配置管理");
        }

        /// <summary>
        /// 显示log按钮点击事件
        /// </summary>
        private void ShowLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取Page1的日志数据
                System.Collections.IList logItems = null;
                if (PageManager.Page1Instance != null)
                {
                    logItems = PageManager.Page1Instance.GetLogItems();
                }

                // 使用统一的日志查看器管理类
                Models.LogViewerManager.ShowLogViewer(
                    ownerWindow: Window.GetWindow(this),
                    windowTitle: "模板配置",
                    logItems: logItems,
                    clearLogAction: () => PageManager.Page1Instance?.ClearLog(),
                    updateLogAction: (message) => LogMessage(message, LogLevel.Info)
                );
            }
            catch (Exception ex)
            {
                LogMessage($"显示日志失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"显示日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 配置页面数据分析按钮点击事件
        /// </summary>
        private void ConfigDataAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogManager.Info("从模板配置打开质量分析仪表板");
                
                // 使用新版本的分析窗口管理器
                if (PageManager.Page1Instance != null)
                {
                    SmartAnalysisWindowManager.ShowAnalysisWindow(PageManager.Page1Instance);
                    LogMessage("已打开新版质量分析仪表板", LogLevel.Info);
                }
                else
                {
                    LogMessage("Page1实例未初始化，无法打开分析窗口", LogLevel.Warning);
                    MessageBox.Show("Page1实例未初始化，无法打开分析窗口。请先返回主页面。", 
                        "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"打开质量分析仪表板失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开质量分析仪表板失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                try
                {
                    int index;

                    // 处理Tag可能是字符串的情况
                    if (button.Tag is int)
                    {
                        index = (int)button.Tag;
                    }
                    else if (button.Tag is string && int.TryParse(button.Tag.ToString(), out int parsedIndex))
                    {
                        index = parsedIndex;
                    }
                    else
                    {
                        throw new InvalidCastException($"按钮Tag类型无效: {button.Tag.GetType().Name}");
                    }

                    // 验证索引范围
                    if (index < 0 || index >= stepNames.Length)
                    {
                        throw new IndexOutOfRangeException($"步骤索引超出范围: {index}");
                    }

                    // 调用内部实现
                    ConfigStepButton_Click_Internal(index);
                }
                catch (Exception ex)
                {
                    // 记录任何可能的异常
                    LogMessage($"更新UI时出错: {ex.Message}", LogLevel.Error);

                    // 获取按钮详细信息用于诊断
                    string tagValue = button.Tag?.ToString() ?? "null";
                    string buttonName = button.Name ?? "未命名按钮";
                    string buttonContent = button.Content?.ToString() ?? "无内容";

                    MessageBox.Show(
                        $"更新UI时出错: {ex.Message}\n\n" +
                        $"诊断信息\n按钮名称: {buttonName}\n内容: {buttonContent}\n" +
                        $"Tag值: {tagValue}\nTag类型: {(button.Tag != null ? button.Tag.GetType().Name : "null")}",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                // 如果按钮没有Tag值，记录错误并显示提示
                string errorMessage = "按钮没有有效的Tag值";
                PageManager.Page1Instance?.LogUpdate(errorMessage);

                // 添加用户界面提示
                if (sender is Button btn)
                {
                    string tagValue = btn.Tag?.ToString() ?? "null";
                    string buttonName = btn.Name ?? "未命名按钮";
                    string buttonContent = btn.Content?.ToString() ?? "无内容";

                    MessageBox.Show(
                        $"{errorMessage}\n\n诊断信息\n按钮名称: {buttonName}\n内容: {buttonContent}\n" +
                        $"Tag值: {tagValue}\nTag类型: {(btn.Tag != null ? btn.Tag.GetType().Name : "null")}",
                        "配置错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        errorMessage,
                        "配置错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        private void PreviousStep_Click(object sender, RoutedEventArgs e)
        {
            // 先保存当前步骤的参数（修复：之前切换步骤时没有保存参数）
            SaveStepParameters(currentStep);
            
            laststep = currentStep;
            if (currentStep > 0)
            {
                currentStep--;
                UpdateUI(currentStep);
                // UpdateUI 中已经会调用 UpdateStepButtons，而 UpdateStepButtons 会调用 GenerateStepButtons
            }
        }

        private void NextStep_Click(object sender, RoutedEventArgs e)
        {
            // 先保存当前步骤的参数（修复：之前切换步骤时没有保存参数）
            SaveStepParameters(currentStep);
            
            laststep = currentStep;
            if (currentStep < stepNames.Length - 1)
            {
                currentStep++;
                UpdateUI(currentStep);
                // UpdateUI 中已经会调用 UpdateStepButtons，而 UpdateStepButtons 会调用 GenerateStepButtons
            }
        }

        private void UpdateImageSdk()
        {
            try
            {
                // 校准界面：始终使用图片选择步骤中设置的固定图片路径
                LogMessage("更新图像SDK：开始设置图片路径到VM模块", LogLevel.Info);
                
                // 直接调用SetImagePathsToVM，它会处理所有的图片路径逻辑
                SetImagePathsToVM();
                
                LogMessage("更新图像SDK：图片路径设置完成", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"更新图像SDK失败: {ex.Message}", LogLevel.Error);
            }
        }

        private void Execute_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                // 🔧 只有在用户点击"执行"按键时才标记配置变更
                MarkAsChanged();
                
                // 先保存当前步骤的参数到字典
                SaveStepParameters(currentStep);

                // 获取当前步骤相关参数
                string stepName = stepNames[currentStep];
                var currentStepConfig = stepConfigurations[currentStep];

                // 统一执行流程：所有步骤都执行相同的流程
                ExecuteUnifiedFlow(stepName);

                
                
                // 执行完成后刷新配置页面的DataGrid
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshConfigDataGrid();
                }), DispatcherPriority.Normal); // 使用Normal优先级，确保及时刷新
                    }
                    catch (Exception ex)
                    {
                MessageBox.Show($"执行{stepNames[currentStep]}操作时出错: {ex.Message}", "执行错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 自动执行模板 - 在模板加载后自动执行一次
        /// </summary>
        public void AutoExecuteTemplate()
        {
            try
            {
                // 保存当前步骤的参数到字典（但不标记为已修改）
                SaveStepParameters(currentStep);

                // 获取当前步骤相关参数
                string stepName = stepNames[currentStep];
                var currentStepConfig = stepConfigurations[currentStep];

                // 统一执行流程：所有步骤都执行相同的流程
                ExecuteUnifiedFlow(stepName);

                // 执行完成后刷新配置页面的DataGrid
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshConfigDataGrid();
                }), DispatcherPriority.Normal);

                LogMessage($"自动执行模板完成: {stepName}", LogLevel.Info);

                // 🔧 简单方案：延迟3秒后退出配置模式，确保模板检测完全完成
                if (ShouldExitTemplateConfigModeAfterAutoExecute())
                {
                    PageManager.Page1Instance?.DetectionManager?.RequestExitTemplateConfigAfterNextUnifiedJudgement();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"自动执行模板时出错: {ex.Message}", LogLevel.Error);

                // 即使出错，也延迟3秒后退出配置模式
                if (ShouldExitTemplateConfigModeAfterAutoExecute())
                {
                    PageManager.Page1Instance?.DetectionManager?.SetSystemState(SystemDetectionState.WaitingForTrigger);
                }
            }
        }

        private bool ShouldExitTemplateConfigModeAfterAutoExecute()
        {
            try
            {
                var mainWindow = Application.Current?.MainWindow as MainWindow;
                if (mainWindow?.ContentC?.Content is Frame activeFrame)
                {
                    if (ReferenceEquals(activeFrame.Content, this))
                    {
                        return false;
                    }
                }

                if (IsLoaded && IsVisible)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 模板配置专用的执行流程
        /// 统一执行完整的"获取路径图像"流程，确保每次都有回调和数据更新
        /// </summary>
        /// <param name="stepName">步骤名称</param>
        private void ExecuteUnifiedFlow(string stepName)
        {
            try
            {
                // 设置为配置模式
                if (PageManager.Page1Instance?.DetectionManager != null)
                {
                    // 设置系统状态为模板配置模式
                    // 这样可以保留数据处理功能，但跳过统计和存图
                    PageManager.Page1Instance.DetectionManager.SetSystemState(SystemDetectionState.TemplateConfiguring);
                }

                bool useVmEngine = ShouldAutoLoadVmSolution();

                // 1. 更新图像SDK（仅VM流程需要）
                if (useVmEngine)
                {
                    UpdateImageSdk();
                }

                // 2. 将参数应用到全局变量
                ApplyParametersToGlobalVariables();

                // 3. VM流程：执行"获取路径图像"；算法流程：直接使用当前路径
                if (useVmEngine)
                {
                    ExecuteImageSelectionStep();
                }

                // 4. 生成当前图像组（与VM解耦）
                try
                {
                    var (source1, source2_1, source2_2) = GetCurrentImagePaths();
                    _currentImageGroup = BuildImageGroupFromPaths(source1, source2_1, source2_2);

                    if (_currentImageGroup == null)
                    {
                        LogMessage("未找到有效图像路径，无法生成图像组", LogLevel.Warning);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"创建当前图像组失败: {ex.Message}", LogLevel.Warning);
                    return;
                }

                // 5. 算法引擎流程（OpenCV + ONNX）
                _imageRenderer?.DisplayImageGroup(_currentImageGroup);

                if (!useVmEngine)
                {
                    _ = PageManager.Page1Instance?.ExecuteAlgorithmPipelineForImageGroup(_currentImageGroup, isTemplateConfig: true);
                    LogMessage($"已为步骤 {stepName} 执行算法引擎流程", LogLevel.Info);
                    return;
                }

                // 3D执行/渲染已迁移到独立进程（Host/Tool），模板配置阶段主进程不执行3D。
                LogMessage($"已为步骤 {stepName} 执行完整的获取路径图像流程", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"执行{stepName}失败: {ex.Message}", LogLevel.Error);
            }
        }

        private ImageGroupSet BuildImageGroupFromPaths(string source1, string source2_1, string source2_2)
        {
            if (string.IsNullOrWhiteSpace(source1))
            {
                return null;
            }

            string parentDir = Path.GetDirectoryName(source1);
            string baseName = Path.GetFileNameWithoutExtension(source1);

            if (baseName.EndsWith("_1") || baseName.EndsWith("_0"))
            {
                baseName = baseName.Substring(0, baseName.LastIndexOf('_'));
            }

            var imageGroup = new ImageGroupSet
            {
                Source1Path = source1,
                Source2_1Path = source2_1,
                Source2_2Path = source2_2,
                BaseName = baseName
            };

            bool is3DImageEnabled = ThreeDSettings.CurrentDetection3DParams?.Enable3DDetection == true;
            if (is3DImageEnabled)
            {
                string suffix = Path.GetFileNameWithoutExtension(source1);
                if (suffix.Contains("_"))
                {
                    suffix = suffix.Substring(suffix.LastIndexOf('_'));
                }

                Page1.FindAndSet3DImagesForGroup(Path.GetDirectoryName(parentDir), suffix, imageGroup, enableLogging: true);
                LogMessage("已为图像组添加3D图像路径", LogLevel.Info);
            }

            LogMessage($"已创建当前图像组: {baseName}, 2D图像: {imageGroup.Has2DImages}, 3D图像: {imageGroup.Has3DImages}", LogLevel.Info);
            return imageGroup;
        }

        /// <summary>
        /// 执行图片选择步骤的特定逻辑
        /// </summary>
        private void ExecuteImageSelectionStep()
        {
            try
            {
                LogMessage("开始执行图像选择步骤", LogLevel.Info);

                // 🔧 修复：在新检测开始前清空3D缓存，确保不显示上次数据
                Page1.Clear3DDataCache();
                LogMessage("已清空3D数据缓存", LogLevel.Info);

                // 重置VM回调标志，开始新的检测周期
                Page1.ResetVmCallbackFlag();
                LogMessage("已重置VM回调标志", LogLevel.Info);

                // 检查VmSolution实例是否可用
                if (VmSolution.Instance == null)
                {
                    LogMessage("VmSolution.Instance为null，无法获取图像流程", LogLevel.Error);
                    throw new InvalidOperationException("VmSolution实例未初始化");
                }

                // 获取"获取路径图像"流程并执行
                var imagePathProcedure = VmSolution.Instance["获取路径图像"] as VmProcedure;
                if (imagePathProcedure != null)
                {
                    LogMessage("找到'获取路径图像'流程，开始执行", LogLevel.Info);
                    imagePathProcedure.Run();
                    LogManager.Info("图像获取流程执行完成", "图像处理");
                    LogMessage("图像获取流程执行成功", LogLevel.Info);
                }
                else
                {
                    LogMessage("未找到'获取路径图像'流程", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"执行图像选择步骤失败: {ex.Message}", LogLevel.Error);
                LogManager.Error($"执行图像选择步骤失败: {ex.Message}\r\n堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }


        private void UpdateUI(int stepIndex)
        {
            // 保存上一个步骤的参数值
            SaveStepParameters();

            // 验证索引范围
            if (stepIndex < 0 || stepIndex >= stepConfigurations.Count)
            {
                LogManager.Warning($"步骤索引超出范围: {stepIndex}", "UI更新");
                return;
            }

            // 获取当前步骤配置
            var config = stepConfigurations[stepIndex];

            // 更新按钮状态
            UpdateStepButtons(stepIndex);

            // 清空参数面板
            InputParametersPanel.Children.Clear();
            // 注意：不再需要清空OutputParametersPanel，因为已替换为ConfigDataGrid

            // 确保存储当前步骤的控件字典已初始化
            if (!inputParameterControls.ContainsKey(stepIndex))
                inputParameterControls[stepIndex] = new Dictionary<string, TextBox>();
            else
                inputParameterControls[stepIndex].Clear();

            // 插入算法引擎选择（仅模板命名步骤显示）
            InsertAlgorithmEngineSelector(config);

            // 动态构建UI
            BuildParametersUI(config);
            BuildActionsUI(config);
            BuildLabelsUI(config);

            // 切换VM模块
            ChangeVM(config.VmModuleName);

            // 3D配置步骤特殊处理：切换到3D渲染界面
            if (config.StepType == StepType.ThreeDConfiguration)
            {
                SwitchTo3DView();
            }
            else
            {
                SwitchToVMView();
            }

            // 处理特殊步骤
            if (config.IsSpecialStep && config.SpecialHandler != null)
            {
                config.SpecialHandler.Invoke(config);
            }

            // 处理晶片位置与尺寸步骤的联动逻辑
            if (config.StepType == StepType.ChipPositionSize)
            {
                InitializeAutoBlkRelatedControls();
            }
            // 处理银面几何尺寸步骤的联动逻辑
            else if (config.StepType == StepType.CoatingGeometrySize)
            {
                InitializeCoatingGeometryRelatedControls();
            }
            // 处理胶点尺寸检测步骤的联动逻辑
            else if (config.StepType == StepType.UpperGluePointDetection)
            {
                InitializeAiGluePointRelatedControls();
            }
            // 处理BLK破损检测步骤的联动逻辑
            else if (config.StepType == StepType.BlkDamageDetection)
            {
                InitializeDamageAlgorithmRelatedControls();
            }
        }

        private bool ShouldAutoLoadVmSolution()
        {
            return string.Equals(AlgorithmEngineSettingsManager.PreferredEngineId, AlgorithmEngineIds.Vm, StringComparison.OrdinalIgnoreCase);
        }

        private void TryAutoLoadVmSolution()
        {
            if (!ShouldAutoLoadVmSolution())
            {
                LogMessage("当前算法引擎非VM，跳过VM解决方案加载", LogLevel.Info);
                return;
            }

            lock (_eventBindingLock)
            {
                if (!_isVmSolutionLoaded)
                {
                    // 同步加载VM解决方案
                    load_vm_solution_async();
                    _isVmSolutionLoaded = true;
                    LogMessage("VM解决方案首次加载启动", LogLevel.Info);
                }
                else
                {
                    LogMessage("VM解决方案已存在，跳过重复加载", LogLevel.Info);
                }
            }
        }

        private void InsertAlgorithmEngineSelector(StepConfiguration config)
        {
            if (config.StepType != StepType.TemplateName)
            {
                return;
            }

            // 项目级引擎：不允许模板内选择
            currentTemplate.AlgorithmEngineId = AlgorithmEngineSettingsManager.PreferredEngineId;
            return;

            AlgorithmEngineRegistry.EnsureInitialized(PageManager.Page1Instance);

            var panel = new StackPanel
            {
                Margin = new Thickness(10, 5, 10, 10)
            };

            var title = new TextBlock
            {
                Text = "算法引擎",
                FontSize = 14,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var comboBox = new ComboBox
            {
                Height = 30,
                FontSize = 13,
                DisplayMemberPath = "EngineName",
                SelectedValuePath = "EngineId",
                ItemsSource = AlgorithmEngineRegistry.GetDescriptors()
            };

            comboBox.SelectionChanged += AlgorithmEngineComboBox_SelectionChanged;
            comboBox.SelectedValue = string.IsNullOrWhiteSpace(currentTemplate?.AlgorithmEngineId)
                ? AlgorithmEngineSettingsManager.PreferredEngineId
                : currentTemplate.AlgorithmEngineId;

            _algorithmEngineComboBox = comboBox;

            var hintText = new TextBlock
            {
                FontSize = 12,
                Foreground = Brushes.LightGray,
                Margin = new Thickness(2, 6, 2, 0),
                TextWrapping = TextWrapping.Wrap
            };

            _algorithmEngineHintText = hintText;
            UpdateAlgorithmEngineHint(comboBox.SelectedItem as AlgorithmEngineDescriptor);

            panel.Children.Add(title);
            panel.Children.Add(comboBox);
            panel.Children.Add(hintText);

            InputParametersPanel.Children.Add(panel);
        }

        private void AlgorithmEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_algorithmEngineComboBox?.SelectedItem is AlgorithmEngineDescriptor descriptor)
            {
                return;
            }
        }

        private void UpdateAlgorithmEngineHint(AlgorithmEngineDescriptor descriptor)
        {
            if (_algorithmEngineHintText == null)
            {
                return;
            }

            if (descriptor == null)
            {
                _algorithmEngineHintText.Text = string.Empty;
                return;
            }

            if (descriptor.IsAvailable)
            {
                _algorithmEngineHintText.Foreground = Brushes.LightGray;
                _algorithmEngineHintText.Text = $"模板设置：{descriptor.Description ?? string.Empty}";
            }
            else
            {
                _algorithmEngineHintText.Foreground = Brushes.Goldenrod;
                _algorithmEngineHintText.Text = $"模板设置：{descriptor.Description}（未启用，运行时自动回退VM）";
            }
        }

        /// <summary>
        /// 更新步骤按钮状态
        /// </summary>
        /// <param name="stepIndex">当前步骤索引</param>
        private void UpdateStepButtons(int stepIndex)
        {
            try
            {
                // 使用新的分组系统来更新按钮状态
                // 这将智能地处理普通按钮和组按钮
                GenerateStepButtons();
                
                // 更新上一步索引
                laststep = stepIndex;
                
                LogManager.Info($"步骤按钮已更新到步骤 {stepIndex}", "UI更新");
            }
            catch (Exception ex)
            {
                LogMessage($"更新按钮状态时出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 设置按钮完成样式（已完成的步骤 - 平面化设计）
        /// </summary>
        /// <param name="button">按钮控件</param>
        private void SetButtonCompletedStyle(Button button)
        {
            // 使用平面化的纯色背景（现代绿色）
            button.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // 现代绿色
            button.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 167, 69));
            button.Foreground = new SolidColorBrush(Colors.White);

            // 平面化设计，无阴影效果
            button.Effect = null;
        }

        /// <summary>
        /// 更新箭头连接器的状态
        /// </summary>
        /// <param name="stepContainer">步骤容器</param>
        /// <param name="buttonIndex">按钮索引</param>
        /// <param name="currentStepIndex">当前步骤索引</param>
        private void UpdateArrowConnector(StackPanel stepContainer, int buttonIndex, int currentStepIndex)
        {
            // 查找箭头连接器（第二个子元素）
            if (stepContainer.Children.Count > 1 && stepContainer.Children[1] is System.Windows.Shapes.Path arrow)
            {
                if (buttonIndex < currentStepIndex)
                {
                    // 已完成步骤之间的箭头 - 绿色
                    arrow.Fill = new SolidColorBrush(Color.FromRgb(40, 167, 69));
                    arrow.Stroke = new SolidColorBrush(Color.FromRgb(40, 167, 69));
                }
                else if (buttonIndex == currentStepIndex)
                {
                    // 当前步骤的箭头 - 蓝色
                    arrow.Fill = new SolidColorBrush(Color.FromRgb(0, 123, 255));
                    arrow.Stroke = new SolidColorBrush(Color.FromRgb(0, 123, 255));
                }
                else
                {
                    // 未完成步骤的箭头 - 灰色
                    arrow.Fill = new SolidColorBrush(Color.FromRgb(108, 117, 125));
                    arrow.Stroke = new SolidColorBrush(Color.FromRgb(108, 117, 125));
                }
            }
        }

        /// <summary>
        /// 设置按钮激活样式（当前步骤 - 平面化设计）
        /// </summary>
        /// <param name="button">按钮控件</param>
        private void SetButtonActiveStyle(Button button)
        {
            // 使用平面化的纯色背景（现代蓝色）
            button.Background = new SolidColorBrush(Color.FromRgb(0, 123, 255)); // 现代蓝色
            button.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 123, 255));
            button.Foreground = new SolidColorBrush(Colors.White);

            // 平面化设计，无阴影效果
            button.Effect = null;
        }

        /// <summary>
        /// 更新按钮中步骤编号的颜色
        /// </summary>
        /// <param name="button">按钮控件</param>
        /// <param name="numberColor">编号颜色</param>
        private void UpdateButtonNumberColor(Button button, Color numberColor)
        {
            if (button.Content is StackPanel content)
            {
                // 查找步骤编号的Border和TextBlock
                foreach (var child in content.Children)
                {
                    if (child is Border border && border.Child is TextBlock numberText)
                    {
                        numberText.Foreground = new SolidColorBrush(numberColor);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 获取按钮的显示名称（用于日志）
        /// </summary>
        /// <param name="button">按钮控件</param>
        private string GetButtonDisplayName(Button button)
        {
            if (button.Content is StackPanel content)
            {
                // 查找步骤名称的TextBlock
                foreach (var child in content.Children)
                {
                    if (child is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text) && !char.IsDigit(textBlock.Text[0]))
                    {
                        return textBlock.Text;
                    }
                }
            }
            return "未知步骤";
        }

        /// <summary>
        /// 动态构建参数UI
        /// </summary>
        /// <param name="config">步骤配置</param>
        private void BuildParametersUI(StepConfiguration config)
        {
            // 构建分组的输入参数
            BuildGroupedInputParameters(config.InputParameters);

            // 仅在模板配置界面：3D配置步骤把“创建3D模板”写死插入到“重新编译”下一行
            if (config.StepType == StepType.ThreeDConfiguration)
            {
                InsertCreate3DTemplateButtonUnderRecompile();
            }

            // 构建输出参数
            foreach (var param in config.OutputParameters)
            {
                AddOutputParameter(param.Name, param.DefaultValue);
            }
        }

        /// <summary>
        /// 构建分组的输入参数UI
        /// </summary>
        /// <param name="parameters">参数列表</param>
        private void BuildGroupedInputParameters(List<ParameterConfig> parameters)
        {
            // 根据Group属性对参数进行分组
            var groupedParams = parameters.GroupBy(p => string.IsNullOrEmpty(p.Group) ? "其他参数" : p.Group);

            bool isFirstGroup = true;
            foreach (var group in groupedParams)
            {
                string groupName = group.Key;
                var groupParams = group.ToList();

                if (groupParams.Count == 1 && groupName == "其他参数")
                {
                    // 如果只有一个未分组的参数，直接添加
                    AddInputParameter(groupParams[0], GetParameterValue(currentStep, groupParams[0].Name, groupParams[0].DefaultValue));
                }
                else
                {
                    // 创建分组UI，只展开第一个分组
                    AddParameterGroup(groupName, groupParams, isFirstGroup);
                    isFirstGroup = false;
                }
            }
        }

        private void InsertCreate3DTemplateButtonUnderRecompile()
        {
            const string markerTag = "Create3DTemplateRow";

            try
            {
                // 优先：在“其他参数”分组里查找“重新编译”行并插入按钮行
                foreach (var element in InputParametersPanel.Children)
                {
                    var expander = element as Expander;
                    if (expander == null)
                    {
                        continue;
                    }

                    var container = expander.Content as StackPanel;
                    if (container == null)
                    {
                        continue;
                    }

                    var headerText = (expander.Header as TextBlock)?.Text ?? expander.Header?.ToString();
                    if (!string.Equals(headerText, "其他参数", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    InsertCreate3DTemplateButtonRowIntoContainer(container, markerTag);
                    return;
                }

                // 兜底：未分组时，直接在根面板里查找
                InsertCreate3DTemplateButtonRowIntoContainer(InputParametersPanel, markerTag);
            }
            catch (Exception ex)
            {
                LogMessage($"插入创建3D模板按键失败: {ex.Message}", LogLevel.Warning);
            }
        }

        private void InsertCreate3DTemplateButtonRowIntoContainer(Panel container, string markerTag)
        {
            // 已经插入过就不重复插
            foreach (var element in container.Children)
            {
                if (element is FrameworkElement fe &&
                    fe.Tag is string tag &&
                    string.Equals(tag, markerTag, StringComparison.Ordinal))
                {
                    return;
                }
            }

            for (int i = 0; i < container.Children.Count; i++)
            {
                var row = container.Children[i] as StackPanel;
                if (row == null || row.Children.Count == 0)
                {
                    continue;
                }

                var label = row.Children[0] as TextBlock;
                if (label == null)
                {
                    continue;
                }

                if (!string.Equals(label.Text, "重新编译", StringComparison.Ordinal))
                {
                    continue;
                }

                var buttonRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(5),
                    Tag = markerTag
                };

                buttonRow.Children.Add(new TextBlock
                {
                    Text = "",
                    Width = 250,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var create3DTemplateButton = new Button
                {
                    Content = "创建3D模板",
                    Height = 24,
                    Padding = new Thickness(10, 2, 10, 2),
                    Background = System.Windows.Media.Brushes.Purple,
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                create3DTemplateButton.Click += Create3DTemplate_Click;

                buttonRow.Children.Add(create3DTemplateButton);
                container.Children.Insert(i + 1, buttonRow);
                return;
            }
        }

        /// <summary>
        /// 添加参数分组UI
        /// </summary>
        /// <param name="groupName">分组名称</param>
        /// <param name="parameters">分组内的参数列表</param>
        /// <param name="isExpanded">是否展开分组</param>
        private void AddParameterGroup(string groupName, List<ParameterConfig> parameters, bool isExpanded = true)
        {
            // 创建分组容器
            var groupExpander = new Expander
            {
                Header = groupName,
                IsExpanded = isExpanded,
                Margin = new Thickness(0, 5, 0, 5),
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(248, 248, 248))
            };

            // 设置Header样式
            var headerTextBlock = new TextBlock
            {
                Text = groupName,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
                Margin = new Thickness(5, 2, 5, 2)
            };
            groupExpander.Header = headerTextBlock;

            // 创建参数容器
            var parameterContainer = new StackPanel { Margin = new Thickness(10, 5, 5, 5) };

            // 添加分组内的参数
            foreach (var param in parameters)
            {
                AddGroupedInputParameter(param, GetParameterValue(currentStep, param.Name, param.DefaultValue), parameterContainer);
            }

            groupExpander.Content = parameterContainer;
            InputParametersPanel.Children.Add(groupExpander);
        }

        /// <summary>
        /// 向分组容器中添加参数控件
        /// </summary>
        /// <param name="param">参数配置</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="container">容器控件</param>
        private void AddGroupedInputParameter(ParameterConfig param, string defaultValue, StackPanel container)
        {
            string name = param.Name;
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            panel.Children.Add(new TextBlock { Text = name, Width = 250, VerticalAlignment = VerticalAlignment.Center });

            // 根据参数类型创建对应的控件
            if (name == "启用3D检测")
            {
                // 3D检测复选框的特殊处理逻辑保持不变
                bool currentSystemState = ThreeDSettings.CurrentDetection3DParams?.Enable3DDetection ?? false;
                bool templateValue = bool.TryParse(defaultValue, out bool templateResult) && templateResult;
                bool initialState = currentSystemState || templateValue;
                
                CheckBox checkBox = new CheckBox 
                { 
                    IsChecked = initialState,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    Width = 25,
                    Height = 25,
                    Content = "", // 删除Content文字，因为已经有标签了
                    FontSize = 14
                };
                
                TextBox hiddenTextBox = new TextBox { Visibility = Visibility.Collapsed };
                hiddenTextBox.Text = checkBox.IsChecked.ToString();
                
                // 3D检测相关的事件处理逻辑保持不变...
                if (checkBox.IsChecked == true)
                {
                    ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = true;
                    PageManager.Page1Instance?.DetectionManager?.StartDetectionCycle(true);
                    LogMessage($"初始化时检测到3D已启用，已同步状态到UnifiedDetectionManager", LogLevel.Info);
                }
                
                // 复选框事件处理逻辑保持不变...
                checkBox.Checked += (s, e) => 
                {
                    hiddenTextBox.Text = "true";
                    ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = true;
                    PageManager.Page1Instance?.DetectionManager?.StartDetectionCycle(true);
                    
                    SaveStepParameters();
                    LogMessage("已保存当前步骤参数（包括重新编译设置）", LogLevel.Info);
                    
                    try
                    {
                        LogMessage("用户启用了3D检测，正在启动3D检测系统...", LogLevel.Info);
                        Task.Run(() =>
                        {
                            try
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage("3D检测已启用（主进程已记录配置）。3D功能将通过独立进程(Host)按需启动。", LogLevel.Info);
                                });
                            }
                            catch (Exception ex)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"3D检测系统启动异常: {ex.Message}", LogLevel.Error);
                                    MessageBox.Show($"3D检测启动时发生异常：{ex.Message}\n\n请稍后手动点击【3D检测系统】按钮重试。", 
                                        "3D启动异常", MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"启动3D检测系统失败: {ex.Message}", LogLevel.Error);
                    }
                };
                
                checkBox.Unchecked += (s, e) => 
                {
                    hiddenTextBox.Text = "false";
                    ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = false;
                    PageManager.Page1Instance?.DetectionManager?.StartDetectionCycle(false);
                    
                    try
                    {
                        LogMessage("用户关闭了3D检测，正在停止3D检测系统...", LogLevel.Info);
                        Task.Run(() =>
                        {
                            try
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage("3D检测系统已停止", LogLevel.Info);
                                    MessageBox.Show("3D检测已关闭！\n\n3D检测系统已停止，现在只进行2D检测。", 
                                        "3D已关闭", MessageBoxButton.OK, MessageBoxImage.Information);
                                });
                            }
                            catch (Exception ex)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"停止3D检测系统失败: {ex.Message}", LogLevel.Warning);
                                });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"停止3D检测系统失败: {ex.Message}", LogLevel.Warning);
                    }
                };
                
                panel.Children.Add(checkBox);
                panel.Children.Add(hiddenTextBox);
                inputParameterControls[currentStep][name] = hiddenTextBox;
            }
            else if (param.Type == ParamType.Boolean)
            {
                // 兼容历史模板使用 "1"/"0" 存储布尔值
                bool initialValue = defaultValue == "1" || (bool.TryParse(defaultValue, out bool result) && result);
                
                CheckBox checkBox = new CheckBox 
                { 
                    IsChecked = initialValue,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    Width = 20,
                    Height = 20,
                    Content = "",
                    FontSize = 12
                };
                
                TextBox hiddenTextBox = new TextBox { Visibility = Visibility.Collapsed };
                hiddenTextBox.Text = checkBox.IsChecked.ToString();

                checkBox.Checked += (s, e) => {
                    hiddenTextBox.Text = "true";
                    // 自动寻BLK的特殊联动处理
                    if (name == "自动寻BLK")
                    {
                        UpdateRelatedControlsState(true);
                    }
                    // AI胶点的特殊联动处理
                    else if (name == "AI胶点")
                    {
                        UpdateGluePointRelatedControlsState(true);
                    }
                    // 破损算法的特殊联动处理
                    //else if (name == "传统破损算法" || name == "AI破损算法")
                    //{
                    //    UpdateDamageAlgorithmRelatedControlsState();
                    //}
                };
                checkBox.Unchecked += (s, e) => {
                    hiddenTextBox.Text = "false";
                    // 自动寻BLK的特殊联动处理
                    if (name == "自动寻BLK")
                    {
                        UpdateRelatedControlsState(false);
                    }
                    // AI胶点的特殊联动处理
                    else if (name == "AI胶点")
                    {
                        UpdateGluePointRelatedControlsState(false);
                    }
                    // 破损算法的特殊联动处理
                    //else if (name == "传统破损算法" || name == "AI破损算法")
                    //{
                    //    UpdateDamageAlgorithmRelatedControlsState();
                    //}
                };
                
                panel.Children.Add(checkBox);
                panel.Children.Add(hiddenTextBox);
                inputParameterControls[currentStep][name] = hiddenTextBox;
            }
            else if (param.Type == ParamType.FilePath)
            {
                // 文件选择
                StackPanel filePanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                TextBox fileTextBox = new TextBox 
                { 
                    Text = defaultValue, 
                    Width = 160, 
                    Margin = new Thickness(0, 0, 5, 0),
                    IsReadOnly = false,
                    Background = System.Windows.Media.Brushes.LightYellow,
                    BorderBrush = System.Windows.Media.Brushes.Orange,
                    BorderThickness = new Thickness(1),
                    ToolTip = "点击选择文件"
                };
                
                Button browseButton = new Button 
                { 
                    Content = "浏览...", 
                    Width = 50, 
                    Height = 22,
                    Background = System.Windows.Media.Brushes.Orange,
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(2, 0, 0, 0),
                    ToolTip = "点击选择文件"
                };
                
                browseButton.Click += (s, e) =>
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog();
                    dialog.Title = $"选择 {name}";
                    
                    // 根据参数名称设置文件过滤器
                    if (name.Contains("模型") && name.Contains("文件"))
                    {
                        dialog.Filter = "模型文件 (*.bin)|*.bin|所有文件 (*.*)|*.*";
                    }
                    else if (name.Contains("模板"))
                    {
                        dialog.Filter = "模板文件 (*.hpmxml)|*.hpmxml|所有文件 (*.*)|*.*";
                    }
                    else if (name.Contains("图片") || name.Contains("图像"))
                    {
                        dialog.Filter = "图像文件 (*.bmp)|*.bmp|所有文件 (*.*)|*.*";
                    }
                    else
                    {
                        dialog.Filter = "所有文件 (*.*)|*.*";
                    }
                    
                    if (dialog.ShowDialog() == true)
                    {
                        fileTextBox.Text = dialog.FileName;
                    }
                };
                
                filePanel.Children.Add(fileTextBox);
                filePanel.Children.Add(browseButton);
                panel.Children.Add(filePanel);
                
                inputParameterControls[currentStep][name] = fileTextBox;
            }
            else if (param.Type == ParamType.FolderPath)
            {
                // 文件夹选择
                StackPanel folderPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                TextBox folderTextBox = new TextBox 
                { 
                    Text = defaultValue, 
                    Width = 160, 
                    Margin = new Thickness(0, 0, 5, 0),
                    IsReadOnly = false,
                    Background = System.Windows.Media.Brushes.LightYellow,
                    BorderBrush = System.Windows.Media.Brushes.Orange,
                    BorderThickness = new Thickness(1),
                    ToolTip = "点击选择文件夹"
                };
                
                Button browseButton = new Button 
                { 
                    Content = "浏览...", 
                    Width = 50, 
                    Height = 22,
                    Background = System.Windows.Media.Brushes.Orange,
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(2, 0, 0, 0),
                    ToolTip = "点击选择文件夹"
                };
                
                browseButton.Click += (s, e) =>
                {
                    if (name == "项目文件夹")
                    {
                        if (TrySelectLJDeveloperUserProjectSourcePath(out string projectSourcePath))
                        {
                            folderTextBox.Text = projectSourcePath;
                        }
                        return;
                    }

                    var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                    folderDialog.Description = $"选择 {name}";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var selectedPath = folderDialog.SelectedPath;

                        // 验证项目文件夹必须是source文件夹
                        if (name == "项目文件夹")
                        {
                            if (!selectedPath.EndsWith("\\source", StringComparison.OrdinalIgnoreCase) &&
                                !selectedPath.EndsWith("/source", StringComparison.OrdinalIgnoreCase))
                            {
                                MessageBox.Show(
                                    "❌ 错误：选择的文件夹必须是source文件夹！\n\n" +
                                    "正确路径格式：C:\\Users\\Public\\Documents\\KEYENCE\\LJ Developer\\User\\[项目名]\\source",
                                    "路径验证失败",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                return;
                            }
                        }

                        folderTextBox.Text = selectedPath;
                    }
                };
                
                folderPanel.Children.Add(folderTextBox);
                folderPanel.Children.Add(browseButton);
                panel.Children.Add(folderPanel);
                
                inputParameterControls[currentStep][name] = folderTextBox;
            }
            else if (param.Type == ParamType.Text)
            {
                TextBox textBox = new TextBox 
                { 
                    Text = defaultValue, 
                    Width = 200, 
                    Margin = new Thickness(10, 0, 0, 0),
                    IsReadOnly = false,
                    Background = System.Windows.Media.Brushes.LightGreen,
                    BorderBrush = System.Windows.Media.Brushes.Green,
                    BorderThickness = new Thickness(1)
                };
                
                panel.Children.Add(textBox);
                inputParameterControls[currentStep][name] = textBox;
            }
            else
            {
                TextBox textBox = new TextBox 
                { 
                    Text = defaultValue, 
                    Width = 200, 
                    Margin = new Thickness(10, 0, 0, 0) 
                };
                
                WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetEnableSmartInput(textBox, true);
                WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetParameterName(textBox, name);
                SetupSmartInputConfig(textBox, name);
                
                panel.Children.Add(textBox);
                inputParameterControls[currentStep][name] = textBox;
            }
            
            container.Children.Add(panel);
        }

        /// <summary>
        /// 动态构建操作按钮UI
        /// </summary>
        /// <param name="config">步骤配置</param>
        private void BuildActionsUI(StepConfiguration config)
        {
            foreach (var action in config.Actions)
            {
                // 为3D配置步骤绑定特定的事件处理器
                if (config.StepType == StepType.ThreeDConfiguration)
                {
                    RoutedEventHandler handler = null;
                    switch (action.Name)
                    {
                        case "设置工具参数":
                            handler = SetToolParameter3D_Click;
                            break;
                        case "设定判定对象":
                            handler = SetJudgement3D_Click;
                            break;
                        case "设定输出对象":
                            handler = SetDataExport3D_Click;
                            break;
                        default:
                            handler = action.Handler;
                            break;
                    }
                    AddConfigurableButton(action.Name, handler, action.BackgroundColor, action.ForegroundColor);
                }
                else
                {
                    AddConfigurableButton(action.Name, action.Handler, action.BackgroundColor, action.ForegroundColor);
                }
            }
        }

        /// <summary>
        /// 动态构建标签UI
        /// </summary>
        /// <param name="config">步骤配置</param>
        private void BuildLabelsUI(StepConfiguration config)
        {
            foreach (var label in config.Labels)
            {
                AddLabbel(label);
            }
        }

        /// <summary>
        /// 添加可配置的按钮
        /// </summary>
        /// <param name="text">按钮文本</param>
        /// <param name="clickHandler">点击事件处理器</param>
        /// <param name="backgroundColor">背景颜色</param>
        /// <param name="foregroundColor">前景颜色</param>
        private void AddConfigurableButton(string text, RoutedEventHandler clickHandler, 
            Brush backgroundColor = null, Brush foregroundColor = null)
        {
            Button button = new Button
            {
                Content = text,
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Background = backgroundColor ?? new SolidColorBrush(Colors.Blue),
                Foreground = foregroundColor ?? new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            if (clickHandler != null)
                button.Click += clickHandler;

            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            panel.Children.Add(button);
            InputParametersPanel.Children.Add(panel);
        }

        private string GetParameterValue(int stepIndex, string paramName, string defaultValue)
        {
            // 将步骤索引转换为StepType
            if (stepIndex >= 0 && stepIndex < stepConfigurations.Count)
            {
                StepType stepType = stepConfigurations[stepIndex].StepType;
                return GetParameterValueByStepType(stepType, paramName, defaultValue);
            }
            return defaultValue;
        }

        /// <summary>
        /// 根据StepType获取参数值（包含兼容性处理）
        /// 兼容性功能：
        /// 1. 镀膜参数迁移：镀膜G1端ROI宽和镀膜G2端ROI宽从镀膜X向ROI宽迁移，默认值50
        /// 2. 晶片增益参数迁移：从已删除的晶片增亮步骤迁移到晶片位置与尺寸步骤
        /// </summary>
        private string GetParameterValueByStepType(StepType stepType, string paramName, string defaultValue)
        {
            // 优先从当前模板参数中获取值
            if (currentTemplate.InputParameters.ContainsKey(stepType) &&
                currentTemplate.InputParameters[stepType].ContainsKey(paramName))
            {
                return currentTemplate.InputParameters[stepType][paramName];
            }

            // 兼容性处理：镀膜参数迁移
            if (stepType == StepType.CoatingGeometrySize)
            {
                if (paramName == "镀膜G1端ROI宽" || paramName == "镀膜G2端ROI宽")
                {
                    // 1. 首先检查模板中是否有镀膜X向ROI宽的值
                    if (currentTemplate.InputParameters.ContainsKey(StepType.CoatingGeometrySize) &&
                        currentTemplate.InputParameters[StepType.CoatingGeometrySize].ContainsKey("镀膜X向ROI宽"))
                    {
                        string legacyValue = currentTemplate.InputParameters[StepType.CoatingGeometrySize]["镀膜X向ROI宽"];
                        LogManager.Info($"模板兼容性处理: {paramName} 使用镀膜X向ROI宽的值: {legacyValue}", "模板配置");
                        return legacyValue;
                    }
                    // 2. 如果连镀膜X向ROI宽都没有，使用默认值50
                    else
                    {
                        LogManager.Info($"模板兼容性处理: {paramName} 使用默认值: 50", "模板配置");
                        return "50";
                    }
                }
            }

            // 兼容性处理：晶片增益参数迁移
            if (stepType == StepType.ChipPositionSize && paramName == "晶片增益")
            {
                // 检查所有步骤中是否有晶片增益参数（因为晶片增亮步骤可能已被删除）
                foreach (var stepParams in currentTemplate.InputParameters.Values)
                {
                    if (stepParams.ContainsKey("晶片增益"))
                    {
                        string legacyValue = stepParams["晶片增益"];
                        LogManager.Info($"模板兼容性处理: 晶片增益 从其他步骤迁移值: {legacyValue}", "模板配置");
                        return legacyValue;
                    }
                }
            }

            // 兼容性处理：G1/G2补偿从历史合并参数迁移
            if (stepType == StepType.ThreeDConfiguration &&
                (paramName == "G1补偿" || paramName == "G2补偿"))
            {
                if (currentTemplate.InputParameters.ContainsKey(stepType) &&
                    currentTemplate.InputParameters[stepType].ContainsKey("G1G2补偿"))
                {
                    string legacyValue = currentTemplate.InputParameters[stepType]["G1G2补偿"];
                    LogManager.Info($"模板兼容性处理: {paramName} 使用历史G1G2补偿值: {legacyValue}", "模板配置");
                    return legacyValue;
                }
            }

            // 如果没有找到任何值，返回默认值
            return defaultValue;
        }

        /// <summary>
        /// 获取3D配置参数值
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>参数值</returns>
        public string Get3DConfigParameter(string parameterName, string defaultValue = "")
        {
            return GetParameterValueByStepType(StepType.ThreeDConfiguration, parameterName, defaultValue);
        }

        /// <summary>
        /// 根据StepType获取显示名称
        /// </summary>
        private string GetStepDisplayNameByType(StepType stepType)
        {
            // 在当前步骤配置中查找对应的显示名称
            var stepConfig = stepConfigurations.FirstOrDefault(s => s.StepType == stepType);
            return stepConfig?.DisplayName ?? stepType.ToString();
        }


        // 优化的参数保存方法，支持可选的步骤索引
        /// <summary>
        /// 保存所有步骤的参数（用于模板保存）
        /// </summary>
        private void SaveAllStepParameters()
        {
            try
            {
                // 遍历所有有输入控件的步骤
                foreach (var stepIndex in inputParameterControls.Keys.ToList())
                {
                    SaveStepParameters(stepIndex);
                }
                
                // 清理重复参数（防止同一参数在多个步骤中重复定义）
                CleanupDuplicateParameters();
                
                LogManager.Info($"已保存所有 {inputParameterControls.Keys.Count} 个步骤的参数", "参数保存");
            }
            catch (Exception ex)
            {
                LogMessage($"保存所有步骤参数时出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 清理无效参数：确保每个参数只在正确的步骤中存在，移除所有无效参数
        /// </summary>
        private void CleanupDuplicateParameters()
        {
            try
            {
                // 参数应该所属的正确步骤映射（根据当前代码配置）
                var parameterStepMap = new Dictionary<string, StepType>();
                for (int i = 0; i < stepConfigurations.Count; i++)
                {
                    var stepConfig = stepConfigurations[i];
                    foreach (var param in stepConfig.InputParameters)
                    {
                        parameterStepMap[param.Name] = stepConfig.StepType;
                    }
                }

                var cleanupLog = new StringBuilder();
                int removedCount = 0;

                // 检查并清理所有无效参数
                foreach (var stepEntry in currentTemplate.InputParameters.ToList())
                {
                    StepType stepType = stepEntry.Key;
                    var parameters = stepEntry.Value.ToList();
                    
                    foreach (var paramPair in parameters)
                    {
                        string paramName = paramPair.Key;
                        bool shouldRemove = false;
                        string reason = "";
                        
                        if (!parameterStepMap.TryGetValue(paramName, out StepType correctStepType))
                        {
                            // 参数在当前配置中根本不存在
                            shouldRemove = true;
                            reason = "参数已废弃，不在当前配置中";
                        }
                        else if (correctStepType != stepType)
                        {
                            // 参数位置错误
                            shouldRemove = true;
                            reason = $"参数位置错误，应在步骤{correctStepType}";
                        }
                        
                        if (shouldRemove)
                        {
                            currentTemplate.InputParameters[stepType].Remove(paramName);
                            cleanupLog.AppendLine($"移除无效参数: '{paramName}' 从步骤{stepType} ({reason})");
                            removedCount++;
                        }
                    }
                    
                    // 如果步骤的参数字典为空，移除整个步骤
                    if (currentTemplate.InputParameters[stepType].Count == 0)
                    {
                        currentTemplate.InputParameters.Remove(stepType);
                        cleanupLog.AppendLine($"移除空步骤: {stepType}");
                    }
                }

                if (removedCount > 0)
                {
                    LogManager.Info($"已清理 {removedCount} 个无效参数", "参数清理");
                    LogMessage($"参数清理详情:\n{cleanupLog}", LogLevel.Info);
                }
                else
                {
                    LogManager.Info("参数配置检查完成，无需清理", "参数清理");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"清理无效参数时出错: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// 保存指定步骤的参数
        /// </summary>
        private void SaveStepParameters(int? stepIndex = null)
        {
            // 使用提供的索引或默认使用laststep
            int indexToSave = stepIndex ?? laststep;

            // 验证索引范围（使用新的验证方法）
            if (!IsValidStepIndex(indexToSave))
            {
                LogMessage($"保存参数时跳过无效步骤索引: {indexToSave}", LogLevel.Warning);
                return;
            }

            // 获取步骤配置
            var stepConfig = stepConfigurations[indexToSave];
            StepType stepType = stepConfig.StepType;

            // 如果指定步骤有输入控件，保存它们的值
            if (inputParameterControls.ContainsKey(indexToSave))
            {
                // 确保步骤字典已初始化（现在使用StepType作为key）
                if (!currentTemplate.InputParameters.ContainsKey(stepType))
                    currentTemplate.InputParameters[stepType] = new Dictionary<string, string>();

                foreach (var pair in inputParameterControls[indexToSave])
                {
                    string paramName = pair.Key;
                    TextBox textBox = pair.Value;
                    string value = textBox.Text;

                    // 特殊处理模板命名步骤（使用StepType而不是硬编码索引）
                    if (stepConfig.StepType == StepType.TemplateName)
                    {
                        if (paramName == "模板名称")
                            currentTemplate.TemplateName = value;
                        else if (paramName == "备注")
                            currentTemplate.Remark = value;
                        else if (paramName == "启用3D检测")
                        {
                            // 保存3D检测启用状态到模板
                            if (bool.TryParse(value, out bool enable3D))
                            {
                                if (currentTemplate.Detection3DParams == null)
                                    currentTemplate.Detection3DParams = new Detection3DParameters();
                                
                                currentTemplate.Detection3DParams.Enable3DDetection = enable3D;
                                
                                // 同时更新内存中的3D参数
                                ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = enable3D;
                            }
                        }
                    }
                    
                    // 🔧 新增：特殊处理3D配置步骤
                    else if (stepConfig.StepType == StepType.ThreeDConfiguration)
                    {
                        if (paramName == "启用3D检测")
                        {
                            // 保存3D检测启用状态到模板和内存
                            if (bool.TryParse(value, out bool enable3D))
                            {
                                if (currentTemplate.Detection3DParams == null)
                                    currentTemplate.Detection3DParams = new Detection3DParameters();
                                
                                currentTemplate.Detection3DParams.Enable3DDetection = enable3D;
                                ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = enable3D;
                            }
                        }
                        else if (paramName == "项目文件夹")
                        {
                            // 🔧 新增：保存项目文件夹到3D参数
                            if (currentTemplate.Detection3DParams == null)
                                currentTemplate.Detection3DParams = new Detection3DParameters();
                            
                            currentTemplate.Detection3DParams.ProjectFolder = value;
                            ThreeDSettings.CurrentDetection3DParams.ProjectFolder = value;
                        }
                        else if (paramName == "重新编译")
                        {
                            // 保存重新编译标识到3D参数
                            if (bool.TryParse(value, out bool reCompile))
                            {
                                if (currentTemplate.Detection3DParams == null)
                                    currentTemplate.Detection3DParams = new Detection3DParameters();
                                
                                currentTemplate.Detection3DParams.ReCompile = reCompile;
                                ThreeDSettings.CurrentDetection3DParams.ReCompile = reCompile;
                            }
                        }
                    }

                    // 保存参数值（现在使用StepType作为key）
                    currentTemplate.InputParameters[stepType][paramName] = value;
                }
            }
        }


        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            // 先保存所有步骤的参数（修复：之前只保存当前步骤）
            SaveAllStepParameters();

            // 清理无效的历史数据
            CleanupTemplateData();

            // 验证模板名称
            if (string.IsNullOrWhiteSpace(currentTemplate.TemplateName))
            {
                MessageBox.Show("请输入模板名称", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 设置样品类型和涂布类型
            currentTemplate.SampleType = currentSampleType;
            currentTemplate.CoatingType = currentCoatingType;
            currentTemplate.AlgorithmEngineId = AlgorithmEngineSettingsManager.PreferredEngineId;
            
            // 记录保存的涂布类型信息
            string coatingTypeDisplay = currentCoatingType == CoatingType.Single ? "单涂布" : "双涂布";
            PageManager.Page1Instance?.LogUpdate($"保存模板时设置涂布类型: {coatingTypeDisplay}");

            // 设置时间戳
            currentTemplate.LastModifiedTime = DateTime.Now;

            // 同步相机参数到模板（从相机配置页读取当前UI值，确保模板落盘时携带最新相机参数）
            try
            {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                if (mainWindow?.frame_CameraConfigPage?.Content is CameraConfigPage cameraConfigPage)
                {
                    var settings = cameraConfigPage.GetCurrentCameraSettingsWithCoaxial();
                    var cameraParams = currentTemplate.CameraParams ?? new CameraParameters();

                    cameraParams.FlyingExposureTime = settings.FlyingExposure;
                    cameraParams.FlyingDelayTime = settings.FlyingDelay;
                    cameraParams.Fixed1ExposureIntensity = 255;
                    cameraParams.Fixed1ExposureTime = settings.Fixed1Time;
                    cameraParams.Fixed2ExposureIntensity = 255;
                    cameraParams.Fixed2ExposureTime = settings.Fixed2Time;
                    cameraParams.Fixed1CoaxialTime = settings.Fixed1Coaxial;
                    cameraParams.Fixed2CoaxialTime = settings.Fixed2Coaxial;
                    cameraParams.Enable45DegreeLight = settings.Enable45Degree;
                    cameraParams.Enable0DegreeLight = settings.Enable0Degree;
                    cameraParams.LidImageSelection = settings.LidImageSelection;
                    cameraParams.CoatingImageSelection = settings.CoatingImageSelection;

                    currentTemplate.CameraParams = cameraParams;
                    PageManager.Page1Instance?.LogUpdate("已将当前相机参数写入模板（即将保存）");
                }
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"同步相机参数到模板失败: {ex.Message}");
            }

            // 将内存中的3D检测参数应用到模板
            try
            {
                ThreeDSettings.ApplyToTemplate(currentTemplate);
                PageManager.Page1Instance?.LogUpdate("已将内存中的3D检测参数保存到模板");
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"保存3D检测参数失败: {ex.Message}");
            }

            // 构建文件名和路径
            string safeFileName = string.Join("_", currentTemplate.TemplateName.Split(System.IO.Path.GetInvalidFileNameChars()));
            string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", $"{safeFileName}.json");
            CurrentTemplateFilePath = filePath;

            try
            {
                // 保存模板到文件
                currentTemplate.SaveToFile(filePath);
                // 更新最后使用的模板路径
                SaveLastUsedTemplate(filePath);
                // 标记为已保存状态
                MarkAsSaved();
                
                // 🔧 新增：同时保存VM解决方案，避免软件重启时参数需要重新注入
                try
                {
                    if (VmSolution.Instance != null)
                    {
                        VmSolution.Save();
                        LogManager.Info($"已同时保存VM解决方案到磁盘", "模板保存");
                    }
                    else
                    {
                        LogManager.Warning("VM解决方案未初始化，跳过VM保存", "模板保存");
                    }
                }
                catch (Exception vmEx)
                {
                    LogManager.Warning($"保存VM解决方案失败: {vmEx.Message}", "模板保存");
                    // VM保存失败不影响模板保存的成功状态
                }
                
                MessageBox.Show($"模板 \"{currentTemplate.TemplateName}\" 已成功保存\n\n✅ 已同时保存VM解决方案到磁盘", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存模板失败: {ex.Message}", "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开3D检测窗口的事件处理方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Open3DDetection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 先保存当前步骤的参数
                SaveStepParameters();

                // 创建并显示3D检测窗口
                MessageBox.Show("3D配置工具已迁移为独立进程（Host/Tool）。\n当前版本主程序不再直接加载Keyence 3D窗口。", "3D提示", MessageBoxButton.OK, MessageBoxImage.Information);
                
                LogMessage("打开3D检测系统窗口", LogLevel.Info);
                
                // 记录到Page1的日志系统
                PageManager.Page1Instance?.LogUpdate("从模板配置打开3D检测系统");
            }
            catch (Exception ex)
            {
                LogMessage($"打开3D检测系统失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开3D检测系统失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void AddInputParameter(ParameterConfig param, string defaultValue)
        {
            string name = param.Name;
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            panel.Children.Add(new TextBlock { Text = name, Width = 250, VerticalAlignment = VerticalAlignment.Center });

            // 根据参数名判断类型，如果是"启用3D检测"则使用CheckBox
            if (name == "启用3D检测")
            {
                // 🔧 关键修复：优先使用当前系统状态，然后是模板值，最后是默认值
                bool currentSystemState = ThreeDSettings.CurrentDetection3DParams?.Enable3DDetection ?? false;
                bool templateValue = bool.TryParse(defaultValue, out bool templateResult) && templateResult;
                bool initialState = currentSystemState || templateValue; // 如果系统已启用或模板配置启用，则为true
                
                CheckBox checkBox = new CheckBox 
                { 
                    IsChecked = initialState,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    Width = 25,
                    Height = 25,
                    Content = "", // 删除Content文字，因为已经有标签了
                    FontSize = 14
                };
                
                // 创建一个隐藏的TextBox来维持现有的参数存储逻辑
                TextBox hiddenTextBox = new TextBox { Visibility = Visibility.Collapsed };
                hiddenTextBox.Text = checkBox.IsChecked.ToString();
                
                // 🔧 关键修复：初始化时同步状态到UnifiedDetectionManager
                if (checkBox.IsChecked == true)
                {
                    ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = true;
                    PageManager.Page1Instance?.DetectionManager?.StartDetectionCycle(true);
                    LogMessage($"初始化时检测到3D已启用，已同步状态到UnifiedDetectionManager", LogLevel.Info);
                }
                
                // 当CheckBox状态改变时更新隐藏TextBox的值
                checkBox.Checked += (s, e) => 
                {
                    hiddenTextBox.Text = "true";
                    // 保存3D检测启用状态到内存
                    ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = true;
                    // ✅ 已屏蔽自动change标记：MarkAsChanged(); - 现在只在用户点击"执行"按键时才记录
                    
                    // 🔧 关键修复：立即更新UnifiedDetectionManager的状态
                    PageManager.Page1Instance?.DetectionManager?.StartDetectionCycle(true);
                    
                    // 🔧 关键修复：在启动3D检测前，先保存当前步骤的参数（确保"重新编译"等参数生效）
                    SaveStepParameters();
                    LogMessage("已保存当前步骤参数（包括重新编译设置）", LogLevel.Info);
                    
                    // 🔧 关键修复：用户启用3D检测时立即尝试启动3D系统
                    try
                    {
                        LogMessage("用户启用了3D检测，正在启动3D检测系统...", LogLevel.Info);
                        
                        // 异步启动3D系统，避免阻塞UI
                        Task.Run(() =>
                        {
                            try
                            {
                                
                                // 回到UI线程显示结果
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage("3D检测已启用（主进程已记录配置）。3D功能将通过独立进程(Host)按需启动。", LogLevel.Info);
                                });
                            }
                            catch (Exception ex)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"3D检测系统启动异常: {ex.Message}", LogLevel.Error);
                                    MessageBox.Show($"3D检测启动时发生异常：{ex.Message}\n\n请稍后手动点击【3D检测系统】按钮重试。", 
                                        "3D启动异常", MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"启动3D检测系统失败: {ex.Message}", LogLevel.Error);
                    }
                };
                checkBox.Unchecked += (s, e) => 
                {
                    hiddenTextBox.Text = "false";
                    // 保存3D检测启用状态到内存
                    ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = false;
                    // ✅ 已屏蔽自动change标记：MarkAsChanged(); - 现在只在用户点击"执行"按键时才记录
                    
                    // 🔧 关键修复：立即更新UnifiedDetectionManager的状态
                    PageManager.Page1Instance?.DetectionManager?.StartDetectionCycle(false);
                    
                    // 🔧 关键修复：用户关闭3D检测时停止3D系统
                    try
                    {
                        LogMessage("用户关闭了3D检测，正在停止3D检测系统...", LogLevel.Info);
                        
                        // 异步停止3D系统
                        Task.Run(() =>
                        {
                            try
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage("3D检测系统已停止", LogLevel.Info);
                                    MessageBox.Show("3D检测已关闭！\n\n3D检测系统已停止，现在只进行2D检测。", 
                                        "3D已关闭", MessageBoxButton.OK, MessageBoxImage.Information);
                                });
                            }
                            catch (Exception ex)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"停止3D检测系统失败: {ex.Message}", LogLevel.Warning);
                                });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"停止3D检测系统失败: {ex.Message}", LogLevel.Warning);
                    }
                };
                
                panel.Children.Add(checkBox);
                panel.Children.Add(hiddenTextBox); // 添加隐藏的TextBox用于存储
                
                // 存储隐藏TextBox的引用以便稍后访问
                inputParameterControls[currentStep][name] = hiddenTextBox;
            }
            else if (param.Type == ParamType.Boolean)
            {
                // 🔧 修复：处理其他布尔类型参数（如"重新编译"），兼容历史 "1"/"0"
                bool initialValue = defaultValue == "1" || (bool.TryParse(defaultValue, out bool result) && result);
                
                CheckBox checkBox = new CheckBox 
                { 
                    IsChecked = initialValue,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    Width = 20,
                    Height = 20,
                    Content = "",
                    FontSize = 12
                };
                
                // 创建一个隐藏的TextBox来维持现有的参数存储逻辑
                TextBox hiddenTextBox = new TextBox { Visibility = Visibility.Collapsed };
                hiddenTextBox.Text = checkBox.IsChecked.ToString();
                
                // 当CheckBox状态改变时更新隐藏TextBox的值
                checkBox.Checked += (s, e) => {
                    hiddenTextBox.Text = "true";
                };
                checkBox.Unchecked += (s, e) => {
                    hiddenTextBox.Text = "false";
                };
                
                panel.Children.Add(checkBox);
                panel.Children.Add(hiddenTextBox); // 添加隐藏的TextBox用于存储
                
                // 存储隐藏TextBox的引用以便稍后访问
                inputParameterControls[currentStep][name] = hiddenTextBox;
            }
            else if (param.Type == ParamType.FolderPath)
            {
                // 🔧 新增：为文件夹参数添加文件夹浏览功能
                StackPanel folderPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                TextBox folderTextBox = new TextBox 
                { 
                    Text = defaultValue, 
                    Width = 160, 
                    Margin = new Thickness(0, 0, 5, 0),
                    IsReadOnly = false,
                    Background = System.Windows.Media.Brushes.LightYellow,
                    BorderBrush = System.Windows.Media.Brushes.Orange,
                    BorderThickness = new Thickness(1),
                    ToolTip = "点击选择文件夹路径"
                };
                
                Button browseButton = new Button
                {
                    Content = "浏览",
                    Width = 35,
                    Height = 23,
                    Background = System.Windows.Media.Brushes.LightBlue,
                    BorderBrush = System.Windows.Media.Brushes.Blue,
                    Cursor = Cursors.Hand
                };
                
                 // 浏览按钮点击事件
                 browseButton.Click += (s, e) => {
                     try
                     {
                        if (name == "项目文件夹")
                        {
                            if (TrySelectLJDeveloperUserProjectSourcePath(out string projectSourcePath))
                            {
                                folderTextBox.Text = projectSourcePath;
                                LogMessage($"已选择项目文件夹(source): {projectSourcePath}", LogLevel.Info);
                            }
                            return;
                        }

                         var dialog = new System.Windows.Forms.FolderBrowserDialog();
                         dialog.Description = $"选择 {name}";

                         // 为项目文件夹设置默认路径
                         if (name == "项目文件夹")
                         {
                            var defaultPath = @"C:\Users\Public\Documents\KEYENCE\LJ Developer\User";
                            // 创建目录如果不存在
                            if (!System.IO.Directory.Exists(defaultPath))
                            {
                                try
                                {
                                    System.IO.Directory.CreateDirectory(defaultPath);
                                }
                                catch (Exception createEx)
                                {
                                    MessageBox.Show($"无法创建默认目录: {createEx.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                            dialog.SelectedPath = defaultPath;
                         }
                         else
                         {
                            dialog.SelectedPath = folderTextBox.Text;
                         }

                        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            var selectedPath = dialog.SelectedPath;

                            // 验证项目文件夹必须是source文件夹
                            if (name == "项目文件夹")
                            {
                                if (!selectedPath.EndsWith("\\source", StringComparison.OrdinalIgnoreCase) &&
                                    !selectedPath.EndsWith("/source", StringComparison.OrdinalIgnoreCase))
                                {
                                    MessageBox.Show(
                                        "❌ 错误：选择的文件夹必须是source文件夹！\n\n" +
                                        "正确路径格式：C:\\Users\\Public\\Documents\\KEYENCE\\LJ Developer\\User\\[项目名]\\source",
                                        "路径验证失败",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                    return;
                                }
                            }

                            folderTextBox.Text = selectedPath;
                            LogMessage($"已选择文件夹: {selectedPath}", LogLevel.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"选择文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                
                // TextBox点击事件 - 也可以打开文件夹选择对话框
                folderTextBox.MouseDoubleClick += (s, e) => {
                    browseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                
                folderTextBox.GotFocus += (s, e) => {
                    folderTextBox.ToolTip = "双击可选择文件夹，或点击右侧浏览按钮";
                };
                
                folderPanel.Children.Add(folderTextBox);
                folderPanel.Children.Add(browseButton);
                panel.Children.Add(folderPanel);
                
                // 存储TextBox引用以便稍后访问
                inputParameterControls[currentStep][name] = folderTextBox;
            }
            else if (IsTextInputParameter(name))
            {
                // 🔧 修复：文本输入参数（如模板名、备注等）不使用智能卡片，支持键盘直接输入
                TextBox textBox = new TextBox 
                { 
                    Text = defaultValue, 
                    Width = 200, 
                    Margin = new Thickness(10, 0, 0, 0),
                    // 保持默认的可编辑状态，不启用智能输入
                    IsReadOnly = false,
                    Background = System.Windows.Media.Brushes.LightGreen, // 浅绿色背景提示这是文本输入框
                    BorderBrush = System.Windows.Media.Brushes.Green,
                    BorderThickness = new Thickness(1)
                };
                
                // 不调用智能输入扩展，保持普通TextBox功能
                // 这样用户可以直接键盘输入模板名、备注等文本
                
                panel.Children.Add(textBox);
                
                // 存储控件引用以便稍后访问
                inputParameterControls[currentStep][name] = textBox;
            }
            else
            {
                // 创建智能输入框
                TextBox textBox = new TextBox 
                { 
                    Text = defaultValue, 
                    Width = 200, 
                    Margin = new Thickness(10, 0, 0, 0) 
                };
                
                // 启用智能输入功能
                WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetEnableSmartInput(textBox, true);
                WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetParameterName(textBox, name);
                
                // 根据参数名设置特定的配置
                SetupSmartInputConfig(textBox, name);
                
                // ✅ 已屏蔽TextBox自动change标记：避免在参数加载/切换步骤时产生大量重复日志
                // textBox.TextChanged += (s, e) => MarkAsChanged();
                // 现在只在用户点击"执行"按键时才记录配置变更
                
                panel.Children.Add(textBox);
                
                // 存储控件引用以便稍后访问
                inputParameterControls[currentStep][name] = textBox;
            }
            
            InputParametersPanel.Children.Add(panel);
        }

        /// <summary>
        /// 添加输出参数（已弃用 - 现在使用ConfigDataGrid显示检测结果）
        /// </summary>
        /// <param name="name">参数名称</param>
        /// <param name="value">参数值</param>
        [Obsolete("此方法已弃用，输出结果现在通过ConfigDataGrid自动同步显示")]
        private void AddOutputParameter(string name, string value)
        {
            // 此方法已弃用：原来用于在OutputParametersPanel中添加输出参数
            // 现在使用ConfigDataGrid自动同步显示Page1的检测结果，无需手动添加
            LogMessage($"输出参数 {name}: {value} (通过DataGrid自动显示)", LogLevel.Info);
        }

        /// <summary>
        /// 为智能输入框设置特定的配置信息
        /// </summary>
        /// <param name="textBox">目标TextBox</param>
        /// <param name="parameterName">参数名称</param>
        private void SetupSmartInputConfig(TextBox textBox, string parameterName)
        {
            try
            {
                // 根据参数名称设置特定的配置信息
                var config = ModuleRegistry.GetSmartInputParameterDisplayConfig(parameterName);
                
                if (!string.IsNullOrEmpty(config.Title))
                    WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetParameterTitle(textBox, config.Title);
                
                if (!string.IsNullOrEmpty(config.Description))
                    WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetParameterDescription(textBox, config.Description);
                
                if (!string.IsNullOrEmpty(config.ImagePath))
                    WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetImagePath(textBox, config.ImagePath);
                
                if (!string.IsNullOrEmpty(config.Unit))
                    WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetUnit(textBox, config.Unit);
                
                if (config.MinValue.HasValue)
                    WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetMinValue(textBox, config.MinValue);
                
                if (config.MaxValue.HasValue)
                    WpfApp2.UI.Controls.TextBoxSmartInputExtensions.SetMaxValue(textBox, config.MaxValue);
            }
            catch (Exception ex)
            {
                LogManager.Warning($"设置智能输入配置失败: {ex.Message}");
            }
        }

        private void ChangeVM(string name)
        {
            // 首先检查是否是图片选择步骤，如果是则显示多图像区域
            if (name == "图片选择")
            {
                // 切换到多图像显示模式
                SingleImageContainer.Visibility = Visibility.Collapsed;
                MultiImageContainer.Visibility = Visibility.Visible;

                _imageRenderer?.DisplayImageGroup(_currentImageGroup);

                PageManager.Page1Instance?.LogUpdate("已切换到多图像显示模式");
                return;
            }
            else
            {
                // 切换到单一VM控件显示模式
                SingleImageContainer.Visibility = Visibility.Visible;
                MultiImageContainer.Visibility = Visibility.Collapsed;
            }

            if (!ShouldAutoLoadVmSolution())
            {
                PageManager.Page1Instance?.LogUpdate("非VM引擎，跳过VM模块切换");
                return;
            }

            // 如果名称为空或不需要VM模块，直接返回
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            // 使用 ModuleRegistry 中的映射表（VmModuleName -> VmModulePath, VmModuleType）
            var vmModuleMap = ModuleRegistry.GetVmModuleMap();
            if (!vmModuleMap.TryGetValue(name, out var config))
            {
                return;
            }

            Action setupAction = GetVmModuleSetupAction(name, config.Path);

            try
            {
                object moduleInstance = VmSolution.Instance[config.Path];

                if (moduleInstance == null)
                {
                    PageManager.Page1Instance?.LogUpdate($"未找到VM模块: {config.Path}");
                    return;
                }

                // 根据模块类型设置VmParamsConfigWithRenderControl.ModuleSource
                switch (config.Type)
                {
                    case VmModuleType.ImageSource:
                        var imageSourceTool = moduleInstance as ImageSourceModuleCs.ImageSourceModuleTool;
                        if (imageSourceTool != null)
                            VmParamsConfigWithRenderControl.ModuleSource = imageSourceTool;
                        break;

                    case VmModuleType.ImageEnhance:
                        var enhanceTool = moduleInstance as IMVSImageEnhanceModuCs.IMVSImageEnhanceModuTool;
                        if (enhanceTool != null)
                            VmParamsConfigWithRenderControl.ModuleSource = enhanceTool;
                        break;

                    case VmModuleType.FeatureMatch:
                        var matchTool = moduleInstance as IMVSHPFeatureMatchModuCs.IMVSHPFeatureMatchModuTool;
                        if (matchTool != null)
                            VmParamsConfigWithRenderControl.ModuleSource = matchTool;
                        break;

                    case VmModuleType.SaveImage:
                        var saveImageTool = moduleInstance as SaveImageCs.SaveImageTool;
                        if (saveImageTool != null)
                            VmParamsConfigWithRenderControl.ModuleSource = saveImageTool;
                        break;

                    case VmModuleType.BlobFind:
                        var blobFindTool = moduleInstance as IMVSBlobFindModuCs.IMVSBlobFindModuTool;
                        if (blobFindTool != null)
                            VmParamsConfigWithRenderControl.ModuleSource = blobFindTool;
                        break;

                    case VmModuleType.LineFind:
                        var lineFindTool = moduleInstance as IMVSLineFindModuCs.IMVSLineFindModuTool;
                        if (lineFindTool != null)
                            VmParamsConfigWithRenderControl.ModuleSource = lineFindTool;
                        break;

                    case VmModuleType.CircleFind:
                        var circleFindTool = moduleInstance as IMVSCircleFindModuCs.IMVSCircleFindModuTool;
                        if (circleFindTool != null)
                            VmParamsConfigWithRenderControl.ModuleSource = circleFindTool;
                        break;

                    case VmModuleType.FlawModuleC:
                        // 对于自定义类型，尝试转换为IVmModule接口
                        var vmModule = moduleInstance as IMVSCnnFlawModuCCs.IMVSCnnFlawModuCTool;
                        if (vmModule != null)
                            VmParamsConfigWithRenderControl.ModuleSource = vmModule;
                        break;
                }

                // 执行额外的设置操作
                setupAction?.Invoke();

                PageManager.Page1Instance?.LogUpdate($"已切换到VM模块: {config.Path}");
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"切换VM模块失败: {ex.Message}");
                MessageBox.Show($"切换到 {name} 模块时出错: {ex.Message}");
            }
        }

        // VmModuleType 枚举已迁移到 WpfApp2.UI.Models.VmModuleType
        // 使用 using alias: VmModuleType = WpfApp2.UI.Models.VmModuleType;

        /// <summary>
        /// 获取VM模块切换时的额外设置逻辑（例如加载匹配模板）
        /// </summary>
        private Action GetVmModuleSetupAction(string vmModuleName, string vmModulePath)
        {
            switch (vmModuleName)
            {
                case "PKG位置匹配":
                    return () => ApplyFeatureMatchTemplate(vmModulePath, GetMatchTemplatePathFromConfig(), "匹配模板");
                case "异图PKG匹配":
                    return () => ApplyFeatureMatchTemplate(vmModulePath, GetCoatingPkgMatchTemplatePathFromConfig(), "镀膜PKG模板");
                case "BLK位置匹配":
                    return () => ApplyFeatureMatchTemplate(vmModulePath, GetBlkMatchTemplatePathFromConfig(), "BLK匹配模板");
                case "镀膜匹配":
                    return () => ApplyFeatureMatchTemplate(vmModulePath, GetCoatingMatchTemplatePathFromConfig(), "镀膜匹配模板");
                default:
                    return null;
            }
        }

        private void ApplyFeatureMatchTemplate(string vmModulePath, string templatePath, string templateLabel)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vmModulePath))
                {
                    return;
                }

                var matchTool = VmSolution.Instance[vmModulePath] as IMVSHPFeatureMatchModuCs.IMVSHPFeatureMatchModuTool;
                if (matchTool == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
                {
                    matchTool.ImportModelData(new string[] { templatePath });
                    PageManager.Page1Instance?.LogUpdate($"已从配置加载{templateLabel}: {templatePath}");
                }
                else if (!string.IsNullOrWhiteSpace(templatePath))
                {
                    PageManager.Page1Instance?.LogUpdate($"{templateLabel}文件不存在: {templatePath}");
                }
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"加载{templateLabel}失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔧 暂时屏蔽：处理划痕检测模型文件路径的特殊逻辑
        /// 恢复到最简单状态，避免复杂的路径处理逻辑
        /// </summary>
        /// <param name="errorLog">错误日志</param>
        private void HandleScratchDetectionModelPath(StringBuilder errorLog)
        {
            // 🔧 暂时完全屏蔽划痕检测模型文件路径处理
            // 由于配置模板路径仍有问题，暂时放弃这个功能
            LogManager.Info("划痕检测模型文件路径处理已暂时屏蔽", "文件路径处理");
            
            /* 
            // 原有复杂逻辑已屏蔽，等待问题解决后重新启用
            try
            {
                // 简单的模型文件路径处理逻辑可以在这里添加
                LogManager.Info("简单的划痕检测处理逻辑（当前为空实现）", "文件路径处理");
            }
            catch (Exception ex)
            {
                string errorMsg = $"处理划痕检测模型文件路径时出错: {ex.Message}";
                errorLog.AppendLine(errorMsg);
                LogManager.Error(errorMsg, "文件路径处理");
            }
            */
        }

        private void AddLabbel(string input_label)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };

            TextBlock textBlock = new TextBlock
            {
                Text = input_label,
                Width = 200,
                Foreground = new SolidColorBrush(Colors.Yellow),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap  // 添加文本换行支持
            };

            panel.Children.Add(textBlock);
            InputParametersPanel.Children.Add(panel);
        }

        // 添加加载模板方法
        public async void LoadTemplate(string templateFilePath, bool autoExecute = true)
        {
            try
            {
                CurrentTemplateFilePath = templateFilePath;

                // 加载模板
                currentTemplate = TemplateParameters.LoadFromFile(templateFilePath);
                currentTemplate.AlgorithmEngineId = AlgorithmEngineSettingsManager.PreferredEngineId;
                
                // 更新当前样品类型和涂布类型
                currentSampleType = currentTemplate.SampleType;
                currentCoatingType = currentTemplate.CoatingType;
                
                // 记录涂布类型加载信息
                string coatingTypeDisplay = currentCoatingType == CoatingType.Single ? "单涂布" : "双涂布";
                PageManager.Page1Instance?.LogUpdate($"已加载模板涂布类型: {coatingTypeDisplay} (步骤数: {stepConfigurations.Count} -> 即将重新配置)");
                UpdateCameraSelectionFlags();
                
                // 重新初始化步骤配置（根据加载的涂布类型）
                stepConfigurations = InitializeStepConfigurations(currentSampleType, currentCoatingType);
                ApplyCameraSpecificStepLayout();
                
                // 记录重新配置后的步骤信息
                PageManager.Page1Instance?.LogUpdate($"重新配置完成: 新步骤数: {stepConfigurations.Count}, 步骤列表: {string.Join(", ", stepConfigurations.Select(s => s.DisplayName))}");

                // 重新初始化分组，确保集合按钮最新
                InitializeStepGroups();
                
                // 清理旧的参数控件映射
                inputParameterControls.Clear();
                
                // 重新生成步骤按钮（因为步骤配置可能已改变）
                GenerateStepButtons();
                
                // 立即设置全局变量（算法层解耦）
                try
                {
                    int typeValue = (int)currentSampleType;
                    AlgorithmGlobalVariables.Set("TYPE", typeValue.ToString());
                    PageManager.Page1Instance?.LogUpdate($"已设置样品类型全局变量 TYPE = {typeValue} ({currentSampleType})");

                    int coatingCount = (int)currentCoatingType;
                    AlgorithmGlobalVariables.Set("涂布数目", coatingCount.ToString());
                    PageManager.Page1Instance?.LogUpdate($"已设置涂布数目全局变量 涂布数目 = {coatingCount} ({currentCoatingType})");
                }
                catch (Exception ex)
                {
                    LogMessage($"设置全局变量失败: {ex.Message}");
                }

                // 重置数据清理提示状态（新模板加载时允许重新询问）
                ResetDataCleanupPromptForNewTemplate();

                // 清理无效的历史数据
                CleanupTemplateData();
                
                //使用messagebox输出当前模板的参数
                string message = $"模板名称: {currentTemplate.TemplateName}\n" +
                                 $"备注: {currentTemplate.Remark}\n" +
                                 $"创建时间: {currentTemplate.CreatedTime}\n" +
                                 $"最后修改时间: {currentTemplate.LastModifiedTime}";
                foreach (var step in currentTemplate.InputParameters)
                {
                    // 根据StepType获取显示名称
                    string stepDisplayName = GetStepDisplayNameByType(step.Key);
                    message += $"\n步骤 {step.Key} ({stepDisplayName}):\n";
                    foreach (var param in step.Value)
                    {
                        message += $"{param.Key}: {param.Value}\n";
                    }
                }
                ScrollableMessageWindow.Show(message, "加载模板", false, "确定", "取消", 3);

                // 重置到第一步并更新UI以反映加载的模板
                currentStep = 0;
                UpdateUI(currentStep);
                // UpdateUI 已经会通过 UpdateStepButtons 调用 GenerateStepButtons

                // 更新page1模板名
                PageManager.Page1Instance.UpdateTemplateName(currentTemplate.TemplateName);

                // 更新TemplateConfigPage的模板名称显示
                UpdateCurrentTemplateNameDisplay();

            // 记录为最后使用的模板路径，防止相机页自动保存写入旧模板
            SaveLastUsedTemplate(templateFilePath);
            CurrentTemplateFilePath = templateFilePath;

                // 加载相机参数到CameraConfigPage
                try
                {
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    if (mainWindow?.frame_CameraConfigPage?.Content is CameraConfigPage cameraConfigPage)
                    {
                        cameraConfigPage.LoadCameraParametersFromTemplate(templateFilePath);
                        PageManager.Page1Instance?.LogUpdate("已将模板中的相机参数同步到相机配置页面");
                    }
                }
                catch (Exception ex)
                {
                    PageManager.Page1Instance?.LogUpdate($"同步相机参数到相机配置页面失败: {ex.Message}");
                }

                // 加载3D检测参数到内存
                try
                {
                    ThreeDSettings.LoadFromTemplate(currentTemplate);
                    PageManager.Page1Instance?.LogUpdate("已将模板中的3D检测参数加载到内存");
                }
                catch (Exception ex)
                {
                    PageManager.Page1Instance?.LogUpdate($"加载3D检测参数失败: {ex.Message}");
                }

                // **新增：自动执行模板**
                if (autoExecute)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            AutoExecuteTemplate();
                            PageManager.Page1Instance?.LogUpdate("已自动执行模板");
                        }
                        catch (Exception autoExecEx)
                        {
                            PageManager.Page1Instance?.LogUpdate($"自动执行模板失败: {autoExecEx.Message}");
                        }
                    }), DispatcherPriority.Background);
                }

                // 添加: 检查并应用图片路径参数（兼容性：支持旧的单图片和新的3图片）
                if (currentTemplate.InputParameters.ContainsKey(StepType.ImageSelection))
                {
                    var stepParams = currentTemplate.InputParameters[StepType.ImageSelection];
                    
                    // 优先检查新的3张图片路径格式
                    if (stepParams.ContainsKey("图像源1路径") ||
                        stepParams.ContainsKey("图像源2_1路径") ||
                        stepParams.ContainsKey("图像源2_2路径"))
                    {
                        try
                        {
                            // 获取3张图片路径
                            var imagePaths = GetCurrentImagePaths();
                            bool allPathsValid = !string.IsNullOrWhiteSpace(imagePaths.Item1) && File.Exists(imagePaths.Item1) &&
                                               !string.IsNullOrWhiteSpace(imagePaths.Item2) && File.Exists(imagePaths.Item2) &&
                                               !string.IsNullOrWhiteSpace(imagePaths.Item3) && File.Exists(imagePaths.Item3);
                            
                            if (allPathsValid)
                            {
                                // 创建ImageGroupSet并保存
                                _currentImageGroup = new ImageGroupSet
                                {
                                    Source1Path = imagePaths.Item1,
                                    Source2_1Path = imagePaths.Item2,
                                    Source2_2Path = imagePaths.Item3,
                                    BaseName = Path.GetFileNameWithoutExtension(imagePaths.Item1)
                                };
                                _imageRenderer?.DisplayImageGroup(_currentImageGroup);
                                
                                PageManager.Page1Instance?.LogUpdate($"已从模板加载完整的3张图片组: {_currentImageGroup.BaseName}");
                                LogMessage($"模板加载: 3张图片全部加载成功", LogLevel.Info);
                            }
                            else
                            {
                                // 部分图片缺失或不存在，弹窗告警
                                var missingFiles = new List<string>();
                                if (string.IsNullOrWhiteSpace(imagePaths.Item1) || !File.Exists(imagePaths.Item1))
                                    missingFiles.Add($"图像源1: {imagePaths.Item1 ?? "未设置"}");
                                if (string.IsNullOrWhiteSpace(imagePaths.Item2) || !File.Exists(imagePaths.Item2))
                                    missingFiles.Add($"图像源2_1: {imagePaths.Item2 ?? "未设置"}");
                                if (string.IsNullOrWhiteSpace(imagePaths.Item3) || !File.Exists(imagePaths.Item3))
                                    missingFiles.Add($"图像源2_2: {imagePaths.Item3 ?? "未设置"}");
                                
                                string errorMsg = $"模板加载错误：部分图片文件缺失或不存在！\n\n" +
                                                $"缺失的文件：\n{string.Join("\n", missingFiles)}\n\n" +
                                                $"请检查模板配置或重新选择完整的图片组。";
                                
                                ScrollableMessageWindow.Show(errorMsg, "图片文件缺失", false);
                                LogMessage($"模板加载: 部分图片文件缺失", LogLevel.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"加载3张图片路径失败: {ex.Message}", LogLevel.Error);
                            ScrollableMessageWindow.Show($"加载3张图片时发生错误: {ex.Message}\n\n请检查模板配置。", 
                                          "加载错误", false);
                        }
                    }
                    // 兼容性：支持旧的单图片路径格式，自动匹配其他图片
                    else if (stepParams.ContainsKey("图片路径"))
                    {
                        string imagePath = stepParams["图片路径"];

                        // 确认文件存在再处理
                    if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                    {
                        try
                        {
                                                // 尝试自动匹配其他图片
                var matchedGroup = AutoMatchImageGroup(imagePath);
                if (matchedGroup != null && matchedGroup.Has2DImages)
                                {
                                    // 匹配成功，保存到成员变量
                                    _currentImageGroup = matchedGroup;
                                    
                                    _imageRenderer?.DisplayImageGroup(_currentImageGroup);
                                    
                                    PageManager.Page1Instance?.LogUpdate($"已从模板加载并自动匹配图片组: {matchedGroup.BaseName}");
                                    LogMessage($"模板加载: 自动匹配成功 - {matchedGroup.BaseName}", LogLevel.Info);
                                }
                                else
                                {
                                    // 匹配失败，弹窗告警并仅设置单张图片
                                    string warningMsg = $"模板加载警告：无法自动匹配其他图片！\n\n" +
                                                      $"当前图片: {Path.GetFileName(imagePath)}\n" +
                                                      $"需要在以下文件夹结构中找到匹配图片：\n" +
                                                      $"父目录/\n" +
                                                      $"├── 图像源1/\n" +
                                                      $"├── 图像源2_1/\n" +
                                                      $"└── 图像源2_2/\n\n" +
                                                      $"目前只加载了一张图片，可能影响检测效果。\n" +
                                                      $"建议手动选择完整的图片组。";
                                    
                                    ScrollableMessageWindow.Show(warningMsg, "图片匹配失败", false);
                                    LogMessage($"模板加载: 图片自动匹配失败 - {imagePath}", LogLevel.Warning);
                                    
                                    _currentImageGroup = new ImageGroupSet
                                    {
                                        Source1Path = imagePath,
                                        BaseName = Path.GetFileNameWithoutExtension(imagePath)
                                    };
                                    _imageRenderer?.DisplayImageGroup(_currentImageGroup);
                                    PageManager.Page1Instance?.LogUpdate($"已加载模板图片(仅一张): {Path.GetFileName(imagePath)}");
                            }
                        }
                        catch (Exception ex)
                        {
                                LogMessage($"模板加载图片处理失败: {ex.Message}", LogLevel.Error);
                                
                                // 发生异常时弹窗告知用户
                                ScrollableMessageWindow.Show($"模板图片加载失败: {ex.Message}\n\n请检查图片路径和文件夹结构。", 
                                              "加载错误", false);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(imagePath))
                    {
                            // 图片文件不存在，弹窗告警
                            string errorMsg = $"模板中的图片文件不存在：\n{imagePath}\n\n请检查文件路径或重新选择图片。";
                            ScrollableMessageWindow.Show(errorMsg, "文件不存在", false);
                            LogMessage($"模板中的图片文件不存在: {imagePath}", LogLevel.Warning);
                        }
                    }
                }

                // 添加: 检查并应用匹配模板路径参数
                string matchTemplatePath = GetMatchTemplatePathFromConfig();
                if (!string.IsNullOrWhiteSpace(matchTemplatePath))
                {
                    if (File.Exists(matchTemplatePath))
                    {
                        try
                        {
                            var matchTool = (IMVSHPFeatureMatchModuCs.IMVSHPFeatureMatchModuTool)VmSolution.Instance["校准.PKG匹配"];
                            if (matchTool != null)
                            {
                                matchTool.ImportModelData(new string[] { matchTemplatePath });
                                PageManager.Page1Instance?.LogUpdate($"已加载匹配模板: {Path.GetFileName(matchTemplatePath)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"加载匹配模板失败: {ex.Message}", LogLevel.Error);
                        }
                    }
                    else
                    {
                        LogMessage($"匹配模板文件不存在: {matchTemplatePath}", LogLevel.Warning);
                    }
                }

                // 加载模板完成后，标记为已保存状态（加载的模板内容未修改）
                MarkAsSaved();
            }
            catch (Exception ex)
            {
                ScrollableMessageWindow.Show($"加载模板失败: {ex.Message}", "加载失败", false);
            }
        }


        private void load_vm_solution_async()
        {
            string vmFilePath = GetVmSolutionFilePath();

            if (string.IsNullOrEmpty(vmFilePath))
            {
                // 用户取消选择或未找到有效文件，退出应用程序
                LogManager.Critical("未选择VM解决方案文件，应用程序将退出", "VM解决方案加载");
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                return;
            }

            try
            {
                // 显示加载提示
                Dispatcher.Invoke(() =>
                {
                    PageManager.Page1Instance?.LogUpdate("正在加载算法平台，请稍候...");
                    // 可以在这里禁用其他相关控件
                });

                // 同步加载VM解决方案
                VmSolution.Load(vmFilePath);

                // 回到UI线程处理其他初始化
                Dispatcher.Invoke(() =>
                {
                    // 按照官方示例模式，初始化时也重新获取并绑定流程实例
                    var PictureProcedure = VmSolution.Instance["输出图显示"] as VmProcedure;
                    var calibrationProcedure = VmSolution.Instance["校准"] as VmProcedure;
                    if (calibrationProcedure != null)
                    {
                        PageManager.Page1Instance.render1.ModuleSource = PictureProcedure;
                        PageManager.Page1Instance.coating.ModuleSource = calibrationProcedure;
                    }

                    // VM加载成功，保存最后成功加载的文件路径到配置
                    try
                    {
                        SaveVmFilePathToConfig(vmFilePath);
                    }
                    catch (Exception ex)
                    {
                        // 保存配置失败不影响主流程，只记录日志
                        PageManager.Page1Instance?.LogUpdate($"保存VM文件路径配置失败: {ex.Message}");
                    }

                    // 如果成功，更新日志
                    PageManager.Page1Instance.LogUpdate($"算法平台加载成功: {Path.GetFileName(vmFilePath)}");

                    // VM加载完成后，初始化Page1的图片保存设置
                    try
                    {
                        PageManager.Page1Instance?.InitializeImageSaveSettings();
                    }
                    catch (Exception ex)
                    {
                        PageManager.Page1Instance?.LogUpdate($"初始化图片保存设置失败: {ex.Message}");
                    }

                    // 使用线程安全的方式确保事件处理器只绑定一次
                    lock (_eventBindingLock)
                    {
                        if (!_isEventHandlerBound)
                        {
                            //注册回调函数
                            VmSolution.OnWorkStatusEvent += VmSolution_OnWorkStatusEvent;
                            _isEventHandlerBound = true;
                            LogMessage("VM事件处理器已绑定", LogLevel.Info);
                        }
                        else
                        {
                            LogMessage("VM事件处理器已存在，跳过重复绑定", LogLevel.Info);
                        }
                    }

                    // 启用控件（如果需要的话可以在这里启用其他控件）
                    PageManager.Page1Instance?.LogUpdate("算法平台初始化完成");
                }, DispatcherPriority.Normal);

            }
            catch (VmException ex)
            {
                string errorMessage = $"加载VM解决方案失败！错误代码: {Convert.ToString(ex.errorCode, 16)}。这是核心检测流程无法启动的严重错误，应用程序将退出。";
                LogManager.Critical(errorMessage, "VM解决方案加载");

                // 加载失败也退出应用程序
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                string errorMessage = $"加载VM解决方案时出现异常: {ex.Message}。这是核心检测流程无法启动的严重错误，应用程序将退出。";
                LogManager.Critical(errorMessage, "VM解决方案加载");

                // 加载失败也退出应用程序
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
        }

        // 保留原有的同步方法作为备用或兼容
        [Obsolete("请使用load_vm_solution_async方法")]
        private void load_vm_solution()
        {
            string vmFilePath = GetVmSolutionFilePath();
            
            if (string.IsNullOrEmpty(vmFilePath))
            {
                // 用户取消选择或未找到有效文件，退出应用程序
                LogManager.Critical("未选择VM解决方案文件，应用程序将退出", "VM解决方案加载");
                Application.Current.Shutdown();
                return;
            }

            try
            {
                VmSolution.Load(vmFilePath);

                // 按照官方示例模式，初始化时也重新获取并绑定流程实例
                var PictureProcedure = VmSolution.Instance["输出图显示"] as VmProcedure;
                var calibrationProcedure = VmSolution.Instance["校准"] as VmProcedure;
                if (calibrationProcedure != null)
                {
                    PageManager.Page1Instance.render1.ModuleSource = PictureProcedure;
                    PageManager.Page1Instance.coating.ModuleSource = calibrationProcedure;
                }

                // VM加载成功，保存最后成功加载的文件路径到配置
                try
                {
                    SaveVmFilePathToConfig(vmFilePath);
                }
                catch (Exception ex)
                {
                    // 保存配置失败不影响主流程，只记录日志
                    PageManager.Page1Instance?.LogUpdate($"保存VM文件路径配置失败: {ex.Message}");
                }

                // 如果成功，更新日志
                PageManager.Page1Instance.LogUpdate($"算法平台加载成功: {Path.GetFileName(vmFilePath)}");

                // VM加载完成后，初始化Page1的图片保存设置
                try
                {
                    PageManager.Page1Instance?.InitializeImageSaveSettings();
                }
                catch (Exception ex)
                {
                    PageManager.Page1Instance?.LogUpdate($"初始化图片保存设置失败: {ex.Message}");
                }

                // 使用线程安全的方式确保事件处理器只绑定一次
                lock (_eventBindingLock)
                {
                    if (!_isEventHandlerBound)
                    {
                        //注册回调函数
                        VmSolution.OnWorkStatusEvent += VmSolution_OnWorkStatusEvent;
                        _isEventHandlerBound = true;
                        LogMessage("VM事件处理器已绑定", LogLevel.Info);
                    }
                    else
                    {
                        LogMessage("VM事件处理器已存在，跳过重复绑定", LogLevel.Info);
                    }
                }

            }
            catch (VmException ex)
            {
                string errorMessage = $"加载VM解决方案失败！错误代码: {Convert.ToString(ex.errorCode, 16)}。这是核心检测流程无法启动的严重错误，应用程序将退出。";
                LogManager.Critical(errorMessage, "VM解决方案加载");
                
                // 加载失败也退出应用程序
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                string errorMessage = $"加载VM解决方案时出现异常: {ex.Message}。这是核心检测流程无法启动的严重错误，应用程序将退出。";
                LogManager.Critical(errorMessage, "VM解决方案加载");
                
                // 加载失败也退出应用程序
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// 清理事件处理器绑定（在应用程序退出时调用）
        /// </summary>
        public static void CleanupEventHandlers()
        {
            lock (_eventBindingLock)
            {
                if (_isEventHandlerBound)
                {
                    try
                    {
                        VmSolution.OnWorkStatusEvent -= VmSolution_OnWorkStatusEvent;
                        _isEventHandlerBound = false;
                        // 注意：这里不能调用LogMessage，因为可能在应用程序关闭过程中
                    }
                    catch (Exception)
                    {
                        // 忽略清理过程中的异常
                    }
                }
                
                // 重置VM加载状态，允许下次启动时重新加载
                _isVmSolutionLoaded = false;
            }
        }

        /// <summary>
        /// 重置VM解决方案加载状态（用于调试或特殊情况）
        /// </summary>
        public static void ResetVmSolutionLoadStatus()
        {
            lock (_eventBindingLock)
            {
                _isVmSolutionLoaded = false;
                Instance?.LogMessage("VM解决方案加载状态已重置", LogLevel.Info);
            }
        }

        /// <summary>
        /// 清理实例资源（在页面被替换前调用，防止内存泄漏）
        /// </summary>
        public void CleanupInstanceResources()
        {
            try
            {
                LogMessage("开始清理TemplateConfigPage实例资源...", LogLevel.Info);

                // 1. 清理按钮缓存（解绑事件处理器）
                ClearButtonCaches();

                // 2. 清理输入参数面板中的控件
                if (InputParametersPanel != null)
                {
                    InputParametersPanel.Children.Clear();
                }

                // 3. 清理输入参数控件字典
                inputParameterControls?.Clear();

                // 4. 清理步骤配置和分组
                stepConfigurations?.Clear();
                stepGroups?.Clear();

                // 5. 清理3D视图资源（主进程不再加载Keyence 3D控件）
                try
                {
                    if (_3DViewHost_Template != null) _3DViewHost_Template.Child = null;
                    _threeDViewHostChild = null;
                }
                catch (Exception ex)
                {
                    LogMessage("清理3D视图资源时出错: " + ex.Message, LogLevel.Warning);
                }

                // 6. 清理VmRenderControl绑定（释放VM模块引用）
                try
                {
                    if (VmRender1 != null) VmRender1.ModuleSource = null;
                    if (VmRender2_1 != null) VmRender2_1.ModuleSource = null;
                    if (VmRender2_2 != null) VmRender2_2.ModuleSource = null;
                    if (VmParamsConfigWithRenderControl != null) VmParamsConfigWithRenderControl.ModuleSource = null;
                }
                catch (Exception ex)
                {
                    LogMessage($"清理VmRenderControl绑定时出错: {ex.Message}", LogLevel.Warning);
                }

                // 7. 清理当前图像组引用
                _currentImageGroup = null;

                // 8. 清理当前模板引用
                currentTemplate = null;

                // 9. 清理静态实例引用（如果当前实例是静态引用的实例）
                if (Instance == this)
                {
                    Instance = null;
                }

                LogMessage("TemplateConfigPage实例资源清理完成", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"清理实例资源时出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 获取VM解决方案文件路径
        /// </summary>
        /// <returns>有效的VM解决方案文件绝对路径，如果用户取消或未找到有效文件则返回null</returns>
        public string GetVmSolutionFilePath()
        {
            // 优先从配置文件读取上次使用的路径
            string lastUsedPath = ReadVmFilePathFromConfig();
            if (!string.IsNullOrEmpty(lastUsedPath) && File.Exists(lastUsedPath))
            {
                PageManager.Page1Instance?.LogUpdate($"自动加载上次使用的VM文件: {lastUsedPath}");
                return lastUsedPath;
            }

            // 如果配置文件中没有或文件不存在，弹窗要求用户选择
            PageManager.Page1Instance?.LogUpdate($"未找到上次使用的VM文件，需要手动选择");

            MessageBoxResult result = ScrollableMessageWindow.Show(
                $"未找到上次使用的VM解决方案文件。\n\n是否手动选择VM解决方案文件？\n\n点击「是」选择文件，点击「否」退出程序。",
                "VM文件缺失",
                true);

            if (result == MessageBoxResult.Cancel)
            {
                return null; // 用户选择退出
            }

            // 用户选择手动指定文件
            return SelectVmSolutionFile();
        }

        /// <summary>
        /// 弹出文件选择对话框选择VM解决方案文件
        /// </summary>
        /// <returns>选择的VM解决方案文件绝对路径，如果用户取消则返回null</returns>
        private string SelectVmSolutionFile()
        {
            while (true)
            {
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "VM解决方案文件|*.sol|所有文件|*.*",
                    Title = "选择VM解决方案文件",
                    DefaultExt = ".sol",
                    InitialDirectory = AppDomain.CurrentDomain.BaseDirectory // 设置初始目录为软件启动文件夹
                };

                if (dialog.ShowDialog() == true)
                {
                    string selectedPath = dialog.FileName;
                    string absolutePath = Path.GetFullPath(selectedPath);
                    
                    // 验证文件格式
                    if (!Path.GetExtension(absolutePath).Equals(".sol", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBoxResult retryResult = ScrollableMessageWindow.Show(
                            $"选择的文件格式不正确！\n\n文件: {absolutePath}\n\n" +
                            $"要求: 必须是.sol格式的VM解决方案文件\n\n" +
                            $"是否重新选择？点击「否」将退出程序。",
                            "文件格式错误",
                            true);
                        
                        if (retryResult == MessageBoxResult.Cancel)
                        {
                            return null; // 用户选择退出
                        }
                        
                        continue; // 重新选择
                    }
                    
                    // 验证文件是否存在
                    if (!File.Exists(absolutePath))
                    {
                        MessageBoxResult retryResult = ScrollableMessageWindow.Show(
                            $"选择的文件不存在！\n\n文件: {absolutePath}\n\n" +
                            $"是否重新选择？点击「否」将退出程序。",
                            "文件不存在",
                            true);
                        
                        if (retryResult == MessageBoxResult.Cancel)
                        {
                            return null; // 用户选择退出
                        }
                        
                        continue; // 重新选择
                    }
                    
                                         PageManager.Page1Instance?.LogUpdate($"用户选择VM文件: {absolutePath}");
                     return absolutePath;
                }
                else
                {
                    // 用户取消选择
                    MessageBoxResult exitResult = ScrollableMessageWindow.Show(
                        "未选择VM解决方案文件。\n\n程序无法继续运行，是否退出？",
                        "确认退出",
                        true);
                    
                    if (exitResult == MessageBoxResult.OK)
                    {
                        return null; // 用户确认退出
                    }
                    
                    // 用户选择重新选择文件
                    continue;
                }
            }
        }

        /// <summary>
        /// 从配置文件读取VM文件路径
        /// </summary>
        /// <returns>配置文件中保存的VM文件路径，如果读取失败或文件不存在则返回null</returns>
        private string ReadVmFilePathFromConfig()
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                string configFilePath = Path.Combine(configDir, "VmSolutionPath.txt");
                
                if (!File.Exists(configFilePath))
                {
                    return null;
                }

                string[] lines = File.ReadAllLines(configFilePath, Encoding.UTF8);
                
                // 查找不以#开头的行（实际路径行）
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("#"))
                    {
                        // 验证路径格式
                        if (Path.IsPathRooted(trimmedLine) && trimmedLine.EndsWith(".sol", StringComparison.OrdinalIgnoreCase))
                        {
                            return trimmedLine;
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"读取VM文件路径配置失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存VM文件路径到配置文件
        /// 保存最后成功加载的VM解决方案文件路径，供下次启动时参考或故障恢复使用
        /// </summary>
        /// <param name="vmFilePath">成功加载的VM文件绝对路径</param>
        private void SaveVmFilePathToConfig(string vmFilePath)
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);

                // 创建配置内容，包含路径和时间戳
                string configContent = $"# VM解决方案文件配置\n" +
                                     $"# 最后成功加载时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                     $"# 文件路径 (绝对路径)\n" +
                                     $"{vmFilePath}\n" +
                                     $"# 文件名: {Path.GetFileName(vmFilePath)}\n" +
                                     $"# 文件大小: {(File.Exists(vmFilePath) ? new FileInfo(vmFilePath).Length : 0)} 字节";

                string configFilePath = Path.Combine(configDir, "VmSolutionPath.txt");
                File.WriteAllText(configFilePath, configContent, Encoding.UTF8);
                
                PageManager.Page1Instance?.LogUpdate($"VM文件路径已保存到配置: {Path.GetFileName(configFilePath)}");
            }
            catch (Exception ex)
            {
                // 配置保存失败不影响主流程
                PageManager.Page1Instance?.LogUpdate($"保存VM文件路径配置失败: {ex.Message}");
            }
        }

        private static void VmSolution_OnWorkStatusEvent(ImvsSdkDefine.IMVS_MODULE_WORK_STAUS WorkStatuInfo)
        {
            // 10009流程：定拍/飞拍相机设定异常监测（只要收到该流程回调就提示）
            if (WorkStatuInfo.nProcessID == 10009)
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_isProcess10009WarningDialogShowing)
                        {
                            return;
                        }

                        _isProcess10009WarningDialogShowing = true;
                        LogManager.Warning("10009流程：检测到定拍或飞拍相机设定异常，准备弹窗提示", "VM回调");

                        var result = ScrollableMessageWindow.Show(
                            "定拍或飞拍相机设定异常，请进入“硬件配置”界面查看",
                            "相机设定异常",
                            showCancel: true,
                            okButtonText: "进入硬件配置",
                            cancelButtonText: "关闭",
                            autoCloseSeconds: 0);

                        if (result == MessageBoxResult.OK)
                        {
                            NavigateToHardwareConfigPageFromVmCallback();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"10009流程：显示弹窗失败 - {ex.Message}", "VM回调");
                    }
                    finally
                    {
                        _isProcess10009WarningDialogShowing = false;
                    }
                }), DispatcherPriority.Background);

                return;
            }

            if (WorkStatuInfo.nWorkStatus == 0) // 流程执行完成
            {
                try
                {
                    switch (WorkStatuInfo.nProcessID)
                    {
                        case 10001: // 主要检测流程
                            // ✅ 按官方示例：立即设置完成标志
                            Page1.SetVmCallbackReceived();
                            
                            // 通知系统测试窗口VM回调完成（如果窗口存在）
                            try
                            {
                                WpfApp2.UI.SystemTestWindow.NotifyVMCallbackCompleted();
                            }
                            catch
                            {
                                // 系统测试窗口可能未打开，忽略异常
                            }
                            
                            // ✅ 按官方示例：VM数据获取在回调函数内直接同步进行
                            string defectType = null;
                            try
                            {
                                defectType = AlgorithmGlobalVariables.Get("异常类型");
                                Page1.SetCached2DDetectionResult(defectType);
                            }
                            catch (Exception vmEx)
                            {
                                LogManager.Error($"获取VM全局变量失败: {vmEx.Message}", "VM回调");
                            }
                            
                            // ✅ 按官方示例：只有UI更新才异步处理
                            if (defectType != null)
                            {
                                Task.Run(() =>
                                {
                                    try
                                    {
                                        Instance?.ReadMeasurementDataFromVM();
                                        LogManager.Info($"2D检测结果已读取（等待统一判定）: {defectType}", "VM回调");
                                    }
                                    catch (Exception ex)
                                    {
                                        LogManager.Error($"UI更新失败: {ex.Message}", "VM回调");
                                    }
                                });
                            }
                            break;
                            
                        case 10000: // IO复位流程
                            Task.Run(() =>
                            {
                                try
                                {
                                    WpfApp2.SMTGPIO.IOManager.ResetAllOutputs();
                                    LogManager.Info("10000流程：IO已自动复位", "VM回调");
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Error($"10000流程：IO复位失败 - {ex.Message}", "VM回调");
                                }
                            });
                            break;
                            
                        case 10004: // 错误复位流程
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    ScrollableMessageWindow.Show(
                                        "✅ 初始化/错误复位完成\n\n操作结果：\n• VM初始化流程执行完成\n• IO设备NG输出已设置\n• 队列清空操作完成\n\n系统已恢复到错误复位状态。",
                                        "错误复位完成",
                                        showCancel: false,
                                        okButtonText: "确定",
                                        cancelButtonText: "取消",
                                        autoCloseSeconds: 3); // 3秒后自动关闭

                                    LogManager.Info("10004流程：错误复位完成弹窗已显示（3秒后自动关闭）", "VM回调");
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Error($"10004流程：显示弹窗失败 - {ex.Message}", "VM回调");
                                }
                            }), DispatcherPriority.Background);
                            break;
                            
                        case 10006: // VM存图流程
                            Task.Run(() =>
                            {
                                try
                                {
                                    LogManager.Info("10006流程：VM存图流程执行完成", "VM回调");
                                    LogManager.Info("VM存图完成，系统准备处理下一次检测", "VM回调");
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Error($"10006流程：VM存图完成处理失败 - {ex.Message}", "VM回调");
                                }
                            });
                            break;
                             
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error($"VM回调异常: {ex.Message}", "VM回调");
                    Task.Run(() =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ScrollableMessageWindow.Show("VM回调处理失败: " + ex.Message, "系统错误", false);
                        }), DispatcherPriority.Background);
                    });
                }
            }
        }

        /// <summary>
        /// VM回调场景下跳转到硬件配置页面
        /// </summary>
        private static void NavigateToHardwareConfigPageFromVmCallback()
        {
            try
            {
                var mainWindow = Application.Current?.Windows?.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow == null)
                {
                    LogManager.Error("无法找到主窗口，无法跳转到硬件配置页面", "VM回调");
                    return;
                }

                if (mainWindow.frame_HardwareConfigPage == null)
                {
                    LogManager.Warning("系统尚未完全初始化，无法跳转到硬件配置页面", "VM回调");
                    MessageBox.Show("系统尚未完全初始化，请稍等片刻后重试", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                mainWindow.ContentC.Content = mainWindow.frame_HardwareConfigPage;
                LogManager.Info("已跳转到硬件配置页面（来源：10009告警弹窗）", "VM回调");
            }
            catch (Exception ex)
            {
                LogManager.Error($"跳转到硬件配置页面失败: {ex.Message}", "VM回调");
                MessageBox.Show($"跳转到硬件配置页面失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// 简化的2D图片转存处理（VM回调完成后直接处理）
        /// </summary>
        /// <param name="isOK">统一判定结果</param>
        /// <param name="defectType">缺陷类型</param>
        public static async Task Handle2DImageTransfer(bool isOK, string defectType)
        {
            try
            {
                string temp2DDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp2DImages");
                
                if (!Directory.Exists(temp2DDirectory))
                {
                    LogManager.Warning("2D临时目录不存在，跳过2D图片转存", "2D图片转存");
                    return;
                }

                // 简单等待VM释放文件（避免占用冲突）
                await Task.Delay(100);

                // 简单直接：获取文件列表，无复杂等待
                var files = Directory.GetFiles(temp2DDirectory, "*.*");
                
                if (files.Length == 0)
                {
                    LogManager.Warning("Temp2DImages目录中没有找到图片文件", "2D图片转存");
                    return;
                }

                // 判断是否需要保存
                bool shouldSave = ShouldSave2DImages(isOK);
                
                if (!shouldSave)
                {
                    Clear2DTemporaryDirectory();
                    return;
                }

                // 需要保存，移动到最终目录
                await Move2DImagesIndependent(defectType);
            }
            catch (Exception ex)
            {
                LogManager.Error($"处理2D图片转存时出错: {ex.Message}", "2D图片转存");
                Clear2DTemporaryDirectory();
            }
        }

        /// <summary>
        /// 判断是否应该保存2D图片
        /// </summary>
        private static bool ShouldSave2DImages(bool isOK)
        {
            try
            {
                bool saveAllImages = false;
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    saveAllImages = PageManager.Page1Instance?.ImageSaveModeToggle?.IsChecked == true;
                });

                return saveAllImages || !isOK; // 保存所有图片 或 仅保存NG图片
            }
            catch (Exception ex)
            {
                LogManager.Error($"判断2D保存策略失败: {ex.Message}", "2D图片转存");
                return false; // 出错时默认不保存
            }
        }

        /// <summary>
        /// 移动2D图片到最终目录
        /// </summary>
        private static async Task Move2DImagesIndependent(string defectType)
        {
            await Task.Run(() =>
            {
                try
                {
                    string safeDefectType = SanitizeFileName(defectType);
                    int currentImageNumber = GetCurrentImageNumber();
                    // 存图序号不再补零：期望 a_1 而不是 a_0001
                    string imageNumberStr = currentImageNumber.ToString();
                    string rootDirectory = GetImageSaveRootDirectory();

                    Move2DImagesCore(rootDirectory, safeDefectType, imageNumberStr);
                }
                catch (Exception ex)
                {
                    LogManager.Error($"移动2D图片失败: {ex.Message}", "2D图片转存");
                }
            });
        }

        /// <summary>
        /// 移动2D图片的核心逻辑（带简单重试机制）
        /// </summary>
        private static void Move2DImagesCore(string rootDirectory, string defectType, string imageNumberStr)
        {
            try
            {
                string temp2DDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp2DImages");
                
                if (!Directory.Exists(temp2DDirectory))
                    return;

                var files = Directory.GetFiles(temp2DDirectory, "*.*");
                if (files.Length == 0)
                    return;

                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    string fileExtension = Path.GetExtension(file);
                    
                    string targetFolder;
                    string newFileName;
                    
                    // 根据文件名确定目标文件夹和新文件名
                    if (fileName.Equals("a", StringComparison.OrdinalIgnoreCase))
                    {
                        targetFolder = Path.Combine(rootDirectory, defectType, "图像源1");
                        newFileName = $"a_{imageNumberStr}{fileExtension}";
                    }
                    else if (fileName.Equals("b", StringComparison.OrdinalIgnoreCase))
                    {
                        targetFolder = Path.Combine(rootDirectory, defectType, "图像源2_1");
                        newFileName = $"b_{imageNumberStr}{fileExtension}";
                    }
                    else if (fileName.Equals("c", StringComparison.OrdinalIgnoreCase))
                    {
                        targetFolder = Path.Combine(rootDirectory, defectType, "图像源2_2");
                        newFileName = $"c_{imageNumberStr}{fileExtension}";
                    }
                    else
                    {
                        LogManager.Warning($"未知2D图片文件名: '{fileName}'，跳过", "2D图片转存");
                        continue;
                    }
                    
                    // 确保目标文件夹存在
                    EnsureDirectoryExists(targetFolder);
                    
                    string finalPath = Path.Combine(targetFolder, newFileName);
                    
                    // 简单重试机制：最多试3次，每次间隔100ms
                    bool moveSuccess = false;
                    for (int retry = 0; retry < 3; retry++)
                    {
                    try
                    {
                        if (!File.Exists(file))
                                break;
                        
                            // 删除目标文件（如果存在）
                        if (File.Exists(finalPath))
                            File.Delete(finalPath);
                        
                        File.Move(file, finalPath);
                            moveSuccess = true;
                            break;
                    }
                    catch (Exception moveEx)
                    {
                            if (retry == 2) // 最后一次尝试失败
                            {
                                LogManager.Error($"移动2D图片失败(重试{retry + 1}次): {Path.GetFileName(file)} - {moveEx.Message}", "2D图片转存");
                            }
                            else
                            {
                                // 等待后重试
                                System.Threading.Thread.Sleep(100);
                            }
                        }
                    }
                }
                }
                catch (Exception ex)
                {
                LogManager.Error($"处理2D图片核心逻辑失败: {ex.Message}", "2D图片转存");
                }
            }
            


        /// <summary>
        /// 清理2D临时目录中的文件
        /// </summary>
        private static void Clear2DTemporaryDirectory()
        {
            try
            {
                string temp2DDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp2DImages");
                
                if (!Directory.Exists(temp2DDirectory))
                    return;
                
                var files = Directory.GetFiles(temp2DDirectory, "*.*");
                
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Warning($"删除临时文件失败: {Path.GetFileName(file)} - {ex.Message}", "2D图片转存");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"清理2D临时目录失败: {ex.Message}", "2D图片转存");
            }
        }

        /// <summary>
        /// 清理文件名中的非法字符，生成安全的文件夹名
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "未知";
                
            // 替换非法字符为下划线
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }
            
            // 移除前后空白字符
            fileName = fileName.Trim();
            
            // 如果为空则使用默认名称
            if (string.IsNullOrEmpty(fileName))
                return "未知";
                
            return fileName;
        }

        /// <summary>
        /// 获取当前图号
        /// </summary>
        public static int GetCurrentImageNumber()
        {
            try
            {
                // 通过Page1的公共方法获取当前图号
                return PageManager.Page1Instance?.GetCurrentImageNumber() ?? 0;
            }
            catch (Exception ex)
            {
                Instance?.LogMessage($"获取当前图号失败: {ex.Message}", LogLevel.Warning);
                return 0;
            }
        }

        /// <summary>
        /// 获取图像保存根目录
        /// </summary>
        private static string GetImageSaveRootDirectory()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string lotValue = PageManager.Page1Instance?.CurrentLotValue ?? "DefaultLot";
                string todayFolder = DateTime.Now.ToString("yyyyMMdd");
                
                return Path.Combine(baseDir, "原图存储", lotValue, todayFolder);
            }
            catch (Exception ex)
            {
                Instance?.LogMessage($"获取图像保存根目录失败: {ex.Message}", LogLevel.Warning);
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "原图存储", "DefaultLot", DateTime.Now.ToString("yyyyMMdd"));
            }
        }

        /// <summary>
        /// 确保目录存在，如果不存在则创建
        /// </summary>
        private static void EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    Instance?.LogMessage($"[2D转存] 创建目录: {directoryPath}", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                Instance?.LogMessage($"[2D转存] 创建目录失败: {directoryPath} - {ex.Message}", LogLevel.Error);
            }
        }

        // 加记录最后使用模板
        private void SaveLastUsedTemplate(string templateFilePath)
        {
            try
            {
                // 创建或更新配置目录
                string configDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);

                // 保存最后使用的模板路径到配置文件
                string configFilePath = System.IO.Path.Combine(configDir, "LastUsedTemplate.txt");
                File.WriteAllText(configFilePath, templateFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存最后使用模板配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将微米(um)转换为像素(pixel)
        /// </summary>
        /// <param name="micrometers">微米值</param>
        /// <param name="pixelSize">可选参数：像元尺寸，默认为4微米/像素</param>
        /// <returns>对应的像素值</returns>
        public static double UmToPixel(double micrometers, double pixelSize = 4.0)
        {
            // 1像素 = pixelSize微米，因此微米/pixelSize = 像素
            return micrometers / pixelSize;
        }

        /// <summary>
        /// 将毫米(mm)转换为像素(pixel)
        /// </summary>
        /// <param name="millimeters">毫米值</param>
        /// <param name="pixelSize">可选参数：像元尺寸，默认为4微米/像素</param>
        /// <returns>对应的像素值</returns>
        public static double MmToPixel(double millimeters, double pixelSize = 4.0)
        {
            // 1毫米 = 1000微米，因此先转换为微米再转换为像素
            double micrometers = millimeters * 1000.0;
            return UmToPixel(micrometers, pixelSize);
        }

        /// <summary>
        /// 读取VM全局变量"异常类型"
        /// </summary>
        /// <param name="updateStatistics">是否更新统计信息，默认为true</param>
        public void ReadDefectTypeFromGlobalVariable(bool updateStatistics = true)
        {
            try
            {
                // 读取异常类型全局变量（算法层解耦）
                string defectType = AlgorithmGlobalVariables.Get("异常类型");

                // 🔧 修复重复读取：立即缓存2D检测结果
                Page1.SetCached2DDetectionResult(defectType);

                // 根据参数决定是否更新界面显示和统计信息
                if (updateStatistics)
                {
                    // 传统模式：更新界面显示和统计信息
                    if (PageManager.Page1Instance != null)
                    {
                        PageManager.Page1Instance.UpdateDefectType(defectType);
                    }
                    UpdateDefectStatistics(defectType);
                    LogMessage($"2D检测结果已读取并更新统计: {defectType}", LogLevel.Info);
                }
                else
                {
                    // 统一判定模式：只记录结果，不更新界面和统计
                    LogMessage($"2D检测结果已读取（等待统一判定）: {defectType}", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"读取异常类型时出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 记录检测数据到数据分析系统
        /// [已弃用] 此方法已被 UpdateDataGridWithMeasurements 中的直接记录逻辑替代
        /// </summary>
        [Obsolete("此方法已弃用，数据现在在 UpdateDataGridWithMeasurements 中直接记录，避免从DataGrid二次读取")]
        private void RecordDetectionDataForAnalysis()
        {
            LogMessage("RecordDetectionDataForAnalysis 方法已弃用，数据已在 UpdateDataGridWithMeasurements 中直接记录，避免从DataGrid二次读取", LogLevel.Info);
        }

        /// <summary>
        /// 统计缺陷类型，更新统计数据和饼图
        /// </summary>
        /// <param name="defectType">缺陷类型字符串，如果是"良品"则计入良品数</param>
        public void UpdateDefectStatistics(string defectType)
        {
            string OK_OR_NG;
            try
            {
                // 检查是否在模板配置模式，如果是则跳过统计更新
                bool isInTemplateConfigMode = PageManager.Page1Instance?.DetectionManager?.SystemState == SystemDetectionState.TemplateConfiguring;
                if (isInTemplateConfigMode)
                {
                    LogMessage("模板配置模式：跳过统计数据更新", LogLevel.Info);
                    return;
                }

                // 增加总数
                totalCount++;

                // 检查是否是良品
                if (defectType == "良品")
                {
                    okCount++;
                    OK_OR_NG = "OK";
                    
                    // 良品情况下重置连续NG计数
                    _consecutiveNGCount = 0;
                    _lastNGType = "";
                }
                else
                {
                    OK_OR_NG = "NG";
                    // 非良品的情况，更新缺陷类型计数
                    if (defectTypeCounter.ContainsKey(defectType))
                    {
                        defectTypeCounter[defectType]++;
                    }
                    else
                    {
                        defectTypeCounter[defectType] = 1;
                    }
                    
                    // 连续NG检测逻辑（仅在正常检测模式下，非测试图片模式）
                    CheckConsecutiveNG(defectType);
                }

                // 计算良率
                yieldRate = (double)okCount / totalCount * 100;

                // 在更新UI前自增图号（每次处理VM回调后）
                PageManager.Page1Instance?.IncrementAndUpdateImageNumber();

                // 更新UI
                UpdateDefectStatisticsUI(yieldRate,OK_OR_NG);
            }
            catch (Exception ex)
            {
                LogMessage($"更新缺陷统计时出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 检测连续相同NG，触发告警
        /// </summary>
        /// <param name="defectType">当前检测到的缺陷类型</param>
        private void CheckConsecutiveNG(string defectType)
        {
            try
            {
                // 按需求：只在正常检测时启用告警，测试图片模式下不告警
                if (IsInTestImageMode() && !_enableDebugModeForTesting)
                {
                    // 测试图片模式下不进行连续NG告警（符合需求要求）
                    return;
                }

                // 检查是否与上一次NG类型相同
                if (_lastNGType == defectType)
                {
                    // 相同NG类型，增加计数
                    _consecutiveNGCount++;
                    LogMessage($"连续NG检测: 相同类型'{defectType}'，当前连续次数: {_consecutiveNGCount}", LogLevel.Info);
                    
                    // 检查是否达到告警阈值
                    if (_consecutiveNGCount >= CONSECUTIVE_NG_ALERT_THRESHOLD)
                    {
                        LogMessage($"触发连续NG告警: '{defectType}' 连续{_consecutiveNGCount}次", LogLevel.Warning);
                        //ShowConsecutiveNGAlert(defectType, _consecutiveNGCount);
                        
                        // 重置计数器，避免重复告警
                        _consecutiveNGCount = 0;
                        _lastNGType = "";
                        LogMessage("连续NG计数器已重置（告警后自动重置）", LogLevel.Info);
                    }
                }
                else
                {
                    // 不同NG类型，重置计数器
                    _consecutiveNGCount = 1;
                    _lastNGType = defectType;
                    LogMessage($"连续NG检测: 新NG类型'{defectType}'，重置计数器，当前次数: 1", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"检测连续NG时出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 判断是否处于测试图片模式
        /// </summary>
        /// <returns>true if在测试图片模式，false if在正常检测模式</returns>
        private bool IsInTestImageMode()
        {
            try
            {
                // 通过Page1的公共方法来判断是否处于图片测试模式
                return PageManager.Page1Instance?.IsInImageTestMode() ?? false;
            }
            catch (Exception ex)
            {
                LogMessage($"判断测试模式时出错: {ex.Message}", LogLevel.Warning);
                // 如果无法判断，默认认为不在测试模式，保证告警功能正常工作
                return false;
            }
        }

        /// <summary>
        /// 显示连续NG告警弹窗
        /// </summary>
        /// <param name="ngType">NG类型</param>
        /// <param name="count">连续次数</param>
        private void ShowConsecutiveNGAlert(string ngType, int count)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 创建醒目的告警弹窗
                    var alertWindow = CreateConsecutiveNGAlertWindow(ngType, count);
                    
                    // 记录告警日志
                    string alertMessage = $"⚠️ 连续NG告警：连续{count}次检测到相同缺陷类型'{ngType}'";
                    LogManager.Warning(alertMessage, "连续NG告警");
                    
                    // 显示弹窗
                    alertWindow.ShowDialog();
                    
                }), System.Windows.Threading.DispatcherPriority.Send); // 使用最高优先级确保立即显示
            }
            catch (Exception ex)
            {
                LogMessage($"显示连续NG告警时出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 创建连续NG告警窗口
        /// </summary>
        /// <param name="ngType">NG类型</param>
        /// <param name="count">连续次数</param>
        /// <returns>告警窗口</returns>
        private Window CreateConsecutiveNGAlertWindow(string ngType, int count)
        {
            var alertWindow = new Window
            {
                Title = "🚨 连续NG告警",
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true, // 始终置顶
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)) // 深色背景
            };

            // 创建主要内容
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 创建告警内容区域
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            // 告警图标和标题
            var alertIcon = new TextBlock
            {
                Text = "⚠️",
                FontSize = 60,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Orange),
                Margin = new Thickness(0, 0, 0, 20)
            };

            // 主要告警信息
            var alertTitle = new TextBlock
            {
                Text = "连续NG检测告警",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 15)
            };

            // 详细信息
            var alertDetails = new TextBlock
            {
                Text = $"检测到连续 {count} 次相同缺陷：\n\n\"{ngType}\"\n\n请检查生产流程或设备状态",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 20),
                LineHeight = 24
            };

            // 时间戳
            var timestamp = new TextBlock
            {
                Text = $"告警时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.LightGray),
                Margin = new Thickness(0, 0, 0, 10)
            };

            contentPanel.Children.Add(alertIcon);
            contentPanel.Children.Add(alertTitle);
            contentPanel.Children.Add(alertDetails);
            contentPanel.Children.Add(timestamp);

            Grid.SetRow(contentPanel, 0);
            mainGrid.Children.Add(contentPanel);

            // 确认按钮
            var confirmButton = new Button
            {
                Content = "我知道了",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(30, 10, 30, 10),
                Margin = new Thickness(0, 10, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)), // 红色背景
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            confirmButton.Click += (s, e) => alertWindow.Close();

            Grid.SetRow(confirmButton, 1);
            mainGrid.Children.Add(confirmButton);

            alertWindow.Content = mainGrid;

            return alertWindow;
        }

        /// <summary>
        /// 重置连续NG计数器（公共方法，可在必要时手动调用）
        /// </summary>
        public void ResetConsecutiveNGCounter()
        {
            _consecutiveNGCount = 0;
            _lastNGType = "";
            LogMessage("连续NG计数器已重置", LogLevel.Info);
        }

        /// <summary>
        /// 设置连续NG告警的调试模式（用于测试验证）
        /// </summary>
        /// <param name="enabled">true=图片测试时也启用告警，false=仅正常检测时启用</param>
        public static void SetDebugModeForConsecutiveNG(bool enabled)
        {
            _enableDebugModeForTesting = enabled;
            string mode = enabled ? "启用" : "禁用";
            Instance?.LogMessage($"连续NG告警调试模式已{mode}（图片测试时{(enabled ? "会" : "不会")}触发告警）", LogLevel.Info);
        }

        /// <summary>
        /// 获取当前连续NG检测状态（用于调试）
        /// </summary>
        /// <returns>状态信息字符串</returns>
        public string GetConsecutiveNGStatus()
        {
            string testMode = IsInTestImageMode() ? "图片测试" : "正常检测";
            string debugMode = _enableDebugModeForTesting ? "启用" : "禁用";
            return $"当前模式: {testMode} | 调试模式: {debugMode} | 上次NG: {(_lastNGType ?? "无")} | 连续次数: {_consecutiveNGCount}";
        }

        /// <summary>
        /// 手动触发连续NG测试（仅用于功能验证）
        /// </summary>
        /// <param name="ngType">NG类型</param>
        public void TestConsecutiveNGAlert(string ngType = "测试缺陷")
        {
            LogMessage($"手动测试连续NG告警，NG类型: {ngType}", LogLevel.Info);
            
            // 连续调用3次相同NG
            for (int i = 1; i <= 3; i++)
            {
                CheckConsecutiveNG(ngType);
                LogMessage($"模拟第{i}次连续NG: {ngType}", LogLevel.Info);
            }
        }

        // <summary>
        /// 更新UI上的统计数据和饼图
        /// </summary>
        private void UpdateDefectStatisticsUI(double yieldRate, string OK_OR_NG)
        {
            try
            {
                // 🔧 性能优化：使用后台优先级，减少阻塞主检测流程
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 更新统计数据显示 - 仅基础UI更新，最高优先级
                    if (PageManager.Page1Instance != null)
                    {
                        PageManager.Page1Instance.OK_num.Text = okCount.ToString();
                        var ngCount = totalCount - okCount;
                        PageManager.Page1Instance.Total_num.Text = totalCount.ToString();
                        PageManager.Page1Instance.NG_num.Text = ngCount.ToString();
                        PageManager.Page1Instance.yieldRate.Text = $"{yieldRate:F2}%";
                        PageManager.Page1Instance.OK_OR_NG.Text = OK_OR_NG;

                        if (OK_OR_NG == "OK")
                        {
                            PageManager.Page1Instance.OK_OR_NG.Background = Brushes.Green;
                        }
                        else
                        {
                            PageManager.Page1Instance.OK_OR_NG.Background = Brushes.Red;
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Send); // 高优先级，立即更新核心UI

                // 🔧 性能优化：使用低优先级更新饼图，避免阻塞核心UI
                try
                {
                    // 直接更新统计饼图显示（内部已有Dispatcher调度）
                    UpdatePieChart();
                }
                catch (Exception ex)
                {
                    LogManager.Warning($"更新统计饼图失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新统计UI时出错: {ex.Message}");
            }
        }
        private void UpdatePieChart()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 数据一致性检查：确保defectTypeCounter与实际统计数据一致
                    int defectCountFromCounter = defectTypeCounter.Where(kvp => kvp.Key != "良品").Sum(kvp => kvp.Value);
                    int actualNgCount = totalCount - okCount;
                    
                    // 如果数据不一致，记录警告并清空饼图
                    if (defectCountFromCounter != actualNgCount)
                    {
                        LogMessage($"数据不一致检测：饼图数据({defectCountFromCounter}) != 实际NG数({actualNgCount})，清空饼图", LogLevel.Warning);
                        PageManager.Page1Instance?.WpfPlot1.Plot.Clear();
                        PageManager.Page1Instance?.WpfPlot1.Refresh();
                        return;
                    }

                    // 过滤掉"良品"类型，只显示各类缺陷
                    var defectEntries = defectTypeCounter.Where(kvp => kvp.Key != "良品").ToList();

                    // 如果没有缺陷数据，清空饼图
                    if (defectEntries.Count == 0 || actualNgCount == 0)
                    {
                        PageManager.Page1Instance?.WpfPlot1.Plot.Clear();
                        PageManager.Page1Instance?.WpfPlot1.Refresh();
                        return;
                    }

                    // 创建缺陷类型的值数组和名称数组
                    double[] values = defectEntries.Select(kvp => (double)kvp.Value).ToArray();
                    string[] labels = defectEntries.Select(kvp => kvp.Key).ToArray();

                    // 获取Page1中的饼图控件
                    var wpfPlot = PageManager.Page1Instance.WpfPlot1;

                    // 🔧 性能优化：减少不必要的重建，仅在需要时清除
                    wpfPlot.Plot.Clear();

                    // 创建新的饼图
                    var pie = wpfPlot.Plot.Add.Pie(values);
                    // 调整饼图大小相关属性 - 适应压缩空间
                    pie.ExplodeFraction = 0.05; // 减小切片分离距离
                    pie.SliceLabelDistance = 0.3; // 将标签放置更靠近饼图中心

                    // 计算总缺陷数量（不包括良品）
                    double total = values.Sum();
                    wpfPlot.Plot.Font.Automatic();

                    // 设置每个饼图切片的标签和图例
                    for (int i = 0; i < pie.Slices.Count; i++)
                    {
                        pie.Slices[i].LabelFontSize = 16; // 调整字体大小为原来的2/3
                        pie.Slices[i].Label = $"{values[i]}"; // 显示数量
                        pie.Slices[i].LabelFontColor = ScottPlot.Color.FromHex("#FFFFFF"); // 白色标签

                        // 确保索引在有效范围内
                        if (i < labels.Length)
                        {
                            // 显示类型名称、数量和百分比
                            pie.Slices[i].LegendText = $"{labels[i]}: {values[i]} ({values[i] / total:p1})";
                        }
                    }

                    // 应用与Page1初始化中相同的设置
                    wpfPlot.Plot.Axes.Frameless();
                    wpfPlot.Plot.HideGrid();
                    wpfPlot.Plot.Legend.FontSize = 16; // 适应压缩空间的图例字体大小
                    wpfPlot.Plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#C8000000"); // 半透明黑色背景
                    
                    // 设置饼图轴限制，使饼图居左显示
                    wpfPlot.Plot.Axes.SetLimitsX(0.5, 3);
                    wpfPlot.Plot.Axes.SetLimitsY(-1.5, 1.5);

                    // 🔧 性能优化：使用低优先级刷新，避免阻塞主UI线程
                    wpfPlot.Refresh();

                }), System.Windows.Threading.DispatcherPriority.Background); // 使用后台优先级
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"更新饼图时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 从VM全局变量读取数据，转换为指定单位并更新DataGrid1
        /// </summary>
        public void ReadMeasurementDataFromVM()
        {
            try
            {
                // 按照官方示例模式，重新获取"校准"流程实例
                var calibrationProcedure = VmSolution.Instance["校准"] as VmProcedure;
                if (calibrationProcedure == null)
                {
                    LogMessage("无法获取'校准'流程实例，无法读取测量数据", LogLevel.Error);
                    return;
                }


                // IMVSBlobFindModuCs.BlobFindParam blobFindModule =
                //     (IMVSBlobFindModuCs.BlobFindParam)calibrationProcedure["主振瑕疵"];
                // var Area = blobFindModule.ModuRoiManager.
                // //输出Area[0]
                // MessageBox.Show(Area[0].ToString());



                string outTable = "";
                string outTable0 = "";
                string outTable1 = "";
                
                // 尝试获取"out"输出
                try
                {
                    outTable = calibrationProcedure.ModuResult.GetOutputString("out").astStringVal[0].strValue;
                }
                catch (Exception ex)
                {
                    LogMessage($"获取out输出失败: {ex.Message}", LogLevel.Warning);
                }
                
                // 尝试获取"out0"输出
                try
                {
                    outTable0 = calibrationProcedure.ModuResult.GetOutputString("out0").astStringVal[0].strValue;
                }
                catch (Exception ex)
                {
                    LogMessage($"获取out0输出失败: {ex.Message}", LogLevel.Warning);
                }
                
                // 如果是双涂布模板，还需读取"out1"输出
                if (currentCoatingType == CoatingType.Double)
                {
                    try
                    {
                        outTable1 = calibrationProcedure.ModuResult.GetOutputString("out1").astStringVal[0].strValue;
                        LogMessage($"双涂布模板已获取out1输出数据", LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"获取out1输出失败: {ex.Message}", LogLevel.Warning);
                    }
                }
                
                // 合并输出数据
                string combinedData = "";
                List<string> validOutputs = new List<string>();
                
                if (!string.IsNullOrWhiteSpace(outTable))
                    validOutputs.Add(outTable);
                if (!string.IsNullOrWhiteSpace(outTable0))
                    validOutputs.Add(outTable0);
                if (!string.IsNullOrWhiteSpace(outTable1))
                    validOutputs.Add(outTable1);
                
                if (validOutputs.Count > 0)
                {
                    combinedData = string.Join(";", validOutputs);
                }
                
                if (!string.IsNullOrWhiteSpace(combinedData))
                {
                    // 解析数据并更新DataGrid
                    UpdateDataGridWithMeasurements(combinedData);
                }
                else
                {
                    LogMessage("没有获取到任何有效的输出数据", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"读取测量数据失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 将像素(pixel)转换为指定的长度单位
        /// </summary>
        /// <param name="pixels">像素值</param>
        /// <param name="targetUnit">目标单位(um, mm, cm等)</param>
        /// <returns>转换后的值</returns>
        private double PixelToUnit(double pixels, string targetUnit)
        {
            // 像素尺寸定义(默认4微米/像素)
            double pixelSize = 4.0; // 微米/像素

            switch (targetUnit.ToLower())
            {
                case "um":
                    return pixels * pixelSize;
                case "mm":
                    return pixels * pixelSize / 1000.0;
                case "cm":
                    return pixels * pixelSize / 10000.0;
                case "pixel":
                    return pixels; // 不转换
                case "deg":
                    return pixels; // 不转换
                default:
                    LogMessage($"未知的单位类型: {targetUnit}，将使用默认转换(微米)", LogLevel.Warning);
                    return pixels * pixelSize; // 默认返回微米
            }
        }

        /// <summary>
        /// 将像素面积转换为实际面积单位
        /// </summary>
        /// <param name="pixelArea">像素面积</param>
        /// <param name="targetUnit">目标单位(um², mm²等)</param>
        /// <returns>转换后的值</returns>
        private double PixelToArea(double pixelArea, string targetUnit)
        {
            // 像素尺寸定义(默认4微米/像素)
            double pixelSize = 4.0; // 微米/像素

            switch (targetUnit.ToLower())
            {
                case "um²":
                    return pixelArea * pixelSize * pixelSize;
                case "mm²":
                    return pixelArea * pixelSize * pixelSize / 1000000.0;
                case "pixel²":
                    return pixelArea; // 不转换
                default:
                    LogMessage($"未知的面积单位类型: {targetUnit}，将使用默认转换(平方微米)", LogLevel.Warning);
                    return pixelArea * pixelSize * pixelSize; // 默认返回平方微米
            }
        }

        /// <summary>
        /// 设置超出范围的行为红色背景，空值行为黄色背景
        /// </summary>
        /// <param name="dataGrid">DataGrid控件</param>
        /// <param name="items">数据项列表</param>
        private void SetOutOfRangeRowsColor(DataGrid dataGrid, IList<DetectionItem> items)
        {
            try
            {
                // 使用 Dispatcher 确保在UI线程中执行，并延迟执行以确保DataGrid已完全加载
                dataGrid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            var item = items[i];
                            var row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(i);
                            
                            if (row != null)
                            {
                                // 检查值是否为空（null、空字符串或仅包含空白字符）
                                bool isEmpty = string.IsNullOrWhiteSpace(item.Value);
                                
                                if (isEmpty)
                                {
                                    // 设置为黄色背景（空值）
                                    row.Background = new SolidColorBrush(Colors.LightYellow);
                                }
                                else if (item.IsOutOfRange)
                                {
                                    // 设置为红色背景（超出范围）
                                    row.Background = new SolidColorBrush(Colors.LightCoral);
                                    // 移除过于频繁的日志输出，避免干扰用户
                                    // LogMessage($"行 {item.RowNumber} ({item.Name}) 超出范围，设置为红色背景", LogLevel.Info);
                                }
                                else
                                {
                                    // 恢复为默认背景（正常值）
                                    row.Background = new SolidColorBrush(Colors.White);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"设置行背景色时出错: {ex.Message}", LogLevel.Error);
                    }
                }), DispatcherPriority.Background);
                
                // 如果行还没有生成，添加LoadingRow事件处理
                dataGrid.LoadingRow -= DataGrid_LoadingRow; // 先移除之前的事件处理
                dataGrid.LoadingRow += DataGrid_LoadingRow;
            }
            catch (Exception ex)
            {
                LogMessage($"设置行颜色失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// DataGrid行加载事件处理（用于动态设置行背景色）
        /// </summary>
        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                var item = e.Row.DataContext as DetectionItem;
                if (item != null)
                {
                    // 检查值是否为空（null、空字符串或仅包含空白字符）
                    bool isEmpty = string.IsNullOrWhiteSpace(item.Value);
                    
                    if (isEmpty)
                    {
                        // 设置为黄色背景（空值）
                        e.Row.Background = new SolidColorBrush(Colors.LightYellow);
                    }
                    else if (item.IsOutOfRange)
                    {
                        // 设置为红色背景（超出范围）
                        e.Row.Background = new SolidColorBrush(Colors.LightCoral);
                    }
                    else
                    {
                        // 设置为默认背景（正常值）
                        e.Row.Background = new SolidColorBrush(Colors.White);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"DataGrid行加载事件处理出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 检查数值是否超出范围
        /// </summary>
        /// <param name="value">数值</param>
        /// <param name="lowerLimit">下限</param>
        /// <param name="upperLimit">上限</param>
        /// <returns>true表示超出范围，false表示在范围内</returns>
        private bool CheckValueOutOfRange(string value, string lowerLimit, string upperLimit)
        {
            try
            {
                // 如果数值、下限或上限为空，则认为在范围内
                if (string.IsNullOrWhiteSpace(value) || 
                    string.IsNullOrWhiteSpace(lowerLimit) || 
                    string.IsNullOrWhiteSpace(upperLimit))
                {
                    return false;
                }

                // 尝试解析为数字
                if (double.TryParse(value, out double numValue) &&
                    double.TryParse(lowerLimit, out double numLower) &&
                    double.TryParse(upperLimit, out double numUpper))
                {
                    // 判断是否在范围内：下限 <= 数值 <= 上限
                    bool isInRange = numValue >= numLower && numValue <= numUpper;
                    return !isInRange; // 返回是否超出范围
                }
                
                // 如果无法解析为数字，则认为在范围内
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"判断数值范围时出错: {ex.Message}", LogLevel.Warning);
                return false; // 出错时默认认为在范围内
            }
        }

        /// <summary>
        /// 更新DataGrid中的测量结果
        /// </summary>
        /// <param name="outTable">输出表格数据（格式：项目名1:数值1,下限1,上限1;项目名2:数值2,下限2,上限2；项目名n:数值n,下限n,上限n）</param>
        private void UpdateDataGridWithMeasurements(string outTable)
        {
            // 开始处理2D检测数据
            
            try
            {
                // 检查输入数据
                if (string.IsNullOrWhiteSpace(outTable))
                {
                    LogMessage("输出表格数据为空", LogLevel.Warning);
                    return;
                }

                // 🔧 重要修复：VM回调中不使用异步操作，但UI访问使用同步Dispatcher.Invoke
                Page1 page1 = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 从主窗口获取Page1的实例
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    if (mainWindow?.frame1?.Content != null)
                    {
                        page1 = mainWindow.frame1.Content as Page1;
                    }
                });

                if (page1 == null)
                {
                    LogMessage("无法获取Page1实例", LogLevel.Error);
                    return;
                }

                // 解析输出表格数据：项目名1:数值1,下限1,上限1;项目名2:数值2,下限2,上限2；项目名n:数值n,下限n,上限n
                var pairs = outTable.Split(';', '；') // 支持中文和英文分号
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                // 准备2D检测项目列表
                var twoDItems = new List<DetectionItem>();
                var detectionItems = new Dictionary<string, DetectionItemValue>();

                // 新样本开始，行号从1开始
                int rowNumber = 1;
                
                foreach (var pair in pairs)
                {
                    try
                    {
                        var parts = pair.Split(':');
                        if (parts.Length == 2)
                        {
                            string itemName = parts[0].Trim();
                            string rightPart = parts[1].Trim();

                            // 解析数值,下限,上限格式
                            var valueParts = rightPart.Split(',');
                            
                            string itemValue = "";
                            string lowerLimit = "";
                            string upperLimit = "";
                            
                            if (valueParts.Length >= 1)
                                itemValue = valueParts[0].Trim();
                            if (valueParts.Length >= 2)
                                lowerLimit = valueParts[1].Trim();
                            if (valueParts.Length >= 3)
                                upperLimit = valueParts[2].Trim();

                            // 判断数值是否在范围内
                            bool isOutOfRange = CheckValueOutOfRange(itemValue, lowerLimit, upperLimit);

                            // 创建新的检测项目（用于DataGrid显示）
                            var detectionItem = new DetectionItem
                            {
                                RowNumber = rowNumber++,
                                Name = itemName,
                                Value = itemValue,
                                LowerLimit = lowerLimit,
                                UpperLimit = upperLimit,
                                IsOutOfRange = isOutOfRange,
                                Is3DItem = false, // 通过VM流程输出的项目标记为非3D项目
                                ToolIndex = -1    // 使用-1标识VM流程输出的项目
                            };

                            twoDItems.Add(detectionItem);

                            // 同时准备数据分析存储的数据（重要：在这里直接记录原始数据）
                            double numericValue = 0;
                            bool isNumeric = double.TryParse(itemValue, out numericValue);
                            
                            double lowerLimitNum = 0;
                            double upperLimitNum = 0;
                            double.TryParse(lowerLimit, out lowerLimitNum);
                            double.TryParse(upperLimit, out upperLimitNum);

                            // 判断是否有有效数据：只有当数值不为空且能转换为数字时才认为有效
                            bool hasValidData = !string.IsNullOrWhiteSpace(itemValue) && isNumeric;

                            detectionItems[itemName] = new DetectionItemValue
                            {
                                Value = hasValidData ? numericValue : 0, // 0作为默认值，但通过HasValidData标识区分
                                StringValue = itemValue,
                                HasValidData = hasValidData, // 关键：区分空数据和0值
                                LowerLimit = lowerLimitNum,
                                UpperLimit = upperLimitNum,
                                IsOutOfRange = hasValidData ? isOutOfRange : false, // 空数据不算超限
                                Is3DItem = false
                            };
                        }
                        else
                        {
                            LogMessage($"无法解析项目数据: {pair}（格式应为 项目名:数值,下限,上限）", LogLevel.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"处理项目数据时出错: {pair}, 错误: {ex.Message}", LogLevel.Error);
                    }
                }

                // 🔧 关键修复：只缓存2D数据，设置2D完成标志，UI操作使用同步调用
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // 缓存2D数据到Page1
                        page1.SetCached2DItems(twoDItems);
                        
                        // 设置2D完成标志（不主动调用管理器，让管理器主动监控）
                        // 这里只设置状态，不触发任何回调
                        //LogManager.Info($"已缓存{twoDItems.Count}个2D检测项目，已设置2D完成标志", "2D数据处理");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"设置2D缓存失败: {ex.Message}", "2D数据处理");
                    }
                });

                // 🔧 重要修改：不再直接记录到DetectionDataStorage，改为统一在ExecuteUnifiedJudgementAndIO中处理
                // 这样可以确保2D和3D数据在同一行记录，避免分行问题
                LogManager.Info($"2D数据已缓存，等待3D数据完成后统一记录到CSV", "2D数据处理");
            }
            catch (Exception ex)
            {
                LogMessage($"缓存2D数据失败: {ex.Message}", LogLevel.Error);
            }
        }



        private void BrowseMatchTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建打开文件对话框
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "高精度匹配模板|*.hpmxml|所有文件|*.*",
                    Title = "选择高精度匹配模板文件"
                };

                // 显示对话框并获取结果
                if (dialog.ShowDialog() == true)
                {
                    // 获取所选模板文件的完整路径
                    string templatePath = dialog.FileName;

                    // 如果当前是PKG位置匹配步骤，更新输入框中的模板路径
                    if (stepConfigurations[currentStep].StepType == StepType.PkgMatching && 
                        inputParameterControls.ContainsKey(currentStep) &&
                        inputParameterControls[currentStep].ContainsKey("匹配模板路径"))
                    {
                        inputParameterControls[currentStep]["匹配模板路径"].Text = templatePath;

                        // 保存参数
                        SaveStepParameters(currentStep);

                        // 立即应用选中的模板文件
                        try
                        {
                            var matchTool = (IMVSHPFeatureMatchModuCs.IMVSHPFeatureMatchModuTool)VmSolution.Instance["校准.PKG匹配"];
                            if (matchTool != null && File.Exists(templatePath))
                            {
                                matchTool.ImportModelData(new string[] { templatePath });
                                PageManager.Page1Instance?.LogUpdate($"已设置匹配模板路径: {templatePath}");
                            }
                            else if (!File.Exists(templatePath))
                            {
                                MessageBox.Show($"模板文件不存在: {templatePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                                MessageBox.Show($"设置匹配模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    // 如果当前是镀膜PKG位置匹配步骤
                    else if (stepConfigurations[currentStep].StepType == StepType.CoatingPkgMatching &&
                             inputParameterControls.ContainsKey(currentStep) &&
                             inputParameterControls[currentStep].ContainsKey("镀膜PKG模板路径"))
                    {
                        inputParameterControls[currentStep]["镀膜PKG模板路径"].Text = templatePath;

                        // 保存参数
                        SaveStepParameters(currentStep);

                        // 立即应用选中的模板文件
                        try
                        {
                            var matchTool = (IMVSHPFeatureMatchModuCs.IMVSHPFeatureMatchModuTool)VmSolution.Instance["校准.异图PKG匹配"];
                            if (matchTool != null && File.Exists(templatePath))
                            {
                                matchTool.ImportModelData(new string[] { templatePath });
                                PageManager.Page1Instance?.LogUpdate($"已设置镀膜PKG模板路径: {templatePath}");
                            }
                            else if (!File.Exists(templatePath))
                            {
                                MessageBox.Show($"模板文件不存在: {templatePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"设置镀膜PKG模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择模板文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 将PKG位置匹配的模板路径沿用到镀膜PKG位置匹配
        /// </summary>
        private void UsePkgTemplateForCoating_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string templatePath = string.Empty;
                int pkgIndex = stepConfigurations.FindIndex(s => s.StepType == StepType.PkgMatching);
                if (pkgIndex >= 0)
                {
                    if (inputParameterControls.ContainsKey(pkgIndex) &&
                        inputParameterControls[pkgIndex].ContainsKey("匹配模板路径"))
                    {
                        templatePath = inputParameterControls[pkgIndex]["匹配模板路径"].Text;
                    }

                    if (string.IsNullOrWhiteSpace(templatePath) &&
                        currentTemplate.InputParameters.ContainsKey(StepType.PkgMatching) &&
                        currentTemplate.InputParameters[StepType.PkgMatching].ContainsKey("匹配模板路径"))
                    {
                        templatePath = currentTemplate.InputParameters[StepType.PkgMatching]["匹配模板路径"];
                    }
                }

                if (string.IsNullOrWhiteSpace(templatePath))
                {
                    MessageBox.Show("未找到PKG位置匹配的模板路径，请先在PKG位置匹配步骤中配置模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (inputParameterControls.ContainsKey(currentStep) &&
                    inputParameterControls[currentStep].ContainsKey("镀膜PKG模板路径"))
                {
                    inputParameterControls[currentStep]["镀膜PKG模板路径"].Text = templatePath;
                    SaveStepParameters(currentStep);
                    PageManager.Page1Instance?.LogUpdate($"已沿用PKG模板路径到镀膜PKG位置匹配: {templatePath}");
                }
                else
                {
                    MessageBox.Show("当前步骤未找到镀膜PKG模板路径输入框，无法沿用PKG模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"沿用PKG模板路径失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 浏览BLK匹配模板文件的事件处理方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BrowseBlkMatchTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建打开文件对话框
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "高精度匹配模板|*.hpmxml|所有文件|*.*",
                    Title = "选择BLK高精度匹配模板文件"
                };

                // 显示对话框并获取结果
                if (dialog.ShowDialog() == true)
                {
                    // 获取所选模板文件的完整路径
                    string templatePath = dialog.FileName;

                    // 如果当前是BLK位置匹配步骤，更新输入框中的模板路径
                    if (stepConfigurations[currentStep].StepType == StepType.BlkMatching && 
                        inputParameterControls.ContainsKey(currentStep) &&
                        inputParameterControls[currentStep].ContainsKey("BLK匹配模板路径"))
                    {
                        inputParameterControls[currentStep]["BLK匹配模板路径"].Text = templatePath;

                        // 保存参数
                        SaveStepParameters(currentStep);

                        // 立即应用选中的模板文件
                        try
                        {
                            var matchTool = (IMVSHPFeatureMatchModuCs.IMVSHPFeatureMatchModuTool)VmSolution.Instance["校准.BLK匹配"];
                            if (matchTool != null && File.Exists(templatePath))
                            {
                                matchTool.ImportModelData(new string[] { templatePath });
                                PageManager.Page1Instance?.LogUpdate($"已设置BLK匹配模板路径: {templatePath}");
                            }
                            else if (!File.Exists(templatePath))
                            {
                                MessageBox.Show($"模板文件不存在: {templatePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"设置BLK匹配模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择BLK模板文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 浏览镀膜匹配模板文件的事件处理方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BrowseCoatingMatchTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建打开文件对话框
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "高精度匹配模板|*.hpmxml|所有文件|*.*",
                    Title = "选择镀膜高精度匹配模板文件"
                };

                // 显示对话框并获取结果
                if (dialog.ShowDialog() == true)
                {
                    // 获取所选模板文件的完整路径
                    string templatePath = dialog.FileName;

                    // 如果当前是镀膜匹配步骤，更新输入框中的模板路径
                    if (stepConfigurations[currentStep].StepType == StepType.CoatingMatching && 
                        inputParameterControls.ContainsKey(currentStep) &&
                        inputParameterControls[currentStep].ContainsKey("镀膜匹配模板路径"))
                    {
                        inputParameterControls[currentStep]["镀膜匹配模板路径"].Text = templatePath;

                        // 保存参数
                        SaveStepParameters(currentStep);

                        // 立即应用选中的模板文件
                        try
                        {
                            var matchTool = (IMVSHPFeatureMatchModuCs.IMVSHPFeatureMatchModuTool)VmSolution.Instance["校准.纯镀膜匹配"];
                            if (matchTool != null && File.Exists(templatePath))
                            {
                                matchTool.ImportModelData(new string[] { templatePath });
                                PageManager.Page1Instance?.LogUpdate($"已设置镀膜匹配模板路径: {templatePath}");
                            }
                            else if (!File.Exists(templatePath))
                            {
                                MessageBox.Show($"模板文件不存在: {templatePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"设置镀膜匹配模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择镀膜模板文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加按钮的辅助方法
        private void AddButton(string text, RoutedEventHandler clickHandler)
        {
            Button button = new Button
            {
                Content = text,
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Colors.Blue),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            button.Click += clickHandler;

            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            panel.Children.Add(button);
            InputParametersPanel.Children.Add(panel);
        }



        /// <summary>
        /// 日志级别枚举
        /// </summary>
        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 记录日志消息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="level">日志级别</param>
        private void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            // 使用Dispatcher确保在UI线程上执行
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 获取时间戳
                    string timestamp = DateTime.Now.ToString("yy-MM-dd HH:mm:ss.fff");

                    // 根据日志级别添加前缀
                    string prefix;
                    switch (level)
                    {
                        case LogLevel.Info:
                            prefix = "[信息] ";
                            break;
                        case LogLevel.Warning:
                            prefix = "[警告] ";
                            break;
                        case LogLevel.Error:
                            prefix = "[错误] ";
                            break;
                        default:
                            prefix = "";
                            break;
                    }

                    // 构建完整消息
                    string fullMessage = $"{prefix}{message}";

                    // 记录到日志文件
                    WriteToLogFile(timestamp, fullMessage, level);

                    // 根据日志级别确定是否显示消息框
                    if (level == LogLevel.Error)
                    {
                        MessageBox.Show(fullMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (level == LogLevel.Warning && IsVerboseLogging)
                    {
                        // 只在详细日志模式下显示警告消息框
                        MessageBox.Show(fullMessage, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    // 可选：控制台输出
                    Console.WriteLine($"[{timestamp}] {fullMessage}");
                }
                catch (Exception ex)
                {
                    // 如果连日志记录都失败了，至少显示一个消息框
                    MessageBox.Show($"记录日志失败: {ex.Message}\n原始消息: {message}",
                                   "日志错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }));
        }

        /// <summary>
        /// 是否启用详细日志(包括警告消息框等)
        /// </summary>
        private bool IsVerboseLogging => false;

        /// <summary>
        /// 将日志写入文件
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        /// <param name="message">消息内容</param>
        /// <param name="level">日志级别</param>
        private void WriteToLogFile(string timestamp, string message, LogLevel level)
        {
            try
            {
                // 创建日志目录
                string logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                // 按日期创建日志文件
                string logFile = System.IO.Path.Combine(logDir, $"{DateTime.Now:yyyy-MM-dd}.log");

                // 构建日志行
                string logLine = $"[{timestamp}] [{level}] {message}";

                // 异步写入日志文件
                Task.Run(() =>
                {
                    try
                    {
                        using (StreamWriter writer = File.AppendText(logFile))
                        {
                            writer.WriteLine(logLine);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果文件写入失败，只能尝试输出到控制台
                        Console.WriteLine($"写入日志文件失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                // 只能尝试输出到控制台
                Console.WriteLine($"准备写入日志文件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 测量变量配置类
        /// </summary>
        private class MeasurementVariableConfig
        {
            /// <summary>
            /// 全局变量名称
            /// </summary>
            public string VariableName { get; set; }

            /// <summary>
            /// 对应DataGrid中的行索引
            /// </summary>
            public int RowIndex { get; set; }

            /// <summary>
            /// 数据格式化模式
            /// </summary>
            public string FormatPattern { get; set; }

            /// <summary>
            /// 单位转换函数
            /// </summary>
            public Func<double, string, double> ConversionFunc { get; set; }

            /// <summary>
            /// 目标单位
            /// </summary>
            public string TargetUnit { get; set; }

            /// <summary>
            /// 在格式化字符串中的位置
            /// </summary>
            public int Position { get; set; }
        }

        /// <summary>
        /// 测量结果类
        /// </summary>
        private class MeasurementResult
        {
            /// <summary>
            /// 原始数值（转换前）
            /// </summary>
            public double OriginalValue { get; set; }

            /// <summary>
            /// 转换后的数值
            /// </summary>
            public double Value { get; set; }

            /// <summary>
            /// 单位
            /// </summary>
            public string Unit { get; set; }

            /// <summary>
            /// 格式化模式
            /// </summary>
            public string FormatPattern { get; set; }

            /// <summary>
            /// 值是否无效
            /// </summary>
            public bool IsInvalid { get; set; }
        }

        /// <summary>
        /// 清空所有统计数据
        /// </summary>
        public void ClearStatistics()
        {
            try
            {
                // 使用静态管理器清空统计数据
                StatisticsManager.ClearAll();
                
                // 重置连续NG检测状态
                ResetConsecutiveNGCounter();

                // 重置性能监控
                //ResetPerformanceMonitor();

                // 强制同步清空Page1的UI显示数据
                if (PageManager.Page1Instance != null)
                {
                    PageManager.Page1Instance.ClearUIDisplayData();
                }

                // 仅记录到日志文件
                LogMessage("统计数据已清空（包括缺陷类型计数器和性能监控）", LogLevel.Info);
            }
            catch (Exception ex)
            {
                string errorMessage = $"清空统计数据时出错: {ex.Message}";
                LogMessage(errorMessage, LogLevel.Error);
                PageManager.Page1Instance?.LogUpdate(errorMessage);
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取当前NG数量（供外部调用）
        /// </summary>
        public int GetCurrentNGCount()
        {
            try
            {
                return defectTypeCounter.Where(kvp => kvp.Key != "良品").Sum(kvp => kvp.Value);
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取当前NG数量失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 重置性能监控系统（仅记录到日志文件）
        /// 注意：不重置检测计数器，保持连续计数
        /// </summary>
        /// <summary>
        /// 重置简洁性能监控计数器
        /// </summary>
        public static void ResetPerformanceMonitor()
        {
            lock (_performanceLock)
            {
                _detectionCount = 0;
                _isDetectionRunning = false;
                _simpleTimer.Reset();
                Instance?.LogMessage("🔄 性能监控计数器已重置", LogLevel.Info);
            }
        }

        /// <summary>
        /// 完全重置简洁性能监控系统
        /// </summary>
        public static void FullResetPerformanceMonitor()
        {
            lock (_performanceLock)
            {
                _detectionCount = 0;
                _isDetectionRunning = false;
                _simpleTimer.Reset();
                Instance?.LogMessage("🔄 性能监控系统已完全重置", LogLevel.Info);
            }
        }

        /// <summary>
        /// 初始化步骤分组配置
        /// </summary>
        private void InitializeStepGroups()
        {
            // 先清理旧的按钮缓存（解绑事件，防止内存泄漏）
            ClearButtonCaches();

            stepGroups.Clear();

            // 清空按钮结构缓存，强制重新生成
            lastButtonStructure = "";
            
            // 查找PKG相关步骤的索引
            var pkgStepIndices = new List<int>();
            for (int i = 0; i < stepConfigurations.Count; i++)
            {
                var stepType = stepConfigurations[i].StepType;
                if (stepType == StepType.PkgEnhance ||
                    stepType == StepType.PkgMatching ||
                    stepType == StepType.PkgAngleMeasure ||
                    stepType == StepType.PkgEdgeSize)
                {
                    pkgStepIndices.Add(i);
                }
            }
            
            // 如果找到了PKG相关步骤，创建PKG定位组
            if (pkgStepIndices.Count > 0)
            {
                var pkgGroup = new StepGroup
                {
                    GroupName = "PKG定位",
                    StepIndices = pkgStepIndices,
                    IsExpanded = false
                };
                stepGroups.Add(pkgGroup);
            }

            // 镀膜图PKG定位分组
            var coatingPkgStepIndices = new List<int>();
            for (int i = 0; i < stepConfigurations.Count; i++)
            {
                var stepType = stepConfigurations[i].StepType;
                if (stepType == StepType.CoatingPkgEnhance ||
                    stepType == StepType.CoatingPkgMatching ||
                    stepType == StepType.CoatingPkgAngleMeasure)
                {
                    coatingPkgStepIndices.Add(i);
                }
            }

            if (coatingPkgStepIndices.Count > 0)
            {
                var coatingPkgGroup = new StepGroup
                {
                    GroupName = "镀膜图PKG定位",
                    StepIndices = coatingPkgStepIndices,
                    IsExpanded = false
                };
                stepGroups.Add(coatingPkgGroup);
            }
        }

        /// <summary>
        /// 添加步骤组（公开方法，用于动态添加步骤组）
        /// </summary>
        /// <param name="groupName">组名称</param>
        /// <param name="stepIndices">步骤索引列表</param>
        public void AddStepGroup(string groupName, List<int> stepIndices)
        {
            var group = new StepGroup
            {
                GroupName = groupName,
                StepIndices = new List<int>(stepIndices),
                IsExpanded = false
            };
            stepGroups.Add(group);
            
            // 重置结构缓存，强制重新生成面板
            lastButtonStructure = "";
            RefreshStepButtons();
        }

        /// <summary>
        /// 获取指定步骤所属的步骤组
        /// </summary>
        /// <param name="stepIndex">步骤索引</param>
        /// <returns>所属步骤组，如果不属于任何组则返回null</returns>
        private StepGroup GetStepGroup(int stepIndex)
        {
            return stepGroups.FirstOrDefault(group => group.ContainsStep(stepIndex));
        }

        /// <summary>
        /// 判断当前步骤是否应该展开某个步骤组
        /// </summary>
        /// <param name="group">步骤组</param>
        /// <returns>是否应该展开</returns>
        private bool ShouldExpandGroup(StepGroup group)
        {
            // 如果当前步骤在这个组内，则展开
            return group.ContainsStep(currentStep);
        }

        /// <summary>
        /// 动态生成步骤按钮
        /// </summary>
        private void GenerateStepButtons()
        {
            try
            {
                // 获取步骤按钮容器
                var buttonPanel = this.FindName("StepButtonsPanel") as StackPanel;
                if (buttonPanel == null)
                {
                    PageManager.Page1Instance?.LogUpdate("未找到步骤按钮容器");
                    return;
                }

                // 更新步骤组的展开状态
                UpdateGroupExpandStates();

                // 生成当前应显示的按钮结构字符串
                string currentStructure = GetCurrentButtonStructure();
                
                // 如果结构发生变化，才重新生成整个面板
                if (currentStructure != lastButtonStructure)
                {
                    // 清空现有按钮和缓存
                    buttonPanel.Children.Clear();
                    ClearButtonCaches();
                    
                    // 生成按钮（支持分组）
                    GenerateButtonsWithGroups(buttonPanel);
                    
                    // 更新结构记录
                    lastButtonStructure = currentStructure;
                }
                else
                {
                    // 结构未变化，只更新按钮状态
                    UpdateButtonStatesOnly();
                }

            }
            catch (Exception ex)
            {
                string errorMessage = $"生成步骤按钮失败: {ex.Message}";
                PageManager.Page1Instance?.LogUpdate(errorMessage);
                MessageBox.Show(errorMessage, "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取当前应显示的按钮结构字符串
        /// </summary>
        /// <returns>结构字符串</returns>
        private string GetCurrentButtonStructure()
        {
            var structure = new List<string>();
            var processedSteps = new HashSet<int>();
            
            for (int i = 0; i < stepConfigurations.Count; i++)
            {
                if (processedSteps.Contains(i))
                    continue;

                var group = GetStepGroup(i);
                
                if (group != null)
                {
                    if (group.IsExpanded)
                    {
                        // 展开状态：记录组内所有步骤
                        var sortedIndices = group.StepIndices.OrderBy(x => x).ToList();
                        foreach (var stepIndex in sortedIndices)
                        {
                            structure.Add($"Step_{stepIndex}");
                            processedSteps.Add(stepIndex);
                        }
                    }
                    else
                    {
                        // 收缩状态：记录组
                        structure.Add($"Group_{group.GroupName}");
                        foreach (var stepIndex in group.StepIndices)
                        {
                            processedSteps.Add(stepIndex);
                        }
                    }
                }
                else
                {
                    structure.Add($"Step_{i}");
                    processedSteps.Add(i);
                }
            }
            
            return string.Join("|", structure);
        }

        /// <summary>
        /// 清空按钮缓存（在清除前先解绑所有事件，防止内存泄漏）
        /// </summary>
        private void ClearButtonCaches()
        {
            // 解绑步骤按钮的事件处理器
            foreach (var kvp in stepButtonCache)
            {
                var button = kvp.Value;
                button.Click -= ConfigStepButton_Click;
                button.MouseEnter -= StepButton_MouseEnter;
                button.MouseLeave -= StepButton_MouseLeave;
            }
            stepButtonCache.Clear();

            // 解绑组按钮的事件处理器
            foreach (var kvp in groupButtonCache)
            {
                var button = kvp.Value;
                button.Click -= GroupButton_Click;
                button.MouseEnter -= StepButton_MouseEnter;
                button.MouseLeave -= StepButton_MouseLeave;
            }
            groupButtonCache.Clear();
        }

        /// <summary>
        /// 只更新按钮状态，不重新生成按钮
        /// </summary>
        private void UpdateButtonStatesOnly()
        {
            // 更新普通步骤按钮状态
            foreach (var kvp in stepButtonCache)
            {
                var stepIndex = kvp.Key;
                var button = kvp.Value;
                UpdateSingleStepButtonState(button, stepIndex);
            }
            
            // 更新组按钮状态
            foreach (var kvp in groupButtonCache)
            {
                var group = kvp.Key;
                var button = kvp.Value;
                SetGroupButtonStyle(button, group);
            }
        }

        /// <summary>
        /// 更新单个步骤按钮的状态
        /// </summary>
        /// <param name="button">按钮</param>
        /// <param name="stepIndex">步骤索引</param>
        private void UpdateSingleStepButtonState(Button button, int stepIndex)
        {
            if (stepIndex == currentStep)
            {
                // 当前步骤：高亮显示
                button.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // 绿色
                button.Foreground = new SolidColorBrush(Colors.White);
            }
            else if (stepIndex < currentStep)
            {
                // 已完成步骤：蓝色
                button.Background = new SolidColorBrush(Color.FromRgb(0, 123, 255)); // 蓝色
                button.Foreground = new SolidColorBrush(Colors.White);
            }
            else
            {
                // 未开始步骤：灰色
                button.Background = new SolidColorBrush(Color.FromRgb(108, 117, 125)); // 灰色
                button.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        /// <summary>
        /// 更新步骤组的展开状态
        /// </summary>
        private void UpdateGroupExpandStates()
        {
            foreach (var group in stepGroups)
            {
                group.IsExpanded = ShouldExpandGroup(group);
            }
        }

        /// <summary>
        /// 生成带分组支持的按钮
        /// </summary>
        /// <param name="buttonPanel">按钮面板</param>
        private void GenerateButtonsWithGroups(StackPanel buttonPanel)
        {
            var processedSteps = new HashSet<int>();
            
            for (int i = 0; i < stepConfigurations.Count; i++)
            {
                if (processedSteps.Contains(i))
                    continue;

                var group = GetStepGroup(i);
                
                if (group != null)
                {
                    // 处理步骤组
                    if (group.IsExpanded)
                    {
                        // 展开状态：显示组内的所有步骤
                        GenerateExpandedGroupButtons(buttonPanel, group, processedSteps);
                    }
                    else
                    {
                        // 收缩状态：只显示组按钮
                        GenerateCollapsedGroupButton(buttonPanel, group, i);
                        // 标记组内所有步骤为已处理
                        foreach (var stepIndex in group.StepIndices)
                        {
                            processedSteps.Add(stepIndex);
                        }
                    }
                }
                else
                {
                    // 普通步骤，直接生成按钮
                    GenerateSingleStepButton(buttonPanel, i, i < stepConfigurations.Count - 1);
                    processedSteps.Add(i);
                }
            }
        }

        /// <summary>
        /// 生成展开状态的组内按钮
        /// </summary>
        /// <param name="buttonPanel">按钮面板</param>
        /// <param name="group">步骤组</param>
        /// <param name="processedSteps">已处理的步骤集合</param>
        private void GenerateExpandedGroupButtons(StackPanel buttonPanel, StepGroup group, HashSet<int> processedSteps)
        {
            var sortedIndices = group.StepIndices.OrderBy(x => x).ToList();
            
            for (int j = 0; j < sortedIndices.Count; j++)
            {
                var stepIndex = sortedIndices[j];
                bool needsArrow = (stepIndex < stepConfigurations.Count - 1) && 
                                  !IsLastStepInSequence(stepIndex, sortedIndices, j);
                
                GenerateSingleStepButton(buttonPanel, stepIndex, needsArrow);
                processedSteps.Add(stepIndex);
            }
        }

        /// <summary>
        /// 生成收缩状态的组按钮
        /// </summary>
        /// <param name="buttonPanel">按钮面板</param>
        /// <param name="group">步骤组</param>
        /// <param name="representativeIndex">代表性索引</param>
        private void GenerateCollapsedGroupButton(StackPanel buttonPanel, StepGroup group, int representativeIndex)
        {
            // 创建主容器
            StackPanel stepContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 创建组按钮
            Button groupButton = CreateGroupButton(group, representativeIndex);
            stepContainer.Children.Add(groupButton);

            // 添加箭头连接（如果这个组后面还有步骤）
            if (HasStepsAfterGroup(group))
            {
                var arrow = CreateArrowConnector();
                stepContainer.Children.Add(arrow);
            }

            // 添加到主容器
            buttonPanel.Children.Add(stepContainer);
        }

        /// <summary>
        /// 生成单个步骤按钮
        /// </summary>
        /// <param name="buttonPanel">按钮面板</param>
        /// <param name="stepIndex">步骤索引</param>
        /// <param name="needsArrow">是否需要箭头</param>
        private void GenerateSingleStepButton(StackPanel buttonPanel, int stepIndex, bool needsArrow)
        {
            var config = stepConfigurations[stepIndex];
            
            // 创建主容器
            StackPanel stepContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 创建美化的步骤按钮
            Button stepButton = CreateStyledStepButton(config, stepIndex);
            stepContainer.Children.Add(stepButton);

            // 添加箭头连接
            if (needsArrow)
            {
                var arrow = CreateArrowConnector();
                stepContainer.Children.Add(arrow);
            }

            // 添加到主容器
            buttonPanel.Children.Add(stepContainer);
        }

        /// <summary>
        /// 创建步骤组按钮
        /// </summary>
        /// <param name="group">步骤组</param>
        /// <param name="representativeIndex">代表性索引</param>
        /// <returns>组按钮</returns>
        private Button CreateGroupButton(StepGroup group, int representativeIndex)
        {
            Button groupButton = new Button
            {
                Width = 120,
                Height = 60,
                Margin = new Thickness(2),
                Tag = representativeIndex, // 使用组的第一个步骤作为标识
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Cursor = Cursors.Hand,
                Template = CreateButtonTemplate(),
                Content = CreateButtonContent(representativeIndex + 1, group.GroupName)
            };

            // 设置组按钮的特殊样式
            SetGroupButtonStyle(groupButton, group);

            // 绑定事件
            groupButton.Click += GroupButton_Click;
            groupButton.MouseEnter += StepButton_MouseEnter;
            groupButton.MouseLeave += StepButton_MouseLeave;

            // 添加到缓存
            groupButtonCache[group] = groupButton;

            return groupButton;
        }

        /// <summary>
        /// 设置组按钮样式
        /// </summary>
        /// <param name="button">按钮</param>
        /// <param name="group">步骤组</param>
        private void SetGroupButtonStyle(Button button, StepGroup group)
        {
            // 判断组内是否有当前步骤
            bool containsCurrentStep = group.ContainsStep(currentStep);
            
            if (containsCurrentStep)
            {
                // 当前组高亮显示
                button.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // 金色
                button.Foreground = new SolidColorBrush(Colors.Black);
            }
            else
            {
                // 默认组样式
                button.Background = new SolidColorBrush(Color.FromRgb(108, 117, 125)); // 灰色
                button.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        /// <summary>
        /// 组按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void GroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int representativeIndex)
            {
                var group = GetStepGroup(representativeIndex);
                if (group != null)
                {
                    // 如果当前步骤不在这个组内，跳转到组的第一个步骤
                    if (!group.ContainsStep(currentStep))
                    {
                        var firstStepIndex = group.StepIndices.Min();
                        ConfigStepButton_Click_Internal(firstStepIndex);
                    }
                    else
                    {
                        // 如果当前已在组内，可以考虑切换展开/收缩状态
                        // 这里选择跳转到当前步骤以刷新UI
                        ConfigStepButton_Click_Internal(currentStep);
                    }
                }
            }
        }

        /// <summary>
        /// 判断是否为序列中的最后一个步骤
        /// </summary>
        /// <param name="stepIndex">步骤索引</param>
        /// <param name="sortedIndices">排序后的索引列表</param>
        /// <param name="currentPosition">当前位置</param>
        /// <returns>是否为最后一个步骤</returns>
        private bool IsLastStepInSequence(int stepIndex, List<int> sortedIndices, int currentPosition)
        {
            // 如果是组内最后一个步骤，还需要检查后面是否有其他步骤
            if (currentPosition == sortedIndices.Count - 1)
            {
                return stepIndex >= stepConfigurations.Count - 1;
            }
            return false;
        }

        /// <summary>
        /// 判断组后面是否还有步骤
        /// </summary>
        /// <param name="group">步骤组</param>
        /// <returns>组后面是否还有步骤</returns>
        private bool HasStepsAfterGroup(StepGroup group)
        {
            var maxGroupIndex = group.StepIndices.Max();
            return maxGroupIndex < stepConfigurations.Count - 1;
        }

        /// <summary>
        /// 配置步骤按钮点击的内部实现
        /// </summary>
        /// <param name="stepIndex">步骤索引</param>
        private void ConfigStepButton_Click_Internal(int stepIndex)
        {
            // 调用原来的步骤切换逻辑
            currentStep = stepIndex;
            UpdateUI(currentStep);
            
            // UpdateUI 已经会通过 UpdateStepButtons 调用 GenerateStepButtons
        }

        /// <summary>
        /// 创建美化的步骤按钮
        /// </summary>
        /// <param name="config">步骤配置</param>
        /// <param name="stepIndex">步骤索引</param>
        /// <returns>美化后的按钮</returns>
        private Button CreateStyledStepButton(StepConfiguration config, int stepIndex)
        {
            Button stepButton = new Button
            {
                Width = 120,
                Height = 60,
                Margin = new Thickness(2),
                Tag = stepIndex,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Cursor = Cursors.Hand,
                // 设置圆角
                Template = CreateButtonTemplate(),
                // 设置内容为包含步骤编号和名称的StackPanel
                Content = CreateButtonContent(stepIndex + 1, config.DisplayName)
            };

            // 设置默认样式
            UpdateSingleStepButtonState(stepButton, stepIndex);

            // 绑定事件
            stepButton.Click += ConfigStepButton_Click;
            stepButton.MouseEnter += StepButton_MouseEnter;
            stepButton.MouseLeave += StepButton_MouseLeave;

            // 添加到缓存
            stepButtonCache[stepIndex] = stepButton;

            return stepButton;
        }

        /// <summary>
        /// 创建按钮内容（步骤名称为主，编号为辅）
        /// </summary>
        /// <param name="stepNumber">步骤编号</param>
        /// <param name="stepName">步骤名称</param>
        /// <returns>按钮内容</returns>
        private StackPanel CreateButtonContent(int stepNumber, string stepName)
        {
            StackPanel content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 步骤名称 - 主要内容
            TextBlock nameText = new TextBlock
            {
                Text = stepName,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 110,
                Margin = new Thickness(0, 8, 0, 4)
            };

            content.Children.Add(nameText);

            // 步骤编号 - 次要信息，小而精简
            TextBlock numberText = new TextBlock
            {
                Text = $"第{stepNumber}步",
                FontSize = 9,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), // 半透明白色
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            content.Children.Add(numberText);

            return content;
        }

        /// <summary>
        /// 创建箭头连接器（平面化风格）
        /// </summary>
        /// <returns>箭头UI元素</returns>
        private UIElement CreateArrowConnector()
        {
            // 创建箭头路径
            System.Windows.Shapes.Path arrowPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,0 L 8,4 L 0,8 Z"),
                Fill = new SolidColorBrush(Color.FromRgb(108, 117, 125)), // 默认灰色
                Stroke = new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                StrokeThickness = 0, // 平面化设计，无边框
                Width = 12,
                Height = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            };

            return arrowPath;
        }

        /// <summary>
        /// 创建按钮模板（圆角样式）
        /// </summary>
        /// <returns>按钮控件模板</returns>
        private ControlTemplate CreateButtonTemplate()
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));

            // 创建边框
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });

            // 创建内容展示器
            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            return template;
        }

        /// <summary>
        /// 设置按钮默认样式（平面化设计）
        /// </summary>
        /// <param name="button">按钮控件</param>
        private void SetButtonDefaultStyle(Button button)
        {
            // 使用平面化的纯色背景
            button.Background = new SolidColorBrush(Color.FromRgb(108, 117, 125)); // 现代灰色
            button.BorderBrush = new SolidColorBrush(Color.FromRgb(108, 117, 125));
            button.Foreground = new SolidColorBrush(Colors.White);

            // 移除阴影效果，使用平面化设计
            button.Effect = null;
        }

        /// <summary>
        /// 鼠标进入事件（平面化悬停效果）
        /// </summary>
        private void StepButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                // 获取当前按钮状态
                int buttonIndex = -1;
                if (button.Tag is int)
                {
                    buttonIndex = (int)button.Tag;
                }

                // 根据当前状态设置悬停效果
                if (buttonIndex == currentStep)
                {
                    // 当前步骤的悬停效果 - 稍微加深的蓝色
                    button.Background = new SolidColorBrush(Color.FromRgb(0, 86, 179));
                }
                else if (buttonIndex < currentStep)
                {
                    // 已完成步骤的悬停效果 - 稍微加深的绿色
                    button.Background = new SolidColorBrush(Color.FromRgb(33, 136, 56));
                }
                else
                {
                    // 未完成步骤的悬停效果 - 稍微加深的灰色
                    button.Background = new SolidColorBrush(Color.FromRgb(90, 98, 104));
                }

                // 平面化设计，无额外效果
                button.Effect = null;
            }
        }

        /// <summary>
        /// 鼠标离开事件（恢复原始状态）
        /// </summary>
        private void StepButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                // 检查当前按钮的状态
                int buttonIndex = -1;
                if (button.Tag is int)
                {
                    buttonIndex = (int)button.Tag;
                }

                // 恢复到相应的状态样式
                if (buttonIndex >= 0)
                {
                    // 检查是否是组按钮
                    var group = GetStepGroup(buttonIndex);
                    if (group != null && !group.IsExpanded && groupButtonCache.ContainsKey(group) && groupButtonCache[group] == button)
                    {
                        // 这是一个组按钮，使用组按钮样式
                        SetGroupButtonStyle(button, group);
                    }
                    else
                    {
                        // 这是一个普通步骤按钮，使用步骤按钮样式
                        UpdateSingleStepButtonState(button, buttonIndex);
                    }
                }
            }
        }

        /// <summary>
        /// 重新生成步骤按钮（用于动态更新配置）
        /// </summary>
        public void RefreshStepButtons()
        {
            try
            {
                // 重新初始化步骤组
                InitializeStepGroups();
                
                // 重新生成按钮
                GenerateStepButtons();

                // 如果当前步骤索引超出范围，重置为0
                if (currentStep >= stepConfigurations.Count)
                {
                    currentStep = 0;
                    // 重新更新按钮状态，因为currentStep发生了变化
                    GenerateStepButtons();
                }

                // 更新UI
                UpdateUI(currentStep);

                PageManager.Page1Instance?.LogUpdate("步骤按钮已刷新");
            }
            catch (Exception ex)
            {
                string errorMessage = $"刷新步骤按钮失败: {ex.Message}";
                PageManager.Page1Instance?.LogUpdate(errorMessage);
                MessageBox.Show(errorMessage, "刷新错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从模板配置中获取匹配模板路径
        /// </summary>
        /// <returns>匹配模板文件路径</returns>
        private string GetMatchTemplatePathFromConfig()
        {
            try
            {
                // 查找PKG位置匹配步骤的索引
                int pkgMatchingStepIndex = -1;
                for (int i = 0; i < stepConfigurations.Count; i++)
                {
                    if (stepConfigurations[i].StepType == StepType.PkgMatching)
                    {
                        pkgMatchingStepIndex = i;
                        break;
                    }
                }

                if (pkgMatchingStepIndex == -1)
                {
                    PageManager.Page1Instance?.LogUpdate("未找到PKG位置匹配步骤配置");
                    return string.Empty;
                }

                // 优先从当前编辑中的输入控件获取
                if (inputParameterControls.ContainsKey(pkgMatchingStepIndex) &&
                    inputParameterControls[pkgMatchingStepIndex].ContainsKey("匹配模板路径"))
                {
                    string currentPath = inputParameterControls[pkgMatchingStepIndex]["匹配模板路径"].Text;
                    if (!string.IsNullOrWhiteSpace(currentPath))
                    {
                        return currentPath;
                    }
                }

                // 然后从已保存的模板参数中获取
                if (currentTemplate.InputParameters.ContainsKey(StepType.PkgMatching) &&
                    currentTemplate.InputParameters[StepType.PkgMatching].ContainsKey("匹配模板路径"))
                {
                    string savedPath = currentTemplate.InputParameters[StepType.PkgMatching]["匹配模板路径"];
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        return savedPath;
                    }
                }

                // 如果都没有找到，返回空字符串
                return string.Empty;
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"获取匹配模板路径时出错: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 从模板配置中获取BLK匹配模板路径
        /// </summary>
        /// <returns>BLK匹配模板文件路径</returns>
        private string GetBlkMatchTemplatePathFromConfig()
        {
            try
            {
                // 查找BLK位置匹配步骤的索引
                int blkMatchingStepIndex = -1;
                for (int i = 0; i < stepConfigurations.Count; i++)
                {
                    if (stepConfigurations[i].StepType == StepType.BlkMatching)
                    {
                        blkMatchingStepIndex = i;
                        break;
                    }
                }

                if (blkMatchingStepIndex == -1)
                {
                    PageManager.Page1Instance?.LogUpdate("未找到BLK位置匹配步骤配置");
                    return string.Empty;
                }

                // 优先从当前编辑中的输入控件获取
                if (inputParameterControls.ContainsKey(blkMatchingStepIndex) &&
                    inputParameterControls[blkMatchingStepIndex].ContainsKey("BLK匹配模板路径"))
                {
                    string currentPath = inputParameterControls[blkMatchingStepIndex]["BLK匹配模板路径"].Text;
                    if (!string.IsNullOrWhiteSpace(currentPath))
                    {
                        return currentPath;
                    }
                }

                // 然后从已保存的模板参数中获取
                if (currentTemplate.InputParameters.ContainsKey(StepType.BlkMatching) &&
                    currentTemplate.InputParameters[StepType.BlkMatching].ContainsKey("BLK匹配模板路径"))
                {
                    string savedPath = currentTemplate.InputParameters[StepType.BlkMatching]["BLK匹配模板路径"];
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        return savedPath;
                    }
                }

                // 如果都没有找到，返回空字符串
                return string.Empty;
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"获取BLK匹配模板路径时出错: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 从模板配置中获取镀膜匹配模板路径
        /// </summary>
        /// <returns>镀膜匹配模板文件路径</returns>
        private string GetCoatingMatchTemplatePathFromConfig()
        {
            try
            {
                // 查找镀膜匹配步骤的索引
                int coatingMatchingStepIndex = -1;
                for (int i = 0; i < stepConfigurations.Count; i++)
                {
                    if (stepConfigurations[i].StepType == StepType.CoatingMatching)
                    {
                        coatingMatchingStepIndex = i;
                        break;
                    }
                }

                if (coatingMatchingStepIndex == -1)
                {
                    PageManager.Page1Instance?.LogUpdate("未找到镀膜匹配步骤配置");
                    return string.Empty;
                }

                // 优先从当前编辑中的输入控件获取
                if (inputParameterControls.ContainsKey(coatingMatchingStepIndex) &&
                    inputParameterControls[coatingMatchingStepIndex].ContainsKey("镀膜匹配模板路径"))
                {
                    string currentPath = inputParameterControls[coatingMatchingStepIndex]["镀膜匹配模板路径"].Text;
                    if (!string.IsNullOrWhiteSpace(currentPath))
                    {
                        return currentPath;
                    }
                }

                // 然后从已保存的模板参数中获取
                if (currentTemplate.InputParameters.ContainsKey(StepType.CoatingMatching) &&
                    currentTemplate.InputParameters[StepType.CoatingMatching].ContainsKey("镀膜匹配模板路径"))
                {
                    string savedPath = currentTemplate.InputParameters[StepType.CoatingMatching]["镀膜匹配模板路径"];
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        return savedPath;
                    }
                }

                // 如果都没有找到，返回空字符串
                return string.Empty;
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"获取镀膜匹配模板路径时出错: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 从模板配置中获取镀膜PKG匹配模板路径
        /// </summary>
        /// <returns>镀膜PKG匹配模板文件路径</returns>
        private string GetCoatingPkgMatchTemplatePathFromConfig()
        {
            try
            {
                int coatingPkgMatchingStepIndex = stepConfigurations.FindIndex(s => s.StepType == StepType.CoatingPkgMatching);
                if (coatingPkgMatchingStepIndex == -1)
                {
                    PageManager.Page1Instance?.LogUpdate("未找到镀膜PKG位置匹配步骤配置");
                    return string.Empty;
                }

                if (inputParameterControls.ContainsKey(coatingPkgMatchingStepIndex) &&
                    inputParameterControls[coatingPkgMatchingStepIndex].ContainsKey("镀膜PKG模板路径"))
                {
                    string currentPath = inputParameterControls[coatingPkgMatchingStepIndex]["镀膜PKG模板路径"].Text;
                    if (!string.IsNullOrWhiteSpace(currentPath))
                    {
                        return currentPath;
                    }
                }

                if (currentTemplate.InputParameters.ContainsKey(StepType.CoatingPkgMatching) &&
                    currentTemplate.InputParameters[StepType.CoatingPkgMatching].ContainsKey("镀膜PKG模板路径"))
                {
                    string savedPath = currentTemplate.InputParameters[StepType.CoatingPkgMatching]["镀膜PKG模板路径"];
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        return savedPath;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"获取镀膜PKG模板路径时出错: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 设置3个图像源模块到对应的VmRenderControl
        /// </summary>
        private void SetupMultiImageRenderControls()
        {
            try
            {
                // 绑定图像源1到第一个VmRenderControl
                var imageSource1 = VmSolution.Instance["获取路径图像.图1"] as ImageSourceModuleCs.ImageSourceModuleTool;
                if (imageSource1 != null)
                {
                    VmRender1.ModuleSource = imageSource1;
                }

                // 绑定图像源2_1到第二个VmRenderControl
                var imageSource2_1 = VmSolution.Instance["获取路径图像.图2_1"] as ImageSourceModuleCs.ImageSourceModuleTool;
                if (imageSource2_1 != null)
                {
                    VmRender2_1.ModuleSource = imageSource2_1;
                }

                // 绑定图像源2_2到第三个VmRenderControl
                var imageSource2_2 = VmSolution.Instance["获取路径图像.图2_2"] as ImageSourceModuleCs.ImageSourceModuleTool;
                if (imageSource2_2 != null)
                {
                    VmRender2_2.ModuleSource = imageSource2_2;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"设置多图像渲染控件失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 设置3张图片路径到对应的VM模块
        /// </summary>
        private void SetImagePathsToVM()
        {
            try
            {
                // 优先使用已自动匹配的图片组
                if (_currentImageGroup != null && _currentImageGroup.IsValid)
                {
                    // 设置图像源1路径
                    var imageSource1 = VmSolution.Instance["获取路径图像.图1"] as ImageSourceModuleCs.ImageSourceModuleTool;
                    imageSource1?.SetImagePath(_currentImageGroup.Source1Path);
                    LogMessage($"图像源1路径已设置: {_currentImageGroup.Source1Path}", LogLevel.Info);

                    // 设置图像源2_1路径
                    var imageSource2_1 = VmSolution.Instance["获取路径图像.图2_1"] as ImageSourceModuleCs.ImageSourceModuleTool;
                    imageSource2_1?.SetImagePath(_currentImageGroup.Source2_1Path);
                    LogMessage($"图像源2_1路径已设置: {_currentImageGroup.Source2_1Path}", LogLevel.Info);

                    // 设置图像源2_2路径
                    var imageSource2_2 = VmSolution.Instance["获取路径图像.图2_2"] as ImageSourceModuleCs.ImageSourceModuleTool;
                    imageSource2_2?.SetImagePath(_currentImageGroup.Source2_2Path);
                    LogMessage($"图像源2_2路径已设置: {_currentImageGroup.Source2_2Path}", LogLevel.Info);
                }
                else
                {
                    // fallback：从第0步的"图片路径"参数获取单个路径
                    string singleImagePath = GetParameterValue(0, "图片路径", "");
                    
                    if (!string.IsNullOrWhiteSpace(singleImagePath) && File.Exists(singleImagePath))
                    {
                        LogMessage($"尝试从单个图片路径自动匹配: {singleImagePath}", LogLevel.Info);
                        
                        // 尝试自动匹配图片组
                        var matchedGroup = AutoMatchImageGroup(singleImagePath);
                        if (matchedGroup != null && matchedGroup.IsValid)
                        {
                            _currentImageGroup = matchedGroup;
                            // 递归调用，这次会走上面的分支
                            SetImagePathsToVM();
                            return;
                        }
                        else
                        {
                            LogMessage("无法从单个图片路径自动匹配到完整的图片组", LogLevel.Warning);
                        }
                    }
                    else
                    {
                        LogMessage("未找到有效的图片路径配置，请先在\"图片选择\"步骤中选择图片", LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"设置图像路径到VM失败: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// 获取当前步骤的3个图像路径
        /// </summary>
        /// <returns>三个图像路径的元组</returns>
        private (string, string, string) GetCurrentImagePaths()
        {
            return GetImagePathsFromStep(currentStep);
        }

        /// <summary>
        /// 获取指定步骤的3个图像路径
        /// </summary>
        /// <param name="stepIndex">步骤索引</param>
        /// <returns>三个图像路径的元组</returns>
        private (string, string, string) GetImagePathsFromStep(int stepIndex)
        {
            string path1 = "";
            string path2_1 = "";
            string path2_2 = "";

            try
            {
                // 从指定步骤的输入控件获取路径
                if (inputParameterControls.ContainsKey(stepIndex))
                {
                    if (inputParameterControls[stepIndex].ContainsKey("图像源1路径"))
                        path1 = inputParameterControls[stepIndex]["图像源1路径"].Text;
                    if (inputParameterControls[stepIndex].ContainsKey("图像源2_1路径"))
                        path2_1 = inputParameterControls[stepIndex]["图像源2_1路径"].Text;
                    if (inputParameterControls[stepIndex].ContainsKey("图像源2_2路径"))
                        path2_2 = inputParameterControls[stepIndex]["图像源2_2路径"].Text;
                }

                // 如果指定步骤的控件中没有值，尝试从模板参数中获取
                if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2_1) || string.IsNullOrWhiteSpace(path2_2))
                {
                    // 将步骤索引转换为StepType
                    if (stepIndex >= 0 && stepIndex < stepConfigurations.Count)
                    {
                        var stepType = stepConfigurations[stepIndex].StepType;
                        if (currentTemplate.InputParameters.ContainsKey(stepType))
                        {
                            var stepParams = currentTemplate.InputParameters[stepType];
                            
                            if (string.IsNullOrWhiteSpace(path1) && stepParams.ContainsKey("图像源1路径"))
                                path1 = stepParams["图像源1路径"];
                            if (string.IsNullOrWhiteSpace(path2_1) && stepParams.ContainsKey("图像源2_1路径"))
                                path2_1 = stepParams["图像源2_1路径"];
                            if (string.IsNullOrWhiteSpace(path2_2) && stepParams.ContainsKey("图像源2_2路径"))
                                path2_2 = stepParams["图像源2_2路径"];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"获取步骤{stepIndex}的图像路径失败: {ex.Message}", LogLevel.Error);
            }

            return (path1, path2_1, path2_2);
        }


        /// <summary>
        /// 浏览图片按钮点击事件（增强版：支持多种图片来源选择）
        /// </summary>
        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示图片来源选择对话框
                ShowImageSourceSelectionDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择图片时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示图片来源选择对话框
        /// </summary>
        private void ShowImageSourceSelectionDialog()
        {
            try
            {
                // 创建选择对话框
                var dialog = new Window
                {
                    Title = "选择图片来源",
                    Width = 450,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.SingleBorderWindow
                };

                var mainPanel = new StackPanel
                {
                    Margin = new Thickness(20),
                    Orientation = Orientation.Vertical
                };

                // 添加标题
                var titleLabel = new TextBlock
                {
                    Text = "请选择图片来源：",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                mainPanel.Children.Add(titleLabel);

                // 按钮1: 模板文件夹
                var btnTemplateFolder = new Button
                {
                    Content = "📁 本模板文件夹",
                    Height = 50,
                    Margin = new Thickness(0, 5, 0, 0),
                    FontSize = 14,
                    Background = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                    Foreground = Brushes.White
                };
                btnTemplateFolder.Click += (s, args) =>
                {
                    dialog.DialogResult = true;
                    dialog.Tag = "template";
                    dialog.Close();
                };
                mainPanel.Children.Add(btnTemplateFolder);

                // 按钮2: 本LOT文件夹
                var btnLOTFolder = new Button
                {
                    Content = "📂 本LOT文件夹",
                    Height = 50,
                    Margin = new Thickness(0, 5, 0, 0),
                    FontSize = 14,
                    Background = new SolidColorBrush(Color.FromRgb(60, 179, 113)),
                    Foreground = Brushes.White
                };
                btnLOTFolder.Click += (s, args) =>
                {
                    dialog.DialogResult = true;
                    dialog.Tag = "lot";
                    dialog.Close();
                };
                mainPanel.Children.Add(btnLOTFolder);

                // 按钮3: 最后一组图
                var btnLastImages = new Button
                {
                    Content = "🔄 最后一组图",
                    Height = 50,
                    Margin = new Thickness(0, 5, 0, 0),
                    FontSize = 14,
                    Background = new SolidColorBrush(Color.FromRgb(255, 140, 0)),
                    Foreground = Brushes.White
                };
                btnLastImages.Click += (s, args) =>
                {
                    dialog.DialogResult = true;
                    dialog.Tag = "last";
                    dialog.Close();
                };
                mainPanel.Children.Add(btnLastImages);

                // 取消按钮
                var btnCancel = new Button
                {
                    Content = "取消",
                    Height = 35,
                    Margin = new Thickness(0, 15, 0, 0),
                    FontSize = 14,
                    Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                    Foreground = Brushes.White
                };
                btnCancel.Click += (s, args) =>
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                };
                mainPanel.Children.Add(btnCancel);

                dialog.Content = mainPanel;

                // 显示对话框并处理结果
                if (dialog.ShowDialog() == true)
                {
                    string selection = dialog.Tag?.ToString();
                    ProcessImageSourceSelection(selection);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"显示图片来源选择对话框失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"显示选择对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 处理图片来源选择结果
        /// </summary>
        private void ProcessImageSourceSelection(string selection)
        {
            try
            {
                switch (selection)
                {
                    case "template":
                        BrowseTemplateFolder();
                        break;
                    case "lot":
                        BrowseLOTFolder();
                        break;
                    case "last":
                        InjectLastTestImages();
                        break;
                    default:
                        LogMessage("未知的图片来源选择", LogLevel.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"处理图片来源选择失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"处理选择失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 浏览模板文件夹
        /// </summary>
        private void BrowseTemplateFolder()
        {
            try
            {
                // 获取当前模板的文件夹路径
                string templateName = !string.IsNullOrWhiteSpace(currentTemplate.TemplateName) ? 
                    currentTemplate.TemplateName : "Default";
                string templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", templateName);
                
                // 如果模板文件夹不存在，创建它
                if (!Directory.Exists(templateDir))
                {
                    Directory.CreateDirectory(templateDir);
                    LogMessage($"创建模板文件夹: {templateDir}", LogLevel.Info);
                }

                // 创建打开文件对话框，指定初始目录为模板文件夹
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*",
                    Title = $"选择模板文件夹中的图片 - {templateName}",
                    InitialDirectory = templateDir
                };

                if (dialog.ShowDialog() == true)
                {
                    string selectedFile = dialog.FileName;
                    ProcessSelectedImageFile(selectedFile, "模板文件夹");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"浏览模板文件夹失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"浏览模板文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 浏览LOT文件夹
        /// </summary>
        private void BrowseLOTFolder()
        {
            try
            {
                // 获取当前LOT号对应的图片存储目录
                string lotImageDir = GetLOTImageDirectory();
                
                if (string.IsNullOrEmpty(lotImageDir) || !Directory.Exists(lotImageDir))
                {
                    MessageBox.Show("未找到当前LOT号对应的图片存储目录。\n\n请确认LOT号设置正确且已有图片数据。", 
                        "LOT文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 创建打开文件对话框，指定初始目录为LOT文件夹
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*",
                    Title = $"选择LOT文件夹中的图片",
                    InitialDirectory = lotImageDir
                };

                if (dialog.ShowDialog() == true)
                {
                    string selectedFile = dialog.FileName;
                    ProcessSelectedImageFile(selectedFile, "LOT文件夹");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"浏览LOT文件夹失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"浏览LOT文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 注入最后一组图
        /// </summary>
        private void InjectLastTestImages()
        {
            try
            {
                // 获取最新测试的图片组
                var lastImageGroup = GetLastTestImageGroup();
                
                if (lastImageGroup == null || !lastImageGroup.IsValid)
                {
                    MessageBox.Show("未找到最新测试的图片组。\n\n请确认已进行过图片测试且图片文件仍然存在。", 
                        "最后一组图", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 直接使用最后一组图
                _currentImageGroup = lastImageGroup;
                // 使用ProcessSelectedImageFile处理最后一组图片
                ProcessSelectedImageFile(lastImageGroup.Source1Path, "最后一组图");
                
                // // 设置到界面
                // inputParameterControls[currentStep]["图片路径"].Text = lastImageGroup.Source1Path;
                
                // // 设置到VM模块
                // SetImagePathsToVM();
                
                // // 设置到VmRenderControl显示
                // SetupMultiImageRenderControls();
                
                // // 保存参数
                // SaveStepParameters(currentStep);
                
                // MessageBox.Show($"✅ 最后一组图已成功注入！\n\n图片组: {lastImageGroup.BaseName}\n来源: {Path.GetDirectoryName(lastImageGroup.Source1Path)}", 
                //     "最后一组图", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // PageManager.Page1Instance?.LogUpdate($"已注入最后一组图: {lastImageGroup.BaseName}");
            }
            catch (Exception ex)
            {
                LogMessage($"注入最后一组图失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"注入最后一组图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 处理选中的图片文件
        /// </summary>
        private void ProcessSelectedImageFile(string selectedFile, string source)
        {
            try
            {
                // 自动匹配其他图片
                var matchedGroup = AutoMatchImageGroup(selectedFile);
                if (matchedGroup != null && matchedGroup.IsValid)
                {
                    // 根据来源决定是否需要复制到模板文件夹
                    bool shouldCopyToTemplate = source != "模板文件夹";
                    
                    if (shouldCopyToTemplate)
                    {
                        // 创建模板文件夹结构并复制图片
                        var templateImageGroup = CreateTemplateImageStructure(matchedGroup);
                        
                        if (templateImageGroup != null)
                        {
                            _currentImageGroup = templateImageGroup;
                            inputParameterControls[currentStep]["图片路径"].Text = templateImageGroup.Source1Path;
                            
                            string templateName = !string.IsNullOrWhiteSpace(currentTemplate.TemplateName) ? 
                                currentTemplate.TemplateName : "当前模板";
                            string templateDir = Path.GetDirectoryName(Path.GetDirectoryName(templateImageGroup.Source1Path));
                            
                            MessageBox.Show(
                                $"✅ 图片复制完成！\n\n" +
                                $"📁 原图片位置：{source}\n" +
                                $"📂 模板位置：{templateDir}\n\n" +
                                $"🔄 输入框路径已自动切换到模板文件夹\n" +
                                $"💾 所有相关图片已复制到模板专用目录中",
                                "模板图片管理", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            // 如果创建模板文件夹失败，使用原始匹配结果
                            _currentImageGroup = matchedGroup;
                            inputParameterControls[currentStep]["图片路径"].Text = matchedGroup.Source1Path;
                            
                            MessageBox.Show(
                                "⚠️ 模板文件夹创建失败，使用原始图片路径\n\n" +
                                "请检查程序目录写权限或手动管理图片文件",
                                "模板图片管理", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        // 直接使用模板文件夹中的图片
                        _currentImageGroup = matchedGroup;
                        inputParameterControls[currentStep]["图片路径"].Text = matchedGroup.Source1Path;
                        
                        MessageBox.Show(
                            $"✅ 图片加载完成！\n\n" +
                            $"📁 来源：{source}\n" +
                            $"📂 图片组：{matchedGroup.BaseName}",
                            "图片选择", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                    }
                    
                    _imageRenderer?.DisplayImageGroup(_currentImageGroup);
                    
                    // 保存参数
                    SaveStepParameters(currentStep);
                    
                    PageManager.Page1Instance?.LogUpdate($"已从{source}加载图片组: {_currentImageGroup.BaseName}");
                }
                else
                {
                    // 清空匹配结果
                    _currentImageGroup = null;
                    
                    LogMessage($"未找到匹配的图片组: {Path.GetFileName(selectedFile)}", LogLevel.Warning);
                    MessageBox.Show("未找到匹配的图片组，请检查文件夹结构。\n\n要求：图片应位于以下结构的文件夹中：\n父目录/\n├── 图像源1/\n├── 图像源2_1/\n└── 图像源2_2/", 
                        "选择图片", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"处理选中图片文件失败: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// 获取LOT号对应的图片存储目录
        /// </summary>
        private string GetLOTImageDirectory()
        {
            try
            {
                // 从Page1获取当前LOT号
                var page1Instance = PageManager.Page1Instance;
                if (page1Instance == null) return "";
                
                // 使用Page1的GetCurrentImageSaveDirectory()方法获取正确的存图路径
                string currentImageSaveDir = page1Instance.GetCurrentImageSaveDirectory();
                
                if (string.IsNullOrEmpty(currentImageSaveDir))
                {
                    LogMessage("无法获取当前图片存储目录", LogLevel.Warning);
                    return "";
                }
                
                // 检查目录是否存在
                if (!Directory.Exists(currentImageSaveDir))
                {
                    LogMessage($"图片存储目录不存在: {currentImageSaveDir}", LogLevel.Warning);
                    return "";
                }
                
                return currentImageSaveDir;
            }
            catch (Exception ex)
            {
                LogMessage($"获取LOT图片目录失败: {ex.Message}", LogLevel.Error);
                return "";
            }
        }

        /// <summary>
        /// 获取最新测试的图片组
        /// </summary>
        private ImageGroupSet GetLastTestImageGroup()
        {
            try
            {
                // 从Page1获取最新测试的图片组信息
                var page1Instance = PageManager.Page1Instance;
                if (page1Instance == null) return null;
                
                // 获取最新的图片测试组（假设Page1有相应的属性或方法）
                var lastImageGroup = page1Instance.GetLastTestImageGroup();
                
                if (lastImageGroup != null && lastImageGroup.IsValid)
                {
                    return lastImageGroup;
                }
                
                
                return null;
            }
            catch (Exception ex)
            {
                LogMessage($"获取最新测试图片组失败: {ex.Message}", LogLevel.Error);
                return null;
            }
        }



        /// <summary>
        /// 浏览并自动匹配图片（新方法）
        /// </summary>
        private void BrowseAndAutoMatchImages()
        {
            try
            {
                // 创建打开文件对话框
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*",
                    Title = "选择任意一张图片（系统将自动匹配其他图片）"
                };

                if (dialog.ShowDialog() == true)
                {
                    string selectedFile = dialog.FileName;
                    
                    // 自动匹配其他图片
                    var matchedGroup = AutoMatchImageGroup(selectedFile);
                    if (matchedGroup != null && matchedGroup.IsValid)
                    {
                        // 自动创建模板文件夹结构并复制图片
                        var templateImageGroup = CreateTemplateImageStructure(matchedGroup);
                        
                        if (templateImageGroup != null)
                        {
                            // 使用模板文件夹中的图片路径
                            _currentImageGroup = templateImageGroup;
                            
                            // 直接写入新路径
                            inputParameterControls[currentStep]["图片路径"].Text = templateImageGroup.Source1Path;
                            
                            // 弹窗提示用户图片已复制到模板文件夹
                            string templateName = !string.IsNullOrWhiteSpace(currentTemplate.TemplateName) ? 
                                currentTemplate.TemplateName : "当前模板";
                            string templateDir = Path.GetDirectoryName(Path.GetDirectoryName(templateImageGroup.Source1Path));
                            
                            MessageBox.Show(
                                $"✅ 图片复制完成！\n\n" +
                                $"📁 原图片位置：{Path.GetDirectoryName(Path.GetDirectoryName(matchedGroup.Source1Path))}\n" +
                                $"📂 模板位置：{templateDir}\n\n" +
                                $"🔄 输入框路径已自动切换到模板文件夹\n" +
                                $"💾 所有相关图片已复制到模板专用目录中",
                                "模板图片管理", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Information);
                                
                            PageManager.Page1Instance?.LogUpdate($"图片已复制到模板目录: {templateName}");
                        }
                        else
                        {
                            // 如果创建模板文件夹失败，使用原始匹配结果
                            _currentImageGroup = matchedGroup;
                            
                            // 保存参数  
                            SaveStepParameters(currentStep);
                            
                            MessageBox.Show(
                                "⚠️ 模板文件夹创建失败，使用原始图片路径\n\n" +
                                "请检查程序目录写权限或手动管理图片文件",
                                "模板图片管理", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Warning);
                        }
                        
                        _imageRenderer?.DisplayImageGroup(_currentImageGroup);
                        
                        PageManager.Page1Instance?.LogUpdate($"已加载图片组: {_currentImageGroup.BaseName}");
                    }
                    else
                    {
                        // 清空匹配结果
                        _currentImageGroup = null;
                        
                        LogMessage($"未找到匹配的图片组: {Path.GetFileName(selectedFile)}", LogLevel.Warning);
                        MessageBox.Show("未找到匹配的图片组，请检查文件夹结构。\n\n要求：图片应位于以下结构的文件夹中：\n父目录/\n├── 图像源1/\n├── 图像源2_1/\n└── 图像源2_2/", "选择图片", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"浏览并自动匹配图片失败: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// 设置指定参数的图像路径
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="imagePath">图像路径</param>
        private void SetImagePath(string paramName, string imagePath)
        {
            if (inputParameterControls.ContainsKey(currentStep))
            {
                if (inputParameterControls[currentStep].ContainsKey(paramName))
                {
                    // 在UI线程上更新文本框
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        inputParameterControls[currentStep][paramName].Text = imagePath;
                        // 强制更新UI
                        inputParameterControls[currentStep][paramName].UpdateLayout();
                    }), System.Windows.Threading.DispatcherPriority.Normal);
                }
            }
        }

        /// <summary>
        /// 自动匹配图片按钮点击事件
        /// </summary>
        private void AutoMatchImages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取当前图像源1的路径
                string source1Path = "";
                if (inputParameterControls.ContainsKey(currentStep) &&
                    inputParameterControls[currentStep].ContainsKey("图像源1路径"))
                {
                    source1Path = inputParameterControls[currentStep]["图像源1路径"].Text;
                }

                if (string.IsNullOrWhiteSpace(source1Path) || !File.Exists(source1Path))
                {
                    MessageBox.Show("请先选择图像源1，然后再进行自动匹配", "自动匹配", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 执行自动匹配
                var matchedGroup = AutoMatchImageGroup(source1Path);
                if (matchedGroup != null && matchedGroup.IsValid)
                {
                    // 自动创建模板文件夹结构并复制图片
                    var templateImageGroup = CreateTemplateImageStructure(matchedGroup);
                    
                    if (templateImageGroup != null)
                    {
                        // 使用模板文件夹中的图片路径
                        _currentImageGroup = templateImageGroup;
                        
                        // 更新用户选择的输入框路径为模板文件夹中图像源1的路径
                        SetImagePath("图像源1路径", templateImageGroup.Source1Path);
                        
                        // 弹窗提示用户图片已复制到模板文件夹
                        string templateName = !string.IsNullOrWhiteSpace(currentTemplate.TemplateName) ? 
                            currentTemplate.TemplateName : "当前模板";
                        string templateDir = Path.GetDirectoryName(Path.GetDirectoryName(templateImageGroup.Source1Path));
                        
                        MessageBox.Show(
                            $"✅ 自动匹配并复制完成！\n\n" +
                            $"🎯 找到匹配图片组: {matchedGroup.BaseName}\n" +
                            $"📁 原图片位置：{Path.GetDirectoryName(Path.GetDirectoryName(matchedGroup.Source1Path))}\n" +
                            $"📂 模板位置：{templateDir}\n\n" +
                            $"🔄 输入框路径已自动切换到模板文件夹",
                            "自动匹配成功", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                            
                        PageManager.Page1Instance?.LogUpdate($"自动匹配并复制到模板: {templateName}");
                    }
                    else
                    {
                        // 如果创建模板文件夹失败，使用原始匹配结果
                        SetImagePath("图像源1路径", matchedGroup.Source1Path);
                        
                        MessageBox.Show($"自动匹配成功！\n找到了匹配的图片组: {matchedGroup.BaseName}\n\n⚠️ 模板文件夹创建失败，使用原始路径", "自动匹配", MessageBoxButton.OK, MessageBoxImage.Information);
                        PageManager.Page1Instance?.LogUpdate($"已自动匹配图片组: {matchedGroup.BaseName}");
                    }

                    // 保存参数
                    SaveStepParameters(currentStep);
                    LogMessage($"自动匹配成功: {matchedGroup.BaseName}", LogLevel.Info);
                }
                else
                {
                    MessageBox.Show("未找到匹配的图片组，请检查文件夹结构", "自动匹配", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"自动匹配失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"自动匹配失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 根据单张图片自动匹配其他图片（复用Page1的逻辑）
        /// </summary>
        /// <param name="singleImagePath">单张图片路径</param>
        /// <returns>匹配的图片组</returns>
        private ImageGroupSet AutoMatchImageGroup(string singleImagePath)
        {
            try
            {
                var parentDir = Path.GetDirectoryName(Path.GetDirectoryName(singleImagePath));
                if (string.IsNullOrEmpty(parentDir))
                {
                    PageManager.Page1Instance?.LogUpdate("无法确定图片的父目录");
                    return null;
                }

                // 提取文件名的数字后缀
                var fileName = Path.GetFileNameWithoutExtension(singleImagePath);
                var match = System.Text.RegularExpressions.Regex.Match(fileName, @"^.+(_\d+)$");
                
                if (!match.Success)
                {
                    PageManager.Page1Instance?.LogUpdate("文件名格式不符合自动匹配要求（需要_数字后缀）");
                    return null;
                }

                string suffix = match.Groups[1].Value;
                
                // 根据后缀创建图片组
                var imageGroup = CreateImageGroupBySuffix(parentDir, suffix);
                
                return imageGroup;
            }
            catch (Exception ex)
            {
                PageManager.Page1Instance?.LogUpdate($"自动匹配图片组失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据指定的数字后缀创建图片组（复用Page1的逻辑）
        /// </summary>
        /// <param name="parentDir">父目录</param>
        /// <param name="suffix">数字后缀</param>
        /// <returns>图片组</returns>
        private ImageGroupSet CreateImageGroupBySuffix(string parentDir, string suffix)
        {
            try
            {
                // 在三个图像源目录中分别查找指定后缀的.bmp文件
                var source1Dir = Path.Combine(parentDir, "图像源1");
                var source2_1Dir = Path.Combine(parentDir, "图像源2_1");
                var source2_2Dir = Path.Combine(parentDir, "图像源2_2");

                string source1Path = null;
                string source2_1Path = null;
                string source2_2Path = null;
                string baseName = "";

                // 查找图像源1文件
                if (Directory.Exists(source1Dir))
                {
                    var source1Files = Directory.GetFiles(source1Dir, $"*{suffix}.bmp");
                    if (source1Files.Length > 0)
                    {
                        source1Path = source1Files[0];
                        var fileName = Path.GetFileNameWithoutExtension(source1Path);
                        var match = System.Text.RegularExpressions.Regex.Match(fileName, @"^(.+)_\d+$");
                        if (match.Success)
                        {
                            baseName = match.Groups[1].Value;
                        }
                    }
                }

                // 查找图像源2_1文件
                if (Directory.Exists(source2_1Dir))
                {
                    var source2_1Files = Directory.GetFiles(source2_1Dir, $"*{suffix}.bmp");
                    if (source2_1Files.Length > 0)
                    {
                        source2_1Path = source2_1Files[0];
                    }
                }

                // 查找图像源2_2文件
                if (Directory.Exists(source2_2Dir))
                {
                    var source2_2Files = Directory.GetFiles(source2_2Dir, $"*{suffix}.bmp");
                    if (source2_2Files.Length > 0)
                    {
                        source2_2Path = source2_2Files[0];
                    }
                }

                // 检查是否找到了完整的三张图片
                if (!string.IsNullOrEmpty(source1Path) && 
                    !string.IsNullOrEmpty(source2_1Path) && 
                    !string.IsNullOrEmpty(source2_2Path))
                {
                    var imageGroup = new ImageGroupSet
                    {
                        Source1Path = source1Path,
                        Source2_1Path = source2_1Path,
                        Source2_2Path = source2_2Path,
                        BaseName = string.IsNullOrEmpty(baseName) ? $"{Path.GetFileName(parentDir)}{suffix}" : $"{baseName}{suffix}"
                    };
                    
                    // 🔧 关键修复：复用现有的3D图片匹配函数
                    // 如果启用了3D检测，则查找匹配的3D图片
                    bool userEnabled3D = ThreeDSettings.CurrentDetection3DParams?.Enable3DDetection ?? false;
                    if (userEnabled3D)
                    {
                        Page1.FindAndSet3DImagesForGroup(parentDir, suffix, imageGroup, enableLogging: true);
                        LogMessage($"已为图片组调用3D图片匹配函数，suffix: {suffix}", LogLevel.Info);
                    }
                    
                    return imageGroup;
                }
                else
                {
                    PageManager.Page1Instance?.LogUpdate($"目录 {Path.GetFileName(parentDir)} 中未找到完整的图片组 (后缀: {suffix})");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"根据后缀创建图片组失败: {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// 创建模板图片文件夹结构并复制图片
        /// </summary>
        /// <param name="originalGroup">原始匹配的图片组</param>
        /// <returns>复制后的模板图片组，如果失败返回null</returns>
        private ImageGroupSet CreateTemplateImageStructure(ImageGroupSet originalGroup)
        {
            try
            {
                // 获取当前模板名
                string templateName = "DefaultTemplate";
                if (!string.IsNullOrWhiteSpace(currentTemplate.TemplateName))
                {
                    templateName = currentTemplate.TemplateName;
                }
                else if (inputParameterControls.ContainsKey(currentStep) &&
                         inputParameterControls[currentStep].ContainsKey("模板名称") &&
                         !string.IsNullOrWhiteSpace(inputParameterControls[currentStep]["模板名称"].Text))
                {
                    templateName = inputParameterControls[currentStep]["模板名称"].Text;
                }

                // 创建安全的文件夹名称（移除非法字符）
                string safeTemplateName = string.Join("_", templateName.Split(Path.GetInvalidFileNameChars()));
                
                // 创建基础路径：程序目录/Templates/模板名/
                string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", safeTemplateName);
                
                // 创建"模板图片"文件夹
                string templateImagesDir = Path.Combine(baseDir, "模板图片");
                
                // 创建以当前时间命名的文件夹
                string timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string timeStampDir = Path.Combine(templateImagesDir, timeStamp);
                
                // 创建"匹配模板"文件夹
                string matchTemplateDir = Path.Combine(baseDir, "匹配模板");
                
                // 创建图像源文件夹
                string source1Dir = Path.Combine(timeStampDir, "图像源1");
                string source2_1Dir = Path.Combine(timeStampDir, "图像源2_1");
                string source2_2Dir = Path.Combine(timeStampDir, "图像源2_2");
                
                // 🔧 修正：创建3D文件夹（统一的3D文件夹，不是分开的Height和Gray）
                string threeDDir = Path.Combine(timeStampDir, "3D");
                
                // 创建所有必要的文件夹
                Directory.CreateDirectory(source1Dir);
                Directory.CreateDirectory(source2_1Dir);
                Directory.CreateDirectory(source2_2Dir);
                Directory.CreateDirectory(threeDDir);
                Directory.CreateDirectory(matchTemplateDir);
                
                LogMessage($"已创建模板文件夹结构: {baseDir}", LogLevel.Info);
                
                // 复制图片文件
                string newSource1Path = null;
                string newSource2_1Path = null;
                string newSource2_2Path = null;
                string newHeightImagePath = null;
                string newGrayImagePath = null;
                
                // 复制图像源1
                if (!string.IsNullOrEmpty(originalGroup.Source1Path) && File.Exists(originalGroup.Source1Path))
                {
                    string fileName = Path.GetFileName(originalGroup.Source1Path);
                    newSource1Path = Path.Combine(source1Dir, fileName);
                    File.Copy(originalGroup.Source1Path, newSource1Path, true);
                    LogMessage($"已复制图像源1: {fileName}", LogLevel.Info);
                }
                
                // 复制图像源2_1
                if (!string.IsNullOrEmpty(originalGroup.Source2_1Path) && File.Exists(originalGroup.Source2_1Path))
                {
                    string fileName = Path.GetFileName(originalGroup.Source2_1Path);
                    newSource2_1Path = Path.Combine(source2_1Dir, fileName);
                    File.Copy(originalGroup.Source2_1Path, newSource2_1Path, true);
                    LogMessage($"已复制图像源2_1: {fileName}", LogLevel.Info);
                }
                
                // 复制图像源2_2
                if (!string.IsNullOrEmpty(originalGroup.Source2_2Path) && File.Exists(originalGroup.Source2_2Path))
                {
                    string fileName = Path.GetFileName(originalGroup.Source2_2Path);
                    newSource2_2Path = Path.Combine(source2_2Dir, fileName);
                    File.Copy(originalGroup.Source2_2Path, newSource2_2Path, true);
                    LogMessage($"已复制图像源2_2: {fileName}", LogLevel.Info);
                }
                
                // 🔧 修正：复制3D图像到统一的3D文件夹（复用现有设计）
                bool is3DCopyEnabled = ThreeDSettings.CurrentDetection3DParams?.Enable3DDetection == true;
                if (is3DCopyEnabled && originalGroup.Has3DImages)
                {
                    // 复制高度图到3D文件夹
                    if (!string.IsNullOrEmpty(originalGroup.HeightImagePath) && File.Exists(originalGroup.HeightImagePath))
                    {
                        string fileName = Path.GetFileName(originalGroup.HeightImagePath);
                        newHeightImagePath = Path.Combine(threeDDir, fileName);
                        File.Copy(originalGroup.HeightImagePath, newHeightImagePath, true);
                        LogMessage($"已复制3D高度图: {fileName}", LogLevel.Info);
                    }
                    
                    // 复制灰度图到3D文件夹
                    if (!string.IsNullOrEmpty(originalGroup.GrayImagePath) && File.Exists(originalGroup.GrayImagePath))
                    {
                        string fileName = Path.GetFileName(originalGroup.GrayImagePath);
                        newGrayImagePath = Path.Combine(threeDDir, fileName);
                        File.Copy(originalGroup.GrayImagePath, newGrayImagePath, true);
                        LogMessage($"已复制3D灰度图: {fileName}", LogLevel.Info);
                    }
                }
                
                // 验证所有文件都成功复制
                if (!string.IsNullOrEmpty(newSource1Path) && 
                    !string.IsNullOrEmpty(newSource2_1Path) && 
                    !string.IsNullOrEmpty(newSource2_2Path))
                {
                    // 创建新的图片组对象，使用模板文件夹中的路径
                    var templateImageGroup = new ImageGroupSet
                    {
                        Source1Path = newSource1Path,
                        Source2_1Path = newSource2_1Path,
                        Source2_2Path = newSource2_2Path,
                        BaseName = originalGroup.BaseName,
                        // 🔧 新增：包含复制后的3D图像路径
                        HeightImagePath = newHeightImagePath,
                        GrayImagePath = newGrayImagePath
                    };
                    
                    LogMessage($"模板图片结构创建成功，所有图片已复制到: {timeStampDir}", LogLevel.Info);
                    
                    return templateImageGroup;
                }
                else
                {
                    LogMessage("图片复制不完整，使用原始路径", LogLevel.Warning);
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"创建模板图片结构失败: {ex.Message}", LogLevel.Error);
                return null;
            }
        }
        
        /// <summary>
        /// 当前匹配的图片组（用于第一步的图片选择）
        /// </summary>
        private ImageGroupSet _currentImageGroup = null;
        
        /// <summary>
        /// 连续NG检测相关字段
        /// </summary>
        private string _lastNGType = "";  // 上一次的NG类型
        private int _consecutiveNGCount = 0;  // 连续相同NG的次数
        private const int CONSECUTIVE_NG_ALERT_THRESHOLD = 3;  // 连续NG告警阈值
        
        /// <summary>
        /// 调试模式开关：设为true时，图片测试模式下也会启用连续NG告警（用于功能验证）
        /// 正常情况下应为false，只在正常检测时启用告警
        /// </summary>
        private static bool _enableDebugModeForTesting = false;  // 默认关闭，按需求只在正常检测时告警

        /// <summary>
        /// 初始化配置页面的DataGrid（简化版：不使用定时器，改为手动同步）
        /// </summary>
        private void InitializeConfigDataGrid()
        {
            try
            {
                // 🎯 简化方案：去掉定时器，改为手动同步
                LogMessage("配置页面DataGrid已初始化（手动同步模式）", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"初始化配置DataGrid失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 从Page1同步DataGrid数据到配置页面（完全线程安全版本）
        /// </summary>
        private void SyncDataGridFromPage1()
        {
            try
            {
                // 🔧 关键修复：确保所有UI访问都在UI线程中执行
                if (!Dispatcher.CheckAccess())
                {
                    // 当前不在UI线程，调度到UI线程执行
                    Dispatcher.BeginInvoke(new System.Action(SyncDataGridFromPage1), 
                        System.Windows.Threading.DispatcherPriority.Background);
                    return;
                }

                // 🔧 修复：将所有UI访问操作都放在UI线程中执行
                System.Collections.IEnumerable page1DataSource = null;
                IList<DetectionItem> page1Items = null;
                object page1SelectedItem = null;
                bool hasPage1Data = false;

                try
                {
                    // 安全获取Page1的DataGrid数据（已在UI线程中）
                    if (PageManager.Page1Instance?.DataGrid1 != null)
                    {
                        page1DataSource = PageManager.Page1Instance.DataGrid1.ItemsSource;

                        if (page1DataSource is IList<DetectionItem> listItems)
                        {
                            page1Items = listItems;
                        }
                        else if (page1DataSource is IEnumerable<DetectionItem> enumerableItems)
                        {
                            page1Items = enumerableItems.ToList();
                        }

                        page1SelectedItem = PageManager.Page1Instance.DataGrid1.SelectedItem;
                        hasPage1Data = page1DataSource != null;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Warning($"获取Page1 DataGrid数据失败: {ex.Message}");
                    return;
                }

                // 通过名称查找配置页面的DataGrid控件
                var configDataGrid = this.FindName("ConfigDataGrid") as DataGrid;
                if (configDataGrid == null)
                {
                    LogManager.Warning("未找到ConfigDataGrid控件");
                    return;
                }

                if (hasPage1Data && page1Items != null)
                {
                    try
                    {
                        // 🎯 同步数据源
                        configDataGrid.ItemsSource = page1DataSource;
                        
                        // 同步超限行的颜色设置
                        if (page1Items.Count > 0)
                        {
                            // 设置LoadingRow事件处理程序
                            configDataGrid.LoadingRow -= ConfigDataGrid_LoadingRow;
                            configDataGrid.LoadingRow += ConfigDataGrid_LoadingRow;
                            
                            // 应用超限行的颜色设置（但不输出日志）
                            SetOutOfRangeRowsColor(configDataGrid, page1Items);
                        }
                        
                        // 强制刷新显示
                        configDataGrid.Items.Refresh();
                        
                        // 监听DataGrid的实际渲染完成事件
                        void OnDataGridRendered(object sender, EventArgs e)
                        {
                            configDataGrid.LayoutUpdated -= OnDataGridRendered;
                            try
                            {
                                WpfApp2.UI.SystemTestWindow.NotifyUIRenderCompleted();
                            }
                            catch
                            {
                                // 系统测试窗口可能未打开，忽略异常
                            }
                        }
                        configDataGrid.LayoutUpdated += OnDataGridRendered;
                        
                        // 同步选中项（如果有的话）
                        if (page1SelectedItem != null)
                        {
                            configDataGrid.SelectedItem = page1SelectedItem;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Warning($"同步ConfigDataGrid数据失败: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        // 如果Page1没有数据，清空配置页面的DataGrid
                        configDataGrid.ItemsSource = null;
                    }
                    catch (Exception ex)
                    {
                        LogManager.Warning($"清空ConfigDataGrid失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"DataGrid同步失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 配置页面DataGrid行加载事件处理（用于动态设置行背景色）
        /// </summary>
        private void ConfigDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                var item = e.Row.DataContext as DetectionItem;
                if (item != null)
                {
                    if (item.IsOutOfRange)
                    {
                        e.Row.Background = new SolidColorBrush(Colors.LightCoral);
                    }
                    else
                    {
                        e.Row.Background = new SolidColorBrush(Colors.White);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"配置页面DataGrid行加载事件处理出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 手动刷新配置页面的DataGrid数据（线程安全版本）
        /// </summary>
        public void RefreshConfigDataGrid()
        {
            try
            {
                // 🔧 修复多线程UI访问问题：确保在UI线程中执行
                if (!Dispatcher.CheckAccess())
                {
                    // 当前不在UI线程，调度到UI线程执行
                    Dispatcher.BeginInvoke(new System.Action(RefreshConfigDataGrid), 
                        System.Windows.Threading.DispatcherPriority.Background);
                    return;
                }

                // 在UI线程中调用同步方法
                SyncDataGridFromPage1();
            }
            catch (Exception ex)
            {
                LogManager.Warning($"刷新配置DataGrid失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导航到指定方向的参数
        /// </summary>
        public void NavigateToParameter(WpfApp2.UI.Controls.NavigationDirection direction, string currentParameterName)
        {
            try
            {
                if (!inputParameterControls.ContainsKey(currentStep))
                    return;

                var currentStepControls = inputParameterControls[currentStep];
                var parameterNames = currentStepControls.Keys.Where(name => !IsTextInputParameter(name)).ToList();
                
                if (parameterNames.Count == 0)
                    return;

                var currentIndex = parameterNames.IndexOf(currentParameterName);
                if (currentIndex == -1)
                    return;

                int targetIndex;
                if (direction == WpfApp2.UI.Controls.NavigationDirection.Previous)
                {
                    targetIndex = currentIndex > 0 ? currentIndex - 1 : parameterNames.Count - 1;
                }
                else
                {
                    targetIndex = currentIndex < parameterNames.Count - 1 ? currentIndex + 1 : 0;
                }

                var targetParameterName = parameterNames[targetIndex];
                var targetTextBox = currentStepControls[targetParameterName];

                // 关闭当前智能输入卡片
                WpfApp2.UI.Controls.TextBoxSmartInputExtensions.CloseCurrentWindow();

                // 打开目标参数的智能输入卡片
                targetTextBox.Focus();
                
                LogMessage($"导航到参数: {targetParameterName}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"参数导航失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 在卡片中导航到指定方向的参数（不关闭卡片）
        /// </summary>
        public void NavigateToParameterInCard(WpfApp2.UI.Controls.NavigationDirection direction, string currentParameterName, 
                                            WpfApp2.UI.Controls.SmartInputCardWindow cardWindow)
        {
            try
            {
                if (!inputParameterControls.ContainsKey(currentStep))
                    return;

                var currentStepControls = inputParameterControls[currentStep];
                var parameterNames = currentStepControls.Keys.Where(name => !IsTextInputParameter(name)).ToList();
                
                if (parameterNames.Count == 0)
                    return;

                var currentIndex = parameterNames.IndexOf(currentParameterName);
                if (currentIndex == -1)
                    return;

                int targetIndex;
                if (direction == WpfApp2.UI.Controls.NavigationDirection.Previous)
                {
                    targetIndex = currentIndex > 0 ? currentIndex - 1 : parameterNames.Count - 1;
                }
                else
                {
                    targetIndex = currentIndex < parameterNames.Count - 1 ? currentIndex + 1 : 0;
                }

                var targetParameterName = parameterNames[targetIndex];
                var targetTextBox = currentStepControls[targetParameterName];

                // 获取目标参数的当前值
                double currentValue = 0;
                if (double.TryParse(targetTextBox.Text, out double parsedValue))
                {
                    currentValue = parsedValue;
                }

                // 获取参数配置
                var parameterConfig = ModuleRegistry.GetSmartInputParameterDisplayConfig(targetParameterName);
                
                // 更新卡片数据而不关闭窗口
                cardWindow.UpdateParameterData(
                    targetParameterName, 
                    currentValue, 
                    GetCurrentStepDisplayName(),
                    parameterConfig.Title,
                    parameterConfig.Description,
                    parameterConfig.ImagePath,
                    parameterConfig.Unit,
                    parameterConfig.MinValue,
                    parameterConfig.MaxValue
                );
                
                LogMessage($"卡片导航到参数: {targetParameterName}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"卡片参数导航失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 自动触发执行
        /// </summary>
        public void TriggerAutoExecution(string parameterName, double newValue)
        {
            try
            {
                // 更新对应TextBox的值
                if (inputParameterControls.ContainsKey(currentStep) && 
                    inputParameterControls[currentStep].ContainsKey(parameterName))
                {
                    var textBox = inputParameterControls[currentStep][parameterName];
                    
                    // 🎯 修复：根据参数配置的步长来决定格式化精度
                    string formattedValue;
                    if (Math.Abs(newValue % 1) < 0.0001) // 判断是否为整数
                    {
                        formattedValue = ((long)Math.Round(newValue)).ToString(); // 整数格式
                        LogMessage($"[TriggerAutoExecution] {parameterName} - 整数格式化: {newValue} -> '{formattedValue}'", LogLevel.Info);
                    }
                    else
                    {
                        // 根据参数配置的步长决定精度，默认为F2
                        var stepSize = GetParameterStepSize(parameterName);
                        string format = GetFormatStringByStepSize(stepSize);
                        formattedValue = newValue.ToString(format);
                        LogMessage($"[TriggerAutoExecution] {parameterName} - 小数格式化(步长={stepSize}, 格式={format}): {newValue} -> '{formattedValue}'", LogLevel.Info);
                    }
                    
                    textBox.Text = formattedValue;
                    LogMessage($"[TriggerAutoExecution] {parameterName} - TextBox.Text已设置为: '{textBox.Text}'", LogLevel.Info);
                }

                // 延迟执行，避免频繁触发
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(300);
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    ExecuteUnifiedFlow(GetSafeStepName(currentStep));
                };
                timer.Start();
                
                LogMessage($"自动执行触发: {parameterName} = {newValue}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"自动执行失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 检查是否是文本输入参数（需要键盘直接输入，不使用智能卡片）
        /// </summary>
        private bool IsTextInputParameter(string parameterName)
        {
            // 明确列出需要键盘输入的参数名
            var textInputParameters = new[] { 
                "模板名", "模板名称", "templatename", "template_name",
                "备注", "备注信息", "comment", "remarks", "note", "notes",
                "描述", "说明", "description", "desc",
                "路径", "path", "file", "文件", "image", "图像", "picture", "图片", "browse", "浏览"
            };
            
            return textInputParameters.Any(keyword => 
                parameterName?.ToLower().Contains(keyword.ToLower()) == true);
        }

        /// <summary>
        /// 获取参数的步长配置
        /// </summary>
        private double GetParameterStepSize(string parameterName)
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SmartInputConfigs.json");
                
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var configs = JsonSerializer.Deserialize<Dictionary<string, SmartInputParameterConfiguration>>(json);
                    
                    if (configs != null)
                    {
                        // 尝试找到匹配的配置
                        var parameterKey = $"{GetSafeStepName(currentStep)}_{parameterName}";
                        
                        if (configs.ContainsKey(parameterKey))
                        {
                            return configs[parameterKey].StepSize;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"获取参数步长失败: {ex.Message}", LogLevel.Warning);
            }
            
            // 默认返回0.01步长（F2精度）
            return 0.01;
        }

        /// <summary>
        /// 根据步长获取格式化字符串
        /// </summary>
        private string GetFormatStringByStepSize(double stepSize)
        {
            if (stepSize >= 1)
                return "F0";
            else if (stepSize >= 0.1)
                return "F1";
            else if (stepSize >= 0.01)
                return "F2";
            else if (stepSize >= 0.001)
                return "F3";
            else
                return "F4";
        }

        /// <summary>
        /// 获取当前步骤的显示名称
        /// </summary>
        public string GetCurrentStepDisplayName()
        {
            try
            {
                if (IsValidStepIndex(currentStep) && stepConfigurations != null && 
                    currentStep < stepConfigurations.Count)
                {
                    return stepConfigurations[currentStep].DisplayName;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"获取当前步骤显示名称失败: {ex.Message}", LogLevel.Warning);
            }

            return "未知步骤";
        }

        /// <summary>
        /// 初始化3D视图控件
        /// </summary>
        private void Initialize3DView()
        {
            try
            {
                _3DViewHost_Template.Child = _threeDViewHostChild;
                LogMessage("3D视图控件已初始化", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"初始化3D视图控件失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 切换到3D视图
        /// </summary>
        private void SwitchTo3DView()
        {
            try
            {
                SingleImageContainer.Visibility = Visibility.Collapsed;
                MultiImageContainer.Visibility = Visibility.Collapsed;
                ThreeDContainer.Visibility = Visibility.Visible;
                
                LogMessage("已切换到3D视图", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"切换到3D视图失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 切换到VM视图
        /// </summary>
        private void SwitchToVMView()
        {
            try
            {
                ThreeDContainer.Visibility = Visibility.Collapsed;
                
                // 根据当前步骤决定显示单图还是多图
                if (currentStep == 0) // 图片选择步骤显示多图
                {
                    SingleImageContainer.Visibility = Visibility.Collapsed;
                    MultiImageContainer.Visibility = Visibility.Visible;
                }
                else // 其他步骤显示单图
                {
                    SingleImageContainer.Visibility = Visibility.Visible;
                    MultiImageContainer.Visibility = Visibility.Collapsed;
                }
                
                LogMessage("已切换到VM视图", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"切换到VM视图失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 3D配置：创建3D模板按钮点击事件
        /// </summary>
        private void Create3DTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string templateName;
                string threeDDirectory = GetCurrentLoaded3DImageDirectoryForLJD(out templateName);

                var guideWindow = new Create3DTemplateGuideWindow(templateName, threeDDirectory)
                {
                    Owner = Application.Current?.MainWindow
                };

                bool? dialogResult = guideWindow.ShowDialog();
                if (dialogResult == true)
                {
                    TryOpenLJDeveloper();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"创建3D模板操作失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"创建3D模板操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetCurrentLoaded3DImageDirectoryForLJD(out string templateName)
        {
            templateName = null;

            try
            {
                templateName = currentTemplate?.TemplateName;
                if (string.IsNullOrWhiteSpace(templateName))
                {
                    templateName = PageManager.Page1Instance?.CurrentTemplateName;
                }

                if (string.IsNullOrWhiteSpace(templateName))
                {
                    templateName = "DefaultTemplate";
                }

                // 用户需求：复制“当前加载的路径的3D图片所在路径”（即当前图像组的3D目录）
                string directory = null;
                if (_currentImageGroup != null)
                {
                    string threeDFilePath = null;
                    if (!string.IsNullOrWhiteSpace(_currentImageGroup.HeightImagePath))
                    {
                        threeDFilePath = _currentImageGroup.HeightImagePath;
                    }
                    else if (!string.IsNullOrWhiteSpace(_currentImageGroup.GrayImagePath))
                    {
                        threeDFilePath = _currentImageGroup.GrayImagePath;
                    }

                    if (!string.IsNullOrWhiteSpace(threeDFilePath))
                    {
                        directory = Path.GetDirectoryName(threeDFilePath);
                    }
                }

                // 兜底：如果当前未加载3D图，则回落到模板目录（仍方便用户定位）
                if (string.IsNullOrWhiteSpace(directory))
                {
                    string safeTemplateName = string.Join("_", templateName.Split(Path.GetInvalidFileNameChars()));
                    directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", safeTemplateName, "3D");
                }

                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch
                {
                    // 忽略创建目录失败：仍允许复制路径给用户手动处理
                }

                return directory;
            }
            catch
            {
                templateName = templateName ?? "DefaultTemplate";
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "DefaultTemplate", "3D");
            }
        }

        private void TryOpenLJDeveloper()
        {
            // 用户要求固定打开此路径（如不存在，则提示并不抛异常）
            string[] candidates =
            {
                @"C:\Program Files\KEYENCE\LJ Developer\bin\LJ_Developer.exe",
                @"C:\Program Files\KEYENCE\LJ Developer\bin\LJ_Developer",
                @"C:\Program Files (x86)\KEYENCE\LJ Developer\bin\LJ_Developer.exe",
                @"C:\Program Files (x86)\KEYENCE\LJ Developer\bin\LJ_Developer"
            };

            string exePath = null;
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    exePath = candidate;
                    break;
                }
            }

            if (string.IsNullOrEmpty(exePath))
            {
                MessageBox.Show(
                    "未找到基恩士LJ Developer可执行文件。\n\n" +
                    "请确认已安装：C:\\Program Files\\KEYENCE\\LJ Developer\\bin\\LJ_Developer.exe",
                    "未找到程序",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };

                System.Diagnostics.Process.Start(processStartInfo);
                LogMessage($"已打开LJ Developer: {exePath}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"打开LJ Developer失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开LJ Developer失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TrySelectLJDeveloperUserProjectSourcePath(out string projectSourcePath)
        {
            projectSourcePath = null;

            string userRoot = @"C:\Users\Public\Documents\KEYENCE\LJ Developer\User";
            string useRootFallback = @"C:\Users\Public\Documents\KEYENCE\LJ Developer\Use";
            if (!Directory.Exists(userRoot) && Directory.Exists(useRootFallback))
            {
                userRoot = useRootFallback;
            }

            if (!Directory.Exists(userRoot))
            {
                MessageBox.Show(
                    $"未找到LJ Developer的User目录：\n{userRoot}",
                    "路径不存在",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            string[] projectDirs;
            try
            {
                projectDirs = Directory.GetDirectories(userRoot);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"读取User目录失败: {ex.Message}\n\n{userRoot}",
                    "读取失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            var projectNames = new List<string>();
            foreach (var dir in projectDirs)
            {
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    projectNames.Add(name);
                }
            }

            if (projectNames.Count == 0)
            {
                MessageBox.Show(
                    $"未在目录中发现任何项目文件夹：\n{userRoot}",
                    "未找到项目",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }

            projectNames.Sort(StringComparer.OrdinalIgnoreCase);

            var selectWindow = new LJDeveloperUserProjectSelectWindow(projectNames)
            {
                Owner = Application.Current?.MainWindow
            };

            if (selectWindow.ShowDialog() != true)
            {
                return false;
            }

            string selectedProjectName = selectWindow.SelectedProjectName;
            if (string.IsNullOrWhiteSpace(selectedProjectName))
            {
                return false;
            }

            string sourcePath = Path.Combine(userRoot, selectedProjectName, "source");
            if (!Directory.Exists(sourcePath))
            {
                MessageBox.Show(
                    $"已选择项目：{selectedProjectName}\n\n但未找到source目录：\n{sourcePath}",
                    "source不存在",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            projectSourcePath = sourcePath;
            return true;
        }

        /// <summary>
        /// 3D设置工具参数按钮点击事件 - 绑定到StaticMeasureEx实例
        /// 使用当前采集的图像进行参数配置，并自动应用DeltaPosition偏移使检测区域与当前图像位置匹配
        /// </summary>
        private void SetToolParameter3D_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    "3D配置/工具参数设置已迁移为独立进程（Host/Tool）。\n当前版本主程序不再直接加载/调用Keyence 3D实例。",
                    "3D提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                LogMessage("3D工具参数设置已跳过（3D已解耦，需在Host/Tool内配置）", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"打开3D工具参数设置失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开3D工具参数设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 3D设定判定对象按钮点击事件 - 绑定到StaticMeasureEx实例
        /// </summary>
        private void SetJudgement3D_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    "3D判定对象设置已迁移为独立进程（Host/Tool）。\n当前版本主程序不再直接加载/调用Keyence 3D实例。",
                    "3D提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                LogMessage("3D判定对象设置已跳过（3D已解耦，需在Host/Tool内配置）", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"打开3D判定设置失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开3D判定设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 3D设定输出对象按钮点击事件 - 绑定到StaticMeasureEx实例
        /// </summary>
        private void SetDataExport3D_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    "3D输出对象设置已迁移为独立进程（Host/Tool）。\n当前版本主程序不再直接加载/调用Keyence 3D实例。",
                    "3D提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                LogMessage("3D输出对象设置已跳过（3D已解耦，需在Host/Tool内配置）", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"打开3D数据输出设置失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开3D数据输出设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新当前模板名称显示
        /// </summary>
        private void UpdateCurrentTemplateNameDisplay()
        {
            try
            {
                // 获取当前模板名称
                string templateName = currentTemplate?.TemplateName ?? PageManager.Page1Instance?.CurrentTemplateName ?? "未知模板";
                
                // 更新导航栏显示
                if (CurrentTemplateNameDisplay != null)
                {
                    CurrentTemplateNameDisplay.Text = templateName;
                }
                
                LogMessage($"当前模板名称显示已更新: {templateName}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"更新当前模板名称显示失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 🔧 同步应用Page1的3D颜色配置到模板配置界面的3D视图
        /// 复用Page1的完备颜色配置功能，确保两个3D视图使用相同的显示效果
        /// 可从外部调用来实时同步颜色配置变化
        /// </summary>
        public void ApplyPage1ColorConfigToTemplateView()
        {
            // 3D颜色配置已迁移到独立进程（Host/Tool），主进程不直接操作Keyence 3D视图。
            LogMessage("3D视图颜色配置同步已跳过（3D已解耦）。", LogLevel.Info);
        }

        /// <summary>
        /// 初始化自动寻BLK相关控件的状态
        /// </summary>
        private void InitializeAutoBlkRelatedControls()
        {
            try
            {
                // 添加"将测量值设为基准值"按钮
                AddMeasurementToReferenceButton();

                // 检查当前步骤是否有自动寻BLK参数控件
                if (inputParameterControls.ContainsKey(currentStep) &&
                    inputParameterControls[currentStep].ContainsKey("自动寻BLK"))
                {
                    var autoBlkTextBox = inputParameterControls[currentStep]["自动寻BLK"];

                    // 获取当前自动寻BLK的状态
                    bool isAutoBlkEnabled = bool.TryParse(autoBlkTextBox.Text, out bool result) && result;

                    // 更新相关控件状态
                    UpdateRelatedControlsState(isAutoBlkEnabled);

                    LogManager.Info($"初始化自动寻BLK联动控件状态: {(isAutoBlkEnabled ? "启用" : "禁用")}", "模板配置");
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"初始化自动寻BLK相关控件状态失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 初始化银面几何尺寸相关控件的状态
        /// </summary>
        private void InitializeCoatingGeometryRelatedControls()
        {
            try
            {
                // 添加"将测量值设为基准值"按钮
                AddCoatingMeasurementToReferenceButton();

                LogManager.Info("初始化银面几何尺寸相关控件状态完成", "模板配置");
            }
            catch (Exception ex)
            {
                LogManager.Warning($"初始化银面几何尺寸相关控件状态失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 添加"将测量值设为基准值"按钮到基准值设定分组
        /// </summary>
        private void AddMeasurementToReferenceButton()
        {
            try
            {
                // 查找基准值设定分组的Expander
                var baseValueExpander = FindBaseValueGroupExpander();
                if (baseValueExpander == null)
                {
                    LogManager.Warning("未找到基准值设定分组，无法添加按钮", "模板配置");
                    return;
                }

                // 获取分组容器
                var parameterContainer = baseValueExpander.Content as StackPanel;
                if (parameterContainer == null)
                {
                    LogManager.Warning("基准值设定分组容器异常，无法添加按钮", "模板配置");
                    return;
                }

                // 创建按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(5, 10, 5, 5),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // 创建"将测量值设为基准值"按钮
                var setMeasurementButton = new Button
                {
                    Content = "将测量值设为基准值",
                    Width = 150,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)), // 蓝色背景
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    ToolTip = "点击后将检测结果中的当前测量值（BLK长度、BLK宽度、PAD_BLK距离）设置为基准值"
                };

                // 添加按钮样式和鼠标悬停效果
                setMeasurementButton.MouseEnter += (s, e) =>
                {
                    setMeasurementButton.Background = new SolidColorBrush(Color.FromRgb(41, 128, 185));
                };
                setMeasurementButton.MouseLeave += (s, e) =>
                {
                    setMeasurementButton.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                };

                // 绑定按钮点击事件
                setMeasurementButton.Click += SetMeasurementToReference_Click;

                buttonPanel.Children.Add(setMeasurementButton);

                // 将按钮面板添加到参数容器的最前面
                parameterContainer.Children.Insert(0, buttonPanel);

                LogManager.Info("成功添加'将测量值设为基准值'按钮", "模板配置");
            }
            catch (Exception ex)
            {
                LogManager.Error($"添加'将测量值设为基准值'按钮失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 添加银面几何尺寸"将测量值设为基准值"按钮到基准值设定分组
        /// </summary>
        private void AddCoatingMeasurementToReferenceButton()
        {
            try
            {
                // 查找基准值设定分组的Expander
                var baseValueExpander = FindBaseValueGroupExpander();
                if (baseValueExpander == null)
                {
                    LogManager.Warning("未找到基准值设定分组，无法添加银面几何尺寸按钮", "模板配置");
                    return;
                }

                // 获取分组容器
                var parameterContainer = baseValueExpander.Content as StackPanel;
                if (parameterContainer == null)
                {
                    LogManager.Warning("基准值设定分组容器异常，无法添加银面几何尺寸按钮", "模板配置");
                    return;
                }

                // 创建按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(5, 10, 5, 5),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // 创建"将测量值设为基准值"按钮
                var setCoatingMeasurementButton = new Button
                {
                    Content = "将测量值设为基准值",
                    Width = 150,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(46, 125, 50)), // 绿色背景，与晶片按钮区分
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(27, 94, 32)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    ToolTip = "点击后将检测结果中的当前测量值（镀膜长、镀膜宽、镀膜中心X）设置为基准值"
                };

                // 添加按钮样式和鼠标悬停效果
                setCoatingMeasurementButton.MouseEnter += (s, e) =>
                {
                    setCoatingMeasurementButton.Background = new SolidColorBrush(Color.FromRgb(27, 94, 32));
                };
                setCoatingMeasurementButton.MouseLeave += (s, e) =>
                {
                    setCoatingMeasurementButton.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                };

                // 绑定按钮点击事件
                setCoatingMeasurementButton.Click += SetCoatingMeasurementToReference_Click;

                buttonPanel.Children.Add(setCoatingMeasurementButton);

                // 将按钮面板添加到参数容器的最前面
                parameterContainer.Children.Insert(0, buttonPanel);

                LogManager.Info("成功添加银面几何尺寸'将测量值设为基准值'按钮", "模板配置");
            }
            catch (Exception ex)
            {
                LogManager.Error($"添加银面几何尺寸'将测量值设为基准值'按钮失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 查找基准值设定分组的Expander控件
        /// </summary>
        /// <returns></returns>
        private Expander FindBaseValueGroupExpander()
        {
            try
            {
                // 遍历InputParametersPanel中的所有子控件
                foreach (var child in InputParametersPanel.Children)
                {
                    if (child is Expander expander)
                    {
                        // 检查Header文本
                        if (expander.Header is TextBlock headerTextBlock &&
                            headerTextBlock.Text == "基准值设定")
                        {
                            return expander;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"查找基准值设定分组失败: {ex.Message}", "模板配置");
            }

            return null;
        }

        /// <summary>
        /// "将测量值设为基准值"按钮点击事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetMeasurementToReference_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogManager.Info("开始执行将测量值设为基准值操作", "模板配置");

                // 从检测结果读取当前测量值并更新基准值
                var updateResults = ReadCurrentMeasurementsAndUpdateReference();

                if (updateResults.Any())
                {
                    // 显示更新结果
                    var resultMessage = string.Join("\n", updateResults);
                    MessageBox.Show($"基准值更新完成：\n\n{resultMessage}",
                        "更新成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    LogManager.Info($"基准值更新成功: {resultMessage}", "模板配置");
                }
                else
                {
                    MessageBox.Show("未找到可更新的基准值参数或检测结果数据不可用\n\n可能原因：\n• 检测结果为空，请先执行检测\n• 所需检测项目（BLK长度、BLK宽度、PAD_BLK距离）数值为空\n• 检测项目名称不匹配",
                        "更新失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LogManager.Warning("未找到可更新的基准值参数", "模板配置");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"设置基准值时发生错误: {ex.Message}";
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Error(errorMessage, "模板配置");
            }
        }

        /// <summary>
        /// 从检测结果读取当前测量值并更新基准值参数
        /// </summary>
        /// <returns>更新结果列表</returns>
        private List<string> ReadCurrentMeasurementsAndUpdateReference()
        {
            var results = new List<string>();

            try
            {
                // 从检测结果DataGrid读取数据，而不是从VM全局变量
                var configDataGrid = this.FindName("ConfigDataGrid") as DataGrid;
                if (configDataGrid == null)
                {
                    LogManager.Error("未找到ConfigDataGrid控件", "模板配置");
                    return results;
                }

                var dataSource = configDataGrid.ItemsSource;
                if (dataSource == null)
                {
                    LogManager.Warning("检测结果DataGrid无数据", "模板配置");
                    return results;
                }

                // 将数据源转换为DetectionItem列表
                IList<DetectionItem> detectionItems = null;
                if (dataSource is IList<DetectionItem> listItems)
                {
                    detectionItems = listItems;
                }
                else if (dataSource is IEnumerable<DetectionItem> enumerableItems)
                {
                    detectionItems = enumerableItems.ToList();
                }

                if (detectionItems == null || detectionItems.Count == 0)
                {
                    LogManager.Warning("检测结果DataGrid中没有检测项目数据", "模板配置");
                    return results;
                }

                // 定义需要更新的基准值参数及其对应的检测结果项目名称
                var referenceParameters = new Dictionary<string, ReferenceParameterInfo>
                {
                    {
                        "晶片长(um)",
                        new ReferenceParameterInfo
                        {
                            VmVariableName = "BLK长度", // 检测结果中的项目名称
                            UnitConversion = (value) => value.ToString("F2"), // 检测结果已经是微米，无需转换
                            Description = "晶片长度"
                        }
                    },
                    {
                        "晶片宽(um)",
                        new ReferenceParameterInfo
                        {
                            VmVariableName = "BLK宽度", // 检测结果中的项目名称
                            UnitConversion = (value) => value.ToString("F2"), // 检测结果已经是微米，无需转换
                            Description = "晶片宽度"
                        }
                    },
                    {
                        "BLK-PKG_距离(um)",
                        new ReferenceParameterInfo
                        {
                            VmVariableName = "PAD_BLK距离", // 检测结果中的项目名称
                            UnitConversion = (value) => value.ToString("F2"), // 检测结果已经是微米，无需转换
                            Description = "BLK-PKG距离"
                        }
                    }
                };

                // 检查当前步骤的参数控件
                if (!inputParameterControls.ContainsKey(currentStep))
                {
                    LogManager.Warning("当前步骤没有参数控件", "模板配置");
                    return results;
                }

                var currentStepControls = inputParameterControls[currentStep];

                foreach (var paramPair in referenceParameters)
                {
                    var parameterName = paramPair.Key;
                    var paramInfo = paramPair.Value;

                    try
                    {
                        // 在检测结果中查找对应的项目
                        var detectionItem = detectionItems.FirstOrDefault(item =>
                            string.Equals(item.Name, paramInfo.VmVariableName, StringComparison.OrdinalIgnoreCase));

                        if (detectionItem == null)
                        {
                            LogManager.Warning($"检测结果中未找到项目'{paramInfo.VmVariableName}'", "模板配置");
                            continue;
                        }

                        // 检查检测结果值是否为空
                        if (string.IsNullOrWhiteSpace(detectionItem.Value))
                        {
                            LogManager.Warning($"检测结果项目'{paramInfo.VmVariableName}'的数值为空", "模板配置");
                            continue;
                        }

                        // 转换数值
                        if (!double.TryParse(detectionItem.Value, out double numericValue))
                        {
                            LogManager.Warning($"检测结果项目'{paramInfo.VmVariableName}'的值'{detectionItem.Value}'无法转换为数值", "模板配置");
                            continue;
                        }

                        // 应用单位转换（检测结果已经是微米，通常不需要转换）
                        string convertedValue = paramInfo.UnitConversion(numericValue);

                        // 更新UI控件
                        if (currentStepControls.ContainsKey(parameterName))
                        {
                            var textBox = currentStepControls[parameterName];
                            string oldValue = textBox.Text;
                            textBox.Text = convertedValue;

                            results.Add($"{paramInfo.Description}: {oldValue} → {convertedValue}");
                            LogManager.Info($"参数更新: {parameterName} = {convertedValue} (来源检测结果:{paramInfo.VmVariableName}={detectionItem.Value})", "模板配置");
                        }
                        else
                        {
                            LogManager.Warning($"未找到参数控件: {parameterName}", "模板配置");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"处理参数'{parameterName}'时出错: {ex.Message}", "模板配置");
                    }
                }

                // 如果没有成功更新任何参数，添加空值警告
                if (results.Count == 0)
                {
                    var missingItems = new List<string>();
                    foreach (var paramPair in referenceParameters)
                    {
                        var detectionItem = detectionItems.FirstOrDefault(item =>
                            string.Equals(item.Name, paramPair.Value.VmVariableName, StringComparison.OrdinalIgnoreCase));

                        if (detectionItem == null)
                        {
                            missingItems.Add($"'{paramPair.Value.VmVariableName}'(未找到)");
                        }
                        else if (string.IsNullOrWhiteSpace(detectionItem.Value))
                        {
                            missingItems.Add($"'{paramPair.Value.VmVariableName}'(数值为空)");
                        }
                    }

                    if (missingItems.Any())
                    {
                        LogManager.Warning($"以下检测项目无法读取数值: {string.Join(", ", missingItems)}", "模板配置");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"读取检测结果失败: {ex.Message}", "模板配置");
            }

            return results;
        }

        /// <summary>
        /// 基准值参数信息类
        /// </summary>
        private class ReferenceParameterInfo
        {
            /// <summary>
            /// 检测结果中的项目名称
            /// </summary>
            public string VmVariableName { get; set; }

            /// <summary>
            /// 单位转换函数（从检测结果值转换为UI显示值）
            /// </summary>
            public Func<double, string> UnitConversion { get; set; }

            /// <summary>
            /// 参数描述
            /// </summary>
            public string Description { get; set; }
        }

        /// <summary>
        /// 更新自动寻BLK相关控件的状态
        /// </summary>
        /// <param name="isAutoBlkEnabled">是否启用自动寻BLK</param>
        private void UpdateRelatedControlsState(bool isAutoBlkEnabled)
        {
            try
            {
                // 需要禁用/启用的参数名称列表
                var relatedParams = new List<string>
                {
                    "BLK基准边（左，下）ROI高度(pix)",
                    "BLK对边（右，上）ROI高度(pix)"
                };

                // 当启用自动寻BLK时，AI搜索框高度不允许输入（逻辑相反）
                var aiSearchParam = "AI搜索框高度";

                // 检查当前是否存在参数控件
                if (inputParameterControls.ContainsKey(currentStep))
                {
                    var currentStepControls = inputParameterControls[currentStep];

                    // 处理BLK相关参数：当启用自动寻BLK时禁用
                    foreach (var paramName in relatedParams)
                    {
                        if (currentStepControls.ContainsKey(paramName))
                        {
                            var textBox = currentStepControls[paramName];
                            textBox.IsReadOnly = isAutoBlkEnabled;
                            textBox.IsEnabled = !isAutoBlkEnabled; // 禁止点击操作

                            if (isAutoBlkEnabled)
                            {
                                // 设置为灰色背景，表示禁用状态
                                textBox.Background = System.Windows.Media.Brushes.LightGray;
                                textBox.Foreground = System.Windows.Media.Brushes.DarkGray;
                            }
                            else
                            {
                                // 恢复正常状态
                                textBox.Background = System.Windows.Media.Brushes.White;
                                textBox.Foreground = System.Windows.Media.Brushes.Black;
                            }
                        }
                    }

                    // 处理AI搜索框高度参数：当启用自动寻BLK时允许输入，不启用时禁用
                    if (currentStepControls.ContainsKey(aiSearchParam))
                    {
                        var aiTextBox = currentStepControls[aiSearchParam];
                        aiTextBox.IsReadOnly = !isAutoBlkEnabled; // 逻辑相反：启用自动寻BLK时允许输入
                        aiTextBox.IsEnabled = isAutoBlkEnabled; // 禁止点击操作

                        if (isAutoBlkEnabled)
                        {
                            // 启用自动寻BLK时，AI搜索框允许输入（正常状态）
                            aiTextBox.Background = System.Windows.Media.Brushes.White;
                            aiTextBox.Foreground = System.Windows.Media.Brushes.Black;
                        }
                        else
                        {
                            // 未启用自动寻BLK时，AI搜索框禁用（灰色状态）
                            aiTextBox.Background = System.Windows.Media.Brushes.LightGray;
                            aiTextBox.Foreground = System.Windows.Media.Brushes.DarkGray;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"更新自动寻BLK相关控件状态失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 初始化AI胶点相关控件的状态
        /// </summary>
        private void InitializeAiGluePointRelatedControls()
        {
            try
            {
                // 检查当前步骤是否有AI胶点参数控件
                if (inputParameterControls.ContainsKey(currentStep) &&
                    inputParameterControls[currentStep].ContainsKey("AI胶点"))
                {
                    var aiGluePointTextBox = inputParameterControls[currentStep]["AI胶点"];

                    // 获取当前AI胶点的状态
                    bool isAiGluePointEnabled = bool.TryParse(aiGluePointTextBox.Text, out bool result) && result;

                    // 更新相关控件状态
                    UpdateGluePointRelatedControlsState(isAiGluePointEnabled);

                    LogManager.Info($"初始化AI胶点联动控件状态: {(isAiGluePointEnabled ? "启用" : "禁用")}", "模板配置");
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"初始化AI胶点相关控件状态失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 初始化破损算法相关控件状态
        /// </summary>
        private void InitializeDamageAlgorithmRelatedControls()
        {
            try
            {
                // 检查当前步骤是否有破损算法参数控件
                if (inputParameterControls.ContainsKey(currentStep))
                {
                    var currentStepControls = inputParameterControls[currentStep];

                    if (currentStepControls.ContainsKey("传统破损算法") ||
                        currentStepControls.ContainsKey("AI破损算法"))
                    {
                        // 更新相关控件状态
                        UpdateDamageAlgorithmRelatedControlsState();

                        LogManager.Info("初始化破损算法联动控件状态完成", "模板配置");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"初始化破损算法相关控件状态失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 更新破损算法相关控件的状态
        /// </summary>
        private void UpdateDamageAlgorithmRelatedControlsState()
        {
            try
            {
                // 需要控制的参数名称
                var damageGrayThresholdParam = "破损灰度阈值";

                // 检查当前是否存在参数控件
                if (inputParameterControls.ContainsKey(currentStep))
                {
                    var currentStepControls = inputParameterControls[currentStep];

                    // 获取两个算法勾选框的状态
                    bool isTraditionalEnabled = false;
                    bool isAiEnabled = false;

                    if (currentStepControls.ContainsKey("传统破损算法"))
                    {
                        var traditionalTextBox = currentStepControls["传统破损算法"];
                        isTraditionalEnabled = bool.TryParse(traditionalTextBox.Text, out bool result) && result;
                    }

                    if (currentStepControls.ContainsKey("AI破损算法"))
                    {
                        var aiTextBox = currentStepControls["AI破损算法"];
                        isAiEnabled = bool.TryParse(aiTextBox.Text, out bool result) && result;
                    }

                    // 处理破损灰度阈值参数：当仅勾选AI破损算法时禁用
                    if (currentStepControls.ContainsKey(damageGrayThresholdParam))
                    {
                        var textBox = currentStepControls[damageGrayThresholdParam];

                        // 仅当只勾选AI破损算法时禁用（即AI算法勾选且传统算法未勾选）
                        bool shouldDisable = isAiEnabled && !isTraditionalEnabled;

                        textBox.IsReadOnly = shouldDisable;
                        textBox.IsEnabled = !shouldDisable;

                        if (shouldDisable)
                        {
                            // 设置为灰色背景，表示禁用状态
                            textBox.Background = System.Windows.Media.Brushes.LightGray;
                            textBox.Foreground = System.Windows.Media.Brushes.DarkGray;
                        }
                        else
                        {
                            // 恢复正常状态
                            textBox.Background = System.Windows.Media.Brushes.White;
                            textBox.Foreground = System.Windows.Media.Brushes.Black;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"更新破损算法相关控件状态失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 更新AI胶点相关控件的状态
        /// </summary>
        /// <param name="isAiGluePointEnabled">是否启用AI胶点</param>
        private void UpdateGluePointRelatedControlsState(bool isAiGluePointEnabled)
        {
            try
            {
                // 需要禁用/启用的参数名称
                var gluePointEdgeThresholdParam = "胶点边缘阈值";
                var gluePointProbabilityThresholdParam = "胶点概率阈值";

                // 检查当前是否存在参数控件
                if (inputParameterControls.ContainsKey(currentStep))
                {
                    var currentStepControls = inputParameterControls[currentStep];

                    // 处理胶点边缘阈值参数：当启用AI胶点时禁用
                    if (currentStepControls.ContainsKey(gluePointEdgeThresholdParam))
                    {
                        var textBox = currentStepControls[gluePointEdgeThresholdParam];
                        textBox.IsReadOnly = isAiGluePointEnabled;
                        textBox.IsEnabled = !isAiGluePointEnabled; // 禁止点击操作

                        if (isAiGluePointEnabled)
                        {
                            // 设置为灰色背景，表示禁用状态
                            textBox.Background = System.Windows.Media.Brushes.LightGray;
                            textBox.Foreground = System.Windows.Media.Brushes.DarkGray;
                        }
                        else
                        {
                            // 恢复正常状态
                            textBox.Background = System.Windows.Media.Brushes.White;
                            textBox.Foreground = System.Windows.Media.Brushes.Black;
                        }
                    }

                    // 处理胶点概率阈值参数：逻辑与胶点边缘阈值相反，当启用AI胶点时启用，禁用时禁用
                    if (currentStepControls.ContainsKey(gluePointProbabilityThresholdParam))
                    {
                        var textBox = currentStepControls[gluePointProbabilityThresholdParam];
                        textBox.IsReadOnly = !isAiGluePointEnabled;
                        textBox.IsEnabled = isAiGluePointEnabled; // AI胶点启用时才可编辑

                        if (!isAiGluePointEnabled)
                        {
                            // AI胶点未启用时，设置为灰色背景，表示禁用状态
                            textBox.Background = System.Windows.Media.Brushes.LightGray;
                            textBox.Foreground = System.Windows.Media.Brushes.DarkGray;
                        }
                        else
                        {
                            // AI胶点启用时，恢复正常状态
                            textBox.Background = System.Windows.Media.Brushes.White;
                            textBox.Foreground = System.Windows.Media.Brushes.Black;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"更新AI胶点相关控件状态失败: {ex.Message}", "模板配置");
            }
        }

        /// <summary>
        /// 镀膜几何尺寸步骤：将测量值设为基准值的点击事件处理器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetCoatingMeasurementToReference_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogManager.Info("开始执行镀膜几何尺寸将测量值设为基准值操作", "模板配置");

                // 从检测结果读取当前测量值并更新基准值
                var updateResults = ReadCoatingMeasurementsAndUpdateReference();

                if (updateResults.Any())
                {
                    // 显示更新结果
                    var resultMessage = string.Join("\n", updateResults);
                    MessageBox.Show($"基准值更新完成：\n\n{resultMessage}",
                        "更新成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    LogManager.Info($"镀膜几何尺寸基准值更新成功: {resultMessage}", "模板配置");
                }
                else
                {
                    MessageBox.Show("未找到可更新的基准值参数或检测结果数据不可用\n\n可能原因：\n• 检测结果为空，请先执行检测\n• 所需检测项目（镀膜长、镀膜宽、镀膜中心X）数值为空\n• 检测项目名称不匹配",
                        "更新失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LogManager.Warning("未找到可更新的镀膜基准值参数", "模板配置");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"设置镀膜基准值时发生错误: {ex.Message}";
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Error(errorMessage, "模板配置");
            }
        }

        /// <summary>
        /// 从检测结果读取镀膜测量值并更新基准值参数
        /// </summary>
        /// <returns>更新结果列表</returns>
        private List<string> ReadCoatingMeasurementsAndUpdateReference()
        {
            var results = new List<string>();

            try
            {
                // 从检测结果DataGrid读取数据
                var configDataGrid = this.FindName("ConfigDataGrid") as DataGrid;
                if (configDataGrid == null)
                {
                    LogManager.Error("未找到ConfigDataGrid控件", "模板配置");
                    return results;
                }

                var dataSource = configDataGrid.ItemsSource;
                if (dataSource == null)
                {
                    LogManager.Warning("检测结果DataGrid无数据", "模板配置");
                    return results;
                }

                // 将数据源转换为DetectionItem列表
                IList<DetectionItem> detectionItems = null;
                if (dataSource is IList<DetectionItem> listItems)
                {
                    detectionItems = listItems;
                }
                else if (dataSource is IEnumerable<DetectionItem> enumerableItems)
                {
                    detectionItems = enumerableItems.ToList();
                }

                if (detectionItems == null || detectionItems.Count == 0)
                {
                    LogManager.Warning("检测结果DataGrid中没有检测项目数据", "模板配置");
                    return results;
                }

                // 定义镀膜几何尺寸需要更新的基准值参数及其对应的检测结果项目名称
                var coatingReferenceParameters = new Dictionary<string, ReferenceParameterInfo>
                {
                    {
                        "镀膜设定长度",
                        new ReferenceParameterInfo
                        {
                            VmVariableName = "镀膜长", // 检测结果中的项目名称
                            UnitConversion = (value) => ((int)Math.Round(value)).ToString(), // 转换为整数，不保留小数
                            Description = "镀膜长度"
                        }
                    },
                    {
                        "镀膜设定宽度",
                        new ReferenceParameterInfo
                        {
                            VmVariableName = "镀膜宽", // 检测结果中的项目名称
                            UnitConversion = (value) => ((int)Math.Round(value)).ToString(), // 转换为整数，不保留小数
                            Description = "镀膜宽度"
                        }
                    },
                    {
                        "镀膜设定中心X",
                        new ReferenceParameterInfo
                        {
                            VmVariableName = "镀膜中心X", // 检测结果中的项目名称
                            UnitConversion = (value) => ((int)Math.Round(value)).ToString(), // 转换为整数，不保留小数
                            Description = "镀膜中心X"
                        }
                    }
                };

                // 检查当前步骤的参数控件
                if (!inputParameterControls.ContainsKey(currentStep))
                {
                    LogManager.Warning("当前步骤没有参数控件", "模板配置");
                    return results;
                }

                var currentStepControls = inputParameterControls[currentStep];

                foreach (var paramPair in coatingReferenceParameters)
                {
                    var parameterName = paramPair.Key;
                    var paramInfo = paramPair.Value;

                    try
                    {
                        // 在检测结果中查找对应的项目
                        var detectionItem = detectionItems.FirstOrDefault(item =>
                            string.Equals(item.Name, paramInfo.VmVariableName, StringComparison.OrdinalIgnoreCase));

                        if (detectionItem == null)
                        {
                            LogManager.Warning($"检测结果中未找到项目'{paramInfo.VmVariableName}'", "模板配置");
                            continue;
                        }

                        // 检查检测结果值是否为空
                        if (string.IsNullOrWhiteSpace(detectionItem.Value))
                        {
                            LogManager.Warning($"检测结果项目'{paramInfo.VmVariableName}'的数值为空", "模板配置");
                            continue;
                        }

                        // 尝试解析测量值
                        if (!double.TryParse(detectionItem.Value, out double measurementValue))
                        {
                            LogManager.Warning($"检测结果项目'{paramInfo.VmVariableName}'的数值格式无效: {detectionItem.Value}", "模板配置");
                            continue;
                        }

                        // 查找对应的基准值参数控件
                        if (!currentStepControls.ContainsKey(parameterName))
                        {
                            LogManager.Warning($"当前步骤中未找到基准值参数'{parameterName}'的控件", "模板配置");
                            continue;
                        }

                        var parameterControl = currentStepControls[parameterName];
                        if (parameterControl == null)
                        {
                            LogManager.Warning($"基准值参数'{parameterName}'的控件为空", "模板配置");
                            continue;
                        }

                        // 格式化并设置新的基准值
                        var newValue = paramInfo.UnitConversion(measurementValue);
                        parameterControl.Text = newValue;

                        // 触发文本更改事件以确保数据绑定
                        parameterControl.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

                        // 添加到更新结果
                        results.Add($"{paramInfo.Description}: {detectionItem.Value} → {newValue}");

                        LogManager.Info($"已更新基准值参数'{parameterName}': {detectionItem.Value} → {newValue}", "模板配置");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"更新基准值参数'{parameterName}'时发生错误: {ex.Message}", "模板配置");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"读取镀膜测量值并更新基准值时发生错误: {ex.Message}", "模板配置");
                throw;
            }

            return results;
        }
    }

    /// <summary>
    /// 带滚动条的详细信息对话框
    /// </summary>
    public class ScrollableMessageWindow : Window
    {
        private readonly TextBox _contentTextBox;
        private MessageBoxResult _result = MessageBoxResult.Cancel;
        private DispatcherTimer _autoCloseTimer;

        public MessageBoxResult Result => _result;

        public ScrollableMessageWindow(string title, string message, bool showCancel = true, string okButtonText = "确定", string cancelButtonText = "取消", int autoCloseSeconds = 0)
        {
            Title = title;
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            
            // 创建主Grid布局
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // 创建滚动文本框
            _contentTextBox = new TextBox
            {
                Text = message,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
                Margin = new Thickness(10),
                Padding = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
            };
            
            Grid.SetRow(_contentTextBox, 0);
            mainGrid.Children.Add(_contentTextBox);
            
            // 创建按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };
            
            // 确定按钮
            var okButton = new Button
            {
                Content = okButtonText,
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsDefault = !showCancel // 如果没有取消按钮，确定按钮为默认
            };
            okButton.Click += (s, e) => { _result = MessageBoxResult.OK; Close(); };
            buttonPanel.Children.Add(okButton);
            
            // 取消按钮（可选）
            if (showCancel)
            {
                var cancelButton = new Button
                {
                    Content = cancelButtonText,
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(5),
                    IsCancel = true
                };
                cancelButton.Click += (s, e) => { _result = MessageBoxResult.Cancel; Close(); };
                buttonPanel.Children.Add(cancelButton);
            }
            
            Grid.SetRow(buttonPanel, 1);
            mainGrid.Children.Add(buttonPanel);
            
            Content = mainGrid;

            // 设置窗口图标和样式
            try
            {
                if (Application.Current.MainWindow != null)
                {
                    Owner = Application.Current.MainWindow;
                    Icon = Application.Current.MainWindow.Icon;
                }
            }
            catch
            {
                // 忽略图标设置错误
            }

            // 设置自动关闭定时器
            if (autoCloseSeconds > 0)
            {
                SetupAutoClose(autoCloseSeconds, okButton);
            }
        }

        /// <summary>
        /// 设置自动关闭定时器
        /// </summary>
        private void SetupAutoClose(int seconds, Button okButton)
        {
            var originalButtonText = okButton.Content.ToString();
            var countdown = seconds;

            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _autoCloseTimer.Tick += (sender, args) =>
            {
                countdown--;
                okButton.Content = $"{originalButtonText} ({countdown}s)";

                if (countdown <= 0)
                {
                    _autoCloseTimer.Stop();
                    _result = MessageBoxResult.OK;
                    Close();
                }
            };

            // 初始显示倒计时
            okButton.Content = $"{originalButtonText} ({countdown}s)";
            _autoCloseTimer.Start();

            // 如果用户点击按钮，停止定时器
            okButton.Click += (s, e) =>
            {
                if (_autoCloseTimer != null && _autoCloseTimer.IsEnabled)
                {
                    _autoCloseTimer.Stop();
                }
            };
        }

        /// <summary>
        /// 显示滚动消息对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <param name="showCancel">是否显示取消按钮</param>
        /// <param name="okButtonText">确定按钮文本</param>
        /// <param name="cancelButtonText">取消按钮文本</param>
        /// <param name="autoCloseSeconds">自动关闭秒数，0表示不自动关闭</param>
        /// <returns>用户选择结果</returns>
        public static MessageBoxResult Show(string message, string title, bool showCancel = true, string okButtonText = "确定", string cancelButtonText = "取消", int autoCloseSeconds = 0)
        {
            var window = new ScrollableMessageWindow(title, message, showCancel, okButtonText, cancelButtonText, autoCloseSeconds);
            window.ShowDialog();
            return window.Result;
        }
    }


}
