using System;
using System.IO;
using Newtonsoft.Json;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// GPIO配置类
    /// </summary>
    public class GPIOConfig
    {
        public SMTGPIODeviceType DeviceType { get; set; } = SMTGPIODeviceType.SCI_EVC3_5;
        public uint Port { get; set; } = 2;
        
        /// <summary>
        /// 设备类型显示名称
        /// </summary>
        public string DeviceDisplayName
        {
            get
            {
                switch (DeviceType)
                {
                    case SMTGPIODeviceType.SCI_EVC2_2: return "EVC2-2";
                    case SMTGPIODeviceType.SCI_EVC2_5: return "EVC2-5";
                    case SMTGPIODeviceType.SCI_EVC3_5: return "EVC3-5";
                    case SMTGPIODeviceType.SCI_Q2: return "Q2";
                    case SMTGPIODeviceType.SCI_Q2C: return "Q2C";
                    case SMTGPIODeviceType.SCI_Q2D: return "Q2D";
                    case SMTGPIODeviceType.SCI_Q3: return "Q3";
                    case SMTGPIODeviceType.SCI_X3: return "X3";
                    case SMTGPIODeviceType.SCI_M3: return "M3";
                    case SMTGPIODeviceType.SCI_PCI1370U: return "PCI1370U";
                    case SMTGPIODeviceType.SCI_ISKUNS01: return "ISKUNS01";
                    case SMTGPIODeviceType.ADVANTECH_PCI1730: return "ADVANTECH PCI1730";
                    case SMTGPIODeviceType.ADVANTECH_PCIE1756: return "ADVANTECH PCIE1756";
                    case SMTGPIODeviceType.LEADSHINE_IOC0640: return "LEADSHINE IOC0640";
                    case SMTGPIODeviceType.A118_IO: return "A118 IO";
                    default: return DeviceType.ToString();
                }
            }
        }
    }

    /// <summary>
    /// GPIO配置管理器
    /// </summary>
    public static class GPIOConfigManager
    {
        private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "gpio_config.json");
        private static GPIOConfig _currentConfig;

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public static GPIOConfig CurrentConfig
        {
            get
            {
                if (_currentConfig == null)
                {
                    _currentConfig = LoadConfig();
                }
                return _currentConfig;
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public static GPIOConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string json = File.ReadAllText(ConfigFile);
                    var config = JsonConvert.DeserializeObject<GPIOConfig>(json);
                    if (config != null)
                    {
                        _currentConfig = config;
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载GPIO配置失败: {ex.Message}");
            }

            // 返回默认配置
            _currentConfig = new GPIOConfig();
            return _currentConfig;
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public static bool SaveConfig(GPIOConfig config)
        {
            try
            {
                // 确保config目录存在
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ConfigFile, json);
                
                _currentConfig = config;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存GPIO配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取所有可用的设备类型
        /// </summary>
        public static SMTGPIODeviceType[] GetAvailableDeviceTypes()
        {
            return new SMTGPIODeviceType[]
            {
                SMTGPIODeviceType.SCI_EVC2_2,
                SMTGPIODeviceType.SCI_EVC2_5,
                SMTGPIODeviceType.SCI_EVC3_5,
                SMTGPIODeviceType.SCI_Q2,
                SMTGPIODeviceType.SCI_Q2C,
                SMTGPIODeviceType.SCI_Q2D,
                SMTGPIODeviceType.SCI_Q3,
                SMTGPIODeviceType.SCI_X3,
                SMTGPIODeviceType.SCI_M3,
                SMTGPIODeviceType.SCI_PCI1370U,
                SMTGPIODeviceType.SCI_ISKUNS01,
                SMTGPIODeviceType.ADVANTECH_PCI1730,
                SMTGPIODeviceType.ADVANTECH_PCIE1756,
                SMTGPIODeviceType.LEADSHINE_IOC0640,
                SMTGPIODeviceType.A118_IO
            };
        }
    }
} 