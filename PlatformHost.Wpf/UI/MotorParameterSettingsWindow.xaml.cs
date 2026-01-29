using System;
using System.Threading.Tasks;
using System.Windows;
using WpfApp2.SMTGPIO;
using System.Globalization;

namespace WpfApp2.UI
{
    /// <summary>
    /// 电机参数设置窗口
    /// </summary>
    public partial class MotorParameterSettingsWindow : Window
    {
        private PLCSerialController _plcController;

        public MotorParameterSettingsWindow()
        {
            InitializeComponent();
            _plcController = PLCSerialController.Instance;
            
            // 窗口加载时自动读取参数
            Loaded += MotorParameterSettingsWindow_Loaded;
            
            // 窗口关闭时自动触发到负坐标
            Closing += MotorParameterSettingsWindow_Closing;
        }

        private async void MotorParameterSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ReadParametersFromPLC();
        }

        /// <summary>
        /// 窗口关闭事件 - 自动触发到负坐标
        /// </summary>
        private async void MotorParameterSettingsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                LogMessage("窗口正在关闭，自动触发到负坐标...");
                await TriggerToNegativeCoordinate();
            }
            catch (Exception ex)
            {
                LogMessage($"窗口关闭时触发到负坐标失败: {ex.Message}");
                // 不阻止窗口关闭
            }
        }

        /// <summary>
        /// 触发电机到正坐标 (R1002)
        /// </summary>
        private async Task TriggerToPositiveCoordinate()
        {
            try
            {
                if (!_plcController.IsConnected)
                {
                    LogMessage("PLC未连接，无法触发到正坐标");
                    MessageBox.Show("PLC未连接，无法执行电机控制", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                LogMessage("触发电机到正坐标 (R1002)...");
                
                // 写入1到R1002寄存器来触发到正坐标
                bool success = await Task.Run(() => _plcController.WriteSingle(addrCombine: "R1002", data: 1));
                
                if (success)
                {
                    LogMessage("到正坐标指令发送成功");
                    StatusTextBlock.Text = "已发送到正坐标指令";
                }
                else
                {
                    LogMessage("到正坐标指令发送失败");
                    StatusTextBlock.Text = "到正坐标指令发送失败";
                    MessageBox.Show("发送到正坐标指令失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"触发到正坐标失败: {ex.Message}");
                StatusTextBlock.Text = $"到正坐标失败: {ex.Message}";
                MessageBox.Show($"触发到正坐标失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 触发电机到负坐标 (R1003)
        /// </summary>
        private async Task TriggerToNegativeCoordinate()
        {
            try
            {
                if (!_plcController.IsConnected)
                {
                    LogMessage("PLC未连接，无法触发到负坐标");
                    return; // 窗口关闭时不显示错误消息
                }

                LogMessage("触发电机到负坐标 (R1003)...");
                
                // 写入1到R1003寄存器来触发到负坐标
                bool success = await Task.Run(() => _plcController.WriteSingle(addrCombine: "R1003", data: 1));
                
                if (success)
                {
                    LogMessage("到负坐标指令发送成功");
                    StatusTextBlock.Text = "已发送到负坐标指令";
                }
                else
                {
                    LogMessage("到负坐标指令发送失败");
                    StatusTextBlock.Text = "到负坐标指令发送失败";
                }
            }
            catch (Exception ex)
            {
                LogMessage($"触发到负坐标失败: {ex.Message}");
                StatusTextBlock.Text = $"到负坐标失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 从PLC读取所有电机参数
        /// </summary>
        private async Task ReadParametersFromPLC()
        {
            try
            {
                StatusTextBlock.Text = "正在读取参数...";
                
                if (!_plcController.IsConnected)
                {
                    StatusTextBlock.Text = "PLC未连接，无法读取参数";
                    LogMessage("PLC未连接，无法读取参数");
                    return;
                }

                LogMessage("开始读取电机参数...");

                // 读取正向坐标 (DM904.D)
                LogMessage("正在读取正向坐标 (DM904.D)...");
                try
                {
                    float forwardCoord = await ReadPLCFloatValue("DM904.D");
                    LogMessage($"正向坐标最终值: {forwardCoord}");
                    ForwardCoordTextBox.Text = forwardCoord.ToString("F2");
                    LogMessage($"正向坐标显示值: {ForwardCoordTextBox.Text}");
                }
                catch (Exception ex)
                {
                    LogMessage($"读取正向坐标失败: {ex.Message}");
                    ForwardCoordTextBox.Text = "读取失败";
                }

                // 读取反向坐标 (DM906.D)
                LogMessage("正在读取反向坐标 (DM906.D)...");
                try
                {
                    float reverseCoord = await ReadPLCFloatValue("DM906.D");
                    LogMessage($"反向坐标最终值: {reverseCoord}");
                    ReverseCoordTextBox.Text = reverseCoord.ToString("F2");
                    LogMessage($"反向坐标显示值: {ReverseCoordTextBox.Text}");
                }
                catch (Exception ex)
                {
                    LogMessage($"读取反向坐标失败: {ex.Message}");
                    ReverseCoordTextBox.Text = "读取失败";
                }

                // 读取启动速度 (DM100.U)
                LogMessage("正在读取启动速度 (DM100.U)...");
                try
                {
                    var startSpeed = await Task.Run(() => _plcController.ReadSingle(addrCombine: "DM100.U"));
                    LogMessage($"启动速度原始值: {startSpeed}");
                    StartSpeedTextBox.Text = startSpeed.ToString();
                    LogMessage($"启动速度显示值: {StartSpeedTextBox.Text}");
                }
                catch (Exception ex)
                {
                    LogMessage($"读取启动速度失败: {ex.Message}");
                    StartSpeedTextBox.Text = "读取失败";
                }

                // 读取加速度 (DM101.U)
                LogMessage("正在读取加速度 (DM101.U)...");
                try
                {
                    var acceleration = await Task.Run(() => _plcController.ReadSingle(addrCombine: "DM101.U"));
                    LogMessage($"加速度原始值: {acceleration}");
                    AccelerationTextBox.Text = acceleration.ToString();
                    LogMessage($"加速度显示值: {AccelerationTextBox.Text}");
                }
                catch (Exception ex)
                {
                    LogMessage($"读取加速度失败: {ex.Message}");
                    AccelerationTextBox.Text = "读取失败";
                }

                // 读取移动速度 (DM102.D)
                LogMessage("正在读取移动速度 (DM102.D)...");
                try
                {
                    var moveSpeed = await Task.Run(() => _plcController.ReadSingle(addrCombine: "DM102.D"));
                    LogMessage($"移动速度原始值: {moveSpeed}");
                    MoveSpeedTextBox.Text = moveSpeed.ToString();
                    LogMessage($"移动速度显示值: {MoveSpeedTextBox.Text}");
                }
                catch (Exception ex)
                {
                    LogMessage($"读取移动速度失败: {ex.Message}");
                    MoveSpeedTextBox.Text = "读取失败";
                }

                // 读取位置负极限 (DM900.D)
                LogMessage("正在读取位置负极限 (DM900.D)...");
                try
                {
                    float negativeLimit = await ReadPLCFloatValue("DM900.D");
                    LogMessage($"位置负极限最终值: {negativeLimit}");
                    NegativeLimitTextBox.Text = negativeLimit.ToString("F2");
                    LogMessage($"位置负极限显示值: {NegativeLimitTextBox.Text}");
                }
                catch (Exception ex)
                {
                    LogMessage($"读取位置负极限失败: {ex.Message}");
                    NegativeLimitTextBox.Text = "读取失败";
                }

                // 读取位置正极限 (DM902.D)
                LogMessage("正在读取位置正极限 (DM902.D)...");
                try
                {
                    float positiveLimit = await ReadPLCFloatValue("DM902.D");
                    LogMessage($"位置正极限最终值: {positiveLimit}");
                    PositiveLimitTextBox.Text = positiveLimit.ToString("F2");
                    LogMessage($"位置正极限显示值: {PositiveLimitTextBox.Text}");
                }
                catch (Exception ex)
                {
                    LogMessage($"读取位置正极限失败: {ex.Message}");
                    PositiveLimitTextBox.Text = "读取失败";
                }

                StatusTextBlock.Text = "参数读取完成";
                LogMessage("所有电机参数读取完成");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"读取失败: {ex.Message}";
                LogMessage($"读取电机参数总体失败: {ex.Message}");
                MessageBox.Show($"读取PLC参数失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 记录日志消息
        /// </summary>
        /// <param name="message">日志消息</param>
        private void LogMessage(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [电机参数] {message}";
                
                // 输出到调试窗口
                System.Diagnostics.Debug.WriteLine(logEntry);
                
                // 也可以输出到控制台（如果有的话）
                Console.WriteLine(logEntry);
                
                // 使用系统日志管理器
                if (message.Contains("失败") || message.Contains("错误"))
                {
                    WpfApp2.UI.Models.LogManager.Error(logEntry, "电机参数");
                }
                else
                {
                    WpfApp2.UI.Models.LogManager.Info(logEntry, "电机参数");
                }
            }
            catch
            {
                // 忽略日志记录异常
            }
        }

        /// <summary>
        /// 写入所有参数到PLC
        /// </summary>
        private async Task WriteParametersToPLC()
        {
            try
            {
                StatusTextBlock.Text = "正在写入参数...";
                
                if (!_plcController.IsConnected)
                {
                    StatusTextBlock.Text = "PLC未连接，无法写入参数";
                    MessageBox.Show("PLC未连接，无法写入参数", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 验证输入数据
                if (!ValidateInput())
                {
                    StatusTextBlock.Text = "输入数据无效";
                    return;
                }

                // 写入正向坐标 (DM904.D) - 使用新的浮点数写入方法
                if (float.TryParse(ForwardCoordTextBox.Text, out float forwardCoord))
                {
                    await WritePLCFloatValue("DM904.D", forwardCoord);
                }

                // 写入反向坐标 (DM906.D) - 使用新的浮点数写入方法
                if (float.TryParse(ReverseCoordTextBox.Text, out float reverseCoord))
                {
                    await WritePLCFloatValue("DM906.D", reverseCoord);
                }

                // 写入启动速度 (DM100.U) - 整数直接写入
                if (int.TryParse(StartSpeedTextBox.Text, out int startSpeed))
                {
                    await Task.Run(() => _plcController.WriteSingle(addrCombine: "DM100.U", data: startSpeed));
                }

                // 写入加速度 (DM101.U) - 整数直接写入
                if (int.TryParse(AccelerationTextBox.Text, out int acceleration))
                {
                    await Task.Run(() => _plcController.WriteSingle(addrCombine: "DM101.U", data: acceleration));
                }

                // 写入移动速度 (DM102.D) - 整数直接写入
                if (int.TryParse(MoveSpeedTextBox.Text, out int moveSpeed))
                {
                    await Task.Run(() => _plcController.WriteSingle(addrCombine: "DM102.D", data: moveSpeed));
                }

                // 写入位置负极限 (DM900.D) - 使用新的浮点数写入方法
                if (float.TryParse(NegativeLimitTextBox.Text, out float negativeLimit))
                {
                    await WritePLCFloatValue("DM900.D", negativeLimit);
                }

                // 写入位置正极限 (DM902.D) - 使用新的浮点数写入方法
                if (float.TryParse(PositiveLimitTextBox.Text, out float positiveLimit))
                {
                    await WritePLCFloatValue("DM902.D", positiveLimit);
                }

                StatusTextBlock.Text = "参数写入完成";
                MessageBox.Show("电机参数已成功写入PLC", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"写入失败: {ex.Message}";
                MessageBox.Show($"写入PLC参数失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 验证输入数据
        /// </summary>
        /// <returns>是否有效</returns>
        private bool ValidateInput()
        {
            // 验证正向坐标
            if (!float.TryParse(ForwardCoordTextBox.Text, out _))
            {
                MessageBox.Show("正向坐标必须是有效的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ForwardCoordTextBox.Focus();
                return false;
            }

            // 验证反向坐标
            if (!float.TryParse(ReverseCoordTextBox.Text, out _))
            {
                MessageBox.Show("反向坐标必须是有效的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ReverseCoordTextBox.Focus();
                return false;
            }

            // 验证启动速度
            if (!int.TryParse(StartSpeedTextBox.Text, out int startSpeed) || startSpeed < 0)
            {
                MessageBox.Show("启动速度必须是有效的正整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                StartSpeedTextBox.Focus();
                return false;
            }

            // 验证加速度
            if (!int.TryParse(AccelerationTextBox.Text, out int acceleration) || acceleration < 0)
            {
                MessageBox.Show("加速度必须是有效的正整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                AccelerationTextBox.Focus();
                return false;
            }

            // 验证移动速度
            if (!int.TryParse(MoveSpeedTextBox.Text, out int moveSpeed) || moveSpeed < 0)
            {
                MessageBox.Show("移动速度必须是有效的正整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                MoveSpeedTextBox.Focus();
                return false;
            }

            // 验证位置负极限
            if (!float.TryParse(NegativeLimitTextBox.Text, out _))
            {
                MessageBox.Show("位置负极限必须是有效的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                NegativeLimitTextBox.Focus();
                return false;
            }

            // 验证位置正极限
            if (!float.TryParse(PositiveLimitTextBox.Text, out _))
            {
                MessageBox.Show("位置正极限必须是有效的数值", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                PositiveLimitTextBox.Focus();
                return false;
            }

            // 验证位置极限的逻辑关系（负极限应该小于正极限）
            if (float.TryParse(NegativeLimitTextBox.Text, out float negLimit) && 
                float.TryParse(PositiveLimitTextBox.Text, out float posLimit))
            {
                if (negLimit >= posLimit)
                {
                    MessageBox.Show("位置负极限必须小于位置正极限", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    NegativeLimitTextBox.Focus();
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 将PLC读取的32位整数转换为浮点数
        /// 处理有符号/无符号整数转换问题
        /// </summary>
        /// <param name="plcIntValue">PLC返回的32位整数</param>
        /// <returns>浮点数</returns>
        private float ConvertPLCIntToFloat(int plcIntValue)
        {
            // 直接将int的位模式转换为float
            // 这样可以正确处理负数的情况
            return BitConverter.ToSingle(BitConverter.GetBytes(plcIntValue), 0);
        }

        /// <summary>
        /// 从PLC读取可能超出int范围的浮点数值
        /// 通过异常处理来获取完整的数值字符串
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <returns>浮点数</returns>
        private async Task<float> ReadPLCFloatValue(string address)
        {
            try
            {
                // 尝试正常读取
                var value = await Task.Run(() => _plcController.ReadSingle(addrCombine: address));
                return ConvertPLCIntToFloat(value);
            }
            catch (Exception ex) when (ex.Message.Contains("PLC响应不是数字格式"))
            {
                // 从异常消息中提取原始字符串
                var match = System.Text.RegularExpressions.Regex.Match(ex.Message, @"'(\d+)'");
                if (match.Success)
                {
                    string rawValue = match.Groups[1].Value;
                    LogMessage($"从异常中提取到原始值: {rawValue}");
                    
                    // 尝试解析为uint，然后转换为float
                    if (uint.TryParse(rawValue, out uint uintValue))
                    {
                        LogMessage($"解析为uint: {uintValue}");
                        
                        // 将uint的位模式转换为float
                        byte[] bytes = BitConverter.GetBytes(uintValue);
                        float result = BitConverter.ToSingle(bytes, 0);
                        LogMessage($"转换为float: {result}");
                        
                        return result;
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// 将浮点数转换为PLC可写入的32位整数
        /// </summary>
        /// <param name="floatValue">浮点数</param>
        /// <returns>PLC可写入的32位整数</returns>
        private int ConvertFloatToPLCInt(float floatValue)
        {
            // 将浮点数转换为字节数组（IEEE 754格式）
            byte[] bytes = BitConverter.GetBytes(floatValue);
            
            // 将字节数组转换为32位有符号整数
            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// 向PLC写入浮点数值（处理超出int范围的情况）
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="floatValue">要写入的浮点数</param>
        /// <returns>是否成功</returns>
        private async Task<bool> WritePLCFloatValue(string address, float floatValue)
        {
            try
            {
                LogMessage($"准备写入 {address} = {floatValue}");
                
                // 转换为32位整数
                int intValue = ConvertFloatToPLCInt(floatValue);
                LogMessage($"转换为有符号整数: {intValue}");
                
                // 转换为无符号整数显示（用于调试）
                uint uintValue = (uint)intValue;
                LogMessage($"对应的无符号整数: {uintValue}");
                
                // 对于负数的浮点值，我们需要使用特殊的处理方式
                if (intValue < 0)
                {
                    // 使用反射或直接发送命令来绕过int限制
                    string command = $"WR {address} {uintValue}";
                    LogMessage($"发送命令: {command}");
                    
                    try
                    {
                        // 尝试使用反射调用SendCommandAsync
                        var sendMethod = typeof(PLCSerialController).GetMethod("SendCommandAsync", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (sendMethod != null)
                        {
                            var task = (Task<string>)sendMethod.Invoke(_plcController, new object[] { command });
                            string response = await task;
                            LogMessage($"PLC响应: {response}");
                            
                            bool success = response == "OK";
                            LogMessage($"写入 {address} {(success ? "成功" : "失败")}");
                            return success;
                        }
                        else
                        {
                            LogMessage("无法获取SendCommandAsync方法，使用标准WriteSingle");
                            // 回退到标准方法（可能会失败）
                            await Task.Run(() => _plcController.WriteSingle(addrCombine: address, data: intValue));
                            LogMessage($"写入 {address} 成功（使用标准方法）");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"反射调用失败，尝试标准方法: {ex.Message}");
                        // 最后尝试标准方法
                        await Task.Run(() => _plcController.WriteSingle(addrCombine: address, data: intValue));
                        LogMessage($"写入 {address} 成功（标准方法备用）");
                        return true;
                    }
                }
                else
                {
                    // 正数可以直接使用标准方法
                    await Task.Run(() => _plcController.WriteSingle(addrCombine: address, data: intValue));
                    LogMessage($"写入 {address} 成功（正数）");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"写入 {address} 失败: {ex.Message}");
                return false;
            }
        }

        #region 事件处理

        /// <summary>
        /// 到正坐标按钮点击事件
        /// </summary>
        private async void ToPositiveCoordButton_Click(object sender, RoutedEventArgs e)
        {
            await TriggerToPositiveCoordinate();
        }

        /// <summary>
        /// 到负坐标按钮点击事件
        /// </summary>
        private async void ToNegativeCoordButton_Click(object sender, RoutedEventArgs e)
        {
            await TriggerToNegativeCoordinate();
        }

        /// <summary>
        /// 刷新参数按钮点击事件
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await ReadParametersFromPLC();
        }

        /// <summary>
        /// 确认修改按钮点击事件
        /// </summary>
        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要将修改的参数写入PLC吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await WriteParametersToPLC();
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion
    }
}