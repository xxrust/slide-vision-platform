using System;
using System.Threading.Tasks;
using System.Windows;
using WpfApp2.UI.Controls;
using WpfApp2.SMTGPIO;
using System.Linq;
using System.Collections.Generic;
using System.IO; // Added for Path and File operations

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 智能分析窗口管理器
    /// 负责管理分析窗口的显示、自动弹窗告警等功能
    /// </summary>
    public static class SmartAnalysisWindowManager
    {
        private static SmartAnalysisWidget _currentWidget = null;
        private static Window _currentWindow = null;
        private static Window _ownerWindow = null;
        private static bool _ownerWindowDisabled = false;
        private static bool _releasedOwnerForFloating = false;
        private static string _lastAlertMessage = "";
        private static DateTime _lastAlertTime = DateTime.MinValue;
        private static bool _isProcessingAlert = false; // 防重复处理标志
        private static bool _isFloatingModeActive = false;
        private static bool _floatingWindowTemporarilyHidden = false;
        private static Rect? _floatingWindowCachedBounds = null;
        private static bool _isPage1CurrentlyVisible = true;

        /// <summary>
        /// 显示数据分析窗口（手动调用）
        /// </summary>
        /// <param name="page1Instance">Page1实例，用于获取分析数据</param>
        public static void ShowAnalysisWindow(Page1 page1Instance)
        {
            try
            {
                // 如果窗口已存在，则激活并置顶
                if (_currentWindow != null && _currentWindow.IsLoaded)
                {
                    _currentWindow.Activate();
                    _currentWindow.WindowState = WindowState.Normal;
                    return;
                }

                // 创建新的分析窗口
                CreateAnalysisWindow(page1Instance, false);
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示数据分析窗口失败: {ex.Message}");
                MessageBox.Show($"打开数据分析窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 自动告警弹窗：窗口弹出且触发O2（NG超限）输出1秒后自动复位




        public static void UpdateFloatingMode(bool isFloatingMode)
        {
            _isFloatingModeActive = isFloatingMode;

            if (!isFloatingMode && _floatingWindowTemporarilyHidden && _isPage1CurrentlyVisible)
            {
                RestoreFloatingWindowInternal();
            }

            if (_ownerWindow == null || !_ownerWindowDisabled)
            {
                return;
            }

            try
            {
                if (isFloatingMode)
                {
                    _ownerWindow.IsEnabled = true;
                    _releasedOwnerForFloating = true;
                }
                else if (_releasedOwnerForFloating)
                {
                    _ownerWindow.IsEnabled = false;
                    _releasedOwnerForFloating = false;
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"切换悬浮模式时恢复主窗口失败: {ex.Message}");
            }
        }



        // 自动告警弹窗：窗口弹出且触发O2（NG超限）输出1秒后自动复位

        public static async Task ShowAutoAlertWindow(Page1 page1Instance, string alertMessage)
        {
            // 防重复：正在处理告警时直接返回
            if (_isProcessingAlert) return;
            
            try
            {
                _isProcessingAlert = true;

                // 若已有窗口，直接更新告警并置顶，同时异步触发O2脉冲
                if (_currentWindow != null && _currentWindow.IsVisible)
                {
                    var alertItem = ExtractItemNameFromAlertMessage(alertMessage);
                    _currentWidget?.ShowAlert(alertItem, alertMessage);
                    _currentWindow.Activate();
                    _ = TriggerAlertOutput(); // 异步触发O2脉冲，不阻塞UI
                    return;
                }

                // 新开窗：先弹出质量分析表，再异步触发O2脉冲
                CreateAnalysisWindow(page1Instance, true, alertMessage);
                _ = TriggerAlertOutput(); // 异步触发O2脉冲，不阻塞UI

                RecordAlertMessage(alertMessage);
                _lastAlertMessage = alertMessage;
                _lastAlertTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                LogManager.Error($"自动告警弹窗失败: {ex.Message}");
            }
            finally
            {
                _isProcessingAlert = false; // 处理完成，清除标记
            }
        }
        /// <param name="alertMessage">告警消息（可选）</param>


        private static void CreateAnalysisWindow(Page1 page1Instance, bool isAutoAlert, string alertMessage = null)

        {

            _currentWindow = new Window

            {

                Title = isAutoAlert ? "智能分析告警" : "智能分析",

                Width = 750,

                Height = 700,

                WindowStartupLocation = WindowStartupLocation.CenterScreen,

                ResizeMode = ResizeMode.CanResize,

                ShowInTaskbar = true

            };



            if (isAutoAlert)

            {

                _currentWindow.Topmost = true;

                _currentWindow.WindowState = WindowState.Normal;

            }



            _ownerWindow = null;

            _ownerWindowDisabled = false;

            _releasedOwnerForFloating = false;

            if (!isAutoAlert && page1Instance != null)

            {

                _ownerWindow = Window.GetWindow(page1Instance) ?? Application.Current?.MainWindow;

                if (_ownerWindow != null && _ownerWindow.IsEnabled)

                {

                    _ownerWindow.IsEnabled = false;

                    _ownerWindowDisabled = true;

                }

            }



            if (_ownerWindow != null)

            {

                _currentWindow.Owner = _ownerWindow;

            }



            _currentWidget = new SmartAnalysisWidget();

            _currentWindow.Content = _currentWidget;



            if (isAutoAlert && !string.IsNullOrEmpty(alertMessage))

            {

                _currentWindow.Loaded += (s, e) =>

                {

                    try

                    {

                        var alertItem = ExtractItemNameFromAlertMessage(alertMessage);

                        _currentWidget?.ShowAlert(alertItem, alertMessage);

                    }

                    catch (Exception ex)

                    {

                        LogManager.Error($"显示告警信息失败: {ex.Message}");

                    }

                };

            }



            var isAlertWindow = isAutoAlert;

            _currentWindow.Closed += (s, e) =>

            {

                try

                {

                    _currentWidget?.MainPage?.SaveChartState();

                }

                catch (Exception ex)

                {

                    LogManager.Error($"保存图表状态失败: {ex.Message}");

                }



                try

                {

                    _currentWidget?.MainPage?.ClearImportedData();

                }

                catch (Exception ex)

                {

                    LogManager.Error($"清理导入数据失败: {ex.Message}");

                }



                ClearAlertCounters();

                _currentWidget = null;

                _currentWindow = null;
                _floatingWindowTemporarilyHidden = false;
                _floatingWindowCachedBounds = null;
                _isFloatingModeActive = false;



                if (_ownerWindow != null)

                {

                    try

                    {

                        if (_ownerWindowDisabled || _releasedOwnerForFloating)

                        {

                            _ownerWindow.IsEnabled = true;

                        }

                    }

                    catch (Exception ex)

                    {

                        LogManager.Warning($"恢复主窗口状态失败: {ex.Message}");

                    }

                }



                _ownerWindow = null;

                _ownerWindowDisabled = false;

                _releasedOwnerForFloating = false;



                if (isAlertWindow)

                {

                    _lastAlertMessage = string.Empty;

                    _lastAlertTime = DateTime.MinValue;

                    LogManager.Info("告警窗口已关闭，告警状态已重置");

                }

            };



            _currentWindow.Show();

        }

        /// <summary>
        /// 触发告警输出（IO信号）- O2 NG超限警告
        /// </summary>
        private static async Task TriggerAlertOutput()
        {
            try
            {
                if (!IOManager.IsInitialized)
                {
                    LogManager.Warning("IOManager未初始化，无法触发告警输出");
                    return;
                }

                // 🚨 触发O2输出1秒 - NG超限警告
                LogManager.Info("触发告警输出 O2 信号 - NG超限警告");
                IOManager.SetSingleOutput(3, true);   // 设置O2为高电平
                await Task.Delay(1000);               // 延时1秒
                IOManager.SetSingleOutput(3, false);  // 设置O2为低电平，自动复位
                
                LogManager.Info("NG超限警告输出信号完成 - O2已自动复位");
            }
            catch (Exception ex)
            {
                LogManager.Error($"触发NG超限警告输出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否需要自动告警
        /// </summary>
        /// <param name="page1Instance">Page1实例</param>
        /// <returns>如果需要告警，返回告警消息；否则返回null</returns>
        public static async Task<string> CheckAutoAlert(Page1 page1Instance)
        {
            try
            {
                // 获取告警设置
                var alertSettings = AlertSettings.Load();
                if (!alertSettings.IsEnabled)
                {
                    return null; // 告警功能未启用
                }

                // 使用DetectionDataStorage获取分析数据
                var itemNames = DetectionDataStorage.GetAllItemNames();
                if (itemNames.Count == 0)
                {
                    return null; // 没有数据
                }

                var totalCount = DetectionDataStorage.GetTotalRecordCount();
                if (totalCount == 0)
                {
                    return null; // 没有数据
                }

                // 获取最近的数据记录
                var recentRecords = DetectionDataStorage.GetRecentRecords(alertSettings.StatisticsCycle);
                
                // 构建分析数据格式 - 根据数据量决定使用范围
                var analysisData = new List<(string ItemName, List<double> Values, double LowerLimit, double UpperLimit)>();
                foreach (var itemName in itemNames)
                {
                    // 根据统计周期概念：如果数据量>=统计周期，使用最近的统计周期数据；否则使用全部数据
                    var dataCount = totalCount >= alertSettings.StatisticsCycle ? alertSettings.StatisticsCycle : totalCount;
                    var values = DetectionDataStorage.GetItemValues(itemName, dataCount);
                    var limits = DetectionDataStorage.GetItemLimits(itemName);
                    
                    if (values != null && values.Any())
                    {
                        analysisData.Add((itemName, values, limits.LowerLimit, limits.UpperLimit));
                    }
                }

                if (analysisData.Count == 0)
                {
                    return null; // 没有有效的分析数据
                }

                // 将DetectionRecord转换为DetectionItem格式用于连续NG检查
                // 针对每个项目应用对应的策略组合
                var consecutiveStates = alertSettings.Profiles.Any(p => p.EnableConsecutiveNGAnalysis)
                    ? CalculateConsecutiveNgState(recentRecords)
                    : new Dictionary<string, ConsecutiveNgState>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in analysisData)
                {
                    var profile = alertSettings.GetProfileForItem(item.ItemName);
                    if (profile == null || !profile.HasAnyStrategyEnabled)
                    {
                        continue;
                    }

                    if (profile.EnableCountAnalysis)
                    {
                        var unprocessedCount = GetUnprocessedOutOfRangeCount(item.ItemName, alertSettings.StatisticsCycle);
                        if (unprocessedCount >= profile.OutOfRangeThreshold)
                        {
                            return $"计数告警: {item.ItemName} 超出范围次数({unprocessedCount})达到阈值({profile.OutOfRangeThreshold}) [策略: {profile.Name}]";
                        }
                    }

                    if (profile.EnableProcessCapabilityAnalysis)
                    {
                        var capability = CalculateCapabilityStats(item.Values, item.LowerLimit, item.UpperLimit);
                        if (capability.IsValid && capability.SampleCount >= alertSettings.MinSampleSize)
                        {
                            if (capability.Ca > profile.CAThreshold)
                            {
                                return $"过程能力告警: {item.ItemName} CA({capability.Ca:F3})超过阈值({profile.CAThreshold:F3}) [样本:{capability.SampleCount}] [策略: {profile.Name}]";
                            }

                            if (capability.Cp < profile.CPThreshold)
                            {
                                return $"过程能力告警: {item.ItemName} CP({capability.Cp:F3})低于阈值({profile.CPThreshold:F3}) [样本:{capability.SampleCount}] [策略: {profile.Name}]";
                            }

                            if (capability.Cpk < profile.CPKThreshold)
                            {
                                return $"过程能力告警: {item.ItemName} CPK({capability.Cpk:F3})低于阈值({profile.CPKThreshold:F3}) [样本:{capability.SampleCount}] [策略: {profile.Name}]";
                            }
                        }
                    }

                    if (profile.EnableConsecutiveNGAnalysis && consecutiveStates.TryGetValue(item.ItemName, out var consecutiveState))
                    {
                        if (consecutiveState.Count >= profile.ConsecutiveNGThreshold)
                        {
                            var defectLabel = string.IsNullOrWhiteSpace(consecutiveState.LastDefectType)
                                ? item.ItemName
                                : consecutiveState.LastDefectType;

                            return $"连续NG告警: {item.ItemName} 连续{consecutiveState.Count}次检测到缺陷({defectLabel}) [策略: {profile.Name}]";
                        }
                    }
                }

                return null; // 未触发告警
            }
            catch (Exception ex)
            {
                LogManager.Error($"检查自动告警失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将DetectionRecord转换为DetectionItem列表（用于连续NG检查）
        /// </summary>
        /// <summary>
        /// 检查并触发自动告警（统一判定后调用）
        /// </summary>
        /// <param name="page1Instance">Page1实例</param>
        public static async void CheckAndTriggerAutoAlert(Page1 page1Instance)
        {
            try
            {
                var alertMessage = await CheckAutoAlert(page1Instance);
                if (!string.IsNullOrEmpty(alertMessage))
                {
                    LogManager.Warning($"触发自动告警: {alertMessage}");
                    await ShowAutoAlertWindow(page1Instance, alertMessage);
                }
                else
                {
                    // 记录告警检查状态，帮助用户诊断问题
                    LogAlertCheckStatus();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"自动告警检查和触发失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录告警检查状态，帮助用户诊断为什么没有触发告警
        /// </summary>
        private static void LogAlertCheckStatus()
        {
            try
            {
                var alertSettings = AlertSettings.Load();
                var totalCount = DetectionDataStorage.GetTotalRecordCount();
                var itemNames = DetectionDataStorage.GetAllItemNames();
                var recentData = DetectionDataStorage.GetRecentRecords(alertSettings.StatisticsCycle);
                var allItems = new HashSet<string>(itemNames, StringComparer.OrdinalIgnoreCase);
                foreach (var key in alertSettings.ItemProfileBindings.Keys)
                {
                    allItems.Add(key);
                }

                LogManager.Info("\U0001F4CA \u544a\u8b66\u68c0\u67e5\u72b6\u6001\u5206\u6790\uff1a");
                LogManager.Info($"  \u544a\u8b66\u542f\u7528: {alertSettings.IsEnabled}");
                LogManager.Info($"  \u603b\u6570\u636e\u91cf: {totalCount}");
                LogManager.Info($"  \u7edf\u8ba1\u5468\u671f\u8bbe\u7f6e: {alertSettings.StatisticsCycle}");
                LogManager.Info($"  \u6700\u5c0f\u6837\u672c\u6570\u8bbe\u7f6e: {alertSettings.MinSampleSize}");
                LogManager.Info($"  \u7b56\u7565\u7ec4\u5408\u6570\u91cf: {alertSettings.StrategyProfiles.Count}");

                var totalExplicitBindings = alertSettings.ItemProfileBindings.Count;
                foreach (var profile in alertSettings.StrategyProfiles)
                {
                    var tags = new List<string>();
                    if (profile.EnableCountAnalysis)
                    {
                        tags.Add($"\u8d85\u9650\u9608\u503c={profile.OutOfRangeThreshold}");
                    }
                    if (profile.EnableProcessCapabilityAnalysis)
                    {
                        tags.Add($"\u8fc7\u7a0b\u80fd\u529b\u9608\u503c(CA={profile.CAThreshold:F3}, CP={profile.CPThreshold:F3}, CPK={profile.CPKThreshold:F3})");
                    }
                    if (profile.EnableConsecutiveNGAnalysis)
                    {
                        tags.Add($"\u8fde\u7eedNG\u9608\u503c={profile.ConsecutiveNGThreshold}");
                    }
                    if (!tags.Any())
                    {
                        tags.Add("\u672a\u542f\u7528\u4efb\u4f55\u7b56\u7565");
                    }

                    var explicitCount = alertSettings.ItemProfileBindings.Count(kv => string.Equals(kv.Value, profile.Id, StringComparison.OrdinalIgnoreCase));
                    string bindingSummary;
                    if (profile.IsDefault)
                    {
                        var implicitCount = Math.Max(allItems.Count - totalExplicitBindings, 0);
                        bindingSummary = $"\u7ed1\u5b9a\u9879\u76ee={explicitCount + implicitCount} (\u5176\u4e2d\u9ed8\u8ba4\u8986\u76d6 {implicitCount})";
                    }
                    else
                    {
                        bindingSummary = $"\u7ed1\u5b9a\u9879\u76ee={explicitCount}";
                    }

                    var profileLabel = profile.IsDefault ? $"{profile.Name} [\u9ed8\u8ba4]" : profile.Name;
                    LogManager.Info($"    {profileLabel}: {string.Join(", ", tags)}，{bindingSummary}");
                }

                if (!alertSettings.IsEnabled)
                {
                    LogManager.Info("\u274C \u544a\u8b66\u672a\u89e6\u53d1\uff1a\u544a\u8b66\u529f\u80fd\u672a\u542f\u7528");
                }
                else if (itemNames.Count == 0)
                {
                    LogManager.Info("\u274C \u544a\u8b66\u672a\u89e6\u53d1\uff1a\u6ca1\u6709\u68c0\u6d4b\u9879\u76ee\u6570\u636e");
                }
                else if (totalCount == 0)
                {
                    LogManager.Info("\u274C \u544a\u8b66\u672a\u89e6\u53d1\uff1a\u6ca1\u6709\u68c0\u6d4b\u6570\u636e");
                }
                else
                {
                    LogManager.Info("\u2705 \u6570\u636e\u6761\u4ef6\u6ee1\u8db3\uff0c\u4f46\u672a\u8fbe\u5230\u4efb\u4f55\u544a\u8b66\u9608\u503c");
                    foreach (var itemName in allItems)
                    {
                        var dataCount = totalCount >= alertSettings.StatisticsCycle ? alertSettings.StatisticsCycle : totalCount;
                        var values = DetectionDataStorage.GetItemValues(itemName, dataCount);
                        var limits = DetectionDataStorage.GetItemLimits(itemName);
                        var profile = alertSettings.GetProfileForItem(itemName);
                        var profileName = profile?.Name ?? "\u9ed8\u8ba4";

                        if (values != null && values.Any())
                        {
                            var outOfRangeCount = values.Count(v => v < limits.LowerLimit || v > limits.UpperLimit);
                            var outOfRangeRate = (double)outOfRangeCount / values.Count * 100;
                            LogManager.Info($"    {itemName} -> \u7ec4\u5408: {profileName}\uff0c\u6570\u636e {values.Count} \u6761\uff0c\u8d85\u9650 {outOfRangeCount} \u6b21 ({outOfRangeRate:F1}%)");
                        }
                        else
                        {
                            LogManager.Info($"    {itemName} -> \u7ec4\u5408: {profileName}\uff0c\u6682\u65e0\u7edf\u8ba1\u6570\u636e");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"\u8bb0\u5f55\u544a\u8b66\u72b6\u6001\u5931\u8d25: {ex.Message}");
            }
        }


        /// <summary>
        /// 公共方法：检查并显示当前告警设置状态（供调试使用）
        /// </summary>
        public static void DiagnoseAlertSettings()
        {
            try
            {
                var alertSettings = AlertSettings.Load();
                var totalCount = DetectionDataStorage.GetTotalRecordCount();
                var itemNames = DetectionDataStorage.GetAllItemNames();
                var recentData = DetectionDataStorage.GetRecentRecords(alertSettings.StatisticsCycle);
                var allItems = new HashSet<string>(itemNames, StringComparer.OrdinalIgnoreCase);
                foreach (var key in alertSettings.ItemProfileBindings.Keys)
                {
                    allItems.Add(key);
                }

                LogManager.Info("=== \U0001F4CA \u544a\u8b66\u7cfb\u7edf\u8bca\u65ad ===");
                LogManager.Info($"\u544a\u8b66\u542f\u7528\u72b6\u6001: {alertSettings.IsEnabled}");
                LogManager.Info($"\u7edf\u8ba1\u5468\u671f\u8bbe\u7f6e: {alertSettings.StatisticsCycle}");
                LogManager.Info($"\u6700\u5c0f\u6837\u672c\u6570\u8bbe\u7f6e: {alertSettings.MinSampleSize}");
                LogManager.Info($"\u7b56\u7565\u7ec4\u5408\u6570\u91cf: {alertSettings.StrategyProfiles.Count}");

                var totalExplicitBindings = alertSettings.ItemProfileBindings.Count;
                foreach (var profile in alertSettings.StrategyProfiles)
                {
                    var tags = new List<string>();
                    if (profile.EnableCountAnalysis)
                    {
                        tags.Add($"\u8d85\u9650\u9608\u503c={profile.OutOfRangeThreshold}");
                    }
                    if (profile.EnableProcessCapabilityAnalysis)
                    {
                        tags.Add($"\u8fc7\u7a0b\u80fd\u529b\u9608\u503c(CA={profile.CAThreshold:F3}, CP={profile.CPThreshold:F3}, CPK={profile.CPKThreshold:F3})");
                    }
                    if (profile.EnableConsecutiveNGAnalysis)
                    {
                        tags.Add($"\u8fde\u7eedNG\u9608\u503c={profile.ConsecutiveNGThreshold}");
                    }
                    if (!tags.Any())
                    {
                        tags.Add("\u672a\u542f\u7528\u4efb\u4f55\u7b56\u7565");
                    }

                    var explicitCount = alertSettings.ItemProfileBindings.Count(kv => string.Equals(kv.Value, profile.Id, StringComparison.OrdinalIgnoreCase));
                    string bindingSummary;
                    if (profile.IsDefault)
                    {
                        var implicitCount = Math.Max(allItems.Count - totalExplicitBindings, 0);
                        bindingSummary = $"\u7ed1\u5b9a\u9879\u76ee={explicitCount + implicitCount} (\u5176\u4e2d\u9ed8\u8ba4\u8986\u76d6 {implicitCount})";
                    }
                    else
                    {
                        bindingSummary = $"\u7ed1\u5b9a\u9879\u76ee={explicitCount}";
                    }

                    var profileLabel = profile.IsDefault ? $"{profile.Name} [\u9ed8\u8ba4]" : profile.Name;
                    LogManager.Info($"    {profileLabel}: {string.Join(", ", tags)}，{bindingSummary}");
                }

                LogManager.Info($"\u5f53\u524d\u603b\u6570\u636e\u91cf: {totalCount}");
                LogManager.Info($"\u5f53\u524d\u5468\u671f\u6570\u636e\u91cf: {recentData.Count}");
                LogManager.Info($"\u68c0\u6d4b\u9879\u76ee\u6570: {allItems.Count}");

                foreach (var itemName in allItems)
                {
                    var dataCount = totalCount >= alertSettings.StatisticsCycle ? alertSettings.StatisticsCycle : totalCount;
                    var values = DetectionDataStorage.GetItemValues(itemName, dataCount);
                    var limits = DetectionDataStorage.GetItemLimits(itemName);
                    var profile = alertSettings.GetProfileForItem(itemName);
                    var profileName = profile?.Name ?? "\u9ed8\u8ba4";

                    if (values != null && values.Any())
                    {
                        var outOfRangeCount = values.Count(v => v < limits.LowerLimit || v > limits.UpperLimit);
                        var outOfRangeRate = (double)outOfRangeCount / values.Count * 100;
                        LogManager.Info($"    {itemName} -> \u7ec4\u5408: {profileName}\uff0c\u6570\u636e {values.Count} \u6761\uff0c\u8d85\u9650 {outOfRangeCount} \u6b21 ({outOfRangeRate:F1}%)");
                    }
                    else
                    {
                        LogManager.Info($"    {itemName} -> \u7ec4\u5408: {profileName}\uff0c\u6682\u65e0\u7edf\u8ba1\u6570\u636e");
                    }
                }

                LogManager.Info($"\u5f53\u524d\u544a\u8b66\u7a97\u53e3\u72b6\u6001: {( _currentWindow?.IsVisible == true ? "\u663e\u793a\u4e2d" : "\u672a\u663e\u793a") }");
                LogManager.Info($"\u4e0a\u6b21\u544a\u8b66\u6d88\u606f: {(_lastAlertMessage ?? "\u65e0")}");
                LogManager.Info($"\u4e0a\u6b21\u544a\u8b66\u65f6\u95f4: {(_lastAlertTime == DateTime.MinValue ? "\u65e0" : _lastAlertTime.ToString("HH:mm:ss"))}");

                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AlertSettings.json");
                LogManager.Info($"\u914d\u7f6e\u6587\u4ef6\u8def\u5f84: {configPath}");
                LogManager.Info($"\u914d\u7f6e\u6587\u4ef6\u5b58\u5728: {File.Exists(configPath)}");
                if (File.Exists(configPath))
                {
                    var content = File.ReadAllText(configPath);
                    LogManager.Info($"\u914d\u7f6e\u6587\u4ef6\u5185\u5bb9: {content}");
                }

                LogManager.Info("=== \u8bca\u65ad\u7ed3\u675f ===");
            }
            catch (Exception ex)
            {
                LogManager.Error($"\u8bca\u65ad\u544a\u8b66\u8bbe\u7f6e\u5931\u8d25: {ex.Message}");
            }
        }


        private static CapabilitySnapshot CalculateCapabilityStats(List<double> values, double lowerLimit, double upperLimit)
        {
            try
            {
                if (values == null || values.Count == 0)
                {
                    return CapabilitySnapshot.Empty;
                }

                var validValues = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (validValues.Count == 0)
                {
                    return CapabilitySnapshot.Empty;
                }

                if (double.IsNaN(lowerLimit) || double.IsNaN(upperLimit) ||
                    lowerLimit == double.MinValue || upperLimit == double.MaxValue ||
                    upperLimit <= lowerLimit)
                {
                    return CapabilitySnapshot.Empty;
                }

                var average = validValues.Average();
                var variance = validValues.Sum(v => Math.Pow(v - average, 2)) / validValues.Count;
                var std = Math.Sqrt(variance);

                if (std <= 0)
                {
                    std = 1e-6;
                }

                var range = upperLimit - lowerLimit;
                var target = (upperLimit + lowerLimit) / 2.0;
                var ca = Math.Abs(average - target) / (range / 2.0);
                var cp = range / (6 * std);
                var cpk = Math.Min((upperLimit - average) / (3 * std), (average - lowerLimit) / (3 * std));

                return new CapabilitySnapshot
                {
                    Average = average,
                    StandardDeviation = std,
                    Ca = ca,
                    Cp = cp,
                    Cpk = cpk,
                    SampleCount = validValues.Count,
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                LogManager.Error($"CalculateCapabilityStats failed: {ex.Message}");
                return CapabilitySnapshot.Empty;
            }
        }

        private static Dictionary<string, ConsecutiveNgState> CalculateConsecutiveNgState(List<DetectionRecord> records)
        {
            var states = new Dictionary<string, ConsecutiveNgState>(StringComparer.OrdinalIgnoreCase);

            if (records == null || records.Count == 0)
            {
                return states;
            }

            foreach (var record in records.OrderBy(r => r.Timestamp))
            {
                foreach (var kvp in record.DetectionItems)
                {
                    var itemName = kvp.Key;
                    var value = kvp.Value;

                    if (!value.HasValidData)
                    {
                        continue;
                    }

                    if (!states.TryGetValue(itemName, out var state))
                    {
                        state = new ConsecutiveNgState();
                        states[itemName] = state;
                    }

                    if (value.IsOutOfRange)
                    {
                        state.Count++;
                        state.LastDefectType = string.IsNullOrWhiteSpace(record.DefectType) ? itemName : record.DefectType;
                    }
                    else
                    {
                        state.Count = 0;
                        state.LastDefectType = string.Empty;
                    }
                }
            }

            return states;
        }

        private sealed class CapabilitySnapshot
        {
            public static readonly CapabilitySnapshot Empty = new CapabilitySnapshot();

            public double Average { get; set; }
            public double StandardDeviation { get; set; }
            public double Ca { get; set; }
            public double Cp { get; set; }
            public double Cpk { get; set; }
            public int SampleCount { get; set; }
            public bool IsValid { get; set; } = false;
        }

        private sealed class ConsecutiveNgState
        {
            public int Count { get; set; }
            public string LastDefectType { get; set; } = string.Empty;
        }

        private static string ExtractItemNameFromAlertMessage(string alertMessage)
        {
            if (string.IsNullOrWhiteSpace(alertMessage))
            {
                return "未知项目";
            }

            try
            {
                var parts = alertMessage.Split(':');
                if (parts.Length > 1)
                {
                    var secondPart = parts[1].Trim();
                    if (!string.IsNullOrEmpty(secondPart))
                    {
                        var spaceParts = secondPart.Split(' ');
                        if (spaceParts.Length > 0 && !string.IsNullOrWhiteSpace(spaceParts[0]))
                        {
                            return spaceParts[0];
                        }
                    }
                }

                return "未知项目";
            }
            catch
            {
                return "未知项目";
            }
        }

        private static int GetUnprocessedOutOfRangeCount(string itemName, int windowSize)
        {
            try
            {
                var recentRecords = DetectionDataStorage.GetRecentRecords(windowSize);
                int unprocessedCount = 0;

                foreach (var record in recentRecords)
                {
                    if (record.DetectionItems.ContainsKey(itemName))
                    {
                        var itemValue = record.DetectionItems[itemName];
                        // 只计算超限且未处理告警的数据
                        if (itemValue.IsOutOfRange && !itemValue.IsAlertProcessed)
                        {
                            unprocessedCount++;
                        }
                    }
                }

                return unprocessedCount;
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取未处理超限次数失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 记录告警消息到告警记录管理器
        /// </summary>
        private static void RecordAlertMessage(string alertMessage)
        {
            try
            {
                var itemName = ExtractItemNameFromAlertMessage(alertMessage);
                var alertType = "未知类型";
                var details = "";
                var cleanedAlertContent = "";

                // 解析告警类型并提取纯净的告警内容
                if (alertMessage.Contains("计数告警") || alertMessage.Contains("超出范围次数"))
                {
                    alertType = "超限计数告警";
                    
                    // 提取阈值和超限次数
                    var countMatch = System.Text.RegularExpressions.Regex.Match(alertMessage, @"超出范围次数\((\d+)\)达到阈值\((\d+)\)");
                    if (countMatch.Success)
                    {
                        var outOfRangeCount = countMatch.Groups[1].Value;
                        var threshold = countMatch.Groups[2].Value;
                        details = $"超限: {outOfRangeCount}次";
                        cleanedAlertContent = $"超出范围次数({outOfRangeCount})达到阈值({threshold})";
                    }
                    else
                    {
                        // 备用匹配
                        var match = System.Text.RegularExpressions.Regex.Match(alertMessage, @"超出范围次数\((\d+)\)");
                        if (match.Success)
                        {
                            details = $"超限: {match.Groups[1].Value}次";
                            cleanedAlertContent = $"超出范围次数({match.Groups[1].Value})";
                        }
                    }
                }
                else if (alertMessage.Contains("连续NG告警"))
                {
                    alertType = "连续NG告警";
                    
                    // 提取连续次数和缺陷类型
                    var consecutiveMatch = System.Text.RegularExpressions.Regex.Match(alertMessage, @"连续(\d+)次检测到缺陷\(([^)]+)\)");
                    if (consecutiveMatch.Success)
                    {
                        var consecutiveCount = consecutiveMatch.Groups[1].Value;
                        var defectType = consecutiveMatch.Groups[2].Value;
                        details = $"连续: {consecutiveCount}次";
                        cleanedAlertContent = $"连续{consecutiveCount}次检测到缺陷({defectType})";
                    }
                }
                else if (alertMessage.Contains("过程能力") || alertMessage.Contains("统计分析"))
                {
                    alertType = "过程能力告警";
                    
                    // 提取CP/CPK等统计值
                    if (alertMessage.Contains("CP") || alertMessage.Contains("CPK") || alertMessage.Contains("CA"))
                    {
                        var statsMatch = System.Text.RegularExpressions.Regex.Match(alertMessage, @"(CP|CPK|CA)[^,]*");
                        if (statsMatch.Success)
                        {
                            details = statsMatch.Value;
                            cleanedAlertContent = alertMessage.Substring(alertMessage.IndexOf(':') + 1).Trim();
                            // 移除项目名部分
                            if (cleanedAlertContent.StartsWith(itemName))
                            {
                                cleanedAlertContent = cleanedAlertContent.Substring(itemName.Length).Trim();
                            }
                        }
                        else
                        {
                            details = "CP/CPK超出阈值";
                            cleanedAlertContent = "过程能力指标超出阈值";
                        }
                    }
                }

                // 如果没有成功提取纯净内容，使用原始消息去除前缀
                if (string.IsNullOrEmpty(cleanedAlertContent))
                {
                    cleanedAlertContent = alertMessage;
                    // 去除"计数告警: "或"连续NG告警: "等前缀
                    var colonIndex = cleanedAlertContent.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        cleanedAlertContent = cleanedAlertContent.Substring(colonIndex + 1).Trim();
                    }
                    // 去除项目名
                    if (cleanedAlertContent.StartsWith(itemName))
                    {
                        cleanedAlertContent = cleanedAlertContent.Substring(itemName.Length).Trim();
                    }
                }

                // 添加记录到告警记录管理器
                WpfApp2.UI.Controls.AlertRecordManager.AddRecord(new WpfApp2.UI.Controls.AlertRecord
                {
                    Timestamp = DateTime.Now,
                    ItemName = itemName,
                    AlertType = alertType,
                    AlertMessage = cleanedAlertContent, // 使用纯净的告警内容
                    Details = details
                });

                LogManager.Info($"✅ 已记录告警信息: {itemName} - {alertType} - {cleanedAlertContent}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"记录告警信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清空质量分析仪表板的数据源与UI状态
        /// </summary>
        public static bool ClearAnalysisData()
        {
            var success = true;

            try
            {
                DetectionDataStorage.ClearAllRecords();
                LogManager.Info("质量分析仪表板数据源已清空");

                ClearAlertCounters();

                void ResetDashboardUi()
                {
                    try
                    {
                        _currentWidget?.MainPage?.ClearAllAnalysisData();
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        LogManager.Error($"清空质量分析仪表板UI失败: {ex.Message}");
                    }
                }

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(ResetDashboardUi);
                }
                else
                {
                    ResetDashboardUi();
                }

                if (success)
                {
                    LogManager.Info("质量分析仪表板数据与UI已同步清空");
                }
            }
            catch (Exception ex)
            {
                success = false;
                LogManager.Error($"清空质量分析仪表板失败: {ex.Message}");
            }

            return success;
        }

        /// <summary>
        /// 处理Page1显隐变化，控制悬浮仪表板的自动隐藏与恢复
        /// </summary>
        public static void HandlePageVisibilityChange(bool isPageVisible)
        {
            void Execute()
            {
                if (_isPage1CurrentlyVisible == isPageVisible)
                {
                    return;
                }

                _isPage1CurrentlyVisible = isPageVisible;

                if (!_isFloatingModeActive || _currentWindow == null)
                {
                    return;
                }

                if (!isPageVisible)
                {
                    HideFloatingWindowForNavigation();
                }
                else
                {
                    RestoreFloatingWindowInternal();
                }
            }

            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.BeginInvoke(new Action(Execute));
                }
                else
                {
                    Execute();
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"处理Page1显隐状态失败: {ex.Message}");
            }
        }

        private static void HideFloatingWindowForNavigation()
        {
            if (_floatingWindowTemporarilyHidden || _currentWindow == null)
            {
                return;
            }

            try
            {
                _floatingWindowCachedBounds = _currentWindow.WindowState == WindowState.Normal
                    ? new Rect(_currentWindow.Left, _currentWindow.Top, _currentWindow.Width, _currentWindow.Height)
                    : _currentWindow.RestoreBounds;

                _currentWindow.Hide();
                _floatingWindowTemporarilyHidden = true;
                LogManager.Info("已在离开Page1时自动隐藏质量分析浮窗");
            }
            catch (Exception ex)
            {
                LogManager.Warning($"自动隐藏质量分析浮窗失败: {ex.Message}");
            }
        }

        private static void RestoreFloatingWindowInternal()
        {
            if (!_floatingWindowTemporarilyHidden || _currentWindow == null)
            {
                return;
            }

            try
            {
                if (_floatingWindowCachedBounds.HasValue)
                {
                    var bounds = _floatingWindowCachedBounds.Value;
                    _currentWindow.Left = bounds.Left;
                    _currentWindow.Top = bounds.Top;
                    _currentWindow.Width = bounds.Width;
                    _currentWindow.Height = bounds.Height;
                }

                _currentWindow.Show();
                _currentWindow.WindowState = WindowState.Normal;
                _floatingWindowCachedBounds = null;
                _floatingWindowTemporarilyHidden = false;
                LogManager.Info("已在返回Page1时恢复质量分析浮窗");
            }
            catch (Exception ex)
            {
                LogManager.Warning($"恢复质量分析浮窗失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清零告警计数器（用户关闭仪表板时调用）
        /// 新机制：将相关超限数据标记为"已处理"，而不是简单清除状态
        /// </summary>
        /// <param name="itemName">要清零的项目名，null表示清零所有项目</param>
        public static void ClearAlertCounters(string itemName = null)
        {
            try
            {
                var alertSettings = AlertSettings.Load();
                var windowSize = alertSettings.StatisticsCycle;

                if (string.IsNullOrEmpty(itemName))
                {
                    // 清零所有项目
                    var allItemNames = DetectionDataStorage.GetAllItemNames();
                    int totalMarked = 0;
                    
                    foreach (var item in allItemNames)
                    {
                        totalMarked += DetectionDataStorage.MarkOutOfRangeAsAlertProcessed(item, windowSize);
                    }
                    
                    LogManager.Info($"✅ 已清零所有项目的告警计数器，标记了 {totalMarked} 个超限数据为已处理");
                }
                else
                {
                    // 清零特定项目
                    var markedCount = DetectionDataStorage.MarkOutOfRangeAsAlertProcessed(itemName, windowSize);
                    LogManager.Info($"✅ 已清零项目 {itemName} 的告警计数器，标记了 {markedCount} 个超限数据为已处理");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"清零告警计数器失败: {ex.Message}");
            }
        }
    }
}

