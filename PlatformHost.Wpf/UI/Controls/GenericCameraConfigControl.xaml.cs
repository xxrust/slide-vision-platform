using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.Hardware;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    public partial class GenericCameraConfigControl : UserControl
    {
        private CameraRole _role = CameraRole.Flying;
        private GenericCameraProfile _profile;
        private IReadOnlyList<OptCameraDeviceInfo> _optDevices = Array.Empty<OptCameraDeviceInfo>();

        public static readonly DependencyProperty CameraIdProperty =
            DependencyProperty.Register(nameof(CameraId), typeof(string), typeof(GenericCameraConfigControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DisplayNameProperty =
            DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(GenericCameraConfigControl),
                new PropertyMetadata(string.Empty));

        public string CameraId
        {
            get => (string)GetValue(CameraIdProperty);
            set => SetValue(CameraIdProperty, value);
        }

        public string DisplayName
        {
            get => (string)GetValue(DisplayNameProperty);
            set => SetValue(DisplayNameProperty, value);
        }

        public GenericCameraConfigControl()
        {
            InitializeComponent();
            Loaded += GenericCameraConfigControl_Loaded;
        }

        private void GenericCameraConfigControl_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureLists();
            if (_profile == null)
            {
                var cameraId = !string.IsNullOrWhiteSpace(CameraId) ? CameraId : _role.ToString();
                LoadProfile(cameraId, DisplayName);
            }

            RefreshDeviceList(false);
        }

        public void LoadProfile(CameraRole role)
        {
            _role = role;
            LoadProfile(role.ToString(), role.ToString());
        }

        public void LoadProfile(string cameraId, string displayName = null)
        {
            EnsureLists();
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                cameraId = CameraRole.Flying.ToString();
            }
            _role = cameraId == CameraRole.Fixed.ToString() ? CameraRole.Fixed : CameraRole.Flying;

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                DisplayName = displayName;
            }

            CameraId = cameraId;
            _profile = GenericCameraManager.GetProfile(cameraId, displayName);

            VendorComboBox.SelectedItem = _profile.Vendor;
            ModelTextBox.Text = _profile.Model ?? string.Empty;
            SerialTextBox.Text = _profile.SerialNumber ?? string.Empty;
            ExposureTextBox.Text = _profile.Settings.ExposureTimeUs.ToString(CultureInfo.InvariantCulture);
            GainTextBox.Text = _profile.Settings.Gain.ToString(CultureInfo.InvariantCulture);
            PixelFormatComboBox.SelectedItem = EnsurePixelFormatItem(_profile.Settings.PixelFormat);
            FrameRateTextBox.Text = _profile.Settings.FrameRate.ToString(CultureInfo.InvariantCulture);
            TriggerSourceComboBox.SelectedItem = _profile.Settings.TriggerSource;
            TriggerEnableCheckBox.IsChecked = _profile.Settings.TriggerEnabled;
            TriggerDelayTextBox.Text = _profile.Settings.TriggerDelayUs.ToString(CultureInfo.InvariantCulture);

            StatusTextBlock.Text = "配置已加载";
            UpdateDeviceStatus(!string.IsNullOrWhiteSpace(_profile.SerialNumber)
                ? $"已绑定: {_profile.SerialNumber}"
                : "未绑定");

            TrySelectDeviceBySerial(_profile.SerialNumber);
        }

        public void SaveProfile()
        {
            EnsureProfile();

            _profile.Vendor = VendorComboBox.SelectedItem?.ToString() ?? _profile.Vendor;
            _profile.Model = ModelTextBox.Text?.Trim() ?? string.Empty;
            _profile.SerialNumber = SerialTextBox.Text?.Trim() ?? string.Empty;
            _profile.CameraId = CameraId;
            _profile.DisplayName = DisplayName;
            _profile.Settings.ExposureTimeUs = ParseDouble(ExposureTextBox.Text, _profile.Settings.ExposureTimeUs);
            _profile.Settings.Gain = ParseDouble(GainTextBox.Text, _profile.Settings.Gain);
            _profile.Settings.PixelFormat = PixelFormatComboBox.SelectedItem?.ToString() ?? _profile.Settings.PixelFormat;
            _profile.Settings.FrameRate = ParseDouble(FrameRateTextBox.Text, _profile.Settings.FrameRate);
            _profile.Settings.TriggerEnabled = TriggerEnableCheckBox.IsChecked == true;
            _profile.Settings.TriggerDelayUs = ParseDouble(TriggerDelayTextBox.Text, _profile.Settings.TriggerDelayUs);

            if (TriggerSourceComboBox.SelectedItem is CameraTriggerSource triggerSource)
            {
                _profile.Settings.TriggerSource = triggerSource;
            }

            GenericCameraManager.SaveProfile(_profile);
            StatusTextBlock.Text = "配置已保存";
            var cameraLabel = !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : CameraId;
            LogManager.Info($"[相机配置] {cameraLabel} 相机参数已保存");
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveProfile();
                ApplyOptSettings();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"保存失败: {ex.Message}";
            }
        }

        private void EnsureLists()
        {
            if (VendorComboBox.ItemsSource == null)
            {
                VendorComboBox.ItemsSource = GenericCameraManager.SupportedVendors;
            }

            if (PixelFormatComboBox.ItemsSource == null)
            {
                PixelFormatComboBox.ItemsSource = OptCameraService.DefaultPixelFormats.ToList();
            }

            if (TriggerSourceComboBox.ItemsSource == null)
            {
                TriggerSourceComboBox.ItemsSource = Enum.GetValues(typeof(CameraTriggerSource));
            }
        }

        private void EnsureProfile()
        {
            if (_profile == null)
            {
                var cameraId = !string.IsNullOrWhiteSpace(CameraId) ? CameraId : _role.ToString();
                _profile = GenericCameraManager.GetProfile(cameraId, DisplayName);
            }
        }

        private static double ParseDouble(string text, double fallback)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            if (double.TryParse(text, out value))
            {
                return value;
            }

            return fallback;
        }

        private void RefreshDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDeviceList(true);
        }

        private void BindDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem is OptCameraDeviceInfo device)
            {
                VendorComboBox.SelectedItem = "OPT";
                ModelTextBox.Text = device.Model ?? string.Empty;
                SerialTextBox.Text = device.SerialNumber ?? string.Empty;
                EnsurePixelFormatItem(_profile?.Settings?.PixelFormat);
                UpdateDeviceStatus($"已绑定: {device.SerialNumber}");
                TryLoadPixelFormats(device);
            }
            else
            {
                UpdateDeviceStatus("未选择设备");
            }
        }

        private void RefreshDeviceList(bool forceRefresh)
        {
            try
            {
                string message;
                _optDevices = OptCameraService.Instance.DiscoverDevices(forceRefresh, out message);
                DeviceComboBox.ItemsSource = _optDevices;

                if (_optDevices.Count > 0 && DeviceComboBox.SelectedItem == null)
                {
                    DeviceComboBox.SelectedIndex = 0;
                }

                UpdateDeviceStatus(message);
                TrySelectDeviceBySerial(SerialTextBox.Text);
            }
            catch (Exception ex)
            {
                UpdateDeviceStatus($"设备刷新失败: {ex.Message}");
            }
        }

        private void TrySelectDeviceBySerial(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber) || _optDevices == null || _optDevices.Count == 0)
            {
                return;
            }

            var matched = _optDevices.FirstOrDefault(device =>
                string.Equals(device.SerialNumber?.Trim(), serialNumber.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                DeviceComboBox.SelectedItem = matched;
            }
        }

        private void TryLoadPixelFormats(OptCameraDeviceInfo device)
        {
            if (device == null)
            {
                return;
            }

            if (OptCameraService.Instance.TryGetPixelFormats(device, out var formats, out var current, out var message))
            {
                PixelFormatComboBox.ItemsSource = formats;
                PixelFormatComboBox.SelectedItem = string.IsNullOrWhiteSpace(current) ? formats.FirstOrDefault() : current;
                UpdateDeviceStatus(message);
            }
            else
            {
                UpdateDeviceStatus(message);
            }
        }

        private void ApplyOptSettings()
        {
            EnsureProfile();
            if (_profile == null)
            {
                return;
            }

            if (!OptCameraService.IsOptVendor(_profile.Vendor))
            {
                StatusTextBlock.Text = "配置已保存（非OPT相机）";
                return;
            }

            if (string.IsNullOrWhiteSpace(_profile.SerialNumber) && DeviceComboBox.SelectedItem is OptCameraDeviceInfo device)
            {
                _profile.SerialNumber = device.SerialNumber;
            }

            if (string.IsNullOrWhiteSpace(_profile.SerialNumber))
            {
                StatusTextBlock.Text = "未绑定序列号，无法下发";
                return;
            }

            if (OptCameraService.Instance.TryApplyProfile(_profile, out var message))
            {
                StatusTextBlock.Text = "配置已下发";
                LogManager.Info($"[相机配置] {DisplayName ?? CameraId} OPT参数已下发");
            }
            else
            {
                StatusTextBlock.Text = $"下发失败: {message}";
                LogManager.Warning($"[相机配置] OPT参数下发失败: {message}");
            }
        }

        private string EnsurePixelFormatItem(string pixelFormat)
        {
            if (string.IsNullOrWhiteSpace(pixelFormat))
            {
                return PixelFormatComboBox.SelectedItem?.ToString() ?? string.Empty;
            }

            if (PixelFormatComboBox.ItemsSource is IEnumerable<string> formats)
            {
                if (!formats.Contains(pixelFormat))
                {
                    var list = formats.ToList();
                    list.Add(pixelFormat);
                    PixelFormatComboBox.ItemsSource = list;
                }
            }

            PixelFormatComboBox.SelectedItem = pixelFormat;
            return pixelFormat;
        }

        private void UpdateDeviceStatus(string message)
        {
            if (DeviceStatusText != null)
            {
                DeviceStatusText.Text = message ?? string.Empty;
            }
        }
    }
}
