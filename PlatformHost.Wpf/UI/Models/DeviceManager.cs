using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WpfApp2.UI.Models
{
    public enum DeviceConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    public sealed class DeviceOperationResult
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }

        public static DeviceOperationResult Ok()
        {
            return new DeviceOperationResult { Success = true, Error = string.Empty };
        }

        public static DeviceOperationResult Fail(string error)
        {
            return new DeviceOperationResult { Success = false, Error = error ?? string.Empty };
        }
    }

    public sealed class DeviceReadResult
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        public object Value { get; private set; }

        public static DeviceReadResult Ok(object value)
        {
            return new DeviceReadResult { Success = true, Error = string.Empty, Value = value };
        }

        public static DeviceReadResult Fail(string error)
        {
            return new DeviceReadResult { Success = false, Error = error ?? string.Empty, Value = null };
        }
    }

    public interface IDeviceClient
    {
        string Id { get; }
        string Name { get; }
        DeviceProtocolType ProtocolType { get; }
        DeviceConnectionStatus Status { get; }
        string LastError { get; }

        Task<DeviceOperationResult> ConnectAsync();
        Task<DeviceOperationResult> DisconnectAsync();
        Task<DeviceReadResult> ReadAsync(string address);
        Task<DeviceOperationResult> WriteAsync(string address, object value);
    }

    public interface IConfigurableDeviceClient
    {
        void UpdateConfig(DeviceConfig config);
    }

    public interface IDeviceManager
    {
        IReadOnlyList<DeviceConfig> GetDevices();
        DeviceConfig GetDeviceById(string id);
        DeviceConfig GetDeviceByName(string name);
        IDeviceClient GetClientById(string id, out string error);
        IDeviceClient GetClientByName(string name, out string error);
        bool AddDevice(DeviceConfig device, out string error);
        bool UpdateDevice(DeviceConfig device, out string error);
        bool RemoveDevice(string id, out string error);
    }

    public sealed class DeviceManager : IDeviceManager
    {
        public static readonly DeviceManager Instance = new DeviceManager();

        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, IDeviceClient> _clients = new Dictionary<string, IDeviceClient>(StringComparer.OrdinalIgnoreCase);

        private DeviceManager()
        {
        }

        public IReadOnlyList<DeviceConfig> GetDevices()
        {
            return DeviceConfigManager.GetDevices();
        }

        public DeviceConfig GetDeviceById(string id)
        {
            return DeviceConfigManager.GetDeviceById(id);
        }

        public DeviceConfig GetDeviceByName(string name)
        {
            return DeviceConfigManager.GetDeviceByName(name);
        }

        public IDeviceClient GetClientById(string id, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "设备ID不能为空";
                LogManager.Error(error);
                return null;
            }

            var device = DeviceConfigManager.GetDeviceById(id);
            if (device == null)
            {
                error = $"未找到设备: {id}";
                LogManager.Error(error);
                return null;
            }

            return GetOrCreateClient(device);
        }

        public IDeviceClient GetClientByName(string name, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "设备名称不能为空";
                LogManager.Error(error);
                return null;
            }

            var device = DeviceConfigManager.GetDeviceByName(name);
            if (device == null)
            {
                error = $"未找到设备: {name}";
                LogManager.Error(error);
                return null;
            }

            return GetOrCreateClient(device);
        }

        public bool AddDevice(DeviceConfig device, out string error)
        {
            var success = DeviceConfigManager.AddDevice(device, out error);
            if (!success && !string.IsNullOrWhiteSpace(error))
            {
                LogManager.Error($"创建设备失败: {error}");
            }
            return success;
        }

        public bool UpdateDevice(DeviceConfig device, out string error)
        {
            var success = DeviceConfigManager.UpdateDevice(device, out error);
            if (!success && !string.IsNullOrWhiteSpace(error))
            {
                LogManager.Error($"更新设备失败: {error}");
                return false;
            }

            if (device != null && !string.IsNullOrWhiteSpace(device.Id))
            {
                lock (_syncRoot)
                {
                    _clients.Remove(device.Id);
                }
            }

            return success;
        }

        public bool RemoveDevice(string id, out string error)
        {
            var success = DeviceConfigManager.RemoveDevice(id, out error);
            if (!success && !string.IsNullOrWhiteSpace(error))
            {
                LogManager.Error($"删除设备失败: {error}");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                lock (_syncRoot)
                {
                    _clients.Remove(id);
                }
            }

            return success;
        }

        private IDeviceClient GetOrCreateClient(DeviceConfig device)
        {
            if (device == null)
            {
                return null;
            }

            lock (_syncRoot)
            {
                if (_clients.TryGetValue(device.Id, out var client))
                {
                    if (client is IConfigurableDeviceClient configurableClient)
                    {
                        configurableClient.UpdateConfig(device);
                    }
                    return client;
                }

                client = CreateClient(device);
                _clients[device.Id] = client;
                return client;
            }
        }

        private static IDeviceClient CreateClient(DeviceConfig device)
        {
            if (device.ProtocolType == DeviceProtocolType.Serial)
            {
                return new KeyencePlcSerialClient(device);
            }

            return new DefaultDeviceClient(device);
        }

        private sealed class DefaultDeviceClient : IDeviceClient, IConfigurableDeviceClient
        {
            private DeviceConfig _config;
            private DeviceConnectionStatus _status = DeviceConnectionStatus.Disconnected;
            private string _lastError = string.Empty;

            public DefaultDeviceClient(DeviceConfig config)
            {
                UpdateConfig(config);
            }

            public string Id => _config?.Id ?? string.Empty;
            public string Name => _config?.Name ?? string.Empty;
            public DeviceProtocolType ProtocolType => _config?.ProtocolType ?? DeviceProtocolType.Serial;
            public DeviceConnectionStatus Status => _status;
            public string LastError => _lastError;

            public Task<DeviceOperationResult> ConnectAsync()
            {
                return Task.FromResult(FailWithError("设备连接未实现"));
            }

            public Task<DeviceOperationResult> DisconnectAsync()
            {
                _status = DeviceConnectionStatus.Disconnected;
                return Task.FromResult(DeviceOperationResult.Ok());
            }

            public Task<DeviceReadResult> ReadAsync(string address)
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    return Task.FromResult(DeviceReadResult.Fail("读取地址不能为空"));
                }

                LogError("设备读取未实现");
                _status = DeviceConnectionStatus.Error;
                return Task.FromResult(DeviceReadResult.Fail("设备读取未实现"));
            }

            public Task<DeviceOperationResult> WriteAsync(string address, object value)
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    return Task.FromResult(DeviceOperationResult.Fail("写入地址不能为空"));
                }

                return Task.FromResult(FailWithError("设备写入未实现"));
            }

            public void UpdateConfig(DeviceConfig config)
            {
                if (config == null)
                {
                    return;
                }

                _config = config.Clone();
            }

            private DeviceOperationResult FailWithError(string message)
            {
                LogError(message);
                _status = DeviceConnectionStatus.Error;
                return DeviceOperationResult.Fail(message);
            }

            private void LogError(string message)
            {
                _lastError = message ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(_lastError))
                {
                    LogManager.Error(_lastError);
                }
            }
        }
    }
}
