using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// Null PLC控制器实现 - 用于PLC硬件不可用时
    /// </summary>
    public sealed class NullPlcController : IPlcController
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public int Timeout { get; set; } = 5000;
        public int WriteTimeout { get; set; } = 100;
        public bool IsConnected => false;
        public Dictionary<string, object> PLCDataDict => new Dictionary<string, object>();
        public string ErrorMessage => "PLC disabled.";

        public event Action<string> LogMessageEvent;
        public event Action<bool> ConnectionStatusChanged;

        public void ConfigureConnection(string portName = "COM1", int baudRate = 9600, int timeout = 5000)
        {
            // No-op
        }

        public bool Connect()
        {
            return false;
        }

        public void Disconnect()
        {
            // No-op
        }

        public Task<int> ReadSingleAsync(string type = "", int number = 0, string format = "", string addrCombine = "")
        {
            return Task.FromResult(0);
        }

        public Task<int[]> ReadMultipleAsync(string type = "", int startNumber = 0, string format = "", int count = 2, string addrCombine = "")
        {
            return Task.FromResult(new int[count]);
        }

        public int ReadSingle(string type = "", int number = 0, string format = "", string addrCombine = "")
        {
            return 0;
        }

        public int[] ReadMultiple(string type = "", int startNumber = 0, string format = "", int count = 2, string addrCombine = "")
        {
            return new int[count];
        }

        public Task<bool> WriteSingleAsync(string type = "", int number = 0, string format = "", int data = 0, string addrCombine = "")
        {
            return Task.FromResult(false);
        }

        public Task<bool> WriteSingleUnsignedAsync(string address, uint data)
        {
            return Task.FromResult(false);
        }

        public bool WriteSingle(string type = "", int number = 0, string format = "", int data = 0, string addrCombine = "")
        {
            return false;
        }

        public Task<bool> SetRelayAsync(string address)
        {
            return Task.FromResult(false);
        }

        public Task<bool> ResetRelayAsync(string address)
        {
            return Task.FromResult(false);
        }

        public Task<bool> ToggleRelayAsync(string address)
        {
            return Task.FromResult(false);
        }

        public bool SetRelay(string address)
        {
            return false;
        }

        public bool ResetRelay(string address)
        {
            return false;
        }

        public bool ToggleRelay(string address)
        {
            return false;
        }

        public void WriteSingleUIAsync(string address, int data, Action<bool> onSuccess = null, Action<string> onError = null)
        {
            onError?.Invoke("PLC disabled.");
        }

        public Task RunTestAsync()
        {
            return Task.CompletedTask;
        }

        public void RunTest()
        {
            // No-op
        }

        public string GetConnectionStatusReport()
        {
            return "PLC disabled.";
        }

        public void ResetConnectionStatus()
        {
            // No-op
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
