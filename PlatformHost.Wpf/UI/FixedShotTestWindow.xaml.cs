using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.SMTGPIO;
using WpfApp2.ThreeD;

namespace WpfApp2.UI
{
    /// <summary>
    /// FixedShotTestWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FixedShotTestWindow : Window
    {
        private readonly Page1 _page1;
        private readonly bool _originalSaveAllImages;
        private readonly string _originalLotValue;
        private string _currentLotValue;
        private int _captureCount;
        private bool _isInitializing;

        public FixedShotTestWindow(Page1 page1)
        {
            InitializeComponent();

            _page1 = page1 ?? throw new ArgumentNullException(nameof(page1));
            _originalSaveAllImages = _page1.GetSaveAllImagesEnabled();
            _originalLotValue = _page1.CurrentLotValue ?? string.Empty;
            _currentLotValue = _originalLotValue;

            Loaded += FixedShotTestWindow_Loaded;
            Closing += FixedShotTestWindow_Closing;
        }

        private async void FixedShotTestWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;
            try
            {
                LotTextBox.Text = _originalLotValue;
                CaptureCountTextBlock.Text = "0";
            }
            finally
            {
                _isInitializing = false;
            }

            try
            {
                _page1.SetSaveAllImagesEnabled(true);
            }
            catch
            {
            }

            try
            {
                if (ThreeDSettings.CurrentDetection3DParams?.Enable3DDetection == true)
                {
                    ThreeDSettings.CurrentDetection3DParams.Enable3DDetection = false;
                    _page1.DetectionManager?.StartDetectionCycle(false);
                }
            }
            catch
            {
            }
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            _captureCount++;
            CaptureCountTextBlock.Text = _captureCount.ToString();

            try
            {
                var plcController = PLCSerialController.Instance;
                if (!plcController.IsConnected)
                {
                    MessageBox.Show("PLC未连接，无法触发抓拍(MR10)。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool success = await plcController.SetRelayAsync("MR10");
                if (!success)
                {
                    MessageBox.Show($"触发抓拍失败：{plcController.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"触发抓拍异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LotTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            string newLotValue = LotTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newLotValue))
            {
                return;
            }

            if (string.Equals(newLotValue, _currentLotValue, StringComparison.Ordinal))
            {
                return;
            }

            _currentLotValue = newLotValue;
            _captureCount = 0;
            CaptureCountTextBlock.Text = "0";

            try
            {
                _page1.UpdateLotValue(newLotValue);
            }
            catch
            {
            }
        }

        private void FixedShotTestWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                _page1.SetSaveAllImagesEnabled(_originalSaveAllImages);
                if (!string.IsNullOrEmpty(_originalLotValue))
                {
                    _page1.UpdateLotValue(_originalLotValue);
                }
            }
            catch
            {
            }
        }
    }
}

