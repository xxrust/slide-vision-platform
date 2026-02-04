using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WpfApp2
{
    /// <summary>
    /// 启动诊断工具
    /// </summary>
    public static class StartupDiagnostic
    {
        private static string _logPath;

        static StartupDiagnostic()
        {
            try
            {
                // 在程序目录创建日志文件
                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _logPath = Path.Combine(exeDir, "startup_diagnostic.log");
            }
            catch
            {
                // 如果无法在程序目录创建，则在临时目录创建
                _logPath = Path.Combine(Path.GetTempPath(), "WpfApp2_startup_diagnostic.log");
            }
        }

        /// <summary>
        /// 记录诊断信息
        /// </summary>
        public static void LogInfo(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}";
                File.AppendAllText(_logPath, logEntry + Environment.NewLine);
            }
            catch
            {
                // 忽略日志写入错误
            }
        }

        /// <summary>
        /// 记录错误信息
        /// </summary>
        public static void LogError(string message, Exception ex = null)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
                if (ex != null)
                {
                    logEntry += $"\nException: {ex.ToString()}";
                }
                File.AppendAllText(_logPath, logEntry + Environment.NewLine);
            }
            catch
            {
                // 忽略日志写入错误
            }
        }

        /// <summary>
        /// 检查系统环境
        /// </summary>
        public static void CheckSystemEnvironment()
        {
            try
            {
                LogInfo("=== 启动诊断开始 ===");
                LogInfo($"程序版本: {Assembly.GetExecutingAssembly().GetName().Version}");
                LogInfo($"操作系统: {Environment.OSVersion}");
                LogInfo($"系统架构: {RuntimeInformation.OSArchitecture}");
                LogInfo($"进程架构: {RuntimeInformation.ProcessArchitecture}");
                LogInfo($".NET版本: {RuntimeInformation.FrameworkDescription}");
                LogInfo($"工作目录: {Environment.CurrentDirectory}");
                LogInfo($"程序目录: {Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}");
                LogInfo($"用户权限: {Environment.UserName}");
                LogInfo($"机器名: {Environment.MachineName}");

                // 检查关键DLL文件
                CheckCriticalDlls();

                // 检查.NET Framework版本
                CheckDotNetFramework();

                LogInfo("=== 系统环境检查完成 ===");
            }
            catch (Exception ex)
            {
                LogError("检查系统环境时出错", ex);
            }
        }

        /// <summary>
        /// 检查关键DLL文件
        /// </summary>
        private static void CheckCriticalDlls()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            bool shouldCheck3D = File.Exists(Path.Combine(exeDir, "Slide.ThreeD.Host.exe"))
                || File.Exists(Path.Combine(exeDir, "CodeMeterCheck.exe"))
                || File.Exists(Path.Combine(exeDir, "LjDevCommon.Core.dll"))
                || File.Exists(Path.Combine(exeDir, "LjDev3dView.Core.dll"))
                || File.Exists(Path.Combine(exeDir, "Dll", "Managed", "LjDev3dView.dll"));

            string disable3D = Environment.GetEnvironmentVariable("SLIDE_DISABLE_3D");
            if (!string.IsNullOrWhiteSpace(disable3D))
            {
                LogInfo($"SLIDE_DISABLE_3D={disable3D} (main process shields 3D)");
            }
            
            string[] criticalDlls = {
                "ScottPlot.dll",
                "ScottPlot.WPF.dll",
                "EPPlus.dll",
                "System.Drawing.Common.dll"
            };

            // 检查基恩士3D检测关键原生DLL
            string[] nativeDlls = {
                "LjDevCommon.Core.dll",
                "LjDev3dView.Core.dll",
                "LjDevMeasure.Core.dll"
            };

            if (!shouldCheck3D)
            {
                // 3D已解耦：未部署3D组件时，不将Keyence 3D依赖缺失视为错误。
                nativeDlls = Array.Empty<string>();
            }

            LogInfo("检查关键DLL文件:");
            foreach (string dll in criticalDlls)
            {
                string dllPath = Path.Combine(exeDir, dll);
                if (File.Exists(dllPath))
                {
                    var fileInfo = new FileInfo(dllPath);
                    LogInfo($"  ✓ {dll} - 大小: {fileInfo.Length} bytes, 修改时间: {fileInfo.LastWriteTime}");
                }
                else
                {
                    LogError($"  ✗ 缺少文件: {dll}");
                }
            }

            LogInfo("\n检查原生DLL文件:");
            foreach (string dll in nativeDlls)
            {
                string dllPath = Path.Combine(exeDir, dll);
                if (File.Exists(dllPath))
                {
                    var fileInfo = new FileInfo(dllPath);
                    LogInfo($"  ✓ {dll} - 大小: {fileInfo.Length} bytes, 修改时间: {fileInfo.LastWriteTime}");
                }
                else
                {
                    LogError($"  ✗ 缺少关键原生文件: {dll}");
                }
            }

            // 检查Dll/Managed目录
            string managedDir = Path.Combine(exeDir, "Dll", "Managed");
            if (Directory.Exists(managedDir))
            {
                LogInfo($"\nDll/Managed目录存在，包含文件: {Directory.GetFiles(managedDir).Length}个");
                foreach (string file in Directory.GetFiles(managedDir, "*.dll"))
                {
                    LogInfo($"  - {Path.GetFileName(file)}");
                }
                
                // 检查关键的基恩士托管DLL
                string[] keyenceDlls = {
                    "LjDevCommon.dll",
                    "LjDev3dView.dll", 
                    "LjDevMeasure.dll",
                    "LjdSampleWrapper.dll"
                };

                if (!shouldCheck3D)
                {
                    keyenceDlls = Array.Empty<string>();
                }
                    
                LogInfo("\n检查基恩士托管DLL:");
                foreach (string dll in keyenceDlls)
                {
                    string dllPath = Path.Combine(managedDir, dll);
                    if (File.Exists(dllPath))
                    {
                        LogInfo($"  ✓ {dll}");
                    }
                    else
                    {
                        LogError($"  ✗ 缺少: {dll}");
                    }
                }
            }
            else
            {
                LogError("Dll/Managed目录不存在 - 这是程序无法启动的主要原因!");
            }
        }

        /// <summary>
        /// 检查.NET Framework版本
        /// </summary>
        private static void CheckDotNetFramework()
        {
            try
            {
                // 检查.NET Framework 4.7.2是否安装
                var netFrameworkKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\");
                if (netFrameworkKey != null)
                {
                    var releaseKey = netFrameworkKey.GetValue("Release");
                    if (releaseKey != null)
                    {
                        int release = (int)releaseKey;
                        LogInfo($".NET Framework Release版本: {release}");
                        
                        if (release >= 461808) // .NET Framework 4.7.2
                        {
                            LogInfo("✓ .NET Framework 4.7.2或更高版本已安装");
                        }
                        else
                        {
                            LogError($"✗ .NET Framework版本过低，需要4.7.2或更高版本 (当前Release: {release})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("检查.NET Framework版本时出错", ex);
            }
        }

        /// <summary>
        /// 显示错误对话框
        /// </summary>
        public static void ShowErrorDialog(string message, Exception ex = null)
        {
            try
            {
                string fullMessage = $"程序启动失败:\n{message}";
                if (ex != null)
                {
                    fullMessage += $"\n\n错误详情:\n{BuildExceptionDetails(ex)}";

                    if (IsMissingSqliteNative(ex))
                    {
                        fullMessage += "\n\n可能原因: 缺少 e_sqlite3.dll，请先还原 NuGet 并重新生成。";
                    }
                }
                fullMessage += $"\n\n详细日志已保存到:\n{_logPath}";

                MessageBox.Show(fullMessage, "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // 如果连MessageBox都无法显示，则尝试写入文件
                try
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "WpfApp2_critical_error.txt"), 
                        $"Critical Error: {message}\nException: {ex?.ToString()}");
                }
                catch
                {
                    // 最后的尝试失败，无能为力
                }
            }
        }

        /// <summary>
        /// 获取日志路径
        /// </summary>
        public static string GetLogPath()
        {
            return _logPath;
        }

        private static string BuildExceptionDetails(Exception ex)
        {
            var lines = new List<string>();
            var current = ex;
            var depth = 0;

            while (current != null && depth < 6)
            {
                lines.Add($"{current.GetType().Name}: {current.Message}");
                current = current.InnerException;
                depth++;
            }

            return string.Join("\n", lines);
        }

        private static bool IsMissingSqliteNative(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is DllNotFoundException && current.Message != null && current.Message.Contains("e_sqlite3"))
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }
    }
}
