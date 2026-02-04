using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfApp2.SMTGPIO;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class IoTestWindow : Window
    {
        private readonly DeviceConfig _deviceConfig;
        private IDeviceClient _client;
        private DispatcherTimer _statusTimer;

        public IoTestWindow(DeviceConfig deviceConfig)
        {
            InitializeComponent();
            _deviceConfig = deviceConfig?.Clone();
            Loaded += IoTestWindow_Loaded;
            Closed += IoTestWindow_Closed;
        }

        private void IoTestWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_deviceConfig == null)
            {
                MessageBox.Show("未提供设备配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            DeviceNameText.Text = $"IO 设备测试 - {_deviceConfig.Name}";
            if (_deviceConfig.Io != null)
            {
                var profile = IoDeviceCatalog.GetProfile(_deviceConfig.Io.DeviceType);
                var modelText = profile?.DisplayName ?? _deviceConfig.Io.DeviceType.ToString();
                DeviceMetaText.Text = $"{_deviceConfig.Brand} / {_deviceConfig.HardwareName} / {modelText} / Port {_deviceConfig.Io.Port}";
            }

            _client = DeviceManager.Instance.GetClientById(_deviceConfig.Id, out var error);
            if (_client == null && !string.IsNullOrWhiteSpace(error))
            {
                MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            IOManager.SetActiveDevice(_deviceConfig.Id);

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _statusTimer.Tick += (s, args) => RefreshStatus();
            _statusTimer.Start();
            RefreshStatus();
        }

        private void IoTestWindow_Closed(object sender, EventArgs e)
        {
            _statusTimer?.Stop();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null)
            {
                MessageBox.Show("设备客户端不可用", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = await _client.ConnectAsync();
            if (!result.Success)
            {
                MessageBox.Show(result.Error, "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RefreshStatus();
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null)
            {
                return;
            }

            await _client.DisconnectAsync();
            RefreshStatus();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
        }

        private void ResetAllButton_Click(object sender, RoutedEventArgs e)
        {
            IOManager.ResetAllOutputs();
            RefreshStatus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void O1Set_Click(object sender, RoutedEventArgs e)
        {
            IOManager.SetSingleOutput(1, true);
            RefreshStatus();
        }

        private void O1Reset_Click(object sender, RoutedEventArgs e)
        {
            IOManager.SetSingleOutput(1, false);
            RefreshStatus();
        }

        private void O2Set_Click(object sender, RoutedEventArgs e)
        {
            IOManager.SetSingleOutput(2, true);
            RefreshStatus();
        }

        private void O2Reset_Click(object sender, RoutedEventArgs e)
        {
            IOManager.SetSingleOutput(2, false);
            RefreshStatus();
        }

        private void O3Set_Click(object sender, RoutedEventArgs e)
        {
            IOManager.SetSingleOutput(3, true);
            RefreshStatus();
        }

        private void O3Reset_Click(object sender, RoutedEventArgs e)
        {
            IOManager.SetSingleOutput(3, false);
            RefreshStatus();
        }

        private void O4Set_Click(object sender, RoutedEventArgs e)
        {
            IOManager.SetSingleOutput(4, true);
            RefreshStatus();
        }

        private void O4Reset_Click(object sender, RoutedEventArgs e)
        {
            IOManager.SetSingleOutput(4, false);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            var status = _client?.Status ?? DeviceConnectionStatus.Disconnected;
            switch (status)
            {
                case DeviceConnectionStatus.Connected:
                    StatusIndicator.Fill = Brushes.LimeGreen;
                    StatusText.Text = "已连接";
                    break;
                case DeviceConnectionStatus.Connecting:
                    StatusIndicator.Fill = Brushes.Goldenrod;
                    StatusText.Text = "连接中";
                    break;
                case DeviceConnectionStatus.Error:
                    StatusIndicator.Fill = Brushes.Red;
                    StatusText.Text = "失败";
                    break;
                default:
                    StatusIndicator.Fill = Brushes.Gray;
                    StatusText.Text = "未连接";
                    break;
            }

            var states = IOManager.GetAllOutputStates();
            UpdateOutputStatus(O1Status, O1StatusText, states, 0);
            UpdateOutputStatus(O2Status, O2StatusText, states, 1);
            UpdateOutputStatus(O3Status, O3StatusText, states, 2);
            UpdateOutputStatus(O4Status, O4StatusText, states, 3);
        }

        private static void UpdateOutputStatus(Shape indicator, System.Windows.Controls.TextBlock label, bool[] states, int index)
        {
            var isHigh = states != null && index >= 0 && index < states.Length && states[index];
            if (indicator != null)
            {
                indicator.Fill = isHigh ? Brushes.LimeGreen : Brushes.Red;
            }

            if (label != null)
            {
                label.Text = isHigh ? "高电平" : "低电平";
            }
        }
    }
}
