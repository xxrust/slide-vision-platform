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
                Name = device.Name,
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
            public string Name { get; set; }
            public string Protocol { get; set; }
            public string Address { get; set; }
            public string StatusText { get; set; }
            public Brush StatusBrush { get; set; }
            public string StatusTooltip { get; set; }
        }
    }
}
