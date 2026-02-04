using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using Newtonsoft.Json;

namespace WpfApp2.UI.Models
{
    public enum DeviceProtocolType
    {
        Serial,
        TcpIp
    }

    public sealed class DeviceSerialOptions
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public Parity Parity { get; set; } = Parity.Even;
        public int ReadTimeout { get; set; } = 5000;
        public int WriteTimeout { get; set; } = 100;

        public DeviceSerialOptions Clone()
        {
            return new DeviceSerialOptions
            {
                PortName = PortName,
                BaudRate = BaudRate,
                DataBits = DataBits,
                StopBits = StopBits,
                Parity = Parity,
                ReadTimeout = ReadTimeout,
                WriteTimeout = WriteTimeout
            };
        }
    }

    public sealed class DeviceTcpOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 0;

        public DeviceTcpOptions Clone()
        {
            return new DeviceTcpOptions
            {
                Host = Host,
                Port = Port
            };
        }
    }

    public sealed class DeviceConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string HardwareName { get; set; } = string.Empty;
        public DeviceProtocolType ProtocolType { get; set; } = DeviceProtocolType.Serial;
        public DeviceSerialOptions Serial { get; set; } = new DeviceSerialOptions();
        public DeviceTcpOptions Tcp { get; set; } = new DeviceTcpOptions();
        public bool Enabled { get; set; } = true;

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
            }

            if (Serial == null)
            {
                Serial = new DeviceSerialOptions();
            }

            if (Tcp == null)
            {
                Tcp = new DeviceTcpOptions();
            }
        }

        public DeviceConfig Clone()
        {
            return new DeviceConfig
            {
                Id = Id,
                Name = Name,
                Brand = Brand,
                HardwareName = HardwareName,
                ProtocolType = ProtocolType,
                Serial = Serial?.Clone() ?? new DeviceSerialOptions(),
                Tcp = Tcp?.Clone() ?? new DeviceTcpOptions(),
                Enabled = Enabled
            };
        }
    }

    public static class DeviceConfigManager
    {
        private static readonly object SyncRoot = new object();
        private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "DeviceManagement.json");
        private static bool _loaded;
        private static List<DeviceConfig> _devices = new List<DeviceConfig>();

        public static IReadOnlyList<DeviceConfig> GetDevices()
        {
            EnsureLoaded();

            lock (SyncRoot)
            {
                return _devices.Select(device => device.Clone()).ToList();
            }
        }

        public static DeviceConfig GetDeviceById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            EnsureLoaded();

            lock (SyncRoot)
            {
                var device = _devices.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
                return device?.Clone();
            }
        }

        public static DeviceConfig GetDeviceByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            EnsureLoaded();

            lock (SyncRoot)
            {
                var device = _devices.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                return device?.Clone();
            }
        }

        public static bool AddDevice(DeviceConfig device, out string error)
        {
            if (!TryNormalizeAndValidate(device, out error))
            {
                return false;
            }

            EnsureLoaded();

            lock (SyncRoot)
            {
                if (_devices.Any(item => string.Equals(item.Name, device.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "设备名称已存在";
                    return false;
                }

                device.EnsureDefaults();
                _devices.Add(device.Clone());
                SaveInternal();
                return true;
            }
        }

        public static bool UpdateDevice(DeviceConfig device, out string error)
        {
            if (!TryNormalizeAndValidate(device, out error))
            {
                return false;
            }

            EnsureLoaded();

            lock (SyncRoot)
            {
                var index = _devices.FindIndex(item => string.Equals(item.Id, device.Id, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    error = "设备不存在";
                    return false;
                }

                if (_devices.Any(item => !string.Equals(item.Id, device.Id, StringComparison.OrdinalIgnoreCase)
                                         && string.Equals(item.Name, device.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "设备名称已存在";
                    return false;
                }

                device.EnsureDefaults();
                _devices[index] = device.Clone();
                SaveInternal();
                return true;
            }
        }

        public static bool RemoveDevice(string id, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "设备ID不能为空";
                return false;
            }

            EnsureLoaded();

            lock (SyncRoot)
            {
                var removed = _devices.RemoveAll(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
                if (removed == 0)
                {
                    error = "设备不存在";
                    return false;
                }

                SaveInternal();
                return true;
            }
        }

        public static void Reload()
        {
            lock (SyncRoot)
            {
                _loaded = false;
                _devices.Clear();
            }

            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_loaded)
                {
                    return;
                }

                _devices = LoadInternal();
                _loaded = true;
            }
        }

        private static List<DeviceConfig> LoadInternal()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    return new List<DeviceConfig>();
                }

                var json = File.ReadAllText(ConfigFile);
                var devices = JsonConvert.DeserializeObject<List<DeviceConfig>>(json) ?? new List<DeviceConfig>();
                foreach (var device in devices)
                {
                    device?.EnsureDefaults();
                }

                return devices.Where(device => device != null).ToList();
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载设备配置失败: {ex.Message}");
                return new List<DeviceConfig>();
            }
        }

        private static void SaveInternal()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                var json = JsonConvert.SerializeObject(_devices, Formatting.Indented);
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存设备配置失败: {ex.Message}");
            }
        }

        private static bool TryNormalizeAndValidate(DeviceConfig device, out string error)
        {
            error = string.Empty;
            if (device == null)
            {
                error = "设备不能为空";
                return false;
            }

            device.EnsureDefaults();

            if (string.IsNullOrWhiteSpace(device.Name))
            {
                error = "设备名称不能为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(device.Brand))
            {
                error = "设备品牌不能为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(device.HardwareName))
            {
                error = "硬件名称不能为空";
                return false;
            }

            switch (device.ProtocolType)
            {
                case DeviceProtocolType.Serial:
                    return ValidateSerial(device.Serial, out error);
                case DeviceProtocolType.TcpIp:
                    return ValidateTcp(device.Tcp, out error);
                default:
                    error = "不支持的协议类型";
                    return false;
            }
        }

        private static bool ValidateSerial(DeviceSerialOptions options, out string error)
        {
            error = string.Empty;
            if (options == null)
            {
                error = "串口参数不能为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.PortName))
            {
                error = "串口号不能为空";
                return false;
            }

            if (options.BaudRate <= 0)
            {
                error = "波特率无效";
                return false;
            }

            if (options.DataBits < 5 || options.DataBits > 8)
            {
                error = "数据位必须在 5-8 之间";
                return false;
            }

            if (options.StopBits == StopBits.None)
            {
                error = "停止位无效";
                return false;
            }

            return true;
        }

        private static bool ValidateTcp(DeviceTcpOptions options, out string error)
        {
            error = string.Empty;
            if (options == null)
            {
                error = "TCP参数不能为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                error = "目标IP不能为空";
                return false;
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                error = "端口范围无效";
                return false;
            }

            return true;
        }
    }
}
