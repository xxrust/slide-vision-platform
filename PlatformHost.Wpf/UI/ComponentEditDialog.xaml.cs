using System;
using System.Windows;

namespace WpfApp2.UI
{
    /// <summary>
    /// ComponentEditDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ComponentEditDialog : Window
    {
        #region 公共属性

        /// <summary>
        /// 组件名称
        /// </summary>
        public string ComponentName { get; private set; }

        /// <summary>
        /// 寄存器地址
        /// </summary>
        public string ComponentRegister { get; private set; }

        /// <summary>
        /// 卡片类型
        /// </summary>
        public CardType CardType { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cardType">卡片类型</param>
        public ComponentEditDialog(CardType cardType)
        {
            InitializeComponent();
            CardType = cardType;
            SetupUI();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 设置界面
        /// </summary>
        private void SetupUI()
        {
            switch (CardType)
            {
                case CardType.RelayControl:
                    TypeTextBlock.Text = "继电器控制卡片";
                    Title = "添加继电器控制卡片";
                    NameTextBox.Text = "";
                    RegisterTextBox.Text = "R506";
                    RegisterHintTextBlock.Text = "示例：R506, MR1915 (继电器地址)";
                    break;
                case CardType.SensorRead:
                    TypeTextBlock.Text = "传感器读取卡片";
                    Title = "添加传感器读取卡片";
                    NameTextBox.Text = "";
                    RegisterTextBox.Text = "R0";
                    RegisterHintTextBlock.Text = "示例：R0, R1, R100 (输入信号地址)";
                    break;
                case CardType.DataWrite:
                    TypeTextBlock.Text = "数据写入卡片";
                    Title = "添加数据写入卡片";
                    NameTextBox.Text = "";
                    RegisterTextBox.Text = "DM0.L";
                    RegisterHintTextBlock.Text = "示例：DM0.L, DM1.L (数据寄存器地址)";
                    break;
            }
            
            // 延迟设置焦点，确保界面完全加载
            this.Loaded += (s, e) => 
            {
                NameTextBox.Focus();
                NameTextBox.SelectAll();
            };
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证组件名称
            string componentName = NameTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(componentName))
            {
                MessageBox.Show("请输入组件名称", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                NameTextBox.SelectAll();
                return;
            }

            // 验证寄存器地址
            string registerAddress = RegisterTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(registerAddress))
            {
                MessageBox.Show("请输入寄存器地址", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                RegisterTextBox.Focus();
                RegisterTextBox.SelectAll();
                return;
            }

            // 简单的寄存器地址格式验证
            if (!IsValidRegisterAddress(registerAddress))
            {
                MessageBox.Show("寄存器地址格式不正确\n\n有效格式示例：\n• 继电器：R506, MR1915\n• 传感器：R0, R1, R100\n• 数据：DM0.L, DM1.L", 
                    "地址格式错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                RegisterTextBox.Focus();
                RegisterTextBox.SelectAll();
                return;
            }

            // 保存输入值
            ComponentName = componentName;
            ComponentRegister = registerAddress;

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 验证寄存器地址格式
        /// </summary>
        private bool IsValidRegisterAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            // 常见的PLC寄存器地址格式
            // R开头：R0, R1, R506等
            // MR开头：MR1915, MR2000等  
            // DM开头：DM0.L, DM1.L等
            address = address.ToUpper();
            
            return address.StartsWith("R") || 
                   address.StartsWith("MR") || 
                   address.StartsWith("DM") ||
                   address.StartsWith("X") ||
                   address.StartsWith("Y") ||
                   address.StartsWith("M");
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }
} 