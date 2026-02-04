using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 验机测试结果分析窗口
    /// 显示矩阵关系：行为不同样品，列为同一样品不同轮次的检测结果及统计
    /// </summary>
    public partial class ValidatorMachineResultsWindow : Window
    {
        // 检测结果数据存储
        // Key: 项目名称, Value: 二维数组 [样品索引, 轮次索引] = 数值
        private Dictionary<string, double[,]> _projectResultsMatrix = new Dictionary<string, double[,]>();

        // 项目列表
        private List<string> _projectNames = new List<string>();

        // 样品数量和巡回次数
        private int _sampleCount = 0;
        private int _loopCycle = 0;

        // LOT号
        private string _lotNumber = string.Empty;

        public ValidatorMachineResultsWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初始化结果数据
        /// </summary>
        /// <param name="sampleCount">样品数量</param>
        /// <param name="loopCycle">巡回次数</param>
        /// <param name="projectNames">检测项目名称列表</param>
        /// <param name="lotNumber">LOT号</param>
        public void InitializeResults(int sampleCount, int loopCycle, List<string> projectNames, string lotNumber = "")
        {
            try
            {
                _sampleCount = sampleCount;
                _loopCycle = loopCycle;
                _projectNames = projectNames ?? new List<string>();
                _lotNumber = lotNumber;

                LogManager.Info($"初始化验机结果窗口 - 样品数: {sampleCount}, 巡回次数: {loopCycle}, 项目数: {_projectNames.Count}, LOT: {lotNumber}");

                // 初始化每个项目的数据矩阵
                _projectResultsMatrix.Clear();
                foreach (var projectName in _projectNames)
                {
                    _projectResultsMatrix[projectName] = new double[sampleCount, loopCycle];
                    // 初始化为0
                    for (int i = 0; i < sampleCount; i++)
                    {
                        for (int j = 0; j < loopCycle; j++)
                        {
                            _projectResultsMatrix[projectName][i, j] = 0;
                        }
                    }
                }

                // 更新UI显示
                LotInfoTextBlock.Text = string.IsNullOrEmpty(lotNumber) ? "" : $"LOT: {lotNumber}";
                SampleCountTextBlock.Text = sampleCount.ToString();
                LoopCycleTextBlock.Text = loopCycle.ToString();

                // 加载项目下拉列表
                ProjectComboBox.ItemsSource = _projectNames;
                if (_projectNames.Count > 0)
                {
                    ProjectComboBox.SelectedIndex = 0;
                }

                LogManager.Info("验机结果窗口初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化验机结果窗口失败: {ex.Message}");
                MessageBox.Show($"初始化结果窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 设置单个检测结果值
        /// </summary>
        /// <param name="projectName">项目名称</param>
        /// <param name="sampleIndex">样品索引 (0-based)</param>
        /// <param name="cycleIndex">轮次索引 (0-based)</param>
        /// <param name="value">检测值</param>
        public void SetResultValue(string projectName, int sampleIndex, int cycleIndex, double value)
        {
            try
            {
                if (_projectResultsMatrix.ContainsKey(projectName) &&
                    sampleIndex >= 0 && sampleIndex < _sampleCount &&
                    cycleIndex >= 0 && cycleIndex < _loopCycle)
                {
                    _projectResultsMatrix[projectName][sampleIndex, cycleIndex] = value;
                    LogManager.Debug($"设置结果: {projectName}[样品{sampleIndex + 1}, 第{cycleIndex + 1}次] = {value:F3}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"设置结果值失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量设置某个样品某次检测的所有项目结果
        /// </summary>
        /// <param name="sampleIndex">样品索引 (0-based)</param>
        /// <param name="cycleIndex">轮次索引 (0-based)</param>
        /// <param name="results">项目名称-值 字典</param>
        public void SetDetectionResults(int sampleIndex, int cycleIndex, Dictionary<string, double> results)
        {
            try
            {
                foreach (var kvp in results)
                {
                    SetResultValue(kvp.Key, sampleIndex, cycleIndex, kvp.Value);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"批量设置结果失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新当前选中项目的显示
        /// </summary>
        public void RefreshDisplay()
        {
            if (ProjectComboBox.SelectedItem is string selectedProject)
            {
                DisplayProjectData(selectedProject);
            }
        }

        /// <summary>
        /// 项目选择改变事件
        /// </summary>
        private void ProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ProjectComboBox.SelectedItem is string selectedProject)
                {
                    LogManager.Info($"选择项目: {selectedProject}");
                    DisplayProjectData(selectedProject);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"项目选择失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示项目数据矩阵
        /// 行: 样品编号（图号1、图号2...）
        /// 列: 第1次, 第2次, ..., 第n次, 最大值, 最小值, 均值, 差值
        /// 统计列计算的是同一样品不同轮次的重复精度
        /// </summary>
        private void DisplayProjectData(string projectName)
        {
            try
            {
                if (!_projectResultsMatrix.ContainsKey(projectName))
                {
                    LogManager.Warning($"未找到项目数据: {projectName}");
                    return;
                }

                var dataMatrix = _projectResultsMatrix[projectName];

                // 清空现有列
                ResultsDataGrid.Columns.Clear();

                // 创建数据行集合
                var dataRows = new ObservableCollection<ValidatorResultRow>();

                // 添加样品名称列（第一列：图号）
                var sampleColumn = new DataGridTextColumn
                {
                    Header = "图号",
                    Binding = new Binding("SampleName"),
                    Width = 80,
                    IsReadOnly = true
                };
                ResultsDataGrid.Columns.Add(sampleColumn);

                // 添加每轮次的数据列
                for (int cycle = 0; cycle < _loopCycle; cycle++)
                {
                    var column = new DataGridTextColumn
                    {
                        Header = $"第{cycle + 1}次",
                        Binding = new Binding($"CycleValues[{cycle}]"),
                        Width = 70,
                        IsReadOnly = true
                    };
                    ResultsDataGrid.Columns.Add(column);
                }

                // 添加统计列
                AddStatisticsColumns();

                // 填充数据行（每行是一个样品/图号）
                for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
                {
                    var row = new ValidatorResultRow
                    {
                        SampleName = $"图号{sampleIndex + 1}",
                        CycleValues = new ObservableCollection<string>()
                    };

                    // 收集该样品所有轮次的值用于统计（计算重复精度）
                    var cycleValues = new List<double>();

                    for (int cycleIndex = 0; cycleIndex < _loopCycle; cycleIndex++)
                    {
                        double value = dataMatrix[sampleIndex, cycleIndex];
                        row.CycleValues.Add(value.ToString("F3"));
                        cycleValues.Add(value);
                    }

                    // 计算统计值（同一样品不同轮次的重复精度）
                    if (cycleValues.Count > 0 && cycleValues.Any(v => v != 0))
                    {
                        var nonZeroValues = cycleValues.Where(v => v != 0).ToList();
                        if (nonZeroValues.Count > 0)
                        {
                            row.MaxValue = nonZeroValues.Max().ToString("F3");
                            row.MinValue = nonZeroValues.Min().ToString("F3");
                            row.AvgValue = nonZeroValues.Average().ToString("F3");
                            row.RangeValue = (nonZeroValues.Max() - nonZeroValues.Min()).ToString("F3");
                        }
                        else
                        {
                            SetEmptyStats(row);
                        }
                    }
                    else
                    {
                        SetEmptyStats(row);
                    }

                    dataRows.Add(row);
                }

                ResultsDataGrid.ItemsSource = dataRows;

                // 更新全局统计
                UpdateGlobalStatistics(projectName);

                LogManager.Info($"显示项目 {projectName} 的数据完成");
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示项目数据失败: {ex.Message}");
            }
        }

        private void SetEmptyStats(ValidatorResultRow row)
        {
            row.MaxValue = "0.000";
            row.MinValue = "0.000";
            row.AvgValue = "0.000";
            row.RangeValue = "0.000";
        }

        /// <summary>
        /// 添加统计列（最大值、最小值、均值、差值）
        /// </summary>
        private void AddStatisticsColumns()
        {
            // 最大值列
            var maxColumn = new DataGridTextColumn
            {
                Header = "最大值",
                Binding = new Binding("MaxValue"),
                Width = 70,
                IsReadOnly = true
            };
            var maxStyle = new Style(typeof(DataGridCell));
            maxStyle.Setters.Add(new Setter(BackgroundProperty, System.Windows.Media.Brushes.LightCoral));
            maxStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            maxColumn.CellStyle = maxStyle;
            ResultsDataGrid.Columns.Add(maxColumn);

            // 最小值列
            var minColumn = new DataGridTextColumn
            {
                Header = "最小值",
                Binding = new Binding("MinValue"),
                Width = 70,
                IsReadOnly = true
            };
            var minStyle = new Style(typeof(DataGridCell));
            minStyle.Setters.Add(new Setter(BackgroundProperty, System.Windows.Media.Brushes.LightBlue));
            minStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            minColumn.CellStyle = minStyle;
            ResultsDataGrid.Columns.Add(minColumn);

            // 均值列
            var avgColumn = new DataGridTextColumn
            {
                Header = "均值",
                Binding = new Binding("AvgValue"),
                Width = 70,
                IsReadOnly = true
            };
            var avgStyle = new Style(typeof(DataGridCell));
            avgStyle.Setters.Add(new Setter(BackgroundProperty, System.Windows.Media.Brushes.LightGreen));
            avgStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            avgColumn.CellStyle = avgStyle;
            ResultsDataGrid.Columns.Add(avgColumn);

            // 差值列
            var rangeColumn = new DataGridTextColumn
            {
                Header = "差值",
                Binding = new Binding("RangeValue"),
                Width = 70,
                IsReadOnly = true
            };
            var rangeStyle = new Style(typeof(DataGridCell));
            rangeStyle.Setters.Add(new Setter(BackgroundProperty, System.Windows.Media.Brushes.LightYellow));
            rangeStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            rangeColumn.CellStyle = rangeStyle;
            ResultsDataGrid.Columns.Add(rangeColumn);
        }

        /// <summary>
        /// 更新重复精度分析 - 计算各图号最大极差
        /// 极差 = 同一图号在不同轮次的最大值 - 最小值
        /// 最大极差 = 所有图号极差中的最大值
        /// </summary>
        private void UpdateGlobalStatistics(string projectName)
        {
            try
            {
                if (!_projectResultsMatrix.ContainsKey(projectName))
                    return;

                var dataMatrix = _projectResultsMatrix[projectName];
                var sampleRanges = new List<double>();

                // 计算每个图号（样品）的极差
                for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
                {
                    var cycleValues = new List<double>();

                    // 收集该样品所有轮次的非零值
                    for (int cycleIndex = 0; cycleIndex < _loopCycle; cycleIndex++)
                    {
                        double value = dataMatrix[sampleIndex, cycleIndex];
                        if (value != 0)
                        {
                            cycleValues.Add(value);
                        }
                    }

                    // 计算该样品的极差（重复精度）
                    if (cycleValues.Count > 1)
                    {
                        double range = cycleValues.Max() - cycleValues.Min();
                        sampleRanges.Add(range);
                    }
                }

                // 显示各图号中的最大极差
                if (sampleRanges.Count > 0)
                {
                    double maxRange = sampleRanges.Max();
                    MaxRangeTextBlock.Text = maxRange.ToString("F3");
                }
                else
                {
                    MaxRangeTextBlock.Text = "--";
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新重复精度分析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出Excel按钮点击
        /// </summary>
        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            ExportData("xlsx");
        }

        /// <summary>
        /// 导出CSV按钮点击
        /// </summary>
        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            ExportData("csv");
        }

        /// <summary>
        /// 导出数据
        /// </summary>
        private void ExportData(string format)
        {
            try
            {
                var saveDialog = new System.Windows.Forms.SaveFileDialog
                {
                    Filter = format == "xlsx" ? "Excel文件|*.xlsx" : "CSV文件|*.csv",
                    DefaultExt = $".{format}",
                    FileName = $"验机结果_{_lotNumber}_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (format == "csv")
                    {
                        ExportToCsv(saveDialog.FileName);
                    }
                    else
                    {
                        // 简化处理：也导出为CSV格式
                        ExportToCsv(saveDialog.FileName.Replace(".xlsx", ".csv"));
                    }
                    MessageBox.Show($"数据导出成功: {saveDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    LogManager.Info($"验机结果数据已导出到: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"导出数据失败: {ex.Message}");
                MessageBox.Show($"导出数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出为CSV格式
        /// 行: 样品（图号），列: 轮次 + 统计列
        /// 统计列计算的是同一样品不同轮次的重复精度
        /// </summary>
        private void ExportToCsv(string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 写入标题信息
                writer.WriteLine($"验机测试结果分析");
                writer.WriteLine($"LOT号,{_lotNumber}");
                writer.WriteLine($"样品数,{_sampleCount}");
                writer.WriteLine($"巡回次数,{_loopCycle}");
                writer.WriteLine($"导出时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();

                // 遍历所有项目
                foreach (var projectName in _projectNames)
                {
                    if (!_projectResultsMatrix.ContainsKey(projectName))
                        continue;

                    var dataMatrix = _projectResultsMatrix[projectName];

                    writer.WriteLine($"项目: {projectName}");

                    // 写入表头（图号 + 各轮次 + 统计列）
                    var headerParts = new List<string> { "图号" };
                    for (int cycle = 0; cycle < _loopCycle; cycle++)
                    {
                        headerParts.Add($"第{cycle + 1}次");
                    }
                    headerParts.AddRange(new[] { "最大值", "最小值", "均值", "差值" });
                    writer.WriteLine(string.Join(",", headerParts));

                    // 写入数据行（每行是一个样品/图号）
                    for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
                    {
                        var rowParts = new List<string> { $"图号{sampleIndex + 1}" };
                        var cycleValues = new List<double>();

                        for (int cycleIndex = 0; cycleIndex < _loopCycle; cycleIndex++)
                        {
                            double value = dataMatrix[sampleIndex, cycleIndex];
                            rowParts.Add(value.ToString("F3"));
                            cycleValues.Add(value);
                        }

                        // 计算统计值（同一样品不同轮次的重复精度）
                        var nonZeroValues = cycleValues.Where(v => v != 0).ToList();
                        if (nonZeroValues.Count > 0)
                        {
                            rowParts.Add(nonZeroValues.Max().ToString("F3"));
                            rowParts.Add(nonZeroValues.Min().ToString("F3"));
                            rowParts.Add(nonZeroValues.Average().ToString("F3"));
                            rowParts.Add((nonZeroValues.Max() - nonZeroValues.Min()).ToString("F3"));
                        }
                        else
                        {
                            rowParts.AddRange(new[] { "0.000", "0.000", "0.000", "0.000" });
                        }

                        writer.WriteLine(string.Join(",", rowParts));
                    }

                    writer.WriteLine();
                }
            }
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// 验机结果数据行
    /// </summary>
    public class ValidatorResultRow
    {
        public string SampleName { get; set; }
        public ObservableCollection<string> CycleValues { get; set; } = new ObservableCollection<string>();
        public string MaxValue { get; set; }
        public string MinValue { get; set; }
        public string AvgValue { get; set; }
        public string RangeValue { get; set; }
    }
}
