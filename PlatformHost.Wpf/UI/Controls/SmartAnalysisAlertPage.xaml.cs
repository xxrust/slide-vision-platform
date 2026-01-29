using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OfficeOpenXml;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// SmartAnalysisAlertPage.xaml 的交互逻辑
    /// </summary>
    public partial class SmartAnalysisAlertPage : UserControl
    {
        public event EventHandler BackRequested;

        private ObservableCollection<AlertRecord> _alertRecords;

        public SmartAnalysisAlertPage()
        {
            InitializeComponent();
            _alertRecords = new ObservableCollection<AlertRecord>();
            AlertRecordDataGrid.ItemsSource = _alertRecords;
            LoadAlertRecords();
        }

        /// <summary>
        /// 加载告警记录
        /// </summary>
        public void LoadAlertRecords()
        {
            try
            {
                _alertRecords.Clear();
                var records = AlertRecordManager.GetAllRecords();
                
                foreach (var record in records.OrderByDescending(r => r.Timestamp))
                {
                    _alertRecords.Add(record);
                }

                UpdateStatistics();
                UpdateEmptyMessage();
                
                LogManager.Info($"已加载 {records.Count} 条告警记录");
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载告警记录失败: {ex.Message}");
                MessageBox.Show($"加载告警记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            try
            {
                var totalCount = _alertRecords.Count;
                var todayCount = _alertRecords.Count(r => r.Timestamp.Date == DateTime.Today);
                var lastAlertTime = _alertRecords.Count > 0 ? _alertRecords.First().Timestamp.ToString("yyyy-MM-dd HH:mm:ss") : "无";

                TotalAlertsText.Text = $"总告警次数: {totalCount}";
                RecentAlertsText.Text = $"今日告警: {todayCount}";
                LastAlertTimeText.Text = $"最后告警: {lastAlertTime}";
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新统计信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新空数据提示
        /// </summary>
        private void UpdateEmptyMessage()
        {
            EmptyMessageText.Visibility = _alertRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AlertRecordDataGrid.Visibility = _alertRecords.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAlertRecords();
        }

        /// <summary>
        /// 导出按钮点击
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_alertRecords.Count == 0)
                {
                    MessageBox.Show("没有数据可导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
                if (!Directory.Exists(exportDir))
                {
                    Directory.CreateDirectory(exportDir);
                }

                var fileName = $"告警记录_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = Path.Combine(exportDir, fileName);

                ExportToExcel(filePath);
                
                MessageBox.Show($"告警记录已导出到:\n{filePath}", "导出完成", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                LogManager.Info($"告警记录已导出: {fileName}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"导出告警记录失败: {ex.Message}");
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出到Excel
        /// </summary>
        private void ExportToExcel(string filePath)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("告警记录");

                // 写入表头
                worksheet.Cells[1, 1].Value = "时间";
                worksheet.Cells[1, 2].Value = "项目名称";
                worksheet.Cells[1, 3].Value = "告警类型";
                worksheet.Cells[1, 4].Value = "告警内容";
                worksheet.Cells[1, 5].Value = "数值详情";

                // 设置表头样式
                for (int col = 1; col <= 5; col++)
                {
                    worksheet.Cells[1, col].Style.Font.Bold = true;
                    worksheet.Cells[1, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // 写入数据
                for (int row = 0; row < _alertRecords.Count; row++)
                {
                    var record = _alertRecords[row];
                    worksheet.Cells[row + 2, 1].Value = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[row + 2, 2].Value = record.ItemName;
                    worksheet.Cells[row + 2, 3].Value = record.AlertType;
                    worksheet.Cells[row + 2, 4].Value = record.AlertMessage;
                    worksheet.Cells[row + 2, 5].Value = record.Details;
                }

                // 自动调整列宽
                worksheet.Cells.AutoFitColumns();

                // 保存文件
                package.SaveAs(new FileInfo(filePath));
            }
        }

        /// <summary>
        /// 清空记录按钮点击
        /// </summary>
        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "确定要清空所有告警记录吗？\n\n此操作将删除所有历史告警记录，且不可恢复！",
                    "确认清空",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    AlertRecordManager.ClearAllRecords();
                    LoadAlertRecords();
                    
                    MessageBox.Show("所有告警记录已清空", "清空完成", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    LogManager.Info("用户清空了所有告警记录");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"清空告警记录失败: {ex.Message}");
                MessageBox.Show($"清空失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 返回按钮点击
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 添加新的告警记录（供外部调用）
        /// </summary>
        public void AddAlertRecord(string itemName, string alertType, string alertMessage, string details = "")
        {
            try
            {
                var record = new AlertRecord
                {
                    Timestamp = DateTime.Now,
                    ItemName = itemName,
                    AlertType = alertType,
                    AlertMessage = alertMessage,
                    Details = details
                };

                AlertRecordManager.AddRecord(record);
                
                // 如果当前页面可见，刷新显示
                if (this.Visibility == Visibility.Visible)
                {
                    LoadAlertRecords();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"添加告警记录失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 告警记录数据模型
    /// </summary>
    public class AlertRecord
    {
        public DateTime Timestamp { get; set; }
        public string ItemName { get; set; }
        public string AlertType { get; set; }
        public string AlertMessage { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// 告警记录管理器
    /// </summary>
    public static class AlertRecordManager
    {
        private static List<AlertRecord> _records = new List<AlertRecord>();
        private static readonly object _lockObject = new object();
        private const int MaxRecords = 1000; // 最大存储1000条记录

        /// <summary>
        /// 添加告警记录
        /// </summary>
        public static void AddRecord(AlertRecord record)
        {
            lock (_lockObject)
            {
                _records.Add(record);
                
                // 限制记录数量
                while (_records.Count > MaxRecords)
                {
                    _records.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 获取所有记录
        /// </summary>
        public static List<AlertRecord> GetAllRecords()
        {
            lock (_lockObject)
            {
                return new List<AlertRecord>(_records);
            }
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public static void ClearAllRecords()
        {
            lock (_lockObject)
            {
                _records.Clear();
            }
        }

        /// <summary>
        /// 获取记录总数
        /// </summary>
        public static int GetTotalCount()
        {
            lock (_lockObject)
            {
                return _records.Count;
            }
        }
    }
} 