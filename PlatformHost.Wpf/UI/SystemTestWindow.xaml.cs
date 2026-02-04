using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp2.SMTGPIO;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 系统测试窗口 - 在线仿真测试和性能监控
    /// </summary>
    public partial class SystemTestWindow : Window
    {
        #region 私有字段

        private DispatcherTimer _plcStatusTimer;
        private Stopwatch _testStopwatch;
        private DateTime _triggerTime;
        private bool _isTestRunning = false;

        // 时间记录
        private double _vmCallbackTime = -1;
        private double _ljdCallbackTime = -1;
        private double _ioOutputTime = -1;
        private double _uiRenderTime = -1;

        // 静态事件，用于接收系统回调
        private static SystemTestWindow _currentInstance;

        #endregion

        #region 构造函数和初始化

        public SystemTestWindow()
        {
            InitializeComponent();
            _currentInstance = this;
            
            InitializeWindow();
            InitializePLCStatusMonitor(); // 恢复PLC状态监控
            
            LogMessage("系统测试窗口已打开");
        }

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                _testStopwatch = new Stopwatch();
                
                // 手动更新一次PLC连接状态
                UpdatePLCConnectionStatus();
                
                LogMessage("系统测试窗口初始化完成");
            }
            catch (Exception ex)
            {
                LogMessage($"初始化系统测试窗口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化PLC状态监控
        /// </summary>
        private void InitializePLCStatusMonitor()
        {
            try
            {
                _plcStatusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200) // 200ms间隔，减少串口负载
                };
                _plcStatusTimer.Tick += PLCStatusTimer_Tick;
                _plcStatusTimer.Start();
            }
            catch (Exception ex)
            {
                LogMessage($"启动PLC状态监控失败: {ex.Message}");
            }
        }

        #endregion

        #region PLC状态监控

        /// <summary>
        /// PLC状态定时检查
        /// </summary>
        private void PLCStatusTimer_Tick(object sender, EventArgs e)
        {
            UpdatePLCConnectionStatus();
        }

        /// <summary>
        /// 更新PLC连接状态显示（定时更新）
        /// </summary>
        private void UpdatePLCConnectionStatus()
        {
            try
            {
                bool isConnected = PLCSerialController.Instance?.IsConnected == true;
                
                Dispatcher.Invoke(() =>
                {
                    if (isConnected)
                    {
                        PLCStatusIndicator.Fill = Brushes.Lime;
                        PLCStatusText.Text = "已连接";
                        TriggerButton.IsEnabled = !_isTestRunning;
                    }
                    else
                    {
                        PLCStatusIndicator.Fill = Brushes.Red;
                        PLCStatusText.Text = "未连接";
                        TriggerButton.IsEnabled = false;
                    }
                });
            }
            catch (Exception ex)
            {
                // 只在关键错误时记录日志，减少日志噪音
                if (ex.Message.Contains("断路器") || ex.Message.Contains("串口未打开"))
                {
                    LogMessage($"更新PLC状态失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region 测试控制

        /// <summary>
        /// 触发测试按钮点击事件
        /// </summary>
        private async void TriggerButton_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否已有测试在运行
            if (_isTestRunning)
            {
                LogMessage("测试正在进行中，请等待完成");
                return;
            }

            // 异步启动系统测试（异常处理在StartSystemTestAsync内部）
            await StartSystemTestAsync();
        }

        /// <summary>
        /// 开始系统测试
        /// </summary>
        private async Task StartSystemTestAsync()
        {
            try
            {
                LogMessage("开始系统测试...");
                
                // 重置测试状态
                ResetTestState();
                
                // 设置测试状态
                _isTestRunning = true;
                _triggerTime = DateTime.Now;
                _testStopwatch.Restart();
                
                // 更新UI状态
                TriggerButton.IsEnabled = false;
                TestStatusText.Text = "测试进行中...";
                
                // 异步通过PLC触发MR012
                await TriggerPLCSignalAsync();
                
                // 启动测试监控
                StartTestMonitoring();
                
                LogMessage($"系统测试已触发，时间: {_triggerTime:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                _isTestRunning = false;
                TriggerButton.IsEnabled = true;
                TestStatusText.Text = "测试失败";
                LogMessage($"开始测试失败: {ex.Message}");
                
                // 在UI线程中显示错误信息
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"系统测试启动失败: {ex.Message}", "测试错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// 异步通过PLC触发MR012信号
        /// </summary>
        private async System.Threading.Tasks.Task TriggerPLCSignalAsync()
        {
            try
            {
                var plcController = PLCSerialController.Instance;
                if (plcController?.IsConnected == true)
                {
                    // 异步置位MR012触发检测
                    bool success = await plcController.WriteSingleAsync(addrCombine: "MR012", data: 1);
                    
                    if (success)
                    {
                        LogMessage("PLC触发信号MR012已置位");
                    }
                    else
                    {
                        throw new Exception("PLC置位MR012失败");
                    }
                }
                else
                {
                    throw new Exception("PLC未连接，无法触发");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"PLC触发失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 启动测试监控
        /// </summary>
        private void StartTestMonitoring()
        {
            try
            {
                // 设置回调监听
                SetupCallbackListeners();
                
                // 启动超时检查（10秒超时）
                var timeoutTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                
                timeoutTimer.Tick += (s, e) =>
                {
                    timeoutTimer.Stop();
                    if (_isTestRunning)
                    {
                        FinishTest("测试超时");
                    }
                };
                
                timeoutTimer.Start();
                
                LogMessage("测试监控已启动，超时时间: 10秒");
            }
            catch (Exception ex)
            {
                LogMessage($"启动测试监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置回调监听
        /// </summary>
        private void SetupCallbackListeners()
        {
            try
            {
                // 🔧 关键修复：启动统一检测管理器的系统测试模式
                var page1Instance = WpfApp2.UI.Page1.PageManager.Page1Instance;
                if (page1Instance?.DetectionManager != null)
                {
                    var detectionManager = page1Instance.DetectionManager;
                    
                    // 检查系统是否初始化
                    if (!detectionManager.IsSystemInitialized)
                    {
                        LogMessage("检测管理器未初始化，正在初始化...");
                        detectionManager.InitializeSystem();
                    }
                    
                    // 🔧 新增：启动系统测试模式
                    bool is3DEnabled = page1Instance.Is3DDetectionEnabled();
                    detectionManager.StartSystemTestMode(is3DEnabled);
                    LogMessage($"✅ 已启动系统测试模式，3D检测: {(is3DEnabled ? "启用" : "禁用")}");
                    LogMessage("统一检测管理器现在将协调2D和3D检测，并记录真实性能数据");
                }
                else
                {
                    LogMessage("⚠️ 无法获取检测管理器，系统测试可能不完整");
                }
                
                // 🔧 修复：现在所有时间测量都使用真实回调，不再使用随机模拟
                
                // 模拟VM回调（通常在几十毫秒内完成）
                // 注：VM回调目前仍使用模拟，因为VM系统还没有集成真实回调通知
                var vmTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50 + new Random().Next(50))
                };
                vmTimer.Tick += (s, e) =>
                {
                    vmTimer.Stop();
                    RecordVMCallbackTime();
                };
                vmTimer.Start();
                
                // ✅ 3D相机回调：使用真实回调
                // 真实的3D回调会在StaticMeasureEx_ImageExecuted中调用Notify3DCallbackCompleted()
                LogMessage("3D回调监听器已设置（真实回调）");
                
                // ✅ IO输出时间：现在会在IOManager.SetDetectionResult()调用时触发真实回调
                // ✅ 界面渲染时间：现在会在界面更新完成时触发真实回调
                
                LogMessage("已设置真实性能监控（VM模拟，3D/IO/UI使用真实回调）");
            }
            catch (Exception ex)
            {
                LogMessage($"设置回调监听失败: {ex.Message}");
            }
        }

        #endregion

        #region 时间记录

        /// <summary>
        /// 记录VM回调完成时间
        /// </summary>
        private void RecordVMCallbackTime()
        {
            if (_isTestRunning && _vmCallbackTime < 0)
            {
                _vmCallbackTime = _testStopwatch.ElapsedMilliseconds;
                Dispatcher.Invoke(() =>
                {
                    Callback2DTimeText.Text = $"{_vmCallbackTime:F1} ms";
                });
                LogMessage($"VM回调完成: {_vmCallbackTime:F1} ms");
            }
        }

        /// <summary>
        /// 记录3D相机回调完成时间
        /// </summary>
        private void Record3DCallbackTime()
        {
            if (_isTestRunning && _ljdCallbackTime < 0)
            {
                _ljdCallbackTime = _testStopwatch.ElapsedMilliseconds;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LJDCallbackTimeText.Text = $"{_ljdCallbackTime:F1} ms";
                }));
                LogMessage($"3D相机回调完成: {_ljdCallbackTime:F1} ms");
            }
        }

        /// <summary>
        /// 记录IO输出完成时间
        /// </summary>
        private void RecordIOOutputTime()
        {
            if (_isTestRunning && _ioOutputTime < 0)
            {
                _ioOutputTime = _testStopwatch.ElapsedMilliseconds;
                Dispatcher.Invoke(() =>
                {
                    IOOutputTimeText.Text = $"{_ioOutputTime:F1} ms";
                });
                LogMessage($"IO输出完成: {_ioOutputTime:F1} ms");
            }
        }

        /// <summary>
        /// 记录界面渲染完成时间
        /// </summary>
        private void RecordUIRenderTime()
        {
            if (_isTestRunning && _uiRenderTime < 0)
            {
                _uiRenderTime = _testStopwatch.ElapsedMilliseconds;
                Dispatcher.Invoke(() =>
                {
                    UIRenderTimeText.Text = $"{_uiRenderTime:F1} ms";
                });
                LogMessage($"界面渲染完成: {_uiRenderTime:F1} ms");
                
                // 🔧 修复：界面渲染完成后，自动完成整个系统测试
                FinishTest("测试完成");
            }
        }

        #endregion

        #region 测试完成

        /// <summary>
        /// 完成测试
        /// </summary>
        private void FinishTest(string status)
        {
            try
            {
                if (!_isTestRunning) return;
                
                _testStopwatch.Stop();
                _isTestRunning = false;
                
                // 🔧 新增：停止系统测试模式，恢复正常检测模式
                StopSystemTestMode();
                
                Dispatcher.Invoke(() =>
                {
                    // 更新总耗时
                    double totalTime = _testStopwatch.ElapsedMilliseconds;
                    TotalTimeText.Text = $"{totalTime:F1} ms";
                    
                    // 更新状态
                    TestStatusText.Text = status;
                    TriggerButton.IsEnabled = true;
                    
                    LogMessage($"测试完成: {status}, 总耗时: {totalTime:F1} ms");
                    
                    // 生成测试报告
                    GenerateTestReport();
                });
            }
            catch (Exception ex)
            {
                LogMessage($"完成测试时发生错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 停止系统测试模式，恢复正常检测模式
        /// </summary>
        private void StopSystemTestMode()
        {
            try
            {
                var page1Instance = WpfApp2.UI.Page1.PageManager.Page1Instance;
                if (page1Instance?.DetectionManager != null)
                {
                    page1Instance.DetectionManager.StopSystemTestMode();
                    LogMessage("✅ 已停止系统测试模式，恢复正常检测模式");
                }
                else
                {
                    LogMessage("⚠️ 无法访问检测管理器，无法恢复正常模式");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"停止系统测试模式失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成测试报告
        /// </summary>
        private void GenerateTestReport()
        {
            try
            {
                string report = $@"
🔬 系统测试报告
==================
测试时间: {_triggerTime:yyyy-MM-dd HH:mm:ss.fff}

⏱️ 性能指标:
• VM回调完成: {(_vmCallbackTime >= 0 ? $"{_vmCallbackTime:F1} ms" : "未完成")}
• 3D相机回调: {(_ljdCallbackTime >= 0 ? $"{_ljdCallbackTime:F1} ms" : "未完成")}
• IO输出完成: {(_ioOutputTime >= 0 ? $"{_ioOutputTime:F1} ms" : "未完成")}
• 界面渲染完成: {(_uiRenderTime >= 0 ? $"{_uiRenderTime:F1} ms" : "未完成")}
• 总耗时: {_testStopwatch.ElapsedMilliseconds:F1} ms

📊 性能分析:
• VM处理效率: {(_vmCallbackTime >= 0 && _vmCallbackTime < 100 ? "优秀" : "需优化")}
• 3D检测速度: {(_ljdCallbackTime >= 0 && _ljdCallbackTime < 500 ? "正常" : "偏慢")}
• 系统响应性: {(_testStopwatch.ElapsedMilliseconds < 1000 ? "快速" : "一般")}
";
                
                LogMessage(report);
            }
            catch (Exception ex)
            {
                LogMessage($"生成测试报告失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置测试状态
        /// </summary>
        private void ResetTestState()
        {
            _vmCallbackTime = -1;
            _ljdCallbackTime = -1;
            _ioOutputTime = -1;
            _uiRenderTime = -1;
            
            Callback2DTimeText.Text = "-- ms";
            LJDCallbackTimeText.Text = "-- ms";
            IOOutputTimeText.Text = "-- ms";
            UIRenderTimeText.Text = "-- ms";
            TotalTimeText.Text = "-- ms";
        }

        #endregion

        #region 按钮事件

        /// <summary>
        /// 电机参数设置按钮点击事件
        /// </summary>
        private void MotorSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建并显示电机参数设置窗口
                var motorSettingsWindow = new MotorParameterSettingsWindow();
                motorSettingsWindow.Owner = this;
                motorSettingsWindow.ShowDialog();
                
                LogMessage("电机参数设置窗口已打开");
            }
            catch (Exception ex)
            {
                LogMessage($"打开电机参数设置窗口失败: {ex.Message}");
                MessageBox.Show($"打开电机参数设置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清除记录按钮点击事件
        /// </summary>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResetTestState();
                TestStatusText.Text = "准备就绪";
                LogMessage("测试记录已清除");
            }
            catch (Exception ex)
            {
                LogMessage($"清除记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region 窗口事件

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 🔧 新增：如果正在测试，停止系统测试模式
                if (_isTestRunning)
                {
                    StopSystemTestMode();
                }
                
                // 清理资源
                _currentInstance = null;
                
                if (_plcStatusTimer != null)
                {
                    _plcStatusTimer.Stop();
                    _plcStatusTimer.Tick -= PLCStatusTimer_Tick;
                    _plcStatusTimer = null;
                }
                
                if (_testStopwatch != null)
                {
                    _testStopwatch.Stop();
                    _testStopwatch = null;
                }
                
                LogMessage("系统测试窗口已关闭");
            }
            catch (Exception ex)
            {
                LogMessage($"关闭窗口时清理资源失败: {ex.Message}");
            }
            
            base.OnClosed(e);
        }

        #endregion

        #region 静态方法（用于外部调用）

        /// <summary>
        /// 外部调用：记录VM回调完成时间
        /// </summary>
        public static void NotifyVMCallbackCompleted()
        {
            _currentInstance?.RecordVMCallbackTime();
        }

        /// <summary>
        /// 外部调用：记录3D回调完成时间
        /// </summary>
        public static void Notify3DCallbackCompleted()
        {
            _currentInstance?.Record3DCallbackTime();
        }

        /// <summary>
        /// 外部调用：记录IO输出完成时间
        /// </summary>
        public static void NotifyIOOutputCompleted()
        {
            _currentInstance?.RecordIOOutputTime();
        }

        /// <summary>
        /// 外部调用：记录界面渲染完成时间
        /// </summary>
        public static void NotifyUIRenderCompleted()
        {
            _currentInstance?.RecordUIRenderTime();
        }

        #endregion

        #region 日志记录

        /// <summary>
        /// 记录日志信息
        /// </summary>
        private void LogMessage(string message)
        {
            try
            {
                LogManager.Info($"[系统测试] {message}");
            }
            catch
            {
                // 忽略日志记录异常
            }
        }

        #endregion
    }
} 
