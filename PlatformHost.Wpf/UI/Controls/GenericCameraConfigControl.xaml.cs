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
                LoadProfile(_role);
            }
        }

        public void LoadProfile(CameraRole role)
        {
            _role = role;
            EnsureLists();
            _profile = GenericCameraManager.GetProfile(role);

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
            LogManager.Info($"[相机配置] {_role} 相机参数已保存");
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
                _profile = GenericCameraManager.GetProfile(_role);
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
