using System;
using System.Linq;
using System.Windows;
using WpfApp2.SMTGPIO;

namespace WpfApp2.UI
{
    /// <summary>
    /// GPIOSettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GPIOSettingsWindow : Window
    {
        private GPIOConfig _currentConfig;

        public GPIOSettingsWindow()
        {
            InitializeComponent();
            InitializeUI();
        }

        /// <summary>
        /// 初始化界面
        /// </summary>
        private void InitializeUI()
        {
            // 加载当前配置
            _currentConfig = GPIOConfigManager.LoadConfig();

            // 初始化设备类型下拉框
            InitializeDeviceComboBox();

            // 设置当前配置值
            LoadCurrentConfig();

            // 更新当前配置显示
            UpdateCurrentConfigDisplay();
        }

        /// <summary>
        /// 初始化设备类型下拉框
        /// </summary>
        private void InitializeDeviceComboBox()
        {
            var deviceTypes = GPIOConfigManager.GetAvailableDeviceTypes();
            
            foreach (var deviceType in deviceTypes)
            {
                var config = new GPIOConfig { DeviceType = deviceType };
                DeviceTypeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = config.DeviceDisplayName,
                    Tag = deviceType
                });
            }
        }

        /// <summary>
        /// 加载当前配置到控件
        /// </summary>
        private void LoadCurrentConfig()
        {
            // 设置设备类型选择
            foreach (ComboBoxItem item in DeviceTypeComboBox.Items)
            {
                if ((SMTGPIODeviceType)item.Tag == _currentConfig.DeviceType)
                {
                    DeviceTypeComboBox.SelectedItem = item;
                    break;
                }
            }

            // 设置端口号
            PortTextBox.Text = _currentConfig.Port.ToString();
        }

        /// <summary>
        /// 更新当前配置显示
        /// </summary>
        private void UpdateCurrentConfigDisplay()
        {
            CurrentDeviceText.Text = _currentConfig.DeviceDisplayName;
            CurrentPortText.Text = _currentConfig.Port.ToString();
        }

        /// <summary>
        /// 确定按钮点击
        /// </summary>
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                if (!ValidateInput())
                {
                    return;
                }

                // 获取新配置
                var newConfig = GetConfigFromUI();

                // 保存配置
                if (GPIOConfigManager.SaveConfig(newConfig))
                {
                    // 更新当前配置显示
                    _currentConfig = newConfig;
                    UpdateCurrentConfigDisplay();
                    
                    // 设置对话框结果为成功
                    DialogResult = true;
                    
                    MessageBox.Show("GPIO配置已保存成功！", 
                        "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // 关闭窗口
                    Close();
                }
                else
                {
                    MessageBox.Show("保存GPIO配置失败，请检查文件权限。", 
                        "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置时出错：{ex.Message}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 测试连接按钮点击
        /// </summary>
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                if (!ValidateInput())
                {
                    return;
                }

                // 获取测试配置
                var testConfig = GetConfigFromUI();

                // 创建临时控制器进行测试
                using (var testController = new SMTGPIOController())
                {
                    bool testResult = testController.Initialize(testConfig.DeviceType);
                    
                    if (testResult)
                    {
                        MessageBox.Show($"GPIO连接测试成功！\n\n设备：{testConfig.DeviceDisplayName}\n端口：{testConfig.Port}", 
                            "测试成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"GPIO连接测试失败！\n\n请检查：\n1. 设备类型是否正确\n2. 硬件连接是否正常\n3. 驱动是否安装", 
                            "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试连接时出错：{ex.Message}\n\n可能原因：\n1. SMTGPIO.dll文件缺失\n2. 驱动未安装\n3. 硬件未连接", 
                    "测试错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 设置对话框结果为取消
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool ValidateInput()
        {
            // 检查设备类型选择
            if (DeviceTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择设备类型。", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                DeviceTypeComboBox.Focus();
                return false;
            }

            // 检查端口号输入
            if (string.IsNullOrWhiteSpace(PortTextBox.Text))
            {
                MessageBox.Show("请输入端口号。", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                PortTextBox.Focus();
                return false;
            }

            if (!uint.TryParse(PortTextBox.Text, out uint port) || port < 1 || port > 8)
            {
                MessageBox.Show("端口号必须是1-8之间的整数。", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                PortTextBox.Focus();
                PortTextBox.SelectAll();
                return false;
            }

            return true;
        }

        /// <summary>
        /// 从界面获取配置
        /// </summary>
        private GPIOConfig GetConfigFromUI()
        {
            var selectedItem = (ComboBoxItem)DeviceTypeComboBox.SelectedItem;
            var deviceType = (SMTGPIODeviceType)selectedItem.Tag;
            var port = uint.Parse(PortTextBox.Text);

            return new GPIOConfig
            {
                DeviceType = deviceType,
                Port = port
            };
        }
    }

    /// <summary>
    /// ComboBox项目类
    /// </summary>
    public class ComboBoxItem
    {
        public string Content { get; set; }
        public object Tag { get; set; }

        public override string ToString()
        {
            return Content;
        }
    }
} 