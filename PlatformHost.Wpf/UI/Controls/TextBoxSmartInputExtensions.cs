using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// TextBox智能输入扩展方法
    /// </summary>
    public static class TextBoxSmartInputExtensions
    {
        // 附加属性：启用智能输入
        public static readonly DependencyProperty EnableSmartInputProperty =
            DependencyProperty.RegisterAttached("EnableSmartInput", typeof(bool), typeof(TextBoxSmartInputExtensions),
                new PropertyMetadata(false, OnEnableSmartInputChanged));

        // 附加属性：参数名称
        public static readonly DependencyProperty ParameterNameProperty =
            DependencyProperty.RegisterAttached("ParameterName", typeof(string), typeof(TextBoxSmartInputExtensions));

        // 附加属性：参数标题
        public static readonly DependencyProperty ParameterTitleProperty =
            DependencyProperty.RegisterAttached("ParameterTitle", typeof(string), typeof(TextBoxSmartInputExtensions));

        // 附加属性：参数描述
        public static readonly DependencyProperty ParameterDescriptionProperty =
            DependencyProperty.RegisterAttached("ParameterDescription", typeof(string), typeof(TextBoxSmartInputExtensions));

        // 附加属性：图片路径
        public static readonly DependencyProperty ImagePathProperty =
            DependencyProperty.RegisterAttached("ImagePath", typeof(string), typeof(TextBoxSmartInputExtensions));

        // 附加属性：单位
        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.RegisterAttached("Unit", typeof(string), typeof(TextBoxSmartInputExtensions),
                new PropertyMetadata("mm"));

        // 附加属性：最小值
        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.RegisterAttached("MinValue", typeof(double?), typeof(TextBoxSmartInputExtensions));

        // 附加属性：最大值
        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.RegisterAttached("MaxValue", typeof(double?), typeof(TextBoxSmartInputExtensions));

        // 存储当前打开的窗口，确保同一时间只有一个智能输入窗口
        private static SmartInputCardWindow _currentWindow;

        #region 附加属性的 Getter/Setter
        public static bool GetEnableSmartInput(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableSmartInputProperty);
        }

        public static void SetEnableSmartInput(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableSmartInputProperty, value);
        }

        public static string GetParameterName(DependencyObject obj)
        {
            return (string)obj.GetValue(ParameterNameProperty);
        }

        public static void SetParameterName(DependencyObject obj, string value)
        {
            obj.SetValue(ParameterNameProperty, value);
        }

        public static string GetParameterTitle(DependencyObject obj)
        {
            return (string)obj.GetValue(ParameterTitleProperty);
        }

        public static void SetParameterTitle(DependencyObject obj, string value)
        {
            obj.SetValue(ParameterTitleProperty, value);
        }

        public static string GetParameterDescription(DependencyObject obj)
        {
            return (string)obj.GetValue(ParameterDescriptionProperty);
        }

        public static void SetParameterDescription(DependencyObject obj, string value)
        {
            obj.SetValue(ParameterDescriptionProperty, value);
        }

        public static string GetImagePath(DependencyObject obj)
        {
            return (string)obj.GetValue(ImagePathProperty);
        }

        public static void SetImagePath(DependencyObject obj, string value)
        {
            obj.SetValue(ImagePathProperty, value);
        }

        public static string GetUnit(DependencyObject obj)
        {
            return (string)obj.GetValue(UnitProperty);
        }

        public static void SetUnit(DependencyObject obj, string value)
        {
            obj.SetValue(UnitProperty, value);
        }

        public static double? GetMinValue(DependencyObject obj)
        {
            return (double?)obj.GetValue(MinValueProperty);
        }

        public static void SetMinValue(DependencyObject obj, double? value)
        {
            obj.SetValue(MinValueProperty, value);
        }

        public static double? GetMaxValue(DependencyObject obj)
        {
            return (double?)obj.GetValue(MaxValueProperty);
        }

        public static void SetMaxValue(DependencyObject obj, double? value)
        {
            obj.SetValue(MaxValueProperty, value);
        }
        #endregion

        private static void OnEnableSmartInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if ((bool)e.NewValue)
                {
                    AttachSmartInput(textBox);
                }
                else
                {
                    DetachSmartInput(textBox);
                }
            }
        }

        private static void AttachSmartInput(TextBox textBox)
        {
            // 添加鼠标点击事件
            textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
            
            // 添加获得焦点事件
            textBox.GotFocus += TextBox_GotFocus;
            
            // 设置只读，防止直接编辑
            textBox.IsReadOnly = true;
            textBox.Cursor = Cursors.Hand;
            
            // 添加视觉提示样式
            ApplySmartInputStyle(textBox);
        }

        private static void DetachSmartInput(TextBox textBox)
        {
            // 移除事件处理器
            textBox.PreviewMouseLeftButtonDown -= TextBox_PreviewMouseLeftButtonDown;
            textBox.GotFocus -= TextBox_GotFocus;
            
            // 恢复可编辑状态
            textBox.IsReadOnly = false;
            textBox.Cursor = Cursors.IBeam;
        }

        private static void ApplySmartInputStyle(TextBox textBox)
        {
            // 为启用智能输入的TextBox添加特殊样式
            try
            {
                var style = new Style(typeof(TextBox));
                
                // 设置背景色提示这是智能输入框
                style.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.LightCyan));
                style.Setters.Add(new Setter(Control.BorderBrushProperty, System.Windows.Media.Brushes.DeepSkyBlue));
                style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
                
                // 添加鼠标悬停效果
                var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                trigger.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.LightBlue));
                style.Triggers.Add(trigger);
                
                textBox.Style = style;
            }
            catch (Exception ex)
            {
                LogManager.Warning($"设置智能输入框样式失败: {ex.Message}");
            }
        }

        private static void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                ShowSmartInputCard(textBox);
                e.Handled = true; // 阻止默认处理
            }
        }

        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                ShowSmartInputCard(textBox);
            }
        }

        private static void ShowSmartInputCard(TextBox textBox)
        {
            try
            {
                // 检查是否是路径类输入框，如果是则不显示智能输入卡片
                if (IsPathInputTextBox(textBox))
                {
                    return;
                }

                // 关闭当前打开的窗口（如果有的话）
                CloseCurrentWindow();

                // 获取参数配置
                var parameterName = GetParameterName(textBox);
                var title = GetParameterTitle(textBox) ?? parameterName;
                var description = GetParameterDescription(textBox);
                var imagePath = GetImagePath(textBox);
                var unit = GetUnit(textBox);
                var minValue = GetMinValue(textBox);
                var maxValue = GetMaxValue(textBox);

                // 获取当前值
                if (!double.TryParse(textBox.Text, out double currentValue))
                {
                    currentValue = 0.0;
                }

                // 如果没有设置参数名，使用textBox的Name或Tag
                if (string.IsNullOrEmpty(parameterName))
                {
                    parameterName = textBox.Name ?? textBox.Tag?.ToString() ?? "未知参数";
                }

                // 获取当前步骤名称
                var stepName = GetCurrentStepName();

                // 创建并显示智能输入卡片
                _currentWindow = SmartInputCardWindow.ShowInputCard(
                    textBox, parameterName, currentValue, stepName, title, description, 
                    imagePath, unit, minValue, maxValue);

                // 订阅值变更事件
                _currentWindow.ValueChanged += (s, e) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 智能格式化：整数显示为整数，小数显示为小数
                        textBox.Text = e.NewValue % 1 == 0 ? e.NewValue.ToString("F0") : e.NewValue.ToString();
                        
                        // 触发TextChanged事件，通知其他组件值已更改
                        var textChangedEventArgs = new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None);
                        textBox.RaiseEvent(textChangedEventArgs);
                    });
                };

                // 订阅窗口关闭事件
                _currentWindow.Closed += (s, e) =>
                {
                    _currentWindow = null;
                };
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示智能输入卡片失败: {ex.Message}");
                MessageBox.Show($"打开参数设置面板失败: {ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 关闭当前打开的智能输入窗口
        /// </summary>
        public static void CloseCurrentWindow()
        {
            if (_currentWindow != null)
            {
                _currentWindow.Close();
                _currentWindow = null;
            }
        }

        /// <summary>
        /// 检查是否是路径类输入框
        /// </summary>
        private static bool IsPathInputTextBox(TextBox textBox)
        {
            var parameterName = GetParameterName(textBox);
            var name = textBox.Name ?? "";
            var tag = textBox.Tag?.ToString() ?? "";
            
            // 检查参数名、控件名或标签是否包含路径相关关键字
            var pathKeywords = new[] { "path", "路径", "template", "模板", "file", "文件", "image", "图像", "picture", "图片", "browse", "浏览" };
            
            return pathKeywords.Any(keyword => 
                parameterName?.ToLower().Contains(keyword) == true ||
                name.ToLower().Contains(keyword) ||
                tag.ToLower().Contains(keyword));
        }

        /// <summary>
        /// 获取当前步骤名称
        /// </summary>
        private static string GetCurrentStepName()
        {
            try
            {
                // 查找TemplateConfigPage实例
                var templateConfigPage = Application.Current.Windows
                    .OfType<Window>()
                    .SelectMany(w => FindVisualChildren<TemplateConfigPage>(w))
                    .FirstOrDefault();

                if (templateConfigPage != null)
                {
                    return templateConfigPage.GetCurrentStepDisplayName();
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"获取当前步骤名称失败: {ex.Message}");
            }

            return "未知步骤";
        }

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

        /// <summary>
        /// 批量为TextBox设置智能输入
        /// </summary>
        /// <param name="textBoxConfigs">TextBox配置列表</param>
        public static void SetupSmartInputBatch(IEnumerable<SmartInputConfig> textBoxConfigs)
        {
            foreach (var config in textBoxConfigs)
            {
                if (config.TextBox != null)
                {
                    SetEnableSmartInput(config.TextBox, true);
                    SetParameterName(config.TextBox, config.ParameterName);
                    
                    if (!string.IsNullOrEmpty(config.Title))
                        SetParameterTitle(config.TextBox, config.Title);
                    
                    if (!string.IsNullOrEmpty(config.Description))
                        SetParameterDescription(config.TextBox, config.Description);
                    
                    if (!string.IsNullOrEmpty(config.ImagePath))
                        SetImagePath(config.TextBox, config.ImagePath);
                    
                    if (!string.IsNullOrEmpty(config.Unit))
                        SetUnit(config.TextBox, config.Unit);
                    
                    if (config.MinValue.HasValue)
                        SetMinValue(config.TextBox, config.MinValue);
                    
                    if (config.MaxValue.HasValue)
                        SetMaxValue(config.TextBox, config.MaxValue);
                }
            }
        }
    }

    /// <summary>
    /// 智能输入配置类
    /// </summary>
    public class SmartInputConfig
    {
        public TextBox TextBox { get; set; }
        public string ParameterName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
        public string Unit { get; set; } = "mm";
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }
} 