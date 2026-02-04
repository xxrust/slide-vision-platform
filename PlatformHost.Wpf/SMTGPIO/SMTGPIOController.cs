using System;
using System.Runtime.InteropServices;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// SMTGPIO设备类型枚举
    /// </summary>
    public enum SMTGPIODeviceType
    {
        SCI_Q2 = 0,
        SCI_Q2C,
        SCI_Q2D,
        SCI_Q3,
        SCI_X3,
        SCI_M3,
        SCI_EVC2_2,
        SCI_EVC2_5,
        SCI_EVC3_5,
        SCI_PCI1370U,
        SCI_ISKUNS01,
        ADVANTECH_PCI1730,
        ADVANTECH_PCIE1756,
        LEADSHINE_IOC0640,
        A118_IO,
    }

    /// <summary>
    /// SMTGPIO IO板控制器
    /// 用于控制数字输入输出端口
    /// </summary>
    public class SMTGPIOController : IDisposable
    {
        #region P/Invoke 声明

        private const string DLL_NAME = "SMTGPIO.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long ScanDevices(ref uint size, IntPtr deviceInfo);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long DevInfoRealse(uint size, IntPtr deviceInfo);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long InitByType(ref long handle, int curDevType);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern long InitByString(ref long handle, string deviceInfo);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long IsInit(long handle, ref bool status);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long Uninit(long handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long GetOutputPortNum(long handle, ref uint num);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long GetOutputPinNum(long handle, uint GPOport, ref uint num);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long GetInputPortNum(long handle, ref uint num);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long GetInputPinNum(long handle, uint GPIport, ref uint num);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long SetOutputPortLevel(long handle, uint GPOport, uint level);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long GetOutputPortLevel(long handle, uint GPOport, ref uint level);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long GetInputPortLevel(long handle, uint GPIport, ref uint level);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long SetOutputPinLevel(long handle, uint GPOport, uint pin, uint level);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long GetOutputPinLevel(long handle, uint GPOport, uint pin, ref uint level);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern long GetInputPinLevel(long handle, uint GPIport, uint pin, ref uint level);

        #endregion

        #region 常量定义

        public const uint HIGH_LEVEL = 1;
        public const uint LOW_LEVEL = 0;

        #endregion

        #region 私有字段

        private long _handle = 0;
        private bool _isInitialized = false;
        private bool _disposed = false;

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 设备句柄
        /// </summary>
        public long Handle => _handle;

        #endregion

        #region 构造函数和析构函数

        public SMTGPIOController()
        {
        }

        ~SMTGPIOController()
        {
            Dispose(false);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 通过设备类型初始化GPIO设备
        /// </summary>
        /// <param name="deviceType">设备类型</param>
        /// <returns>是否成功</returns>
        public bool Initialize(SMTGPIODeviceType deviceType = SMTGPIODeviceType.SCI_EVC3_5)
        {
            try
            {
                if (_isInitialized)
                {
                    return true;
                }

                long result = InitByType(ref _handle, (int)deviceType);
                _isInitialized = result == 0;
                
                if (_isInitialized)
                {
                    // 初始化成功后，复位所有输出
                    SetAllOutputPinsLow();
                }
                
                return _isInitialized;
            }
            catch (Exception ex)
            {
                throw new Exception($"初始化SMTGPIO设备失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 通过设备信息字符串初始化GPIO设备
        /// </summary>
        /// <param name="deviceInfo">设备信息</param>
        /// <returns>是否成功</returns>
        public bool Initialize(string deviceInfo)
        {
            try
            {
                if (_isInitialized)
                {
                    return true;
                }

                long result = InitByString(ref _handle, deviceInfo);
                _isInitialized = result == 0;
                
                if (_isInitialized)
                {
                    // 初始化成功后，复位所有输出
                    SetAllOutputPinsLow();
                }
                
                return _isInitialized;
            }
            catch (Exception ex)
            {
                throw new Exception($"初始化SMTGPIO设备失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取输出端口数量
        /// </summary>
        /// <returns>端口数量，失败返回-1</returns>
        public int GetOutputPortCount()
        {
            if (!_isInitialized) return -1;

            uint num = 0;
            long result = GetOutputPortNum(_handle, ref num);
            return result == 0 ? (int)num : -1;
        }

        /// <summary>
        /// 获取输入端口数量
        /// </summary>
        /// <returns>端口数量，失败返回-1</returns>
        public int GetInputPortCount()
        {
            if (!_isInitialized) return -1;

            uint num = 0;
            long result = GetInputPortNum(_handle, ref num);
            return result == 0 ? (int)num : -1;
        }

        /// <summary>
        /// 获取指定输出端口的引脚数量
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <returns>引脚数量，失败返回-1</returns>
        public int GetOutputPinCount(uint port)
        {
            if (!_isInitialized) return -1;

            uint num = 0;
            long result = GetOutputPinNum(_handle, port, ref num);
            return result == 0 ? (int)num : -1;
        }

        /// <summary>
        /// 获取指定输入端口的引脚数量
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <returns>引脚数量，失败返回-1</returns>
        public int GetInputPinCount(uint port)
        {
            if (!_isInitialized) return -1;

            uint num = 0;
            long result = GetInputPinNum(_handle, port, ref num);
            return result == 0 ? (int)num : -1;
        }

        /// <summary>
        /// 设置输出端口电平
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <param name="level">电平值</param>
        /// <returns>是否成功</returns>
        public bool SetOutputPortLevel(uint port, uint level)
        {
            if (!_isInitialized) return false;

            long result = SetOutputPortLevel(_handle, port, level);
            return result == 0;
        }

        /// <summary>
        /// 获取输出端口电平
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <returns>电平值，失败返回-1</returns>
        public int GetOutputPortLevel(uint port)
        {
            if (!_isInitialized) return -1;

            uint level = 0;
            long result = GetOutputPortLevel(_handle, port, ref level);
            return result == 0 ? (int)level : -1;
        }

        /// <summary>
        /// 获取输入端口电平
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <returns>电平值，失败返回-1</returns>
        public int GetInputPortLevel(uint port)
        {
            if (!_isInitialized) return -1;

            uint level = 0;
            long result = GetInputPortLevel(_handle, port, ref level);
            return result == 0 ? (int)level : -1;
        }

        /// <summary>
        /// 设置输出引脚电平
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <param name="pin">引脚号（从1开始）</param>
        /// <param name="level">电平值</param>
        /// <returns>是否成功</returns>
        public bool SetOutputPinLevel(uint port, uint pin, uint level)
        {
            if (!_isInitialized) return false;

            long result = SetOutputPinLevel(_handle, port, pin, level);
            return result == 0;
        }

        /// <summary>
        /// 获取输出引脚电平
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <param name="pin">引脚号（从1开始）</param>
        /// <returns>电平值，失败返回-1</returns>
        public int GetOutputPinLevel(uint port, uint pin)
        {
            if (!_isInitialized) return -1;

            uint level = 0;
            long result = GetOutputPinLevel(_handle, port, pin, ref level);
            return result == 0 ? (int)level : -1;
        }

        /// <summary>
        /// 获取输入引脚电平
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <param name="pin">引脚号（从1开始）</param>
        /// <returns>电平值，失败返回-1</returns>
        public int GetInputPinLevel(uint port, uint pin)
        {
            if (!_isInitialized) return -1;

            uint level = 0;
            long result = GetInputPinLevel(_handle, port, pin, ref level);
            return result == 0 ? (int)level : -1;
        }

        /// <summary>
        /// 设置输出引脚（简化接口）
        /// </summary>
        /// <param name="port">端口号（从1开始）</param>
        /// <param name="pin">引脚号（从1开始）</param>
        /// <param name="isHigh">是否设置为高电平</param>
        public void SetOutputPin(uint port, uint pin, bool isHigh)
        {
            SetOutputPinLevel(port, pin, isHigh ? HIGH_LEVEL : LOW_LEVEL);
        }

        /// <summary>
        /// 复位所有输出引脚为低电平
        /// </summary>
        public void SetAllOutputPinsLow()
        {
            if (!_isInitialized) return;

            try
            {
                int outputPorts = GetOutputPortCount();
                if (outputPorts > 0)
                {
                    for (uint port = 1; port <= outputPorts; port++)
                    {
                        SetOutputPortLevel(port, LOW_LEVEL);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"复位输出引脚失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 根据检测结果设置IO输出
        /// IO1 = 完成信号 (检测完成时设为高电平)
        /// IO2 = 结果信号 (OK时为高电平，NG时为低电平)
        /// </summary>
        /// <param name="isOK">检测结果，true为OK，false为NG</param>
        /// <param name="port">端口号，如果不指定则使用配置文件中的端口</param>
        public void SetDetectionResult(bool isOK, uint? port = null)
        {
            if (!_isInitialized) return;

            try
            {
                // 如果没有指定端口，则从配置文件获取
                uint targetPort = port ?? GPIOConfigManager.CurrentConfig.Port;
                
                // IO1 = 完成信号，无论OK/NG都设为高电平表示检测完成
                SetOutputPinLevel(targetPort, 1, HIGH_LEVEL);
                
                // IO2 = 结果信号，OK为高电平，NG为低电平
                SetOutputPinLevel(targetPort, 2, isOK ? HIGH_LEVEL : LOW_LEVEL);
            }
            catch (Exception ex)
            {
                throw new Exception($"设置检测结果失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 释放设备资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_isInitialized)
                {
                    try
                    {
                        // 释放前先复位所有输出
                        SetAllOutputPinsLow();
                        
                        // 释放设备
                        Uninit(_handle);
                    }
                    catch (Exception)
                    {
                        // 忽略释放时的异常
                    }
                    finally
                    {
                        _isInitialized = false;
                        _handle = 0;
                    }
                }
                _disposed = true;
            }
        }

        #endregion
    }
} 