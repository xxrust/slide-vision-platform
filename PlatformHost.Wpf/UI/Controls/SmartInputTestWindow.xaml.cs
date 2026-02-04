using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// 智能输入卡片测试窗口
    /// </summary>
    public partial class SmartInputTestWindow : Window
    {
        public SmartInputTestWindow()
        {
            InitializeComponent();
            InitializeTestWindow();
        }

        private void InitializeTestWindow()
        {
            try
            {
                // 为了演示目的，在窗口加载后短暂延迟以确保所有控件都已初始化
                Loaded += (s, e) =>
                {
                    LogMessage("智能输入卡片测试窗口已加载");
                    LogMessage("您可以点击任何带蓝色边框的输入框来测试智能输入功能");
                };
            }
            catch (Exception ex)
            {
                LogMessage($"初始化测试窗口失败: {ex.Message}");
            }
        }

        private void GetAllValues_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var values = new StringBuilder();
                values.AppendLine("=== 当前参数值 ===");
                values.AppendLine($"PKG设定宽度: {PkgWidthTextBox.Text} mm");
                values.AppendLine($"PKG设定高度: {PkgHeightTextBox.Text} mm");
                values.AppendLine($"晶片设定宽度: {ChipWidthTextBox.Text} μm");
                values.AppendLine($"胶点设定直径: {GluePointTextBox.Text} μm");
                values.AppendLine($"检测区域X坐标: {DetectionXTextBox.Text} pixel");
                values.AppendLine($"阈值设定: {ThresholdTextBox.Text}");
                values.AppendLine($"自定义参数: {CustomTextBox.Text} mm");

                MessageBox.Show(values.ToString(), "当前参数值", MessageBoxButton.OK, MessageBoxImage.Information);
                LogMessage("已显示所有参数值");
            }
            catch (Exception ex)
            {
                LogMessage($"获取参数值失败: {ex.Message}");
                MessageBox.Show($"获取参数值时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetValues_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PkgWidthTextBox.Text = "10.5";
                PkgHeightTextBox.Text = "8.2";
                ChipWidthTextBox.Text = "2500";
                GluePointTextBox.Text = "150";
                DetectionXTextBox.Text = "100";
                ThresholdTextBox.Text = "128";
                CustomTextBox.Text = "25.0";

                LogMessage("已重置所有参数为默认值");
                MessageBox.Show("所有参数已重置为默认值", "重置完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"重置参数失败: {ex.Message}");
                MessageBox.Show($"重置参数时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("关闭智能输入卡片测试窗口");
                
                // 关闭当前可能打开的智能输入卡片
                TextBoxSmartInputExtensions.CloseCurrentWindow();
                
                Close();
            }
            catch (Exception ex)
            {
                LogMessage($"关闭窗口失败: {ex.Message}");
                Close(); // 强制关闭
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 确保关闭所有相关的智能输入窗口
                TextBoxSmartInputExtensions.CloseCurrentWindow();
                LogMessage("智能输入卡片测试窗口已关闭");
            }
            catch (Exception ex)
            {
                // 忽略关闭时的错误，因为窗口已经在关闭过程中
                Console.WriteLine($"关闭窗口时发生错误: {ex.Message}");
            }
            
            base.OnClosed(e);
        }

        private void LogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] SmartInputTest: {message}");
                
                // 如果LogManager可用，也记录到日志
                if (typeof(WpfApp2.UI.Models.LogManager) != null)
                {
                    WpfApp2.UI.Models.LogManager.Info(message, "智能输入测试");
                }
            }
            catch
            {
                // 忽略日志记录错误
            }
        }

        /// <summary>
        /// 静态方法：显示测试窗口
        /// </summary>
        public static void ShowTestWindow()
        {
            try
            {
                var testWindow = new SmartInputTestWindow();
                testWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开测试窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 静态方法：显示模态测试窗口
        /// </summary>
        public static bool? ShowTestWindowDialog()
        {
            try
            {
                var testWindow = new SmartInputTestWindow();
                return testWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开测试窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
} 