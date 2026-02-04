using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// SMTGPIO IO板控制器 - 修复版本
    /// 修复了参数类型和调用约定问题
    /// </summary>
    public class SMTGPIOController_FixedVersion : IDisposable
    {
        #region P/Invoke 声明 - 多种尝试方式

        private const string DLL_NAME = "SMTGPIO.dll";

        /// <summary>
        /// 设备类型枚举
        /// </summary>
        public enum DeviceType
        {
            SCI_EVC3_5 = 8  // 根据头文件，SCI_EVC3_5 = 8
        }

        // 方案1: StdCall + IntPtr (最可能正确)
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "InitByType")]
        private static extern int InitByType_StdCall(ref IntPtr handle, DeviceType deviceType);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Uninit")]
        private static extern int Uninit_StdCall(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetOutputPortNum")]
        private static extern int GetOutputPortNum_StdCall(IntPtr handle, ref uint outputPortNum);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "SetOutputPinLevel")]
        private static extern int SetOutputPinLevel_StdCall(IntPtr handle, uint portNum, uint pinNum, uint level);

        // 方案2: Cdecl + IntPtr
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "InitByType")]
        private static extern int InitByType_Cdecl(ref IntPtr handle, DeviceType deviceType);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Uninit")]
        private static extern int Uninit_Cdecl(IntPtr handle);

        // 方案3: StdCall + long long (原始尝试，但可能有问题)
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "InitByType")]
        private static extern int InitByType_LongLong(ref long handle, DeviceType deviceType);

        #endregion

        #region 私有字段

        private IntPtr _handle = IntPtr.Zero;
        private bool _isInitialized = false;
        private bool _disposed = false;

        #endregion

        #region 公共属性

        public bool IsInitialized => _isInitialized;
        public IntPtr Handle => _handle;

        #endregion

        #region 构造函数

        public SMTGPIOController_FixedVersion()
        {
        }

        ~SMTGPIOController_FixedVersion()
        {
            Dispose(false);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 尝试多种方式初始化设备
        /// </summary>
        public bool Initialize(DeviceType deviceType = DeviceType.SCI_EVC3_5)
        {
            if (_isInitialized)
            {
                return true;
            }

            Console.WriteLine("=== SMTGPIO 初始化测试（修复版） ===");
            Console.WriteLine($"目标设备类型: {deviceType} ({(int)deviceType})");
            Console.WriteLine($"应用程序目录: {AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine($"进程架构: {(Environment.Is64BitProcess ? "x64" : "x86")}");
            Console.WriteLine();

            // 检查DLL文件
            if (!CheckRequiredFiles())
            {
                throw new FileNotFoundException("缺少必需的SMTGPIO DLL文件");
            }

            // 尝试不同的调用方式
            var attempts = new[]
            {
                new { Name = "StdCall + IntPtr", Method = (Func<bool>)(() => TryInitialize_StdCall_IntPtr(deviceType)) },
                new { Name = "Cdecl + IntPtr", Method = (Func<bool>)(() => TryInitialize_Cdecl_IntPtr(deviceType)) },
                new { Name = "StdCall + LongLong", Method = (Func<bool>)(() => TryInitialize_StdCall_LongLong(deviceType)) }
            };

            foreach (var attempt in attempts)
            {
                try
                {
                    Console.WriteLine($"尝试方法: {attempt.Name}");
                    if (attempt.Method())
                    {
                        Console.WriteLine($"✓ 成功！使用方法: {attempt.Name}");
                        _isInitialized = true;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ 失败: {ex.Message}");
                }
                Console.WriteLine();
            }

            throw new Exception("所有初始化方法都失败了。这可能表明：\n" +
                "1. SMTGPIO硬件设备未连接\n" +
                "2. 驱动程序未安装\n" +
                "3. 设备被其他程序占用\n" +
                "4. 需要管理员权限");
        }

        private bool TryInitialize_StdCall_IntPtr(DeviceType deviceType)
        {
            IntPtr handle = IntPtr.Zero;
            int result = InitByType_StdCall(ref handle, deviceType);
            Console.WriteLine($"  InitByType_StdCall 返回: {result}, Handle: 0x{handle.ToInt64():X}");
            
            if (result == 0 && handle != IntPtr.Zero)
            {
                _handle = handle;
                return true;
            }
            return false;
        }

        private bool TryInitialize_Cdecl_IntPtr(DeviceType deviceType)
        {
            IntPtr handle = IntPtr.Zero;
            int result = InitByType_Cdecl(ref handle, deviceType);
            Console.WriteLine($"  InitByType_Cdecl 返回: {result}, Handle: 0x{handle.ToInt64():X}");
            
            if (result == 0 && handle != IntPtr.Zero)
            {
                _handle = handle;
                return true;
            }
            return false;
        }

        private bool TryInitialize_StdCall_LongLong(DeviceType deviceType)
        {
            long handle = 0;
            int result = InitByType_LongLong(ref handle, deviceType);
            Console.WriteLine($"  InitByType_LongLong 返回: {result}, Handle: {handle}");
            
            if (result == 0 && handle != 0)
            {
                _handle = new IntPtr(handle);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 检查必需的文件是否存在
        /// </summary>
        private bool CheckRequiredFiles()
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string[] requiredFiles = {
                "SMTGPIO.dll",
                "SMTGPIO_Driver_SV.dll",
                "SvApiLibx64.dll",
                "SvIoCtrlx64.sys"
            };

            Console.WriteLine("检查必需文件:");
            bool allExists = true;
            foreach (string fileName in requiredFiles)
            {
                string fullPath = Path.Combine(appPath, fileName);
                bool exists = File.Exists(fullPath);
                Console.WriteLine($"  {fileName}: {(exists ? "✓" : "✗")}");
                if (!exists) allExists = false;
            }
            Console.WriteLine();

            return allExists;
        }

        /// <summary>
        /// 测试输出功能
        /// </summary>
        public void TestOutput()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("设备未初始化");
            }

            try
            {
                Console.WriteLine("测试输出功能...");
                
                // 获取输出端口数量
                uint portCount = 0;
                int result = GetOutputPortNum_StdCall(_handle, ref portCount);
                Console.WriteLine($"输出端口数量: {portCount} (返回码: {result})");

                if (result == 0 && portCount > 0)
                {
                    // 测试设置第一个端口的第一个引脚
                    result = SetOutputPinLevel_StdCall(_handle, 1, 1, 0); // 设置为低电平
                    Console.WriteLine($"设置端口1引脚1为低电平: {(result == 0 ? "成功" : "失败")} (返回码: {result})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试输出功能时出错: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_isInitialized && _handle != IntPtr.Zero)
                {
                    try
                    {
                        Uninit_StdCall(_handle);
                        Console.WriteLine("设备已正确释放");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"释放设备时出错: {ex.Message}");
                    }
                }

                _handle = IntPtr.Zero;
                _isInitialized = false;
                _disposed = true;
            }
        }

        #endregion
    }
} 