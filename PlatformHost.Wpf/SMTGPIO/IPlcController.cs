using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// PLC控制器接口抽象
    /// </summary>
    public interface IPlcController : IDisposable
    {
        /// <summary>
        /// 串口名称
        /// </summary>
        string PortName { get; set; }

        /// <summary>
        /// 波特率
        /// </summary>
        int BaudRate { get; set; }

        /// <summary>
        /// 读取超时时间(毫秒)
        /// </summary>
        int Timeout { get; set; }

        /// <summary>
        /// 写入超时时间(毫秒)
        /// </summary>
        int WriteTimeout { get; set; }

        /// <summary>
        /// 连接状态
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// PLC数据字典
        /// </summary>
        Dictionary<string, object> PLCDataDict { get; }

        /// <summary>
        /// 错误信息
        /// </summary>
        string ErrorMessage { get; }

        /// <summary>
        /// 配置PLC连接参数
        /// </summary>
        void ConfigureConnection(string portName = "COM1", int baudRate = 9600, int timeout = 5000);

        /// <summary>
        /// 连接PLC
        /// </summary>
        bool Connect();

        /// <summary>
        /// 断开PLC连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 异步读取单个寄存器
        /// </summary>
        Task<int> ReadSingleAsync(string type = "", int number = 0, string format = "", string addrCombine = "");

        /// <summary>
        /// 异步读取多个连续寄存器
        /// </summary>
        Task<int[]> ReadMultipleAsync(string type = "", int startNumber = 0, string format = "", int count = 2, string addrCombine = "");

        /// <summary>
        /// 读取单个寄存器（同步）
        /// </summary>
        int ReadSingle(string type = "", int number = 0, string format = "", string addrCombine = "");

        /// <summary>
        /// 读取多个连续寄存器（同步）
        /// </summary>
        int[] ReadMultiple(string type = "", int startNumber = 0, string format = "", int count = 2, string addrCombine = "");

        /// <summary>
        /// 异步写入单个寄存器
        /// </summary>
        Task<bool> WriteSingleAsync(string type = "", int number = 0, string format = "", int data = 0, string addrCombine = "");

        /// <summary>
        /// 异步写入单个寄存器（无符号32位）
        /// </summary>
        Task<bool> WriteSingleUnsignedAsync(string address, uint data);

        /// <summary>
        /// 写入单个寄存器（同步）
        /// </summary>
        bool WriteSingle(string type = "", int number = 0, string format = "", int data = 0, string addrCombine = "");

        /// <summary>
        /// 异步设置继电器
        /// </summary>
        Task<bool> SetRelayAsync(string address);

        /// <summary>
        /// 异步复位继电器
        /// </summary>
        Task<bool> ResetRelayAsync(string address);

        /// <summary>
        /// 异步翻转继电器状态
        /// </summary>
        Task<bool> ToggleRelayAsync(string address);

        /// <summary>
        /// 设置继电器（同步）
        /// </summary>
        bool SetRelay(string address);

        /// <summary>
        /// 复位继电器（同步）
        /// </summary>
        bool ResetRelay(string address);

        /// <summary>
        /// 翻转继电器状态（同步）
        /// </summary>
        bool ToggleRelay(string address);

        /// <summary>
        /// UI线程安全的异步写入方法
        /// </summary>
        void WriteSingleUIAsync(string address, int data, Action<bool> onSuccess = null, Action<string> onError = null);

        /// <summary>
        /// 异步执行测试操作
        /// </summary>
        Task RunTestAsync();

        /// <summary>
        /// 执行测试操作（同步）
        /// </summary>
        void RunTest();

        /// <summary>
        /// 获取连接状态报告
        /// </summary>
        string GetConnectionStatusReport();

        /// <summary>
        /// 重置连接状态
        /// </summary>
        void ResetConnectionStatus();

        /// <summary>
        /// 日志事件
        /// </summary>
        event Action<string> LogMessageEvent;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        event Action<bool> ConnectionStatusChanged;
    }
}
