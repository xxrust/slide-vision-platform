using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp2.UI.Models;
using static WpfApp2.UI.Page1;

namespace WpfApp2.UI
{
    public partial class LotSettingWindow : Window
    {
        private string originalLotValue;
        private const string LOT_CONFIG_FILE = "LotConfig.txt";
        private string configFilePath;

        public string LotValue { get; private set; }

        public LotSettingWindow(string currentLotValue)
        {
            InitializeComponent();
            originalLotValue = currentLotValue;
            LotTextBox.Text = currentLotValue;
            configFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", LOT_CONFIG_FILE);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置焦点到文本框
            LotTextBox.Focus();
            LotTextBox.SelectAll();

            if (VirtualKeyboard != null)
            {
                VirtualKeyboard.TargetTextBox = LotTextBox;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 保存LOT值
            LotValue = LotTextBox.Text;

            // 将LOT值保存到配置文件
            SaveLotValueToFile(LotValue);

            // 记录日志（但不要提前更新Page1的LOT值，让Page1的LOT_MouseDown来处理）
            LogManager.Info($"LOT设置窗口：用户确认新LOT值为 {LotValue}");

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 恢复原始值
            LotValue = originalLotValue;
            DialogResult = false;
            Close();
        }

        private void SaveLotValueToFile(string lotValue)
        {
            try
            {
                // 确保Config目录存在
                string configDir = System.IO.Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // 保存LOT值到文件
                File.WriteAllText(configFilePath, lotValue);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存LOT值失败: {ex.Message}", "保存错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Info($"保存LOT值失败: {ex.Message}");
            }
        }

        public static string LoadLotValueFromFile()
        {
            string configFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", LOT_CONFIG_FILE);

            try
            {
                if (File.Exists(configFilePath))
                {
                    return File.ReadAllText(configFilePath).Trim();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载LOT值失败: {ex.Message}", "加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Info($"加载LOT值失败: {ex.Message}");
            }

            return "LOT_error"; // 默认值
        }
    }
}

