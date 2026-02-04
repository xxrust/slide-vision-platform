using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp2.SMTGPIO;
using WpfApp2.Hardware;
using WpfApp2.UI.Controls;
using WpfApp2.UI.Models;
using System.Threading.Tasks;

namespace WpfApp2.UI
{
    /// <summary>
    /// 硬件配置页面 - 包含相机配置和IO控制功能
    /// </summary>
    public partial class HardwareConfigPage : Page
    {
        // IO状态更新定时器
        private DispatcherTimer _ioStatusTimer;
        
        // PLC状态监控定时器
        private DispatcherTimer _plcStatusTimer;

        private const int CamerasPerPage = 2;
        private int _cameraPageIndex = 0;
        private List<CameraDefinition> _cameraCatalog = new List<CameraDefinition>();

        public HardwareConfigPage()
        {
            InitializeComponent();
            LogMessage("硬件配置页面构造函数开始执行");
            InitializeIOStatusTimer();
            InitializePLCStatusTimer();
            LogMessage("硬件配置页面已初始化");
            
            // 延迟初始化相机模块，等待页面完全加载
            this.Loaded += HardwareConfigPage_Loaded;
        }

        /// <summary>
        /// 页面加载完成事件处理器
        /// </summary>
        private void HardwareConfigPage_Loaded(object sender, RoutedEventArgs e)
        {
            LogMessage("页面Loaded事件触发，重新启动状态监控");
            
            // 🔧 关键修复：每次页面加载时重新启动状态监控定时器
            // 因为页面是单例，构造函数只执行一次，需要在Loaded时重新启动定时器
            InitializeIOStatusTimer();
            
            // 启动PLC状态定时器
            InitializePLCStatusTimer();
            
            // 延迟加载通用相机配置
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializeCameraProfiles();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 初始化通用相机配置
        /// </summary>
        private void InitializeCameraProfiles()
        {
            try
            {
                _cameraCatalog = CameraCatalogManager.GetCameras().ToList();
                _cameraPageIndex = 0;
                UpdateCameraPage();
                LogMessage("通用相机配置已加载");
            }
            catch (Exception ex)
            {
                LogMessage($"初始化通用相机配置失败: {ex.Message}", LogLevel.Warning);
            }
        }

        private void UpdateCameraPage()
        {
            if (CameraItemsControl == null)
            {
                return;
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(_cameraCatalog.Count / (double)CamerasPerPage));
            _cameraPageIndex = Math.Max(0, Math.Min(_cameraPageIndex, totalPages - 1));

            var pageItems = _cameraCatalog
                .Skip(_cameraPageIndex * CamerasPerPage)
                .Take(CamerasPerPage)
                .ToList();

            CameraItemsControl.ItemsSource = pageItems;
            UpdateCameraPageButtons(totalPages);
        }

        private void UpdateCameraPageButtons(int totalPages)
        {
            var showPaging = totalPages > 1;
            if (CameraPageTextBlock != null)
            {
                CameraPageTextBlock.Text = $"第 {_cameraPageIndex + 1} / {totalPages} 页";
                CameraPageTextBlock.Visibility = showPaging ? Visibility.Visible : Visibility.Collapsed;
            }

            if (PrevCameraPageButton != null)
            {
                PrevCameraPageButton.IsEnabled = _cameraPageIndex > 0;
                PrevCameraPageButton.Opacity = PrevCameraPageButton.IsEnabled ? 1.0 : 0.5;
                PrevCameraPageButton.Visibility = showPaging ? Visibility.Visible : Visibility.Collapsed;
            }

            if (NextCameraPageButton != null)
            {
                NextCameraPageButton.IsEnabled = _cameraPageIndex < totalPages - 1;
                NextCameraPageButton.Opacity = NextCameraPageButton.IsEnabled ? 1.0 : 0.5;
                NextCameraPageButton.Visibility = showPaging ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SaveCurrentPageProfiles()
        {
            if (CameraItemsControl == null)
            {
                return;
            }

            foreach (var item in CameraItemsControl.Items)
            {
                var container = CameraItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container == null)
                {
                    continue;
                }

                var control = FindVisualChild<GenericCameraConfigControl>(container);
                control?.SaveProfile();
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 初始化IO状态更新定时器
        /// </summary>
        private void InitializeIOStatusTimer()
        {
            try
            {
                // 确保之前的定时器已经清理
                if (_ioStatusTimer != null)
                {
                    _ioStatusTimer.Stop();
                    _ioStatusTimer.Tick -= UpdateIOStatus;
                    _ioStatusTimer = null;
                }

                _ioStatusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500) // 每500ms更新一次IO状态
                };
                _ioStatusTimer.Tick += UpdateIOStatus;
                _ioStatusTimer.Start();
            }
            catch (Exception ex)
            {
                LogMessage($"初始化IO状态定时器失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 更新IO状态指示灯
        /// O0(原IO1): 检测状态输出 - 1→检测完成  0→检测中
        /// O1(原IO2): 检测结果输出 - 1→OK结果  0→NG结果  
        /// O2(原IO3): NG超限警告 - 单项NG超限告警
        /// O3(原IO4): 功能扩展输出 - 功能待定
        /// 
        /// 状态指示：绿色=高电平(1)，红色=低电平(0)，灰色=连接失败
        /// </summary>
        private void UpdateIOStatus(object sender, EventArgs e)
        {
            try
            {
                // 检查IOManager是否已初始化
                if (!IOManager.IsInitialized)
                {
                    // IOManager未初始化时，将所有指示灯设为灰色（连接失败）
                    SetAllStatusLights(Brushes.Gray);
                    return;
                }

                // 获取当前IO状态并更新指示灯
                var ioStates = IOManager.GetAllOutputStates();
                
                if (ioStates != null && ioStates.Length >= 4)
                {
                    // DispatcherTimer已经在UI线程中执行，不需要再次Invoke
                    // 绿色=高电平(置位)，红色=低电平(复位)
                    IO1StatusLight.Fill = ioStates[0] ? Brushes.Lime : Brushes.Red;
                    IO2StatusLight.Fill = ioStates[1] ? Brushes.Lime : Brushes.Red;
                    IO3StatusLight.Fill = ioStates[2] ? Brushes.Lime : Brushes.Red;
                    IO4StatusLight.Fill = ioStates[3] ? Brushes.Lime : Brushes.Red;
                }
                else
                {
                    // 获取状态失败时，设为灰色表示连接异常
                    SetAllStatusLights(Brushes.Gray);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"更新IO状态异常: {ex.Message}", LogLevel.Warning);
                
                // 异常时设为灰色表示连接失败
                SetAllStatusLights(Brushes.Gray);
            }
        }

        /// <summary>
        /// 设置所有IO状态指示灯为指定颜色
        /// </summary>
        private void SetAllStatusLights(Brush brush)
        {
            try
            {
                IO1StatusLight.Fill = brush;
                IO2StatusLight.Fill = brush;
                IO3StatusLight.Fill = brush;
                IO4StatusLight.Fill = brush;
            }
            catch
            {
                // 忽略UI更新异常
            }
        }

        #region PLC状态监控

        /// <summary>
        /// 初始化PLC状态监控定时器
        /// </summary>
        private void InitializePLCStatusTimer()
        {
            try
            {
                // 确保之前的定时器已经清理
                if (_plcStatusTimer != null)
                {
                    _plcStatusTimer.Stop();
                    _plcStatusTimer.Tick -= UpdatePLCStatus;
                    _plcStatusTimer = null;
                }

                _plcStatusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200) // 200ms间隔，减少串口负载
                };
                _plcStatusTimer.Tick += UpdatePLCStatus;
                _plcStatusTimer.Start();
                
            }
            catch (Exception ex)
            {
                LogMessage($"初始化PLC状态定时器失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 异步更新PLC状态指示灯
        /// R002: 准备检测
        /// R003: 到达下CCD  
        /// R004: 到达CCD4
        /// 
        /// 状态指示：绿色=ON(1)，红色=OFF(0)，灰色=通信异常
        /// </summary>
        private async void UpdatePLCStatus(object sender, EventArgs e)
        {
            try
            {
                // 检查PLC串口控制器是否可用
                if (!IsPLCControllerAvailable())
                {
                    // PLC控制器不可用时，将所有指示灯设为灰色（通信异常）
                    SetAllPLCStatusLights(Brushes.Gray);
                    return;
                }

                // 异步读取PLC继电器状态
                Dictionary<string, bool> relayStates = await ReadPLCRelayStatesAsync();
                
                if (relayStates != null && relayStates.Count >= 3)
                {
                    // 更新各个继电器状态指示灯
                    // 绿色=ON(1)，红色=OFF(0)
                    R002StatusLight.Fill = relayStates.ContainsKey("R002") && relayStates["R002"] ? Brushes.Lime : Brushes.Red;
                    R003StatusLight.Fill = relayStates.ContainsKey("R003") && relayStates["R003"] ? Brushes.Lime : Brushes.Red;
                    R004StatusLight.Fill = relayStates.ContainsKey("R004") && relayStates["R004"] ? Brushes.Lime : Brushes.Red;
                }
                else
                {
                    // 读取状态失败时，设为灰色表示通信异常
                    SetAllPLCStatusLights(Brushes.Gray);
                    // 减少错误日志频率，只在断路器开启或严重错误时记录
                }
            }
            catch (Exception ex)
            {
                // 只在严重异常时记录，减少日志噪音
                if (ex.Message.Contains("断路器") || ex.Message.Contains("串口未打开"))
                {
                    LogMessage($"PLC状态更新异常: {ex.Message}", LogLevel.Warning);
                }
                
                // 异常时设为灰色表示通信失败
                SetAllPLCStatusLights(Brushes.Gray);
            }
        }

        /// <summary>
        /// 检查PLC串口控制器是否可用
        /// </summary>
        private bool IsPLCControllerAvailable()
        {
            try
            {
                // 检查PLCSerialController单例是否连接
                return PLCSerialController.Instance?.IsConnected == true;
            }
            catch (Exception ex)
            {
                // 减少频繁的错误日志，只在断路器开启时记录
                if (ex.Message.Contains("断路器"))
                {
                    LogMessage($"PLC控制器状态检查失败: {ex.Message}", LogLevel.Warning);
                }
                return false;
            }
        }

        /// <summary>
        /// 异步读取PLC继电器状态
        /// 使用ReadSingleAsync方法读取R002、R003、R004继电器状态
        /// </summary>
        private async Task<Dictionary<string, bool>> ReadPLCRelayStatesAsync()
        {
            try
            {
                var relayStates = new Dictionary<string, bool>();
                var plcController = PLCSerialController.Instance;
                
                if (plcController == null || !plcController.IsConnected)
                {
                    // 减少频繁的警告日志
                    return null;
                }
                
                // 并行读取所有继电器状态，提高效率
                var readTasks = new[]
                {
                    Task.Run(async () => new { Address = "R002", Value = await plcController.ReadSingleAsync(addrCombine: "R002") }),
                    Task.Run(async () => new { Address = "R003", Value = await plcController.ReadSingleAsync(addrCombine: "R003") }),
                    Task.Run(async () => new { Address = "R004", Value = await plcController.ReadSingleAsync(addrCombine: "R004") })
                };

                // 等待所有读取任务完成
                var results = await Task.WhenAll(readTasks);
                
                // 处理结果
                foreach (var result in results)
                {
                    relayStates[result.Address] = result.Value > 0;
                }
                
                return relayStates;
            }
            catch (Exception ex)
            {
                // 只在关键错误时记录日志，减少日志噪音
                if (ex.Message.Contains("断路器") || ex.Message.Contains("串口未打开"))
                {
                    LogMessage($"读取PLC继电器状态异常: {ex.Message}", LogLevel.Warning);
                }
                return null;
            }
        }

        /// <summary>
        /// 设置所有PLC状态指示灯为指定颜色
        /// </summary>
        private void SetAllPLCStatusLights(Brush brush)
        {
            try
            {
                R002StatusLight.Fill = brush;
                R003StatusLight.Fill = brush;
                R004StatusLight.Fill = brush;
            }
            catch
            {
                // 忽略UI更新异常
            }
        }

        #endregion

        #region IO控制事件处理器

        /// <summary>
        /// O0(检测状态输出)置位按钮点击事件
        /// </summary>
        private void IO1SetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetSingleOutput(1, true);
                LogMessage("O0(检测状态输出)已置位 - 检测完成状态");
            }
            catch (Exception ex)
            {
                LogMessage($"O0置位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// O0(检测状态输出)复位按钮点击事件
        /// </summary>
        private void IO1ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetSingleOutput(1, false);
                LogMessage("O0(检测状态输出)已复位 - 检测中状态");
            }
            catch (Exception ex)
            {
                LogMessage($"O0复位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// O1(检测结果输出)置位按钮点击事件
        /// </summary>
        private void IO2SetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetSingleOutput(2, true);
                LogMessage("O1(检测结果输出)已置位 - OK结果");
            }
            catch (Exception ex)
            {
                LogMessage($"O1置位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// O1(检测结果输出)复位按钮点击事件
        /// </summary>
        private void IO2ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetSingleOutput(2, false);
                LogMessage("O1(检测结果输出)已复位 - NG结果");
            }
            catch (Exception ex)
            {
                LogMessage($"O1复位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// O2(NG超限警告)置位按钮点击事件
        /// </summary>
        private void IO3SetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetSingleOutput(3, true);
                LogMessage("O2(NG超限警告)已置位 - 单项NG超限告警");
            }
            catch (Exception ex)
            {
                LogMessage($"O2置位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// O2(NG超限警告)复位按钮点击事件
        /// </summary>
        private void IO3ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetSingleOutput(3, false);
                LogMessage("O2(NG超限警告)已复位 - 清除告警");
            }
            catch (Exception ex)
            {
                LogMessage($"O2复位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// O3(功能扩展输出)置位按钮点击事件
        /// </summary>
        private void IO4SetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetSingleOutput(4, true);
                LogMessage("O3(功能扩展输出)已置位");
            }
            catch (Exception ex)
            {
                LogMessage($"O3置位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// O3(功能扩展输出)复位按钮点击事件
        /// </summary>
        private void IO4ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetSingleOutput(4, false);
                LogMessage("O3(功能扩展输出)已复位");
            }
            catch (Exception ex)
            {
                LogMessage($"O3复位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 全部复位按钮点击事件
        /// </summary>
        private void AllResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.ResetAllOutputs();
                LogMessage("所有IO输出已复位 (O0,O1,O2,O3=0)");
            }
            catch (Exception ex)
            {
                LogMessage($"全部复位失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// OK输出按钮点击事件
        /// </summary>
        private void OKOutputButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IOManager.SetDetectionResult(true); // OK结果：O0=1(检测完成), O1=1(OK结果)
                LogMessage("已设置OK输出 (O0=1检测完成, O1=1OK结果)");
            }
            catch (Exception ex)
            {
                LogMessage($"设置OK输出失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// NG输出按钮点击事件
        /// </summary>
        private void NGOutputButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("触发NG输出 (O0=1检测完成, O1=0NG结果)");
                IOManager.SetDetectionResult(false);
            }
            catch (Exception ex)
            {
                LogMessage($"NG输出失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// GPIO设置按钮点击事件
        /// </summary>
        private void GPIOSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("打开GPIO设置窗口");
                
                var settingsWindow = new GPIOSettingsWindow();
                settingsWindow.Owner = Window.GetWindow(this);
                
                bool? result = settingsWindow.ShowDialog();
                
                if (result == true)
                {
                    LogMessage("GPIO设置已保存");
                    
                    // 询问用户是否重新初始化IO控制器
                    var confirmResult = MessageBox.Show(
                        "GPIO配置已保存。是否现在重新初始化IO控制器以应用新配置？\n\n注意：这将暂时中断当前的IO控制。", 
                        "应用新配置", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);
                    
                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        ReinitializeIOController();
                    }
                    else
                    {
                        LogMessage("新配置将在下次初始化IO控制器时生效");
                    }
                }
                else
                {
                    LogMessage("GPIO设置已取消");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"打开GPIO设置失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开GPIO设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 重新初始化IO控制器
        /// </summary>
        private void ReinitializeIOController()
        {
            try
            {
                LogMessage("正在重新初始化IO控制器...");
                
                // 释放当前IO控制器
                IOManager.Dispose();
                
                // 重新初始化
                bool initResult = IOManager.Initialize();
                
                if (initResult)
                {
                    LogMessage("IO控制器重新初始化成功");
                    MessageBox.Show("IO控制器已成功应用新配置！", "初始化成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage("IO控制器重新初始化失败", LogLevel.Error);
                    MessageBox.Show("IO控制器重新初始化失败，请检查新配置是否正确。", "初始化失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"重新初始化IO控制器失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"重新初始化IO控制器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private void RefreshCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.DataContext is CameraDefinition camera)
                {
                    var container = CameraItemsControl?.ItemContainerGenerator.ContainerFromItem(camera) as ContentPresenter;
                    var control = container == null ? null : FindVisualChild<GenericCameraConfigControl>(container);
                    control?.LoadProfile(camera.Id, camera.Name);
                    LogMessage($"手动刷新相机配置: {camera.Name}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"手动刷新相机配置失败: {ex.Message}", LogLevel.Warning);
            }
        }

        private void PrevCameraPageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveCurrentPageProfiles();
                _cameraPageIndex = Math.Max(0, _cameraPageIndex - 1);
                UpdateCameraPage();
            }
            catch (Exception ex)
            {
                LogMessage($"切换上一页失败: {ex.Message}", LogLevel.Warning);
            }
        }

        private void NextCameraPageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveCurrentPageProfiles();
                _cameraPageIndex++;
                UpdateCameraPage();
            }
            catch (Exception ex)
            {
                LogMessage($"切换下一页失败: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// PLC串口配置按钮点击事件
        /// </summary>
        private void PLCConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("正在打开PLC串口配置窗口...");
                
                // 创建PLC配置窗口
                var plcConfigWindow = new Window
                {
                    Title = "PLC串口配置与测试",
                    Width = 1200,
                    Height = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow,
                    Content = new PLCSerialConfigPage()
                };
                
                // 显示模态对话框
                plcConfigWindow.ShowDialog();
                
                LogMessage("PLC串口配置窗口已关闭");
            }
            catch (Exception ex)
            {
                LogMessage($"打开PLC串口配置窗口时出错: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开PLC串口配置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 3D配置按钮点击事件
        /// </summary>
        private void ThreeDConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("正在打开3D配置窗口...");
                
                // 创建3D配置窗口 - 保留原有的实验窗口
                MessageBox.Show("3D配置工具已迁移为独立进程（Host/Tool）。\n当前版本主程序不再直接加载Keyence 3D窗口。", "3D提示", MessageBoxButton.OK, MessageBoxImage.Information); return;
                
                // 显示模态对话框
                LogMessage("3D配置已迁移为独立进程，主程序未打开3D窗口");
            }
            catch (Exception ex)
            {
                LogMessage($"打开3D配置窗口时出错: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"打开3D配置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 返回按钮点击事件
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存相机参数
                SaveCameraParameters();

                // 停止IO状态定时器
                if (_ioStatusTimer != null)
                {
                    _ioStatusTimer.Stop();
                    _ioStatusTimer.Tick -= UpdateIOStatus;
                    _ioStatusTimer = null;
                }

                // 停止PLC状态定时器
                if (_plcStatusTimer != null)
                {
                    _plcStatusTimer.Stop();
                    _plcStatusTimer.Tick -= UpdatePLCStatus;
                    _plcStatusTimer = null;
                }

                // 🔧 关键修复：从硬件配置页面返回时重置检测管理器状态
                WpfApp2.UI.Page1.PageManager.ResetDetectionManagerOnPageReturn("硬件配置页面");

                // 返回主界面
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow != null)
                {
                    mainWindow.ContentC.Content = mainWindow.frame1; // 返回Page1
                    LogMessage("已返回主界面");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"返回主界面失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 保存相机参数
        /// </summary>
        private void SaveCameraParameters()
        {
            try
            {
                LogMessage("正在保存相机参数...");

                SaveCurrentPageProfiles();

                LogMessage("相机参数保存操作完成");
            }
            catch (Exception ex)
            {
                LogMessage($"保存相机参数过程中发生错误: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 日志记录方法
        /// </summary>
        private void LogMessage(string message, LogLevel level = LogLevel.Info)
        {
            try
            {
                // 记录到Page1的日志
                LogManager.Info($"[硬件配置] {message}");
            }
            catch (Exception ex)
            {
                // 静默处理日志记录错误
                System.Diagnostics.Debug.WriteLine($"日志记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 日志级别枚举
        /// </summary>
        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 页面卸载时的清理工作
        /// </summary>
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存相机参数（以防用户没有通过返回按钮退出）
                SaveCameraParameters();

                // 停止IO状态定时器
                if (_ioStatusTimer != null)
                {
                    _ioStatusTimer.Stop();
                    _ioStatusTimer.Tick -= UpdateIOStatus;
                    _ioStatusTimer = null;
                }

                // 停止PLC状态定时器
                if (_plcStatusTimer != null)
                {
                    _plcStatusTimer.Stop();
                    _plcStatusTimer.Tick -= UpdatePLCStatus;
                    _plcStatusTimer = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"页面卸载清理失败: {ex.Message}");
            }
        }
    }
} 
