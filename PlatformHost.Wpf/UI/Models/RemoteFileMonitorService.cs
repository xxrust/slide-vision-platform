using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 远程文件监控服务
    /// 定时检测远程配置文件，自动切换LOT号和模板
    /// </summary>
    public class RemoteFileMonitorService : IDisposable
    {
        private static RemoteFileMonitorService _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static RemoteFileMonitorService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new RemoteFileMonitorService();
                        }
                    }
                }
                return _instance;
            }
        }

        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitorTask;
        private RemoteSourceConfig _config;
        private bool _isRunning;

        /// <summary>
        /// 监控是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// LOT变更事件
        /// </summary>
        public event Action<string> OnLotChanged;

        /// <summary>
        /// 模板变更事件
        /// </summary>
        public event Action<string> OnTemplateChanged;

        /// <summary>
        /// 模板不存在事件
        /// </summary>
        public event Action<string> OnTemplateNotFound;

        /// <summary>
        /// 远程文件读取失败事件
        /// </summary>
        public event Action<string> OnRemoteFileError;

        /// <summary>
        /// 状态变更事件（用于日志记录）
        /// </summary>
        public event Action<string> OnStatusChanged;

        private RemoteFileMonitorService()
        {
            _config = RemoteSourceConfig.Load();
        }

        /// <summary>
        /// 启动监控服务
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                LogManager.Warning("远程文件监控服务已在运行中");
                return;
            }

            _config = RemoteSourceConfig.Load();

            if (!_config.IsEnabled)
            {
                LogManager.Info("远程文件监控服务未启用");
                return;
            }

            if (string.IsNullOrWhiteSpace(_config.LotFilePath) && string.IsNullOrWhiteSpace(_config.TemplateFilePath))
            {
                LogManager.Warning("LOT和模板文件路径均未配置，监控服务未启动");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            _monitorTask = Task.Run(() => MonitorLoop(_cancellationTokenSource.Token));

            OnStatusChanged?.Invoke($"远程文件监控服务已启动，检测间隔: {_config.CheckIntervalMs}ms");
            LogManager.Info($"远程文件监控服务已启动: LOT={_config.LotFilePath}, 模板={_config.TemplateFilePath}");
        }

        /// <summary>
        /// 停止监控服务
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _cancellationTokenSource?.Cancel();
            _isRunning = false;

            try
            {
                _monitorTask?.Wait(3000); // 最多等待3秒
            }
            catch (Exception ex)
            {
                LogManager.Error($"停止监控服务时出错: {ex.Message}");
            }

            OnStatusChanged?.Invoke("远程文件监控服务已停止");
            LogManager.Info("远程文件监控服务已停止");
        }

        /// <summary>
        /// 重新加载配置并重启服务
        /// </summary>
        public void Reload()
        {
            // 保留上次读取的值
            string lastLot = _config?.LastLotValue;
            string lastTemplate = _config?.LastTemplateName;

            Stop();
            _config = RemoteSourceConfig.Load();

            // 恢复上次读取的值（这样重启后可以检测到变更）
            if (!string.IsNullOrEmpty(lastLot))
            {
                _config.LastLotValue = lastLot;
            }
            if (!string.IsNullOrEmpty(lastTemplate))
            {
                _config.LastTemplateName = lastTemplate;
            }

            if (_config.IsEnabled)
            {
                Start();
            }

            LogManager.Info($"[远程监控] 服务已重新加载, LastLot={_config.LastLotValue}, LastTemplate={_config.LastTemplateName}");
        }

        /// <summary>
        /// 监控循环
        /// </summary>
        private async Task MonitorLoop(CancellationToken cancellationToken)
        {
            // 如果已经有上次的值，则不是首次读取（直接进行比较）
            bool hasExistingValues = !string.IsNullOrEmpty(_config.LastLotValue) || !string.IsNullOrEmpty(_config.LastTemplateName);
            bool isFirstRead = !hasExistingValues;

            LogManager.Info($"[远程监控] 监控循环启动, hasExistingValues={hasExistingValues}, isFirstRead={isFirstRead}");

            // 首次读取，初始化当前值或检测变更
            await CheckRemoteFile(isFirstRead: isFirstRead);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.CheckIntervalMs, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await CheckRemoteFile(isFirstRead: false);
                }
                catch (TaskCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    LogManager.Error($"远程文件监控循环异常: {ex.Message}");
                    OnRemoteFileError?.Invoke($"监控循环异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查远程文件
        /// </summary>
        /// <param name="isFirstRead">是否是首次读取（首次读取不触发变更事件）</param>
        private async Task CheckRemoteFile(bool isFirstRead)
        {
            try
            {
                // 检查LOT文件
                if (!string.IsNullOrWhiteSpace(_config.LotFilePath))
                {
                    await CheckLotFile(isFirstRead);
                }

                // 检查模板文件
                if (!string.IsNullOrWhiteSpace(_config.TemplateFilePath))
                {
                    await CheckTemplateFile(isFirstRead);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"检查远程文件时出错: {ex.Message}");
                OnRemoteFileError?.Invoke($"读取远程文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查LOT文件
        /// </summary>
        private async Task CheckLotFile(bool isFirstRead)
        {
            try
            {
                string filePath = GetAbsoluteFilePath(_config.LotFilePath);
                LogManager.Info($"[远程监控] 检查LOT文件: {filePath}, 首次读取: {isFirstRead}");

                if (!File.Exists(filePath))
                {
                    OnRemoteFileError?.Invoke($"LOT文件不存在: {filePath}");
                    LogManager.Warning($"LOT文件不存在: {filePath}");
                    return;
                }

                string fileContent = await ReadFileWithRetry(filePath);
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    OnRemoteFileError?.Invoke("LOT文件内容为空");
                    return;
                }

                string lotValue = fileContent.Trim();
                LogManager.Info($"[远程监控] LOT文件内容: '{lotValue}', 上次值: '{_config.LastLotValue}'");

                if (isFirstRead)
                {
                    _config.LastLotValue = lotValue;
                    LogManager.Info($"初始化远程LOT值: {lotValue}");
                }
                else if (_config.LastLotValue != lotValue)
                {
                    string oldLot = _config.LastLotValue;
                    _config.LastLotValue = lotValue;
                    LogManager.Info($"检测到LOT变更: {oldLot} -> {lotValue}, 准备触发事件");

                    if (OnLotChanged != null)
                    {
                        LogManager.Info($"[远程监控] 触发OnLotChanged事件");
                        OnLotChanged.Invoke(lotValue);
                    }
                    else
                    {
                        LogManager.Warning("[远程监控] OnLotChanged事件没有订阅者!");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"检查LOT文件时出错: {ex.Message}");
                OnRemoteFileError?.Invoke($"读取LOT文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查模板文件
        /// </summary>
        private async Task CheckTemplateFile(bool isFirstRead)
        {
            try
            {
                string filePath = GetAbsoluteFilePath(_config.TemplateFilePath);
                LogManager.Info($"[远程监控] 检查模板文件: {filePath}, 首次读取: {isFirstRead}");

                if (!File.Exists(filePath))
                {
                    OnRemoteFileError?.Invoke($"模板文件不存在: {filePath}");
                    LogManager.Warning($"模板文件不存在: {filePath}");
                    return;
                }

                string fileContent = await ReadFileWithRetry(filePath);
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    OnRemoteFileError?.Invoke("模板文件内容为空");
                    return;
                }

                string templateName = fileContent.Trim();
                LogManager.Info($"[远程监控] 模板文件内容: '{templateName}', 上次值: '{_config.LastTemplateName}'");

                if (isFirstRead)
                {
                    _config.LastTemplateName = templateName;
                    LogManager.Info($"初始化远程模板名: {templateName}");
                }
                else if (_config.LastTemplateName != templateName)
                {
                    string oldTemplate = _config.LastTemplateName;
                    _config.LastTemplateName = templateName;
                    LogManager.Info($"检测到模板变更: {oldTemplate} -> {templateName}");

                    // 检查模板是否存在
                    string templatePath = FindTemplatePath(templateName);
                    if (string.IsNullOrEmpty(templatePath))
                    {
                        LogManager.Warning($"模板不存在: {templateName}, 准备触发OnTemplateNotFound事件");
                        if (OnTemplateNotFound != null)
                        {
                            LogManager.Info($"[远程监控] 触发OnTemplateNotFound事件");
                            OnTemplateNotFound.Invoke(templateName);
                        }
                        else
                        {
                            LogManager.Warning("[远程监控] OnTemplateNotFound事件没有订阅者!");
                        }
                    }
                    else
                    {
                        LogManager.Info($"模板存在，准备加载: {templatePath}");
                        if (OnTemplateChanged != null)
                        {
                            LogManager.Info($"[远程监控] 触发OnTemplateChanged事件");
                            OnTemplateChanged.Invoke(templatePath);
                        }
                        else
                        {
                            LogManager.Warning("[远程监控] OnTemplateChanged事件没有订阅者!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"检查模板文件时出错: {ex.Message}");
                OnRemoteFileError?.Invoke($"读取模板文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取绝对文件路径
        /// </summary>
        private string GetAbsoluteFilePath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            // 相对路径基于程序目录
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
        }

        /// <summary>
        /// 带重试的文件读取（处理文件被占用的情况）
        /// </summary>
        private async Task<string> ReadFileWithRetry(string filePath, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // 使用FileShare.ReadWrite允许其他进程同时读写
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        return await sr.ReadToEndAsync();
                    }
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    await Task.Delay(500); // 等待500ms后重试
                }
            }

            return null;
        }

        /// <summary>
        /// 查找模板文件路径
        /// </summary>
        /// <param name="templateName">模板名称（不含扩展名）</param>
        /// <returns>模板文件完整路径，如果不存在返回null</returns>
        private string FindTemplatePath(string templateName)
        {
            string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");

            if (!Directory.Exists(templatesDir))
                return null;

            // 尝试直接匹配 .json 文件
            string directPath = Path.Combine(templatesDir, $"{templateName}.json");
            if (File.Exists(directPath))
                return directPath;

            // 尝试在子目录中查找
            foreach (var dir in Directory.GetDirectories(templatesDir))
            {
                string subPath = Path.Combine(dir, $"{templateName}.json");
                if (File.Exists(subPath))
                    return subPath;
            }

            // 尝试模糊匹配（模板名可能包含或不包含扩展名）
            string templateNameWithoutExt = Path.GetFileNameWithoutExtension(templateName);
            string[] matchingFiles = Directory.GetFiles(templatesDir, $"*{templateNameWithoutExt}*.json", SearchOption.AllDirectories);
            if (matchingFiles.Length > 0)
            {
                // 优先选择完全匹配的
                foreach (var file in matchingFiles)
                {
                    if (Path.GetFileNameWithoutExtension(file).Equals(templateNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
                return matchingFiles[0];
            }

            return null;
        }

        /// <summary>
        /// 手动触发一次检测（用于测试）
        /// </summary>
        public async Task ManualCheck()
        {
            await CheckRemoteFile(isFirstRead: false);
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public RemoteSourceConfig GetConfig()
        {
            return _config;
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public void UpdateConfig(RemoteSourceConfig config)
        {
            _config = config;
            _config.Save();
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
        }
    }
}
