using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WpfApp2.SMTGPIO;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// ThreeDCalibrationWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ThreeDCalibrationWindow : Window
    {
        private const string NavigatorShortcutPath = @"C:\Users\Public\Desktop\LJ-X Navigator.lnk";

        private const string CurrentCoordAddress = "DM986.D";
        private const string TargetCoordAddress = "DM908.D";
        private const string ForwardCoordAddress = "DM904.D";
        private const string ReverseCoordAddress = "DM906.D";
        private const string TriggerRelayAddress = "MR1100";

        private readonly PLCSerialController _plcController;
        private readonly DispatcherTimer _pollTimer;

        private ThreeDCalibrationConfig _config;
        private float? _latestCurrentCoord;
        private bool _isBusy;
        private bool _isPolling;

        public ThreeDCalibrationWindow()
        {
            InitializeComponent();

            _plcController = PLCSerialController.Instance;
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _pollTimer.Tick += PollTimer_Tick;

            Loaded += ThreeDCalibrationWindow_Loaded;
            Closed += ThreeDCalibrationWindow_Closed;
        }

        private async void ThreeDCalibrationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _config = ThreeDCalibrationConfig.Load();
            GapTextBox.Text = _config.Gap.ToString("F2", CultureInfo.InvariantCulture);

            await RefreshCalibrationDisplayAsync();

            _pollTimer.Start();
            await PollCurrentCoordAsync();
        }

        private void ThreeDCalibrationWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                _pollTimer.Stop();
            }
            catch
            {
                // 窗口关闭时不阻塞关闭流程
            }
        }

        private async void PollTimer_Tick(object sender, EventArgs e)
        {
            await PollCurrentCoordAsync();
        }

        private async Task PollCurrentCoordAsync()
        {
            if (_isPolling || _isBusy)
            {
                return;
            }

            if (!_plcController.IsConnected)
            {
                StatusTextBlock.Text = "PLC未连接，无法读取当前坐标";
                return;
            }

            _isPolling = true;
            try
            {
                float current = await ReadPLCFloatValueAsync(CurrentCoordAddress);
                _latestCurrentCoord = current;
                CurrentCoordTextBlock.Text = current.ToString("F3", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"读取当前坐标失败: {ex.Message}";
            }
            finally
            {
                _isPolling = false;
            }
        }

        private float GetGapValue()
        {
            if (float.TryParse(GapTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float gap) && gap > 0)
            {
                return gap;
            }

            if (float.TryParse(GapTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out gap) && gap > 0)
            {
                return gap;
            }

            return 0.1f;
        }

        private void SetBusyState(bool isBusy, string status = null)
        {
            _isBusy = isBusy;

            OpenNavigatorButton.IsEnabled = !isBusy;
            ToScanCenterButton.IsEnabled = !isBusy;
            ToCalibrationButton.IsEnabled = !isBusy;
            SetCalibrationButton.IsEnabled = !isBusy;
            MoveBackwardButton.IsEnabled = !isBusy;
            MoveForwardButton.IsEnabled = !isBusy;

            if (!string.IsNullOrWhiteSpace(status))
            {
                StatusTextBlock.Text = status;
            }
        }

        private async Task RefreshCalibrationDisplayAsync()
        {
            try
            {
                if (_config?.CalibrationPosition.HasValue == true)
                {
                    CalibrationCoordTextBlock.Text = _config.CalibrationPosition.Value.ToString("F3", CultureInfo.InvariantCulture);
                    return;
                }

                if (!_plcController.IsConnected)
                {
                    CalibrationCoordTextBlock.Text = "--";
                    StatusTextBlock.Text = "校准位未配置（PLC未连接，无法计算扫描中心）";
                    return;
                }

                float center = await ReadScanCenterAsync();
                CalibrationCoordTextBlock.Text = $"{center.ToString("F3", CultureInfo.InvariantCulture)}（默认）";
                StatusTextBlock.Text = "校准位未配置，默认使用3D扫描中心";
            }
            catch (Exception ex)
            {
                CalibrationCoordTextBlock.Text = "--";
                StatusTextBlock.Text = $"刷新校准位显示失败: {ex.Message}";
            }
        }

        private async Task<float> ReadScanCenterAsync()
        {
            float forward = await ReadPLCFloatValueAsync(ForwardCoordAddress);
            float reverse = await ReadPLCFloatValueAsync(ReverseCoordAddress);
            return (forward + reverse) / 2.0f;
        }

        private async Task MoveToAsync(float target)
        {
            if (_isBusy)
            {
                return;
            }

            if (!_plcController.IsConnected)
            {
                MessageBox.Show("PLC未连接，无法执行移动", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusyState(true, $"正在写入目标坐标 {target.ToString("F3", CultureInfo.InvariantCulture)} -> {TargetCoordAddress} ...");
            try
            {
                bool writeOk = await WritePLCFloatValueAsync(TargetCoordAddress, target);
                if (!writeOk)
                {
                    throw new Exception($"写入{TargetCoordAddress}失败");
                }

                bool relayOk = await _plcController.SetRelayAsync(TriggerRelayAddress);
                if (!relayOk)
                {
                    throw new Exception($"置位{TriggerRelayAddress}失败");
                }

                StatusTextBlock.Text = $"已写入{TargetCoordAddress}并置位{TriggerRelayAddress}，目标={target.ToString("F3", CultureInfo.InvariantCulture)}";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"执行移动失败: {ex.Message}";
                MessageBox.Show($"执行移动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async Task<float> ReadPLCFloatValueAsync(string address)
        {
            try
            {
                int intValue = await _plcController.ReadSingleAsync(addrCombine: address);
                return BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);
            }
            catch (Exception ex) when (ex.Message.Contains("PLC响应不是数字格式"))
            {
                var match = Regex.Match(ex.Message, @"'(\d+)'");
                if (match.Success && uint.TryParse(match.Groups[1].Value, out uint uintValue))
                {
                    return BitConverter.ToSingle(BitConverter.GetBytes(uintValue), 0);
                }
                throw;
            }
        }

        private async Task<bool> WritePLCFloatValueAsync(string address, float floatValue)
        {
            int intValue = BitConverter.ToInt32(BitConverter.GetBytes(floatValue), 0);
            if (intValue >= 0)
            {
                return await _plcController.WriteSingleAsync(addrCombine: address, data: intValue);
            }

            uint uintValue = (uint)intValue;
            return await _plcController.WriteSingleUnsignedAsync(address, uintValue);
        }

        private void OpenNavigatorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(NavigatorShortcutPath))
                {
                    MessageBox.Show($"未找到快捷方式:\n{NavigatorShortcutPath}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = NavigatorShortcutPath,
                    UseShellExecute = true
                });

                StatusTextBlock.Text = "已打开LJ-X Navigator";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"打开LJ-X Navigator失败: {ex.Message}";
                MessageBox.Show($"打开LJ-X Navigator失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ToScanCenterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                float center = await ReadScanCenterAsync();
                await MoveToAsync(center);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"计算/移动到扫描中心失败: {ex.Message}";
                MessageBox.Show($"计算/移动到扫描中心失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ToCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                float target = _config?.CalibrationPosition ?? await ReadScanCenterAsync();
                await MoveToAsync(target);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"移动到校准位失败: {ex.Message}";
                MessageBox.Show($"移动到校准位失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SetCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_latestCurrentCoord.HasValue)
                {
                    if (!_plcController.IsConnected)
                    {
                        MessageBox.Show("PLC未连接，无法读取当前坐标作为校准位", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _latestCurrentCoord = await ReadPLCFloatValueAsync(CurrentCoordAddress);
                }

                _config.CalibrationPosition = _latestCurrentCoord.Value;
                _config.Gap = GetGapValue();
                _config.Save();

                CalibrationCoordTextBlock.Text = _config.CalibrationPosition.Value.ToString("F3", CultureInfo.InvariantCulture);
                StatusTextBlock.Text = $"已保存校准位: {_config.CalibrationPosition.Value.ToString("F3", CultureInfo.InvariantCulture)}";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"保存校准位失败: {ex.Message}";
                MessageBox.Show($"保存校准位失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MoveForwardButton_Click(object sender, RoutedEventArgs e)
        {
            await MoveByGapAsync(forward: true);
        }

        private async void MoveBackwardButton_Click(object sender, RoutedEventArgs e)
        {
            await MoveByGapAsync(forward: false);
        }

        private async Task MoveByGapAsync(bool forward)
        {
            try
            {
                float gap = GetGapValue();
                if (gap <= 0)
                {
                    MessageBox.Show("gap必须为正数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!_latestCurrentCoord.HasValue)
                {
                    if (!_plcController.IsConnected)
                    {
                        MessageBox.Show("PLC未连接，无法读取当前坐标", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _latestCurrentCoord = await ReadPLCFloatValueAsync(CurrentCoordAddress);
                }

                float target = forward ? _latestCurrentCoord.Value + gap : _latestCurrentCoord.Value - gap;
                await MoveToAsync(target);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"微调失败: {ex.Message}";
                MessageBox.Show($"微调失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Yield();
            Close();
        }
    }
}
