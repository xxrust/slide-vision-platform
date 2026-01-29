using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Management;
using System.Diagnostics;
using Microsoft.Win32;

namespace WpfApp2.SMTGPIO
{
    /// <summary>
    /// SMTGPIO系统环境诊断工具
    /// </summary>
    public static class SystemDiagnostic
    {
        /// <summary>
        /// 执行完整的系统诊断
        /// </summary>
        /// <returns>诊断报告</returns>
        public static string RunFullDiagnostic()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== SMTGPIO 系统诊断报告 ===");
            report.AppendLine($"诊断时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            // 1. 基本系统信息
            report.AppendLine("【系统信息】");
            report.AppendLine(GetSystemInfo());
            report.AppendLine();

            // 2. DLL文件检查
            report.AppendLine("【DLL文件检查】");
            report.AppendLine(CheckDllFiles());
            report.AppendLine();

            // 3. 驱动程序检查
            report.AppendLine("【驱动程序检查】");
            report.AppendLine(CheckDrivers());
            report.AppendLine();

            // 4. 硬件设备检查
            report.AppendLine("【硬件设备检查】");
            report.AppendLine(CheckHardwareDevices());
            report.AppendLine();

            // 5. 运行时库检查
            report.AppendLine("【运行时库检查】");
            report.AppendLine(CheckRuntimeLibraries());
            report.AppendLine();

            // 6. 权限检查
            report.AppendLine("【权限检查】");
            report.AppendLine(CheckPermissions());
            report.AppendLine();

            // 7. 建议
            report.AppendLine("【建议措施】");
            report.AppendLine(GetRecommendations());

            return report.ToString();
        }

        private static string GetSystemInfo()
        {
            var info = new System.Text.StringBuilder();
            try
            {
                info.AppendLine($"操作系统: {Environment.OSVersion}");
                info.AppendLine($"系统架构: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
                info.AppendLine($"进程架构: {(Environment.Is64BitProcess ? "x64" : "x86")}");
                info.AppendLine($"处理器数量: {Environment.ProcessorCount}");
                info.AppendLine($"用户名: {Environment.UserName}");
                info.AppendLine($"机器名: {Environment.MachineName}");
                info.AppendLine($"当前目录: {Environment.CurrentDirectory}");
                info.AppendLine($"应用程序目录: {AppDomain.CurrentDomain.BaseDirectory}");
                
                // .NET Framework 版本
                var version = Environment.Version;
                info.AppendLine($".NET Framework: {version}");
            }
            catch (Exception ex)
            {
                info.AppendLine($"获取系统信息失败: {ex.Message}");
            }
            return info.ToString();
        }

        private static string CheckDllFiles()
        {
            var result = new System.Text.StringBuilder();
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            
            string[] requiredFiles = {
                "SMTGPIO.dll",
                "SMTGPIO_Driver_SV.dll",
                "SvApiLibx64.dll",
                "SvIoCtrlx64.sys"
            };

            foreach (string fileName in requiredFiles)
            {
                string fullPath = Path.Combine(appPath, fileName);
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    result.AppendLine($"✓ {fileName} - 存在 ({fileInfo.Length} 字节, {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
                    
                    // 检查文件版本
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(fullPath);
                        if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                        {
                            result.AppendLine($"  版本: {versionInfo.FileVersion}");
                        }
                    }
                    catch { }
                }
                else
                {
                    result.AppendLine($"✗ {fileName} - 不存在");
                }
            }

            return result.ToString();
        }

        private static string CheckDrivers()
        {
            var result = new System.Text.StringBuilder();
            try
            {
                // 检查系统服务
                var services = System.ServiceProcess.ServiceController.GetServices();
                bool foundSMTService = false;
                
                foreach (var service in services)
                {
                    if (service.ServiceName.ToUpper().Contains("SMT") || 
                        service.ServiceName.ToUpper().Contains("GPIO") ||
                        service.ServiceName.ToUpper().Contains("SV"))
                    {
                        result.AppendLine($"服务: {service.ServiceName} - {service.Status}");
                        foundSMTService = true;
                    }
                }

                if (!foundSMTService)
                {
                    result.AppendLine("未找到SMTGPIO相关的系统服务");
                }

                // 检查设备管理器中的设备
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity"))
                    {
                        bool foundDevice = false;
                        foreach (ManagementObject device in searcher.Get())
                        {
                            string name = device["Name"]?.ToString() ?? "";
                            string deviceId = device["DeviceID"]?.ToString() ?? "";
                            
                            if (name.ToUpper().Contains("SMT") || 
                                name.ToUpper().Contains("GPIO") ||
                                deviceId.ToUpper().Contains("SMT"))
                            {
                                string status = device["Status"]?.ToString() ?? "";
                                result.AppendLine($"设备: {name} - {status}");
                                foundDevice = true;
                            }
                        }
                        
                        if (!foundDevice)
                        {
                            result.AppendLine("未在设备管理器中找到SMTGPIO设备");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.AppendLine($"检查设备管理器失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"检查驱动程序失败: {ex.Message}");
            }
            return result.ToString();
        }

        private static string CheckHardwareDevices()
        {
            var result = new System.Text.StringBuilder();
            try
            {
                // 检查USB设备
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_USBHub"))
                {
                    int usbDeviceCount = 0;
                    foreach (ManagementObject usb in searcher.Get())
                    {
                        usbDeviceCount++;
                    }
                    result.AppendLine($"USB设备总数: {usbDeviceCount}");
                }

                // 检查串口设备
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SerialPort"))
                {
                    foreach (ManagementObject port in searcher.Get())
                    {
                        string name = port["Name"]?.ToString() ?? "";
                        string deviceId = port["DeviceID"]?.ToString() ?? "";
                        result.AppendLine($"串口: {name} ({deviceId})");
                    }
                }

                // 检查并口设备
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ParallelPort"))
                {
                    foreach (ManagementObject port in searcher.Get())
                    {
                        string name = port["Name"]?.ToString() ?? "";
                        result.AppendLine($"并口: {name}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"检查硬件设备失败: {ex.Message}");
            }
            return result.ToString();
        }

        private static string CheckRuntimeLibraries()
        {
            var result = new System.Text.StringBuilder();
            try
            {
                // 检查Visual C++ Redistributable
                var vcRedistVersions = new[]
                {
                    @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64", // 2015-2019
                    @"SOFTWARE\Microsoft\VisualStudio\12.0\VC\Runtimes\x64", // 2013
                    @"SOFTWARE\Microsoft\VisualStudio\11.0\VC\Runtimes\x64", // 2012
                    @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
                };

                bool foundVCRedist = false;
                foreach (string keyPath in vcRedistVersions)
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                var version = key.GetValue("Version")?.ToString();
                                var installed = key.GetValue("Installed")?.ToString();
                                if (installed == "1")
                                {
                                    result.AppendLine($"✓ Visual C++ Redistributable x64: {version}");
                                    foundVCRedist = true;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (!foundVCRedist)
                {
                    result.AppendLine("✗ 未找到Visual C++ Redistributable x64");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"检查运行时库失败: {ex.Message}");
            }
            return result.ToString();
        }

        private static string CheckPermissions()
        {
            var result = new System.Text.StringBuilder();
            try
            {
                // 检查是否以管理员身份运行
                bool isElevated = IsRunningAsAdministrator();
                result.AppendLine($"管理员权限: {(isElevated ? "是" : "否")}");

                // 检查应用程序目录的写入权限
                string appPath = AppDomain.CurrentDomain.BaseDirectory;
                bool canWrite = CanWriteToDirectory(appPath);
                result.AppendLine($"应用程序目录写入权限: {(canWrite ? "是" : "否")}");

                // 检查系统目录访问权限
                bool canAccessSystem = CanAccessSystemDirectory();
                result.AppendLine($"系统目录访问权限: {(canAccessSystem ? "是" : "否")}");
            }
            catch (Exception ex)
            {
                result.AppendLine($"检查权限失败: {ex.Message}");
            }
            return result.ToString();
        }

        private static string GetRecommendations()
        {
            var recommendations = new System.Text.StringBuilder();
            
            recommendations.AppendLine("基于诊断结果的建议:");
            recommendations.AppendLine("1. 如果缺少DLL文件，请从SMTGPIO_V2.0.0.0_C_x64\\runtime目录复制");
            recommendations.AppendLine("2. 如果未找到驱动程序，请安装SMTGPIO驱动程序");
            recommendations.AppendLine("3. 如果缺少Visual C++ Redistributable，请安装Microsoft Visual C++ 2015-2019 Redistributable (x64)");
            recommendations.AppendLine("4. 如果权限不足，请以管理员身份运行程序");
            recommendations.AppendLine("5. 确保SMTGPIO硬件设备已正确连接");
            recommendations.AppendLine("6. 如果在虚拟机中运行，请检查USB直通设置");

            return recommendations.ToString();
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static bool CanWriteToDirectory(string directoryPath)
        {
            try
            {
                string testFile = Path.Combine(directoryPath, "test_write_permission.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanAccessSystemDirectory()
        {
            try
            {
                string systemPath = Environment.SystemDirectory;
                var files = Directory.GetFiles(systemPath, "*.dll");
                return files.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
} 