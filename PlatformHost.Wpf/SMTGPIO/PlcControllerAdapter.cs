using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// PLCSerialController的适配器实现 - 将单例适配为接口
    /// </summary>
    public sealed class PlcControllerAdapter : IPlcController
    {
        private readonly PLCSerialController _instance;

        public PlcControllerAdapter()
        {
            _instance = PLCSerialController.Instance;
        }

        public string PortName
        {
            get => _instance.PortName;
            set => _instance.PortName = value;
        }

        public int BaudRate
        {
            get => _instance.BaudRate;
            set => _instance.BaudRate = value;
        }

        public int Timeout
        {
            get => _instance.Timeout;
            set => _instance.Timeout = value;
        }

        public int WriteTimeout
        {
            get => _instance.WriteTimeout;
            set => _instance.WriteTimeout = value;
        }

        public bool IsConnected => _instance.IsConnected;

        public Dictionary<string, object> PLCDataDict => _instance.PLCDataDict;

        public string ErrorMessage => _instance.ErrorMessage;

        public event Action<string> LogMessageEvent
        {
            add => _instance.LogMessageEvent += value;
            remove => _instance.LogMessageEvent -= value;
        }

        public event Action<bool> ConnectionStatusChanged
        {
            add => _instance.ConnectionStatusChanged += value;
            remove => _instance.ConnectionStatusChanged -= value;
        }

        public void ConfigureConnection(string portName = "COM1", int baudRate = 9600, int timeout = 5000)
        {
            _instance.ConfigureConnection(portName, baudRate, timeout);
        }

        public bool Connect()
        {
            return _instance.Connect();
        }

        public void Disconnect()
        {
            _instance.Disconnect();
        }

        public Task<int> ReadSingleAsync(string type = "", int number = 0, string format = "", string addrCombine = "")
        {
            return _instance.ReadSingleAsync(type, number, format, addrCombine);
        }

        public Task<int[]> ReadMultipleAsync(string type = "", int startNumber = 0, string format = "", int count = 2, string addrCombine = "")
        {
            return _instance.ReadMultipleAsync(type, startNumber, format, count, addrCombine);
        }

        public int ReadSingle(string type = "", int number = 0, string format = "", string addrCombine = "")
        {
            return _instance.ReadSingle(type, number, format, addrCombine);
        }

        public int[] ReadMultiple(string type = "", int startNumber = 0, string format = "", int count = 2, string addrCombine = "")
        {
            return _instance.ReadMultiple(type, startNumber, format, count, addrCombine);
        }

        public Task<bool> WriteSingleAsync(string type = "", int number = 0, string format = "", int data = 0, string addrCombine = "")
        {
            return _instance.WriteSingleAsync(type, number, format, data, addrCombine);
        }

        public Task<bool> WriteSingleUnsignedAsync(string address, uint data)
        {
            return _instance.WriteSingleUnsignedAsync(address, data);
        }

        public bool WriteSingle(string type = "", int number = 0, string format = "", int data = 0, string addrCombine = "")
        {
            return _instance.WriteSingle(type, number, format, data, addrCombine);
        }

        public Task<bool> SetRelayAsync(string address)
        {
            return _instance.SetRelayAsync(address);
        }

        public Task<bool> ResetRelayAsync(string address)
        {
            return _instance.ResetRelayAsync(address);
        }

        public Task<bool> ToggleRelayAsync(string address)
        {
            return _instance.ToggleRelayAsync(address);
        }

        public bool SetRelay(string address)
        {
            return _instance.SetRelay(address);
        }

        public bool ResetRelay(string address)
        {
            return _instance.ResetRelay(address);
        }

        public bool ToggleRelay(string address)
        {
            return _instance.ToggleRelay(address);
        }

        public void WriteSingleUIAsync(string address, int data, Action<bool> onSuccess = null, Action<string> onError = null)
        {
            _instance.WriteSingleUIAsync(address, data, onSuccess, onError);
        }

        public Task RunTestAsync()
        {
            return _instance.RunTestAsync();
        }

        public void RunTest()
        {
            _instance.RunTest();
        }

        public string GetConnectionStatusReport()
        {
            return _instance.GetConnectionStatusReport();
        }

        public void ResetConnectionStatus()
        {
            _instance.ResetConnectionStatus();
        }

        public void Dispose()
        {
            _instance.Dispose();
        }
    }
}
