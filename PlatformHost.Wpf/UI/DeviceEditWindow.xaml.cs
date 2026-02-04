using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.SMTGPIO;
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
            InitializeIoOptions();
            LoadFromConfig(_workingConfig);
        }

        private void HardwareComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHardwareVisibility();
            UpdateIoProfileFromSelection();
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
            SetComboBoxText(BrandComboBox, config.Brand);
            SetComboBoxText(HardwareComboBox, config.HardwareName);

            SelectComboBoxItem(ProtocolComboBox, config.ProtocolType.ToString());

            if (config.Io != null)
            {
                SelectIoDeviceType(config.Io.DeviceType);
                IoPortTextBox.Text = config.Io.Port.ToString();
                if (IoBusTypeText != null)
                {
                    IoBusTypeText.Text = IoDeviceCatalog.FormatBusType(config.Io.BusType);
                }
            }

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

            UpdateHardwareVisibility();
            UpdateProtocolVisibility();
            UpdateIoProfileFromSelection();
        }

        private void UpdateHardwareVisibility()
        {
            var hardware = HardwareComboBox?.Text?.Trim();
            var isIo = string.Equals(hardware, "IO", StringComparison.OrdinalIgnoreCase);

            if (IoGroup != null)
            {
                IoGroup.Visibility = isIo ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ProtocolComboBox != null)
            {
                ProtocolComboBox.Visibility = isIo ? Visibility.Collapsed : Visibility.Visible;
            }

            if (ProtocolLabel != null)
            {
                ProtocolLabel.Visibility = isIo ? Visibility.Collapsed : Visibility.Visible;
            }

            if (BrandComboBox != null)
            {
                BrandComboBox.IsEnabled = false;
                if (!isIo)
                {
                    BrandComboBox.Text = "Keyence";
                }
            }

            if (isIo)
            {
                if (SerialGroup != null)
                {
                    SerialGroup.Visibility = Visibility.Collapsed;
                }

                if (TcpGroup != null)
                {
                    TcpGroup.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                UpdateProtocolVisibility();
            }
        }

        private void UpdateProtocolVisibility()
        {
            var protocol = GetSelectedProtocol();
            if (SerialGroup == null || TcpGroup == null)
            {
                return;
            }
            var isIo = string.Equals(HardwareComboBox?.Text?.Trim(), "IO", StringComparison.OrdinalIgnoreCase);
            if (isIo)
            {
                SerialGroup.Visibility = Visibility.Collapsed;
                TcpGroup.Visibility = Visibility.Collapsed;
                return;
            }
            SerialGroup.Visibility = protocol == DeviceProtocolType.Serial ? Visibility.Visible : Visibility.Collapsed;
            TcpGroup.Visibility = protocol == DeviceProtocolType.TcpIp ? Visibility.Visible : Visibility.Collapsed;
        }

        private DeviceProtocolType GetSelectedProtocol()
        {
            if (ProtocolComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tag)
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
            updated.Brand = BrandComboBox.Text?.Trim();
            updated.HardwareName = HardwareComboBox.Text?.Trim();
            var isIo = string.Equals(updated.HardwareName, "IO", StringComparison.OrdinalIgnoreCase);
            updated.ProtocolType = isIo ? DeviceProtocolType.Io : protocol;

            if (string.IsNullOrWhiteSpace(updated.HardwareName))
            {
                error = "硬件名称不能为空";
                return false;
            }

            if (isIo)
            {
                if (IoDeviceTypeComboBox.SelectedItem == null)
                {
                    error = "请选择IO设备型号";
                    return false;
                }

                var selectedType = (System.Windows.Controls.ComboBoxItem)IoDeviceTypeComboBox.SelectedItem;
                if (!(selectedType.Tag is IoDeviceProfile profile))
                {
                    error = "IO设备型号无效";
                    return false;
                }

                if (!uint.TryParse(IoPortTextBox.Text, out var port) || port < 1 || port > 8)
                {
                    error = "端口号必须在 1-8 之间";
                    return false;
                }

                updated.Io = new DeviceIoOptions
                {
                    DeviceType = profile.DeviceType,
                    Port = port,
                    BusType = profile.BusType
                };
                updated.Brand = profile.Brand;
            }
            else if (protocol == DeviceProtocolType.Serial)
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

            if (string.IsNullOrWhiteSpace(updated.Brand))
            {
                error = "设备品牌不能为空";
                return false;
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
                if (item is System.Windows.Controls.ComboBoxItem comboItem)
                {
                    var tag = comboItem.Tag?.ToString();
                    if (!string.IsNullOrWhiteSpace(tag) && string.Equals(tag, tagValue, StringComparison.OrdinalIgnoreCase))
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

        private static void SetComboBoxText(ComboBox comboBox, string value)
        {
            if (comboBox == null)
            {
                return;
            }

            var text = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
                return;
            }

            foreach (var item in comboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem)
                {
                    var tagText = comboItem.Tag?.ToString() ?? comboItem.Content?.ToString();
                    if (string.Equals(tagText, text, StringComparison.OrdinalIgnoreCase))
                    {
                        comboBox.SelectedItem = comboItem;
                        comboBox.Text = comboItem.Content?.ToString() ?? text;
                        return;
                    }
                }
            }

            comboBox.Text = text;
        }

        private static bool TryParseEnum<TEnum>(ComboBox comboBox, out TEnum value) where TEnum : struct
        {
            value = default;
            if (comboBox == null)
            {
                return false;
            }

            if (comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem comboItem)
            {
                var tagValue = comboItem.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tagValue) && Enum.TryParse(tagValue, true, out value))
                {
                    return true;
                }

                var contentValue = comboItem.Content?.ToString();
                if (!string.IsNullOrWhiteSpace(contentValue) && Enum.TryParse(contentValue, true, out value))
                {
                    return true;
                }
            }

            var text = comboBox.Text;
            if (!string.IsNullOrWhiteSpace(text) && Enum.TryParse(text, true, out value))
            {
                return true;
            }

            return false;
        }

        private void InitializeIoOptions()
        {
            if (IoDeviceTypeComboBox == null)
            {
                return;
            }

            IoDeviceTypeComboBox.Items.Clear();
            foreach (var profile in IoDeviceCatalog.GetProfiles())
            {
                IoDeviceTypeComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem
                {
                    Content = profile.DisplayName,
                    Tag = profile
                });
            }

            IoDeviceTypeComboBox.SelectionChanged += IoDeviceTypeComboBox_SelectionChanged;
            if (IoDeviceTypeComboBox.Items.Count > 0)
            {
                IoDeviceTypeComboBox.SelectedIndex = 0;
            }
        }

        private void IoDeviceTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateIoProfileFromSelection();
        }

        private void UpdateIoProfileFromSelection()
        {
            var isIo = string.Equals(HardwareComboBox?.Text?.Trim(), "IO", StringComparison.OrdinalIgnoreCase);
            if (!isIo)
            {
                return;
            }

            var profile = GetSelectedIoProfile();
            if (profile == null)
            {
                return;
            }

            if (BrandComboBox != null)
            {
                BrandComboBox.Text = profile.Brand;
            }

            if (IoBusTypeText != null)
            {
                IoBusTypeText.Text = IoDeviceCatalog.FormatBusType(profile.BusType);
            }
        }

        private IoDeviceProfile GetSelectedIoProfile()
        {
            if (IoDeviceTypeComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem comboItem
                && comboItem.Tag is IoDeviceProfile profile)
            {
                return profile;
            }

            return null;
        }

        private void SelectIoDeviceType(SMTGPIODeviceType deviceType)
        {
            if (IoDeviceTypeComboBox == null)
            {
                return;
            }

            foreach (var item in IoDeviceTypeComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && comboItem.Tag is IoDeviceProfile profile)
                {
                    if (profile.DeviceType == deviceType)
                    {
                        IoDeviceTypeComboBox.SelectedItem = comboItem;
                        UpdateIoProfileFromSelection();
                        return;
                    }
                }
            }

            if (IoDeviceTypeComboBox.Items.Count > 0)
            {
                IoDeviceTypeComboBox.SelectedIndex = 0;
            }
        }
    }
}
