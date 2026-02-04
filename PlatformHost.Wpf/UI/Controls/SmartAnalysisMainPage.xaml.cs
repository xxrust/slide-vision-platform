using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using ScottPlot;
using Microsoft.Win32;
using System.IO;
using System.Text;
using WpfApp2.UI.Models;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using Newtonsoft.Json;

namespace WpfApp2.UI.Controls
{
    public partial class SmartAnalysisMainPage : UserControl, INotifyPropertyChanged
    {
        #region 私有字段
        private string _currentSelectedItem = "";
        private int _currentDataCount = 1000;
        private Dictionary<string, List<double>> _analysisData = new Dictionary<string, List<double>>();
        private Dictionary<string, (double LowerLimit, double UpperLimit)> _itemLimits = new Dictionary<string, (double, double)>();
        private string _currentChartType = "BoxPlot"; // 保存当前图表类型状态
        
        // 统计数据
        private Dictionary<string, StatisticsData> _statisticsCache = new Dictionary<string, StatisticsData>();
        
        // 导入数据管理
        private bool _hasImportedData = false;
        private Dictionary<string, List<double>> _importedAnalysisData = new Dictionary<string, List<double>>();
        private Dictionary<string, (double LowerLimit, double UpperLimit)> _importedItemLimits = new Dictionary<string, (double, double)>();
        
        // 新增：支持多种上下限数据源
        private Dictionary<string, (double LowerLimit, double UpperLimit)> _documentLimits = new Dictionary<string, (double, double)>(); // 文档中的上下限
        private Dictionary<string, (double LowerLimit, double UpperLimit)> _currentRealTimeLimits = new Dictionary<string, (double, double)>(); // 当前实时数据的上下限
        private bool _usingDocumentLimits = true; // 当前是否使用文档上下限
        private bool _isFloatingMode = false;
        private GridLength _originalControlRowHeight;
        private GridLength _originalProjectRowHeight;
        private GridLength _originalChartRowHeight;
        private GridLength _originalStatisticsRowHeight;
        private double _originalControlRowMinHeight;
        private double _originalProjectRowMinHeight;
        private double _originalProjectRowMaxHeight;
        private double _originalChartRowMinHeight;
        private double _originalStatisticsRowMinHeight;
        
        // 上下限内存管理器 - 维护每个项目的最新有效上下限
        private static Dictionary<string, (double LowerLimit, double UpperLimit)> _limitsMemoryStorage = new Dictionary<string, (double, double)>();

        // 自动更新
        private DispatcherTimer _autoRefreshTimer;
        private bool _isAutoRefreshEnabled = false;
        private bool _isAutoRefreshInProgress = false;
        private readonly TimeSpan _autoRefreshInterval = TimeSpan.FromSeconds(3);
        #endregion

        #region 上下限内存管理方法
        /// <summary>
        /// 更新项目的上下限到内存存储
        /// 原则：存储最后一条数据的上下限，如果最后一条数据含无效值（不是0），则维持不变
        /// </summary>
        /// <param name="itemName">项目名称</param>
        /// <param name="upperLimitValues">上限值列表</param>
        /// <param name="lowerLimitValues">下限值列表</param>
        private void UpdateLimitsMemoryStorage(string itemName, List<double> upperLimitValues, List<double> lowerLimitValues)
        {
            try
            {
                if (upperLimitValues == null || lowerLimitValues == null || 
                    !upperLimitValues.Any() || !lowerLimitValues.Any())
                {
                    LogManager.Warning($"[LimitsMemory] 项目 {itemName} 上下限数据为空，跳过更新");
                    return;
                }

                // 从最后一条数据开始向前查找有效的上下限
                for (int i = upperLimitValues.Count - 1; i >= 0; i--)
                {
                    var upperLimit = upperLimitValues[i];
                    var lowerLimit = i < lowerLimitValues.Count ? lowerLimitValues[i] : 0;

                    // 检查是否为有效值（不为0且不为NaN/Infinity）
                    if (upperLimit != 0 && lowerLimit != 0 && 
                        !double.IsNaN(upperLimit) && !double.IsNaN(lowerLimit) &&
                        !double.IsInfinity(upperLimit) && !double.IsInfinity(lowerLimit) &&
                        upperLimit > lowerLimit) // 确保上限大于下限
                    {
                        _limitsMemoryStorage[itemName] = (lowerLimit, upperLimit);
                        LogManager.Info($"[LimitsMemory] 更新项目 {itemName} 上下限: [{lowerLimit:F3}, {upperLimit:F3}] (第{i+1}条数据)");
                        return;
                    }
                }

                // 如果没有找到有效的上下限，维持原有值不变
                if (_limitsMemoryStorage.ContainsKey(itemName))
                {
                    var existing = _limitsMemoryStorage[itemName];
                    LogManager.Info($"[LimitsMemory] 项目 {itemName} 无有效上下限，维持原值: [{existing.LowerLimit:F3}, {existing.UpperLimit:F3}]");
                }
                else
                {
                    LogManager.Warning($"[LimitsMemory] 项目 {itemName} 无有效上下限且无历史值，跳过");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[LimitsMemory] 更新项目 {itemName} 上下限失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取项目的内存存储上下限
        /// </summary>
        /// <param name="itemName">项目名称</param>
        /// <returns>上下限值，如果不存在则返回默认值</returns>
        private (double LowerLimit, double UpperLimit) GetLimitsFromMemoryStorage(string itemName)
        {
            if (_limitsMemoryStorage.ContainsKey(itemName))
            {
                return _limitsMemoryStorage[itemName];
            }
            
            // 如果内存中没有，尝试从DetectionDataStorage获取作为初始值
            var currentLimits = DetectionDataStorage.GetItemLimits(itemName);
            if (currentLimits.LowerLimit != double.MinValue && currentLimits.UpperLimit != double.MaxValue)
            {
                _limitsMemoryStorage[itemName] = currentLimits;
                LogManager.Info($"[LimitsMemory] 初始化项目 {itemName} 上下限: [{currentLimits.LowerLimit:F3}, {currentLimits.UpperLimit:F3}]");
                return currentLimits;
            }

            return (double.MinValue, double.MaxValue); // 默认值
        }
        #endregion

        #region 统计数据结构
        public class StatisticsData
        {
            public double Average { get; set; }
            public double StandardDeviation { get; set; }
            public double Ca { get; set; }        // 准确度指数
            public double Cp { get; set; }        // 精密度指数  
            public double Cpk { get; set; }       // 过程能力指数
            public int OutOfRangeCount { get; set; }
            public double OutOfRangeProbability { get; set; }
            public int SampleCount { get; set; }
        }
        #endregion

        #region 属性变更通知
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        public SmartAnalysisMainPage()
        {
            InitializeAutoRefreshTimer();
            InitializeComponent();
            CacheOriginalRowHeights();
            LoadSavedChartState(); // 先加载保存的状态
            InitializeCharts();     // 再初始化图表（会使用加载的状态）
            Unloaded += SmartAnalysisMainPage_Unloaded;
            // 不在构造函数中自动加载数据，等待外部调用LoadDetectionItems()
        }

        #region 初始化
        private void InitializeCharts()
        {
            try
            {
                SetupChartStyle();
                
                // 根据保存的状态显示对应图表
                ShowChart(_currentChartType);
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化图表失败: {ex.Message}");
            }
        }

        private void SetupChartStyle()
        {
            try
            {
                // 设置图表样式 - 使用ScottPlot 5.0正确的API
                BoxPlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
                BoxPlot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
                ControlChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
                ControlChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
                HistogramChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
                HistogramChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
                
                LogManager.Info("[SetupChart] 图表样式设置完成");
            }
            catch (Exception ex)
            {
                LogManager.Error($"设置图表样式失败: {ex.Message}");
            }
        }

        public void SetFloatingMode(bool isFloatingMode)
        {
            if (_isFloatingMode == isFloatingMode)
            {
                return;
            }

            _isFloatingMode = isFloatingMode;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ApplyFloatingModeLayout(isFloatingMode));
            }
            else
            {
                ApplyFloatingModeLayout(isFloatingMode);
            }

            LogManager.Info($"[SmartAnalysis] 悬浮模式切换: {isFloatingMode}");
        }

        private void CacheOriginalRowHeights()
        {
            if (ControlRow != null)
            {
                _originalControlRowHeight = ControlRow.Height;
                _originalControlRowMinHeight = ControlRow.MinHeight;
            }
            if (ProjectRow != null)
            {
                _originalProjectRowHeight = ProjectRow.Height;
                _originalProjectRowMinHeight = ProjectRow.MinHeight;
                _originalProjectRowMaxHeight = ProjectRow.MaxHeight;
            }
            if (ChartRow != null)
            {
                _originalChartRowHeight = ChartRow.Height;
                _originalChartRowMinHeight = ChartRow.MinHeight;
            }
            if (StatisticsRow != null)
            {
                _originalStatisticsRowHeight = StatisticsRow.Height;
                _originalStatisticsRowMinHeight = StatisticsRow.MinHeight;
            }
        }

        private void ApplyFloatingModeLayout(bool isFloatingMode)
        {
            if (ControlAreaGrid == null || StatisticsBorder == null || DataStatusPanel == null || ItemsBorder == null || ChartBorder == null)
            {
                return;
            }

            ControlAreaGrid.Visibility = isFloatingMode ? Visibility.Collapsed : Visibility.Visible;
            StatisticsBorder.Visibility = isFloatingMode ? Visibility.Collapsed : Visibility.Visible;
            DataStatusPanel.Visibility = isFloatingMode ? Visibility.Collapsed : Visibility.Visible;

            if (isFloatingMode)
            {
                ControlRow.Height = new GridLength(0);
                ControlRow.MinHeight = 0;
                ProjectRow.Height = GridLength.Auto;
                ProjectRow.MinHeight = 0;
                ProjectRow.MaxHeight = double.PositiveInfinity;
                ChartRow.Height = new GridLength(1, GridUnitType.Star);
                ChartRow.MinHeight = 150;
                StatisticsRow.Height = new GridLength(0);
                StatisticsRow.MinHeight = 0;
                ItemsBorder.Margin = new Thickness(5, 2, 5, 2);
                ChartBorder.Margin = new Thickness(5, 2, 5, 5);
                ItemsBorder.Background = Brushes.White;
                ChartBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                ControlRow.Height = _originalControlRowHeight;
                ControlRow.MinHeight = _originalControlRowMinHeight;
                ProjectRow.Height = _originalProjectRowHeight;
                ProjectRow.MinHeight = _originalProjectRowMinHeight;
                ProjectRow.MaxHeight = _originalProjectRowMaxHeight;
                ChartRow.Height = _originalChartRowHeight;
                ChartRow.MinHeight = _originalChartRowMinHeight;
                StatisticsRow.Height = _originalStatisticsRowHeight;
                StatisticsRow.MinHeight = _originalStatisticsRowMinHeight;
                ItemsBorder.Margin = new Thickness(10, 5, 10, 5);
                ChartBorder.Margin = new Thickness(10, 5, 10, 5);
                ItemsBorder.Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFA));
                ChartBorder.BorderThickness = new Thickness(1);
            }
        }

        private void UpdateProjectButtonHighlight(string selectedItem)
        {
            if (ItemButtonsPanel == null) return;

            foreach (var child in ItemButtonsPanel.Children)
            {
                if (child is StackPanel rowPanel)
                {
                    foreach (var element in rowPanel.Children)
                    {
                        if (element is Button button)
                        {
                            var itemName = button.Tag as string ?? button.Content?.ToString();
                            if (!string.IsNullOrEmpty(selectedItem) && string.Equals(itemName, selectedItem, StringComparison.OrdinalIgnoreCase))
                            {
                                button.Background = new SolidColorBrush(Color.FromRgb(255, 168, 74));
                            }
                            else
                            {
                                button.ClearValue(Button.BackgroundProperty);
                            }
                        }
                    }
                }
            }
        }

        private void InitializeAutoRefreshTimer()
        {
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = _autoRefreshInterval
            };
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
        }

        private void StartAutoRefresh()
        {
            if (_autoRefreshTimer == null)
            {
                InitializeAutoRefreshTimer();
            }

            if (_isAutoRefreshEnabled && _autoRefreshTimer.IsEnabled)
            {
                return;
            }

            _isAutoRefreshEnabled = true;
            _autoRefreshTimer.Start();
            LogManager.Info($"[SmartAnalysis] 自动更新已开启（间隔 {_autoRefreshInterval.TotalSeconds:F0}s）");
        }

        private void StopAutoRefresh()
        {
            if (_autoRefreshTimer == null)
            {
                _isAutoRefreshEnabled = false;
                return;
            }

            bool wasRunning = _autoRefreshTimer.IsEnabled || _isAutoRefreshEnabled;
            _isAutoRefreshEnabled = false;
            _autoRefreshTimer.Stop();

            if (wasRunning)
            {
                LogManager.Info("[SmartAnalysis] 自动更新已关闭");
            }
        }

        private void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (!_isAutoRefreshEnabled || _isAutoRefreshInProgress)
            {
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(_currentSelectedItem))
                {
                    return;
                }

                _isAutoRefreshInProgress = true;
                RefreshData();
            }
            catch (Exception ex)
            {
                LogManager.Error($"[SmartAnalysis] 自动刷新失败: {ex.Message}");
            }
            finally
            {
                _isAutoRefreshInProgress = false;
            }
        }

        private void SmartAnalysisMainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAutoRefresh();
        }

        public void LoadDetectionItems()
        {
            try
            {
                // 如果有导入数据，优先使用导入数据
                if (_hasImportedData)
                {
                    UpdateUIWithImportedDataWithFocusFilter();
                    return;
                }

                // 使用DetectionDataStorage获取数据（与DataAnalysisPage保持一致）
                var allItemNames = DetectionDataStorage.GetAllItemNames();
                
                // 获取关注的项目列表
                var focusedProjects = WpfApp2.UI.Models.FocusedProjectsManager.GetFocusedProjects();
                
                // 过滤出关注的项目（如果没有设置关注项目，则显示所有项目）
                var focusedProjectCount = focusedProjects.Count;
                var itemNames = focusedProjectCount == 0 ? allItemNames : allItemNames.Where((Func<string, bool>)(name => focusedProjects.Contains(name))).ToList();
                
                LogManager.Info($"[LoadDetectionItems] 所有项目: {allItemNames.Count}, 关注项目: {focusedProjectCount}, 显示项目: {itemNames.Count}");
                
                // 清空现有数据
                _analysisData.Clear();
                _itemLimits.Clear();
                _statisticsCache.Clear();
                
                // 获取数据数量（默认1000条）
                int dataCount = _currentDataCount;
                
                // 填充数据
                foreach (var itemName in itemNames)
                {
                    try
                    {
                        var values = DetectionDataStorage.GetItemValues(itemName, dataCount);
                        var limits = DetectionDataStorage.GetItemLimits(itemName);
                        
                        if (values != null && values.Any())
                        {
                            _analysisData[itemName] = values;
                            _itemLimits[itemName] = limits;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"获取项目 {itemName} 数据失败: {ex.Message}");
                    }
                }
                
                // 创建项目按钮网格布局 (每行4个)
                CreateProjectGrid(_analysisData.Keys.ToList());
                
                // 如果有数据，默认选择超限次数最多的项目（与DataAnalysisPage保持一致）
                if (_analysisData.Any())
                {
                    string selectedItem = FindItemWithMostOutOfRange();
                    SelectItem(selectedItem);
                }
                
                LogManager.Info($"成功加载 {_analysisData.Count} 个检测项目的数据（已应用关注项目过滤）");
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载检测项目失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 查找超限次数最多的项目
        /// </summary>
        private string FindItemWithMostOutOfRange()
        {
            var currentData = GetCurrentAnalysisData();
            var currentLimits = GetCurrentItemLimits();
            
            if (!currentData.Any()) return "";
            
            string maxItem = currentData.Keys.First();
            int maxOutOfRange = 0;
            
            foreach (var item in currentData)
            {
                var itemName = item.Key;
                var values = item.Value;
                var limits = currentLimits.ContainsKey(itemName) ? currentLimits[itemName] : (LowerLimit: 0, UpperLimit: 0);
                
                int outOfRangeCount = values.Count(v => v < limits.LowerLimit || v > limits.UpperLimit);
                
                if (outOfRangeCount > maxOutOfRange)
                {
                    maxOutOfRange = outOfRangeCount;
                    maxItem = itemName;
                }
            }
            
            return maxItem;
        }

        private void CreateProjectGrid(List<string> items)
        {
            LogManager.Info($"[CreateProjectGrid] 开始创建项目按钮 - 项目数: {items?.Count ?? 0}");
            
            ItemButtonsPanel.Children.Clear();
            
            if (!items.Any())
            {
                var noDataText = new TextBlock
                {
                    Text = "没有检测项目数据",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    FontSize = 28,
                    Foreground = Brushes.Gray
                };
                ItemButtonsPanel.Children.Add(noDataText);
                LogManager.Info($"[CreateProjectGrid] 没有项目数据，显示提示信息");
                return;
            }

            // 计算行数 (每行4个)
            int itemsPerRow = 4;
            int rowCount = (int)Math.Ceiling((double)items.Count / itemsPerRow);
            
            LogManager.Info($"[CreateProjectGrid] 计算布局 - 每行{itemsPerRow}个，共{rowCount}行");
            
            for (int row = 0; row < rowCount; row++)
            {
                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                // 添加这一行的按钮
                for (int col = 0; col < itemsPerRow; col++)
                {
                    int itemIndex = row * itemsPerRow + col;
                    if (itemIndex >= items.Count) break;
                    
                    string itemName = items[itemIndex];
                    var button = new Button
                    {
                        Content = itemName,
                        Width = 120,
                        Height = 30,
                        Margin = new Thickness(5, 0, 5, 0),
                        Style = (Style)FindResource("ChartButtonStyle"),
                        Tag = itemName
                    };
                    
                    button.Click += (s, e) => {
                        LogManager.Info($"[CreateProjectGrid] 项目按钮点击: {itemName}");
                        SelectItem(itemName);
                    };
                    rowPanel.Children.Add(button);
                    
                    LogManager.Info($"[CreateProjectGrid] 创建按钮: {itemName} (第{row}行第{col}列)");
                }
                
                ItemButtonsPanel.Children.Add(rowPanel);
            }
            
            LogManager.Info($"[CreateProjectGrid] 项目按钮创建完成");
        }

        private void LoadSavedChartState()
        {
            // 从JSON配置文件加载保存的图表状态
            try
            {
                _currentChartType = ChartSettings.GetLastChartType();
                LogManager.Info($"已加载保存的图表状态: {_currentChartType}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载图表状态失败: {ex.Message}");
                _currentChartType = "BoxPlot"; // 默认状态
            }
        }

        public void SaveChartState()
        {
            // 保存当前图表状态到JSON配置文件
            try
            {
                ChartSettings.SaveChartType(_currentChartType);
                LogManager.Info($"已保存图表状态: {_currentChartType}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存图表状态失败: {ex.Message}");
            }
        }
        #endregion

        #region 图表切换
        private void BoxPlotButton_Click(object sender, RoutedEventArgs e)
        {
            ShowChart("BoxPlot");
        }

        private void ControlChartButton_Click(object sender, RoutedEventArgs e)
        {
            ShowChart("ControlChart");
        }

        private void HistogramButton_Click(object sender, RoutedEventArgs e)
        {
            ShowChart("Histogram");
        }

        private void ShowChart(string chartType)
        {
            try
            {
                _currentChartType = chartType;
                SaveChartState();
                
                // 隐藏所有图表
                BoxPlot.Visibility = Visibility.Collapsed;
                ControlChart.Visibility = Visibility.Collapsed;
                HistogramChart.Visibility = Visibility.Collapsed;
                NoDataMessage.Visibility = Visibility.Collapsed;
                
                // 显示选中的图表
                switch (chartType)
                {
                    case "BoxPlot":
                        BoxPlot.Visibility = Visibility.Visible;
                        UpdateButtonStyles(BoxPlotButton);
                        break;
                    case "ControlChart":
                        ControlChart.Visibility = Visibility.Visible;
                        UpdateButtonStyles(ControlChartButton);
                        break;
                    case "Histogram":
                        HistogramChart.Visibility = Visibility.Visible;
                        UpdateButtonStyles(HistogramButton);
                        break;
                }
                
                // 刷新当前选中项目的数据
                if (!string.IsNullOrEmpty(_currentSelectedItem))
                {
                    RefreshData();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"切换图表失败: {ex.Message}");
            }
        }

        private void UpdateButtonStyles(Button selectedButton)
        {
            // 重置所有按钮样式
            BoxPlotButton.Background = new SolidColorBrush(Color.FromRgb(74, 144, 226));
            ControlChartButton.Background = new SolidColorBrush(Color.FromRgb(74, 144, 226));
            HistogramButton.Background = new SolidColorBrush(Color.FromRgb(74, 144, 226));
            
            // 高亮选中按钮
            selectedButton.Background = new SolidColorBrush(Color.FromRgb(53, 122, 189));
        }
        #endregion

        #region 项目选择和数据处理
        private void SelectItem(string itemName)
        {
            try
            {
                LogManager.Info($"[SelectItem] 开始选择项目: {itemName}");
                _currentSelectedItem = itemName;
                
                var currentData = GetCurrentAnalysisData();
                var currentLimits = GetCurrentItemLimits();

                if (!currentData.ContainsKey(itemName))
                {
                    LogManager.Warning($"[SelectItem] 项目 {itemName} 在数据源中不存在");
                    ShowNoDataMessage($"项目 {itemName} 没有数据");
                    return;
                }

                var allData = currentData[itemName];
                var limits = currentLimits.ContainsKey(itemName) ? currentLimits[itemName] : (LowerLimit: 0, UpperLimit: 0);

                LogManager.Info($"[SelectItem] 项目 {itemName} 总数据量: {allData.Count}, 当前数据数: {_currentDataCount}");

                var dataToAnalyze = allData.Count > _currentDataCount ? 
                    allData.Skip(allData.Count - _currentDataCount).ToList() : 
                    allData.ToList();
                
                LogManager.Info($"[SelectItem] 实际分析数据量: {dataToAnalyze.Count}");
                
                if (!dataToAnalyze.Any())
                {
                    ShowNoDataMessage("没有数据可显示");
                    return;
                }

                UpdateProjectButtonHighlight(_currentSelectedItem);
                HideNoDataMessage();
                
                // 更新图表
                UpdateCharts(dataToAnalyze);
                
                // 更新统计信息
                UpdateStatistics(dataToAnalyze);
            }
            catch (Exception ex)
            {
                LogManager.Error($"选择检测项目失败: {ex.Message}");
                LogManager.Error($"异常详情: {ex.StackTrace}");
            }
        }

        private void DataCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedItem = DataCountComboBox.SelectedItem;
                if (selectedItem == null) return;

                // 明确使用系统的ComboBoxItem类型（解决命名空间冲突）
                var comboBoxItem = selectedItem as System.Windows.Controls.ComboBoxItem;
                if (comboBoxItem == null) return;

                // 检查控件是否已经初始化（防止在构造函数中过早访问）
                if (CustomDataCountLabel == null || CustomDataCountTextBox == null || ApplyCustomCountButton == null)
                {
                    return; // 控件还未初始化完成，跳过处理
                }

                // 正常ComboBoxItem处理
                if (comboBoxItem.Tag is string tagValue)
                {
                    if (tagValue == "custom")
                    {
                        // 显示自定义输入控件
                        CustomDataCountLabel.Visibility = Visibility.Visible;
                        CustomDataCountTextBox.Visibility = Visibility.Visible;
                        ApplyCustomCountButton.Visibility = Visibility.Visible;
                        LogManager.Info($"[DataCount] 切换到自定义数据量模式");
                    }
                    else
                    {
                        // 隐藏自定义输入控件
                        CustomDataCountLabel.Visibility = Visibility.Collapsed;
                        CustomDataCountTextBox.Visibility = Visibility.Collapsed;
                        ApplyCustomCountButton.Visibility = Visibility.Collapsed;
                        
                        if (int.TryParse(tagValue, out int count))
                        {
                            _currentDataCount = count == 0 ? int.MaxValue : count; // 0表示全部数据
                            LogManager.Info($"[DataCount] 设置数据量: {(count == 0 ? "全部" : count.ToString())}");
                            RefreshData();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"处理数据量选择改变失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自定义数据量输入验证
        /// </summary>
        private void CustomDataCountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 只允许数字输入
            if (!char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 应用自定义数据量
        /// </summary>
        private void ApplyCustomCountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(CustomDataCountTextBox.Text, out int customCount) && customCount > 0)
                {
                    _currentDataCount = customCount;
                    LogManager.Info($"[DataCount] 应用自定义数据量: {customCount}");
                    RefreshData();
                    MessageBox.Show($"已应用自定义数据量：{customCount}", "设置成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("请输入有效的数字（大于0）", "输入错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    CustomDataCountTextBox.Focus();
                    CustomDataCountTextBox.SelectAll();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用自定义数据量失败: {ex.Message}");
                MessageBox.Show($"应用失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // 重新加载检测项目列表和数据
            LoadDetectionItems();
        }

        private void AutoRefreshCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            StartAutoRefresh();

            // 立即刷新当前显示的图表，避免等待下一次定时器触发
            if (!string.IsNullOrEmpty(_currentSelectedItem))
            {
                RefreshData();
            }
        }

        private void AutoRefreshCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            StopAutoRefresh();
        }

        /// <summary>
        /// 应用文档上下限按钮点击事件
        /// </summary>
        private void ApplyDocumentLimitsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_hasImportedData || !_documentLimits.Any())
                {
                    MessageBox.Show("没有文档上下限数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _usingDocumentLimits = true;
                LogManager.Info($"[LimitSwitch] 切换到文档上下限");

                // 更新按钮状态
                ApplyDocumentLimitsButton.Background = new SolidColorBrush(Color.FromRgb(111, 66, 193)); // 更深的紫色表示选中
                ApplyCurrentLimitsButton.Background = new SolidColorBrush(Color.FromRgb(32, 201, 151)); // 原始颜色

                // 刷新当前选中项目的显示
                if (!string.IsNullOrEmpty(_currentSelectedItem))
                {
                    RefreshData();
                }

                MessageBox.Show("已应用文档上下限", "切换成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用文档上下限失败: {ex.Message}");
                MessageBox.Show($"应用文档上下限失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 应用当前上下限按钮点击事件
        /// </summary>
        private void ApplyCurrentLimitsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_hasImportedData)
                {
                    MessageBox.Show("当前不是导入数据状态", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _usingDocumentLimits = false;
                LogManager.Info($"[LimitSwitch] 切换到当前实时上下限");

                // 更新按钮状态
                ApplyCurrentLimitsButton.Background = new SolidColorBrush(Color.FromRgb(32, 150, 120)); // 更深的绿色表示选中
                ApplyDocumentLimitsButton.Background = new SolidColorBrush(Color.FromRgb(111, 66, 193)); // 原始颜色

                // 刷新当前选中项目的显示
                if (!string.IsNullOrEmpty(_currentSelectedItem))
                {
                    RefreshData();
                }

                MessageBox.Show("已应用当前实时数据上下限", "切换成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用当前上下限失败: {ex.Message}");
                MessageBox.Show($"应用当前上下限失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshData()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentSelectedItem))
                {
                    ShowNoDataMessage("请选择检测项目");
                    return;
                }

                if (_hasImportedData)
                {
                    // 使用导入数据
                    if (!_importedAnalysisData.ContainsKey(_currentSelectedItem))
                    {
                        ShowNoDataMessage("导入的数据中没有此项目");
                        return;
                    }

                    var values = _importedAnalysisData[_currentSelectedItem];
                    var dataToAnalyze = values.Count > _currentDataCount ? 
                        values.Skip(values.Count - _currentDataCount).ToList() : 
                        values.ToList();

                    if (!dataToAnalyze.Any())
                    {
                        ShowNoDataMessage("没有数据可显示");
                        return;
                    }

                    HideNoDataMessage();
                    UpdateCharts(dataToAnalyze);
                    UpdateStatistics(dataToAnalyze);
                }
                else
                {
                    // 重新从DetectionDataStorage获取最新数据
                    var values = DetectionDataStorage.GetItemValues(_currentSelectedItem, _currentDataCount);
                    var limits = DetectionDataStorage.GetItemLimits(_currentSelectedItem);

                    // 添加调试信息
                    LogManager.Info($"[SmartAnalysis] 项目: {_currentSelectedItem}, 请求数据量: {_currentDataCount}, 实际获取数据量: {values?.Count ?? 0}");

                    if (values == null || !values.Any())
                    {
                        ShowNoDataMessage("没有数据可显示");
                        return;
                    }

                    // 更新缓存数据
                    _analysisData[_currentSelectedItem] = values;
                    _itemLimits[_currentSelectedItem] = limits;

                    HideNoDataMessage();
                    UpdateCharts(values);
                    UpdateStatistics(values);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"刷新数据失败: {ex.Message}");
                ShowNoDataMessage("刷新数据失败");
            }
        }

        // 旧的UpdateCharts方法已删除，现在统一使用新方法更新所有图表
        #endregion

        #region 图表更新方法
        private void UpdateBoxPlot(string itemName, List<double> values, (double LowerLimit, double UpperLimit) limits)
        {
            try
            {
                BoxPlot.Plot.Clear();
                BoxPlot.Plot.Font.Automatic();

                if (values == null || values.Count == 0)
                {
                    BoxPlot.Plot.Axes.Bottom.Label.Text = itemName;
                    BoxPlot.Plot.Axes.Left.Label.Text = "Value";
                    
                    // 设置正常字体大小（扩大1倍）
                    BoxPlot.Plot.Axes.Bottom.Label.FontSize = 24;
                    BoxPlot.Plot.Axes.Left.Label.FontSize = 24;
                    BoxPlot.Plot.Axes.Bottom.TickLabelStyle.FontSize = 20;
                    BoxPlot.Plot.Axes.Left.TickLabelStyle.FontSize = 20;
                    
                    // 隐藏横坐标刻度标签（箱须图不需要显示X轴刻度）
                    BoxPlot.Plot.Axes.Bottom.TickLabelStyle.IsVisible = false;
                    
                    // 设置X轴范围为-2~3，Y轴使用计算的显示范围
                    BoxPlot.Plot.Axes.SetLimits(-2, 3, 70, 110);
                    
                    BoxPlot.Refresh();
                    LogManager.Warning($"箱须图数据为空，显示空图表");
                    return;
                }

                // 计算箱须图统计量
                var sortedValues = values.OrderBy(x => x).ToArray();
                var count = sortedValues.Length;
                
                // 记录原始数据范围以便对比
                var dataMin = sortedValues.Min();
                var dataMax = sortedValues.Max();

                var q1 = GetPercentile(sortedValues, 25);
                var median = GetPercentile(sortedValues, 50);
                var q3 = GetPercentile(sortedValues, 75);
                var iqr = q3 - q1;
                
                // 计算须的范围 (1.5 * IQR规则)
                var lowerWhisker = Math.Max(dataMin, q1 - 1.5 * iqr);
                var upperWhisker = Math.Min(dataMax, q3 + 1.5 * iqr);
                
                // 检查箱须图中的超限数据
                try
                {
                    var validLimits = limits.LowerLimit != double.MinValue && limits.UpperLimit != double.MaxValue && 
                                     !double.IsNaN(limits.LowerLimit) && !double.IsNaN(limits.UpperLimit);
                    if (validLimits)
                    {
                        var outOfRangeCount = values.Count(v => v < limits.LowerLimit || v > limits.UpperLimit);
                        if (outOfRangeCount > 0)
                        {
                            LogManager.Warning($"箱须图中发现 {outOfRangeCount} 个超限数据点");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Warning($"检查箱须图超限数据时出错: {ex.Message}");
                }

                // 更新盒须图显示
                var box = new ScottPlot.Box()
                {
                    Position = 0,
                    BoxMin = q1,    // 箱体下边界（第一四分位数）
                    BoxMax = q3,    // 箱体上边界（第三四分位数）
                    BoxMiddle = median, // 中位数线
                    WhiskerMin = lowerWhisker, // 下须
                    WhiskerMax = upperWhisker,  // 上须
                };

                // 添加箱图到图表
                BoxPlot.Plot.Add.Box(box);

                // 添加离群点
                var outliers = values.Where(x => x < lowerWhisker || x > upperWhisker).ToArray();
                if (outliers.Length > 0)
                {
                    // 在5.0版本中，Scatter方法使用不同的参数
                    var scatter = BoxPlot.Plot.Add.Scatter(
                        Enumerable.Repeat(0.0, outliers.Length).ToArray(), // X坐标
                        outliers // Y坐标
                    );
                    scatter.Color = ScottPlot.Color.FromHex("#FF6B6B");
                    scatter.MarkerSize = 8;
                }

                // 添加上下限线
                if (limits.LowerLimit != double.MinValue)
                {
                    var lowerLimitLine = BoxPlot.Plot.Add.HorizontalLine(limits.LowerLimit);
                    lowerLimitLine.LineColor = ScottPlot.Color.FromHex("#FF0000");
                    lowerLimitLine.LineWidth = 2;
                    lowerLimitLine.LinePattern = ScottPlot.LinePattern.Dashed;
                }

                if (limits.UpperLimit != double.MaxValue)
                {
                    var upperLimitLine = BoxPlot.Plot.Add.HorizontalLine(limits.UpperLimit);
                    upperLimitLine.LineColor = ScottPlot.Color.FromHex("#FF0000");
                    upperLimitLine.LineWidth = 2;
                    upperLimitLine.LinePattern = ScottPlot.LinePattern.Dashed;
                }

                // 计算数据均值
                var dataAverage = values.Average();

                // 添加上下限线
                if (limits.LowerLimit != double.MinValue && limits.UpperLimit != double.MaxValue &&
                    !double.IsNaN(limits.LowerLimit) && !double.IsNaN(limits.UpperLimit))
                {
                    // 上下限中心线（绿色）
                    var targetValue = (limits.LowerLimit + limits.UpperLimit) / 2.0;
                    var targetLine = BoxPlot.Plot.Add.HorizontalLine(targetValue);
                    targetLine.LineColor = ScottPlot.Color.FromHex("#00AA00"); // 深绿色，代表上下限中心
                    targetLine.LineWidth = 2;
                    targetLine.LinePattern = ScottPlot.LinePattern.Solid;

                    // 数据均值线（蓝色）
                    var meanLine = BoxPlot.Plot.Add.HorizontalLine(dataAverage);
                    meanLine.LineColor = ScottPlot.Color.FromHex("#0080FF"); // 蓝色，代表数据均值
                    meanLine.LineWidth = 2;
                    meanLine.LinePattern = ScottPlot.LinePattern.DenselyDashed;

                    LogManager.Info($"箱须图添加上下限中心线: {targetValue:F2}, 数据均值线: {dataAverage:F2}");
                }
                else
                {
                    // 只有数据均值线（蓝色）
                    var meanLine = BoxPlot.Plot.Add.HorizontalLine(dataAverage);
                    meanLine.LineColor = ScottPlot.Color.FromHex("#0080FF"); // 蓝色，代表数据均值
                    meanLine.LineWidth = 2;
                    meanLine.LinePattern = ScottPlot.LinePattern.DenselyDashed;

                    LogManager.Info($"箱须图添加数据均值线: {dataAverage:F2}");
                }
                
                // 计算Y轴显示范围，以上下限为基准
                double displayYMin, displayYMax;
                
                // 验证限制值的有效性
                bool hasValidLimits = limits.LowerLimit != double.MinValue && 
                                     limits.UpperLimit != double.MaxValue && 
                                     !double.IsNaN(limits.LowerLimit) && 
                                     !double.IsNaN(limits.UpperLimit) && 
                                     !double.IsInfinity(limits.LowerLimit) && 
                                     !double.IsInfinity(limits.UpperLimit);
                
                if (hasValidLimits)
                {
                    // 以上下限为基准设置显示范围，保留20%留白
                    var limitRange = limits.UpperLimit - limits.LowerLimit;
                    var limitMargin = limitRange * 0.2; // 上下限范围的20%作为留白
                    displayYMin = limits.LowerLimit - limitMargin;
                    displayYMax = limits.UpperLimit + limitMargin;
                    
                    // 检查数据极值，如果超过上下限则扩展坐标包含极值
                    var currentDataMin = values.Min();
                    var currentDataMax = values.Max();
                    
                    // 如果数据最小值小于下限，扩展显示范围
                    if (currentDataMin < limits.LowerLimit)
                    {
                        var extraMargin = Math.Abs(currentDataMin - limits.LowerLimit) * 0.1; // 额外10%边距
                        displayYMin = currentDataMin - extraMargin;
                    }
                    
                    // 如果数据最大值大于上限，扩展显示范围
                    if (currentDataMax > limits.UpperLimit)
                    {
                        var extraMargin = Math.Abs(currentDataMax - limits.UpperLimit) * 0.1; // 额外10%边距
                        displayYMax = currentDataMax + extraMargin;
                    }
                }
                else
                {
                    // 没有有效上下限时，以数据范围为基准
                    var currentDataMin = values.Min();
                    var currentDataMax = values.Max();
                    var dataRange = currentDataMax - currentDataMin;
                    var dataMargin = Math.Max(dataRange * 0.1, 0.1);
                    displayYMin = currentDataMin - dataMargin;
                    displayYMax = currentDataMax + dataMargin;
                }
                
                // 设置坐标轴
                BoxPlot.Plot.Axes.Bottom.Label.Text = itemName;
                BoxPlot.Plot.Axes.Left.Label.Text = "Value";
                
                // 设置轴标签字体大小（扩大1倍）
                BoxPlot.Plot.Axes.Bottom.Label.FontSize = 24;
                BoxPlot.Plot.Axes.Left.Label.FontSize = 24;
                
                // 设置刻度标签字体大小（扩大1倍）
                BoxPlot.Plot.Axes.Bottom.TickLabelStyle.FontSize = 20;
                BoxPlot.Plot.Axes.Left.TickLabelStyle.FontSize = 20;
                
                // 隐藏横坐标刻度标签（箱须图不需要显示X轴刻度）
                BoxPlot.Plot.Axes.Bottom.TickLabelStyle.IsVisible = false;
                
                // 设置X轴范围为-2~3，Y轴使用计算的显示范围
                BoxPlot.Plot.Axes.SetLimits(-2, 3, displayYMin, displayYMax);

                // 启用网格
                BoxPlot.Plot.Grid.IsVisible = true;

                // 刷新箱须图
                try
                {
                    BoxPlot.Refresh();
                    LogManager.Info("箱须图刷新完成");
                }
                catch (Exception ex)
                {
                    LogManager.Warning($"箱须图刷新失败: {ex.Message}");
                }

                LogManager.Info($"箱须图更新完成: {itemName}, Q1={q1:F2}, 中位数={median:F2}, Q3={q3:F2}, Y轴范围=[{displayYMin:F2}, {displayYMax:F2}]");
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新箱须图失败: {ex.Message}");
            }
        }

        private void UpdateControlChart(string itemName, List<double> values, (double LowerLimit, double UpperLimit) limits)
        {
            try
            {
                ControlChart.Plot.Clear();
                ControlChart.Plot.Font.Automatic();

                if (values == null || values.Count == 0)
                {
                    var noDataText = ControlChart.Plot.Add.Text("No Data", 0, 0);
                    noDataText.LabelFontSize = 28;
                    ControlChart.Plot.Axes.Bottom.Label.Text = "Sample No.";
                    ControlChart.Plot.Axes.Left.Label.Text = "Measured Value";
                    ControlChart.Plot.Axes.Bottom.Label.FontSize = 24;
                    ControlChart.Plot.Axes.Left.Label.FontSize = 24;
                    ControlChart.Plot.Axes.Bottom.TickLabelStyle.FontSize = 20;
                    ControlChart.Plot.Axes.Left.TickLabelStyle.FontSize = 20;
                    ControlChart.Refresh();
                    return;
                }

                // 准备X轴数据（样本序号）
                double[] xData = Enumerable.Range(1, values.Count).Select(i => (double)i).ToArray();
                double[] yData = values.ToArray();

                // 添加数据点散点图
                var scatter = ControlChart.Plot.Add.Scatter(xData, yData);
                scatter.Color = ScottPlot.Color.FromHex("#3498DB");
                scatter.LineStyle.Pattern = ScottPlot.LinePattern.Solid;
                scatter.LineStyle.Width = 0; // 不显示连线
                scatter.MarkerStyle.Size = 8;

                // 计算统计值
                double mean = values.Average();
                double stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / values.Count);

                // 添加数据均值线（蓝色）
                var meanLine = ControlChart.Plot.Add.HorizontalLine(mean);
                meanLine.Color = ScottPlot.Color.FromHex("#0080FF");
                meanLine.LineStyle.Width = 2;

                // 如果有规格限，也显示出来
                bool hasValidLimits = limits.LowerLimit != double.MinValue &&
                                     limits.UpperLimit != double.MaxValue &&
                                     !double.IsNaN(limits.LowerLimit) &&
                                     !double.IsNaN(limits.UpperLimit) &&
                                     !double.IsInfinity(limits.LowerLimit) &&
                                     !double.IsInfinity(limits.UpperLimit);

                if (hasValidLimits)
                {
                    // 上限线（红色）
                    var upperSpecLine = ControlChart.Plot.Add.HorizontalLine(limits.UpperLimit);
                    upperSpecLine.Color = ScottPlot.Color.FromHex("#FF0000");
                    upperSpecLine.LineStyle.Pattern = ScottPlot.LinePattern.Dashed;
                    upperSpecLine.LineStyle.Width = 2;

                    // 下限线（红色）
                    var lowerSpecLine = ControlChart.Plot.Add.HorizontalLine(limits.LowerLimit);
                    lowerSpecLine.Color = ScottPlot.Color.FromHex("#FF0000");
                    lowerSpecLine.LineStyle.Pattern = ScottPlot.LinePattern.Dashed;
                    lowerSpecLine.LineStyle.Width = 2;

                    // 上下限中心线（绿色）
                    var targetValue = (limits.LowerLimit + limits.UpperLimit) / 2.0;
                    var targetLine = ControlChart.Plot.Add.HorizontalLine(targetValue);
                    targetLine.Color = ScottPlot.Color.FromHex("#00AA00");
                    targetLine.LineStyle.Width = 2;
                    targetLine.LineStyle.Pattern = ScottPlot.LinePattern.Solid;
                }

                // 添加文本标签到线上
                double xLabelPositionLeft = values.Count * 0.05;  // 左边5%位置显示USL/LSL/Target标签
                double xLabelPositionRight = values.Count * 0.85; // 右边85%位置显示Mean标签

                // 数据均值线标签（蓝色）- 放在右边
                var meanLabel = ControlChart.Plot.Add.Text($"Mean {mean:F3}", xLabelPositionRight, mean);
                meanLabel.LabelFontColor = ScottPlot.Color.FromHex("#0080FF");
                meanLabel.LabelFontSize = 24;
                meanLabel.LabelBold = true;

                // 规格限标签
                if (hasValidLimits)
                {
                    // 上限标签（红色）- 放在左边，位置稍微偏上
                    var uslLabel = ControlChart.Plot.Add.Text($"USL {limits.UpperLimit:F3}", xLabelPositionLeft, limits.UpperLimit + Math.Abs(limits.UpperLimit) * 0.02);
                    uslLabel.LabelFontColor = ScottPlot.Color.FromHex("#FF0000");
                    uslLabel.LabelFontSize = 24;
                    uslLabel.LabelBold = true;

                    // 下限标签（红色）- 放在左边
                    var lslLabel = ControlChart.Plot.Add.Text($"LSL {limits.LowerLimit:F3}", xLabelPositionLeft, limits.LowerLimit);
                    lslLabel.LabelFontColor = ScottPlot.Color.FromHex("#FF0000");
                    lslLabel.LabelFontSize = 24;
                    lslLabel.LabelBold = true;

                    // 上下限中心标签（绿色）- 放在左边
                    var targetValue = (limits.LowerLimit + limits.UpperLimit) / 2.0;
                    var centerLabel = ControlChart.Plot.Add.Text($"Target {targetValue:F3}", xLabelPositionLeft, targetValue);
                    centerLabel.LabelFontColor = ScottPlot.Color.FromHex("#00AA00");
                    centerLabel.LabelFontSize = 24;
                    centerLabel.LabelBold = true;
                }

                // 设置轴标签和字体
                ControlChart.Plot.Axes.Bottom.Label.Text = "Sample No.";
                ControlChart.Plot.Axes.Left.Label.Text = "Measured Value";
                ControlChart.Plot.Axes.Bottom.Label.FontSize = 24;
                ControlChart.Plot.Axes.Left.Label.FontSize = 24;
                ControlChart.Plot.Axes.Bottom.TickLabelStyle.FontSize = 20;
                ControlChart.Plot.Axes.Left.TickLabelStyle.FontSize = 20;

                // 设置合理的显示范围
                double yMin = values.Min() - Math.Abs(mean) * 0.1;
                double yMax = values.Max() + Math.Abs(mean) * 0.1;
                
                if (hasValidLimits)
                {
                    yMin = Math.Min(yMin, limits.LowerLimit - Math.Abs(mean) * 0.05);
                    yMax = Math.Max(yMax, limits.UpperLimit + Math.Abs(mean) * 0.05);
                }

                ControlChart.Plot.Axes.SetLimits(0.5, values.Count + 0.5, yMin, yMax);

                ControlChart.Refresh();

                LogManager.Info($"控制图更新完成: {itemName}, 均值={mean:F3}, 标准差={stdDev:F3}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新控制图时出错: {ex.Message}");
            }
        }

        private void UpdateHistogram(string itemName, List<double> values, (double LowerLimit, double UpperLimit) limits)
        {
            try
            {
                HistogramChart.Plot.Clear();
                HistogramChart.Plot.Font.Automatic();

                // 严格的数据验证
                if (values == null || values.Count == 0)
                {
                    HistogramChart.Plot.Axes.Bottom.Label.Text = "Value";
                    HistogramChart.Plot.Axes.Left.Label.Text = "Frequency";
                    HistogramChart.Plot.Axes.Bottom.Label.FontSize = 24;
                    HistogramChart.Plot.Axes.Left.Label.FontSize = 24;
                    HistogramChart.Plot.Axes.Bottom.TickLabelStyle.FontSize = 20;
                    HistogramChart.Plot.Axes.Left.TickLabelStyle.FontSize = 20;
                    HistogramChart.Refresh();
                    LogManager.Warning($"直方图数据为空 - 项目: {itemName}");
                    return;
                }

                // 过滤无效数据
                var validValues = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (validValues.Count == 0)
                {
                    LogManager.Warning($"直方图没有有效数据 - 项目: {itemName}, 原始数据量: {values.Count}");
                    return;
                }

                if (validValues.Count != values.Count)
                {
                    LogManager.Info($"直方图过滤无效数据 - 项目: {itemName}, 原始: {values.Count}, 有效: {validValues.Count}");
                }

                // 使用过滤后的有效数据
                values = validValues;

                // 安全地计算实际数据范围
                double dataMin = values.Min();
                double dataMax = values.Max();
                
                // 对于数据范围很小的情况（包括只有1个数据点），扩展范围以便正常创建直方图
                if (Math.Abs(dataMax - dataMin) < 1e-10)
                {
                    var center = dataMin;
                    var expansion = Math.Max(Math.Abs(center) * 0.1, 1.0);
                    dataMin = center - expansion;
                    dataMax = center + expansion;
                }
                
                // 确定直方图的分组范围和显示范围
                double binMin, binMax, displayMin, displayMax;
                int binCount;
                
                // 验证限制值的有效性
                bool hasValidLimits = limits.LowerLimit != double.MinValue && 
                                     limits.UpperLimit != double.MaxValue && 
                                     !double.IsNaN(limits.LowerLimit) && 
                                     !double.IsNaN(limits.UpperLimit) && 
                                     !double.IsInfinity(limits.LowerLimit) && 
                                     !double.IsInfinity(limits.UpperLimit);
                
                if (hasValidLimits)
                {
                    binMin = Math.Min(limits.LowerLimit, dataMin);
                    binMax = Math.Max(limits.UpperLimit, dataMax);
                    binCount = 10;
                    
                    var limitRange = binMax - binMin;
                    var margin = limitRange * 0.1;
                    displayMin = binMin - margin;
                    displayMax = binMax + margin;
                    
                    var outOfRangeCount = values.Count(v => v < limits.LowerLimit || v > limits.UpperLimit);
                }
                else
                {
                    var actualRange = dataMax - dataMin;
                    var margin = Math.Max(actualRange * 0.1, 0.1);
                    
                    binMin = dataMin - margin;
                    binMax = dataMax + margin;
                    displayMin = binMin;
                    displayMax = binMax;
                    
                    binCount = Math.Min(20, Math.Max(5, (int)Math.Sqrt(values.Count)));
                }

                // 根据规格上下限差值动态调整bin数量和宽度 - 增强处理数值一致性高的情况
                double binWidth = 0.1; // 默认宽度
                double dataRange = dataMax - dataMin;
                double dataAverage = validValues.Average();
                double dataStdDev = validValues.Count > 1 ? Math.Sqrt(validValues.Select(x => Math.Pow(x - dataAverage, 2)).Average()) : 0;
                
                // 检测数值一致性 - 当标准差很小或相对变异系数很小时
                bool isHighConsistency = false;
                if (validValues.Count > 1)
                {
                    double coefficientOfVariation = Math.Abs(dataAverage) > 1e-10 ? (dataStdDev / Math.Abs(dataAverage)) : 0;
                    isHighConsistency = dataStdDev < 1e-6 || coefficientOfVariation < 1e-6;
                    
                    if (isHighConsistency)
                    {
                        LogManager.Info($"检测到高一致性数据 - 项目: {itemName}, 标准差: {dataStdDev:E3}, 变异系数: {coefficientOfVariation:E3}");
                    }
                }
                
                if (hasValidLimits)
                {
                    double limitRange = limits.UpperLimit - limits.LowerLimit;
                    
                    // 需求：当上下限的值小于等于1时，直方图的宽度应为0.01
                    if (limitRange <= 1.0)
                    {
                        binWidth = 0.01;
                        binCount = Math.Max(10, (int)Math.Ceiling(limitRange / binWidth));
                    }
                    else
                    {
                        // 需求：当公差范围>1时，直方图的分bin应该是USL和LSL差值的1/20
                        binWidth = limitRange / 20.0;
                        binCount = 20; // 固定为20个bin
                    }
                    
                    // 对于高一致性数据，进一步调整binWidth
                    if (isHighConsistency && dataRange < binWidth / 100)
                    {
                        binWidth = Math.Max(dataRange * 10, Math.Abs(dataAverage) * 1e-6);
                        LogManager.Info($"高一致性数据调整binWidth - 项目: {itemName}, 原binWidth: {limitRange / 20.0:F6}, 新binWidth: {binWidth:F6}");
                    }
                    
                    LogManager.Info($"直方图bin计算 - 项目: {itemName}, 公差范围: {limitRange:F3}, binWidth: {binWidth:F6}, binCount: {binCount}");
                }
                else
                {
                    binCount = 10;
                    
                    if (isHighConsistency)
                    {
                        // 高一致性数据：使用基于数据绝对值的相对binWidth
                        if (Math.Abs(dataAverage) > 1e-10)
                        {
                            binWidth = Math.Abs(dataAverage) * 1e-6; // 数据值的百万分之一
                        }
                        else
                        {
                            binWidth = Math.Max(dataRange * 10, 1e-6); // 扩大数据范围或使用最小值
                        }
                        LogManager.Info($"高一致性数据特殊处理 - 项目: {itemName}, 数据均值: {dataAverage:F6}, binWidth: {binWidth:F6}");
                    }
                    else
                    {
                        // 正常数据：根据数据范围确定宽度
                        binWidth = Math.Max(dataRange / 20.0, 0.01);
                    }
                    
                    LogManager.Info($"直方图bin计算 - 项目: {itemName}, 数据范围: {dataRange:F6}, binWidth: {binWidth:F6}, binCount: {binCount}");
                }

                // 创建直方图数据
                if (values.Count == 1)
                {
                    // 特殊处理：只有1个数据点时，创建一个简单的柱状图
                    double singleValue = values[0];
                    double[] bins = { singleValue };
                    double[] counts = { 1.0 };
                    
                    var barPlot = HistogramChart.Plot.Add.Bars(bins, counts);
                    
                    // 安全设置柱子宽度 - 添加保护性检查
                    if (barPlot != null && barPlot.Bars != null && barPlot.Bars.Count > 0)
                    {
                        foreach (var bar in barPlot.Bars)
                        {
                            if (bar != null)
                            {
                                bar.Size = binWidth * 0.9; // 使用统一的宽度设置
                                bar.LineWidth = 0;
                            }
                        }
                    }
                    barPlot.Color = ScottPlot.Color.FromHex("#3498DB");
                    
                    LogManager.Info($"单数据点直方图 - 项目: {itemName}, 数值: {singleValue:F3}, 宽度: {binWidth:F4}");
                }
                else
                {
                    // 多个数据点时使用正常的直方图
                    try
                    {
                        // 强化参数安全性验证 - 防止"0 must be greater than firstBin"错误
                        var currentRange = dataMax - dataMin;
                        
                        // 首先检查数据范围
                        if (currentRange <= 0)
                        {
                            LogManager.Warning($"数据范围异常，使用备用直方图 - 项目: {itemName}, 范围: {currentRange}");
                            CreateBackupHistogram(itemName, values, limits);
                            return;
                        }
                        
                        // 验证binWidth有效性
                        if (binWidth <= 0 || double.IsNaN(binWidth) || double.IsInfinity(binWidth))
                        {
                            binWidth = Math.Max(currentRange / 10.0, 0.01);
                            LogManager.Warning($"binWidth异常，重新计算 - 项目: {itemName}, 新binWidth: {binWidth:F4}");
                        }
                        
                        // 限制binWidth范围，防止过大或过小
                        if (binWidth > currentRange)
                        {
                            binWidth = currentRange / 5.0; // 最少5个bin
                            LogManager.Info($"binWidth过大，调整 - 项目: {itemName}, 数据范围: {currentRange:F3}, 调整后binWidth: {binWidth:F4}");
                        }
                        
                        if (binWidth < 0.001) // 防止过小的binWidth
                        {
                            binWidth = Math.Max(currentRange / 20.0, 0.001);
                            LogManager.Info($"binWidth过小，调整 - 项目: {itemName}, 调整后binWidth: {binWidth:F4}");
                        }

                        // 使用自定义的binWidth创建直方图
                        var hist = ScottPlot.Statistics.Histogram.WithBinSize(binWidth, values.ToArray());

                        // 检查直方图数据
                        if (hist == null || hist.Bins == null || hist.Counts == null || 
                            hist.Bins.Length == 0 || hist.Counts.Length == 0 || 
                            hist.Bins.Length != hist.Counts.Length)
                        {
                            LogManager.Warning($"直方图数据无效，使用备用方案 - 项目: {itemName}");
                            CreateBackupHistogram(itemName, values, limits);
                            return;
                        }

                        // 创建柱状图（左Y轴）
                        var barPlot = HistogramChart.Plot.Add.Bars(hist.Bins, hist.Counts);

                        // 安全设置每个bar的宽度
                        if (barPlot?.Bars != null)
                        {
                            foreach (var bar in barPlot.Bars)
                            {
                                if (bar != null)
                                {
                                    bar.Size = Math.Min(binWidth * 0.9, dataRange * 0.1); // 限制bar宽度
                                    bar.LineWidth = 0;
                                }
                            }
                        }

                        barPlot.Color = ScottPlot.Color.FromHex("#3498DB");
                        
                        // 日志记录直方图设置信息
                        LogManager.Info($"直方图设置成功 - 项目: {itemName}, 宽度: {binWidth:F4}, 组数: {hist.Bins.Length}, 数据范围: {dataRange:F3}");
                    }
                    catch (Exception ex)
                    {
                        //LogManager.Error($"创建直方图失败，使用备用方案 - 项目: {itemName}, 错误: {ex.Message}");
                        CreateBackupHistogram(itemName, values, limits);
                        return;
                    }
                }

                // 添加概率密度曲线（只有数据点>=5且范围>1e-10时才添加）
                if (values.Count >= 5 && Math.Abs(values.Max() - values.Min()) > 1e-10)
                {
                    try
                    {
                        var uniqueValues = values.Distinct().Count();
                        if (uniqueValues >= 2)
                        {
                            var pd = new ScottPlot.Statistics.ProbabilityDensity(values.ToArray());
                            double range = values.Max() - values.Min();
                            double stepSize = range / 20.0;
                            double[] xs = ScottPlot.Generate.Range(values.Min(), values.Max(), stepSize);
                            
                            // 安全检查：确保stepSize有效且数组长度合理
                            if (stepSize > 0 && xs != null && xs.Length > 1 && xs.Length <= 1000)
                            {
                                double[] ys = null;
                                try
                                {
                                    ys = pd.GetYs(xs);
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Warning($"生成概率密度数据失败 - 项目: {itemName}, 错误: {ex.Message}");
                                    ys = null; // 设置为null，跳过概率密度曲线，继续显示直方图
                                }
                                
                                // 验证生成的数据
                                if (ys != null && xs.Length == ys.Length && xs.Length > 0 && 
                                    !ys.Any(y => double.IsNaN(y) || double.IsInfinity(y)))
                                {
                                    var curve = HistogramChart.Plot.Add.ScatterLine(xs, ys);
                                    curve.Axes.YAxis = HistogramChart.Plot.Axes.Right; // 使用右侧Y轴显示概率密度
                                    curve.LineWidth = 2;
                                    curve.LineColor = ScottPlot.Color.FromHex("#E74C3C");
                                    curve.LinePattern = ScottPlot.LinePattern.DenselyDashed;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 概率密度曲线添加失败时继续显示直方图
                        LogManager.Warning($"添加概率密度曲线失败 - 项目: {itemName}, 错误: {ex.Message}");
                    }
                }

                // 添加数据均值线（蓝色虚线）
                var meanLine = HistogramChart.Plot.Add.VerticalLine(dataAverage);
                meanLine.LineColor = ScottPlot.Color.FromHex("#0080FF");
                meanLine.LineWidth = 2;
                meanLine.LinePattern = ScottPlot.LinePattern.DenselyDashed;

                // 添加上下限线
                if (hasValidLimits)
                {
                    // 下限线（红色虚线）
                    var lowerLimitLine = HistogramChart.Plot.Add.VerticalLine(limits.LowerLimit);
                    lowerLimitLine.LineColor = ScottPlot.Color.FromHex("#FF0000");
                    lowerLimitLine.LineWidth = 2;
                    lowerLimitLine.LinePattern = ScottPlot.LinePattern.Dashed;

                    // 上限线（红色虚线）
                    var upperLimitLine = HistogramChart.Plot.Add.VerticalLine(limits.UpperLimit);
                    upperLimitLine.LineColor = ScottPlot.Color.FromHex("#FF0000");
                    upperLimitLine.LineWidth = 2;
                    upperLimitLine.LinePattern = ScottPlot.LinePattern.Dashed;

                    // 上下限中心线（绿色实线）
                    var targetValue = (limits.LowerLimit + limits.UpperLimit) / 2.0;
                    var targetLine = HistogramChart.Plot.Add.VerticalLine(targetValue);
                    targetLine.LineColor = ScottPlot.Color.FromHex("#00AA00");
                    targetLine.LineWidth = 2;
                    targetLine.LinePattern = ScottPlot.LinePattern.Solid;
                }

                // 设置坐标轴和刷新
                HistogramChart.Plot.Axes.Bottom.Label.Text = $"{itemName} Value";
                HistogramChart.Plot.Axes.Left.Label.Text = "Frequency";
                //HistogramChart.Plot.Axes.Right.Label.Text = "概率密度";
                
                HistogramChart.Plot.Axes.Bottom.Label.FontSize = 24;
                HistogramChart.Plot.Axes.Left.Label.FontSize = 24;
                HistogramChart.Plot.Axes.Right.Label.FontSize = 24;
                
                HistogramChart.Plot.Axes.Bottom.TickLabelStyle.FontSize = 20;
                HistogramChart.Plot.Axes.Left.TickLabelStyle.FontSize = 20;
                HistogramChart.Plot.Axes.Right.TickLabelStyle.FontSize = 20;

                // 恢复原来的显示范围设置逻辑，但确保Y轴下限始终为0
                HistogramChart.Plot.Axes.SetLimits(displayMin, displayMax, 0, double.NaN);
                HistogramChart.Plot.Grid.IsVisible = true;
                HistogramChart.Refresh();
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新直方图失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建备用直方图（当主要方法失败时使用）
        /// 强化版本，能处理各种异常数据情况
        /// </summary>
        private void CreateBackupHistogram(string itemName, List<double> values, (double LowerLimit, double UpperLimit) limits)
        {
            try
            {
                LogManager.Info($"使用备用直方图方案 - 项目: {itemName}, 数据量: {values.Count}");
                
                // 严格的数据验证
                if (values == null || values.Count == 0)
                {
                    var noDataText = HistogramChart.Plot.Add.Text($"No Data: {itemName}", 0, 0);
                    noDataText.LabelFontSize = 28;
                    return;
                }
                
                // 过滤有效数据
                var validValues = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                if (validValues.Count == 0)
                {
                    var noValidDataText = HistogramChart.Plot.Add.Text($"No Valid Data: {itemName}", 0, 0);
                    noValidDataText.LabelFontSize = 28;
                    return;
                }
                
                // 计算数据范围
                double dataMin = validValues.Min();
                double dataMax = validValues.Max();
                double range = dataMax - dataMin;
                
                // 处理数据范围为0的情况（所有数据相同）
                if (range <= 1e-10)
                {
                    // 创建单值直方图
                    double singleValue = dataMin;
                    double[] bins = { singleValue };
                    double[] counts = { validValues.Count };
                    
                    var singleBarPlot = HistogramChart.Plot.Add.Bars(bins, counts);
                    if (singleBarPlot?.Bars != null)
                    {
                        foreach (var bar in singleBarPlot.Bars)
                        {
                            if (bar != null)
                            {
                                bar.Size = Math.Max(Math.Abs(singleValue) * 0.1, 1.0); // 自适应宽度
                                bar.LineWidth = 0;
                            }
                        }
                    }
                    singleBarPlot.Color = ScottPlot.Color.FromHex("#3498DB");
                    LogManager.Info($"备用单值直方图创建成功 - 项目: {itemName}, 值: {singleValue:F3}");
                    return;
                }
                
                // 动态确定bin数量
                int binCount = Math.Min(20, Math.Max(5, (int)Math.Sqrt(validValues.Count)));
                double binWidth = range / binCount;
                
                // 最后的安全检查
                if (binWidth <= 0 || double.IsNaN(binWidth) || double.IsInfinity(binWidth))
                {
                    binWidth = 1.0; // 强制设置为安全值
                }
                
                // 手动创建直方图数据
                double[] binCenters = new double[binCount];
                double[] binCounts = new double[binCount];
                
                for (int i = 0; i < binCount; i++)
                {
                    binCenters[i] = dataMin + (i + 0.5) * binWidth;
                    binCounts[i] = 0;
                }
                
                // 计算每个bin的数据点数量，使用更安全的索引计算
                foreach (var value in validValues)
                {
                    double normalizedValue = (value - dataMin) / binWidth;
                    int binIndex = (int)Math.Floor(normalizedValue);
                    
                    // 强制限制索引范围
                    if (binIndex < 0) binIndex = 0;
                    if (binIndex >= binCount) binIndex = binCount - 1;
                    
                    binCounts[binIndex]++;
                }
                
                // 创建柱状图
                var barPlot = HistogramChart.Plot.Add.Bars(binCenters, binCounts);
                
                if (barPlot?.Bars != null)
                {
                    foreach (var bar in barPlot.Bars)
                    {
                        if (bar != null)
                        {
                            bar.Size = binWidth * 0.8;
                            bar.LineWidth = 0;
                        }
                    }
                }
                
                barPlot.Color = ScottPlot.Color.FromHex("#3498DB");
                
                LogManager.Info($"备用直方图创建成功 - 项目: {itemName}, 有效数据: {validValues.Count}, binCount: {binCount}, binWidth: {binWidth:F4}, 范围: [{dataMin:F3}, {dataMax:F3}]");
            }
            catch (Exception ex)
            {
                LogManager.Error($"备用直方图创建失败 - 项目: {itemName}, 错误: {ex.Message}");
                LogManager.Error($"异常详情: {ex.StackTrace}");
                
                // 最后的备选方案：显示简单的文本提示
                try
                {
                    var errorText = HistogramChart.Plot.Add.Text($"Data Display Error\\n{itemName}\\n{ex.Message}", 0, 0);
                    errorText.LabelFontSize = 28;
                }
                catch
                {
                    // 连文本都无法显示时，至少记录日志
                    LogManager.Error($"连备选文本显示都失败 - 项目: {itemName}");
                }
            }
        }

        /// <summary>
        /// 计算百分位数
        /// </summary>
        private double GetPercentile(double[] sortedArray, double percentile)
        {
            if (sortedArray.Length == 0) return 0;
            if (sortedArray.Length == 1) return sortedArray[0];

            double n = sortedArray.Length;
            double index = (percentile / 100.0) * (n - 1);
            int lowerIndex = (int)Math.Floor(index);
            int upperIndex = (int)Math.Ceiling(index);

            if (lowerIndex == upperIndex)
            {
                return sortedArray[lowerIndex];
            }

            double weight = index - lowerIndex;
            return sortedArray[lowerIndex] * (1 - weight) + sortedArray[upperIndex] * weight;
        }

        // 更新UpdateCharts方法以使用新的签名
        private void UpdateCharts(List<double> dataToAnalyze)
        {
            if (string.IsNullOrEmpty(_currentSelectedItem) || !dataToAnalyze.Any())
            {
                return;
            }

            // 获取当前项目的上下限
            var currentLimits = GetCurrentItemLimits();
            var limits = currentLimits.ContainsKey(_currentSelectedItem) 
                ? currentLimits[_currentSelectedItem] 
                : (LowerLimit: double.MinValue, UpperLimit: double.MaxValue);

            // 使用新的方法签名调用图表更新方法
            UpdateBoxPlot(_currentSelectedItem, dataToAnalyze, limits);
            UpdateControlChart(_currentSelectedItem, dataToAnalyze, limits);
            UpdateHistogram(_currentSelectedItem, dataToAnalyze, limits);
        }
        #endregion

        #region 统计信息更新
        private void UpdateStatistics(List<double> values)
        {
            try
            {
                if (!values.Any()) return;

                var stats = CalculateStatistics(values);
                _statisticsCache[_currentSelectedItem] = stats;

                LogManager.Info($"[UpdateStatistics] 开始更新统计UI - 项目: {_currentSelectedItem}");

                // 更新UI显示
                AvgValue.Text = stats.Average.ToString("F3");
                StdValue.Text = stats.StandardDeviation.ToString("F3");
                CaValue.Text = stats.Ca.ToString("F3");
                CpValue.Text = stats.Cp.ToString("F3");
                CpkValue.Text = stats.Cpk.ToString("F3");
                OutOfRangeCount.Text = stats.OutOfRangeCount.ToString();
                OutOfRangeProbability.Text = $"{stats.OutOfRangeProbability:P2}";
                SampleCount.Text = stats.SampleCount.ToString();

                LogManager.Info($"[UpdateStatistics] UI更新完成 - AVG={stats.Average:F3}, STD={stats.StandardDeviation:F3}, CA={stats.Ca:F3}, CP={stats.Cp:F3}, CPK={stats.Cpk:F3}");

                // 设置Cpk颜色提示
                if (stats.Cpk < 1.0)
                    CpkValue.Foreground = Brushes.Red;
                else if (stats.Cpk < 1.33)
                    CpkValue.Foreground = Brushes.Orange;
                else
                    CpkValue.Foreground = Brushes.Green;

                // 设置异常率颜色提示
                if (stats.OutOfRangeProbability > 0.05)
                    OutOfRangeProbability.Foreground = Brushes.Red;
                else if (stats.OutOfRangeProbability > 0.01)
                    OutOfRangeProbability.Foreground = Brushes.Orange;
                else
                    OutOfRangeProbability.Foreground = Brushes.Green;
                    
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新统计信息失败: {ex.Message}");
                LogManager.Error($"异常详情: {ex.StackTrace}");
            }
        }

        private StatisticsData CalculateStatistics(List<double> values)
        {
            var stats = new StatisticsData();
            
            if (!values.Any()) return stats;

            // 基本统计
            stats.Average = values.Average();
            stats.StandardDeviation = Math.Sqrt(values.Select(x => Math.Pow(x - stats.Average, 2)).Average());
            stats.SampleCount = values.Count;

            LogManager.Info($"[CalculateStatistics] 项目: {_currentSelectedItem}, 样本数: {stats.SampleCount}, 均值: {stats.Average:F3}, 标准差: {stats.StandardDeviation:F3}");

            // 获取规格限
            var currentLimits = GetCurrentItemLimits();
            if (currentLimits.ContainsKey(_currentSelectedItem))
            {
                var limits = currentLimits[_currentSelectedItem];
                double lsl = limits.LowerLimit;  // 下规格限
                double usl = limits.UpperLimit;  // 上规格限
                double target = (usl + lsl) / 2;  // 目标值（规格中心）

                LogManager.Info($"[CalculateStatistics] 规格限: LSL={lsl:F3}, USL={usl:F3}, Target={target:F3}");

                // 计算Ca（准确度指数）
                stats.Ca = Math.Abs(stats.Average - target) / ((usl - lsl) / 2);

                // 计算Cp（精密度指数）
                stats.Cp = (usl - lsl) / (6 * stats.StandardDeviation);

                // 计算Cpk（过程能力指数）
                double cpkUpper = (usl - stats.Average) / (3 * stats.StandardDeviation);
                double cpkLower = (stats.Average - lsl) / (3 * stats.StandardDeviation);
                stats.Cpk = Math.Min(cpkUpper, cpkLower);

                LogManager.Info($"[CalculateStatistics] Ca={stats.Ca:F3}, Cp={stats.Cp:F3}, Cpk={stats.Cpk:F3}");

                // 计算超限数量和概率
                stats.OutOfRangeCount = values.Count(v => v < lsl || v > usl);
                stats.OutOfRangeProbability = (double)stats.OutOfRangeCount / values.Count;

                LogManager.Info($"[CalculateStatistics] 超限数量: {stats.OutOfRangeCount}, 超限率: {stats.OutOfRangeProbability:P2}");
            }
            else
            {
                LogManager.Warning($"[CalculateStatistics] 项目 {_currentSelectedItem} 没有找到规格限信息");
            }

            return stats;
        }
        #endregion

        #region 数据导出
        public void ExportData()
        {
            try
            {
                // 检查是否有数据可导出
                if (_hasImportedData)
                {
                    // 导入数据模式：导出导入的数据
                    if (_importedAnalysisData.Count == 0)
                    {
                        MessageBox.Show("没有导入数据可导出", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    // 实时数据模式：检查DetectionDataStorage中的数据
                    if (DetectionDataStorage.GetTotalRecordCount() == 0)
                    {
                        MessageBox.Show("没有实时数据可导出，请先进行检测", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel文件|*.xlsx|CSV文件|*.csv",
                    DefaultExt = "xlsx",
                    FileName = GenerateExportFileName()
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var extension = Path.GetExtension(saveDialog.FileName).ToLower();
                    
                    if (extension == ".csv")
                    {
                        ExportToCsvFormat(saveDialog.FileName);
                    }
                    else
                    {
                        ExportToExcelFormat(saveDialog.FileName);
                    }
                    
                    MessageBox.Show($"数据已导出到: {saveDialog.FileName}", "导出成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"导出失败: {ex.Message}");
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateExportFileName()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dataSource = _hasImportedData ? "导入数据" : "实时数据";
            var limitSource = "";
            
            if (_hasImportedData)
            {
                limitSource = _usingDocumentLimits ? "_文档上下限" : "_当前上下限";
            }
            
            return $"{dataSource}_{timestamp}{limitSource}";
        }

        private void ExportToCsvFormat(string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                if (_hasImportedData)
                {
                    ExportImportedDataToCsv(writer);
                }
                else
                {
                    ExportRealTimeDataToCsv(writer);
                }
            }
            LogManager.Info($"成功导出CSV数据到: {filePath}");
        }

        private void ExportImportedDataToCsv(StreamWriter writer)
        {
            // 写入表头信息
            writer.WriteLine($"导出时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"数据来源,导入数据");
            writer.WriteLine($"数据量设置,{(_currentDataCount == int.MaxValue ? "全部" : _currentDataCount.ToString())}");
            writer.WriteLine($"上下限来源,{(_usingDocumentLimits ? "文档上下限" : "当前实时上下限")}");
            writer.WriteLine();

            // 获取所有项目名称
            var allItems = _importedAnalysisData.Keys.ToList();
            var currentLimits = GetCurrentItemLimits();
            
            if (!allItems.Any())
            {
                writer.WriteLine("没有导入数据可导出");
                return;
            }
            
            // 写入表头
            var headers = new List<string> { "序号", "时间戳" };
            foreach (var item in allItems)
            {
                headers.Add(item);
                headers.Add($"{item}_下限");
                headers.Add($"{item}_上限");
                headers.Add($"{item}_判定");
            }
            writer.WriteLine(string.Join(",", headers));

            // 确定要导出的数据量 - 修复索引越界问题
            int maxDataCount = _importedAnalysisData.Values.Any() ? _importedAnalysisData.Values.Max(v => v.Count) : 0;
            if (maxDataCount == 0)
            {
                writer.WriteLine("导入数据为空");
                return;
            }
            
            int exportCount = _currentDataCount == int.MaxValue ? maxDataCount : Math.Min(_currentDataCount, maxDataCount);
            
            // 写入数据行
            for (int i = 0; i < exportCount; i++)
            {
                var row = new List<string> { (i + 1).ToString(), DateTime.Now.AddMinutes(-exportCount + i).ToString("yyyy-MM-dd HH:mm:ss") };
                
                foreach (var item in allItems)
                {
                    if (!_importedAnalysisData.ContainsKey(item))
                    {
                        // 如果项目不存在，添加空值
                        row.Add("");
                        row.Add("");
                        row.Add("");
                        row.Add("");
                        continue;
                    }
                    
                    var data = _importedAnalysisData[item];
                    var limits = currentLimits.ContainsKey(item) ? currentLimits[item] : (LowerLimit: 0, UpperLimit: 0);
                    
                    // 修复索引计算问题：从最新数据开始倒推
                    int dataIndex = Math.Max(0, data.Count - exportCount + i);
                    
                    if (dataIndex < data.Count && data.Count > 0)
                    {
                        var value = data[dataIndex];
                        row.Add(value.ToString("F6"));
                        row.Add(limits.LowerLimit.ToString("F6"));
                        row.Add(limits.UpperLimit.ToString("F6"));
                        row.Add((value < limits.LowerLimit || value > limits.UpperLimit) ? "NG" : "OK");
                    }
                    else
                    {
                        // 数据不足时添加空值
                        row.Add("");
                        row.Add(limits.LowerLimit.ToString("F6"));
                        row.Add(limits.UpperLimit.ToString("F6"));
                        row.Add("");
                    }
                }
                
                writer.WriteLine(string.Join(",", row));
            }
        }

        private void ExportRealTimeDataToCsv(StreamWriter writer)
        {
            // 写入表头信息
            writer.WriteLine($"导出时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"数据来源,实时数据");
            writer.WriteLine($"数据量设置,{(_currentDataCount == int.MaxValue ? "全部" : _currentDataCount.ToString())}");
            writer.WriteLine();

            // 获取实时数据并导出（使用与DataAnalysisPage相同的方式）
            var exportData = DetectionDataStorage.GetExportData(_currentDataCount == int.MaxValue ? 0 : _currentDataCount);
            
            if (exportData.Count == 0)
            {
                writer.WriteLine("没有实时数据可导出");
                return;
            }

            // 获取所有列名
            var columnNames = exportData.SelectMany(row => row.Keys).Distinct().ToList();
            
            // 写入表头
            writer.WriteLine(string.Join(",", columnNames));
            
            // 写入数据
            foreach (var rowData in exportData)
            {
                var values = columnNames.Select(col => rowData.ContainsKey(col) ? rowData[col]?.ToString() ?? "" : "").ToArray();
                writer.WriteLine(string.Join(",", values));
            }
        }

        private void ExportToExcelFormat(string filePath)
        {
            // 简化实现：暂时用CSV格式替代Excel，后续可增加EPPlus支持
            var csvPath = Path.ChangeExtension(filePath, ".csv");
            ExportToCsvFormat(csvPath);
            LogManager.Info($"Excel导出暂时以CSV格式保存到: {csvPath}");
        }
        #endregion

        #region 辅助方法
        private void ShowNoDataMessage(string message)
        {
            NoDataMessage.Visibility = Visibility.Visible;
            NoDataMessage.Text = message;
        }

        private void HideNoDataMessage()
        {
            NoDataMessage.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region 新增功能方法
        
        /// <summary>
        /// 导入按钮点击事件
        /// </summary>
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "CSV文件|*.csv|Excel文件|*.xlsx|所有文件|*.*",
                    DefaultExt = "csv",
                    Title = "选择要导入的数据文件"
                };

                if (openDialog.ShowDialog() == true)
                {
                    ImportDataFromFile(openDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出按钮点击事件
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            ExportData();
        }

        /// <summary>
        /// 从文件导入数据
        /// </summary>
        private void ImportDataFromFile(string filePath)
        {
            try
            {
                // 清除之前的导入数据
                ClearImportedData();

                var extension = System.IO.Path.GetExtension(filePath).ToLower();
                
                if (extension == ".csv")
                {
                    ImportFromCSV(filePath);
                }
                else if (extension == ".xlsx")
                {
                    ImportFromExcel(filePath);
                }
                else
                {
                    MessageBox.Show("不支持的文件格式", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 标记为已导入数据
                _hasImportedData = true;

                // 更新界面
                UpdateUIWithImportedData();

                MessageBox.Show($"成功导入 {_importedAnalysisData.Count} 个检测项目的数据", "导入完成", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从CSV文件导入数据
        /// </summary>
        private void ImportFromCSV(string filePath)
        {
            var lines = System.IO.File.ReadAllLines(filePath);
            if (lines.Length < 2) return;

            // 解析表头
            var headers = lines[0].Split(',');
            var dataColumns = new Dictionary<string, List<double>>();
            var upperLimitColumns = new Dictionary<string, List<double>>(); // 上限列
            var lowerLimitColumns = new Dictionary<string, List<double>>(); // 下限列
            var judgementColumns = new List<string>(); // 判定列（跳过）

            // 分析表头，识别不同类型的列
            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i].Trim();
                if (string.IsNullOrEmpty(header) || header == "序号" || header == "时间" || header == "缺陷类型")
                    continue;

                if (header.EndsWith("_上限"))
                {
                    // 上限列
                    var baseItemName = header.Replace("_上限", "");
                    upperLimitColumns[baseItemName] = new List<double>();
                    LogManager.Info($"[ImportCSV] 识别上限列: {header} -> {baseItemName}");
                }
                else if (header.EndsWith("_下限"))
                {
                    // 下限列
                    var baseItemName = header.Replace("_下限", "");
                    lowerLimitColumns[baseItemName] = new List<double>();
                    LogManager.Info($"[ImportCSV] 识别下限列: {header} -> {baseItemName}");
                }
                else if (header.EndsWith("_超限") || header.Contains("判定") || header.Contains("结果"))
                {
                    // 判定列，跳过
                    judgementColumns.Add(header);
                    LogManager.Info($"[ImportCSV] 跳过判定列: {header}");
                }
                else
                {
                    // 数据列
                    dataColumns[header] = new List<double>();
                    LogManager.Info($"[ImportCSV] 识别数据列: {header}");
                }
            }

            // 解析数据行
            for (int row = 1; row < lines.Length; row++)
            {
                var values = lines[row].Split(',');
                for (int col = 0; col < Math.Min(values.Length, headers.Length); col++)
                {
                    var header = headers[col].Trim();
                    var cellValue = values[col].Trim();
                    
                    if (!double.TryParse(cellValue, out double numericValue))
                        continue;

                    // 根据列类型存储数据
                    if (header.EndsWith("_上限"))
                    {
                        var baseItemName = header.Replace("_上限", "");
                        if (upperLimitColumns.ContainsKey(baseItemName))
                        {
                            upperLimitColumns[baseItemName].Add(numericValue);
                        }
                    }
                    else if (header.EndsWith("_下限"))
                    {
                        var baseItemName = header.Replace("_下限", "");
                        if (lowerLimitColumns.ContainsKey(baseItemName))
                        {
                            lowerLimitColumns[baseItemName].Add(numericValue);
                        }
                    }
                    else if (!judgementColumns.Contains(header) && dataColumns.ContainsKey(header))
                    {
                        dataColumns[header].Add(numericValue);
                    }
                }
            }

            // 处理数据列和对应的上下限
            foreach (var item in dataColumns)
            {
                var itemName = item.Key;
                var itemData = item.Value;
                
                if (!itemData.Any()) continue;

                // 存储数据
                _importedAnalysisData[itemName] = itemData;

                // 处理上下限 - 使用新的内存管理机制
                double upperLimit, lowerLimit;
                bool hasDocumentLimits = false;

                // 如果CSV中有上下限列，更新内存存储
                if (upperLimitColumns.ContainsKey(itemName) && upperLimitColumns[itemName].Any() &&
                    lowerLimitColumns.ContainsKey(itemName) && lowerLimitColumns[itemName].Any())
                {
                    // 使用新的上下限管理机制：从最后一条数据开始查找有效值
                    UpdateLimitsMemoryStorage(itemName, upperLimitColumns[itemName], lowerLimitColumns[itemName]);
                    
                    // 从内存存储获取上下限
                    var memoryLimits = GetLimitsFromMemoryStorage(itemName);
                    if (memoryLimits.LowerLimit != double.MinValue && memoryLimits.UpperLimit != double.MaxValue)
                    {
                        upperLimit = memoryLimits.UpperLimit;
                        lowerLimit = memoryLimits.LowerLimit;
                        hasDocumentLimits = true;
                        LogManager.Info($"[ImportCSV] 项目 {itemName} 使用内存存储上下限: [{lowerLimit:F3}, {upperLimit:F3}]");
                    }
                    else
                    {
                        // 内存中没有有效值，使用3σ计算
                        var avg = itemData.Average();
                        var std = Math.Sqrt(itemData.Select(x => Math.Pow(x - avg, 2)).Average());
                        upperLimit = avg + 3 * std;
                        lowerLimit = avg - 3 * std;
                        LogManager.Info($"[ImportCSV] 项目 {itemName} 内存无效值，使用3σ计算上下限: [{lowerLimit:F3}, {upperLimit:F3}]");
                    }
                }
                else
                {
                    // CSV中没有上下限列，使用3σ原则计算上下限
                    var avg = itemData.Average();
                    var std = Math.Sqrt(itemData.Select(x => Math.Pow(x - avg, 2)).Average());
                    upperLimit = avg + 3 * std;
                    lowerLimit = avg - 3 * std;
                    LogManager.Info($"[ImportCSV] 项目 {itemName} 无上下限列，使用3σ计算上下限: [{lowerLimit:F3}, {upperLimit:F3}]");
                }

                // 存储文档上下限（如果有的话）
                if (hasDocumentLimits)
                {
                    _documentLimits[itemName] = (lowerLimit, upperLimit);
                }
                
                // 获取当前实时数据的上下限（从DetectionDataStorage）
                var currentLimits = DetectionDataStorage.GetItemLimits(itemName);
                _currentRealTimeLimits[itemName] = currentLimits;

                // 默认使用文档上下限（如果有），否则使用计算的上下限
                _importedItemLimits[itemName] = (lowerLimit, upperLimit);

                LogManager.Info($"[ImportCSV] 项目: {itemName}, 数据量: {itemData.Count}, 数据范围: {itemData.Min():F3} ~ {itemData.Max():F3}");
            }

            LogManager.Info($"[ImportCSV] 导入完成 - 数据项目: {_importedAnalysisData.Count}, 有文档上下限: {_documentLimits.Count}");
        }

        /// <summary>
        /// 从Excel文件导入数据
        /// </summary>
        private void ImportFromExcel(string filePath)
        {
            // 这里可以实现Excel导入逻辑
            // 暂时使用简化实现，可以后续扩展
            MessageBox.Show("Excel导入功能开发中，请使用CSV格式", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 使用导入的数据更新界面
        /// </summary>
        private void UpdateUIWithImportedData()
        {
            if (!_hasImportedData) return;

            // 添加调试信息
            LogManager.Info($"[UpdateUI] 导入数据项目数: {_importedAnalysisData.Count}");
            foreach (var item in _importedAnalysisData)
            {
                LogManager.Info($"[UpdateUI] 项目: {item.Key}, 数据量: {item.Value.Count}");
            }

            // 更新状态指示器
            DataSourceIndicator.Text = "导入数据";
            DataSourceIndicator.Foreground = Brushes.Orange;

            // 显示上下限切换按钮（如果有文档上下限）
            if (_documentLimits.Any())
            {
                LimitSwitchPanel.Visibility = Visibility.Visible;
                LogManager.Info($"[UpdateUI] 显示上下限切换按钮，文档上下限项目数: {_documentLimits.Count}");
            }
            else
            {
                LimitSwitchPanel.Visibility = Visibility.Collapsed;
                LogManager.Info($"[UpdateUI] 隐藏上下限切换按钮，无文档上下限");
            }

            // 创建项目按钮网格布局
            CreateProjectGrid(_importedAnalysisData.Keys.ToList());

            // 默认选择第一个项目
            if (_importedAnalysisData.Any())
            {
                var firstItem = _importedAnalysisData.Keys.First();
                LogManager.Info($"[UpdateUI] 选择第一个项目: {firstItem}");
                SelectItem(firstItem);
            }
        }

        /// <summary>
        /// 使用导入的数据更新界面（应用关注项目过滤）
        /// </summary>
        private void UpdateUIWithImportedDataWithFocusFilter()
        {
            if (!_hasImportedData) return;

            // 获取关注的项目列表
            var focusedProjects = WpfApp2.UI.Models.FocusedProjectsManager.GetFocusedProjects();
            
            // 过滤出关注的导入项目
            var filteredImportedData = new Dictionary<string, List<double>>();
            var filteredItemLimits = new Dictionary<string, (double LowerLimit, double UpperLimit)>();
            var filteredDocumentLimits = new Dictionary<string, (double LowerLimit, double UpperLimit)>();
            var filteredCurrentLimits = new Dictionary<string, (double LowerLimit, double UpperLimit)>();

            var focusedProjectCount = focusedProjects.Count;
            foreach (var item in _importedAnalysisData)
            {
                var itemName = item.Key;
                
                // 如果没有设置关注项目，或者该项目在关注列表中
                if (focusedProjectCount == 0 || focusedProjects.Contains(itemName))
                {
                    filteredImportedData[itemName] = item.Value;
                    
                    if (_importedItemLimits.ContainsKey(itemName))
                        filteredItemLimits[itemName] = _importedItemLimits[itemName];
                    
                    if (_documentLimits.ContainsKey(itemName))
                        filteredDocumentLimits[itemName] = _documentLimits[itemName];
                    
                    if (_currentRealTimeLimits.ContainsKey(itemName))
                        filteredCurrentLimits[itemName] = _currentRealTimeLimits[itemName];
                }
            }

            LogManager.Info($"[UpdateUIWithFocusFilter] 导入数据过滤 - 原始: {_importedAnalysisData.Count}, 关注: {focusedProjectCount}, 显示: {filteredImportedData.Count}");

            // 更新状态指示器
            DataSourceIndicator.Text = $"导入数据 (关注项目: {filteredImportedData.Count})";
            DataSourceIndicator.Foreground = Brushes.Orange;

            // 显示上下限切换按钮（如果有文档上下限）
            if (filteredDocumentLimits.Any())
            {
                LimitSwitchPanel.Visibility = Visibility.Visible;
                LogManager.Info($"[UpdateUIWithFocusFilter] 显示上下限切换按钮，文档上下限项目数: {filteredDocumentLimits.Count}");
            }
            else
            {
                LimitSwitchPanel.Visibility = Visibility.Collapsed;
                LogManager.Info($"[UpdateUIWithFocusFilter] 隐藏上下限切换按钮，无文档上下限");
            }

            // 创建项目按钮网格布局（使用过滤后的数据）
            CreateProjectGrid(filteredImportedData.Keys.ToList());

            // 默认选择第一个项目
            if (filteredImportedData.Any())
            {
                var firstItem = filteredImportedData.Keys.First();
                LogManager.Info($"[UpdateUIWithFocusFilter] 选择第一个关注项目: {firstItem}");
                SelectItem(firstItem);
            }
            else
            {
                // 没有关注的项目时显示提示
                ShowNoDataMessage("没有关注的项目，请在关注项目设置中添加项目");
            }
        }

        /// <summary>
        /// 清除导入的数据
        /// </summary>
        public void ClearImportedData()
        {
            _importedAnalysisData.Clear();
            _importedItemLimits.Clear();
            _documentLimits.Clear();
            _currentRealTimeLimits.Clear();
            _hasImportedData = false;
            _usingDocumentLimits = true;
            
            LogManager.Info("[ClearImportedData] 已清除所有导入数据");
        }

        /// <summary>
        /// 清空质量分析仪表板的实时/导入数据以及UI状态
        /// </summary>
        public void ClearAllAnalysisData()
        {
            try
            {
                void ResetInternal()
                {
                    LogManager.Info("[SmartAnalysisMainPage] 收到仪表板数据清空请求");

                    // 同步清空内存数据
                    ClearImportedData();
                    _analysisData.Clear();
                    _itemLimits.Clear();
                    _statisticsCache.Clear();
                    _currentRealTimeLimits.Clear();
                    _documentLimits.Clear();
                    _limitsMemoryStorage.Clear();
                    _currentSelectedItem = string.Empty;

                    // 重置项目列表与图表显示
                    ItemButtonsPanel.Children.Clear();
                    ShowNoDataMessage("没有数据可显示");

                    BoxPlot.Plot.Clear();
                    BoxPlot.Refresh();
                    ControlChart.Plot.Clear();
                    ControlChart.Refresh();
                    HistogramChart.Plot.Clear();
                    HistogramChart.Refresh();

                    BoxPlot.Visibility = Visibility.Visible;
                    ControlChart.Visibility = Visibility.Collapsed;
                    HistogramChart.Visibility = Visibility.Collapsed;

                    // 重置统计面板
                    AvgValue.Text = "--";
                    StdValue.Text = "--";
                    SampleCount.Text = "--";
                    CaValue.Text = "--";
                    CpValue.Text = "--";
                    CpkValue.Text = "--";
                    OutOfRangeCount.Text = "--";
                    OutOfRangeProbability.Text = "--";
                    CpkValue.Foreground = Brushes.Black;
                    OutOfRangeProbability.Foreground = Brushes.Black;

                    DataSourceIndicator.Text = "实时数据";
                    DataSourceIndicator.Foreground = new SolidColorBrush(Color.FromRgb(40, 167, 69));
                    LimitSwitchPanel.Visibility = Visibility.Collapsed;

                    UpdateButtonStyles(BoxPlotButton);
                    ClearAlertInfo();
                }

                if (Dispatcher.CheckAccess())
                {
                    ResetInternal();
                }
                else
                {
                    Dispatcher.Invoke(ResetInternal);
                }

                LogManager.Info("[SmartAnalysisMainPage] 仪表板数据与UI状态已清空");
            }
            catch (Exception ex)
            {
                LogManager.Error($"清空仪表板数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有可用的项目名称（包括实时检测的和导入的）
        /// </summary>
        /// <returns>所有项目名称的列表</returns>
        public static List<string> GetAllAvailableItemNames()
        {
            var allItemNames = new List<string>();
            
            // 获取实时检测的项目
            var realTimeItems = DetectionDataStorage.GetAllItemNames();
            allItemNames.AddRange(realTimeItems);
            
            // 获取导入的项目
            // 需要通过静态方式访问导入数据，因为可能有多个SmartAnalysisMainPage实例
            // 暂时先从任何现有的SmartAnalysisWidget实例获取
            try
            {
                var smartAnalysisWidget = Application.Current.Windows
                    .OfType<Window>()
                    .SelectMany(w => FindVisualChildren<SmartAnalysisWidget>(w))
                    .FirstOrDefault();
                    
                if (smartAnalysisWidget != null)
                {
                    var mainPage = FindVisualChildren<SmartAnalysisMainPage>(smartAnalysisWidget).FirstOrDefault();
                    if (mainPage != null && mainPage._hasImportedData)
                    {
                        var importedItems = mainPage._importedAnalysisData.Keys.ToList();
                        // 避免重复项目
                        foreach (var item in importedItems)
                        {
                            if (!allItemNames.Contains(item))
                            {
                                allItemNames.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"获取导入项目时出错: {ex.Message}");
            }
            
            return allItemNames.Distinct().ToList();
        }
        
        /// <summary>
        /// 辅助方法：查找可视化树中的子控件
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前使用的数据源（导入数据优先）
        /// </summary>
        private Dictionary<string, List<double>> GetCurrentAnalysisData()
        {
            return _hasImportedData ? _importedAnalysisData : _analysisData;
        }

        /// <summary>
        /// 获取当前使用的限制值（导入数据优先）
        /// </summary>
        private Dictionary<string, (double LowerLimit, double UpperLimit)> GetCurrentItemLimits()
        {
            if (_hasImportedData)
            {
                // 如果是导入数据，根据用户选择返回不同来源的上下限
                if (_usingDocumentLimits && _documentLimits.Any())
                {
                    // 创建混合字典：优先使用文档上下限，回退到导入上下限
                    var mixedLimits = new Dictionary<string, (double LowerLimit, double UpperLimit)>(_importedItemLimits);
                    foreach (var docLimit in _documentLimits)
                    {
                        mixedLimits[docLimit.Key] = docLimit.Value;
                    }
                    return mixedLimits;
                }
                else
                {
                    // 使用当前实时数据的上下限
                    var mixedLimits = new Dictionary<string, (double LowerLimit, double UpperLimit)>(_importedItemLimits);
                    foreach (var currentLimit in _currentRealTimeLimits)
                    {
                        if (currentLimit.Value.LowerLimit != double.MinValue && currentLimit.Value.UpperLimit != double.MaxValue)
                        {
                            mixedLimits[currentLimit.Key] = currentLimit.Value;
                        }
                    }
                    return mixedLimits;
                }
            }
            else
            {
                return _itemLimits;
            }
        }
        
        /// <summary>
        /// 更新告警设置
        /// </summary>
        public void UpdateAlertSettings(AlertSettings alertSettings)
        {
            try
            {
                // 这里可以根据告警设置更新相关显示
                LogManager.Info($"告警设置已更新: 启用={alertSettings.IsEnabled}, 统计周期={alertSettings.StatisticsCycle}");
                
                // 可以在这里实现告警状态的检查和显示
                CheckAndDisplayAlerts(alertSettings);
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新告警设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查并显示告警信息
        /// </summary>
        private void CheckAndDisplayAlerts(AlertSettings alertSettings)
        {
            try
            {
                if (!alertSettings.IsEnabled || string.IsNullOrEmpty(_currentSelectedItem))
                {
                    AlertItemText.Text = "\u65e0\u544a\u8b66";
                    TriggerReasonText.Text = string.Empty;
                    return;
                }

                var stats = _statisticsCache.ContainsKey(_currentSelectedItem)
                    ? _statisticsCache[_currentSelectedItem]
                    : null;

                if (stats == null)
                {
                    AlertItemText.Text = _currentSelectedItem;
                    TriggerReasonText.Text = "\u6682\u65e0\u7edf\u8ba1\u6570\u636e";
                    return;
                }

                var profile = alertSettings.GetProfileForItem(_currentSelectedItem);
                if (profile == null)
                {
                    AlertItemText.Text = _currentSelectedItem;
                    TriggerReasonText.Text = "\u672a\u627e\u5230\u5339\u914d\u7684\u7b56\u7565\u7ec4\u5408";
                    return;
                }

                var alertMessages = new List<string>();

                if (profile.EnableCountAnalysis && stats.OutOfRangeCount >= profile.OutOfRangeThreshold)
                {
                    alertMessages.Add($"\u8d85\u9650\u6b21\u6570\u8d85\u8fc7{profile.OutOfRangeThreshold}\u6b21");
                }

                if (profile.EnableProcessCapabilityAnalysis)
                {
                    if (stats.Ca > profile.CAThreshold)
                    {
                        alertMessages.Add($"CA>{profile.CAThreshold:F3}");
                    }
                    if (stats.Cp < profile.CPThreshold)
                    {
                        alertMessages.Add($"CP<{profile.CPThreshold:F3}");
                    }
                    if (stats.Cpk < profile.CPKThreshold)
                    {
                        alertMessages.Add($"CPK<{profile.CPKThreshold:F3}");
                    }
                }

                if (profile.EnableConsecutiveNGAnalysis)
                {
                    int consecutiveNG = GetConsecutiveNGCount(_currentSelectedItem);
                    if (consecutiveNG >= profile.ConsecutiveNGThreshold)
                    {
                        alertMessages.Add($"\u8fde\u7eedNG\u8d85\u8fc7{profile.ConsecutiveNGThreshold}\u6b21");
                    }
                }

                if (alertMessages.Any())
                {
                    var label = profile.IsDefault ? _currentSelectedItem : $"{_currentSelectedItem} ({profile.Name})";
                    AlertItemText.Text = label;
                    TriggerReasonText.Text = string.Join(", ", alertMessages);
                }
                else
                {
                    AlertItemText.Text = _currentSelectedItem;
                    TriggerReasonText.Text = "\u65e0\u544a\u8b66\u89e6\u53d1";
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"\u68c0\u67e5\u544a\u8b66\u5931\u8d25: {ex.Message}");
            }
        }
        /// <summary>
        /// 获取连续NG次数（需要从外部系统获取）
        /// </summary>
        private int GetConsecutiveNGCount(string itemName)
        {
            try
            {
                // 这里需要调用外部的连续NG计数功能
                // 暂时返回0，后续需要与实际的NG计数系统对接
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 显示告警信息
        /// </summary>
        public void ShowAlertInfo(string alertItem, string triggerReason)
        {
            try
            {
                AlertItemText.Text = alertItem;
                TriggerReasonText.Text = triggerReason;
                LogManager.Info($"显示告警信息: {alertItem} - {triggerReason}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示告警信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除告警信息
        /// </summary>
        public void ClearAlertInfo()
        {
            try
            {
                AlertItemText.Text = "正常";
                TriggerReasonText.Text = "无告警触发";
            }
            catch (Exception ex)
            {
                LogManager.Error($"清除告警信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 选择并显示指定项目
        /// </summary>
        public void SelectAndShowItem(string itemName)
        {
            try
            {
                // 查找对应的按钮
                var targetButton = ItemButtonsPanel.Children.OfType<Button>()
                    .FirstOrDefault(b => b.Tag as string == itemName);

                if (targetButton != null)
                {
                    // 更新选中状态
                    _currentSelectedItem = itemName;
                    UpdateButtonStyles(targetButton);
                    
                    // 切换到对应的图表
                    if (_analysisData.ContainsKey(itemName) && _analysisData[itemName].Any())
                    {
                        UpdateCharts(_analysisData[itemName]);
                    }
                    
                    LogManager.Info($"已选择并显示项目: {itemName}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"选择并显示项目失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置统计窗口按钮点击事件
        /// </summary>
        private void ResetStatisticsWindowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "重置统计窗口将会：\n" +
                    "1. 清空当前的统计数据窗口\n" +
                    "2. 从下一次检测开始重新计算CA、CP、CPK等统计指标\n" +
                    "3. 不会影响历史数据的保存\n\n" +
                    "确定要重置统计窗口吗？",
                    "确认重置统计窗口",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 调用重置统计窗口的逻辑
                    ResetStatisticsWindow();
                    
                    // 清除当前告警信息
                    ClearAlertInfo();
                    
                    // 刷新图表显示
                    if (!string.IsNullOrEmpty(_currentSelectedItem) && 
                        _analysisData.ContainsKey(_currentSelectedItem) && 
                        _analysisData[_currentSelectedItem].Any())
                    {
                        UpdateCharts(_analysisData[_currentSelectedItem]);
                    }
                    
                    MessageBox.Show("统计窗口已重置，下次检测将开始新的统计周期。", 
                        "重置完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    LogManager.Info("用户手动重置了统计窗口");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"重置统计窗口失败: {ex.Message}");
                MessageBox.Show($"重置统计窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 重置统计窗口的具体逻辑
        /// </summary>
        private void ResetStatisticsWindow()
        {
            try
            {
                LogManager.Info("执行统计窗口重置操作");
                
                // 🎯 核心功能：清零告警计数器，让系统重新开始计数
                // 这实现了滑动窗口机制中的"重置"概念
                SmartAnalysisWindowManager.ClearAlertCounters();
                
                // 清除分析数据缓存
                _analysisData.Clear();
                
                // 刷新当前显示的数据
                LoadDetectionItems();
                
                LogManager.Info("✅ 统计窗口重置完成：告警计数器已清零，系统将重新开始计数");
            }
            catch (Exception ex)
            {
                LogManager.Error($"执行统计窗口重置失败: {ex.Message}");
                throw;
            }
        }

        #endregion
    }

    /// <summary>
    /// 图表设置类 - 用于持久化存储图表显示状态
    /// </summary>
    public class ChartSettings
    {
        public string LastSelectedChartType { get; set; } = "BoxPlot";
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// 保存图表设置到JSON文件
        /// </summary>
        public void Save()
        {
            try
            {
                var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                Directory.CreateDirectory(configDir);
                
                var filePath = Path.Combine(configDir, "ChartSettings.json");
                this.LastModified = DateTime.Now;
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                
                LogManager.Info($"图表设置已保存到: {filePath}, 类型: {LastSelectedChartType}");
            }
            catch (UnauthorizedAccessException ex)
            {
                LogManager.Error($"保存图表设置失败，权限不足: {ex.Message}");
                // 尝试保存到用户目录
                try
                {
                    var userConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WpfApp2");
                    Directory.CreateDirectory(userConfigDir);
                    
                    var userFilePath = Path.Combine(userConfigDir, "ChartSettings.json");
                    this.LastModified = DateTime.Now;
                    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    File.WriteAllText(userFilePath, json, Encoding.UTF8);
                    LogManager.Info($"图表设置已保存到用户目录: {userFilePath}");
                }
                catch (Exception userEx)
                {
                    LogManager.Error($"保存到用户目录也失败: {userEx.Message}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存图表设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从JSON文件加载图表设置
        /// </summary>
        public static ChartSettings Load()
        {
            try
            {
                // 首先尝试从程序目录加载
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ChartSettings.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath, Encoding.UTF8);
                    var settings = JsonConvert.DeserializeObject<ChartSettings>(json);
                    if (settings != null)
                    {
                        LogManager.Info($"从程序目录加载图表设置: {settings.LastSelectedChartType}");
                        return settings;
                    }
                }
                
                // 如果程序目录没有，尝试从用户目录加载
                var userFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "WpfApp2", "ChartSettings.json");
                if (File.Exists(userFilePath))
                {
                    var json = File.ReadAllText(userFilePath, Encoding.UTF8);
                    var settings = JsonConvert.DeserializeObject<ChartSettings>(json);
                    if (settings != null)
                    {
                        LogManager.Info($"从用户目录加载图表设置: {settings.LastSelectedChartType}");
                        return settings;
                    }
                }

                LogManager.Info("图表设置文件不存在，使用默认设置");
                return new ChartSettings(); // 返回默认设置
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载图表设置失败: {ex.Message}，使用默认设置");
                return new ChartSettings(); // 加载失败时返回默认设置
            }
        }

        /// <summary>
        /// 静态方法：保存图表类型
        /// </summary>
        public static void SaveChartType(string chartType)
        {
            try
            {
                var settings = Load();
                settings.LastSelectedChartType = chartType;
                settings.Save();
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存图表类型失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 静态方法：获取上次选择的图表类型
        /// </summary>
        public static string GetLastChartType()
        {
            try
            {
                var settings = Load();
                return settings.LastSelectedChartType;
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取图表类型失败: {ex.Message}");
                return "BoxPlot"; // 默认返回盒须图
            }
        }
    }
} 
