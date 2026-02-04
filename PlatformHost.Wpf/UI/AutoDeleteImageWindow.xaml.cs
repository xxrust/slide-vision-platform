using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json;
using WpfApp2.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// AutoDeleteImageWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AutoDeleteImageWindow : Window
    {
        private Timer autoCheckTimer;
        private string targetFolder;
        private AutoDeleteConfig config;
        private readonly string configFilePath;
        private bool isAutoCheckRunning = false;
        private DateTime nextCheckTime;

        public AutoDeleteImageWindow()
        {
            InitializeComponent();

            // 配置文件路径
            configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AutoDeleteConfig.json");

            // 设置目标文件夹
            targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "原图存储");
            TargetFolderText.Text = targetFolder;

            // 初始化定时器（先初始化定时器）
            InitializeTimer();

            // 加载配置（包括自动启动检测）
            LoadConfig();

            // 更新磁盘空间信息
            UpdateDiskSpaceInfo();

            AddLog("自动删图配置窗口已打开");
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimer()
        {
            autoCheckTimer = new Timer();
            autoCheckTimer.Interval = 3600000; // 1小时 = 3600000毫秒
            autoCheckTimer.Elapsed += AutoCheckTimer_Elapsed;
        }

        /// <summary>
        /// 定时器触发事件
        /// </summary>
        private void AutoCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog($"[{DateTime.Now:HH:mm:ss}] 自动检测开始...");
                ExecuteDeleteStrategies();
                UpdateNextCheckTime();
            });
        }

        /// <summary>
        /// 更新下次检测时间
        /// </summary>
        private void UpdateNextCheckTime()
        {
            if (isAutoCheckRunning)
            {
                nextCheckTime = DateTime.Now.AddHours(1);
                NextCheckTimeText.Text = $"下次检测时间：{nextCheckTime:HH:mm:ss}";
            }
            else
            {
                NextCheckTimeText.Text = "下次检测时间：--";
            }
        }

        /// <summary>
        /// 更新磁盘空间信息
        /// </summary>
        private void UpdateDiskSpaceInfo()
        {
            try
            {
                DriveInfo drive = new DriveInfo("D");
                double freeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                CurrentDiskSpaceText.Text = $"D盘剩余空间：{freeSpaceGB:F2} GB";

                // 根据剩余空间显示不同颜色
                if (freeSpaceGB < 50)
                {
                    CurrentDiskSpaceText.Foreground = System.Windows.Media.Brushes.Red;
                }
                else if (freeSpaceGB < 100)
                {
                    CurrentDiskSpaceText.Foreground = System.Windows.Media.Brushes.Yellow;
                }
                else
                {
                    CurrentDiskSpaceText.Foreground = System.Windows.Media.Brushes.LightGreen;
                }
            }
            catch (Exception ex)
            {
                CurrentDiskSpaceText.Text = "D盘剩余空间：无法获取";
                AddLog($"获取磁盘空间失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行删除策略
        /// </summary>
        private void ExecuteDeleteStrategies()
        {
            UpdateDiskSpaceInfo();

            if (!Directory.Exists(targetFolder))
            {
                AddLog($"目标文件夹不存在: {targetFolder}");
                return;
            }

            int deletedCount = 0;
            long deletedSize = 0;

            // 策略1：按天数删除
            if (EnableDeleteByDaysCheckBox.IsChecked == true)
            {
                if (int.TryParse(DaysTextBox.Text, out int days))
                {
                    var result = DeleteImagesByDays(days);
                    deletedCount += result.Item1;
                    deletedSize += result.Item2;
                }
            }

            // 策略2：按磁盘空间删除
            if (EnableDeleteBySpaceCheckBox.IsChecked == true)
            {
                if (double.TryParse(TriggerSpaceTextBox.Text, out double triggerSpace) &&
                    double.TryParse(TargetSpaceTextBox.Text, out double targetSpace))
                {
                    var result = DeleteImagesByDiskSpace(triggerSpace, targetSpace);
                    deletedCount += result.Item1;
                    deletedSize += result.Item2;
                }
            }

            // 更新状态
            LastCheckTimeText.Text = $"上次检测时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            UpdateDiskSpaceInfo();

            if (deletedCount > 0)
            {
                double deletedSizeMB = deletedSize / 1024.0 / 1024.0;
                AddLog($"删除完成：共删除 {deletedCount} 个文件，释放 {deletedSizeMB:F2} MB 空间");
            }
            else
            {
                AddLog("没有需要删除的文件");
            }
        }

        /// <summary>
        /// 按天数删除图片
        /// </summary>
        private Tuple<int, long> DeleteImagesByDays(int days)
        {
            int deletedCount = 0;
            long deletedSize = 0;
            DateTime cutoffDate = DateTime.Now.AddDays(-days);

            AddLog($"执行策略1：删除 {days} 天前的图片（{cutoffDate:yyyy-MM-dd} 之前）");

            try
            {
                var files = Directory.GetFiles(targetFolder, "*.bmp", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(targetFolder, "*.jpg", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(targetFolder, "*.png", SearchOption.AllDirectories));

                foreach (var file in files)
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try
                        {
                            deletedSize += fileInfo.Length;
                            File.Delete(file);
                            deletedCount++;

                            if (deletedCount % 100 == 0)
                            {
                                AddLog($"已删除 {deletedCount} 个文件...");
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"删除文件失败 {Path.GetFileName(file)}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"按天数删除失败: {ex.Message}");
            }

            return new Tuple<int, long>(deletedCount, deletedSize);
        }

        /// <summary>
        /// 按磁盘空间删除图片
        /// </summary>
        private Tuple<int, long> DeleteImagesByDiskSpace(double triggerSpaceGB, double targetSpaceGB)
        {
            int deletedCount = 0;
            long deletedSize = 0;

            try
            {
                DriveInfo drive = new DriveInfo("D");
                double freeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

                if (freeSpaceGB >= triggerSpaceGB)
                {
                    AddLog($"D盘剩余空间 {freeSpaceGB:F2} GB >= {triggerSpaceGB} GB，不需要删除");
                    return new Tuple<int, long>(0, 0);
                }

                AddLog($"执行策略2：D盘剩余空间 {freeSpaceGB:F2} GB < {triggerSpaceGB} GB，开始删除...");

                // 获取所有图片文件并按创建时间排序（从旧到新）
                var files = Directory.GetFiles(targetFolder, "*.bmp", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(targetFolder, "*.jpg", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(targetFolder, "*.png", SearchOption.AllDirectories))
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.CreationTime)
                    .ToList();

                foreach (var fileInfo in files)
                {
                    if (freeSpaceGB >= targetSpaceGB)
                    {
                        AddLog($"已达到目标空间 {targetSpaceGB} GB，停止删除");
                        break;
                    }

                    try
                    {
                        deletedSize += fileInfo.Length;
                        fileInfo.Delete();
                        deletedCount++;

                        // 更新剩余空间
                        freeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

                        if (deletedCount % 100 == 0)
                        {
                            AddLog($"已删除 {deletedCount} 个文件，当前剩余空间 {freeSpaceGB:F2} GB");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"删除文件失败 {fileInfo.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"按磁盘空间删除失败: {ex.Message}");
            }

            return new Tuple<int, long>(deletedCount, deletedSize);
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        private void AddLog(string message)
        {
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            LogTextBox.AppendText(logMessage);
            LogTextBox.ScrollToEnd();
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    config = JsonConvert.DeserializeObject<AutoDeleteConfig>(json);

                    // 应用配置到UI
                    EnableDeleteByDaysCheckBox.IsChecked = config.EnableDeleteByDays;
                    DaysTextBox.Text = config.Days.ToString();
                    EnableDeleteBySpaceCheckBox.IsChecked = config.EnableDeleteBySpace;
                    TriggerSpaceTextBox.Text = config.TriggerSpaceGB.ToString();
                    TargetSpaceTextBox.Text = config.TargetSpaceGB.ToString();

                    // 更新Panel的启用状态
                    DeleteByDaysPanel.IsEnabled = config.EnableDeleteByDays;
                    DeleteBySpacePanel.IsEnabled = config.EnableDeleteBySpace;

                    // 如果自动检测已启用，则启动
                    if (config.AutoCheckEnabled)
                    {
                        // 延迟启动以确保UI已完全初始化
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            StartAutoCheck();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }

                    AddLog("配置已加载");
                }
                else
                {
                    config = new AutoDeleteConfig();
                    AddLog("使用默认配置");
                }
            }
            catch (Exception ex)
            {
                config = new AutoDeleteConfig();
                AddLog($"加载配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfig()
        {
            try
            {
                config = new AutoDeleteConfig
                {
                    EnableDeleteByDays = EnableDeleteByDaysCheckBox.IsChecked == true,
                    Days = int.TryParse(DaysTextBox.Text, out int days) ? days : 7,
                    EnableDeleteBySpace = EnableDeleteBySpaceCheckBox.IsChecked == true,
                    TriggerSpaceGB = double.TryParse(TriggerSpaceTextBox.Text, out double trigger) ? trigger : 50,
                    TargetSpaceGB = double.TryParse(TargetSpaceTextBox.Text, out double target) ? target : 100,
                    AutoCheckEnabled = isAutoCheckRunning
                };

                string configDir = Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFilePath, json);

                AddLog("配置已保存");
                MessageBox.Show("配置保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"保存配置失败: {ex.Message}");
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 启动自动检测
        /// </summary>
        private void StartAutoCheck()
        {
            if (!isAutoCheckRunning)
            {
                isAutoCheckRunning = true;
                autoCheckTimer.Start();

                // 更新UI状态
                if (AutoCheckStatusText != null)
                {
                    AutoCheckStatusText.Text = "自动检测：已启用";
                    AutoCheckStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                }

                if (StartAutoCheckButton != null)
                {
                    StartAutoCheckButton.Content = "⏸ 停止自动检测";
                }

                UpdateNextCheckTime();
                AddLog("自动检测已启动，每小时执行一次");
            }
        }

        /// <summary>
        /// 停止自动检测
        /// </summary>
        private void StopAutoCheck()
        {
            if (isAutoCheckRunning)
            {
                autoCheckTimer.Stop();
                isAutoCheckRunning = false;
                AutoCheckStatusText.Text = "自动检测：未启用";
                AutoCheckStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                StartAutoCheckButton.Content = "⏰ 启动自动检测";
                NextCheckTimeText.Text = "下次检测时间：--";
                AddLog("自动检测已停止");
            }
        }

        // 事件处理器
        private void EnableDeleteByDaysCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            DeleteByDaysPanel.IsEnabled = true;
        }

        private void EnableDeleteByDaysCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            DeleteByDaysPanel.IsEnabled = false;
        }

        private void EnableDeleteBySpaceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            DeleteBySpacePanel.IsEnabled = true;
        }

        private void EnableDeleteBySpaceCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            DeleteBySpacePanel.IsEnabled = false;
        }

        private void ManualExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要立即执行删除操作吗？\n此操作不可恢复！", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                AddLog("手动执行删除操作...");
                ExecuteDeleteStrategies();
            }
        }

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }

        private void StartAutoCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAutoCheckRunning)
            {
                StopAutoCheck();
                config.AutoCheckEnabled = false;
            }
            else
            {
                StartAutoCheck();
                config.AutoCheckEnabled = true;
            }

            // 保存自动检测状态
            SaveAutoCheckStatus();
        }

        /// <summary>
        /// 仅保存自动检测状态
        /// </summary>
        private void SaveAutoCheckStatus()
        {
            try
            {
                // 更新自动检测状态
                config.AutoCheckEnabled = isAutoCheckRunning;

                string configDir = Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFilePath, json);

                AddLog($"自动检测状态已保存: {(isAutoCheckRunning ? "启用" : "停用")}");
            }
            catch (Exception ex)
            {
                AddLog($"保存自动检测状态失败: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (autoCheckTimer != null)
            {
                autoCheckTimer.Dispose();
            }
        }
    }

    /// <summary>
    /// 自动删除配置类
    /// </summary>
    public class AutoDeleteConfig
    {
        public bool EnableDeleteByDays { get; set; } = false;
        public int Days { get; set; } = 7;
        public bool EnableDeleteBySpace { get; set; } = false;
        public double TriggerSpaceGB { get; set; } = 50;
        public double TargetSpaceGB { get; set; } = 100;
        public bool AutoCheckEnabled { get; set; } = false;
    }
}