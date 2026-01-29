using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SampleWrapper.SMTGPIO
{
    /// <summary>
    /// SMTGPIO使用示例
    /// 展示如何在点胶检测项目中使用IO板控制器
    /// </summary>
    public class SMTGPIOExample
    {
        private SMTGPIOController _gpioController;
        private bool _isRunning = false;

        public SMTGPIOExample()
        {
            _gpioController = new SMTGPIOController();
        }

        /// <summary>
        /// 基本使用示例
        /// </summary>
        public async Task BasicUsageExample()
        {
            try
            {
                // 1. 初始化IO板
                Console.WriteLine("正在初始化SMTGPIO设备...");
                bool initResult = _gpioController.Initialize(SMTGPIOController.DeviceType.SCI_EVC3_5);
                
                if (!initResult)
                {
                    Console.WriteLine("设备初始化失败");
                    return;
                }
                
                Console.WriteLine("设备初始化成功");

                // 2. 获取端口信息
                uint outputPortCount = _gpioController.GetOutputPortCount();
                uint inputPortCount = _gpioController.GetInputPortCount();
                
                Console.WriteLine($"输出端口数量: {outputPortCount}");
                Console.WriteLine($"输入端口数量: {inputPortCount}");

                // 3. 设置所有输出引脚为低电平
                Console.WriteLine("设置所有输出引脚为低电平...");
                _gpioController.SetAllOutputPinsLow();
                Console.WriteLine("设置完成");

                // 4. 演示单个引脚控制
                await DemonstratePinControl();

                // 5. 读取输入状态
                await ReadInputStatus();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"基本使用示例出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 演示引脚控制
        /// </summary>
        private async Task DemonstratePinControl()
        {
            try
            {
                Console.WriteLine("开始演示引脚控制...");
                
                uint outputPortCount = _gpioController.GetOutputPortCount();
                
                for (uint port = 1; port <= outputPortCount; port++)
                {
                    uint pinCount = _gpioController.GetOutputPinCount(port);
                    Console.WriteLine($"端口 {port} 有 {pinCount} 个引脚");
                    
                    // 逐个点亮引脚
                    for (uint pin = 1; pin <= pinCount; pin++)
                    {
                        // 设置为高电平
                        _gpioController.SetOutputPinLevel(port, pin, SMTGPIOController.HIGH_LEVEL);
                        Console.WriteLine($"端口 {port} 引脚 {pin} 设置为高电平");
                        
                        await Task.Delay(500); // 延时500ms
                        
                        // 设置为低电平
                        _gpioController.SetOutputPinLevel(port, pin, SMTGPIOController.LOW_LEVEL);
                        Console.WriteLine($"端口 {port} 引脚 {pin} 设置为低电平");
                        
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"引脚控制演示出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取输入状态
        /// </summary>
        private async Task ReadInputStatus()
        {
            try
            {
                Console.WriteLine("开始读取输入状态...");
                
                var pinLevels = _gpioController.GetAllInputPinLevels();
                
                foreach (var kvp in pinLevels)
                {
                    string pinInfo = kvp.Key; // "端口-引脚" 格式
                    uint level = kvp.Value;
                    string levelStr = level == SMTGPIOController.HIGH_LEVEL ? "高电平" : "低电平";
                    
                    Console.WriteLine($"输入引脚 {pinInfo}: {levelStr}");
                }
                
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取输入状态出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 点胶检测流程中的IO控制示例
        /// </summary>
        public async Task GlueInspectionIOControl()
        {
            try
            {
                // 初始化IO板
                if (!_gpioController.Initialize())
                {
                    throw new Exception("IO板初始化失败");
                }

                Console.WriteLine("点胶检测IO控制流程开始...");

                // 1. 检测开始信号 - 设置准备就绪指示灯
                _gpioController.SetOutputPinLevel(1, 1, SMTGPIOController.HIGH_LEVEL);
                Console.WriteLine("设置准备就绪指示灯");

                // 2. 等待启动信号
                Console.WriteLine("等待启动信号...");
                while (true)
                {
                    uint startSignal = _gpioController.GetInputPinLevel(1, 1);
                    if (startSignal == SMTGPIOController.HIGH_LEVEL)
                    {
                        Console.WriteLine("收到启动信号");
                        break;
                    }
                    await Task.Delay(100);
                }

                // 3. 开始检测 - 设置检测中指示灯
                _gpioController.SetOutputPinLevel(1, 2, SMTGPIOController.HIGH_LEVEL);
                Console.WriteLine("设置检测中指示灯");

                // 4. 模拟检测过程
                Console.WriteLine("模拟检测过程...");
                await Task.Delay(2000);

                // 5. 检测完成 - 根据结果设置输出
                bool inspectionPassed = true; // 假设检测通过

                if (inspectionPassed)
                {
                    // 检测通过 - 绿灯
                    _gpioController.SetOutputPinLevel(1, 3, SMTGPIOController.HIGH_LEVEL);
                    Console.WriteLine("检测通过 - 绿灯亮起");
                }
                else
                {
                    // 检测失败 - 红灯
                    _gpioController.SetOutputPinLevel(1, 4, SMTGPIOController.HIGH_LEVEL);
                    Console.WriteLine("检测失败 - 红灯亮起");
                }

                // 6. 关闭检测中指示灯
                _gpioController.SetOutputPinLevel(1, 2, SMTGPIOController.LOW_LEVEL);

                // 7. 等待复位信号
                Console.WriteLine("等待复位信号...");
                await Task.Delay(3000);

                // 8. 复位所有输出
                _gpioController.SetAllOutputPinsLow();
                Console.WriteLine("复位所有输出");

                Console.WriteLine("点胶检测IO控制流程完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"点胶检测IO控制出错: {ex.Message}");
                // 确保复位所有输出
                try
                {
                    _gpioController?.SetAllOutputPinsLow();
                }
                catch { }
            }
        }

        /// <summary>
        /// 连续监控输入状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ContinuousInputMonitoring(CancellationToken cancellationToken)
        {
            try
            {
                if (!_gpioController.Initialize())
                {
                    throw new Exception("IO板初始化失败");
                }

                _isRunning = true;
                Console.WriteLine("开始连续监控输入状态...");

                while (_isRunning && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var inputLevels = _gpioController.GetAllInputPinLevels();
                        
                        // 检查特定输入引脚的状态变化
                        foreach (var kvp in inputLevels)
                        {
                            string pinInfo = kvp.Key;
                            uint level = kvp.Value;
                            
                            // 根据具体需求处理输入状态
                            if (level == SMTGPIOController.HIGH_LEVEL)
                            {
                                Console.WriteLine($"检测到输入信号: {pinInfo}");
                                // 在这里处理特定的输入事件
                                await HandleInputEvent(pinInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"监控输入状态时出错: {ex.Message}");
                    }

                    await Task.Delay(200, cancellationToken); // 200ms轮询间隔
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("输入监控已取消");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连续输入监控出错: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// 处理输入事件
        /// </summary>
        /// <param name="pinInfo">引脚信息</param>
        private async Task HandleInputEvent(string pinInfo)
        {
            switch (pinInfo)
            {
                case "1-1": // 端口1引脚1
                    Console.WriteLine("处理启动信号");
                    // 触发检测流程
                    break;
                    
                case "1-2": // 端口1引脚2
                    Console.WriteLine("处理停止信号");
                    // 停止当前操作
                    break;
                    
                case "1-3": // 端口1引脚3
                    Console.WriteLine("处理复位信号");
                    // 复位系统状态
                    _gpioController.SetAllOutputPinsLow();
                    break;
                    
                default:
                    Console.WriteLine($"未处理的输入信号: {pinInfo}");
                    break;
            }
            
            await Task.Delay(50); // 防抖延时
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopMonitoring()
        {
            _isRunning = false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _isRunning = false;
            _gpioController?.Dispose();
        }
    }
} 