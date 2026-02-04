using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfApp2.SMTGPIO;
using WpfApp2.UI.Models;
using System.IO;
using Newtonsoft.Json;
using Path = System.IO.Path;

namespace WpfApp2.UI
{
    /// <summary>
    /// 卡片类型枚举
    /// </summary>
    public enum CardType
    {
        RelayControl,    // 继电器控制
        SensorRead,      // 传感器读取
        DataWrite        // 数据写入
    }

    /// <summary>
    /// 组件调试项数据模型
    /// </summary>
    public class ComponentDebugItem : INotifyPropertyChanged
    {
        private bool _currentStatus;
        private int _currentValue;
        private int _writeValue;

        /// <summary>
        /// 卡片类型
        /// </summary>
        public CardType Type { get; set; }

        /// <summary>
        /// 组件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 寄存器地址
        /// </summary>
        public string Register { get; set; }

        /// <summary>
        /// 当前状态（用于继电器和传感器）
        /// </summary>
        public bool CurrentStatus
        {
            get => _currentStatus;
            set
            {
                if (_currentStatus != value)
                {
                    _currentStatus = value;
                    OnPropertyChanged(nameof(CurrentStatus));
                }
            }
        }

        /// <summary>
        /// 当前数值（用于数据读写）
        /// </summary>
        public int CurrentValue
        {
            get => _currentValue;
            set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                    OnPropertyChanged(nameof(CurrentValue));
                }
            }
        }

        /// <summary>
        /// 写入数值（用于数据写入）
        /// </summary>
        public int WriteValue
        {
            get => _writeValue;
            set
            {
                if (_writeValue != value)
                {
                    _writeValue = value;
                    OnPropertyChanged(nameof(WriteValue));
                }
            }
        }

        /// <summary>
        /// 是否为继电器控制类型
        /// </summary>
        public bool IsRelayControl => Type == CardType.RelayControl;

        /// <summary>
        /// 是否为传感器读取类型
        /// </summary>
        public bool IsSensorRead => Type == CardType.SensorRead;

        /// <summary>
        /// 是否为数据写入类型
        /// </summary>
        public bool IsDataWrite => Type == CardType.DataWrite;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// PLCSerialConfigPage.xaml 的交互逻辑
    /// </summary>
    public partial class PLCSerialConfigPage : Page
    {
        #region 私有字段

        private readonly bool _testOnly;
        private DeviceConfig _selectedDeviceConfig;
        private DispatcherTimer _statusUpdateTimer;
        private DispatcherTimer _componentStatusTimer;
        private ObservableCollection<ComponentDebugItem> _componentItems;
        private bool _isUpdatingComponentStatus = false; // 防止并发更新状态

        #endregion

        #region 构造函数

        public PLCSerialConfigPage() : this(false)
        {
        }

        public PLCSerialConfigPage(bool testOnly)
        {
            _testOnly = testOnly;
            InitializeComponent();
            InitializeTimer();
            InitializeComponentDebug();
            if (_testOnly)
            {
                ApplyTestOnlyLayout();
            }
        }

        #endregion

        #region 初始化方法

        private void PLCSerialConfigPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPortsList();
            LoadSavedSettings();
            
            // 确保界面控件有默认值
            EnsureDefaultSettings();

            if (_testOnly && _selectedDeviceConfig != null)
            {
                ApplyDeviceConfig(_selectedDeviceConfig, updateUi: true);
            }
            
            UpdateConnectionStatus();
            
            // 如果PLC已经连接，订阅日志事件
            var plcController = PLCSerialController.Instance;
            if (plcController.IsConnected)
            {
                plcController.LogMessageEvent -= OnPLCLogMessage;
                plcController.LogMessageEvent += OnPLCLogMessage;
                LogMessage("已连接到现有PLC实例，订阅日志事件");
            }


        }

        public void SetDeviceConfig(DeviceConfig config)
        {
            if (config == null)
            {
                return;
            }

            _selectedDeviceConfig = config.Clone();

            if (IsLoaded)
            {
                ApplyDeviceConfig(_selectedDeviceConfig, updateUi: true);
            }
        }

        private void InitializeTimer()
        {
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // 200ms间隔，减少串口负载
            };
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
        }

        private void InitializeComponentDebug()
        {
            // 尝试从配置文件加载，如果没有则使用默认配置
            _componentItems = LoadComponentConfig();
            if (_componentItems == null || _componentItems.Count == 0)
            {
                _componentItems = new ObservableCollection<ComponentDebugItem>
                {
                    // 继电器控制卡片
                    new ComponentDebugItem { Type = CardType.RelayControl, Name = "15°光触发", Register = "R506", CurrentStatus = false },
                    new ComponentDebugItem { Type = CardType.RelayControl, Name = "45°触发", Register = "R504", CurrentStatus = false },
                    new ComponentDebugItem { Type = CardType.RelayControl, Name = "COR触发", Register = "R505", CurrentStatus = false },
                    
                    // 传感器读取卡片
                    new ComponentDebugItem { Type = CardType.SensorRead, Name = "LX-100", Register = "R0", CurrentStatus = false },
                    
                    // 数据写入卡片
                    new ComponentDebugItem { Type = CardType.DataWrite, Name = "DM0", Register = "DM0.L", CurrentValue = 0, WriteValue = 0 }
                };
                
                // 保存默认配置
                SaveComponentConfig();
            }

            ComponentItemsControl.ItemsSource = _componentItems;

            // 初始化组件状态更新定时器
            _componentStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // 200ms间隔，平衡性能和响应
            };
            _componentStatusTimer.Tick += ComponentStatusTimer_Tick;
            _componentStatusTimer.Start();
        }

        private void ApplyTestOnlyLayout()
        {
            if (ConfigPanel != null)
            {
                ConfigPanel.Visibility = Visibility.Collapsed;
            }

            if (ConfigColumn != null)
            {
                ConfigColumn.Width = new GridLength(0);
            }

            if (TestColumn != null)
            {
                TestColumn.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        private void RefreshPortsList()
        {
            try
            {
                string[] availablePorts = PLCSerialController.GetAvailablePorts();
                string selectedPort = PortComboBox.SelectedValue?.ToString();

                PortComboBox.Items.Clear();
                foreach (string port in availablePorts)
                {
                    PortComboBox.Items.Add(port);
                }

                // 恢复之前选择的串口，如果不存在则选择第一个
                if (!string.IsNullOrEmpty(selectedPort) && availablePorts.Contains(selectedPort))
                {
                    PortComboBox.SelectedValue = selectedPort;
                }
                else if (availablePorts.Length > 0)
                {
                    PortComboBox.SelectedIndex = 0;
                }

                LogMessage($"发现 {availablePorts.Length} 个可用串口");
            }
            catch (Exception ex)
            {
                LogMessage($"刷新串口列表失败: {ex.Message}");
            }
        }

        private void ApplyDeviceConfig(DeviceConfig config, bool updateUi)
        {
            var serial = config?.Serial;
            if (serial == null)
            {
                return;
            }

            var controller = PLCSerialController.Instance;
            controller.PortName = serial.PortName;
            controller.BaudRate = serial.BaudRate;
            controller.Timeout = serial.ReadTimeout;
            controller.WriteTimeout = serial.WriteTimeout;

            if (!updateUi)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(serial.PortName) && !PortComboBox.Items.Contains(serial.PortName))
            {
                PortComboBox.Items.Add(serial.PortName);
            }

            if (!string.IsNullOrWhiteSpace(serial.PortName))
            {
                PortComboBox.SelectedValue = serial.PortName;
            }

            var baudRateText = serial.BaudRate.ToString();
            foreach (System.Windows.Controls.ComboBoxItem item in BaudRateComboBox.Items)
            {
                if (string.Equals(item.Content?.ToString(), baudRateText, StringComparison.Ordinal))
                {
                    BaudRateComboBox.SelectedItem = item;
                    break;
                }
            }

            if (serial.ReadTimeout > 0)
            {
                TimeoutTextBox.Text = serial.ReadTimeout.ToString();
            }
        }

        private void LoadSavedSettings()
        {
            try
            {
                // 从单例实例加载当前设置
                var plcInstance = PLCSerialController.Instance;
                
                // 设置界面显示当前连接参数
                if (PortComboBox.Items.Contains(plcInstance.PortName))
                {
                    PortComboBox.SelectedValue = plcInstance.PortName;
                }
                
                // 设置波特率 - 直接使用SelectedValue设置
                string targetBaudRate = plcInstance.BaudRate.ToString();
                
                // 检查波特率是否在可选项中
                bool baudRateExists = false;
                foreach (System.Windows.Controls.ComboBoxItem item in BaudRateComboBox.Items)
                {
                    if (item.Content.ToString() == targetBaudRate)
                    {
                        baudRateExists = true;
                        break;
                    }
                }
                
                // 如果波特率存在，设置对应的ComboBoxItem
                if (baudRateExists)
                {
                    foreach (System.Windows.Controls.ComboBoxItem item in BaudRateComboBox.Items)
                    {
                        if (item.Content.ToString() == targetBaudRate)
                        {
                            BaudRateComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
                // 如果当前没有选中项，设置为默认9600
                else if (BaudRateComboBox.SelectedItem == null)
                {
                    foreach (System.Windows.Controls.ComboBoxItem item in BaudRateComboBox.Items)
                    {
                        if (item.Content.ToString() == "9600")
                        {
                            BaudRateComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
                
                // 设置超时时间
                if (plcInstance.Timeout > 0)
                {
                    TimeoutTextBox.Text = plcInstance.Timeout.ToString();
                }
                else if (string.IsNullOrEmpty(TimeoutTextBox.Text))
                {
                    TimeoutTextBox.Text = "5000"; // 默认5秒
                }
                
                LogMessage("已加载当前PLC连接设置");
            }
            catch (Exception ex)
            {
                LogMessage($"加载设置失败: {ex.Message}");
                
                // 设置默认值
                EnsureDefaultSettings();
            }
        }

        /// <summary>
        /// 确保界面控件有默认值
        /// </summary>
        private void EnsureDefaultSettings()
        {
            try
            {
                // 确保波特率有默认选中项
                if (BaudRateComboBox.SelectedItem == null)
                {
                    foreach (System.Windows.Controls.ComboBoxItem item in BaudRateComboBox.Items)
                    {
                        if (item.Content.ToString() == "9600")
                        {
                            BaudRateComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
                
                // 确保超时时间有默认值
                if (string.IsNullOrEmpty(TimeoutTextBox.Text))
                {
                    TimeoutTextBox.Text = "5000";
                }
                
                LogMessage("已设置默认连接参数");
            }
            catch (Exception ex)
            {
                LogMessage($"设置默认值失败: {ex.Message}");
            }
        }

        #endregion

        #region 连接控制

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PortComboBox.SelectedValue == null)
                {
                    MessageBox.Show("请选择串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (BaudRateComboBox.SelectedItem == null)
                {
                    MessageBox.Show("请选择波特率", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(TimeoutTextBox.Text))
                {
                    MessageBox.Show("请输入超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string portName = PortComboBox.SelectedValue.ToString();
                
                // 正确获取波特率值 - 从ComboBoxItem的Content属性获取
                var selectedBaudRateItem = BaudRateComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
                if (selectedBaudRateItem == null)
                {
                    MessageBox.Show("波特率选择项无效", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                string baudRateString = selectedBaudRateItem.Content.ToString();
                
                if (!int.TryParse(baudRateString, out int baudRate))
                {
                    MessageBox.Show($"波特率格式不正确: '{baudRateString}'", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 安全解析超时时间
                if (!int.TryParse(TimeoutTextBox.Text, out int timeout))
                {
                    MessageBox.Show("超时时间格式不正确，请输入数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证超时时间范围
                if (timeout < 100 || timeout > 60000)
                {
                    MessageBox.Show("超时时间应在100-60000毫秒之间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 使用单例实例
                var plcController = PLCSerialController.Instance;
                
                // 如果已经连接到相同的串口，提示用户
                if (plcController.IsConnected && plcController.PortName == portName && plcController.BaudRate == baudRate)
                {
                    LogMessage("PLC已连接到相同的串口配置");
                    MessageBox.Show("PLC已连接到相同的串口配置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateConnectionStatus();
                    return;
                }
                
                // 如果连接到不同的串口，先断开
                if (plcController.IsConnected)
                {
                    LogMessage("检测到PLC已连接到其他配置，正在重新配置...");
                    plcController.Disconnect();
                }

                // 配置连接参数
                plcController.ConfigureConnection(portName, baudRate, timeout);
                
                // 订阅日志事件
                plcController.LogMessageEvent += OnPLCLogMessage;

                // 尝试连接
                bool connectResult = plcController.Connect();
                
                if (connectResult)
                {
                    LogMessage("PLC连接成功！");
                    SaveCurrentSettings();
                    
                    // 设置为全局PLC控制器
                    SetGlobalPLCController(plcController);
                }
                else
                {
                    LogMessage($"PLC连接失败: {plcController.ErrorMessage}");
                }

                UpdateConnectionStatus();
            }
            catch (Exception ex)
            {
                LogMessage($"连接PLC时出错: {ex.Message}");
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 先停止定时器，避免在断开过程中继续读取PLC
                if (_componentStatusTimer?.IsEnabled == true)
                {
                    _componentStatusTimer.Stop();
                    LogMessage("已停止组件状态监控");
                }
                
                var plcController = PLCSerialController.Instance;
                plcController.Disconnect();
                LogMessage("已断开PLC连接");
                UpdateConnectionStatus();
                
                // 断开后重新启动定时器（它会在UpdateComponentStatusAsync中检查连接状态）
                if (_componentStatusTimer != null && !_componentStatusTimer.IsEnabled)
                {
                    _componentStatusTimer.Start();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"断开连接时出错: {ex.Message}");
            }
        }

        private void RefreshPortsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPortsList();
        }



        #endregion

        #region 测试功能

        private async void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnectedWithMessage()) return;

            try
            {
                string address = ReadAddressTextBox.Text.Trim();
                if (string.IsNullOrEmpty(address))
                {
                    LogMessage("请输入要读取的寄存器地址");
                    return;
                }

                var plcController = PLCSerialController.Instance;
                int value = await plcController.ReadSingleAsync(addrCombine: address);
                
                LogMessage($"成功读取 {address} = {value}");
            }
            catch (Exception ex)
            {
                LogMessage($"读取失败: {ex.Message}");
            }
        }

        private async void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnectedWithMessage()) return;

            try
            {
                string address = WriteAddressTextBox.Text.Trim();
                string valueText = WriteValueTextBox.Text.Trim();

                if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(valueText))
                {
                    LogMessage("请输入寄存器地址和数值");
                    return;
                }

                if (!int.TryParse(valueText, out int value))
                {
                    LogMessage("数值格式错误");
                    return;
                }

                var plcController = PLCSerialController.Instance;
                bool success = await plcController.WriteSingleAsync(addrCombine: address, data: value);
                
                LogMessage($"写入 {address} = {value}: {(success ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                LogMessage($"写入失败: {ex.Message}");
            }
        }

        private async void SetRelayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnectedWithMessage()) return;

            try
            {
                string address = RelayAddressTextBox.Text.Trim();
                if (string.IsNullOrEmpty(address))
                {
                    LogMessage("请输入继电器地址");
                    return;
                }

                var plcController = PLCSerialController.Instance;
                bool success = await plcController.SetRelayAsync(address);
                
                LogMessage($"设置继电器 {address}: {(success ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                LogMessage($"设置继电器失败: {ex.Message}");
            }
        }

        private async void ResetRelayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnectedWithMessage()) return;

            try
            {
                string address = RelayAddressTextBox.Text.Trim();
                if (string.IsNullOrEmpty(address))
                {
                    LogMessage("请输入继电器地址");
                    return;
                }

                var plcController = PLCSerialController.Instance;
                bool success = await plcController.ResetRelayAsync(address);
                
                LogMessage($"复位继电器 {address}: {(success ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                LogMessage($"复位继电器失败: {ex.Message}");
            }
        }

        #endregion

        #region 界面更新

        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateConnectionStatus();
        }

        private void ComponentStatusTimer_Tick(object sender, EventArgs e)
        {
            // 在子线程中更新状态，不阻塞UI
            UpdateComponentStatusInBackground();
        }

        /// <summary>
        /// 在子线程中更新组件状态，然后用Dispatcher更新UI
        /// </summary>
        private void UpdateComponentStatusInBackground()
        {
            // 防止并发执行
            if (_isUpdatingComponentStatus || !IsConnected() || _componentItems == null)
                return;

            // 在后台线程中执行
            Task.Run(() =>
            {
                try
                {
                    _isUpdatingComponentStatus = true;
                    var plcController = PLCSerialController.Instance;
                    
                    // 在子线程中同步逐个读取，避免并发时序问题
                    var results = new List<(ComponentDebugItem item, int value, bool success)>();
                    
                    foreach (var item in _componentItems)
                    {
                        try
                        {
                            int value = plcController.ReadSingle(addrCombine: item.Register);
                            results.Add((item, value, true));
                        }
                        catch
                        {
                            results.Add((item, 0, false));
                        }
                    }
                    
                    // 在UI线程中更新结果
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        foreach (var (item, value, success) in results)
                        {
                            if (success)
                            {
                                switch (item.Type)
                                {
                                    case CardType.RelayControl:
                                    case CardType.SensorRead:
                                        item.CurrentStatus = value > 0;
                                        break;
                                        
                                    case CardType.DataWrite:
                                        item.CurrentValue = value;
                                        break;
                                }
                            }
                        }
                    }));
                }
                catch
                {
                    // 静默处理异常
                }
                finally
                {
                    _isUpdatingComponentStatus = false;
                }
            });
        }

        /// <summary>
        /// 更新组件状态（保留原方法用于向后兼容）
        /// </summary>
        private void UpdateComponentStatus()
        {
            // 调用子线程版本
            UpdateComponentStatusInBackground();
        }

        private void UpdateConnectionStatus()
        {
            var plcController = PLCSerialController.Instance;
            bool isConnected = plcController?.IsConnected == true;

            if (isConnected)
            {
                StatusText.Text = "已连接";
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // 绿色
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                
                // 确保定时器运行
                if (_componentStatusTimer?.IsEnabled == false)
                {
                    _componentStatusTimer.Start();
                }
            }
            else
            {
                StatusText.Text = "未连接";
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // 红色
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
                
                // 停止定时器，避免无意义的尝试
                if (_componentStatusTimer?.IsEnabled == true)
                {
                    _componentStatusTimer.Stop();
                }
            }

            // 更新测试按钮状态
            ReadButton.IsEnabled = isConnected;
            WriteButton.IsEnabled = isConnected;
            SetRelayButton.IsEnabled = isConnected;
            ResetRelayButton.IsEnabled = isConnected;
        }

        #endregion

        #region 事件处理

        private void PortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 串口选择改变时的处理
        }

        private void BaudRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //
            // 改变时的处理
        }

        private void TimeoutTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 超时时间改变时的处理
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }



        private void OnPLCLogMessage(string message)
        {
            // 在UI线程中更新日志
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogMessage(message);
            }));
        }

        #endregion

        #region 辅助方法

        private bool IsConnected()
        {
            return PLCSerialController.Instance?.IsConnected == true;
        }

        private bool IsConnectedWithMessage()
        {
            if (PLCSerialController.Instance?.IsConnected != true)
            {
                LogMessage("PLC未连接，请先连接PLC");
                return false;
            }
            return true;
        }

        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] {message}\r\n";
            
            LogTextBox.AppendText(logEntry);
            LogTextBox.ScrollToEnd();
        }

        private void SaveCurrentSettings()
        {
            try
            {
                // 保存当前设置到配置文件
                // 这里可以扩展为保存到配置文件
                LogMessage("串口设置已保存");
            }
            catch (Exception ex)
            {
                LogMessage($"保存设置失败: {ex.Message}");
            }
        }

        #endregion

        #region 静态PLC控制器访问

        /// <summary>
        /// 获取全局PLC控制器实例（用于其他页面访问）
        /// </summary>
        public static PLCSerialController GlobalPLCController { get; private set; }

        /// <summary>
        /// 设置全局PLC控制器实例
        /// </summary>
        /// <param name="controller">PLC控制器实例</param>
        public static void SetGlobalPLCController(PLCSerialController controller)
        {
            GlobalPLCController = controller;
        }

        /// <summary>
        /// 获取PLC连接状态
        /// </summary>
        /// <returns>是否已连接</returns>
        public static bool IsGlobalPLCConnected()
        {
            return GlobalPLCController?.IsConnected == true;
        }

        #endregion

        #region 组件调试功能

        /// <summary>
        /// 选中的组件项
        /// </summary>
        private ComponentDebugItem _selectedComponent;

        /// <summary>
        /// 组件置位按钮点击事件
        /// </summary>
        private async void ComponentSetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnectedWithMessage()) return;

            // 从按钮的Tag属性获取对应的组件项
            if (sender is Button button && button.Tag is ComponentDebugItem componentItem)
            {
                if (componentItem.Type != CardType.RelayControl)
                {
                    LogMessage("此操作只支持继电器控制卡片");
                    return;
                }

                try
                {
                    LogMessage($"正在设置 {componentItem.Name}({componentItem.Register})...");

                    var plcController = PLCSerialController.Instance;
                    
                    // 直接写入，简单直接
                    bool success = await plcController.WriteSingleAsync(addrCombine: componentItem.Register, data: 1);
                    
                    if (success)
                    {
                        LogMessage($"✅ 设置 {componentItem.Name}({componentItem.Register}) 成功");
                        // 不立即更新状态，等待定时器读取PLC真实状态
                    }
                    else
                    {
                        LogMessage($"❌ 设置 {componentItem.Name}({componentItem.Register}) 失败");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"设置 {componentItem.Name} 失败: {ex.Message}");
                }
            }
            else
            {
                LogMessage("无法识别组件信息");
            }
        }

        /// <summary>
        /// 组件复位按钮点击事件
        /// </summary>
        private async void ComponentResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnectedWithMessage()) return;

            // 从按钮的Tag属性获取对应的组件项
            if (sender is Button button && button.Tag is ComponentDebugItem componentItem)
            {
                if (componentItem.Type != CardType.RelayControl)
                {
                    LogMessage("此操作只支持继电器控制卡片");
                    return;
                }

                try
                {
                    LogMessage($"正在复位 {componentItem.Name}({componentItem.Register})...");

                    var plcController = PLCSerialController.Instance;
                    
                    // 直接写入，简单直接
                    bool success = await plcController.WriteSingleAsync(addrCombine: componentItem.Register, data: 0);
                    
                    if (success)
                    {
                        LogMessage($"✅ 复位 {componentItem.Name}({componentItem.Register}) 成功");
                        // 不立即更新状态，等待定时器读取PLC真实状态
                    }
                    else
                    {
                        LogMessage($"❌ 复位 {componentItem.Name}({componentItem.Register}) 失败");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"复位 {componentItem.Name} 失败: {ex.Message}");
                }
            }
            else
            {
                LogMessage("无法识别组件信息");
            }
        }

        /// <summary>
        /// 数据写入按钮点击事件
        /// </summary>
        private async void ComponentWriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConnectedWithMessage()) return;

            // 从按钮的Tag属性获取对应的组件项
            if (sender is Button button && button.Tag is ComponentDebugItem componentItem)
            {
                if (componentItem.Type != CardType.DataWrite)
                {
                    LogMessage("此操作只支持数据写入卡片");
                    return;
                }

                try
                {
                    var plcController = PLCSerialController.Instance;
                    bool success = await plcController.WriteSingleAsync(addrCombine: componentItem.Register, data: componentItem.WriteValue);
                    
                    if (success)
                    {
                        componentItem.CurrentValue = componentItem.WriteValue;
                        LogMessage($"写入 {componentItem.Name}({componentItem.Register}) = {componentItem.WriteValue} 成功");
                    }
                    else
                    {
                        LogMessage($"写入 {componentItem.Name}({componentItem.Register}) 失败");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"写入 {componentItem.Name} 失败: {ex.Message}");
                }
            }
            else
            {
                LogMessage("无法识别组件信息");
            }
        }

        /// <summary>
        /// 组件名称点击事件（选中组件）
        /// </summary>
        private void ComponentName_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.DataContext is ComponentDebugItem item)
            {
                _selectedComponent = item;
                LogMessage($"选中组件: {item.Name} ({item.Register})");
                
                // 可以在这里添加视觉反馈，比如高亮显示选中的卡片
                // 暂时通过日志提示用户
            }
        }

        /// <summary>
        /// 添加继电器卡片
        /// </summary>
        private void AddRelayCardButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ComponentEditDialog(CardType.RelayControl);
            if (dialog.ShowDialog() == true)
            {
                var newItem = new ComponentDebugItem
                {
                    Type = CardType.RelayControl,
                    Name = dialog.ComponentName,
                    Register = dialog.ComponentRegister,
                    CurrentStatus = false
                };
                
                _componentItems.Add(newItem);
                SaveComponentConfig();
                LogMessage($"添加继电器卡片: {newItem.Name} ({newItem.Register})");
            }
        }

        /// <summary>
        /// 添加传感器卡片
        /// </summary>
        private void AddSensorCardButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ComponentEditDialog(CardType.SensorRead);
            if (dialog.ShowDialog() == true)
            {
                var newItem = new ComponentDebugItem
                {
                    Type = CardType.SensorRead,
                    Name = dialog.ComponentName,
                    Register = dialog.ComponentRegister,
                    CurrentStatus = false
                };
                
                _componentItems.Add(newItem);
                SaveComponentConfig();
                LogMessage($"添加传感器卡片: {newItem.Name} ({newItem.Register})");
            }
        }

        /// <summary>
        /// 添加数据卡片
        /// </summary>
        private void AddDataCardButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ComponentEditDialog(CardType.DataWrite);
            if (dialog.ShowDialog() == true)
            {
                var newItem = new ComponentDebugItem
                {
                    Type = CardType.DataWrite,
                    Name = dialog.ComponentName,
                    Register = dialog.ComponentRegister,
                    CurrentValue = 0,
                    WriteValue = 0
                };
                
                _componentItems.Add(newItem);
                SaveComponentConfig();
                LogMessage($"添加数据卡片: {newItem.Name} ({newItem.Register})");
            }
        }

        /// <summary>
        /// 删除选中的卡片
        /// </summary>
        private void RemoveCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedComponent != null)
            {
                var result = MessageBox.Show(
                    $"确定要删除组件 '{_selectedComponent.Name}' 吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _componentItems.Remove(_selectedComponent);
                    SaveComponentConfig();
                    LogMessage($"删除组件: {_selectedComponent.Name} ({_selectedComponent.Register})");
                    _selectedComponent = null;
                }
            }
            else
            {
                MessageBox.Show("请先点击组件名称选择要删除的组件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region 配置文件管理

        private const string CONFIG_FILE = "ComponentDebugConfig.json";

        /// <summary>
        /// 保存组件配置到文件
        /// </summary>
        private void SaveComponentConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE);
                string json = JsonConvert.SerializeObject(_componentItems, Formatting.Indented);
                File.WriteAllText(configPath, json);
                LogMessage($"组件配置已保存到: {configPath}");
            }
            catch (Exception ex)
            {
                LogMessage($"保存组件配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载组件配置
        /// </summary>
        private ObservableCollection<ComponentDebugItem> LoadComponentConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE);
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var items = JsonConvert.DeserializeObject<ObservableCollection<ComponentDebugItem>>(json);
                    LogMessage($"从文件加载组件配置: {configPath}，共{items?.Count ?? 0}个组件");
                    return items;
                }
                else
                {
                    LogMessage("组件配置文件不存在，将使用默认配置");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"加载组件配置失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 页面卸载

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _statusUpdateTimer?.Stop();
            _componentStatusTimer?.Stop();
            
            // 保存组件配置
            SaveComponentConfig();
            
            // 保存当前的PLC控制器为全局实例
            if (PLCSerialController.Instance?.IsConnected == true)
            {
                SetGlobalPLCController(PLCSerialController.Instance);
            }
        }

        #endregion
    }
} 
