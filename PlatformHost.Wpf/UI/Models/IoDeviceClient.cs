using System;
using System.Threading.Tasks;
using WpfApp2.SMTGPIO;

namespace WpfApp2.UI.Models
{
    public sealed class IoDeviceClient : IDeviceClient, IConfigurableDeviceClient
    {
        private readonly object _syncRoot = new object();
        private DeviceConfig _config;
        private DeviceConnectionStatus _status = DeviceConnectionStatus.Disconnected;
        private string _lastError = string.Empty;

        public IoDeviceClient(DeviceConfig config)
        {
            UpdateConfig(config);
        }

        public string Id => _config?.Id ?? string.Empty;
        public string Name => _config?.Name ?? string.Empty;
        public DeviceProtocolType ProtocolType => _config?.ProtocolType ?? DeviceProtocolType.Io;

        public DeviceConnectionStatus Status
        {
            get
            {
                if (IOManager.IsInitialized && _status != DeviceConnectionStatus.Connecting)
                {
                    _status = DeviceConnectionStatus.Connected;
                }
                else if (!IOManager.IsInitialized && _status == DeviceConnectionStatus.Connected)
                {
                    _status = DeviceConnectionStatus.Disconnected;
                }

                return _status;
            }
        }

        public string LastError => _lastError;

        public Task<DeviceOperationResult> ConnectAsync()
        {
            lock (_syncRoot)
            {
                if (_config == null)
                {
                    return Task.FromResult(DeviceOperationResult.Fail("IO设备配置为空"));
                }

                try
                {
                    _status = DeviceConnectionStatus.Connecting;
                    IOManager.SetActiveDevice(_config.Id);

                    var initResult = IOManager.Initialize();
                    if (initResult)
                    {
                        _status = DeviceConnectionStatus.Connected;
                        _lastError = string.Empty;
                        return Task.FromResult(DeviceOperationResult.Ok());
                    }

                    var message = "IO控制器初始化失败";
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
                IOManager.Dispose();
                _status = DeviceConnectionStatus.Disconnected;
                return Task.FromResult(DeviceOperationResult.Ok());
            }
        }

        public Task<DeviceReadResult> ReadAsync(string address)
        {
            return Task.FromResult(DeviceReadResult.Fail("IO设备不支持读取"));
        }

        public Task<DeviceOperationResult> WriteAsync(string address, object value)
        {
            return Task.FromResult(DeviceOperationResult.Fail("IO设备不支持写入"));
        }

        public void UpdateConfig(DeviceConfig config)
        {
            if (config == null)
            {
                return;
            }

            _config = config.Clone();
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
    }
}
