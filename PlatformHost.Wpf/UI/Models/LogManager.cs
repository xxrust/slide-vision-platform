using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 统一日志管理器
    /// 整合文件日志、界面日志、弹窗提示等功能
    /// </summary>
    public static class LogManager
    {
        #region 线程安全

        /// <summary>
        /// 文件写入锁，确保多线程环境下文件写入的线程安全
        /// </summary>
        private static readonly object _fileLock = new object();

        #endregion

        #region 日志级别和配置

        /// <summary>
        /// 日志级别枚举
        /// </summary>
        public enum LogLevel
        {
            Debug,      // 调试信息（可完全关闭）
            Info,       // 一般信息
            Warning,    // 警告信息
            Error,      // 错误信息
            Critical    // 严重错误（始终显示和弹窗）
        }

        /// <summary>
        /// 日志配置
        /// </summary>
        public static class Config
        {
            /// <summary>
            /// 是否启用调试日志（默认关闭，生产环境建议关闭）
            /// </summary>
            public static bool EnableDebugLog { get; set; } = false;

            /// <summary>
            /// 是否启用详细日志（默认关闭，包括技术细节）
            /// </summary>
            public static bool EnableVerboseLog { get; set; } = false;

            /// <summary>
            /// 是否启用文件日志（默认开启）
            /// </summary>
            public static bool EnableFileLog { get; set; } = true;

            /// <summary>
            /// 是否启用界面日志（默认开启）
            /// </summary>
            public static bool EnableUILog { get; set; } = true;

            /// <summary>
            /// 错误级别是否自动弹窗（默认开启）
            /// </summary>
            public static bool EnableErrorPopup { get; set; } = true;

            /// <summary>
            /// 严重错误级别是否自动弹窗（默认开启，不建议关闭）
            /// </summary>
            public static bool EnableCriticalPopup { get; set; } = true;
        }

        #endregion

        #region 核心日志方法

        /// <summary>
        /// 记录日志（主要方法）
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="level">日志级别</param>
        /// <param name="source">日志来源（页面名称等）</param>
        /// <param name="forceShow">强制显示（忽略配置）</param>
        public static void Log(string message, LogLevel level = LogLevel.Info, string source = "", bool forceShow = false)
        {
            try
            {
                // 级别过滤
                if (!ShouldLog(level) && !forceShow)
                {
                    return;
                }

                // 构建完整消息
                string fullMessage = BuildLogMessage(message, level, source);
                string timestamp = DateTime.Now.ToString("yy-MM-dd HH:mm:ss.fff");

                // 异步处理，避免阻塞UI
                Task.Run(() =>
                {
                    // 文件日志
                    if (Config.EnableFileLog)
                    {
                        WriteToFile(timestamp, fullMessage, level);
                    }
                });

                // UI线程处理界面日志和弹窗
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 界面日志
                        if (Config.EnableUILog)
                        {
                            UpdateUILog(fullMessage);
                        }

                        // 弹窗处理
                        HandlePopup(fullMessage, level, forceShow);
                    }
                    catch (Exception ex)
                    {
                        // 界面日志失败时至少输出到控制台
                        Console.WriteLine($"UI日志更新失败: {ex.Message}");
                        Console.WriteLine($"原始消息: {fullMessage}");
                    }
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                // 日志系统本身出错时的兜底处理
                Console.WriteLine($"日志系统错误: {ex.Message}");
                Console.WriteLine($"原始消息: {message}");
            }
        }

        #endregion

        #region 便捷方法

        /// <summary>
        /// 调试日志（可被配置完全关闭）
        /// </summary>
        public static void Debug(string message, string source = "")
        {
            Log(message, LogLevel.Debug, source);
        }

        /// <summary>
        /// 信息日志
        /// </summary>
        public static void Info(string message, string source = "")
        {
            Log(message, LogLevel.Info, source);
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        public static void Warning(string message, string source = "")
        {
            Log(message, LogLevel.Warning, source);
        }

        /// <summary>
        /// 错误日志（会弹窗）
        /// </summary>
        public static void Error(string message, string source = "")
        {
            Log(message, LogLevel.Error, source);
        }

        /// <summary>
        /// 严重错误日志（强制弹窗）
        /// </summary>
        public static void Critical(string message, string source = "")
        {
            Log(message, LogLevel.Critical, source, forceShow: true);
        }

        /// <summary>
        /// 详细日志（技术细节，可被配置关闭）
        /// </summary>
        public static void Verbose(string message, string source = "")
        {
            if (Config.EnableVerboseLog)
            {
                Log($"[详细] {message}", LogLevel.Debug, source);
            }
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 判断是否应该记录此级别的日志
        /// </summary>
        private static bool ShouldLog(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return Config.EnableDebugLog;
                case LogLevel.Info:
                case LogLevel.Warning:
                case LogLevel.Error:
                case LogLevel.Critical:
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 构建日志消息
        /// </summary>
        private static string BuildLogMessage(string message, LogLevel level, string source)
        {
            string prefix = GetLevelPrefix(level);
            string sourcePrefix = string.IsNullOrEmpty(source) ? "" : $"[{source}] ";
            
            return $"{prefix}{sourcePrefix}{message}";
        }

        /// <summary>
        /// 获取日志级别前缀
        /// </summary>
        private static string GetLevelPrefix(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return "[调试] ";
                case LogLevel.Info:
                    return "";
                case LogLevel.Warning:
                    return "[警告] ";
                case LogLevel.Error:
                    return "[错误] ";
                case LogLevel.Critical:
                    return "[严重] ";
                default:
                    return "";
            }
        }

        /// <summary>
        /// 写入文件日志（线程安全）
        /// </summary>
        private static void WriteToFile(string timestamp, string message, LogLevel level)
        {
            try
            {
                lock (_fileLock)
                {
                    string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
                    if (!Directory.Exists(logDir))
                        Directory.CreateDirectory(logDir);

                    string logFile = Path.Combine(logDir, $"{DateTime.Now:yyyy-MM-dd}.log");
                    string logLine = $"[{timestamp}] [{level}] {message}";

                    // 使用FileStream和StreamWriter确保更好的文件访问控制
                    using (FileStream fileStream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (StreamWriter writer = new StreamWriter(fileStream, Encoding.UTF8))
                    {
                        writer.WriteLine(logLine);
                        writer.Flush(); // 确保立即写入
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入日志文件失败: {ex.Message}");
                // 可以考虑写入到备用日志文件
                try
                {
                    string fallbackFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fallback.log");
                    File.AppendAllText(fallbackFile, $"[{DateTime.Now:yy-MM-dd HH:mm:ss.fff}] [FALLBACK] {message}\r\n");
                }
                catch
                {
                    // 备用方案也失败时，只能输出到控制台
                }
            }
        }

        /// <summary>
        /// 更新界面日志
        /// </summary>
        private static void UpdateUILog(string message)
        {
            try
            {
                // 更新到Page1的日志显示（避免递归调用）
                WpfApp2.UI.Page1.PageManager.Page1Instance?.LogUpdate(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新界面日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理弹窗显示
        /// </summary>
        private static void HandlePopup(string message, LogLevel level, bool forceShow)
        {
            try
            {
                bool shouldPopup = forceShow;

                if (!shouldPopup)
                {
                    switch (level)
                    {
                        case LogLevel.Error:
                            shouldPopup = Config.EnableErrorPopup;
                            break;
                        case LogLevel.Critical:
                            shouldPopup = Config.EnableCriticalPopup;
                            break;
                    }
                }

                if (shouldPopup)
                {
                    MessageBoxImage icon = level == LogLevel.Critical ? MessageBoxImage.Stop :
                                         level == LogLevel.Error ? MessageBoxImage.Error :
                                         level == LogLevel.Warning ? MessageBoxImage.Warning :
                                         MessageBoxImage.Information;

                    string title = level == LogLevel.Critical ? "严重错误" :
                                  level == LogLevel.Error ? "错误" :
                                  level == LogLevel.Warning ? "警告" :
                                  "信息";

                    MessageBox.Show(message, title, MessageBoxButton.OK, icon);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示弹窗失败: {ex.Message}");
            }
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 从配置文件加载日志配置
        /// </summary>
        public static void LoadConfigFromFile()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "LogConfig.json");
                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath, Encoding.UTF8);
                    // 简单的JSON解析，避免依赖Newtonsoft.Json
                    if (jsonContent.Contains("\"ProductionMode\": true"))
                    {
                        SetProductionMode();
                        Info("配置文件加载成功，使用生产模式", "LogManager");
                    }
                    else if (jsonContent.Contains("\"ProductionMode\": false"))
                    {
                        SetDebugMode();
                        Info("配置文件加载成功，使用调试模式", "LogManager");
                    }
                    else
                    {
                        SetProductionMode();
                        Info("配置文件格式不明确，使用默认生产模式", "LogManager");
                    }
                }
                else
                {
                    // 配置文件不存在，使用默认的生产模式配置
                    SetProductionMode();
                    Info("配置文件不存在，使用默认生产模式配置", "LogManager");
                }
            }
            catch (Exception ex)
            {
                // 配置加载失败，使用默认配置
                SetProductionMode();
                Error($"加载日志配置失败，使用默认配置: {ex.Message}", "LogManager");
            }
        }

        /// <summary>
        /// 设置生产模式（关闭调试和详细日志）
        /// </summary>
        public static void SetProductionMode()
        {
            Config.EnableDebugLog = false;
            Config.EnableVerboseLog = false;
            Config.EnableFileLog = true;
            Config.EnableUILog = true;
            Config.EnableErrorPopup = true;
            Config.EnableCriticalPopup = true;
        }

        /// <summary>
        /// 设置调试模式（开启所有日志）
        /// </summary>
        public static void SetDebugMode()
        {
            Config.EnableDebugLog = true;
            Config.EnableVerboseLog = true;
            Config.EnableFileLog = true;
            Config.EnableUILog = true;
            Config.EnableErrorPopup = true;
            Config.EnableCriticalPopup = true;
        }

        /// <summary>
        /// 设置静默模式（只记录文件，不弹窗）
        /// </summary>
        public static void SetSilentMode()
        {
            Config.EnableDebugLog = false;
            Config.EnableVerboseLog = false;
            Config.EnableFileLog = true;
            Config.EnableUILog = true;
            Config.EnableErrorPopup = false;
            Config.EnableCriticalPopup = false;
        }

        #endregion
    }
} 