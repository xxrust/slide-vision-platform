using System;
using System.Windows;
using WpfApp2.UI;
using System.Threading.Tasks;
using WpfApp2.UI.Models;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// IO设备管理器 - 简化版（移除锁机制）
    /// 在工业控制场景中，IO操作是顺序执行的，不需要锁保护
    /// </summary>
    public static class IOManager
    {
        private static SMTGPIOController _gpioController;
        private static bool _isInitialized = false;
        // 🔧 移除锁对象：private static readonly object _lockObject = new object();

        /// <summary>
        /// IO控制器是否已初始化
        /// </summary>
        public static bool IsInitialized
        {
            get { return _isInitialized; }
        }

        /// <summary>
        /// 初始化IO控制器
        /// </summary>
        /// <returns>是否初始化成功</returns>
        public static bool Initialize()
        {
            try
            {
                if (_isInitialized)
                {
                    LogMessage("IO控制器已初始化，跳过重复初始化", LogLevel.Info);
                    return true;
                }

                _gpioController = new SMTGPIOController();
                
                // 根据配置的设备类型初始化GPIO控制器
                var cfg = GPIOConfigManager.CurrentConfig;
                bool initResult = _gpioController.Initialize(cfg.DeviceType);
                
                if (initResult)
                {
                    _isInitialized = true;
                    LogMessage("IO控制器初始化成功", LogLevel.Info);
                    
                    // 初始化后复位所有输出
                    _gpioController.SetAllOutputPinsLow();
                    LogMessage("IO输出已初始化为低电平", LogLevel.Info);
                    
                    return true;
                }
                else
                {
                    LogMessage("IO控制器初始化失败", LogLevel.Error);
                    
                    // 同步显示错误对话框
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        MessageBox.Show("IO控制器初始化失败，请检查硬件连接和配置。", 
                            "IO初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("IO控制器初始化失败，请检查硬件连接和配置。", 
                                "IO初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"初始化IO控制器时发生异常: {ex.Message}", LogLevel.Error);
                
                // 同步显示错误对话框
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    MessageBox.Show($"IO初始化异常: {ex.Message}", 
                        "IO初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"IO初始化异常: {ex.Message}", 
                            "IO初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                
                return false;
            }
        }

        /// <summary>
        /// 设置检测结果输出到IO端口
        /// </summary>
        /// <param name="isOK">检测结果，true为OK，false为NG</param>
        public static void SetDetectionResult(bool isOK)
        {
            try
            {
                if (!IsInitialized)
                {
                    LogMessage("IO控制器未初始化，无法设置检测结果", LogLevel.Warning);
                    return;
                }

                var startTime = DateTime.Now;
                _gpioController.SetDetectionResult(isOK);
                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                
                string resultText = isOK ? "OK" : "NG";
                string ioStatus = isOK ? "IO1=1, IO2=1" : "IO1=1, IO2=0";
                LogMessage($"检测结果: {resultText}, IO输出: {ioStatus}, 耗时: {duration:F1}ms", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"设置IO输出失败: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 记录日志消息
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="level">日志级别</param>
        private static void LogMessage(string message, LogLevel level)
        {
            try
            {
                // 同步记录到Page1的日志显示
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    LogManager.Info($"[IO] {message}");
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LogManager.Info($"[IO] {message}");
                    });
                }

                // 根据级别显示不同的处理
                if (level == LogLevel.Error)
                {
                    // 错误级别的消息已经在调用方处理弹窗，这里不重复处理
                    Console.WriteLine($"[IO ERROR] {message}");
                }
                else if (level == LogLevel.Warning)
                {
                    Console.WriteLine($"[IO WARNING] {message}");
                }
                else
                {
                    Console.WriteLine($"[IO INFO] {message}");
                }
            }
            catch
            {
                // 忽略日志记录异常，避免影响主流程
            }
        }

        /// <summary>
        /// 设置单个输出口的状态
        /// </summary>
        /// <param name="pinNumber">引脚号 (1-4)</param>
        /// <param name="isHigh">是否为高电平</param>
        public static void SetSingleOutput(int pinNumber, bool isHigh)
        {
            // 🔧 移除锁保护，简化IO操作
            try
            {
                if (!IsInitialized)
                {
                    LogMessage("IO控制器未初始化，无法设置单个输出", LogLevel.Warning);
                    return;
                }

                if (pinNumber < 1 || pinNumber > 4)
                {
                    throw new ArgumentException("引脚号必须在1-4之间", nameof(pinNumber));
                }

                // 使用配置文件中的端口号
                var config = GPIOConfigManager.CurrentConfig;
                _gpioController.SetOutputPin(config.Port, (uint)pinNumber, isHigh);
                LogMessage($"IO{pinNumber}已设置为{(isHigh ? "高" : "低")}电平", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"设置IO{pinNumber}失败: {ex.Message}", LogLevel.Error);
                
                // 同步显示错误对话框
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    MessageBox.Show($"IO{pinNumber}控制失败: {ex.Message}", 
                        "IO控制错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"IO{pinNumber}控制失败: {ex.Message}", 
                            "IO控制错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            }
        }

        /// <summary>
        /// 获取所有输出口的状态
        /// </summary>
        /// <returns>输出状态数组，索引0-3对应IO1-IO4</returns>
        public static bool[] GetAllOutputStates()
        {
            // 🔧 移除锁保护，简化IO操作
            try
            {
                if (!IsInitialized)
                {
                    return new bool[4]; // 返回全为false的数组
                }

                // 使用配置文件中的端口号
                var config = GPIOConfigManager.CurrentConfig;
                var states = new bool[4];
                for (int i = 1; i <= 4; i++)
                {
                    int level = _gpioController.GetOutputPinLevel(config.Port, (uint)i);
                    states[i - 1] = level == 1; // 将int转换为bool，1为true，其他为false
                }
                return states;
            }
            catch (Exception ex)
            {
                LogMessage($"获取输出状态失败: {ex.Message}", LogLevel.Warning);
                return new bool[4]; // 返回全为false的数组
            }
        }

        /// <summary>
        /// 复位所有IO输出
        /// </summary>
        public static void ResetAllOutputs()
        {
            // 🔧 移除锁保护，简化IO操作
            try
            {
                if (!IsInitialized)
                {
                    LogMessage("IO控制器未初始化，无法复位输出", LogLevel.Warning);
                    return;
                }

                _gpioController.SetAllOutputPinsLow();
                LogMessage("所有IO输出已复位，流程主动清空", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"复位IO输出失败: {ex.Message}", LogLevel.Error);
                
                // 同步显示错误对话框
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    MessageBox.Show($"IO复位失败: {ex.Message}", 
                        "IO控制错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"IO复位失败: {ex.Message}", 
                            "IO控制错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
            }
        }

        /// <summary>
        /// 释放IO控制器资源
        /// </summary>
        public static void Dispose()
        {
            // 🔧 移除锁保护，简化资源释放
            try
            {
                if (_gpioController != null)
                {
                    _gpioController.Dispose();
                    _gpioController = null;
                }
                
                _isInitialized = false;
                LogMessage("IO控制器资源已释放", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogMessage($"释放IO控制器资源时出错: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 日志级别枚举
        /// </summary>
        private enum LogLevel
        {
            Info,
            Warning,
            Error
        }
    }
} 
