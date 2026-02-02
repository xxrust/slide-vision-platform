using System;
using System.Globalization;
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
            TriggerSourceComboBox.SelectedItem = _profile.Settings.TriggerSource;
            TriggerEnableCheckBox.IsChecked = _profile.Settings.TriggerEnabled;
            TriggerDelayTextBox.Text = _profile.Settings.TriggerDelayUs.ToString(CultureInfo.InvariantCulture);

            StatusTextBlock.Text = "配置已加载";
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
    }
}
