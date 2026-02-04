using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class PlcSerialTestWindow : Window
    {
        private readonly DeviceConfig _deviceConfig;
        private IDeviceClient _client;
        private DispatcherTimer _statusTimer;

        public PlcSerialTestWindow(DeviceConfig deviceConfig)
        {
            InitializeComponent();
            _deviceConfig = deviceConfig?.Clone();
            Loaded += PlcSerialTestWindow_Loaded;
            Closed += PlcSerialTestWindow_Closed;
        }

        private void PlcSerialTestWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_deviceConfig == null)
            {
                MessageBox.Show("未提供设备配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            DeviceNameText.Text = $"PLC串口测试 - {_deviceConfig.Name}";
            var serial = _deviceConfig.Serial;
            if (serial != null)
            {
                DeviceMetaText.Text = $"{_deviceConfig.Brand} / {_deviceConfig.HardwareName} / {serial.PortName} / {serial.BaudRate}";
            }

            _client = DeviceManager.Instance.GetClientById(_deviceConfig.Id, out var error);
            if (_client == null && !string.IsNullOrWhiteSpace(error))
            {
                MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var testPage = new PLCSerialConfigPage(true);
            testPage.SetDeviceConfig(_deviceConfig);
            TestFrame.Content = testPage;

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _statusTimer.Tick += (s, args) => UpdateStatus();
            _statusTimer.Start();
            UpdateStatus();
        }

        private void PlcSerialTestWindow_Closed(object sender, EventArgs e)
        {
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
            }
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

            UpdateStatus();
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null)
            {
                return;
            }

            await _client.DisconnectAsync();
            UpdateStatus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateStatus()
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
        }
    }
}
