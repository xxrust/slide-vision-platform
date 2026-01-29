using System;
using System.Threading;
using System.Threading.Tasks;
using SampleWrapper.SMTGPIO;

namespace SampleWrapper.SMTGPIO
{
    /// <summary>
    /// SMTGPIO测试程序
    /// 用于测试IO板控制功能
    /// </summary>
    public class TestProgram
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("SMTGPIO IO板控制测试程序");
            Console.WriteLine("==============================");

            var example = new SMTGPIOExample();

            try
            {
                // 选择测试模式
                Console.WriteLine("请选择测试模式:");
                Console.WriteLine("1. 基本功能测试");
                Console.WriteLine("2. 点胶检测流程测试");
                Console.WriteLine("3. 连续输入监控测试");
                Console.WriteLine("4. 手动控制测试");
                Console.Write("请输入选择 (1-4): ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await RunBasicTest(example);
                        break;
                    case "2":
                        await RunGlueInspectionTest(example);
                        break;
                    case "3":
                        await RunContinuousMonitoringTest(example);
                        break;
                    case "4":
                        await RunManualControlTest();
                        break;
                    default:
                        Console.WriteLine("无效选择，运行基本功能测试");
                        await RunBasicTest(example);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"程序执行出错: {ex.Message}");
                Console.WriteLine($"详细错误信息: {ex}");
            }
            finally
            {
                example.Dispose();
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// 运行基本功能测试
        /// </summary>
        private static async Task RunBasicTest(SMTGPIOExample example)
        {
            Console.WriteLine("\n=== 基本功能测试 ===");
            await example.BasicUsageExample();
        }

        /// <summary>
        /// 运行点胶检测流程测试
        /// </summary>
        private static async Task RunGlueInspectionTest(SMTGPIOExample example)
        {
            Console.WriteLine("\n=== 点胶检测流程测试 ===");
            await example.GlueInspectionIOControl();
        }

        /// <summary>
        /// 运行连续监控测试
        /// </summary>
        private static async Task RunContinuousMonitoringTest(SMTGPIOExample example)
        {
            Console.WriteLine("\n=== 连续输入监控测试 ===");
            Console.WriteLine("监控将持续10秒，按 Ctrl+C 可提前停止");

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var monitoringTask = example.ContinuousInputMonitoring(cts.Token);
                
                // 监听Ctrl+C
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    example.StopMonitoring();
                };

                await monitoringTask;
            }
        }

        /// <summary>
        /// 运行手动控制测试
        /// </summary>
        private static async Task RunManualControlTest()
        {
            Console.WriteLine("\n=== 手动控制测试 ===");
            
            using (var controller = new SMTGPIOController())
            {
                try
                {
                    // 初始化
                    if (!controller.Initialize())
                    {
                        Console.WriteLine("设备初始化失败");
                        return;
                    }

                    Console.WriteLine("设备初始化成功");
                    Console.WriteLine("输入命令进行手动控制:");
                    Console.WriteLine("  set <端口> <引脚> <电平>  - 设置输出引脚 (电平: 0=低, 1=高)");
                    Console.WriteLine("  get <端口> <引脚>        - 读取输入引脚");
                    Console.WriteLine("  info                    - 显示端口信息");
                    Console.WriteLine("  reset                   - 复位所有输出");
                    Console.WriteLine("  exit                    - 退出");

                    while (true)
                    {
                        Console.Write("\n> ");
                        string input = Console.ReadLine();
                        
                        if (string.IsNullOrEmpty(input))
                            continue;

                        string[] parts = input.Split(' ');
                        string command = parts[0].ToLower();

                        try
                        {
                            switch (command)
                            {
                                case "set":
                                    if (parts.Length >= 4)
                                    {
                                        uint port = uint.Parse(parts[1]);
                                        uint pin = uint.Parse(parts[2]);
                                        uint level = uint.Parse(parts[3]);
                                        
                                        controller.SetOutputPinLevel(port, pin, level);
                                        Console.WriteLine($"已设置端口{port}引脚{pin}为{(level == 1 ? "高" : "低")}电平");
                                    }
                                    else
                                    {
                                        Console.WriteLine("用法: set <端口> <引脚> <电平>");
                                    }
                                    break;

                                case "get":
                                    if (parts.Length >= 3)
                                    {
                                        uint port = uint.Parse(parts[1]);
                                        uint pin = uint.Parse(parts[2]);
                                        
                                        uint level = controller.GetInputPinLevel(port, pin);
                                        Console.WriteLine($"端口{port}引脚{pin}当前为{(level == 1 ? "高" : "低")}电平");
                                    }
                                    else
                                    {
                                        Console.WriteLine("用法: get <端口> <引脚>");
                                    }
                                    break;

                                case "info":
                                    uint outputPorts = controller.GetOutputPortCount();
                                    uint inputPorts = controller.GetInputPortCount();
                                    
                                    Console.WriteLine($"输出端口数量: {outputPorts}");
                                    Console.WriteLine($"输入端口数量: {inputPorts}");
                                    
                                    for (uint i = 1; i <= outputPorts; i++)
                                    {
                                        uint pins = controller.GetOutputPinCount(i);
                                        Console.WriteLine($"  输出端口{i}: {pins}个引脚");
                                    }
                                    
                                    for (uint i = 1; i <= inputPorts; i++)
                                    {
                                        uint pins = controller.GetInputPinCount(i);
                                        Console.WriteLine($"  输入端口{i}: {pins}个引脚");
                                    }
                                    break;

                                case "reset":
                                    controller.SetAllOutputPinsLow();
                                    Console.WriteLine("已复位所有输出引脚");
                                    break;

                                case "exit":
                                    return;

                                default:
                                    Console.WriteLine("未知命令");
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"命令执行出错: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"手动控制测试出错: {ex.Message}");
                }
            }
        }
    }
} 