using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 模板与LOT号来源设置窗口
    /// </summary>
    public partial class RemoteSourceSettingWindow : Window
    {
        private RemoteSourceConfig _config;

        public RemoteSourceSettingWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载配置
            _config = RemoteSourceConfig.Load();

            // 设置界面状态
            if (_config.IsEnabled)
            {
                RemoteSourceRadio.IsChecked = true;
            }
            else
            {
                LocalSourceRadio.IsChecked = true;
            }

            LotFilePathTextBox.Text = _config.LotFilePath;
            TemplateFilePathTextBox.Text = _config.TemplateFilePath;

            // 转换毫秒为秒
            int intervalSeconds = _config.CheckIntervalMs / 1000;
            IntervalTextBox.Text = intervalSeconds.ToString();

            // 更新状态显示
            UpdateStatusDisplay();
        }

        private void SourceRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (RemoteConfigGroup == null) return;

            bool isRemote = RemoteSourceRadio.IsChecked == true;
            RemoteConfigGroup.Visibility = isRemote ? Visibility.Visible : Visibility.Collapsed;
            TestButton.IsEnabled = isRemote;
        }

        private void BrowseLotFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择LOT号文件",
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                CheckFileExists = false // 允许选择可能暂时不存在的网络文件
            };

            if (dialog.ShowDialog() == true)
            {
                LotFilePathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseTemplateFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择模板名文件",
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                CheckFileExists = false // 允许选择可能暂时不存在的网络文件
            };

            if (dialog.ShowDialog() == true)
            {
                TemplateFilePathTextBox.Text = dialog.FileName;
            }
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            string lotPath = LotFilePathTextBox.Text.Trim();
            string templatePath = TemplateFilePathTextBox.Text.Trim();

            if (string.IsNullOrEmpty(lotPath) && string.IsNullOrEmpty(templatePath))
            {
                MessageBox.Show("请至少输入一个文件路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestButton.IsEnabled = false;
            TestButton.Content = "测试中...";

            try
            {
                string resultMessage = "测试结果:\n\n";
                bool hasError = false;

                // 测试LOT文件
                if (!string.IsNullOrEmpty(lotPath))
                {
                    resultMessage += "【LOT文件】\n";
                    string absolutePath = Path.IsPathRooted(lotPath)
                        ? lotPath
                        : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, lotPath));

                    if (!File.Exists(absolutePath))
                    {
                        resultMessage += $"  ✗ 文件不存在: {absolutePath}\n\n";
                        hasError = true;
                    }
                    else
                    {
                        using (var fs = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var sr = new StreamReader(fs))
                        {
                            string content = await sr.ReadToEndAsync();
                            string lotValue = content.Trim();
                            resultMessage += $"  ✓ 文件存在: {absolutePath}\n";
                            resultMessage += $"  LOT值: {(string.IsNullOrEmpty(lotValue) ? "(为空)" : lotValue)}\n\n";
                        }
                    }
                }

                // 测试模板文件
                if (!string.IsNullOrEmpty(templatePath))
                {
                    resultMessage += "【模板文件】\n";
                    string absolutePath = Path.IsPathRooted(templatePath)
                        ? templatePath
                        : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, templatePath));

                    if (!File.Exists(absolutePath))
                    {
                        resultMessage += $"  ✗ 文件不存在: {absolutePath}\n\n";
                        hasError = true;
                    }
                    else
                    {
                        using (var fs = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var sr = new StreamReader(fs))
                        {
                            string content = await sr.ReadToEndAsync();
                            string templateName = content.Trim();
                            resultMessage += $"  ✓ 文件存在: {absolutePath}\n";
                            resultMessage += $"  模板名: {(string.IsNullOrEmpty(templateName) ? "(为空)" : templateName)}\n";

                            if (!string.IsNullOrEmpty(templateName))
                            {
                                string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                                string templateFilePath = Path.Combine(templatesDir, $"{templateName}.json");

                                if (File.Exists(templateFilePath))
                                {
                                    resultMessage += $"  ✓ 模板文件存在: {templateFilePath}\n";
                                }
                                else
                                {
                                    resultMessage += $"  ⚠ 警告: 模板文件不存在: {templateFilePath}\n";
                                }
                            }
                        }
                    }
                }

                MessageBox.Show(resultMessage, hasError ? "测试完成（有错误）" : "测试成功",
                    MessageBoxButton.OK, hasError ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestButton.IsEnabled = true;
                TestButton.Content = "测试连接";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool isRemote = RemoteSourceRadio.IsChecked == true;

            // 验证输入
            if (isRemote)
            {
                string lotPath = LotFilePathTextBox.Text.Trim();
                string templatePath = TemplateFilePathTextBox.Text.Trim();

                if (string.IsNullOrEmpty(lotPath) && string.IsNullOrEmpty(templatePath))
                {
                    MessageBox.Show("请至少输入一个文件路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(IntervalTextBox.Text.Trim(), out int intervalSeconds) || intervalSeconds < 1)
                {
                    MessageBox.Show("检测间隔必须是大于0的整数（秒）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _config.LotFilePath = lotPath;
                _config.TemplateFilePath = templatePath;
                _config.CheckIntervalMs = intervalSeconds * 1000;
            }

            _config.IsEnabled = isRemote;
            _config.Save();

            // 重新加载监控服务
            RemoteFileMonitorService.Instance.Reload();

            string statusMsg = isRemote
                ? $"已启用远程监控，将每 {_config.CheckIntervalMs / 1000} 秒检测一次文件变化"
                : "已切换为本地模式，远程监控已停止";

            MessageBox.Show(statusMsg, "设置已保存", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateStatusDisplay()
        {
            var service = RemoteFileMonitorService.Instance;

            StatusText.Text = service.IsRunning ? "运行中" : "未启动";
            StatusText.Foreground = service.IsRunning
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.Yellow;

            // 获取Page1的当前值
            if (Page1.PageManager.Page1Instance != null)
            {
                CurrentLotText.Text = Page1.PageManager.Page1Instance.CurrentLotValue ?? "--";
                CurrentTemplateText.Text = Page1.PageManager.Page1Instance.CurrentTemplateName ?? "--";
            }
        }
    }
}
