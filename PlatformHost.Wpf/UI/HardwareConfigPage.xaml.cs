using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VM.Core;
using GlobalCameraModuleCs;
using WpfApp2.SMTGPIO;
using static WpfApp2.UI.Page1;
using WpfApp2.UI.Models;
using System.Threading.Tasks;

namespace WpfApp2.UI
{
    /// <summary>
    /// 硬件配置页面 - 包含相机配置和IO控制功能
    /// </summary>
    public partial class HardwareConfigPage : Page
    {
        // 相机模块
        private GlobalCameraModuleTool _flyingCameraModule;
        private GlobalCameraModuleTool _fixedCameraModule;
        
        // IO状态更新定时器
        private DispatcherTimer _ioStatusTimer;
        
        // PLC状态监控定时器
        private DispatcherTimer _plcStatusTimer;

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
            
            // 延迟一小段时间，确保VM解决方案已完全加载
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializeCameraModules();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 初始化相机模块
        /// </summary>
        private void InitializeCameraModules()
        {
            try
            {
                // 检查VM解决方案是否已加载
                if (!IsVmSolutionReady())
                {
                    LogMessage("VM解决方案尚未完全加载，跳过相机模块初始化", LogLevel.Warning);
                    return;
                }

                // 初始化飞拍相机模块
                InitializeFlyingCamera();
                
                // 初始化定拍相机模块
                InitializeFixedCamera();
                
                LogMessage("相机模块初始化完成");
            }
            catch (Exception ex)
            {
                LogMessage($"初始化相机模块失败: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// 检查VM解决方案是否准备就绪
        /// </summary>
        private bool IsVmSolutionReady()
        {
            try
            {
                // 检查VmSolution实例是否存在
                if (VmSolution.Instance == null)
                {
                    return false;
                }

                // 检查是否有模板配置页面实例（表示VM已加载）
                if (TemplateConfigPage.Instance == null)
                {
                    return false;
                }

                // 尝试访问一个基本的VM模块来验证VM是否真正可用
                var testModule = VmSolution.Instance["获取路径图像"];
                return testModule != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 列出VM中可用的模块，帮助用户调试
        /// </summary>
        private void ListAvailableModules(string targetModuleName)
        {
            try
            {
                if (VmSolution.Instance == null) return;

                LogMessage($"正在查找与'{targetModuleName}'相关的模块...");
                
                // 尝试列出一些常见的模块名称模式
                var commonPatterns = new[]
                {
                    targetModuleName,
                    targetModuleName.Replace("相机", ""),
                    "相机",
                    "Camera",
                    "GlobalCamera",
                    "飞拍",
                    "定拍"
                };

                bool foundAny = false;
                foreach (var pattern in commonPatterns)
                {
                    try
                    {
                        var module = VmSolution.Instance[pattern];
                        if (module != null)
                        {
                            LogMessage($"  找到模块: '{pattern}' (类型: {module.GetType().Name})");
                            foundAny = true;
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略单个模块查找失败
                    }
                }

                if (!foundAny)
                {
                    LogMessage("  未找到相关的相机模块，请检查VM解决方案配置");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"列出可用模块时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化飞拍相机
        /// </summary>
        private void InitializeFlyingCamera()
        {
            try
            {
                // 检查VmSolution是否已加载
                if (VmSolution.Instance == null)
                {
                    LogMessage("VM解决方案未加载，跳过飞拍相机初始化", LogLevel.Warning);
                    return;
                }

                LogMessage("正在查找飞拍相机模块...");

                // 参考TemplateConfigPage的方式，直接通过模块名获取
                _flyingCameraModule = (GlobalCameraModuleCs.GlobalCameraModuleTool)VmSolution.Instance["飞拍相机"];
                
                if (_flyingCameraModule != null)
                {
                    LogMessage("✓ 已找到飞拍相机模块");
                    
                    // 参考TemplateConfigPage的方式，设置ModuleSource
                    FlyingCameraControl.ModuleSource = _flyingCameraModule;
                    LogMessage("✓ 飞拍相机模块已绑定到界面控件");
                }
                else
                {
                    LogMessage("⚠ 未找到飞拍相机模块，请在VM解决方案中添加名为'飞拍相机'的GlobalCameraModuleTool模块", LogLevel.Warning);
                    
                    // 列出当前VM中所有可用的模块，帮助用户调试
                    ListAvailableModules("飞拍相机");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"初始化飞拍相机失败: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// 初始化定拍相机
        /// </summary>
        private void InitializeFixedCamera()
        {
            try
            {
                // 检查VmSolution是否已加载
                if (VmSolution.Instance == null)
                {
                    LogMessage("VM解决方案未加载，跳过定拍相机初始化", LogLevel.Warning);
                    return;
                }

                LogMessage("正在查找定拍相机模块...");

                // 参考TemplateConfigPage的方式，直接通过模块名获取
                _fixedCameraModule = (GlobalCameraModuleCs.GlobalCameraModuleTool)VmSolution.Instance["定拍相机"];
                
                if (_fixedCameraModule != null)
                {
                    LogMessage("✓ 已找到定拍相机模块");
                    
                    // 参考TemplateConfigPage的方式，设置ModuleSource
                    FixedCameraControl.ModuleSource = _fixedCameraModule;
                    LogMessage("✓ 定拍相机模块已绑定到界面控件");
                }
                else
                {
                    LogMessage("⚠ 未找到定拍相机模块，请在VM解决方案中添加名为'定拍相机'的GlobalCameraModuleTool模块", LogLevel.Warning);
                    
                    // 列出当前VM中所有可用的模块，帮助用户调试
                    ListAvailableModules("定拍相机");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"初始化定拍相机失败: {ex.Message}", LogLevel.Warning);
            }
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

        /// <summary>
        /// 刷新飞拍相机模块按钮点击事件
        /// </summary>
        private void RefreshFlyingCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("手动刷新飞拍相机模块...");
                InitializeFlyingCamera();
            }
            catch (Exception ex)
            {
                LogMessage($"手动刷新飞拍相机模块失败: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// 刷新定拍相机模块按钮点击事件
        /// </summary>
        private void RefreshFixedCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("手动刷新定拍相机模块...");
                InitializeFixedCamera();
            }
            catch (Exception ex)
            {
                LogMessage($"手动刷新定拍相机模块失败: {ex.Message}", LogLevel.Warning);
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

                // 保存飞拍相机参数
                if (_flyingCameraModule != null)
                {
                    try
                    {
                        _flyingCameraModule.SaveParamToUser1();
                        LogMessage("✓ 飞拍相机参数已保存");
                        LogManager.Info("飞拍相机参数已保存");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"保存飞拍相机参数失败: {ex.Message}", LogLevel.Warning);
                    }
                }
                else
                {
                    LogMessage("飞拍相机模块未初始化，跳过参数保存", LogLevel.Warning);
                }

                // 保存定拍相机参数
                if (_fixedCameraModule != null)
                {
                    try
                    {
                        _fixedCameraModule.SaveParamToUser1();
                        LogMessage("✓ 定拍相机参数已保存");
                        LogManager.Info("定拍相机参数已保存");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"保存定拍相机参数失败: {ex.Message}", LogLevel.Warning);
                    }
                }
                else
                {
                    LogMessage("定拍相机模块未初始化，跳过参数保存", LogLevel.Warning);
                }

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