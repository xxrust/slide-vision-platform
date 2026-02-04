using System;
using System.Threading.Tasks;
using WpfApp2.SMTGPIO;

namespace WpfApp2.UI.Models
{
    public sealed class KeyencePlcSerialClient : IDeviceClient, IConfigurableDeviceClient
    {
        private readonly object _syncRoot = new object();
        private DeviceConfig _config;
        private DeviceConnectionStatus _status = DeviceConnectionStatus.Disconnected;
        private string _lastError = string.Empty;

        public KeyencePlcSerialClient(DeviceConfig config)
        {
            UpdateConfig(config);
        }

        public string Id => _config?.Id ?? string.Empty;
        public string Name => _config?.Name ?? string.Empty;
        public DeviceProtocolType ProtocolType => DeviceProtocolType.Serial;

        public DeviceConnectionStatus Status
        {
            get
            {
                var connected = PLCSerialController.Instance?.IsConnected == true;
                if (!connected && _status == DeviceConnectionStatus.Connected)
                {
                    _status = DeviceConnectionStatus.Disconnected;
                }

                if (connected && _status == DeviceConnectionStatus.Disconnected)
                {
                    _status = DeviceConnectionStatus.Connected;
                }

                return _status;
            }
        }

        public string LastError => _lastError;

        public Task<DeviceOperationResult> ConnectAsync()
        {
            lock (_syncRoot)
            {
                try
                {
                    _status = DeviceConnectionStatus.Connecting;
                    var controller = PLCSerialController.Instance;
                    ApplyConfig(controller, _config?.Serial);

                    var connected = controller.Connect();
                    if (connected)
                    {
                        _status = DeviceConnectionStatus.Connected;
                        _lastError = string.Empty;
                        return Task.FromResult(DeviceOperationResult.Ok());
                    }

                    var message = controller.ErrorMessage ?? "PLC连接失败";
                    SetError(message);
                    return Task.FromResult(DeviceOperationResult.Fail(message));
                }
                catch (Exception ex)
                {
                    SetError(ex.Message);
                    return Task.FromResult(DeviceOperationResult.Fail(ex.Message));
                }
            }
        }

        public Task<DeviceOperationResult> DisconnectAsync()
        {
            lock (_syncRoot)
            {
                PLCSerialController.Instance?.Disconnect();
                _status = DeviceConnectionStatus.Disconnected;
                return Task.FromResult(DeviceOperationResult.Ok());
            }
        }

        public async Task<DeviceReadResult> ReadAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return DeviceReadResult.Fail("读取地址不能为空");
            }

            if (PLCSerialController.Instance?.IsConnected != true)
            {
                return DeviceReadResult.Fail("PLC未连接");
            }

            try
            {
                var result = await PLCSerialController.Instance.ReadSingleAsync(addrCombine: address);
                return DeviceReadResult.Ok(result);
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                return DeviceReadResult.Fail(ex.Message);
            }
        }

        public async Task<DeviceOperationResult> WriteAsync(string address, object value)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return DeviceOperationResult.Fail("写入地址不能为空");
            }

            if (PLCSerialController.Instance?.IsConnected != true)
            {
                return DeviceOperationResult.Fail("PLC未连接");
            }

            if (!TryConvertValue(value, out var intValue, out var error))
            {
                return DeviceOperationResult.Fail(error);
            }

            try
            {
                var success = await PLCSerialController.Instance.WriteSingleAsync(addrCombine: address, data: intValue);
                if (success)
                {
                    return DeviceOperationResult.Ok();
                }

                SetError("PLC写入失败");
                return DeviceOperationResult.Fail("PLC写入失败");
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                return DeviceOperationResult.Fail(ex.Message);
            }
        }

        public void UpdateConfig(DeviceConfig config)
        {
            if (config == null)
            {
                return;
            }

            _config = config.Clone();
        }

        private void ApplyConfig(PLCSerialController controller, DeviceSerialOptions options)
        {
            if (controller == null || options == null)
            {
                return;
            }

            controller.PortName = options.PortName;
            controller.BaudRate = options.BaudRate;
            controller.Timeout = options.ReadTimeout;
            controller.WriteTimeout = options.WriteTimeout;
        }

        private void SetError(string message)
        {
            _lastError = message ?? string.Empty;
            _status = DeviceConnectionStatus.Error;
            if (!string.IsNullOrWhiteSpace(_lastError))
            {
                LogManager.Error(_lastError);
            }
        }

        private static bool TryConvertValue(object value, out int intValue, out string error)
        {
            intValue = 0;
            error = string.Empty;

            if (value == null)
            {
                error = "写入值不能为空";
                return false;
            }

            if (value is bool boolValue)
            {
                intValue = boolValue ? 1 : 0;
                return true;
            }

            if (value is int directInt)
            {
                intValue = directInt;
                return true;
            }

            if (int.TryParse(value.ToString(), out var parsed))
            {
                intValue = parsed;
                return true;
            }

            error = "写入值必须为数字";
            return false;
        }
    }
}
