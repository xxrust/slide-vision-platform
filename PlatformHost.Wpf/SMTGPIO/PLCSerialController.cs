using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using WpfApp2.UI.Models;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// PLC串口通信控制器 - 基于Python版本优化的C#实现（单例模式）
    /// 支持三菱PLC的串口通信协议，集成断路器模式和异步通信
    /// </summary>
    public class PLCSerialController : IDisposable
    {
        #region 单例模式实现
        
        private static PLCSerialController _instance;
        private static readonly object _lockObject = new object();
        
        /// <summary>
        /// 获取PLC控制器的单例实例
        /// </summary>
        public static PLCSerialController Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new PLCSerialController();
                        }
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// 重置单例实例（主要用于测试或重新初始化）
        /// </summary>
        public static void ResetInstance()
        {
            lock (_lockObject)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }
        
        #endregion

        #region 简化失败计数

        private int _consecutiveFailures = 0;
        private const int ALERT_THRESHOLD = 5; // 减少告警频率

        #endregion

        #region 私有字段

        private SerialPort _serialPort;
        private readonly Dictionary<string, object> _plcDataDict = new Dictionary<string, object>();
        private bool _isConnected = false;
        private bool _disposed = false;
        private bool _isShuttingDown = false;

        // 默认串口参数
        private string _portName = "COM1";
        private int _baudRate = 9600;
        private int _timeout = 5000;
        private int _writeTimeout = 100;

        #endregion

        #region 公共属性

        /// <summary>
        /// 串口名称
        /// </summary>
        public string PortName
        {
            get => _portName;
            set => _portName = value;
        }

        /// <summary>
        /// 波特率
        /// </summary>
        public int BaudRate
        {
            get => _baudRate;
            set => _baudRate = value;
        }

        /// <summary>
        /// 读取超时时间(毫秒)
        /// </summary>
        public int Timeout
        {
            get => _timeout;
            set => _timeout = value;
        }

        /// <summary>
        /// 写入超时时间(毫秒)
        /// </summary>
        public int WriteTimeout
        {
            get => _writeTimeout;
            set => _writeTimeout = value;
        }

        /// <summary>
        /// 连接状态（改进版本，更准确地检测串口状态）
        /// </summary>
        public bool IsConnected 
        { 
            get 
            {
                // 如果串口为空或未打开，直接返回false
                if (_serialPort?.IsOpen != true)
                {
                    if (_isConnected)
                    {
                        _isConnected = false;
                        LogKeyMessage("串口已断开，自动更新连接状态");
                    }
                    return false;
                }
                
                return _isConnected;
            }
        }

        /// <summary>
        /// PLC数据字典
        /// </summary>
        public Dictionary<string, object> PLCDataDict
        {
            get
            {
                lock (_lockObject)
                {
                    return new Dictionary<string, object>(_plcDataDict);
                }
            }
        }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; private set; }



        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 私有构造函数（单例模式）
        /// </summary>
        private PLCSerialController()
        {
            LogKeyMessage("PLC串口控制器已初始化");
        }

        /// <summary>
        /// 配置PLC连接参数
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="timeout">超时时间</param>
        public void ConfigureConnection(string portName = "COM1", int baudRate = 9600, int timeout = 5000)
        {
            _portName = portName;
            _baudRate = baudRate;
            _timeout = timeout;
            LogKeyMessage($"PLC连接参数已配置: 串口={portName}, 波特率={baudRate}, 超时={timeout}ms");
        }

        #endregion

        #region 连接和断开

        /// <summary>
        /// 连接PLC
        /// </summary>
        /// <returns>连接是否成功</returns>
        public bool Connect()
        {
            try
            {
                // 先清理旧的串口连接
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }
                _serialPort?.Dispose();

                _serialPort = new SerialPort(_portName, _baudRate, Parity.Even, 8, StopBits.One)
                {
                    ReadTimeout = _timeout,
                    WriteTimeout = _writeTimeout,
                    Encoding = Encoding.UTF8
                };

                _serialPort.Open();

                // 发送通信开始指令，不等待响应（与Python版本保持一致）
                StartCommunicate();
                
                _isConnected = true;
                _consecutiveFailures = 0;
                ErrorMessage = null;
                
                LogKeyMessage($"PLC串口连接成功: {_portName}, 波特率: {_baudRate}");
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                ErrorMessage = ex.Message;
                
                // 连接失败时清理串口对象
                try
                {
                    _serialPort?.Dispose();
                    _serialPort = null;
                }
                catch { }
                
                LogManager.Critical($"PLC串口连接失败: {ErrorMessage}。请检查: 1.PLC是否正常开机 2.串口线是否连接正常 3.串口号和波特率是否正确 4.是否有其他程序占用串口", "PLC通信");
                return false;
            }
        }

        /// <summary>
        /// 断开PLC连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _isConnected = false;
                
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }
                
                // 释放串口资源
                _serialPort?.Dispose();
                _serialPort = null;
                
                LogKeyMessage("PLC串口已断开");
            }
            catch (Exception ex)
            {
                LogKeyMessage($"断开PLC串口时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送通信开始指令
        /// </summary>
        private void StartCommunicate()
        {
            try
            {
                // 直接发送CR指令，不等待响应（与Python版本保持一致）
                string command = "CR\r";
                byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                
                _serialPort.Write(commandBytes, 0, commandBytes.Length);
                LogKeyMessage("通信开始指令已发送");
            }
            catch (Exception ex)
            {
                LogKeyMessage($"发送通信开始指令失败: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 简单重连逻辑

        /// <summary>
        /// 记录通信失败（简化版本，不显示弹窗）
        /// </summary>
        private void RecordFailure()
        {
            _consecutiveFailures++;
            
            // 简化处理：仅记录失败次数，不显示弹窗
            // 避免读取寄存器失败时产生大量告警
            if (_consecutiveFailures == ALERT_THRESHOLD)
            {
                LogKeyMessage($"PLC通信连续失败{_consecutiveFailures}次，请检查连接状态");
            }
        }

        /// <summary>
        /// 记录通信成功
        /// </summary>
        private void RecordSuccess()
        {
            if (_consecutiveFailures > 0)
            {
                LogKeyMessage("PLC通信已恢复正常");
                _consecutiveFailures = 0;
            }
        }





        #endregion

        #region 异步通信方法

        /// <summary>
        /// 异步发送命令并接收响应
        /// </summary>
        /// <param name="command">命令字符串</param>
        /// <returns>响应字符串</returns>
        private async Task<string> SendCommandAsync(string command)
        {
            if (_serialPort?.IsOpen != true)
            {
                throw new InvalidOperationException("串口未打开");
            }

            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        // 发送命令前清空接收缓冲区，避免残留数据干扰
                        if (_serialPort.BytesToRead > 0)
                        {
                            _serialPort.ReadExisting();
                        }
                        
                        string fullCommand = command + "\r";
                        byte[] commandBytes = Encoding.UTF8.GetBytes(fullCommand);
                        
                        _serialPort.Write(commandBytes, 0, commandBytes.Length);

                        // 确保数据发送完成（减少延迟提高响应速度）
                        Thread.Sleep(5);  // 从50ms减少到5ms

                        // 读取响应直到遇到\r\n
                        string rawResponse = _serialPort.ReadTo("\r\n");
                        string response = rawResponse.Trim();
                        
                        // 记录成功
                        RecordSuccess();
                        
                        return response;
                    }
                    catch (TimeoutException)
                    {
                        RecordFailure();
                        throw new TimeoutException("PLC通信超时");
                    }
                    catch (Exception ex)
                    {
                        // 串口相关异常时重置连接状态
                        if (ex.Message.Contains("端口") || ex.Message.Contains("串口") || 
                            ex.Message.Contains("I/O") || ex.Message.Contains("连接"))
                        {
                            _isConnected = false;
                            LogKeyMessage("检测到串口断开，重置连接状态");
                        }
                        
                        RecordFailure();
                        throw new Exception($"PLC通信错误: {ex.Message}");
                    }
                }
            });
        }

        #endregion

        #region 异步读取操作

        /// <summary>
        /// 异步读取单个寄存器
        /// </summary>
        /// <param name="type">寄存器类型(如R, MR等)</param>
        /// <param name="number">寄存器编号</param>
        /// <param name="format">格式(如.L等)</param>
        /// <param name="addrCombine">完整地址(可选)</param>
        /// <returns>读取的值</returns>
        public async Task<int> ReadSingleAsync(string type = "", int number = 0, string format = "", string addrCombine = "")
        {
            try
            {
                string command;
                if (!string.IsNullOrEmpty(addrCombine))
                {
                    command = $"RD {addrCombine}";
                }
                else
                {
                    command = $"RD {type}{number}{format}";
                }

                string response = await SendCommandAsync(command);
                
                // 严格按照Python逻辑：直接转换为整数
                if (int.TryParse(response, out int value))
                {
                    string key = !string.IsNullOrEmpty(addrCombine) ? addrCombine : $"{type}{number}";
                    lock (_lockObject)
                    {
                        _plcDataDict[key] = value;
                    }
                    return value;
                }
                else
                {
                    throw new Exception($"PLC响应不是数字格式: '{response}'");
                }
            }
            catch (Exception ex)
            {
                // 只在关键错误时记录日志
                if (ex.Message.Contains("断路器") || ex.Message.Contains("串口未打开"))
                {
                    LogKeyMessage($"读取PLC寄存器失败: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// 异步读取多个连续寄存器
        /// </summary>
        /// <param name="type">寄存器类型</param>
        /// <param name="startNumber">起始编号</param>
        /// <param name="format">格式</param>
        /// <param name="count">读取数量</param>
        /// <param name="addrCombine">完整地址(可选)</param>
        /// <returns>读取的值数组</returns>
        public async Task<int[]> ReadMultipleAsync(string type = "", int startNumber = 0, string format = "", int count = 2, string addrCombine = "")
        {
            try
            {
                string command;
                if (!string.IsNullOrEmpty(addrCombine))
                {
                    command = $"RDS {addrCombine} {count}";
                }
                else
                {
                    command = $"RDS {type}{startNumber}{format} {count}";
                }

                string response = await SendCommandAsync(command);
                string[] responseArray = response.Split(' ');

                if (responseArray.Length != count)
                {
                    throw new Exception($"读取数据数量不匹配，期望{count}个，实际{responseArray.Length}个");
                }

                int[] results = new int[count];
                for (int i = 0; i < count; i++)
                {
                    if (!int.TryParse(responseArray[i], out results[i]))
                    {
                        throw new Exception($"无法解析第{i + 1}个数据: {responseArray[i]}");
                    }

                    // 计算实际地址并存储到字典
                    int actualAddress;
                    if (type == "" || type == "R" || type == "MR")
                    {
                        actualAddress = (startNumber + i) / 16 * 100 + (startNumber + i) % 16;
                    }
                    else if (format == ".L")
                    {
                        actualAddress = startNumber + 2 * i;
                    }
                    else
                    {
                        actualAddress = startNumber + i;
                    }

                    string key = $"{type}{actualAddress}";
                    lock (_lockObject)
                    {
                        _plcDataDict[key] = results[i];
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                // 只在关键错误时记录日志
                if (ex.Message.Contains("断路器") || ex.Message.Contains("串口未打开"))
                {
                    LogKeyMessage($"读取多个PLC寄存器失败: {ex.Message}");
                }
                throw;
            }
        }

        #endregion

        #region 同步方法兼容性（内部调用异步）

        /// <summary>
        /// 读取单个寄存器（同步方法，内部调用异步）
        /// </summary>
        public int ReadSingle(string type = "", int number = 0, string format = "", string addrCombine = "")
        {
            try
            {
                return ReadSingleAsync(type, number, format, addrCombine).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// 读取多个连续寄存器（同步方法，内部调用异步）
        /// </summary>
        public int[] ReadMultiple(string type = "", int startNumber = 0, string format = "", int count = 2, string addrCombine = "")
        {
            try
            {
                return ReadMultipleAsync(type, startNumber, format, count, addrCombine).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        #endregion

        #region 写入操作

        /// <summary>
        /// 异步写入单个寄存器
        /// </summary>
        /// <param name="type">寄存器类型</param>
        /// <param name="number">寄存器编号</param>
        /// <param name="format">格式</param>
        /// <param name="data">要写入的数据</param>
        /// <param name="addrCombine">完整地址(可选)</param>
        /// <returns>是否成功</returns>
        public async Task<bool> WriteSingleAsync(string type = "", int number = 0, string format = "", int data = 0, string addrCombine = "")
        {
            try
            {
                string command;
                if (!string.IsNullOrEmpty(addrCombine))
                {
                    command = $"WR {addrCombine} {data}";
                }
                else
                {
                    command = $"WR {type}{number}{format} {data}";
                }

                string response = await SendCommandAsync(command);
                return response == "OK";
            }
            catch (Exception ex)
            {
                // 只在关键错误时记录日志
                if (ex.Message.Contains("断路器") || ex.Message.Contains("串口未打开"))
                {
                    LogKeyMessage($"写入PLC寄存器失败: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// 异步写入单个寄存器（无符号32位，适用于写入浮点/位模式数据）
        /// </summary>
        /// <param name="address">完整地址（例如DM908.D）</param>
        /// <param name="data">无符号32位数据</param>
        /// <returns>是否成功</returns>
        public async Task<bool> WriteSingleUnsignedAsync(string address, uint data)
        {
            try
            {
                string command = $"WR {address} {data}";
                string response = await SendCommandAsync(command);
                return response == "OK";
            }
            catch (Exception ex)
            {
                // 只在关键错误时记录日志
                if (ex.Message.Contains("断路器") || ex.Message.Contains("串口未打开"))
                {
                    LogKeyMessage($"写入PLC寄存器失败: {ex.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// 写入单个寄存器（同步方法，内部调用异步）
        /// </summary>
        public bool WriteSingle(string type = "", int number = 0, string format = "", int data = 0, string addrCombine = "")
        {
            try
            {
                return WriteSingleAsync(type, number, format, data, addrCombine).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        #endregion

        #region 便捷操作方法

        /// <summary>
        /// 设置继电器(置位)
        /// </summary>
        /// <param name="address">继电器地址</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SetRelayAsync(string address)
        {
            return await WriteSingleAsync(addrCombine: address, data: 1);
        }

        /// <summary>
        /// 复位继电器(复位)
        /// </summary>
        /// <param name="address">继电器地址</param>
        /// <returns>是否成功</returns>
        public async Task<bool> ResetRelayAsync(string address)
        {
            return await WriteSingleAsync(addrCombine: address, data: 0);
        }

        /// <summary>
        /// 翻转继电器状态
        /// </summary>
        /// <param name="address">继电器地址</param>
        /// <returns>是否成功</returns>
        public async Task<bool> ToggleRelayAsync(string address)
        {
            try
            {
                int currentStatus = await ReadSingleAsync(addrCombine: address);
                return currentStatus == 1 ? await ResetRelayAsync(address) : await SetRelayAsync(address);
            }
            catch (Exception ex)
            {
                LogKeyMessage($"翻转继电器状态失败: {ex.Message}");
                return false;
            }
        }

        // 同步版本（兼容性）
        public bool SetRelay(string address) => SetRelayAsync(address).GetAwaiter().GetResult();
        public bool ResetRelay(string address) => ResetRelayAsync(address).GetAwaiter().GetResult();
        public bool ToggleRelay(string address) => ToggleRelayAsync(address).GetAwaiter().GetResult();

        /// <summary>
        /// UI线程安全的异步写入方法（用于解决界面卡死问题）
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="data">写入数据</param>
        /// <param name="onSuccess">成功回调</param>
        /// <param name="onError">错误回调</param>
        public async void WriteSingleUIAsync(string address, int data, Action<bool> onSuccess = null, Action<string> onError = null)
        {
            try
            {
                bool result = await WriteSingleAsync(addrCombine: address, data: data);
                
                // 在UI线程中执行回调
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    onSuccess?.Invoke(result);
                }));
            }
            catch (Exception ex)
            {
                // 在UI线程中执行错误回调
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    onError?.Invoke(ex.Message);
                }));
            }
        }

        #endregion

        #region 测试方法

        /// <summary>
        /// 执行测试操作
        /// </summary>
        public async Task RunTestAsync()
        {
            try
            {
                LogKeyMessage("开始PLC通信测试...");

                // 读取测试
                await ReadMultipleAsync(type: "R", startNumber: 1, count: 20);
                LogKeyMessage("读取R1-R20完成");

                // 写入测试
                bool writeResult = await WriteSingleAsync(type: "MR", number: 2000, data: 1);
                LogKeyMessage($"写入MR2000=1: {(writeResult ? "成功" : "失败")}");

                // 继电器操作测试
                bool setResult = await SetRelayAsync("MR1915");
                LogKeyMessage($"设置MR1915: {(setResult ? "成功" : "失败")}");

                bool resetResult = await ResetRelayAsync("MR2000");
                LogKeyMessage($"复位MR2000: {(resetResult ? "成功" : "失败")}");

                LogKeyMessage("PLC通信测试完成");
            }
            catch (Exception ex)
            {
                LogKeyMessage($"PLC测试失败: {ex.Message}");
            }
        }

        public void RunTest() => RunTestAsync().GetAwaiter().GetResult();

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 获取可用的串口列表
        /// </summary>
        /// <returns>串口名称数组</returns>
        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// 测试串口连接（使用单例实例）
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <returns>测试结果</returns>
        public static bool TestConnection(string portName, int baudRate = 9600)
        {
            try
            {
                var instance = Instance;
                
                // 如果当前已连接到相同的串口，直接返回true
                if (instance.IsConnected && instance.PortName == portName && instance.BaudRate == baudRate)
                {
                    return true;
                }
                
                // 断开当前连接
                if (instance.IsConnected)
                {
                    instance.Disconnect();
                }
                
                // 配置新的连接参数并测试连接
                instance.ConfigureConnection(portName, baudRate);
                return instance.Connect();
            }
            catch (Exception ex)
            {
                LogManager.Error($"测试PLC连接失败: {ex.Message}", "PLC串口");
                return false;
            }
        }

        #endregion

        #region 日志和事件

        /// <summary>
        /// 日志事件
        /// </summary>
        public event Action<string> LogMessageEvent;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<bool> ConnectionStatusChanged;

        /// <summary>
        /// 记录关键信息日志（只记录重要信息）
        /// </summary>
        /// <param name="message">日志消息</param>
        private void LogKeyMessage(string message)
        {
            // 使用统一日志管理器
            if (message.Contains("失败") || message.Contains("错误") || message.Contains("异常") || message.Contains("断路器") || message.Contains("告警"))
            {
                LogManager.Error(message, "PLC串口");
            }
            else
            {
                LogManager.Info(message, "PLC串口");
            }
            
            // 保持事件通知兼容性
            LogMessageEvent?.Invoke($"[PLC串口] {message}");
        }

        #endregion

        #region IDisposable实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 设置关闭标志，阻止弹出错误提示
                    _isShuttingDown = true;
                    
                    LogKeyMessage("PLC串口控制器正在释放资源");
                    Disconnect();
                    _serialPort?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~PLCSerialController()
        {
            Dispose(false);
        }

        #endregion

        #region 性能监控和状态报告

        /// <summary>
        /// 获取连接状态报告
        /// </summary>
        public string GetConnectionStatusReport()
        {
            return $"串口连接: {(_serialPort?.IsOpen == true ? "已连接" : "未连接")}, " +
                   $"逻辑连接: {_isConnected}, " +
                   $"连续失败次数: {_consecutiveFailures}, " +
                   $"最终状态: {IsConnected}";
        }

        /// <summary>
        /// 重置连接状态（手动恢复）
        /// </summary>
        public void ResetConnectionStatus()
        {
            _consecutiveFailures = 0;
            LogKeyMessage("连接状态已手动重置");
        }



        #endregion
    }
} 
