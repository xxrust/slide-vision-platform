using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    public partial class SmartAnalysisWidget : UserControl
    {
        private AlertSettings _currentAlertSettings;
        private bool _isFloatingMode = false;
        private Window _hostWindow = null;
        private EventHandler _hostWindowStateChangedHandler = null;
        private bool? _originalTopmost = null;
        private bool? _originalShowInTaskbar = null;
        private ResizeMode? _originalResizeMode = null;
        private WindowStyle? _originalWindowStyle = null;
        private double? _originalWindowWidth = null;
        private double? _originalWindowHeight = null;
        private double? _originalMinWidth = null;
        private double? _originalMinHeight = null;
        private SizeToContent? _originalSizeToContent = null;
        
        public SmartAnalysisWidget()
        {
            InitializeComponent();
            
            // 预加载配置，确保在显示 MainPage 之前设置都已就绪
            _currentAlertSettings = AlertSettings.Load();
            
            // 强制初始化关注项目，避免首次打开时延迟
            var focusedProjects = FocusedProjectsManager.GetFocusedProjects();
            LogManager.Info($"[SmartAnalysisWidget] 预加载关注项目设置，项目数: {focusedProjects.Count}");

            Loaded += SmartAnalysisWidget_Loaded;
            Unloaded += SmartAnalysisWidget_Unloaded;
            
            SubscribeToPageEvents();
            ShowPage("Main");
            
            // 将加载的告警设置传递给 MainPage
            MainPage?.UpdateAlertSettings(_currentAlertSettings);
            
            // 确保 MainPage 使用最新的关注项目设置
            MainPage?.LoadDetectionItems();

            ApplyFloatingModeState();
        }

        private void SmartAnalysisWidget_Loaded(object sender, RoutedEventArgs e)
        {
            AttachHostWindow();
            ApplyFloatingModeState();
        }

        private void SmartAnalysisWidget_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachHostWindow();
        }

        private void SubscribeToPageEvents()
        {
            AlertPage.BackRequested += OnAlertPageBackRequested;
            FocusPage.SettingsSaved += OnFocusSettingsSaved;
            FocusPage.SettingsCancelled += OnFocusSettingsCancelled;
            SettingsPage.SettingsSaved += OnSettingsSaved;
            SettingsPage.SettingsCancelled += OnSettingsCancelled;
            HelpPage.BackRequested += OnHelpBackRequested;
        }

        private void AttachHostWindow()
        {
            var window = Window.GetWindow(this);
            if (window == null)
            {
                return;
            }

            if (_hostWindow == window && _hostWindowStateChangedHandler != null)
            {
                return;
            }

            DetachHostWindow();
            _hostWindow = window;

            // 记录原始窗口状态，便于退出悬浮模式时恢复
            _originalTopmost = _hostWindow.Topmost;
            _originalShowInTaskbar = _hostWindow.ShowInTaskbar;
            _originalResizeMode = _hostWindow.ResizeMode;
            _originalWindowStyle = _hostWindow.WindowStyle;
            _originalWindowWidth = _hostWindow.Width;
            _originalWindowHeight = _hostWindow.Height;
            _originalMinWidth = _hostWindow.MinWidth;
            _originalMinHeight = _hostWindow.MinHeight;
            _originalSizeToContent = _hostWindow.SizeToContent;

            _hostWindowStateChangedHandler = (s, e) =>
            {
                if (_isFloatingMode && _hostWindow != null && _hostWindow.WindowState == WindowState.Minimized)
                {
                    _hostWindow.WindowState = WindowState.Normal;
                }
            };
            _hostWindow.StateChanged += _hostWindowStateChangedHandler;
        }

        private void DetachHostWindow()
        {
            if (_hostWindow != null && _hostWindowStateChangedHandler != null)
            {
                _hostWindow.StateChanged -= _hostWindowStateChangedHandler;
            }

            _hostWindowStateChangedHandler = null;
            _hostWindow = null;
            _originalTopmost = null;
            _originalShowInTaskbar = null;
            _originalResizeMode = null;
            _originalWindowStyle = null;
            _originalWindowWidth = null;
            _originalWindowHeight = null;
            _originalMinWidth = null;
            _originalMinHeight = null;
            _originalSizeToContent = null;
        }

        private void OnSettingsSaved(object sender, EventArgs e)
        {
            var savedSettings = SettingsPage.GetCurrentAlertSettings();
            _currentAlertSettings = savedSettings;
            MainPage?.UpdateAlertSettings(_currentAlertSettings);
            ShowPage("Main");
        }

        private void OnSettingsCancelled(object sender, EventArgs e)
        {
            ShowPage("Main");
        }

        private void OnHelpBackRequested(object sender, EventArgs e)
        {
            ShowPage("Main");
        }

        private void OnAlertPageBackRequested(object sender, EventArgs e)
        {
            ShowPage("Main");
        }

        private void OnFocusSettingsSaved(object sender, EventArgs e)
        {
            FocusedProjectsManager.Reload();
            MainPage?.LoadDetectionItems();
            ShowPage("Main");
        }

        private void OnFocusSettingsCancelled(object sender, EventArgs e)
        {
            ShowPage("Main");
        }

        private void ShowPage(string pageType)
        {
            MainPage.Visibility = Visibility.Collapsed;
            AlertPage.Visibility = Visibility.Collapsed;
            FocusPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Collapsed;
            HelpPage.Visibility = Visibility.Collapsed;

            if (_isFloatingMode && pageType != "Main")
            {
                pageType = "Main";
            }

            switch (pageType)
            {
                case "Main":
                    MainPage.Visibility = Visibility.Visible;
                    break;
                case "Alert":
                    AlertPage.Visibility = Visibility.Visible;
                    AlertPage.LoadAlertRecords();
                    break;
                case "Focus":
                    FocusPage.Visibility = Visibility.Visible;
                    FocusPage.RefreshData();
                    break;
                case "Settings":
                    SettingsPage.Visibility = Visibility.Visible;
                    break;
                case "Help":
                    HelpPage.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            MainPage?.ExportData();
        }

        private void FocusButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("Focus");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("Settings");
        }

        private void AlertRecordButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("Alert");
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("Help");
        }

        private void FloatingModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isFloatingMode = !_isFloatingMode;
            ApplyFloatingModeState();
        }

        private void ApplyFloatingModeState()
        {
            if (!IsLoaded)
            {
                return;
            }

            AttachHostWindow();

            if (FloatingModeButton != null)
            {
                FloatingModeButton.Content = _isFloatingMode ? "退出悬浮" : "悬浮模式";
                if (_isFloatingMode)
                {
                    FloatingModeButton.Background = new SolidColorBrush(Color.FromRgb(32, 201, 151));
                }
                else
                {
                    FloatingModeButton.ClearValue(Button.BackgroundProperty);
                }
            }

            AlertRecordButton.IsEnabled = !_isFloatingMode;
            FocusButton.IsEnabled = !_isFloatingMode;
            SettingsButton.IsEnabled = !_isFloatingMode;
            HelpButton.IsEnabled = !_isFloatingMode;

            if (_isFloatingMode)
            {
                ShowPage("Main");
            }

            SmartAnalysisWindowManager.UpdateFloatingMode(_isFloatingMode);
            ConfigureHostWindowFloatingState();
            MainPage?.SetFloatingMode(_isFloatingMode);
        }

        private void ConfigureHostWindowFloatingState()
        {
            if (_hostWindow == null)
            {
                return;
            }

            if (_isFloatingMode)
            {
                _hostWindow.Topmost = true;
                _hostWindow.ShowInTaskbar = false;
                _hostWindow.ResizeMode = ResizeMode.NoResize;
                _hostWindow.WindowStyle = WindowStyle.ToolWindow;
                _hostWindow.SizeToContent = SizeToContent.Manual;

                double floatingHeight = 480;
                double floatingWidth = Math.Max(520, _hostWindow.Width);
                _hostWindow.Height = floatingHeight;
                _hostWindow.Width = floatingWidth;
                _hostWindow.MinHeight = 320;
                _hostWindow.MinWidth = 480;

                if (_hostWindow.WindowState == WindowState.Minimized)
                {
                    _hostWindow.WindowState = WindowState.Normal;
                }
            }
            else
            {
                _hostWindow.Topmost = _originalTopmost ?? false;
                _hostWindow.ShowInTaskbar = _originalShowInTaskbar ?? true;
                _hostWindow.ResizeMode = _originalResizeMode ?? ResizeMode.CanResize;
                _hostWindow.WindowStyle = _originalWindowStyle ?? WindowStyle.SingleBorderWindow;
                _hostWindow.SizeToContent = _originalSizeToContent ?? SizeToContent.Manual;
                if (_originalWindowHeight.HasValue) _hostWindow.Height = _originalWindowHeight.Value;
                if (_originalWindowWidth.HasValue) _hostWindow.Width = _originalWindowWidth.Value;
                if (_originalMinHeight.HasValue) _hostWindow.MinHeight = _originalMinHeight.Value;
                if (_originalMinWidth.HasValue) _hostWindow.MinWidth = _originalMinWidth.Value;
            }
        }

        public void ShowMainPage()
        {
            ShowPage("Main");
        }
        
        public AlertSettings GetCurrentAlertSettings()
        {
            return _currentAlertSettings;
        }

        public void ShowAlert(string alertItem, string triggerReason)
        {
            try
            {
                ShowPage("Main");
                MainPage?.ShowAlertInfo(alertItem, triggerReason);
                MainPage?.SelectAndShowItem(alertItem);
                LogManager.Info($"智能分析组件显示告警: {alertItem} - {triggerReason}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示告警信息失败: {ex.Message}");
            }
        }

        public void ClearAlert()
        {
            try
            {
                MainPage?.ClearAlertInfo();
            }
            catch (Exception ex)
            {
                LogManager.Error($"清除告警状态失败: {ex.Message}");
            }
        }
    }
}
