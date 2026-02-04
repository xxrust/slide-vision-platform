using System;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// SMTGPIO测试工具
    /// 用于验证不同的调用方式
    /// </summary>
    public static class TestGPIO
    {
        /// <summary>
        /// 运行SMTGPIO测试
        /// </summary>
        public static void RunTest()
        {
            Console.WriteLine("=== SMTGPIO 调用方式测试 ===");
            Console.WriteLine();

            // 测试修复版本
            Console.WriteLine("1. 测试修复版本控制器...");
            TestFixedVersion();

            Console.WriteLine();
            Console.WriteLine("2. 测试原始版本控制器...");
            TestOriginalVersion();

            Console.WriteLine();
            Console.WriteLine("=== 测试完成 ===");
        }

        private static void TestFixedVersion()
        {
            try
            {
                using (var controller = new SMTGPIOController_FixedVersion())
                {
                    bool success = controller.Initialize();
                    if (success)
                    {
                        Console.WriteLine("✓ 修复版本初始化成功！");
                        controller.TestOutput();
                    }
                    else
                    {
                        Console.WriteLine("✗ 修复版本初始化失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 修复版本测试异常: {ex.Message}");
                Console.WriteLine($"详细错误: {ex}");
            }
        }

        private static void TestOriginalVersion()
        {
            try
            {
                using (var controller = new SMTGPIOController())
                {
                    bool success = controller.Initialize();
                    if (success)
                    {
                        Console.WriteLine("✓ 原始版本初始化成功！");
                        
                        // 测试基本功能
                        var outputPorts = controller.GetOutputPortCount();
                        var inputPorts = controller.GetInputPortCount();
                        Console.WriteLine($"输出端口数: {outputPorts}, 输入端口数: {inputPorts}");
                    }
                    else
                    {
                        Console.WriteLine("✗ 原始版本初始化失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 原始版本测试异常: {ex.Message}");
                
                // 如果有详细诊断信息，显示关键部分
                if (ex.Message.Contains("详细诊断信息"))
                {
                    // 只显示前几行的诊断信息，避免输出过长
                    string[] lines = ex.Message.Split('\n');
                    for (int i = 0; i < Math.Min(lines.Length, 20); i++)
                    {
                        if (lines[i].Contains("【") || lines[i].Contains("✓") || lines[i].Contains("✗"))
                        {
                            Console.WriteLine($"  {lines[i]}");
                        }
                    }
                }
            }
        }
    }
} 