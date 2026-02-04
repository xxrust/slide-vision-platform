using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class DeviceManagementWindow : Window
    {
        private readonly ObservableCollection<DeviceListItem> _items = new ObservableCollection<DeviceListItem>();

        public DeviceManagementWindow()
        {
            InitializeComponent();
            DeviceGrid.ItemsSource = _items;
            Loaded += DeviceManagementWindow_Loaded;
        }

        private void DeviceManagementWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDevices();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDevices();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DeviceEditWindow(new DeviceConfig(), false)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                if (!DeviceManager.Instance.AddDevice(dialog.ResultConfig, out var error))
                {
                    MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RefreshDevices();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(DeviceGrid.SelectedItem is DeviceListItem selected))
            {
                MessageBox.Show("请先选择设备", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new DeviceEditWindow(selected.Config, true)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                if (!DeviceManager.Instance.UpdateDevice(dialog.ResultConfig, out var error))
                {
                    MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RefreshDevices();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(DeviceGrid.SelectedItem is DeviceListItem selected))
            {
                MessageBox.Show("请先选择设备", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"确定删除设备 \"{selected.Name}\" 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            if (!DeviceManager.Instance.RemoveDevice(selected.Config.Id, out var error))
            {
                MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshDevices();
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(DeviceGrid.SelectedItem is DeviceListItem selected))
            {
                MessageBox.Show("请先选择设备", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selected.Config.ProtocolType != DeviceProtocolType.Serial)
            {
                MessageBox.Show("当前仅支持串口设备测试", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var testWindow = new PlcSerialTestWindow(selected.Config)
            {
                Owner = this
            };
            testWindow.ShowDialog();
        }

        private void RefreshDevices()
        {
            _items.Clear();

            var devices = DeviceManager.Instance.GetDevices();
            foreach (var device in devices)
            {
                var item = BuildItem(device);
                _items.Add(item);
            }

            EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static DeviceListItem BuildItem(DeviceConfig device)
        {
            var status = DeviceConnectionStatus.Disconnected;
            var tooltip = string.Empty;
            var protocol = device.ProtocolType.ToString();
            var address = string.Empty;

            if (device.ProtocolType == DeviceProtocolType.Serial)
            {
                var serial = device.Serial ?? new DeviceSerialOptions();
                address = $"{serial.PortName} / {serial.BaudRate}";
            }
            else
            {
                var tcp = device.Tcp ?? new DeviceTcpOptions();
                address = $"{tcp.Host}:{tcp.Port}";
            }

            var client = DeviceManager.Instance.GetClientById(device.Id, out var error);
            if (client == null)
            {
                status = DeviceConnectionStatus.Error;
                tooltip = error;
            }
            else
            {
                status = client.Status;
                tooltip = client.LastError;
            }

            return new DeviceListItem
            {
                Config = device.Clone(),
                Name = device.Name,
                Brand = device.Brand,
                HardwareName = device.HardwareName,
                Protocol = protocol,
                Address = address,
                StatusText = FormatStatus(status),
                StatusBrush = GetStatusBrush(status),
                StatusTooltip = string.IsNullOrWhiteSpace(tooltip) ? null : tooltip
            };
        }

        private static string FormatStatus(DeviceConnectionStatus status)
        {
            switch (status)
            {
                case DeviceConnectionStatus.Connecting:
                    return "连接中";
                case DeviceConnectionStatus.Connected:
                    return "已连接";
                case DeviceConnectionStatus.Error:
                    return "失败";
                default:
                    return "未连接";
            }
        }

        private static Brush GetStatusBrush(DeviceConnectionStatus status)
        {
            switch (status)
            {
                case DeviceConnectionStatus.Connecting:
                    return Brushes.Goldenrod;
                case DeviceConnectionStatus.Connected:
                    return Brushes.LimeGreen;
                case DeviceConnectionStatus.Error:
                    return Brushes.Red;
                default:
                    return Brushes.Gray;
            }
        }

        private sealed class DeviceListItem
        {
            public DeviceConfig Config { get; set; }
            public string Name { get; set; }
            public string Brand { get; set; }
            public string HardwareName { get; set; }
            public string Protocol { get; set; }
            public string Address { get; set; }
            public string StatusText { get; set; }
            public Brush StatusBrush { get; set; }
            public string StatusTooltip { get; set; }
        }
    }
}
