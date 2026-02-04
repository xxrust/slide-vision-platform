using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// 智能输入卡片用户控件
    /// </summary>
    public partial class SmartInputCard : UserControl, INotifyPropertyChanged
    {
        #region 事件定义
        public event EventHandler<ValueChangedEventArgs> ValueChanged;
        public event EventHandler CardClosed;
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region 依赖属性
        public static readonly DependencyProperty CurrentValueProperty =
            DependencyProperty.Register(nameof(CurrentValue), typeof(double), typeof(SmartInputCard),
                new PropertyMetadata(0.0, OnCurrentValueChanged));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(SmartInputCard),
                new PropertyMetadata(0.0, OnRangeChanged));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(SmartInputCard),
                new PropertyMetadata(100.0, OnRangeChanged));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(SmartInputCard),
                new PropertyMetadata("", OnUnitChanged));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SmartInputCard),
                new PropertyMetadata("参数设置", OnTitleChanged));

        public static readonly DependencyProperty ParameterNameProperty =
            DependencyProperty.Register(nameof(ParameterName), typeof(string), typeof(SmartInputCard),
                new PropertyMetadata(string.Empty));
        #endregion

        #region 属性
        public double CurrentValue
        {
            get => (double)GetValue(CurrentValueProperty);
            set => SetValue(CurrentValueProperty, value);
        }

        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string ParameterName
        {
            get => (string)GetValue(ParameterNameProperty);
            set => SetValue(ParameterNameProperty, value);
        }

        private double _inputValue;
        public double InputValue
        {
            get => _inputValue;
            set
            {
                // 根据当前步长精度对输入值进行舍入，确保精度一致性
                var newValue = RoundToStepPrecision(value);
                if (Math.Abs(_inputValue - newValue) > 0.0001) // 避免不必要的更新
                {
                    _inputValue = newValue;
                    OnPropertyChanged(nameof(InputValue));
                    UpdateInputDisplay();
                    UpdateSliderValue();
                }
                // 移除自动触发执行，改为手动触发
                // TriggerAutoExecution();
            }
        }

        private string _currentInput = "0";
        private bool _hasDecimalPoint = false;
        private bool _isNegative = false;
        private bool _shouldClearOnNextInput = false; // 下次输入时是否应该清空输入框
        
        // 导航相关属性
        public event EventHandler<NavigationEventArgs> NavigationRequested;
        public event EventHandler<ExecutionEventArgs> AutoExecutionRequested;
        
        // 配置相关属性
        public string StepName { get; set; } = "";
        private SmartInputParameterConfiguration _currentConfiguration;
        private double _currentStepSize = 1.0; // 当前步长
        
        // 防抖相关属性
        private DispatcherTimer _debounceTimer;
        private bool _isSliderChanging = false;
        private const int DEBOUNCE_DELAY_MS = 300; // 防抖延迟300毫秒
        #endregion

        #region 构造函数
        public SmartInputCard()
        {
            try
            {
                InitializeComponent();
                DataContext = this;
                
                // 立即初始化默认步长
                _currentStepSize = 1.0;
                
                // 延迟初始化，确保窗口和所有UI元素都已加载
                Loaded += (s, e) =>
                {
                    InitializeCard();
                    InitializeDebounceTimer();
                };
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化智能输入卡片失败: {ex.Message}");
                // 确保关键对象不为null
                if (_debounceTimer == null)
                {
                    _debounceTimer = new DispatcherTimer();
                    _debounceTimer.Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS);
                    _debounceTimer.Tick += DebounceTimer_Tick;
                }
            }
        }
        #endregion

        #region 初始化方法
        private void InitializeCard()
        {
            // 先设置滑动条的初始范围
            if (ValueSlider != null)
            {
                ValueSlider.Minimum = MinValue;
                ValueSlider.Maximum = MaxValue;
            }
            
            // 设置初始值
            InputValue = CurrentValue;
            
            // 加载配置
            LoadParameterConfiguration();
            
            // 确保输入框和滑动条显示正确的值
            ResetInput();
        }

        private void InitializeDebounceTimer()
        {
            try
            {
                _debounceTimer = new DispatcherTimer();
                _debounceTimer.Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS);
                _debounceTimer.Tick += DebounceTimer_Tick;
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化防抖计时器失败: {ex.Message}");
                _debounceTimer = null;
            }
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                _debounceTimer?.Stop();
                _isSliderChanging = false;
                
                // 执行防抖后的自动执行
                TriggerAutoExecution();
            }
            catch (Exception ex)
            {
                LogManager.Error($"防抖计时器回调失败: {ex.Message}");
                _isSliderChanging = false; // 确保状态重置
            }
        }

        private void LoadParameterConfiguration()
        {
            try
            {
                // 使用新的配置系统
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SmartInputConfigs.json");
                
                if (File.Exists(configPath) && !string.IsNullOrEmpty(StepName) && !string.IsNullOrEmpty(ParameterName))
                {
                    var parameterKey = $"{StepName}_{ParameterName}";
                    
                    var json = File.ReadAllText(configPath);
                    var configs = JsonSerializer.Deserialize<Dictionary<string, SmartInputParameterConfiguration>>(json);
                    
                    if (configs != null && configs.ContainsKey(parameterKey))
                    {
                        _currentConfiguration = configs[parameterKey];
                    }
                    else
                    {
                        _currentConfiguration = new SmartInputParameterConfiguration();
                    }
                    // 总是应用配置到UI，无论是从文件加载的还是默认的
                    ApplyNewParameterConfig(_currentConfiguration);
                }
                else
                {
                    _currentConfiguration = new SmartInputParameterConfiguration();
                    // 没有配置文件时也要应用默认配置
                    ApplyNewParameterConfig(_currentConfiguration);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载参数配置失败: {ex.Message}");
                _currentConfiguration = new SmartInputParameterConfiguration();
            }
        }

        private void ApplyParameterConfig(ParameterDisplayConfig config)
        {
            if (!string.IsNullOrEmpty(config.Title))
                Title = config.Title;

            if (!string.IsNullOrEmpty(config.Description) && DescriptionTextBlock != null)
                DescriptionTextBlock.Text = config.Description;

            if (!string.IsNullOrEmpty(config.ImagePath))
                LoadDescriptionImage(config.ImagePath);

            if (!string.IsNullOrEmpty(config.Unit))
                Unit = config.Unit;

            if (config.MinValue.HasValue)
                MinValue = config.MinValue.Value;

            if (config.MaxValue.HasValue)
                MaxValue = config.MaxValue.Value;
        }

        private void ApplyNewParameterConfig(SmartInputParameterConfiguration config)
        {
            if (config == null) return;

            try
            {
                // 设置单位（包括空字符串的情况）
                Unit = config.Unit ?? "";
                
                if (UnitTextBlock != null)
                {
                    var unit = config.Unit ?? "";
                    
                    if (string.IsNullOrEmpty(unit))
                    {
                        UnitTextBlock.Text = "";
                        UnitTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        UnitTextBlock.Text = unit;
                        UnitTextBlock.Visibility = Visibility.Visible;
                    }
                }

                // 设置范围
                if (config.MinValue.HasValue)
                    MinValue = config.MinValue.Value;

                if (config.MaxValue.HasValue)
                    MaxValue = config.MaxValue.Value;

                // 设置描述
                if (!string.IsNullOrEmpty(config.Description) && DescriptionTextBlock != null)
                    DescriptionTextBlock.Text = config.Description;

                // 加载图片
                if (!string.IsNullOrEmpty(config.ImagePath))
                    LoadDescriptionImage(config.ImagePath);

                // 更新滑动条精度
                UpdateSliderPrecision(config.StepSize);
                
                // 更新滑动条范围和值
                UpdateSliderRange();
                UpdateSliderValue();
                
                // 强制更新当前值显示
                UpdateCurrentValueDisplay();
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用参数配置失败: {ex.Message}");
            }
        }

        private void UpdateSliderPrecision(double stepSize)
        {
            // 保存当前步长
            _currentStepSize = stepSize;
            
            if (ValueSlider != null)
            {
                // 根据步长设置滑动条的步长
                ValueSlider.TickFrequency = stepSize;
                ValueSlider.SmallChange = stepSize;
                ValueSlider.LargeChange = stepSize * 10;
                ValueSlider.IsSnapToTickEnabled = true; // 确保启用对齐到刻度
                
                // 重新设置当前值以确保精度一致性
                var currentValue = InputValue;
                InputValue = currentValue; // 触发setter中的精度舍入
                
                // 更新输入显示的格式
                if (InputTextBox != null)
                {
                    if (double.TryParse(_currentInput, out double value))
                    {
                        // 使用新的格式化方法
                        var preciseValue = RoundToStepPrecision(value);
                        InputTextBox.Text = FormatValue(preciseValue);
                        _currentInput = InputTextBox.Text;
                    }
                }
            }
        }

        /// <summary>
        /// 根据步长获取显示格式字符串
        /// </summary>
        private string GetFormatString(double stepSize)
        {
            if (stepSize >= 1)
            {
                // 步长大于等于1，显示整数
                return "F0";
            }
            else if (stepSize >= 0.1)
            {
                // 步长0.1，显示1位小数
                return "F1";
            }
            else if (stepSize >= 0.01)
            {
                // 步长0.01，显示2位小数
                return "F2";
            }
            else
            {
                // 步长小于0.01（包括0.001），显示3位小数
                return "F3";
            }
        }

        private void LoadDescriptionImage(string imagePath)
        {
            try
            {
                var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ParameterImages", imagePath);
                if (File.Exists(fullPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    DescriptionImage.Source = bitmap;
                }
                else
                {
                    // 如果图片不存在，显示默认图片或隐藏图片区域
                    DescriptionImage.Source = null;
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"加载参数说明图片失败: {ex.Message}");
                DescriptionImage.Source = null;
            }
        }
        #endregion

        #region 依赖属性回调
        private static void OnCurrentValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartInputCard card)
            {
                card.InputValue = (double)e.NewValue;
                card.UpdateCurrentValueDisplay();
            }
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartInputCard card)
            {
                card.UpdateSliderRange();
            }
        }

        private static void OnUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartInputCard card && card.UnitTextBlock != null)
            {
                var unit = e.NewValue?.ToString() ?? "";
                if (string.IsNullOrEmpty(unit))
                {
                    card.UnitTextBlock.Text = "";
                    card.UnitTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    card.UnitTextBlock.Text = unit;
                    card.UnitTextBlock.Visibility = Visibility.Visible;
                }
            }
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartInputCard card && card.TitleTextBlock != null)
            {
                card.TitleTextBlock.Text = e.NewValue?.ToString() ?? "参数设置";
            }
        }
        #endregion

        #region UI更新方法
        private void UpdateCurrentValueDisplay()
        {
            if (CurrentValueTextBlock != null)
            {
                CurrentValueTextBlock.Text = FormatValue(CurrentValue);
            }
        }

        private void UpdateInputDisplay()
        {
            if (InputTextBox != null)
            {
                InputTextBox.Text = _currentInput;
            }
        }

        private void UpdateSliderRange()
        {
            if (ValueSlider != null && MinValueText != null && MaxValueText != null)
            {
                ValueSlider.Minimum = MinValue;
                ValueSlider.Maximum = MaxValue;
                MinValueText.Text = FormatValue(MinValue);
                MaxValueText.Text = FormatValue(MaxValue);
            }
        }

        private void UpdateSliderValue()
        {
            if (ValueSlider != null)
            {
                // 确保输入值在有效范围内
                var clampedValue = Math.Max(MinValue, Math.Min(MaxValue, InputValue));
                
                // 避免在滑条改变过程中循环更新
                if (!_isSliderChanging)
                {
                    // 先暂时禁用事件处理，避免循环
                    ValueSlider.ValueChanged -= ValueSlider_ValueChanged;
                    try
                    {
                        ValueSlider.Value = clampedValue;
                    }
                    finally
                    {
                        ValueSlider.ValueChanged += ValueSlider_ValueChanged;
                    }
                }
            }
        }
        #endregion

        #region 数字键盘事件处理
        private void NumberButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var digit = button.Content.ToString();
                
                // 如果标记了需要清空，则清空输入框后开始输入
                if (_shouldClearOnNextInput)
                {
                    _currentInput = digit == "0" ? "0" : digit;
                    _hasDecimalPoint = false;
                    _isNegative = false;
                    _shouldClearOnNextInput = false;
                }
                else
                {
                    // 原有逻辑
                    if (_currentInput == "0" && digit != "0")
                    {
                        _currentInput = digit;
                    }
                    else if (_currentInput != "0")
                    {
                        _currentInput += digit;
                    }
                }

                UpdateDisplayValue();
            }
        }

        private void DecimalButton_Click(object sender, RoutedEventArgs e)
        {
            // 如果标记了需要清空，则清空输入框后开始输入
            if (_shouldClearOnNextInput)
            {
                _currentInput = "0.";
                _hasDecimalPoint = true;
                _isNegative = false;
                _shouldClearOnNextInput = false;
                UpdateInputDisplay();
            }
            else if (!_hasDecimalPoint)
            {
                _currentInput += ".";
                _hasDecimalPoint = true;
                UpdateInputDisplay();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _currentInput = "0";
            _hasDecimalPoint = false;
            _isNegative = false;
            UpdateDisplayValue();
        }

        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentInput.Length > 1)
            {
                var lastChar = _currentInput[_currentInput.Length - 1];
                if (lastChar == '.')
                {
                    _hasDecimalPoint = false;
                }
                _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
            }
            else
            {
                _currentInput = "0";
                _hasDecimalPoint = false;
            }

            if (_currentInput == "-")
            {
                _currentInput = "0";
                _isNegative = false;
            }

            UpdateDisplayValue();
        }

        private void SignButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentInput == "0")
            {
                return;
            }

            if (_isNegative)
            {
                _currentInput = _currentInput.Substring(1); // 移除负号
                _isNegative = false;
            }
            else
            {
                _currentInput = "-" + _currentInput;
                _isNegative = true;
            }

            UpdateDisplayValue();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyInput();
            // 手动确认时触发执行
            TriggerManualExecution();
            // 确认后，下次输入时清空输入框
            _shouldClearOnNextInput = true;
        }

        private void UpdateDisplayValue()
        {
            if (double.TryParse(_currentInput, out double value))
            {
                // 仅更新内部值，不触发精度舍入（让用户完成输入）
                _inputValue = value;
                OnPropertyChanged(nameof(InputValue));
                // 不调用 UpdateSliderValue() 避免在输入过程中干扰
            }
            UpdateInputDisplay();
        }

        private void ApplyInput()
        {
            if (double.TryParse(_currentInput, out double value))
            {
                // 根据步长精度处理输入值
                var preciseValue = RoundToStepPrecision(value);
                
                // 检查范围
                if (preciseValue < MinValue || preciseValue > MaxValue)
                {
                    MessageBox.Show($"输入值必须在 {MinValue} 到 {MaxValue} 之间", "输入错误", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CurrentValue = preciseValue;
                OnValueChanged(preciseValue); // 使用精确值
            }
        }
        #endregion

        #region 滑动条事件处理
        private void ValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                // 直接使用滑动条的值进行精度处理，不做额外的边界值检测
                var preciseNewValue = RoundToStepPrecision(e.NewValue);
                var preciseCurrentValue = RoundToStepPrecision(InputValue);
                
                if (Math.Abs(preciseNewValue - preciseCurrentValue) > 0.0001) // 避免循环更新
                {
                    _isSliderChanging = true;
                    
                    // 直接更新值
                    _inputValue = preciseNewValue; // 直接设置，避免触发setter
                    _currentInput = FormatValue(preciseNewValue);
                    _hasDecimalPoint = _currentInput.Contains(".");
                    _isNegative = preciseNewValue < 0;
                    _shouldClearOnNextInput = true; // 滑动条改变后，下次输入时清空
                    
                    // 更新UI显示
                    OnPropertyChanged(nameof(InputValue));
                    UpdateInputDisplay();
                    
                    // 重启防抖计时器
                    if (_debounceTimer != null)
                    {
                        _debounceTimer.Stop();
                        _debounceTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"滑条值变化处理失败: {ex.Message}");
                _isSliderChanging = false; // 确保状态重置
            }
        }
        #endregion

        #region 操作按钮事件处理
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseCard();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 触发窗口拖动事件
            WindowDragRequested?.Invoke(this, e);
        }
        
        // 添加窗口拖动事件
        public event EventHandler<MouseButtonEventArgs> WindowDragRequested;

        private void ConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(StepName) || string.IsNullOrEmpty(ParameterName))
                {
                    MessageBox.Show("参数信息不完整，无法打开配置", "配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var configWindow = new SmartInputParameterConfigWindow(StepName, ParameterName);
                configWindow.Owner = Window.GetWindow(this);
                
                if (configWindow.ShowDialog() == true)
                {
                    // 重新加载配置
                    LoadParameterConfiguration();
                    
                    // 强制刷新UI显示
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 强制触发Unit属性更新
                        var currentUnit = Unit;
                        Unit = "";
                        Unit = currentUnit;
                    }), DispatcherPriority.Background);
                    
                    LogManager.Info($"参数配置已更新: {StepName}_{ParameterName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开配置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.Error($"打开配置窗口失败: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseCard();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyInput();
            // 手动确认时触发执行
            TriggerManualExecution();
            // 应用后，下次输入时清空输入框
            _shouldClearOnNextInput = true;
            CloseCard();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, new NavigationEventArgs(NavigationDirection.Previous, ParameterName));
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, new NavigationEventArgs(NavigationDirection.Next, ParameterName));
        }

        private void CloseCard()
        {
            // 停止防抖计时器
            _debounceTimer?.Stop();
            CardClosed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 手动触发执行（确认按钮点击时）
        /// </summary>
        private void TriggerManualExecution()
        {
            var preciseValue = GetPreciseValue();
            AutoExecutionRequested?.Invoke(this, new ExecutionEventArgs(ParameterName, preciseValue));
        }

        /// <summary>
        /// 自动触发执行（滑条防抖后）
        /// </summary>
        private void TriggerAutoExecution()
        {
            try
            {
                // 延迟触发，避免频繁执行
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    AutoExecutionRequested?.Invoke(this, new ExecutionEventArgs(ParameterName, GetPreciseValue()));
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LogManager.Error($"触发自动执行失败: {ex.Message}");
            }
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 设置参数配置
        /// </summary>
        public void SetParameterConfig(string parameterName, string title = null, string description = null, 
                                     string imagePath = null, string unit = null, double? minValue = null, double? maxValue = null)
        {
            ParameterName = parameterName;
            
            if (!string.IsNullOrEmpty(title))
                Title = title;
            
            if (!string.IsNullOrEmpty(description) && DescriptionTextBlock != null)
                DescriptionTextBlock.Text = description;
            
            if (!string.IsNullOrEmpty(imagePath))
                LoadDescriptionImage(imagePath);
            
            // 设置单位（包括空字符串的情况）
            if (unit != null)
            {
                Unit = unit;
                if (UnitTextBlock != null)
                {
                    if (string.IsNullOrEmpty(unit))
                    {
                        UnitTextBlock.Text = "";
                        UnitTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        UnitTextBlock.Text = unit;
                        UnitTextBlock.Visibility = Visibility.Visible;
                    }
                }
            }
            
            if (minValue.HasValue)
                MinValue = minValue.Value;
            
            if (maxValue.HasValue)
                MaxValue = maxValue.Value;
                
            // 设置完配置后，重新加载配置以确保同步
            LoadParameterConfiguration();
        }

        /// <summary>
        /// 强制同步滑动条位置到当前值
        /// </summary>
        public void ForceSliderSync()
        {
            if (ValueSlider != null)
            {
                // 暂时移除事件处理器，避免循环
                ValueSlider.ValueChanged -= ValueSlider_ValueChanged;
                
                try
                {
                    // 确保滑动条的范围是正确的
                    ValueSlider.Minimum = MinValue;
                    ValueSlider.Maximum = MaxValue;
                    ValueSlider.TickFrequency = _currentStepSize;
                    
                    // 设置滑动条的值为当前输入值
                    var targetValue = RoundToStepPrecision(InputValue);
                    ValueSlider.Value = targetValue;
                    
                    LogManager.Info($"ForceSliderSync: Min={MinValue}, Max={MaxValue}, Target={targetValue}, Actual={ValueSlider.Value}");
                }
                finally
                {
                    // 重新附加事件处理器
                    ValueSlider.ValueChanged += ValueSlider_ValueChanged;
                }
            }
        }
        
        /// <summary>
        /// 重置输入值
        /// </summary>
        public void ResetInput()
        {
            // 确保CurrentValue也使用正确的精度
            var preciseCurrentValue = RoundToStepPrecision(CurrentValue);
            CurrentValue = preciseCurrentValue; // 更新CurrentValue的精度
            InputValue = preciseCurrentValue;
            _currentInput = FormatValue(preciseCurrentValue);
            _hasDecimalPoint = _currentInput.Contains(".");
            _isNegative = preciseCurrentValue < 0;
            _shouldClearOnNextInput = true; // 卡片打开后，标记下次输入需要清空
            
            // 确保UI显示正确的值
            UpdateInputDisplay();
            // 确保滑动条位置与当前值同步
            UpdateSliderValue();
        }
        #endregion

        #region 事件触发
        protected virtual void OnValueChanged(double newValue)
        {
            ValueChanged?.Invoke(this, new ValueChangedEventArgs(ParameterName, newValue));
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 根据步长精度对数值进行舍入
        /// </summary>
        private double RoundToStepPrecision(double value)
        {
            // 使用更宽松的边界值检测
            var tolerance = _currentStepSize * 0.5; // 使用步长的一半作为容差
            
            // 检查是否接近边界值
            if (Math.Abs(value - MinValue) <= tolerance)
                return MinValue;
            if (Math.Abs(value - MaxValue) <= tolerance)
                return MaxValue;
            
            // 按步长对齐，使用更精确的计算
            var steps = Math.Round((value - MinValue) / _currentStepSize, MidpointRounding.AwayFromZero);
            var alignedValue = MinValue + steps * _currentStepSize;
            
            // 确保不超出范围
            alignedValue = Math.Max(MinValue, Math.Min(MaxValue, alignedValue));
            
            // 再次检查边界值（防止浮点误差）
            if (Math.Abs(alignedValue - MinValue) < _currentStepSize * 0.01)
                return MinValue;
            if (Math.Abs(alignedValue - MaxValue) < _currentStepSize * 0.01)
                return MaxValue;
            
            // 根据步长精度进行最终舍入
            var decimalPlaces = GetDecimalPlaces(_currentStepSize);
            return Math.Round(alignedValue, decimalPlaces);
        }
        
        /// <summary>
        /// 根据步长获取小数位数
        /// </summary>
        private int GetDecimalPlaces(double stepSize)
        {
            if (stepSize >= 1)
                return 0;
            else if (stepSize >= 0.1)
                return 1;
            else if (stepSize >= 0.01)
                return 2;
            else if (stepSize >= 0.001)
                return 3;
            else
                return 4;
        }

        /// <summary>
        /// 获取格式化的数值字符串，确保整数不显示小数点
        /// </summary>
        private string FormatValue(double value)
        {
            var decimalPlaces = GetDecimalPlaces(_currentStepSize);
            var formatString = $"F{decimalPlaces}";
            var formatted = value.ToString(formatString);
            
            // 只有当步长大于等于1时，整数才不显示小数点
            if (decimalPlaces == 0 || (value == Math.Round(value) && _currentStepSize >= 1))
            {
                return Math.Round(value).ToString("F0");
            }
            
            return formatted;
        }

        /// <summary>
        /// 获取精确的数值，避免浮点数精度问题
        /// </summary>
        public double GetPreciseValue()
        {
            // 使用相同的精度逻辑
            return RoundToStepPrecision(InputValue);
        }

        /// <summary>
        /// 调试用：获取当前步长信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"StepSize: {_currentStepSize}, InputValue: {InputValue}, PreciseValue: {GetPreciseValue()}, CurrentInput: {_currentInput}";
        }


        #endregion

        #region 资源清理
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
        }

        public void Dispose()
        {
            try
            {
                _debounceTimer?.Stop();
                if (_debounceTimer != null)
                {
                    _debounceTimer.Tick -= DebounceTimer_Tick;
                    _debounceTimer = null;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"清理防抖计时器失败: {ex.Message}");
            }
        }
        #endregion
    }

    #region 辅助类和事件参数
    /// <summary>
    /// 值变更事件参数
    /// </summary>
    public class ValueChangedEventArgs : EventArgs
    {
        public string ParameterName { get; }
        public double NewValue { get; }

        public ValueChangedEventArgs(string parameterName, double newValue)
        {
            ParameterName = parameterName;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// 参数显示配置
    /// </summary>
    public class ParameterDisplayConfig
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
        public string Unit { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }

    /// <summary>
    /// 导航事件参数
    /// </summary>
    public class NavigationEventArgs : EventArgs
    {
        public NavigationDirection Direction { get; }
        public string CurrentParameterName { get; }

        public NavigationEventArgs(NavigationDirection direction, string currentParameterName)
        {
            Direction = direction;
            CurrentParameterName = currentParameterName;
        }
    }

    /// <summary>
    /// 执行事件参数
    /// </summary>
    public class ExecutionEventArgs : EventArgs
    {
        public string ParameterName { get; }
        public double NewValue { get; }

        public ExecutionEventArgs(string parameterName, double newValue)
        {
            ParameterName = parameterName;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// 导航方向
    /// </summary>
    public enum NavigationDirection
    {
        Previous,
        Next
    }
    #endregion
} 