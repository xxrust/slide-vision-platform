using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 专业启动加载界面
    /// </summary>
    public partial class SplashScreen : Window
    {
        private DispatcherTimer _progressTimer;
        private int _currentProgress = 0;
        private readonly string[] _loadingSteps = new string[]
        {
            "初始化系统组件...",
            "加载配置文件...",
            "初始化日志管理器...",
            "加载VM解决方案...",
            "初始化相机模块...",
            "连接硬件设备...",
            "初始化3D检测系统...",
            "加载用户界面...",
            "准备就绪..."
        };
        
        private int _currentStepIndex = 0;

        public SplashScreen()
        {
            InitializeComponent();
            SetVersionText();
            InitializeAnimations();
            StartLoadingProcess();
        }

        private void SetVersionText()
        {
            try
            {
                if (VersionText != null)
                {
                    VersionText.Text = AppVersionInfo.GetSplashVersionText();
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"设置启动界面版本信息失败: {ex.Message}", "SplashScreen");
            }
        }

        /// <summary>
        /// 初始化动画效果
        /// </summary>
        private void InitializeAnimations()
        {
            try
            {
                // 启动文字淡入淡出动画
                var textFadeStoryboard = (Storyboard)FindResource("TextFadeAnimation");
                if (textFadeStoryboard != null)
                {
                    Storyboard.SetTarget(textFadeStoryboard, LoadingStatusText);
                    textFadeStoryboard.Begin();
                }
            }
            catch (Exception ex)
            {
                // 动画初始化失败不影响主要功能
                LogManager.Warning($"启动界面动画初始化失败: {ex.Message}", "SplashScreen");
            }
        }

        /// <summary>
        /// 开始加载过程
        /// </summary>
        private void StartLoadingProcess()
        {
            try
            {
                // 创建进度更新定时器
                _progressTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(150) // 每150ms更新一次
                };
                _progressTimer.Tick += ProgressTimer_Tick;
                _progressTimer.Start();

                LogManager.Info("启动加载界面已显示", "SplashScreen");
            }
            catch (Exception ex)
            {
                LogManager.Error($"启动加载过程失败: {ex.Message}", "SplashScreen");
                // 如果加载过程失败，直接关闭启动界面
                Close();
            }
        }

        /// <summary>
        /// 进度定时器事件处理
        /// </summary>
        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 更新进度条
                _currentProgress += 2; // 每次增加2%
                
                if (_currentProgress > 100)
                {
                    _currentProgress = 100;
                }

                // 更新UI
                LoadingProgressBar.Value = _currentProgress;
                ProgressPercentage.Text = $"{_currentProgress}%";

                // 更新加载步骤
                UpdateLoadingStep();

                // 检查是否完成加载
                if (_currentProgress >= 100)
                {
                    CompleteLoading();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新加载进度失败: {ex.Message}", "SplashScreen");
                CompleteLoading(); // 出错时直接完成加载
            }
        }

        /// <summary>
        /// 更新加载步骤显示
        /// </summary>
        private void UpdateLoadingStep()
        {
            try
            {
                // 根据进度确定当前步骤
                int targetStepIndex = (_currentProgress * _loadingSteps.Length) / 100;
                targetStepIndex = Math.Min(targetStepIndex, _loadingSteps.Length - 1);

                if (targetStepIndex != _currentStepIndex && targetStepIndex < _loadingSteps.Length)
                {
                    _currentStepIndex = targetStepIndex;
                    
                    // 更新主状态文本
                    LoadingStatusText.Text = _loadingSteps[_currentStepIndex];
                    
                    // 更新详细状态文本
                    UpdateDetailStatus(_currentStepIndex);
                    
                    LogManager.Verbose($"加载步骤: {_loadingSteps[_currentStepIndex]} ({_currentProgress}%)", "SplashScreen");
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"更新加载步骤失败: {ex.Message}", "SplashScreen");
            }
        }

        /// <summary>
        /// 更新详细状态信息
        /// </summary>
        /// <param name="stepIndex">当前步骤索引</param>
        private void UpdateDetailStatus(int stepIndex)
        {
            try
            {
                string detailText = "";
                
                switch (stepIndex)
                {
                    case 0: detailText = "初始化核心组件和服务..."; break;
                    case 1: detailText = "读取系统配置参数..."; break;
                    case 2: detailText = "设置日志记录系统..."; break;
                    case 3: detailText = "加载图像处理算法..."; break;
                    case 4: detailText = "检测相机连接状态..."; break;
                    case 5: detailText = "建立硬件通信链路..."; break;
                    case 6: detailText = "启动3D检测引擎..."; break;
                    case 7: detailText = "渲染用户界面元素..."; break;
                    case 8: detailText = "系统准备完成，即将启动..."; break;
                    default: detailText = "正在处理..."; break;
                }
                
                DetailStatusText.Text = detailText;
            }
            catch (Exception ex)
            {
                LogManager.Warning($"更新详细状态失败: {ex.Message}", "SplashScreen");
            }
        }

        /// <summary>
        /// 完成加载过程
        /// </summary>
        private void CompleteLoading()
        {
            try
            {
                // 停止定时器
                _progressTimer?.Stop();
                _progressTimer = null;

                // 延迟一下让用户看到100%完成
                Task.Delay(500).ContinueWith(_ =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            LogManager.Info("启动加载完成，关闭启动界面", "SplashScreen");
                            
                            // 创建淡出动画
                            var fadeOut = new DoubleAnimation
                            {
                                From = 1.0,
                                To = 0.0,
                                Duration = TimeSpan.FromMilliseconds(500)
                            };
                            
                            fadeOut.Completed += (s, e) =>
                            {
                                Close();
                            };
                            
                            // 开始淡出动画
                            BeginAnimation(OpacityProperty, fadeOut);
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"关闭启动界面失败: {ex.Message}", "SplashScreen");
                            Close(); // 强制关闭
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                LogManager.Error($"完成加载过程失败: {ex.Message}", "SplashScreen");
                Close(); // 强制关闭
            }
        }

        /// <summary>
        /// 手动设置进度（供外部调用）
        /// </summary>
        /// <param name="progress">进度百分比 (0-100)</param>
        /// <param name="status">状态文本</param>
        public void SetProgress(int progress, string status = null)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 外部控制进度时，停止内部模拟进度，避免提前关闭启动界面
                    _progressTimer?.Stop();
                    _progressTimer = null;

                    _currentProgress = Math.Max(0, Math.Min(100, progress));
                    LoadingProgressBar.Value = _currentProgress;
                    ProgressPercentage.Text = $"{_currentProgress}%";
                    
                    if (!string.IsNullOrEmpty(status))
                    {
                        LoadingStatusText.Text = status;
                    }
                    
                    if (_currentProgress >= 100)
                    {
                        CompleteLoading();
                    }
                }));
            }
            catch (Exception ex)
            {
                LogManager.Warning($"设置进度失败: {ex.Message}", "SplashScreen");
            }
        }

        /// <summary>
        /// 手动设置详细状态
        /// </summary>
        /// <param name="detailStatus">详细状态文本</param>
        public void SetDetailStatus(string detailStatus)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    DetailStatusText.Text = detailStatus ?? "";
                }));
            }
            catch (Exception ex)
            {
                LogManager.Warning($"设置详细状态失败: {ex.Message}", "SplashScreen");
            }
        }

        /// <summary>
        /// 强制关闭启动界面
        /// </summary>
        public void ForceClose()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _progressTimer?.Stop();
                    Close();
                }));
            }
            catch (Exception ex)
            {
                LogManager.Error($"强制关闭启动界面失败: {ex.Message}", "SplashScreen");
            }
        }

        /// <summary>
        /// 窗口关闭时的清理工作
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _progressTimer?.Stop();
                _progressTimer = null;
                LogManager.Info("启动界面已关闭", "SplashScreen");
            }
            catch (Exception ex)
            {
                LogManager.Warning($"启动界面清理失败: {ex.Message}", "SplashScreen");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }

    /// <summary>
    /// 进度条宽度转换器（简化版本，如果需要更复杂的进度条样式）
    /// </summary>
    public class ProgressToWidthConverter : System.Windows.Data.IValueConverter
    {
        public static readonly ProgressToWidthConverter Instance = new ProgressToWidthConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double progress)
            {
                return progress; // 简化处理，直接返回进度值
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 
