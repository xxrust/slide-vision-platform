using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 实时数据记录器 - 高效批量保存检测数据到CSV文件
    /// </summary>
    public class RealTimeDataLogger
    {
        #region 私有字段
        
        private static RealTimeDataLogger _instance;
        private static readonly object _instanceLock = new object();
        
        private readonly List<DetectionRecord> _buffer = new List<DetectionRecord>();
        private readonly object _bufferLock = new object();
        
        private Timer _flushTimer;
        private string _currentLotNumber;
        private string _currentMainCsvFilePath;         // 主CSV文件路径（所有数据）
        private Dictionary<string, string> _ngCsvFilePaths; // NG分类CSV文件路径字典
        private Dictionary<string, List<DetectionRecord>> _ngBuffers; // NG分类缓存字典
        private readonly Dictionary<string, List<RealTimeDataExportColumn>> _fileExportPlans = new Dictionary<string, List<RealTimeDataExportColumn>>(StringComparer.OrdinalIgnoreCase);
        
        // 配置参数
        private const int BATCH_SIZE = 10;              // 批量写入大小
        private const int FLUSH_INTERVAL_MS = 5000;    // 定时刷新间隔（5秒）
        private const string DATA_FOLDER = "RealTimeData"; // 数据存储文件夹
        
        #endregion

        #region 单例模式
        
        /// <summary>
        /// 获取实时数据记录器实例
        /// </summary>
        public static RealTimeDataLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new RealTimeDataLogger();
                        }
                    }
                }
                return _instance;
            }
        }
        
        #endregion

        #region 构造函数
        
        private RealTimeDataLogger()
        {
            _ngCsvFilePaths = new Dictionary<string, string>();
            _ngBuffers = new Dictionary<string, List<DetectionRecord>>();
            InitializeTimer();
            EnsureDataDirectoryExists();
        }
        
        #endregion

        #region 公共方法
        
        /// <summary>
        /// 记录检测数据
        /// </summary>
        /// <param name="record">检测记录</param>
        public void LogDetectionData(DetectionRecord record)
        {
            if (record == null) return;
            
            try
            {
                lock (_bufferLock)
                {
                    // 检查LOT号是否变更
                    if (_currentLotNumber != record.LotNumber)
                    {
                        // LOT号变更，先刷新旧数据，然后创建新文件
                        FlushAllBuffersToFile();
                        UpdateLotNumber(record.LotNumber);
                    }
                    
                    // 添加到主缓存（包含所有数据）
                    _buffer.Add(record);
                    
                    // 如果是NG数据，同时添加到对应的NG分类缓存
                    if (!record.IsOK && !string.IsNullOrEmpty(record.DefectType) && record.DefectType != "良品")
                    {
                        string defectType = record.DefectType;
                        
                        // 如果这是新的NG类型，创建对应的缓存和文件路径
                        if (!_ngBuffers.ContainsKey(defectType))
                        {
                            _ngBuffers[defectType] = new List<DetectionRecord>();
                            _ngCsvFilePaths[defectType] = GenerateNgCsvFilePath(record.LotNumber, defectType);
                        }
                        
                        _ngBuffers[defectType].Add(record);
                    }
                    
                    // 检查是否需要刷新（主缓存或任一NG缓存满时）
                    bool shouldFlush = _buffer.Count >= BATCH_SIZE;
                    if (!shouldFlush)
                    {
                        foreach (var ngBuffer in _ngBuffers.Values)
                        {
                            if (ngBuffer.Count >= BATCH_SIZE)
                            {
                                shouldFlush = true;
                                break;
                            }
                        }
                    }
                    
                    if (shouldFlush)
                    {
                        FlushAllBuffersToFile();
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"记录检测数据失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 手动刷新缓存数据到文件
        /// </summary>
        public void Flush()
        {
            lock (_bufferLock)
            {
                FlushAllBuffersToFile();
            }
        }
        
        /// <summary>
        /// 设置新的LOT号
        /// </summary>
        /// <param name="lotNumber">LOT号</param>
        public void SetLotNumber(string lotNumber)
        {
            if (string.IsNullOrWhiteSpace(lotNumber)) return;
            
            lock (_bufferLock)
            {
                if (_currentLotNumber != lotNumber)
                {
                    // 刷新旧数据
                    FlushAllBuffersToFile();
                    
                    // 更新LOT号
                    UpdateLotNumber(lotNumber);
                }
            }
        }
        
        /// <summary>
        /// 获取当前主CSV文件路径
        /// </summary>
        public string GetCurrentMainCsvFilePath()
        {
            return _currentMainCsvFilePath;
        }
        
        /// <summary>
        /// 获取所有NG分类CSV文件路径
        /// </summary>
        public Dictionary<string, string> GetNgCsvFilePaths()
        {
            lock (_bufferLock)
            {
                return new Dictionary<string, string>(_ngCsvFilePaths);
            }
        }
        
        /// <summary>
        /// 关闭记录器并保存所有数据
        /// </summary>
        public void Shutdown()
        {
            try
            {
                _flushTimer?.Dispose();
                
                lock (_bufferLock)
                {
                    FlushAllBuffersToFile();
                }
                
                LogManager.Info("实时数据记录器已关闭");
            }
            catch (Exception ex)
            {
                LogManager.Error($"关闭实时数据记录器失败: {ex.Message}");
            }
        }
        
        #endregion

        #region 私有方法
        
        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimer()
        {
            _flushTimer = new Timer(TimerCallback, null, FLUSH_INTERVAL_MS, FLUSH_INTERVAL_MS);
        }
        
        /// <summary>
        /// 定时器回调方法
        /// </summary>
        private void TimerCallback(object state)
        {
            try
            {
                lock (_bufferLock)
                {
                    // 检查是否有任何缓存有数据
                    bool hasData = _buffer.Count > 0;
                    if (!hasData)
                    {
                        foreach (var ngBuffer in _ngBuffers.Values)
                        {
                            if (ngBuffer.Count > 0)
                            {
                                hasData = true;
                                break;
                            }
                        }
                    }
                    
                    if (hasData)
                    {
                        FlushAllBuffersToFile();
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"定时刷新数据失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新LOT号
        /// </summary>
        private void UpdateLotNumber(string lotNumber)
        {
            _currentLotNumber = lotNumber;
            
            // 确保LOT目录存在
            EnsureLotDirectoryExists(lotNumber);
            
            _currentMainCsvFilePath = GenerateMainCsvFilePath(lotNumber);
            
            // 清空NG分类相关的数据（新LOT开始）
            _ngCsvFilePaths.Clear();
            _ngBuffers.Clear();
            _fileExportPlans.Clear();
            
            LogManager.Info($"实时数据记录器：切换到LOT {lotNumber}，主文件路径：{_currentMainCsvFilePath}");
        }
        
        /// <summary>
        /// 生成主CSV文件路径（包含所有数据）
        /// </summary>
        private string GenerateMainCsvFilePath(string lotNumber)
        {
            var now = DateTime.Now;
            var dateStr = now.ToString("yyyyMMdd");
            
            var fileName = $"{lotNumber}_{dateStr}_全部数据.csv";
            var lotDirectory = GetLotDirectory(lotNumber);
            
            return Path.Combine(lotDirectory, fileName);
        }
        
        /// <summary>
        /// 生成NG分类CSV文件路径
        /// </summary>
        private string GenerateNgCsvFilePath(string lotNumber, string defectType)
        {
            var now = DateTime.Now;
            var dateStr = now.ToString("yyyyMMdd");
            
            // 清理文件名中的特殊字符
            var safeDefectType = SanitizeFileName(defectType);
            var fileName = $"{lotNumber}_{dateStr}_NG_{safeDefectType}.csv";
            var lotDirectory = GetLotDirectory(lotNumber);
            
            return Path.Combine(lotDirectory, fileName);
        }
        
        /// <summary>
        /// 获取指定LOT号的数据目录路径
        /// </summary>
        private string GetLotDirectory(string lotNumber)
        {
            var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DATA_FOLDER);
            return Path.Combine(dataDirectory, lotNumber);
        }
        
        /// <summary>
        /// 确保数据目录存在
        /// </summary>
        private void EnsureDataDirectoryExists()
        {
            try
            {
                var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DATA_FOLDER);
                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"创建数据目录失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 确保LOT号对应的目录存在
        /// </summary>
        private void EnsureLotDirectoryExists(string lotNumber)
        {
            try
            {
                var lotDirectory = GetLotDirectory(lotNumber);
                if (!Directory.Exists(lotDirectory))
                {
                    Directory.CreateDirectory(lotDirectory);
                    LogManager.Info($"创建LOT目录: {lotDirectory}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"创建LOT目录失败: {ex.Message}");
            }
        }
        
        
        
        /// <summary>
        /// 将所有缓存数据刷新到对应文件
        /// </summary>
        private void FlushAllBuffersToFile()
        {
            try
            {
                int totalFlushedRecords = 0;
                
                // 刷新主缓存到主CSV文件
                if (_buffer.Count > 0 && !string.IsNullOrEmpty(_currentMainCsvFilePath))
                {
                    var mainCsvLines = new List<string>();
                    
                    // 检查文件是否存在，如果不存在则添加表头
                    bool needHeader = !File.Exists(_currentMainCsvFilePath);
                    var exportPlan = GetOrCreateExportPlanForFile(_currentMainCsvFilePath, _buffer.First(), needHeader);
                    if (needHeader)
                    {
                        var headerLine = CreateCsvHeaderFromPlan(exportPlan);
                        mainCsvLines.Add(headerLine);
                    }
                    
                    foreach (var record in _buffer)
                    {
                        var line = ConvertRecordToCsvLine(record, exportPlan);
                        if (!string.IsNullOrEmpty(line))
                        {
                            mainCsvLines.Add(line);
                        }
                    }
                    
                    if (mainCsvLines.Any())
                    {
                        // 异步写入主文件
                        Task.Run(() => WriteLinesToFile(_currentMainCsvFilePath, mainCsvLines));
                        totalFlushedRecords += mainCsvLines.Count - (needHeader ? 1 : 0); // 减去表头行
                    }
                    
                    // 清空主缓存
                    _buffer.Clear();
                }
                
                // 刷新各个NG分类缓存到对应的NG CSV文件
                foreach (var kvp in _ngBuffers.ToList())
                {
                    string defectType = kvp.Key;
                    var ngBuffer = kvp.Value;
                    
                    if (ngBuffer.Count > 0 && _ngCsvFilePaths.ContainsKey(defectType))
                    {
                        var ngCsvLines = new List<string>();
                        string ngFilePath = _ngCsvFilePaths[defectType];
                        
                        // 检查文件是否存在，如果不存在则添加表头
                        bool needHeader = !File.Exists(ngFilePath);
                        var exportPlan = GetOrCreateExportPlanForFile(ngFilePath, ngBuffer.First(), needHeader);
                        if (needHeader)
                        {
                            var headerLine = CreateCsvHeaderFromPlan(exportPlan);
                            ngCsvLines.Add(headerLine);
                        }
                        
                        foreach (var record in ngBuffer)
                        {
                            var line = ConvertRecordToCsvLine(record, exportPlan);
                            if (!string.IsNullOrEmpty(line))
                            {
                                ngCsvLines.Add(line);
                            }
                        }
                        
                        if (ngCsvLines.Any())
                        {
                            // 异步写入NG分类文件
                            Task.Run(() => WriteLinesToFile(ngFilePath, ngCsvLines));
                            totalFlushedRecords += ngCsvLines.Count - (needHeader ? 1 : 0); // 减去表头行
                        }
                        
                        // 清空NG缓存
                        ngBuffer.Clear();
                    }
                }
                
                if (totalFlushedRecords > 0)
                {
                    LogManager.Info($"实时数据记录器：已刷新 {totalFlushedRecords} 条记录到文件（主文件 + NG分类文件）");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"刷新数据到文件失败: {ex.Message}");
            }
        }

        private const string CsvHeaderImageNumber = "\u5e8f\u53f7";
        private const string CsvHeaderTimestamp = "\u65f6\u95f4\u6233";
        private const string CsvHeaderLotNumber = "LOT\u53f7";
        private const string CsvHeaderDefectType = "\u7f3a\u9677\u7c7b\u578b";
        private const string CsvHeaderResult = "\u7ed3\u679c";

        private const string CsvSuffixLowerLimit = "_\u4e0b\u9650";
        private const string CsvSuffixUpperLimit = "_\u4e0a\u9650";
        private const string CsvSuffixOutOfRange = "_\u8d85\u9650";

        private const string CsvYes = "\u662f";
        private const string CsvNo = "\u5426";

        private List<RealTimeDataExportColumn> GetOrCreateExportPlanForFile(string filePath, DetectionRecord sampleRecord, bool needHeader)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BuildDefaultPlan(sampleRecord);
            }

            if (_fileExportPlans.TryGetValue(filePath, out var cachedPlan))
            {
                return cachedPlan;
            }

            if (!needHeader)
            {
                if (TryLoadExportPlanFromExistingHeader(filePath, out var planFromHeader))
                {
                    _fileExportPlans[filePath] = planFromHeader;
                    return planFromHeader;
                }
            }

            var plan = BuildExportPlanFromConfig(sampleRecord);
            _fileExportPlans[filePath] = plan;
            return plan;
        }

        private List<RealTimeDataExportColumn> BuildExportPlanFromConfig(DetectionRecord record)
        {
            try
            {
                var config = RealTimeDataExportConfigManager.Load();
                if (config != null && config.Mode == RealTimeDataExportMode.Custom)
                {
                    var template = RealTimeDataExportConfigManager.GetActiveTemplate(config);
                    var cols = template?.Columns;
                    var normalized = NormalizeTemplateColumns(cols);
                    if (normalized.Count > 0)
                    {
                        return normalized;
                    }

                    LogManager.Warning("[实时数据导出] 自定义模板为空，自动回退到默认输出");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[实时数据导出] 加载导出配置失败，自动回退到默认输出: {ex.Message}");
            }

            return BuildDefaultPlan(record);
        }

        private List<RealTimeDataExportColumn> BuildDefaultPlan(DetectionRecord record)
        {
            var plan = new List<RealTimeDataExportColumn>
            {
                new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.ImageNumber },
                new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Timestamp },
                new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.LotNumber },
                new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.DefectType },
                new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Result }
            };

            var itemNames = record?.DetectionItems?.Keys?.OrderBy(k => k).ToList() ?? new List<string>();
            foreach (var itemName in itemNames)
            {
                plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = itemName, ItemField = RealTimeDataExportItemField.Value });
                plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = itemName, ItemField = RealTimeDataExportItemField.LowerLimit });
                plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = itemName, ItemField = RealTimeDataExportItemField.UpperLimit });
                plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = itemName, ItemField = RealTimeDataExportItemField.IsOutOfRange });
            }

            return plan;
        }

        private List<RealTimeDataExportColumn> NormalizeTemplateColumns(List<RealTimeDataExportColumn> columns)
        {
            var normalized = new List<RealTimeDataExportColumn>();
            if (columns == null)
            {
                return normalized;
            }

            foreach (var column in columns)
            {
                if (column == null)
                {
                    continue;
                }

                if (column.Kind == RealTimeDataExportColumnKind.Meta)
                {
                    if (column.MetaField == null)
                    {
                        continue;
                    }

                    normalized.Add(CloneExportColumn(column));
                    continue;
                }

                if (column.Kind == RealTimeDataExportColumnKind.Item)
                {
                    if (column.ItemField == null || string.IsNullOrWhiteSpace(column.ItemName))
                    {
                        continue;
                    }

                    normalized.Add(CloneExportColumn(column));
                }
            }

            return normalized;
        }

        private RealTimeDataExportColumn CloneExportColumn(RealTimeDataExportColumn column)
        {
            return new RealTimeDataExportColumn
            {
                Kind = column.Kind,
                MetaField = column.MetaField,
                ItemName = column.ItemName,
                ItemField = column.ItemField
            };
        }

        private bool TryLoadExportPlanFromExistingHeader(string filePath, out List<RealTimeDataExportColumn> exportPlan)
        {
            exportPlan = null;
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                using (var reader = new StreamReader(filePath, Encoding.UTF8, true))
                {
                    var headerLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerLine))
                    {
                        return false;
                    }

                    return TryBuildExportPlanFromHeaderLine(headerLine, out exportPlan);
                }
            }
            catch
            {
                exportPlan = null;
                return false;
            }
        }

        private bool TryBuildExportPlanFromHeaderLine(string headerLine, out List<RealTimeDataExportColumn> exportPlan)
        {
            exportPlan = null;
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return false;
            }

            var columns = SplitCsvLine(headerLine);
            if (columns.Count == 0)
            {
                return false;
            }

            var plan = new List<RealTimeDataExportColumn>();
            foreach (var raw in columns)
            {
                string col = (raw ?? string.Empty).Trim();
                if (col.Length == 0)
                {
                    continue;
                }

                if (string.Equals(col, CsvHeaderImageNumber, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.ImageNumber });
                    continue;
                }

                if (string.Equals(col, CsvHeaderTimestamp, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Timestamp });
                    continue;
                }

                if (string.Equals(col, CsvHeaderLotNumber, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.LotNumber });
                    continue;
                }

                if (string.Equals(col, CsvHeaderDefectType, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.DefectType });
                    continue;
                }

                if (string.Equals(col, CsvHeaderResult, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Result });
                    continue;
                }

                if (col.EndsWith(CsvSuffixLowerLimit, StringComparison.OrdinalIgnoreCase))
                {
                    var name = col.Substring(0, col.Length - CsvSuffixLowerLimit.Length);
                    plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = name, ItemField = RealTimeDataExportItemField.LowerLimit });
                    continue;
                }

                if (col.EndsWith(CsvSuffixUpperLimit, StringComparison.OrdinalIgnoreCase))
                {
                    var name = col.Substring(0, col.Length - CsvSuffixUpperLimit.Length);
                    plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = name, ItemField = RealTimeDataExportItemField.UpperLimit });
                    continue;
                }

                if (col.EndsWith(CsvSuffixOutOfRange, StringComparison.OrdinalIgnoreCase))
                {
                    var name = col.Substring(0, col.Length - CsvSuffixOutOfRange.Length);
                    plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = name, ItemField = RealTimeDataExportItemField.IsOutOfRange });
                    continue;
                }

                plan.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = col, ItemField = RealTimeDataExportItemField.Value });
            }

            if (plan.Count == 0)
            {
                return false;
            }

            exportPlan = plan;
            return true;
        }

        private List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null)
            {
                return result;
            }

            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == ',')
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    else if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }

            result.Add(current.ToString());
            return result;
        }

        private string CreateCsvHeaderFromPlan(List<RealTimeDataExportColumn> exportPlan)
        {
            try
            {
                var headers = new List<string>();
                foreach (var column in exportPlan ?? new List<RealTimeDataExportColumn>())
                {
                    if (column == null)
                    {
                        continue;
                    }

                    if (column.Kind == RealTimeDataExportColumnKind.Meta)
                    {
                        headers.Add(GetMetaHeader(column.MetaField));
                        continue;
                    }

                    string itemName = column.ItemName ?? string.Empty;
                    switch (column.ItemField)
                    {
                        case RealTimeDataExportItemField.Value:
                            headers.Add(itemName);
                            break;
                        case RealTimeDataExportItemField.LowerLimit:
                            headers.Add(itemName + CsvSuffixLowerLimit);
                            break;
                        case RealTimeDataExportItemField.UpperLimit:
                            headers.Add(itemName + CsvSuffixUpperLimit);
                            break;
                        case RealTimeDataExportItemField.IsOutOfRange:
                            headers.Add(itemName + CsvSuffixOutOfRange);
                            break;
                    }
                }

                return string.Join(",", headers);
            }
            catch (Exception ex)
            {
                LogManager.Error($"生成CSV表头失败: {ex.Message}");
                return string.Join(",", new[] { CsvHeaderImageNumber, CsvHeaderTimestamp, CsvHeaderLotNumber, CsvHeaderDefectType, CsvHeaderResult });
            }
        }

        private string GetMetaHeader(RealTimeDataExportMetaField? metaField)
        {
            switch (metaField)
            {
                case RealTimeDataExportMetaField.ImageNumber:
                    return CsvHeaderImageNumber;
                case RealTimeDataExportMetaField.Timestamp:
                    return CsvHeaderTimestamp;
                case RealTimeDataExportMetaField.LotNumber:
                    return CsvHeaderLotNumber;
                case RealTimeDataExportMetaField.DefectType:
                    return CsvHeaderDefectType;
                case RealTimeDataExportMetaField.Result:
                    return CsvHeaderResult;
                default:
                    return CsvHeaderResult;
            }
        }

        private string ConvertRecordToCsvLine(DetectionRecord record, List<RealTimeDataExportColumn> exportPlan)
        {
            try
            {
                var csvValues = new List<string>();
                foreach (var column in exportPlan ?? new List<RealTimeDataExportColumn>())
                {
                    if (column == null)
                    {
                        csvValues.Add("");
                        continue;
                    }

                    if (column.Kind == RealTimeDataExportColumnKind.Meta)
                    {
                        csvValues.Add(EscapeCsvValue(GetMetaValue(record, column.MetaField)));
                        continue;
                    }

                    csvValues.Add(EscapeCsvValue(GetItemValue(record, column.ItemName, column.ItemField)));
                }

                return string.Join(",", csvValues);
            }
            catch (Exception ex)
            {
                LogManager.Error($"转换CSV行失败: {ex.Message}");
                return "";
            }
        }

        private string GetMetaValue(DetectionRecord record, RealTimeDataExportMetaField? metaField)
        {
            if (record == null)
            {
                return string.Empty;
            }

            switch (metaField)
            {
                case RealTimeDataExportMetaField.ImageNumber:
                    return record.ImageNumber ?? string.Empty;
                case RealTimeDataExportMetaField.Timestamp:
                    return record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                case RealTimeDataExportMetaField.LotNumber:
                    return record.LotNumber ?? string.Empty;
                case RealTimeDataExportMetaField.DefectType:
                    return record.DefectType ?? string.Empty;
                case RealTimeDataExportMetaField.Result:
                    return record.IsOK ? "OK" : "NG";
                default:
                    return string.Empty;
            }
        }

        private string GetItemValue(DetectionRecord record, string itemName, RealTimeDataExportItemField? field)
        {
            if (record?.DetectionItems == null || string.IsNullOrWhiteSpace(itemName) || !record.DetectionItems.TryGetValue(itemName, out var item) || item == null)
            {
                return string.Empty;
            }

            switch (field)
            {
                case RealTimeDataExportItemField.Value:
                    return item.HasValidData ? item.Value.ToString("F4") : string.Empty;
                case RealTimeDataExportItemField.LowerLimit:
                    return IsValidLimit(item.LowerLimit) ? item.LowerLimit.ToString("F4") : string.Empty;
                case RealTimeDataExportItemField.UpperLimit:
                    return IsValidLimit(item.UpperLimit) ? item.UpperLimit.ToString("F4") : string.Empty;
                case RealTimeDataExportItemField.IsOutOfRange:
                    return item.HasValidData ? (item.IsOutOfRange ? CsvYes : CsvNo) : string.Empty;
                default:
                    return string.Empty;
            }
        }
        
        /// <summary>
        /// 清理文件名中的非法字符，生成安全的文件夹名
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "未知";
                
            // 替换非法字符为下划线
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }
            
            // 移除前后空白字符
            fileName = fileName.Trim();
            
            // 如果为空则使用默认名称
            if (string.IsNullOrEmpty(fileName))
                return "未知";
                
            return fileName;
        }

        /// <summary>
        /// 从记录生成CSV表头
        /// </summary>
        private string CreateCsvHeaderFromRecord(DetectionRecord record)
        {
            try
            {
                var headerColumns = new List<string> { "序号", "时间戳", "LOT号", "缺陷类型", "结果" };
                
                // 使用记录中的项目名称，按字母顺序排序保证一致性
                var itemNames = record.DetectionItems.Keys.OrderBy(k => k).ToList();
                
                // 为每个项目添加四列：值、下限、上限、是否超限
                foreach (var itemName in itemNames)
                {
                    headerColumns.AddRange(new[]
                    {
                        itemName,
                        $"{itemName}_下限",
                        $"{itemName}_上限",
                        $"{itemName}_超限"
                    });
                }
                
                return string.Join(",", headerColumns);
            }
            catch (Exception ex)
            {
                LogManager.Error($"生成CSV表头失败: {ex.Message}");
                return "序号,时间戳,LOT号,缺陷类型,结果";
            }
        }

        /// <summary>
        /// 将检测记录转换为CSV行
        /// </summary>
        private string ConvertRecordToCsvLine(DetectionRecord record)
        {
            try
            {
                var csvValues = new List<string>
                {
                    EscapeCsvValue(record.ImageNumber ?? ""),
                    EscapeCsvValue(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")),
                    EscapeCsvValue(record.LotNumber ?? ""),
                    EscapeCsvValue(record.DefectType ?? ""),
                    EscapeCsvValue(record.IsOK ? "OK" : "NG")
                };
                
                // 使用当前记录中的项目名称，按字母顺序排序保证一致性
                var itemNames = record.DetectionItems.Keys.OrderBy(k => k).ToList();
                
                // 为每个项目添加数据：值、下限、上限、是否超限
                foreach (var itemName in itemNames)
                {
                    var item = record.DetectionItems[itemName];
                    
                    // 项目值：对于没有有效数据的项目，显示空字符串
                    csvValues.Add(EscapeCsvValue(item.HasValidData ? item.Value.ToString("F4") : ""));
                    
                    // 下限和上限：始终显示，但过滤掉无效值
                    csvValues.Add(EscapeCsvValue(IsValidLimit(item.LowerLimit) ? item.LowerLimit.ToString("F4") : ""));
                    csvValues.Add(EscapeCsvValue(IsValidLimit(item.UpperLimit) ? item.UpperLimit.ToString("F4") : ""));
                    
                    // 是否超限：只有在有有效数据时才显示
                    csvValues.Add(EscapeCsvValue(item.HasValidData ? (item.IsOutOfRange ? "是" : "否") : ""));
                }
                
                return string.Join(",", csvValues);
            }
            catch (Exception ex)
            {
                LogManager.Error($"转换CSV行失败: {ex.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// 检查上下限值是否有效
        /// </summary>
        private bool IsValidLimit(double limitValue)
        {
            return limitValue != double.MinValue && 
                   limitValue != double.MaxValue &&
                   !double.IsNaN(limitValue) && 
                   !double.IsInfinity(limitValue);
        }
        
        /// <summary>
        /// 转义CSV值
        /// </summary>
        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
                
            // 如果包含逗号、引号或换行符，需要用引号包围并转义内部引号
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            
            return value;
        }
        
        /// <summary>
        /// 异步写入行到指定文件
        /// </summary>
        private async Task WriteLinesToFile(string filePath, List<string> lines)
        {
            try
            {
                using (var writer = new StreamWriter(filePath, append: true, Encoding.UTF8))
                {
                    foreach (var line in lines)
                    {
                        await writer.WriteLineAsync(line);
                    }
                    await writer.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"异步写入文件失败 ({filePath}): {ex.Message}");
            }
        }
        
        #endregion
    }
} 
