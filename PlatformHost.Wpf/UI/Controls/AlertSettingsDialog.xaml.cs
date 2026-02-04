using System;
using System.Windows;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// 告警设置对话框（旧版入口，仍保持单组合界面以兼容历史逻辑）
    /// </summary>
    public partial class AlertSettingsDialog : Window
    {
        public AlertSettings AlertSettings { get; private set; }

        public AlertSettingsDialog(AlertSettings currentSettings)
        {
            InitializeComponent();
            AlertSettings = currentSettings ?? new AlertSettings();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                EnableAlertCheckBox.IsChecked = AlertSettings.IsEnabled;
                StatisticsCycleTextBox.Text = AlertSettings.StatisticsCycle.ToString();
                MinSampleSizeTextBox.Text = AlertSettings.MinSampleSize.ToString();

                EnableCountAnalysisCheckBox.IsChecked = AlertSettings.EnableCountAnalysis;
                OutOfRangeThresholdTextBox.Text = AlertSettings.OutOfRangeThreshold.ToString();

                EnableProcessCapabilityCheckBox.IsChecked = AlertSettings.EnableProcessCapabilityAnalysis;
                CAThresholdTextBox.Text = AlertSettings.CAThreshold.ToString("F3");
                CPThresholdTextBox.Text = AlertSettings.CPThreshold.ToString("F3");
                CPKThresholdTextBox.Text = AlertSettings.CPKThreshold.ToString("F3");

                EnableConsecutiveNGCheckBox.IsChecked = AlertSettings.EnableConsecutiveNGAnalysis;
                ConsecutiveNGThresholdTextBox.Text = AlertSettings.ConsecutiveNGThreshold.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateAndSaveSettings()
        {
            try
            {
                if (!int.TryParse(StatisticsCycleTextBox.Text, out int statisticsCycle) || statisticsCycle <= 0)
                {
                    MessageBox.Show("统计周期必须是大于0的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatisticsCycleTextBox.Focus();
                    return false;
                }

                if (!int.TryParse(MinSampleSizeTextBox.Text, out int minSampleSize) || minSampleSize <= 0)
                {
                    MessageBox.Show("最小样本量必须是大于0的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    MinSampleSizeTextBox.Focus();
                    return false;
                }

                if (!int.TryParse(OutOfRangeThresholdTextBox.Text, out int outOfRangeThreshold) || outOfRangeThreshold <= 0)
                {
                    MessageBox.Show("超限次数阈值必须是大于0的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OutOfRangeThresholdTextBox.Focus();
                    return false;
                }

                if (!double.TryParse(CAThresholdTextBox.Text, out double caThreshold) || caThreshold <= 0)
                {
                    MessageBox.Show("CA阈值必须是大于0的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CAThresholdTextBox.Focus();
                    return false;
                }

                if (!double.TryParse(CPThresholdTextBox.Text, out double cpThreshold) || cpThreshold <= 0)
                {
                    MessageBox.Show("CP阈值必须是大于0的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CPThresholdTextBox.Focus();
                    return false;
                }

                if (!double.TryParse(CPKThresholdTextBox.Text, out double cpkThreshold) || cpkThreshold <= 0)
                {
                    MessageBox.Show("CPK阈值必须是大于0的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CPKThresholdTextBox.Focus();
                    return false;
                }

                if (!int.TryParse(ConsecutiveNGThresholdTextBox.Text, out int consecutiveNGThreshold) || consecutiveNGThreshold <= 0)
                {
                    MessageBox.Show("连续NG阈值必须是大于0的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ConsecutiveNGThresholdTextBox.Focus();
                    return false;
                }

                bool hasEnabledStrategy = (EnableCountAnalysisCheckBox.IsChecked == true) ||
                                          (EnableProcessCapabilityCheckBox.IsChecked == true) ||
                                          (EnableConsecutiveNGCheckBox.IsChecked == true);

                if (EnableAlertCheckBox.IsChecked == true && !hasEnabledStrategy)
                {
                    MessageBox.Show("启用告警功能时必须至少选择一种告警策略", "设置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                AlertSettings.IsEnabled = EnableAlertCheckBox.IsChecked == true;
                AlertSettings.StatisticsCycle = statisticsCycle;
                AlertSettings.MinSampleSize = minSampleSize;

                AlertSettings.EnableCountAnalysis = EnableCountAnalysisCheckBox.IsChecked == true;
                AlertSettings.OutOfRangeThreshold = outOfRangeThreshold;

                AlertSettings.EnableProcessCapabilityAnalysis = EnableProcessCapabilityCheckBox.IsChecked == true;
                AlertSettings.CAThreshold = caThreshold;
                AlertSettings.CPThreshold = cpThreshold;
                AlertSettings.CPKThreshold = cpkThreshold;

                AlertSettings.EnableConsecutiveNGAnalysis = EnableConsecutiveNGCheckBox.IsChecked == true;
                AlertSettings.ConsecutiveNGThreshold = consecutiveNGThreshold;

                AlertSettings.Save();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateAndSaveSettings())
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
