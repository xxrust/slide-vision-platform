using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using WpfApp2.UI.Models;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 开机启动管理器 - 优化版，解决开机卡死问题
    /// </summary>
    public static class AutoStartupManager
    {
        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "点胶检测系统";
        private const string CONFIG_FILE = "Config/AutoStartup.txt";
        
        // 系统就绪检查参数
        private const int MAX_STARTUP_WAIT_TIME = 120000; // 最大等待2分钟
        private const int SYSTEM_CHECK_INTERVAL = 2000;   // 每2秒检查一次
        private const int MIN_STARTUP_DELAY = 15000;      // 最小启动延迟15秒

        /// <summary>
        /// 检查并提示用户设置开机启动
        /// </summary>
        public static void CheckAndPromptAutoStartup()
        {
            try
            {
                // 检查是否已经询问过用户
                if (HasUserBeenAsked())
                {
                    // 即使已询问过，仍然需要初始化PLC自动连接
                    InitializeAutoConnectPLC();
                    return;
                }

                // 检查当前是否已设置开机启动
                if (IsAutoStartupEnabled())
                {
                    // 已经设置了开机启动，保存状态并初始化PLC
                    SaveUserChoice(true);
                    InitializeAutoConnectPLC();
                    return;
                }

                // 询问用户是否设置开机启动
                MessageBoxResult result = MessageBox.Show(
                    "🚀 点胶检测系统 - 开机启动设置\n\n" +
                    "是否设置开机自动启动？\n\n" +
                    "✅ 优点：\n" +
                    "• 系统启动后自动运行检测程序\n" +
                    "• 无需手动启动，提高工作效率\n" +
                    "• 智能延迟启动，确保系统稳定\n\n" +
                    "⚠️ 注意：\n" +
                    "• 程序将在系统完全就绪后启动\n" +
                    "• 可以随时在帮助菜单中修改此设置\n\n" +
                    "是否启用开机自动启动？",
                    "开机启动设置",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                bool enableAutoStartup = result == MessageBoxResult.Yes;

                if (enableAutoStartup)
                {
                    SetAutoStartup(true);
                }

                // 保存用户选择，下次不再询问
                SaveUserChoice(enableAutoStartup);

                string message = enableAutoStartup ? "开机启动已启用" : "开机启动未启用";
                LogManager.Info($"{message}（用户选择）", "AutoStartup");

                // 无论用户选择如何，都初始化PLC自动连接
                InitializeAutoConnectPLC();
            }
            catch (Exception ex)
            {
                LogManager.Error($"检查开机启动设置失败: {ex.Message}", "AutoStartup");
                // 即使失败，也尝试初始化PLC连接
                InitializeAutoConnectPLC();
            }
        }

        /// <summary>
        /// 管理开机启动设置（从帮助菜单调用）
        /// </summary>
        public static void ManageAutoStartupSetting()
        {
            try
            {
                bool currentStatus = IsAutoStartupEnabled();
                string currentStatusText = currentStatus ? "已启用" : "未启用";
                
                MessageBoxResult result = MessageBox.Show(
                    $"🔧 开机启动管理\n\n" +
                    $"当前状态：{currentStatusText}\n\n" +
                    $"说明：\n" +
                    $"• 启用：系统启动后自动运行检测程序\n" +
                    $"• 禁用：需要手动启动程序\n" +
                    $"• 智能启动：等待系统完全就绪后启动\n\n" +
                    $"是否{(currentStatus ? "禁用" : "启用")}开机启动？",
                    "开机启动管理",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    bool newStatus = !currentStatus;
                    SetAutoStartup(newStatus);
                    SaveUserChoice(newStatus);
                    
                    string action = newStatus ? "启用" : "禁用";
                    MessageBox.Show($"开机启动已{action}！", "设置完成", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    LogManager.Info($"用户{action}了开机启动", "AutoStartup");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"管理开机启动设置失败: {ex.Message}", "AutoStartup");
                MessageBox.Show($"设置失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 设置开机启动 - 使用智能启动脚本
        /// </summary>
        /// <param name="enable">是否启用</param>
        public static void SetAutoStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (enable)
                    {
                        // 创建智能启动脚本
                        string scriptPath = CreateSmartStartupScript();
                        if (!string.IsNullOrEmpty(scriptPath))
                        {
                            key?.SetValue(APP_NAME, $"\"{scriptPath}\"");
                            LogManager.Info($"开机启动已启用，使用智能启动脚本: {scriptPath}", "AutoStartup");
                        }
                        else
                        {
                            // 备用方案：直接启动主程序
                            string exePath = Process.GetCurrentProcess().MainModule.FileName;
                            key?.SetValue(APP_NAME, $"\"{exePath}\"");
                            LogManager.Warning("智能启动脚本创建失败，使用直接启动方式", "AutoStartup");
                        }
                    }
                    else
                    {
                        key?.DeleteValue(APP_NAME, false);
                        
                        // 清理智能启动脚本
                        CleanupSmartStartupScript();
                        LogManager.Info("开机启动已禁用，智能启动脚本已清理", "AutoStartup");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"设置开机启动失败: {ex.Message}", "AutoStartup");
                throw;
            }
        }

        /// <summary>
        /// 创建智能启动脚本
        /// </summary>
        /// <returns>脚本文件路径</returns>
        private static string CreateSmartStartupScript()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string scriptPath = Path.Combine(appDir, "SmartStartup.bat");
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                
                // 创建智能启动批处理脚本
                string scriptContent = $@"@echo off
REM 点胶检测系统智能启动脚本
REM 等待系统完全就绪后启动应用程序

echo [%date% %time%] 点胶检测系统智能启动开始... >> ""{Path.Combine(appDir, "startup.log")}""

REM 最小延迟15秒，确保系统基本服务启动
timeout /t 15 /nobreak > nul

REM 等待网络服务就绪
:WAIT_NETWORK
ping 127.0.0.1 -n 1 > nul 2>&1
if errorlevel 1 (
    echo [%date% %time%] 等待网络服务... >> ""{Path.Combine(appDir, "startup.log")}""
    timeout /t 2 /nobreak > nul
    goto WAIT_NETWORK
)

REM 等待Windows资源管理器完全启动
:WAIT_EXPLORER
tasklist /fi ""imagename eq explorer.exe"" | find ""explorer.exe"" > nul
if errorlevel 1 (
    echo [%date% %time%] 等待资源管理器... >> ""{Path.Combine(appDir, "startup.log")}""
    timeout /t 2 /nobreak > nul
    goto WAIT_EXPLORER
)

REM 检查系统负载是否合理（CPU使用率检查）
:WAIT_SYSTEM_READY
for /f ""skip=1 tokens=2 delims=,"" %%i in ('wmic cpu get loadpercentage /format:csv') do (
    if %%i LSS 80 goto START_APP
)
echo [%date% %time%] 等待系统负载降低... >> ""{Path.Combine(appDir, "startup.log")}""
timeout /t 5 /nobreak > nul
goto WAIT_SYSTEM_READY

:START_APP
echo [%date% %time%] 系统就绪，启动点胶检测系统... >> ""{Path.Combine(appDir, "startup.log")}""

REM 启动应用程序
start """" ""{exePath}""

echo [%date% %time%] 点胶检测系统启动完成 >> ""{Path.Combine(appDir, "startup.log")}""
exit
";

                File.WriteAllText(scriptPath, scriptContent, System.Text.Encoding.Default);
                LogManager.Info($"智能启动脚本已创建: {scriptPath}", "AutoStartup");
                return scriptPath;
            }
            catch (Exception ex)
            {
                LogManager.Error($"创建智能启动脚本失败: {ex.Message}", "AutoStartup");
                return null;
            }
        }

        /// <summary>
        /// 清理智能启动脚本
        /// </summary>
        private static void CleanupSmartStartupScript()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string scriptPath = Path.Combine(appDir, "SmartStartup.bat");
                string logPath = Path.Combine(appDir, "startup.log");
                
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
                
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                }
                
                LogManager.Info("智能启动脚本和日志文件已清理", "AutoStartup");
            }
            catch (Exception ex)
            {
                LogManager.Warning($"清理智能启动脚本失败: {ex.Message}", "AutoStartup");
            }
        }

        /// <summary>
        /// 检查是否已启用开机启动
        /// </summary>
        public static bool IsAutoStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    object value = key?.GetValue(APP_NAME);
                    return value != null;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"检查开机启动状态失败: {ex.Message}", "AutoStartup");
                return false;
            }
        }

        /// <summary>
        /// 获取开机启动状态描述
        /// </summary>
        public static string GetAutoStartupStatusDescription()
        {
            try
            {
                bool isEnabled = IsAutoStartupEnabled();
                if (isEnabled)
                {
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string scriptPath = Path.Combine(appDir, "SmartStartup.bat");
                    bool hasSmartScript = File.Exists(scriptPath);
                    
                    return hasSmartScript ? "已启用（智能启动）" : "已启用（直接启动）";
                }
                else
                {
                    return "未启用";
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取开机启动状态失败: {ex.Message}", "AutoStartup");
                return "状态未知";
            }
        }

        /// <summary>
        /// 检查是否已询问过用户
        /// </summary>
        private static bool HasUserBeenAsked()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE);
                return File.Exists(configPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 保存用户选择
        /// </summary>
        private static void SaveUserChoice(bool enabled)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE);
                string configDir = Path.GetDirectoryName(configPath);
                
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                string content = $"AutoStartup={enabled}\nAskedDate={DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                File.WriteAllText(configPath, content);
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存开机启动配置失败: {ex.Message}", "AutoStartup");
            }
        }

        /// <summary>
        /// 初始化开机自动连接PLC功能
        /// </summary>
        private static void InitializeAutoConnectPLC()
        {
            try
            {
                LogManager.Info("开始初始化PLC自动连接...", "AutoStartup");

                // 延迟5秒后尝试自动连接PLC，确保程序完全启动
                var autoConnectTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };

                autoConnectTimer.Tick += (sender, e) =>
                {
                    autoConnectTimer.Stop();
                    AttemptAutoConnectPLC();
                };

                autoConnectTimer.Start();
                LogManager.Info("PLC自动连接定时器已启动（5秒后执行）", "AutoStartup");
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化PLC自动连接失败: {ex.Message}", "AutoStartup");
            }
        }

        /// <summary>
        /// 尝试自动连接PLC
        /// </summary>
        private static void AttemptAutoConnectPLC()
        {
            try
            {
                LogManager.Info("开始尝试自动连接PLC串口...", "AutoStartup");

                // 获取可用的串口列表
                string[] availablePorts = WpfApp2.SMTGPIO.PLCSerialController.GetAvailablePorts();

                if (availablePorts.Length == 0)
                {
                    LogManager.Info("未发现可用串口，跳过PLC自动连接", "AutoStartup");
                    return;
                }

                LogManager.Info($"发现 {availablePorts.Length} 个可用串口: {string.Join(", ", availablePorts)}", "AutoStartup");

                // 尝试连接默认串口配置（按优先级顺序）
                string[] preferredPorts = { "COM1", "COM2", "COM3", "COM4", "COM5" };

                foreach (string preferredPort in preferredPorts)
                {
                    if (availablePorts.Contains(preferredPort))
                    {
                        LogManager.Info($"尝试连接首选串口: {preferredPort}", "AutoStartup");
                        if (TryConnectPLC(preferredPort))
                        {
                            LogManager.Info($"✅ PLC自动连接成功: {preferredPort}", "AutoStartup");
                            return;
                        }
                    }
                }

                // 如果首选串口都连接失败，尝试第一个可用串口
                if (availablePorts.Length > 0)
                {
                    string firstPort = availablePorts[0];
                    LogManager.Info($"尝试连接第一个可用串口: {firstPort}", "AutoStartup");
                    if (TryConnectPLC(firstPort))
                    {
                        LogManager.Info($"✅ PLC自动连接成功: {firstPort}", "AutoStartup");
                    }
                    else
                    {
                        LogManager.Info("❌ PLC自动连接失败，所有串口都无法连接", "AutoStartup");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"PLC自动连接过程中出错: {ex.Message}", "AutoStartup");
            }
        }

        /// <summary>
        /// 尝试连接指定串口的PLC（使用单例模式）
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <returns>连接是否成功</returns>
        private static bool TryConnectPLC(string portName)
        {
            try
            {
                // 使用PLC控制器单例实例
                var plcController = WpfApp2.SMTGPIO.PLCSerialController.Instance;

                // 如果已经连接到相同的串口，直接返回成功
                if (plcController.IsConnected && plcController.PortName == portName)
                {
                    LogManager.Info($"PLC已连接到串口 {portName}，跳过重复连接", "AutoStartup");
                    return true;
                }

                // 如果连接到不同串口，先断开
                if (plcController.IsConnected)
                {
                    LogManager.Info($"断开当前PLC连接，准备连接到串口 {portName}", "AutoStartup");
                    plcController.Disconnect();
                }

                // 配置新的连接参数
                plcController.ConfigureConnection(portName, 9600, 3000);

                // 尝试连接
                bool connectResult = plcController.Connect();

                if (connectResult)
                {
                    LogManager.Info($"PLC串口 {portName} 连接成功", "AutoStartup");
                    return true;
                }
                else
                {
                    LogManager.Info($"PLC串口 {portName} 连接失败: {plcController.ErrorMessage}", "AutoStartup");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"尝试连接PLC串口 {portName} 时出错: {ex.Message}", "AutoStartup");
                return false;
            }
        }
    }
} 