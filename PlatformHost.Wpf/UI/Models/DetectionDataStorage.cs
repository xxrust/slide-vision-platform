using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace WpfApp2.UI.Models
{
    /// <summary>
    /// 单次检测记录
    /// </summary>
    public class DetectionRecord
    {
        /// <summary>
        /// 检测时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 缺陷类型
        /// </summary>
        public string DefectType { get; set; }

        /// <summary>
        /// 检测项目和对应数值的字典
        /// Key: 项目名称，Value: 测量数值
        /// </summary>
        public Dictionary<string, DetectionItemValue> DetectionItems { get; set; }

        /// <summary>
        /// LOT号
        /// </summary>
        public string LotNumber { get; set; }

        /// <summary>
        /// 图片序号（用于标识具体是哪张图片的数据）
        /// </summary>
        public string ImageNumber { get; set; }

        /// <summary>
        /// 是否为良品
        /// </summary>
        public bool IsOK => DefectType == "良品";

        public DetectionRecord()
        {
            DetectionItems = new Dictionary<string, DetectionItemValue>();
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 检测项目数值信息
    /// </summary>
    public class DetectionItemValue
    {
        /// <summary>
        /// 测量数值
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// 数值字符串（用于显示）
        /// </summary>
        public string ValueString { get; set; }

        /// <summary>
        /// 数值字符串（用于显示） - 别名属性
        /// </summary>
        public string StringValue 
        { 
            get => ValueString; 
            set => ValueString = value; 
        }

        /// <summary>
        /// 是否有实际检测数据（区分空数据和0值）
        /// </summary>
        public bool HasValidData { get; set; }

        /// <summary>
        /// 下限
        /// </summary>
        public double LowerLimit { get; set; }

        /// <summary>
        /// 上限
        /// </summary>
        public double UpperLimit { get; set; }

        /// <summary>
        /// 是否超出范围
        /// </summary>
        public bool IsOutOfRange { get; set; }

        /// <summary>
        /// 是否为3D检测项目
        /// </summary>
        public bool Is3DItem { get; set; }

        /// <summary>
        /// 工具索引（用于3D检测）
        /// </summary>
        public int ToolIndex { get; set; }

        /// <summary>
        /// 是否已处理告警（用于滑动窗口机制）
        /// 当超限数据触发告警后，用户关闭告警窗口时，会将此标记设为true
        /// 下次计算超限次数时，会排除这些已处理的数据
        /// </summary>
        public bool IsAlertProcessed { get; set; } = false;
    }

    /// <summary>
    /// 检测数据存储管理器
    /// </summary>
    public static class DetectionDataStorage
    {
        private static readonly ConcurrentQueue<DetectionRecord> _detectionHistory = new ConcurrentQueue<DetectionRecord>();
        private static readonly object _lockObject = new object();
        private const int MaxRecords = 10000; // 最大存储10000条记录

        /// <summary>
        /// 添加新的检测记录
        /// </summary>
        /// <param name="record">检测记录</param>
        public static void AddDetectionRecord(DetectionRecord record)
        {
            lock (_lockObject)
            {
                _detectionHistory.Enqueue(record);

                // 保持最大记录数限制
                while (_detectionHistory.Count > MaxRecords)
                {
                    _detectionHistory.TryDequeue(out _);
                }
                
                // 同时记录到实时数据文件
                try
                {
                    RealTimeDataLogger.Instance.LogDetectionData(record);
                }
                catch (Exception ex)
                {
                    // 实时记录失败不应影响主流程，只记录错误日志
                    System.Diagnostics.Debug.WriteLine($"实时数据记录失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 添加新的检测记录（简化版本）
        /// </summary>
        /// <param name="defectType">缺陷类型</param>
        /// <param name="lotNumber">LOT号</param>
        /// <param name="detectionItems">检测项目数据</param>
                public static void AddRecord(string defectType, string lotNumber, Dictionary<string, DetectionItemValue> detectionItems, string imageNumber = "")
        {
            var record = new DetectionRecord
            {
                DefectType = defectType,
                LotNumber = lotNumber,
                DetectionItems = detectionItems,
                Timestamp = DateTime.Now,
                ImageNumber = imageNumber ?? ""
            };

            AddDetectionRecord(record);
        }

        /// <summary>
        /// 获取所有记录
        /// </summary>
        /// <returns>所有检测记录</returns>
        public static List<DetectionRecord> GetAllRecords()
        {
            lock (_lockObject)
            {
                return _detectionHistory.ToList();
            }
        }

        /// <summary>
        /// 获取最近的N条记录
        /// </summary>
        /// <param name="count">记录数量，默认获取全部</param>
        /// <returns>检测记录列表</returns>
        public static List<DetectionRecord> GetRecentRecords(int count = 0)
        {
            lock (_lockObject)
            {
                var allRecords = _detectionHistory.ToList();
                
                if (count <= 0 || count >= allRecords.Count)
                {
                    return allRecords;
                }

                // 返回最后的count条记录
                return allRecords.Skip(Math.Max(0, allRecords.Count - count)).ToList();
            }
        }

        /// <summary>
        /// 获取所有可用的检测项目名称
        /// </summary>
        /// <returns>项目名称列表</returns>
        public static List<string> GetAllItemNames()
        {
            lock (_lockObject)
            {
                var itemNames = new HashSet<string>();
                
                foreach (var record in _detectionHistory)
                {
                    foreach (var item in record.DetectionItems.Keys)
                    {
                        itemNames.Add(item);
                    }
                }

                return itemNames.OrderBy(name => name).ToList();
            }
        }

        /// <summary>
        /// 获取指定项目的所有数值
        /// </summary>
        /// <param name="itemName">项目名称</param>
        /// <param name="count">记录数量，0表示全部</param>
        /// <returns>数值列表</returns>
        public static List<double> GetItemValues(string itemName, int count = 0)
        {
            lock (_lockObject)
            {
                var records = GetRecentRecords(count);
                var values = new List<double>();

                foreach (var record in records)
                {
                    if (record.DetectionItems.ContainsKey(itemName))
                    {
                        var item = record.DetectionItems[itemName];
                        // 只添加有有效数据的值（排除空数据）
                        if (item.HasValidData)
                        {
                            values.Add(item.Value);
                        }
                    }
                }

                return values;
            }
        }

        /// <summary>
        /// 获取指定项目的所有数值（包含空数据标识）
        /// </summary>
        /// <param name="itemName">项目名称</param>
        /// <param name="count">记录数量，0表示全部</param>
        /// <returns>包含有效性的数值列表</returns>
        public static List<(double Value, bool HasValidData)> GetItemValuesWithValidity(string itemName, int count = 0)
        {
            lock (_lockObject)
            {
                var records = GetRecentRecords(count);
                var values = new List<(double, bool)>();

                foreach (var record in records)
                {
                    if (record.DetectionItems.ContainsKey(itemName))
                    {
                        var item = record.DetectionItems[itemName];
                        values.Add((item.Value, item.HasValidData));
                    }
                }

                return values;
            }
        }

        /// <summary>
        /// 获取指定项目的限制值
        /// </summary>
        /// <param name="itemName">项目名称</param>
        /// <returns>下限和上限的元组</returns>
        public static (double LowerLimit, double UpperLimit) GetItemLimits(string itemName)
        {
            lock (_lockObject)
            {
                // 从最新的记录开始向前查找，找到第一个有有效上下限的记录
                foreach (var record in _detectionHistory.Reverse())
                {
                    if (record.DetectionItems.ContainsKey(itemName))
                    {
                        var item = record.DetectionItems[itemName];
                        
                        // 检查是否有有效的上下限（不为默认值且不为NaN，0是有效值）
                        if (item.LowerLimit != double.MinValue && item.UpperLimit != double.MaxValue &&
                            !double.IsNaN(item.LowerLimit) && !double.IsNaN(item.UpperLimit) &&
                            !double.IsInfinity(item.LowerLimit) && !double.IsInfinity(item.UpperLimit))
                        {
                            return (item.LowerLimit, item.UpperLimit);
                        }
                    }
                }

                // 如果没有找到有效的上下限，返回默认值
                return (double.MinValue, double.MaxValue);
            }
        }

        /// <summary>
        /// 获取总记录数
        /// </summary>
        /// <returns>总记录数</returns>
        public static int GetTotalRecordCount()
        {
            return _detectionHistory.Count;
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public static void ClearAllRecords()
        {
            lock (_lockObject)
            {
                while (_detectionHistory.TryDequeue(out _)) { }
            }
        }

        /// <summary>
        /// 标记指定项目在当前滑动窗口内的超限数据为"已处理告警"
        /// 这是滑动窗口机制的核心：不删除数据，但让已告警的超限数据不再计入下次计算
        /// </summary>
        /// <param name="itemName">项目名称</param>
        /// <param name="windowSize">滑动窗口大小</param>
        /// <returns>标记的数据条数</returns>
        public static int MarkOutOfRangeAsAlertProcessed(string itemName, int windowSize)
        {
            lock (_lockObject)
            {
                try
                {
                    if (string.IsNullOrEmpty(itemName) || windowSize <= 0)
                    {
                        return 0;
                    }

                    // 获取最近的windowSize条记录
                    var recentRecords = _detectionHistory
                        .Where(r => r.DetectionItems.ContainsKey(itemName))
                        .OrderByDescending(r => r.Timestamp)
                        .Take(windowSize)
                        .ToList();

                    int markedCount = 0;

                    foreach (var record in recentRecords)
                    {
                        if (record.DetectionItems.ContainsKey(itemName))
                        {
                            var itemValue = record.DetectionItems[itemName];
                            // 如果是超限且尚未处理，则标记为已处理
                            if (itemValue.IsOutOfRange && !itemValue.IsAlertProcessed)
                            {
                                itemValue.IsAlertProcessed = true;
                                markedCount++;
                            }
                        }
                    }

                    LogManager.Info($"✅ 已将项目 {itemName} 在滑动窗口内的 {markedCount} 个超限数据标记为已处理");
                    return markedCount;
                }
                catch (Exception ex)
                {
                    LogManager.Error($"标记告警处理状态失败: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// 获取用于Excel导出的数据
        /// </summary>
        /// <param name="count">记录数量</param>
        /// <returns>导出数据</returns>
        public static List<Dictionary<string, object>> GetExportData(int count = 0)
        {
            lock (_lockObject)
            {
                var records = GetRecentRecords(count);
                var exportData = new List<Dictionary<string, object>>();

                foreach (var record in records)
                {
                    var row = new Dictionary<string, object>
                    {
                        ["时间戳"] = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["LOT号"] = record.LotNumber ?? "",
                        ["缺陷类型"] = record.DefectType ?? "",
                        ["结果"] = record.IsOK ? "OK" : "NG"
                    };

                    // 添加所有检测项目的数值
                    foreach (var item in record.DetectionItems)
                    {
                        // 对于没有有效数据的项目，显示空字符串
                        row[item.Key] = item.Value.HasValidData ? item.Value.Value : (object)"";
                        row[$"{item.Key}_下限"] = item.Value.LowerLimit;
                        row[$"{item.Key}_上限"] = item.Value.UpperLimit;
                        row[$"{item.Key}_超限"] = item.Value.HasValidData ? (item.Value.IsOutOfRange ? "是" : "否") : "";
                    }

                    exportData.Add(row);
                }

                return exportData;
            }
        }
    }
} 