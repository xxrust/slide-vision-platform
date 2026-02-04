using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// 智能输入卡片弹出窗口
    /// </summary>
    public partial class SmartInputCardWindow : Window
    {
        public event EventHandler<ValueChangedEventArgs> ValueChanged;
        
        private readonly string _originalValue;

        public SmartInputCardWindow()
        {
            InitializeComponent();
            InitializeWindow();
        }

        public SmartInputCardWindow(string parameterName, double currentValue, string stepName = null,
                                  string title = null, string description = null, string imagePath = null, 
                                  string unit = null, double? minValue = null, double? maxValue = null) : this()
        {
            _originalValue = currentValue.ToString();
            SetupInputCard(parameterName, currentValue, stepName, title, description, imagePath, unit, minValue, maxValue);
        }

        private void InitializeWindow()
        {
            // 订阅卡片事件
            InputCard.ValueChanged += InputCard_ValueChanged;
            InputCard.CardClosed += InputCard_CardClosed;
            InputCard.NavigationRequested += InputCard_NavigationRequested;
            InputCard.AutoExecutionRequested += InputCard_AutoExecutionRequested;
            InputCard.WindowDragRequested += InputCard_WindowDragRequested;
            
            // 支持ESC键关闭
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                }
            };

            // 窗口加载后获得焦点并同步滑动条
            Loaded += (s, e) => 
            {
                Focus();
                // 强制同步滑动条位置
                InputCard?.ForceSliderSync();
            };
        }

        private void SetupInputCard(string parameterName, double currentValue, string stepName = null,
                                  string title = null, string description = null, string imagePath = null, 
                                  string unit = null, double? minValue = null, double? maxValue = null)
        {
            InputCard.ParameterName = parameterName;
            InputCard.CurrentValue = currentValue;
            InputCard.StepName = stepName ?? "";
            
            if (!string.IsNullOrEmpty(title))
                InputCard.Title = title;
            
            if (!string.IsNullOrEmpty(unit))
                InputCard.Unit = unit;
            
            if (minValue.HasValue)
                InputCard.MinValue = minValue.Value;
            
            if (maxValue.HasValue)
                InputCard.MaxValue = maxValue.Value;

            // 如果提供了描述或图片路径，直接设置
            if (!string.IsNullOrEmpty(description) || !string.IsNullOrEmpty(imagePath))
            {
                InputCard.SetParameterConfig(parameterName, title, description, imagePath, unit, minValue, maxValue);
            }
        }

        /// <summary>
        /// 在指定位置显示窗口
        /// </summary>
        public void ShowAt(Point position)
        {
            // 先显示窗口以获取实际尺寸
            Show();
            
            // 确保窗口已完成布局
            UpdateLayout();
            
            // 获取屏幕尺寸和窗口尺寸
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var windowWidth = ActualWidth;
            var windowHeight = ActualHeight;
            
            // 计算窗口位置，确保不超出屏幕边界
            var left = Math.Max(0, Math.Min(position.X - windowWidth / 2, screenWidth - windowWidth));
            var top = Math.Max(0, Math.Min(position.Y - windowHeight / 2, screenHeight - windowHeight));
            
            Left = left;
            Top = top;
        }

        /// <summary>
        /// 在指定位置显示窗口，可选择是否右对齐
        /// </summary>
        public void ShowAt(Point position, bool alignRight = false)
        {
            if (!alignRight)
            {
                ShowAt(position);
                return;
            }

            // 先显示窗口以获取实际尺寸
            Show();
            
            // 确保窗口已完成布局
            UpdateLayout();
            
            // 获取屏幕尺寸和窗口尺寸
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var windowWidth = ActualWidth;
            var windowHeight = ActualHeight;
            
            // 右对齐显示，预留20像素边距
            var rightMargin = 20;
            var left = screenWidth - windowWidth - rightMargin;
            var top = Math.Max(20, Math.Min(position.Y - windowHeight / 2, screenHeight - windowHeight - 20));
            
            Left = left;
            Top = top;
        }

        /// <summary>
        /// 在指定控件附近显示窗口，卡片右边与屏幕右边界对齐
        /// </summary>
        public void ShowNear(FrameworkElement element)
        {
            if (element == null) return;

            // 先显示窗口以获取实际尺寸
            Show();
            
            // 确保窗口已完成布局
            UpdateLayout();

            // 获取控件在屏幕上的位置
            var elementPosition = element.PointToScreen(new Point(0, 0));
            var elementHeight = element.ActualHeight;

            // 获取屏幕尺寸和窗口尺寸
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var windowWidth = ActualWidth;
            var windowHeight = ActualHeight;
            
            // 卡片右边与屏幕右边界对齐，预留20像素边距
            var rightMargin = 20;
            var cardLeft = screenWidth - windowWidth - rightMargin;
            
            // 垂直位置以控件中心为准，但确保不超出屏幕边界
            var cardTop = elementPosition.Y + elementHeight / 2 - windowHeight / 2;
            cardTop = Math.Max(20, Math.Min(cardTop, screenHeight - windowHeight - 20));
            
            // 设置窗口位置
            Left = cardLeft;
            Top = cardTop;
        }

        #region 事件处理
        private void InputCard_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }

        private void InputCard_CardClosed(object sender, EventArgs e)
        {
            Close();
        }

        private void InputCard_NavigationRequested(object sender, NavigationEventArgs e)
        {
            // 在配置页面中处理导航
            try
            {
                var templateConfigPage = Application.Current.Windows
                    .OfType<Window>()
                    .SelectMany(w => FindVisualChildren<TemplateConfigPage>(w))
                    .FirstOrDefault();

                if (templateConfigPage != null)
                {
                    templateConfigPage.NavigateToParameterInCard(e.Direction, e.CurrentParameterName, this);
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"参数导航失败: {ex.Message}");
            }
        }

        private void InputCard_AutoExecutionRequested(object sender, ExecutionEventArgs e)
        {
            // 自动触发执行按钮
            try
            {
                var templateConfigPage = Application.Current.Windows
                    .OfType<Window>()
                    .SelectMany(w => FindVisualChildren<TemplateConfigPage>(w))
                    .FirstOrDefault();

                if (templateConfigPage != null)
                {
                    templateConfigPage.TriggerAutoExecution(e.ParameterName, e.NewValue);
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"自动执行失败: {ex.Message}");
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 只有直接点击Border背景时才关闭窗口
            if (e.Source == sender)
            {
                Close();
            }
        }

        private void InputCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 阻止事件冒泡到Grid，避免关闭窗口
            e.Handled = true;
        }

        private void InputCard_WindowDragRequested(object sender, MouseButtonEventArgs e)
        {
            // 处理窗口拖动
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch (Exception ex)
                {
                    LogManager.Warning($"窗口拖动失败: {ex.Message}");
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // ESC键关闭窗口
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
        #endregion

        #region 静态便捷方法
        /// <summary>
        /// 显示智能输入卡片
        /// </summary>
        public static SmartInputCardWindow ShowInputCard(FrameworkElement nearElement, string parameterName, 
                                                       double currentValue, string stepName = null, string title = null, 
                                                       string description = null, string imagePath = null, string unit = null, 
                                                       double? minValue = null, double? maxValue = null)
        {
            var window = new SmartInputCardWindow(parameterName, currentValue, stepName, title, description, 
                                                imagePath, unit, minValue, maxValue);
            
            if (nearElement != null)
            {
                window.ShowNear(nearElement);
            }
            else
            {
                // 居中显示
                window.Show();
                window.UpdateLayout();
                
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                window.Left = (screenWidth - window.ActualWidth) / 2;
                window.Top = (screenHeight - window.ActualHeight) / 2;
            }
            
            return window;
        }

        /// <summary>
        /// 显示智能输入卡片并等待结果
        /// </summary>
        public static (bool confirmed, double value) ShowInputCardDialog(FrameworkElement nearElement, 
                                                                        string parameterName, double currentValue,
                                                                        string stepName = null, string title = null, 
                                                                        string description = null, string imagePath = null, 
                                                                        string unit = null, double? minValue = null, 
                                                                        double? maxValue = null)
        {
            var window = new SmartInputCardWindow(parameterName, currentValue, stepName, title, description, 
                                                imagePath, unit, minValue, maxValue);
            
            bool confirmed = false;
            double finalValue = currentValue;
            
            window.ValueChanged += (s, e) =>
            {
                confirmed = true;
                finalValue = e.NewValue;
            };
            
            if (nearElement != null)
            {
                window.ShowNear(nearElement);
            }
            else
            {
                // 居中显示
                window.Show();
                window.UpdateLayout();
                
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                window.Left = (screenWidth - window.ActualWidth) / 2;
                window.Top = (screenHeight - window.ActualHeight) / 2;
            }
            
            // 将窗口转为模态并等待关闭
            window.Hide(); // 先隐藏
            window.ShowDialog(); // 再以模态方式显示
            
            return (confirmed, finalValue);
        }
        #endregion

        /// <summary>
        /// 更新卡片数据而不关闭窗口
        /// </summary>
        public void UpdateParameterData(string parameterName, double currentValue, string stepName = null,
                                      string title = null, string description = null, string imagePath = null, 
                                      string unit = null, double? minValue = null, double? maxValue = null)
        {
            try
            {
                // 更新卡片数据
                SetupInputCard(parameterName, currentValue, stepName, title, description, imagePath, unit, minValue, maxValue);
                
                // 强制同步滑动条位置
                InputCard?.ForceSliderSync();
                
                LogManager.Info($"卡片数据已更新: {parameterName} = {currentValue}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新卡片数据失败: {ex.Message}");
            }
        }

        #region 辅助方法
        /// <summary>
        /// 查找可视化树中的子控件
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        #endregion
    }
} 