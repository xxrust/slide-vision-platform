using System;
using System.IO;
using WpfApp2.SMTGPIO;

namespace WpfApp2.SMTGPIO.DiagnosticTool
{
    /// <summary>
    /// SMTGPIO独立诊断工具
    /// 可以在目标机器上运行，生成详细的环境诊断报告
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SMTGPIO 诊断工具 v1.0");
            Console.WriteLine("====================");
            Console.WriteLine();

            try
            {
                // 运行完整诊断
                string report = SystemDiagnostic.RunFullDiagnostic();
                
                // 显示报告
                Console.WriteLine(report);
                
                // 保存报告到文件
                string reportPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    $"SMTGPIO_Diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                );
                
                File.WriteAllText(reportPath, report);
                Console.WriteLine($"\n诊断报告已保存到: {reportPath}");
                
                // 尝试初始化SMTGPIO
                Console.WriteLine("\n=== 尝试初始化SMTGPIO ===");
                TestSMTGPIOInitialization();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"诊断过程中发生错误: {ex.Message}");
                Console.WriteLine($"详细错误: {ex}");
            }
            
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
        
        private static void TestSMTGPIOInitialization()
        {
            try
            {
                using (var controller = new SMTGPIOController())
                {
                    Console.WriteLine("正在尝试初始化SMTGPIO...");
                    bool success = controller.Initialize();
                    
                    if (success)
                    {
                        Console.WriteLine("✓ SMTGPIO初始化成功！");
                        
                        // 获取基本信息
                        var outputPorts = controller.GetOutputPortCount();
                        var inputPorts = controller.GetInputPortCount();
                        
                        Console.WriteLine($"  输出端口数: {outputPorts}");
                        Console.WriteLine($"  输入端口数: {inputPorts}");
                        
                        // 测试设置输出
                        Console.WriteLine("正在测试输出功能...");
                        controller.SetAllOutputPinsLow();
                        Console.WriteLine("✓ 输出测试完成");
                    }
                    else
                    {
                        Console.WriteLine("✗ SMTGPIO初始化失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ SMTGPIO初始化异常: {ex.Message}");
                
                // 如果是常见错误，提供具体建议
                if (ex.Message.Contains("-1"))
                {
                    Console.WriteLine("\n【错误代码-1的常见解决方案】");
                    Console.WriteLine("1. 安装SMTGPIO驱动程序");
                    Console.WriteLine("2. 确保硬件设备已连接");
                    Console.WriteLine("3. 以管理员身份运行程序");
                    Console.WriteLine("4. 检查设备是否被其他程序占用");
                    Console.WriteLine("5. 安装Visual C++ Redistributable 2015-2019 (x64)");
                }
            }
        }
    }
} 