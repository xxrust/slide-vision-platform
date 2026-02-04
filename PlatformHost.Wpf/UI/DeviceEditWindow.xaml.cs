using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class DeviceEditWindow : Window
    {
        private readonly DeviceConfig _workingConfig;
        public DeviceConfig ResultConfig { get; private set; }

        public DeviceEditWindow(DeviceConfig config, bool isEdit)
        {
            InitializeComponent();
            _workingConfig = config?.Clone() ?? new DeviceConfig();
            HeaderText.Text = isEdit ? "编辑设备" : "新增设备";
            LoadFromConfig(_workingConfig);
        }

        private void ProtocolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateProtocolVisibility();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildConfig(out var config, out var error))
            {
                MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultConfig = config;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void LoadFromConfig(DeviceConfig config)
        {
            NameTextBox.Text = config.Name ?? string.Empty;

            SelectComboBoxItem(ProtocolComboBox, config.ProtocolType.ToString());

            var serial = config.Serial ?? new DeviceSerialOptions();
            SerialPortTextBox.Text = serial.PortName ?? string.Empty;
            BaudRateTextBox.Text = serial.BaudRate.ToString();
            DataBitsTextBox.Text = serial.DataBits.ToString();
            SelectComboBoxItem(StopBitsComboBox, serial.StopBits.ToString());
            SelectComboBoxItem(ParityComboBox, serial.Parity.ToString());
            ReadTimeoutTextBox.Text = serial.ReadTimeout.ToString();
            WriteTimeoutTextBox.Text = serial.WriteTimeout.ToString();

            var tcp = config.Tcp ?? new DeviceTcpOptions();
            TcpHostTextBox.Text = tcp.Host ?? string.Empty;
            TcpPortTextBox.Text = tcp.Port.ToString();

            UpdateProtocolVisibility();
        }

        private void UpdateProtocolVisibility()
        {
            var protocol = GetSelectedProtocol();
            SerialGroup.Visibility = protocol == DeviceProtocolType.Serial ? Visibility.Visible : Visibility.Collapsed;
            TcpGroup.Visibility = protocol == DeviceProtocolType.TcpIp ? Visibility.Visible : Visibility.Collapsed;
        }

        private DeviceProtocolType GetSelectedProtocol()
        {
            if (ProtocolComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (Enum.TryParse(tag, out DeviceProtocolType protocol))
                {
                    return protocol;
                }
            }

            return DeviceProtocolType.Serial;
        }

        private bool TryBuildConfig(out DeviceConfig config, out string error)
        {
            config = null;
            error = string.Empty;

            var name = NameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "设备名称不能为空";
                return false;
            }

            var protocol = GetSelectedProtocol();
            var updated = _workingConfig.Clone();
            updated.Name = name;
            updated.ProtocolType = protocol;

            if (protocol == DeviceProtocolType.Serial)
            {
                var portName = SerialPortTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(portName))
                {
                    error = "串口号不能为空";
                    return false;
                }

                if (!int.TryParse(BaudRateTextBox.Text, out var baudRate) || baudRate <= 0)
                {
                    error = "波特率无效";
                    return false;
                }

                if (!int.TryParse(DataBitsTextBox.Text, out var dataBits) || dataBits < 5 || dataBits > 8)
                {
                    error = "数据位必须在 5-8 之间";
                    return false;
                }

                if (!TryParseEnum(StopBitsComboBox, out StopBits stopBits))
                {
                    error = "停止位无效";
                    return false;
                }

                if (!TryParseEnum(ParityComboBox, out Parity parity))
                {
                    error = "校验位无效";
                    return false;
                }

                if (!int.TryParse(ReadTimeoutTextBox.Text, out var readTimeout) || readTimeout <= 0)
                {
                    error = "读取超时无效";
                    return false;
                }

                if (!int.TryParse(WriteTimeoutTextBox.Text, out var writeTimeout) || writeTimeout <= 0)
                {
                    error = "写入超时无效";
                    return false;
                }

                updated.Serial = new DeviceSerialOptions
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    DataBits = dataBits,
                    StopBits = stopBits,
                    Parity = parity,
                    ReadTimeout = readTimeout,
                    WriteTimeout = writeTimeout
                };
            }
            else
            {
                var host = TcpHostTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(host))
                {
                    error = "目标IP不能为空";
                    return false;
                }

                if (!int.TryParse(TcpPortTextBox.Text, out var port) || port <= 0 || port > 65535)
                {
                    error = "端口范围无效";
                    return false;
                }

                updated.Tcp = new DeviceTcpOptions
                {
                    Host = host,
                    Port = port
                };
            }

            config = updated;
            return true;
        }

        private static void SelectComboBoxItem(ComboBox comboBox, string tagValue)
        {
            if (comboBox == null)
            {
                return;
            }

            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem comboItem && comboItem.Tag is string tag)
                {
                    if (string.Equals(tag, tagValue, StringComparison.OrdinalIgnoreCase))
                    {
                        comboBox.SelectedItem = comboItem;
                        return;
                    }
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private static bool TryParseEnum<TEnum>(ComboBox comboBox, out TEnum value) where TEnum : struct
        {
            value = default;
            if (comboBox.SelectedItem is ComboBoxItem comboItem && comboItem.Tag is string tag)
            {
                return Enum.TryParse(tag, out value);
            }

            return false;
        }
    }
}
