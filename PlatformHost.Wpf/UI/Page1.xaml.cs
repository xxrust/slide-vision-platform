using ScottPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Colors = ScottPlot.Colors;
using System.Runtime.Remoting.Contexts;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Path = System.IO.Path;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using Slide.ThreeD.Contracts;
using Slide.Platform.Abstractions;
using WpfApp2.ThreeD;
using WpfApp2.Algorithms;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfApp2.SMTGPIO;
using WpfApp2.UI.Models;
using WpfApp2.Models;
using LogManager = WpfApp2.UI.Models.LogManager;
using System.Threading;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;

namespace WpfApp2.UI
{
    /// <summary>
    /// Page1.xaml 的交互逻辑
    /// </summary>
    /// 

    public partial class Page1 : Page
    {

        private string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log", "Message"); // 日志路径
                                                                                                                  // 声明为成员变量

        // 3D视图相关
        // 3D已解耦：主进程不再承载Keyence 3D控件，避免加载3D相关DLL触发加密狗依赖。
        private System.Windows.Forms.Control _threeDViewHostChild = new System.Windows.Forms.Panel();
         
        // 2D视图相关
        // 2D控件同样保持为占位（如需恢复2D/3D渲染，放到独立Host/Tool里实现）。
        private System.Windows.Forms.Control _twoDViewHostChild = new System.Windows.Forms.Panel();

        // 图片检测相关
        internal ImageTestManager _imageTestManager = new ImageTestManager();
        private Storyboard _flashStoryboard;
        
        // 图片保存相关
        private int _currentImageNumber = 0;
        
        // 记录最新保存的图像源1文件路径（用于最后一组图片功能）
        private string _lastSavedImageSource1Path = "";
        private readonly string _imageNumberConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ImageNumber.txt");

        // 2D检测完成状态（由算法引擎结果驱动）
        private static volatile bool _is2DDetectionCompleted = false;
        
        // 添加3D检测完成时间戳，用于延迟判断2D异常
        private static DateTime? _3DCompletionTime = null;
        // 🔧 移除锁：private static readonly object _3DCompletionLock = new object();
        
        // 添加异步2D异常检查定时器
        // 已移除：_2DTimeoutCheckTimer（修复死锁问题）
        
        // 统一检测管理器
        private UnifiedDetectionManager _detectionManager;

        // 硬件控制器接口
        private readonly IIoController _ioController;
        private readonly IPlcController _plcController;

        // 算法引擎结果缓存
        private AlgorithmResult _lastAlgorithmResult;
        private AlgorithmResult _lastRenderResult;
        private TemplateParameters _templateOverride;
        private ImageGroupSet _lastExecutedImageGroup;
        private TrayDetectionWindow _trayDetectionWindow;
        private readonly List<RenderSelectionOption> _renderSelectionOptions = new List<RenderSelectionOption>();
        private string _renderMainSelectionKey;
        private string _renderStepSelectionKey;

        public event EventHandler<AlgorithmResultEventArgs> AlgorithmResultProduced;

        /// <summary>
        /// 获取检测管理器实例
        /// </summary>
        public UnifiedDetectionManager DetectionManager => _detectionManager;

        public AlgorithmResult LastAlgorithmResult => _lastAlgorithmResult;

        private sealed class RenderSelectionOption
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
        }

        public void SetTemplateOverride(TemplateParameters template)
        {
            _templateOverride = template;
        }

        public void ClearTemplateOverride()
        {
            _templateOverride = null;
        }

        /// <summary>
        /// 获取当前主界面表格中的检测项目快照（用于导出配置界面）
        /// </summary>
        public List<DetectionItem> GetAllDetectionItemsSnapshot()
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    return CreateAllDetectionItemsSnapshot();
                }

                return Dispatcher.Invoke(CreateAllDetectionItemsSnapshot);
            }
            catch
            {
                return new List<DetectionItem>();
            }
        }

        private List<DetectionItem> CreateAllDetectionItemsSnapshot()
        {
            return _fullDataList
                .Where(i => i != null)
                .Select(i => new DetectionItem
                {
                    RowNumber = i.RowNumber,
                    Name = i.Name,
                    Value = i.Value,
                    LowerLimit = i.LowerLimit,
                    UpperLimit = i.UpperLimit,
                    IsOutOfRange = i.IsOutOfRange,
                    Is3DItem = i.Is3DItem,
                    ToolIndex = i.ToolIndex
                })
                .ToList();
        }

        // 🔧 新增：测试模式数据管理器（完全独立，不影响现有功能）
        private TestModeDataManager _testModeDataManager;
        public bool _isTestModeActive = false;

        // 🔧 新增：验机模式相关变量
        private bool _isValidatorMachineMode = false;
        private int _validatorMachineSampleCount = 0;
        private int _validatorMachineLoopCycle = 0;
        private List<string> _validatorMachineResults = new List<string>();
        private ValidatorMachineResultsWindow _validatorMachineResultsWindow = null;  // 验机结果窗口引用
        private string _validatorMachineLotNumber = string.Empty;  // 验机LOT号

        // ===== 晶片高度计算参数缓存 =====
        /// <summary>
        /// 晶片高度计算所需的3D参数
        /// </summary>
        private class ChipHeightCalcParams3D
        {
            // 图形搜索中心 (PKG中心)
            public double PkgCenterX { get; set; } = double.NaN;
            public double PkgCenterY { get; set; } = double.NaN;
            // 直线起点终点 (用于计算PKG角度)
            public double LineStartX { get; set; } = double.NaN;
            public double LineStartY { get; set; } = double.NaN;
            public double LineEndX { get; set; } = double.NaN;
            public double LineEndY { get; set; } = double.NaN;
            // 晶片平面参数 (A=X斜率, B=Y斜率, C=Z截距)
            public double ChipPlaneA { get; set; } = double.NaN;
            public double ChipPlaneB { get; set; } = double.NaN;
            public double ChipPlaneC { get; set; } = double.NaN;
            // 参考平面(002平面)参数
            public double RefPlaneA { get; set; } = double.NaN;
            public double RefPlaneB { get; set; } = double.NaN;
            public double RefPlaneC { get; set; } = double.NaN;

            // ===== 新策略：晶片边缘与交点（3D） =====
            // [022] 晶片下边缘（GlobalDetectLine）
            public double ChipBottomLineStartX { get; set; } = double.NaN;
            public double ChipBottomLineStartY { get; set; } = double.NaN;
            public double ChipBottomLineEndX { get; set; } = double.NaN;
            public double ChipBottomLineEndY { get; set; } = double.NaN;
            // [023] 晶片左边缘（GlobalDetectLine）
            public double ChipLeftLineStartX { get; set; } = double.NaN;
            public double ChipLeftLineStartY { get; set; } = double.NaN;
            public double ChipLeftLineEndX { get; set; } = double.NaN;
            public double ChipLeftLineEndY { get; set; } = double.NaN;
            // [024] 晶片交点（GlobalDetectPoint）
            public double ChipIntersectionX { get; set; } = double.NaN;
            public double ChipIntersectionY { get; set; } = double.NaN;

            public bool IsValid => !double.IsNaN(PkgCenterX) && !double.IsNaN(PkgCenterY) &&
                                   !double.IsNaN(LineStartX) && !double.IsNaN(LineStartY) &&
                                   !double.IsNaN(LineEndX) && !double.IsNaN(LineEndY) &&
                                   !double.IsNaN(ChipPlaneA) && !double.IsNaN(ChipPlaneB) && !double.IsNaN(ChipPlaneC) &&
                                   !double.IsNaN(RefPlaneC);

            /// <summary>
            /// 是否已收集到晶片边缘+交点（用于无PKG中心的新映射策略）
            /// </summary>
            public bool HasChipEdgeData =>
                !double.IsNaN(ChipBottomLineStartX) && !double.IsNaN(ChipBottomLineStartY) &&
                !double.IsNaN(ChipBottomLineEndX) && !double.IsNaN(ChipBottomLineEndY) &&
                !double.IsNaN(ChipLeftLineStartX) && !double.IsNaN(ChipLeftLineStartY) &&
                !double.IsNaN(ChipLeftLineEndX) && !double.IsNaN(ChipLeftLineEndY) &&
                !double.IsNaN(ChipIntersectionX) && !double.IsNaN(ChipIntersectionY);
        }

        /// <summary>
        /// 晶片高度计算所需的2D参数
        /// </summary>
        private class ChipHeightCalcParams2D
        {
            // PKG中心 (像素)
            public double PkgCenterX { get; set; } = double.NaN;
            public double PkgCenterY { get; set; } = double.NaN;
            // 晶片中心 (像素)
            public double ChipCenterX { get; set; } = double.NaN;
            public double ChipCenterY { get; set; } = double.NaN;
            // BLK-PKG角度 (度)
            public double ChipAngle { get; set; } = double.NaN;
            // BLK长度/宽度 (像素)
            public double ChipLength { get; set; } = double.NaN;
            public double ChipWidth { get; set; } = double.NaN;

            public bool IsValid => !double.IsNaN(PkgCenterX) && !double.IsNaN(PkgCenterY) &&
                                   !double.IsNaN(ChipCenterX) && !double.IsNaN(ChipCenterY) &&
                                   !double.IsNaN(ChipAngle) &&
                                   !double.IsNaN(ChipLength) && !double.IsNaN(ChipWidth);
        }

        private static ChipHeightCalcParams3D _chipHeightParams3D = null;
        private static ChipHeightCalcParams2D _chipHeightParams2D = null;
        // ===== 晶片高度计算参数缓存结束 =====

        // ===== G1/G2 直接提取值缓存 =====
        private static double _extractedG1Value = 0;
        private static double _extractedG2Value = 0;
        private static bool _hasExtractedG1 = false;
        private static bool _hasExtractedG2 = false;
        // ===== G1/G2 直接提取值缓存结束 =====

        // 显示模式相关 - 支持显示所有项或仅显示关注项
        private bool _showFocusedOnly = false;
        private List<DetectionItem> _fullDataList = new List<DetectionItem>();
        private readonly ObservableCollection<DetectionItem> _dataGridItems = new ObservableCollection<DetectionItem>();
        private HashSet<string> _focusedProjects = new HashSet<string>();
        private readonly string _focusedProjectsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "FocusedProjects.json");
        
        // 🎨 自定义3D和2D视图颜色配置
        
        /// <summary>
        /// 3D视图自定义颜色范围配置
        /// </summary>
        public class View3DColorConfig
        {
            public bool UseCustomColorRange { get; set; } = false;  // 是否使用自定义颜色范围
            public double ColorRangeMin { get; set; } = -2.0;       // 自定义最小值（毫米）
            public double ColorRangeMax { get; set; } = 2.0;        // 自定义最大值（毫米）
            public float MeshTransparent { get; set; } = 0.5f;      // 网格透明度 (0-1)
            public float BlendWeight { get; set; } = 0.5f;          // 混合权重 (0-1)
            public bool DisplayColorBar { get; set; } = true;       // 显示颜色条
            public bool DisplayGrid { get; set; } = true;           // 显示网格
            public bool DisplayAxis { get; set; } = true;           // 显示坐标轴
        }

        /// <summary>
        /// 同步DataGrid显示项到指定列表
        /// </summary>
        private void SyncDataGridItems(IList<DetectionItem> sourceItems)
        {
            if (sourceItems == null)
            {
                return;
            }

            while (_dataGridItems.Count > sourceItems.Count)
            {
                _dataGridItems.RemoveAt(_dataGridItems.Count - 1);
            }

            for (int i = 0; i < sourceItems.Count; i++)
            {
                var sourceItem = sourceItems[i];
                if (i < _dataGridItems.Count)
                {
                    var targetItem = _dataGridItems[i];
                    targetItem.RowNumber = sourceItem.RowNumber;
                    targetItem.Name = sourceItem.Name;
                    targetItem.Value = sourceItem.Value;
                    targetItem.LowerLimit = sourceItem.LowerLimit;
                    targetItem.UpperLimit = sourceItem.UpperLimit;
                    targetItem.IsOutOfRange = sourceItem.IsOutOfRange;
                    targetItem.Is3DItem = sourceItem.Is3DItem;
                    targetItem.ToolIndex = sourceItem.ToolIndex;
                }
                else
                {
                    _dataGridItems.Add(sourceItem);
                }
            }
        }

        /// <summary>
        /// 2D视图自定义颜色范围配置
        /// </summary>
        public class View2DColorConfig
        {
            public bool UseCustomColorRange { get; set; } = false;  // 是否使用自定义颜色范围
            public double ColorRangeMin { get; set; } = -2.0;       // 自定义最小值（毫米）
            public double ColorRangeMax { get; set; } = 2.0;        // 自定义最大值（毫米）
        }
        
        // 颜色配置实例
        public View3DColorConfig _3DColorConfig = new View3DColorConfig();
        public View2DColorConfig _2DColorConfig = new View2DColorConfig();

        /// <summary>
        /// 从颜色配置窗口应用设置到3D/2D配置对象。
        /// 3D已解耦：主进程不再直接操作Keyence视图控件，仅保存配置并记录日志。
        /// </summary>
        public void ApplyColorConfigFromWindow(
            bool useCustomColorRange,
            double colorRangeMin,
            double colorRangeMax,
            double meshTransparent,
            double blendWeight,
            bool displayColorBar,
            bool displayGrid,
            bool displayAxis)
        {
            try
            {
                _3DColorConfig.UseCustomColorRange = useCustomColorRange;
                _3DColorConfig.ColorRangeMin = colorRangeMin;
                _3DColorConfig.ColorRangeMax = colorRangeMax;
                _3DColorConfig.MeshTransparent = (float)meshTransparent;
                _3DColorConfig.BlendWeight = (float)blendWeight;
                _3DColorConfig.DisplayColorBar = displayColorBar;
                _3DColorConfig.DisplayGrid = displayGrid;
                _3DColorConfig.DisplayAxis = displayAxis;

                _2DColorConfig.UseCustomColorRange = useCustomColorRange;
                _2DColorConfig.ColorRangeMin = colorRangeMin;
                _2DColorConfig.ColorRangeMax = colorRangeMax;

                LogUpdate($"[颜色配置] 已更新配置对象（3D已解耦，不直接应用视图）：自定义={useCustomColorRange}, 范围=[{colorRangeMin:F3}, {colorRangeMax:F3}]");
            }
            catch (Exception ex)
            {
                LogUpdate("应用颜色配置失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 确保DataGrid1设置了红色显示事件处理（NG项目红底、空值黄底）。
        /// </summary>
        private void EnsureDataGridRedDisplaySetup()
        {
            try
            {
                DataGrid1.LoadingRow -= DataGrid1_LoadingRow;
                DataGrid1.LoadingRow += DataGrid1_LoadingRow;
            }
            catch (Exception ex)
            {
                LogUpdate("设置DataGrid红色显示事件时出错: " + ex.Message);
            }
        }

        private void DataGrid1_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                if (e.Row.Item is DetectionItem item)
                {
                    bool isEmpty = string.IsNullOrWhiteSpace(item.Value);
                    if (isEmpty)
                    {
                        e.Row.Background = new SolidColorBrush(System.Windows.Media.Colors.LightYellow);
                    }
                    else if (item.IsOutOfRange)
                    {
                        e.Row.Background = new SolidColorBrush(System.Windows.Media.Colors.LightCoral);
                    }
                    else
                    {
                        e.Row.Background = new SolidColorBrush(System.Windows.Media.Colors.White);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUpdate("设置DataGrid行背景色时出错: " + ex.Message);
            }
        }

        /// <summary>
        /// 统一更新DataGrid：同时应用2D和3D缓存数据，一次性更新避免分两次刷新。
        /// </summary>
        public void UnifiedUpdateDataGrid()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var newItems = new List<DetectionItem>();
                        int rowNumber = 1;

                        if (_cached2DItems != null && _cached2DItems.Count > 0)
                        {
                            foreach (var item in _cached2DItems)
                            {
                                if (_hidden2DItemNames.Contains(item.Name?.Trim() ?? string.Empty))
                                {
                                    continue;
                                }

                                newItems.Add(new DetectionItem
                                {
                                    RowNumber = rowNumber++,
                                    Name = item.Name,
                                    Value = item.Value,
                                    LowerLimit = item.LowerLimit,
                                    UpperLimit = item.UpperLimit,
                                    IsOutOfRange = item.IsOutOfRange,
                                    Is3DItem = false,
                                    ToolIndex = item.ToolIndex
                                });
                            }
                        }

                        if (_cached3DItems != null && _cached3DItems.Count > 0)
                        {
                            foreach (var item in _cached3DItems)
                            {
                                newItems.Add(new DetectionItem
                                {
                                    RowNumber = rowNumber++,
                                    Name = item.Name,
                                    Value = item.Value,
                                    LowerLimit = item.LowerLimit,
                                    UpperLimit = item.UpperLimit,
                                    IsOutOfRange = item.IsOutOfRange,
                                    Is3DItem = true,
                                    ToolIndex = item.ToolIndex
                                });
                            }
                        }

                        _fullDataList.Clear();
                        _fullDataList.AddRange(newItems);

                        IList<DetectionItem> itemsToDisplay;
                        if (_showFocusedOnly)
                        {
                            itemsToDisplay = newItems.Where(item => _focusedProjects.Contains(item.Name)).ToList();
                            for (int i = 0; i < itemsToDisplay.Count; i++)
                            {
                                itemsToDisplay[i].RowNumber = i + 1;
                            }
                        }
                        else
                        {
                            itemsToDisplay = newItems;
                        }

                        SyncDataGridItems(itemsToDisplay);
                        ApplyRowColorFormatting();
                        EnsureDataGridRedDisplaySetup();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error("[UnifiedUpdate] 刷新DataGrid界面时出错: " + ex.Message, "Page1");
                    }
                }), DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                LogManager.Error("[UnifiedUpdate] 统一更新DataGrid失败: " + ex.Message, "Page1");
            }
        }

        private void RecordOutOfRangeItems(List<DetectionItem> items, string defectType)
        {
            try
            {
                if (items == null) return;

                string imageNumber = GetCurrentImageNumberForRecord();
                var outOfRangeItems = items.Where(item => item.IsOutOfRange).ToList();
                if (outOfRangeItems.Count == 0) return;

                var record = new OutOfRangeRecord
                {
                    ImageNumber = imageNumber,
                    DefectType = defectType,
                    DetectionTime = DateTime.Now,
                    OutOfRangeItems = outOfRangeItems.Select(item => new OutOfRangeItem
                    {
                        ItemName = item.Name,
                        Value = item.Value,
                        LowerLimit = item.LowerLimit,
                        UpperLimit = item.UpperLimit,
                        IsOutOfRange = item.IsOutOfRange
                    }).ToList()
                };

                SaveOutOfRangeRecord(record);
            }
            catch (Exception ex)
            {
                LogManager.Error("记录超限项目失败: " + ex.Message);
            }
        }

        private void SaveOutOfRangeRecord(OutOfRangeRecord record)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string lotDir = Path.Combine(baseDir, "原图存储", CurrentLOTNumber);

                if (!Directory.Exists(lotDir))
                {
                    Directory.CreateDirectory(lotDir);
                }

                string fileName = $"超限记录_{CurrentLOTNumber}.json";
                string filePath = Path.Combine(lotDir, fileName);

                List<OutOfRangeRecord> allRecords = new List<OutOfRangeRecord>();
                if (File.Exists(filePath))
                {
                    try
                    {
                        string existingContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(existingContent))
                        {
                            allRecords = Newtonsoft.Json.JsonConvert.DeserializeObject<List<OutOfRangeRecord>>(existingContent)
                                         ?? new List<OutOfRangeRecord>();
                        }
                    }
                    catch (Exception readEx)
                    {
                        LogManager.Warning("读取现有超限记录文件失败，将创建新文件: " + readEx.Message);
                        allRecords = new List<OutOfRangeRecord>();
                    }
                }

                allRecords.Add(record);
                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(allRecords, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, jsonContent, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogManager.Error("保存超限记录失败: " + ex.Message);
            }
        }
        
        /// <summary>
        /// 标记2D检测已完成（由算法引擎结果回调调用）
        /// </summary>
        public static void Set2DDetectionCompleted()
        {
            _is2DDetectionCompleted = true;
        }
        
        /// <summary>
        /// 重置2D检测完成标志（在开始新的检测周期时调用）
        /// </summary>
        public static void Reset2DDetectionFlag()
        {
            _is2DDetectionCompleted = false;
            // 🔧 移除锁：直接操作
            _3DCompletionTime = null;
            
            // 已移除：定时器清理代码（修复死锁问题）
            
            // 清空3D数据缓存，为新的检测周期做准备
            // 🔧 移除锁：直接操作
            _cached3DItems = null;
        }
        
        /// <summary>
        /// 检查2D检测是否已完成
        /// </summary>
        public static bool Is2DDetectionCompleted()
        {
            return _is2DDetectionCompleted;
        }
        
        /// <summary>
        /// 设置3D检测完成时间戳（由3D检测回调调用）
        /// </summary>
        public static void Set3DCompletionTime()
        {
            // 🔧 移除锁：直接操作
            _3DCompletionTime = DateTime.Now;
            
            // 已移除：StartAsync2DTimeoutCheck()（修复死锁问题）
        }
        
        // 已移除：StartAsync2DTimeoutCheck() 和 CheckFor2DTimeout() 方法
        // 原因：这些方法导致UI线程死锁，在空闲状态下错误触发定时器
        
        /// <summary>
        /// 检查3D检测是否已完成以及完成后经过的时间
        /// </summary>
        /// <returns>(is3DCompleted, elapsedMilliseconds)</returns>
        public static (bool is3DCompleted, double elapsedMs) Get3DCompletionStatus()
        {
            // 🔧 移除锁：直接操作
            if (_3DCompletionTime.HasValue)
            {
                double elapsedMs = (DateTime.Now - _3DCompletionTime.Value).TotalMilliseconds;
                return (true, elapsedMs);
            }
            return (false, 0);
        }

        public Page1()
        {
            InitializeComponent();
            Loaded += Page1_Loaded;
            Unloaded += Page1_Unloaded;
            IsVisibleChanged += Page1_IsVisibleChanged;
            DataGrid1.ItemsSource = _dataGridItems;

            // 初始化日志管理器（从配置文件加载设置）
            LogManager.LoadConfigFromFile();
            LogManager.Info($"{SystemBrandingManager.GetSystemName()}启动", "System");

            // 初始化3D视图
            _3DViewHost.Child = _threeDViewHostChild;

            // 初始化2D视图
            // Laser2DViewHost.Child = _twoDViewHostChild;

            // 加载保存的LOT值
            CurrentLotValue = LotSettingWindow.LoadLotValueFromFile();

            WpfPlot1.Plot.Axes.Frameless();
            WpfPlot1.Plot.HideGrid();

            //WpfPlot1.Plot.Legend.FontSize = 20;

            // 设置饼图轴限制，使饼图居左显示
            WpfPlot1.Plot.Axes.SetLimitsX(0.5, 3);
            WpfPlot1.Plot.Axes.SetLimitsY(-1.5, 1.5);

            WpfPlot1.Refresh();
            PageManager.Page1Instance = this; // 保存实例

            // 初始化硬件控制器接口
            _ioController = HardwareControllerFactory.CreateIoController(useRealHardware: true);
            _plcController = HardwareControllerFactory.CreatePlcController(useRealHardware: true);

            // 🔧 关键修复：初始化统一检测管理器，传递this实例
            _detectionManager = new UnifiedDetectionManager(this);

            // 初始化算法引擎注册表
            AlgorithmEngineRegistry.Initialize();
            
            // 🔧 新增：初始化显示模式
            InitializeDisplayMode();
                                              //Lj3DView ljd3dView = new Lj3DView();
                                              //MainGrid.Children.Add(ljd3dView);


            // 初始化图片检测卡片
            InitializeImageTestCard();
            
            // 确保DataGrid1设置了红色显示事件处理
            EnsureDataGridRedDisplaySetup();
            
            // **新增：检测开机启动设置（延迟执行）**
            InitializeAutoStartupCheck();
            
            // 🔧 新增：初始化时更新3D图像管理器的UI状态缓存
            Task.Run(() =>
            {
                Thread.Sleep(1000); // 延迟1秒确保UI完全加载
            });

            // 🔧 新增：初始化远程文件监控服务（如果已配置启用）
            Task.Run(() =>
            {
                Thread.Sleep(2000); // 延迟2秒确保其他组件初始化完成
                Dispatcher.Invoke(() => InitializeRemoteFileMonitor());
            });
        }

        private void Page1_Loaded(object sender, RoutedEventArgs e)
        {
            SmartAnalysisWindowManager.HandlePageVisibilityChange(true);
        }

        private void Page1_Unloaded(object sender, RoutedEventArgs e)
        {
            SmartAnalysisWindowManager.HandlePageVisibilityChange(false);
        }

        private void Page1_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isVisible && IsLoaded)
            {
                SmartAnalysisWindowManager.HandlePageVisibilityChange(isVisible);
            }
        }



        // 添加一个用于绑定的公共属性
        private string _currentTemplateName = "MESA-25";
        public string CurrentTemplateName
        {
            get { return _currentTemplateName; }
            set
            {
                _currentTemplateName = value;
                // 在UI线程上更新TextBlock
                Dispatcher.BeginInvoke(new Action(() => {
                    TemplateNameText.Text = value;
                }));
            }
        }

        private string _currentLotValue = "17USK-87";
        public string CurrentLotValue
        {
            get { return _currentLotValue; }
            set
            {
                _currentLotValue = value;
                // 在UI线程上更新TextBlock
                Dispatcher.BeginInvoke(new Action(() => {
                    LOT.Text = value;
                }));
            }
        }

        private void FlowButton_Click(object sender, RoutedEventArgs e)
        {
            //var mainWindow = (MainWindow)Application.Current.MainWindow;
            var mainWindow = Application.Current.Windows
    .OfType<MainWindow>()
    .FirstOrDefault();
            
            // 🔧 新增：切换页面时恢复检测处理
            _detectionManager?.SetSystemState(SystemDetectionState.WaitingForTrigger);
            LogManager.Info("[页面切换] 切换到算法流程界面，已恢复检测处理");
            
            mainWindow.ContentC.Content = mainWindow.frame2; // 切换到算法流程界面（Page2）
        }



        public static class PageManager
        {
            public static Page1 Page1Instance { get; set; }

            /// <summary>
            /// 🔧 公共方法：重置检测管理器状态并恢复检测处理
            /// 用于从其他页面返回时调用，确保检测状态正确
            /// </summary>
            /// <param name="source">调用来源（用于日志记录）</param>
            public static void ResetDetectionManagerOnPageReturn(string source = "未知页面")
            {
                try
                {
                    var page1Instance = Page1Instance;
                    if (page1Instance != null)
                    {
                        Page1Instance.DetectionManager.Reset();
                        // 🔧 关键修复：重新同步3D使能状态，确保状态一致性
                        bool shouldEnable3D = page1Instance.Is3DDetectionEnabled();
                        page1Instance._detectionManager?.StartDetectionCycle(shouldEnable3D);
                        
                        page1Instance._detectionManager?.SetSystemState(SystemDetectionState.WaitingForTrigger);
                        LogManager.Info($"[{source}] 页面返回时已重置检测状态并恢复检测处理，3D启用: {shouldEnable3D}");
                    }
                    else
                    {
                        LogManager.Warning($"[{source}] Page1实例不存在，无法重置检测管理器");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error($"[{source}] 重置检测管理器失败: {ex.Message}");
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //显示fram2
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.ContentC.Content = mainWindow.frame2; // 设置内容为 Page2
            
            // 🔧 新增：切换页面时恢复检测处理
            _detectionManager?.SetSystemState(SystemDetectionState.WaitingForTrigger);
            LogManager.Info("[页面切换] 切换到算法流程页面，已恢复检测处理");
        }

        /// <summary>
        /// 消息显示
        /// </summary>
        /// <param name="str"></param>
        public void LogUpdate(string str)
        {
            string timeStamp = DateTime.Now.ToString("yy-MM-dd HH:mm:ss.fff");
            
            // 🔧 修复跨线程访问问题：将所有UI操作移到UI线程中执行
            listViewLog.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    //如果记录超过1000条，应当清空再添加记录，以防记录的条目巨大引起界面卡顿和闪烁
                    if (listViewLog.Items.Count > 1000)
                        listViewLog.Items.Clear();

                    listViewLog.Items.Insert(0, new LogEntry { TimeStamp = timeStamp, Message = str });
                }
                catch (Exception ex)
                {
                    // 在UI线程中处理UI异常，避免影响后台线程
                    System.Diagnostics.Debug.WriteLine($"LogUpdate UI操作异常: {ex.Message}");
                }
            }));

            SaveLog(str);
        }

        /// <summary>
        /// 保存日志
        /// </summary>
        /// <param name="str"></param>
        private void SaveLog(string str)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(logPath))//如果日志目录不存在就创建
                    {
                        Directory.CreateDirectory(logPath);
                    }
                    string filename = logPath + "/" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";//用日期对日志文件命名
                    StreamWriter mySw = File.AppendText(filename);
                    mySw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss::ffff\t") + str);
                    mySw.Close();
                }
                catch
                {
                    return;
                }
            });
        }

        /// <summary>
        /// 清空消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void logClear_Click(object sender, EventArgs e)
        {
            ClearLog();
        }

        /// <summary>
        /// 清空日志的公共方法
        /// </summary>
        public void ClearLog()
        {
            listViewLog.Items.Clear();
        }

        /// <summary>
        /// 获取日志项的公共方法
        /// </summary>
        /// <returns>日志项列表</returns>
        public System.Collections.IList GetLogItems()
        {
            return listViewLog.Items;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //打开文件选择窗口，让用户选择查看不同类型的文件
            try
            {
                var fileSelectionWindow = new FileSelectionWindow(CurrentLotValue);
                fileSelectionWindow.Owner = Application.Current.MainWindow;
                fileSelectionWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogManager.Error($"打开文件选择窗口失败: {ex.Message}");
                MessageBox.Show($"打开文件选择窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                
                // 添加空引用检查
                if (mainWindow?.frame_ConfigPage == null)
                {
                    LogUpdate("系统尚未完全初始化，请稍后重试");
                    MessageBox.Show("系统尚未完全初始化，请稍等片刻后重试", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 🔧 关键修复：切换页面时重置检测管理器状态并恢复检测处理
                _detectionManager?.Reset();
                _detectionManager?.SetSystemState(SystemDetectionState.TemplateConfiguring);
                LogManager.Info("[页面切换] 切换到参数配置页面，已切换为模板配置模式");
                
                mainWindow.ContentC.Content = mainWindow.frame_ConfigPage; // 打开参数配置页面
                LogUpdate("已进入参数配置页面");
            }
            catch (Exception ex)
            {
                LogUpdate($"打开参数配置页面失败: {ex.Message}");
                MessageBox.Show($"打开参数配置页面失败: {ex.Message}\n\n如果系统刚启动，请稍等片刻后重试", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 相机配置按钮点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                
                // 添加空引用检查
                if (mainWindow?.frame_CameraConfigPage == null)
                {
                    LogUpdate("系统尚未完全初始化，请稍后重试");
                    MessageBox.Show("系统尚未完全初始化，请稍等片刻后重试", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 直接进入相机配置界面，不触发配置模式
                // 配置模式将由用户在界面内点击"定拍15度光测试"按钮时手动激活
                mainWindow.ContentC.Content = mainWindow.frame_CameraConfigPage; 
                LogUpdate("已进入相机配置页面");
                LogManager.Info("[相机配置] 已进入相机配置界面，配置模式需手动激活");
            }
            catch (Exception ex)
            {
                LogUpdate($"打开相机配置页面失败: {ex.Message}");
                MessageBox.Show($"打开相机配置页面失败: {ex.Message}\n\n如果系统刚启动，请稍等片刻后重试", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新当前检测结果的显示效果（应用新的颜色配置）
        /// </summary>
        public void RefreshCurrentDetectionDisplay()
        {
            LogManager.Info("刷新检测结果显示：3D已解耦，主进程不再直接刷新Keyence视图（将于Host/Tool内处理）");
        }

        /// <summary>
        /// 数据分析按钮点击事件处理器
        /// </summary>
        public void DataAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogManager.Info("打开质量分析仪表板");
                SmartAnalysisWindowManager.ShowAnalysisWindow(this);
            }
            catch (Exception ex)
            {
                LogManager.Error($"打开质量分析仪表板失败: {ex.Message}");
                MessageBox.Show($"打开质量分析仪表板失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取分析数据（从数据队列或缓存的检测结果）
        /// </summary>
        public List<(string ItemName, List<double> Values, double LowerLimit, double UpperLimit)> GetAnalysisDataFromDataQueue()
        {
            var analysisData = new List<(string, List<double>, double, double)>();
            
            try
            {
                analysisData = GetDataFromCachedResults();
                
                LogManager.Info($"获取到 {analysisData.Count} 个分析项目");
                return analysisData;
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取分析数据失败: {ex.Message}");
                return new List<(string, List<double>, double, double)>();
            }
        }

        /// <summary>
        /// 从缓存的检测结果获取数据
        /// </summary>
        private List<(string ItemName, List<double> Values, double LowerLimit, double UpperLimit)> GetDataFromCachedResults()
        {
            var analysisData = new List<(string, List<double>, double, double)>();
            
            try
            {
                // 从2D和3D缓存数据中获取
                var all2DItems = GetCached2DItems() ?? new List<DetectionItem>();
                var all3DItems = _cached3DItems ?? new List<DetectionItem>();
                
                var allItems = all2DItems.Concat(all3DItems).ToList();
                
                if (allItems.Any())
                {
                    var groupedData = allItems.GroupBy(item => item.Name).ToList();
                    
                    foreach (var group in groupedData)
                    {
                        var values = new List<double>();
                        double lowerLimit = 0, upperLimit = 0;
                        
                        foreach (var item in group)
                        {
                            if (double.TryParse(item.Value, out double value))
                            {
                                values.Add(value);
                            }
                            
                            if (double.TryParse(item.LowerLimit, out double lower))
                                lowerLimit = lower;
                            if (double.TryParse(item.UpperLimit, out double upper))
                                upperLimit = upper;
                        }
                        
                        if (values.Any())
                        {
                            analysisData.Add((group.Key, values, lowerLimit, upperLimit));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"从缓存结果获取数据失败: {ex.Message}");
            }
            
            return analysisData;
        }

        // 添加更新模板名称的方法
        public void UpdateTemplateName(string templateName)
        {
            CurrentTemplateName = templateName;
            
            // 同时加载和应用该模板的颜色配置
            LoadAndApplyColorConfigFromCurrentTemplate();
        }

        /// <summary>
        /// 从当前模板加载并应用颜色配置
        /// </summary>
        public void LoadAndApplyColorConfigFromCurrentTemplate()
        {
            try
            {
                string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                string templatePath = Path.Combine(templatesDir, $"{CurrentTemplateName}.json");
                
                if (File.Exists(templatePath))
                {
                    var template = WpfApp2.Models.TemplateParameters.LoadFromFile(templatePath);
                    if (template?.ColorParams != null)
                    {
                        // 应用颜色配置到3D/2D视图
                        ApplyColorConfigFromWindow(
                            template.ColorParams.UseCustomColorRange,
                            template.ColorParams.ColorRangeMin,
                            template.ColorParams.ColorRangeMax,
                            template.ColorParams.MeshTransparent,
                            template.ColorParams.BlendWeight,
                            template.ColorParams.DisplayColorBar,
                            template.ColorParams.DisplayGrid,
                            template.ColorParams.DisplayAxis
                        );
                        
                        LogUpdate($"已从模板 '{CurrentTemplateName}' 自动应用颜色配置");
                    }
                    else
                    {
                        LogUpdate($"模板 '{CurrentTemplateName}' 中无颜色配置");
                    }
                }
                else
                {
                    LogUpdate($"模板文件不存在: {templatePath}");
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"从模板加载颜色配置失败: {ex.Message}");
            }
        }

        private void LOT_MouseDown(object sender, RoutedEventArgs e)
        {
            // 创建并显示LOT设置窗口
            var lotSettingWindow = new LotSettingWindow(CurrentLotValue);
            if (lotSettingWindow.ShowDialog() == true)
            {
                // 获取新的LOT值
                string newLotValue = lotSettingWindow.LotValue;
                string oldLotValue = CurrentLotValue;
                
                // LogUpdate($"LOT变更检查：旧值='{oldLotValue}', 新值='{newLotValue}'"); // 客户日志：技术细节不显示
                
                // 如果LOT值发生了变化，进行完整的更新流程
                if (oldLotValue != newLotValue)
                {
                    // 更新LOT值
                    CurrentLotValue = newLotValue;
                    LogUpdate($"LOT已更新：{oldLotValue} → {newLotValue}");
                    
                    // 通知实时数据记录器LOT号变更
                    try
                    {
                        WpfApp2.UI.Models.RealTimeDataLogger.Instance.SetLotNumber(newLotValue);
                        LogManager.Info($"实时数据记录器已更新LOT号：{newLotValue}");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"更新实时数据记录器LOT号失败: {ex.Message}");
                    }
                    
                    // 重置图号并更新所有相关算法变量
                    ResetImageNumberForNewLot();
                }
                else
                {
                    // LogUpdate("LOT值未发生变化，无需重置图号"); // 客户日志：技术细节不显示
                }
            }
            else
            {
                LogUpdate("用户取消了LOT设置");
            }
        }

        // 添加更新LOT值的公共方法
        public void UpdateLotValue(string lotValue)
        {
            CurrentLotValue = lotValue;
        }

        /// <summary>
        /// 更新NG类型显示
        /// </summary>
        /// <param name="defectType">NG类型字符串</param>
        public void UpdateDefectType(string defectType)
        {
            try
            {
                // 在UI线程上更新NG类型显示
                Dispatcher.BeginInvoke(new Action(() => {
                    if (string.IsNullOrWhiteSpace(defectType))
                    {
                        DefectType.Text = "--";
                    }
                    else
                    {
                        DefectType.Text = defectType;
                        LogManager.Verbose($"检测到NG类型: {defectType}", "Page1"); // 详细日志，生产模式下不显示
                    }
                }));
            }
            catch (Exception ex)
            {
                LogUpdate($"更新NG类型显示失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 数据清空按钮点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 添加弹窗警报，防止误操作
                var result = MessageBox.Show(
                    "确定要清空所有检测数据吗？\n\n此操作将清空：\n• 主界面统计数据和饼图\n• 数据分析页面的所有图表和缓存\n• 质量分析仪表板的图表与缓存\n• 界面显示日志\n\n此操作不可恢复！",
                    "数据清空确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return; // 用户取消操作
                }

                bool statisticsCleared = false;
                bool qualityDashboardCleared = false;

                // 通过静态实例引用直接访问TemplateConfigPage
                if (TemplateConfigPage.Instance != null)
                {
                    TemplateConfigPage.Instance.ClearStatistics();
                    statisticsCleared = true;
                    LogUpdate("通过静态实例成功清空统计变量");
                }
                else
                {
                    LogUpdate("TemplateConfigPage.Instance为null，尝试其他方法");

                    // 备用方法：通过MainWindow访问TemplateConfigPage
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    if (mainWindow?.frame2?.Content is TemplateConfigPage templateConfigPage)
                    {
                        templateConfigPage.ClearStatistics();
                        statisticsCleared = true;
                        LogUpdate("通过MainWindow成功清空统计变量");
                    }
                    else
                    {
                        LogUpdate("警告：无法访问到TemplateConfigPage实例，统计数据可能未完全清空");
                    }
                }

                // 清空质量分析仪表板的数据与缓存
                qualityDashboardCleared = SmartAnalysisWindowManager.ClearAnalysisData();
                if (qualityDashboardCleared)
                {
                    LogUpdate("质量分析仪表板数据和缓存已清空");
                }
                else
                {
                    LogUpdate("警告：质量分析仪表板数据未完全清空，请检查日志");
                }

                // 强制清空界面显示数据（确保与内部数据同步）
                ClearUIDisplayData();

                // 注意：新版本使用SmartAnalysisWindowManager，不需要手动清理旧版DataAnalysisPage

                // 清空日志
                listViewLog.Items.Clear();

                // 🔧 新增：清空保存的生产统计数据文件
                if (statisticsCleared)
                {
                    try
                    {
                        ProductionStatsPersistence.ClearSavedStats();
                        LogUpdate("已同时清空保存的生产统计数据文件");
                    }
                    catch (Exception ex)
                    {
                        LogUpdate($"清空保存的生产统计数据文件时出错: {ex.Message}");
                    }
                }

                string summaryMessage = statisticsCleared
                    ? "统计数据已完全清空（包括内部计数器、饼图、数据分析页面和保存文件）"
                    : "界面数据已清空，但内部计数器可能未清空";

                summaryMessage += qualityDashboardCleared
                    ? "，质量分析仪表板缓存已重置"
                    : "，质量分析仪表板缓存可能未完全清空";

                LogUpdate(summaryMessage);
            }
            catch (Exception ex)
            {
                LogUpdate($"清空数据时出错: {ex.Message}");
                MessageBox.Show($"清空数据时出错: {ex.Message}", "清空错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// 清空UI显示数据（独立方法，可被其他地方调用）
        /// </summary>
        public void ClearUIDisplayData()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 清空统计数据显示
                    Total_num.Text = "0";
                    OK_num.Text = "0";
                    NG_num.Text = "0";
                    yieldRate.Text = "100.00%";
                    OK_OR_NG.Text = "OK";
                    OK_OR_NG.Background = Brushes.Green;
                    DefectType.Text = "--"; // 清空NG类型显示

                    // 清空饼图数据
                    WpfPlot1.Plot.Clear();
                    WpfPlot1.Refresh();
                });
            }
            catch (Exception ex)
            {
                LogUpdate($"清空UI显示数据时出错: {ex.Message}");
            }
        }



        #if false // Legacy in-proc Keyence 3D (removed from main process; kept for reference)


        /// <summary>
        /// 更新3D检测结果到Page1的3D视图
        /// </summary>
        /// <param name="result">3D检测结果</param>
        /// <param name="measureEx">3D检测系统实例</param>
        public void Update3DDetectionResult(LjdExecuteResult result, LjdMeasureEx measureEx = null)
        {
            try
            {
                if (result == null) return;

                // **修复竞态条件：确保3D数据缓存同步更新完成**
                if (measureEx != null)
                {
                    // 先同步更新3D检测数据缓存（不依赖UI线程）
                    Update3DDetectionDataFromOutputTargets(result, measureEx);
                }

                // 在UI线程中更新3D视图显示（异步执行，不影响缓存更新）
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 更新2D和3D视图显示
                        if (result.DstHeightImages.Length > 1)
                        {
                            //_View2D.SetImage(result.DstHeightImages, result.DstGrayImages);
                            _View3D.SetImageEx(result.DstHeightImages, result.DstGrayImages);
                        }
                        else
                        {
                            //_View2D.SetImage(result.DstHeightImage, result.DstGrayImage);
                            _View3D.SetImageEx(result.DstHeightImage, result.DstGrayImage);
                        }

                        // 🔧 **关键修复**: 每次更新2D视图后都调用自适应和设置工具信息
                        //_View2D.ColorRangeFitCommand();
                        //if (result.Results != null && result.Results.Count > 0)
                        //{
                        //    _View2D.SetToolInfo(result.Results);
                        //}

                        // 只设置工具信息显示，不重新应用颜色配置（避免重置视图状态）
                        if (_View3D?.LJView3D != null && result.Results != null && result.Results.Count > 0)
                        {
                            _View3D.LJView3D.SetToolInfo(result.Results);
                        }

                    }
                    catch (Exception ex)
                    {
                        LogUpdate($"更新3D视图时出错: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                LogUpdate($"处理3D检测结果时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 🎨 应用自定义3D视图设置（替代自适应颜色）
        /// </summary>
        /// <param name="result">3D检测结果</param>
        private void ApplyCustom3DViewSettings(LjdExecuteResult result)
        {
            try
            {
                if (_View3D?.LJView3D == null) return;
                
                var lj3DView = _View3D.LJView3D;
                
                if (_3DColorConfig.UseCustomColorRange)
                {
                    // 🎨 自定义颜色范围设置（基于官方示例）
                    // 参考：LJD_SampleApplication/Lj3DViewControl.cs 第238-264行
                    var customColorRange = LColorRange.Create(
                        lj3DView.ColorRange.UpperLimit / 32768,  // 保持原有的上限比例
                        _3DColorConfig.ColorRangeMin,            // 自定义最小值
                        _3DColorConfig.ColorRangeMax             // 自定义最大值
                    );
                    lj3DView.ColorRange = customColorRange;
                    LogUpdate($"[3D颜色] 应用自定义颜色范围: [{_3DColorConfig.ColorRangeMin:F2}, {_3DColorConfig.ColorRangeMax:F2}]");
                }
                else
                {
                    // 使用自适应颜色范围
                    if (result.DstHeightImages.Length > 1)
                    {
                        lj3DView.ColorRange = Lj3DView.GetFitRange(result.DstHeightImages);
                    }
                    else
                    {
                        lj3DView.ColorRange = Lj3DView.GetFitRange(new LHeightImage[] { result.DstHeightImage });
                    }
                    LogUpdate($"[3D颜色] 使用自适应颜色范围: [{lj3DView.ColorRange.Low:F2}, {lj3DView.ColorRange.High:F2}]");
                }
                
                // 应用其他3D视觉效果设置
                // 🔧 修复：不要修改GridPosition，这会导致3D视图位置重置
                // lj3DView.GridPosition = (int)Math.Floor(lj3DView.ColorRange.Low);
                lj3DView.MeshTransparent = _3DColorConfig.MeshTransparent;
                lj3DView.BlendWeight = _3DColorConfig.BlendWeight;
                lj3DView.DisplayColorBar = _3DColorConfig.DisplayColorBar;
                lj3DView.DisplayGrid = _3DColorConfig.DisplayGrid;
                lj3DView.DisplayAxis = _3DColorConfig.DisplayAxis;
                
                // 设置工具信息显示
                if (result.Results != null && result.Results.Count > 0)
                {
                    lj3DView.SetToolInfo(result.Results);
                    LogUpdate($"[3D工具] 已设置{result.Results.Count}个工具信息显示");
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"应用3D视图设置时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 🎨 应用自定义2D视图设置
        /// </summary>
        /// <param name="result">检测结果</param>
        private void ApplyCustom2DViewSettings(LjdExecuteResult result)
        {
            try
            {
                if (_View2D == null) return;
                
                if (_2DColorConfig.UseCustomColorRange)
                {
                    // 🎨 2D视图自定义颜色设置 - 目前2D视图ColorRange属性可能不支持，先记录配置
                    LogManager.Info($"[2D颜色] 用户设置自定义2D颜色范围: [{_2DColorConfig.ColorRangeMin:F3}, {_2DColorConfig.ColorRangeMax:F3}]");
                    
                    // 暂时使用自适应颜色，等待基恩士提供2D颜色范围设置API
                    _View2D.ColorRangeFitCommand();
                    LogManager.Info("[2D颜色] 暂时使用自适应颜色范围，等待2D ColorRange API支持");
                }
                else
                {
                    // 使用2D视图的自适应颜色范围
                    _View2D.ColorRangeFitCommand();
                    LogManager.Info("[2D颜色] 使用2D视图自适应颜色范围");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用2D视图设置时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 🎨 提供用户界面配置3D颜色设置的方法
        /// </summary>
        public void Configure3DViewSettings(double minValue = -2.0, double maxValue = 2.0, 
                                          float transparency = 0.5f, bool useCustom = true)
        {
            _3DColorConfig.UseCustomColorRange = useCustom;
            _3DColorConfig.ColorRangeMin = minValue;
            _3DColorConfig.ColorRangeMax = maxValue;
            _3DColorConfig.MeshTransparent = Math.Max(0f, Math.Min(1f, transparency));
            
            LogUpdate($"[3D配置] 颜色范围设置已更新: 自定义={useCustom}, 范围=[{minValue:F2}, {maxValue:F2}], 透明度={transparency:F2}");
        }
        
        /// <summary>
        /// 🎨 提供用户界面配置2D颜色设置的方法
        /// </summary>
        public void Configure2DViewSettings(double minValue = -2.0, double maxValue = 2.0, bool useCustom = true)
        {
            _2DColorConfig.UseCustomColorRange = useCustom;
            _2DColorConfig.ColorRangeMin = minValue;
            _2DColorConfig.ColorRangeMax = maxValue;
            
            LogUpdate($"[2D配置] 颜色范围设置已更新: 自定义={useCustom}, 范围=[{minValue:F2}, {maxValue:F2}]");
        }

        /// <summary>
        /// 🎨 从颜色配置窗口应用设置到3D/2D视图
        /// </summary>
        public void ApplyColorConfigFromWindow(bool useCustomColorRange, double colorRangeMin, double colorRangeMax, 
            double meshTransparent, double blendWeight, bool displayColorBar, bool displayGrid, bool displayAxis)
        {
            try
            {
                // 更新配置对象
                _3DColorConfig.UseCustomColorRange = useCustomColorRange;
                _3DColorConfig.ColorRangeMin = colorRangeMin;
                _3DColorConfig.ColorRangeMax = colorRangeMax;
                _3DColorConfig.MeshTransparent = (float)meshTransparent;
                _3DColorConfig.BlendWeight = (float)blendWeight;
                _3DColorConfig.DisplayColorBar = displayColorBar;
                _3DColorConfig.DisplayGrid = displayGrid;
                _3DColorConfig.DisplayAxis = displayAxis;

                _2DColorConfig.UseCustomColorRange = useCustomColorRange;
                _2DColorConfig.ColorRangeMin = colorRangeMin;
                _2DColorConfig.ColorRangeMax = colorRangeMax;

                // 应用到3D视图
                if (_View3D?.LJView3D != null)
                {
                    var lj3DView = _View3D.LJView3D;
                    
                    // 只在自定义模式下设置颜色范围
                    if (useCustomColorRange)
                    {
                        var customColorRange = LColorRange.Create(
                            lj3DView.ColorRange.UpperLimit / 32768,
                            colorRangeMin,
                            colorRangeMax
                        );
                        lj3DView.ColorRange = customColorRange;
                        // 🔧 修复：不要修改GridPosition，这会导致3D视图位置重置
                        // lj3DView.GridPosition = (int)Math.Floor(colorRangeMin);
                    }
                    
                    // 应用其他视觉效果设置
                    lj3DView.MeshTransparent = (float)meshTransparent;
                    lj3DView.BlendWeight = (float)blendWeight;
                    lj3DView.DisplayColorBar = displayColorBar;
                    lj3DView.DisplayGrid = displayGrid;
                    lj3DView.DisplayAxis = displayAxis;
                    
                    LogUpdate($"[3D颜色] 配置已应用: 自定义={useCustomColorRange}, 范围=[{colorRangeMin:F3}, {colorRangeMax:F3}]");
                }
                
                // 应用到2D视图
                if (_View2D != null && useCustomColorRange)
                {
                    // 2D暂时使用自适应，等待API支持
                    _View2D.ColorRangeFitCommand();
                    LogUpdate("[2D颜色] 暂时使用自适应颜色范围，等待2D ColorRange API支持");
                }


            }
            catch (Exception ex)
            {
                LogUpdate($"应用颜色配置失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 初始化3D检测项目到DataGrid（基于用户设定的输出对象）
        /// </summary>
        /// <param name="measureEx">3D检测系统实例</param>
        public void Initialize3DDetectionItemsFromOutputTargets(LjdMeasureEx measureEx)
        {
            try
            {
                if (measureEx == null || !measureEx.IsEnable) return;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var currentItems = _dataGridItems.Where(item => !item.Is3DItem).ToList();
                        int twoDItemCount = currentItems.Count;

                        int rowNumber = 1;
                        foreach (var item in currentItems)
                        {
                            item.ToolIndex = -1;
                            item.RowNumber = rowNumber++;
                        }

                        LogUpdate($"3D检测初始化: 保留{twoDItemCount}个2D项目，准备重建3D项目");

                        var outputTargets = GetOutputTargets(measureEx);
                        if (outputTargets != null && outputTargets.Length > 0)
                        {
                            int actualItemCount = outputTargets.Length / 2;
                            if (outputTargets.Length % 2 == 0 && actualItemCount > 0)
                            {
                                int nextRowNumber = rowNumber;
                                for (int i = 0; i < actualItemCount; i++)
                                {
                                    int nameIndex = i * 2;

                                    currentItems.Add(new DetectionItem
                                    {
                                        RowNumber = nextRowNumber++,
                                        Name = $"[3D]工具{i + 1}",
                                        Value = string.Empty,
                                        Is3DItem = true,
                                        ToolIndex = nameIndex
                                    });
                                }

                                LogManager.Verbose($"已添加{actualItemCount}个3D检测项目到数据表格（工具名+数值模式），当前总项目数: {currentItems.Count}", "Page1");
                            }
                            else
                            {
                                int nextRowNumber = rowNumber;
                                for (int i = 0; i < outputTargets.Length; i++)
                                {
                                    currentItems.Add(new DetectionItem
                                    {
                                        RowNumber = nextRowNumber++,
                                        Name = $"[3D]项目{i + 1}",
                                        Value = string.Empty,
                                        Is3DItem = true,
                                        ToolIndex = i
                                    });
                                }

                                LogManager.Verbose($"已添加{outputTargets.Length}个3D输出项目到数据表格（传统模式），当前总项目数: {currentItems.Count}", "Page1");
                            }
                        }
                        else
                        {
                            LogUpdate("未找到3D输出配置，跳过3D检测项目初始化");
                        }

                        _fullDataList = currentItems.ToList();
                        if (_showFocusedOnly)
                        {
                            var filteredItems = currentItems.Where(item => _focusedProjects.Contains(item.Name)).ToList();
                            SyncDataGridItems(filteredItems);
                        }
                        else
                        {
                            SyncDataGridItems(currentItems);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUpdate($"初始化3D检测项目时出错: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                LogUpdate($"调度3D检测项目初始化时出错: {ex.Message}");
            }
        }

        #endif
        /// <summary>
        /// 缓存3D检测数据，等待与2D数据同步更新
        /// </summary>
        private static List<DetectionItem> _cached3DItems = null;
        // 🔧 移除锁：private static readonly object _3DDataCacheLock = new object();

        // 2D数据缓存相关
        private static List<DetectionItem> _cached2DItems = null;
        // 🔧 移除锁：private static readonly object _2DDataCacheLock = new object();

        /// <summary>
        /// 缓存晶片高度计算结果（由TryCalculateChipHeight计算，在统一判定时使用）
        /// </summary>
        private static List<(string name, double x2d, double y2d, double x3d, double y3d, double chipH, double refH, double relH)> _cachedChipHeightResults = null;

        /// <summary>
        /// 缓存综合检测项目（需要2D和3D都完成后才能计算的项目，如晶片平面估计）
        /// </summary>
        private static List<DetectionItem> _cachedCombinedItems = null;
        
        /// <summary>
        /// 记录上次UnifiedUpdateDataGrid调用时间，防止重复调用
        /// </summary>
        private static DateTime? _lastUnifiedUpdateTime = null;
        
        /// <summary>
        /// 清空3D数据缓存（在新检测开始前调用，确保不显示上次数据）
        /// </summary>
        public static void Clear3DDataCache()
        {
            // 🔧 移除锁：直接操作
            if (_cached3DItems != null && _cached3DItems.Count > 0)
            {
                _cached3DItems = null;
            }
            else
            {
                LogManager.Verbose($"[3D缓存] 3D缓存已为空，无需清空", "Page1");
            }

            // 清空晶片高度计算结果缓存
            _cachedChipHeightResults = null;

            // 清空综合项目缓存
            _cachedCombinedItems = null;
        }
        
        // 需要从Grid中隐藏的2D项目名称（仅用于晶片高度计算，不显示给用户）
        private static readonly HashSet<string> _hidden2DItemNames = new HashSet<string>
        {
            "PKG中心X", "PKG中心Y", "晶片中心X", "晶片中心Y"
        };

        /// <summary>
        /// 设置2D检测数据缓存，并通知统一检测管理器2D完成
        /// </summary>
        public void SetCached2DItems(List<DetectionItem> items)
        {
            // ===== 提取2D参数用于晶片高度计算 =====
            _chipHeightParams2D = new ChipHeightCalcParams2D();

            if (items != null && items.Count > 0)
            {
                foreach (var item in items)
                {
                    string name = item.Name?.Trim() ?? "";
                    if (double.TryParse(item.Value, out double value))
                    {
                        switch (name)
                        {
                            case "PKG中心X":
                                _chipHeightParams2D.PkgCenterX = value;
                                break;
                            case "PKG中心Y":
                                _chipHeightParams2D.PkgCenterY = value;
                                break;
                            case "晶片中心X":
                                _chipHeightParams2D.ChipCenterX = value;
                                break;
                            case "晶片中心Y":
                                _chipHeightParams2D.ChipCenterY = value;
                                break;
                            case "BLK_PKG角度":
                            case "BLK-PKG角度":  // 兼容两种命名方式
                                _chipHeightParams2D.ChipAngle = value;
                                break;
                            case "BLK长度":
                                _chipHeightParams2D.ChipLength = value;
                                break;
                            case "BLK宽度":
                                _chipHeightParams2D.ChipWidth = value;
                                break;
                        }
                    }
                }

                // 输出收集到的2D参数汇总
                if (_chipHeightParams2D.IsValid)
                {
                    LogManager.Info($"[晶片高度计算] 2D参数收集完成: PKG中心=({_chipHeightParams2D.PkgCenterX:F2},{_chipHeightParams2D.PkgCenterY:F2})像素, " +
                        $"晶片中心=({_chipHeightParams2D.ChipCenterX:F2},{_chipHeightParams2D.ChipCenterY:F2})像素, " +
                        $"BLK角度={_chipHeightParams2D.ChipAngle:F3}°, 长度={_chipHeightParams2D.ChipLength:F2}像素, 宽度={_chipHeightParams2D.ChipWidth:F2}像素", "Page1");

                    // 如果3D参数也有效，尝试计算晶片高度
                    TryCalculateChipHeight();
                }
                else
                {
                    LogManager.Warning($"[晶片高度计算] 2D参数不完整: PKG中心=({_chipHeightParams2D.PkgCenterX},{_chipHeightParams2D.PkgCenterY}), " +
                        $"晶片中心=({_chipHeightParams2D.ChipCenterX},{_chipHeightParams2D.ChipCenterY}), " +
                        $"角度={_chipHeightParams2D.ChipAngle}, 长度={_chipHeightParams2D.ChipLength}, 宽度={_chipHeightParams2D.ChipWidth}", "Page1");
                }
            }
            // ===== 2D参数提取结束 =====

            // 🔧 移除锁：工业控制中检测数据处理是顺序的，不需要锁保护
            _cached2DItems = items;
            LogManager.Info($"[2D缓存] 已缓存{items?.Count ?? 0}个2D检测项目");

            // 🔧 架构修复：简化逻辑，移除错误的"应急启动"机制
            if (_detectionManager != null)
            {
                // 检查是否应该处理检测结果
                if (!_detectionManager.ShouldProcessDetection())
                {
                    return;
                }

                // 系统必须已正确初始化
                if (!_detectionManager.IsSystemInitialized)
                {
                    LogManager.Error("[2D缓存] ❌ 系统未初始化，无法处理2D完成标记");
                    return;
                }
            }
            else
            {
                LogManager.Error("[2D缓存] ❌ 统一检测管理器为null，这是严重错误！");
                return;
            }

            // 🔧 关键修复：通知统一检测管理器2D检测完成
            // 统一检测管理器会检查检测周期是否完成，自动执行统一判定
            _detectionManager?.Mark2DCompleted();

        }
        
        /// <summary>
        /// 获取2D检测数据缓存
        /// </summary>
        public static List<DetectionItem> GetCached2DItems()
        {
            // 🔧 移除锁：直接返回，工业控制中不会有并发访问
            return _cached2DItems;
        }
        
        /// <summary>
        /// 清空2D检测数据缓存
        /// </summary>
        public static void Clear2DDataCache()
        {
            // 🔧 移除锁：直接操作，工业控制中数据处理是顺序的
            if (_cached2DItems != null && _cached2DItems.Count > 0)
            {
                _cached2DItems = null;
            }
            else
            {
                LogManager.Verbose($"[2D缓存] 2D缓存已为空，无需清空", "Page1");
            }
        }

        // ===== 晶片高度计算相关常量 =====
        /// <summary>
        /// 2D相机像元尺寸 (mm)，默认4μm = 0.004mm
        /// </summary>
        private const double PIXEL_SIZE_MM = 0.004;

        /// <summary>
        /// 尝试计算晶片高度（当2D和3D参数都有效时）
        /// </summary>
        private void TryCalculateChipHeight()
        {
            try
            {
                // 检查是否启用晶片平面估计
                string chipPlaneEstimationEnabled = TemplateConfigPage.Instance?.Get3DConfigParameter("晶片平面估计", "false") ?? "false";
                if (chipPlaneEstimationEnabled.ToLower() != "true")
                {
                    return;  // 未启用，直接返回
                }

                // 检查2D和3D参数是否都有效
                if (_chipHeightParams2D == null || !_chipHeightParams2D.IsValid)
                {
                    LogManager.Warning("[晶片高度计算] 2D参数无效，跳过计算", "Page1");
                    return;
                }

                if (_chipHeightParams3D == null || (!(_chipHeightParams3D.IsValid || _chipHeightParams3D.HasChipEdgeData)))
                {
                    LogManager.Warning("[晶片高度计算] 3D参数无效，跳过计算", "Page1");
                    return;
                }

                // 无论使用哪种策略，晶片平面与参考平面参数必须存在
                if (double.IsNaN(_chipHeightParams3D.ChipPlaneA) || double.IsNaN(_chipHeightParams3D.ChipPlaneB) ||
                    double.IsNaN(_chipHeightParams3D.ChipPlaneC) || double.IsNaN(_chipHeightParams3D.RefPlaneC))
                {
                    LogManager.Warning("[晶片高度计算] 3D平面参数不完整，跳过计算", "Page1");
                    return;
                }

                //LogManager.Info("[晶片高度计算] ===== 开始计算晶片四角高度 =====", "Page1");

                // 步骤1: 计算晶片尺寸(mm)
                // 2D检测结果的长度/宽度单位是微米(来自算法)，需先换算成毫米
                double halfLengthMm = (_chipHeightParams2D.ChipLength / 1000.0) / 2;
                double halfWidthMm = (_chipHeightParams2D.ChipWidth / 1000.0) / 2;

                // ===== 新策略：使用3D晶片边缘+交点建立坐标，不依赖PKG中心 =====
                if (_chipHeightParams3D.HasChipEdgeData)
                {
                    double chipLengthMm = _chipHeightParams2D.ChipLength / 1000.0;
                    double chipWidthMm = _chipHeightParams2D.ChipWidth / 1000.0;

                    // 交点作为左下角（晶片左边缘与下边缘交点）
                    double p0x = _chipHeightParams3D.ChipIntersectionX;
                    double p0y = _chipHeightParams3D.ChipIntersectionY;

                    // 下边缘方向向量（从交点附近端点指向远端）
                    (double bx1, double by1) = (_chipHeightParams3D.ChipBottomLineStartX, _chipHeightParams3D.ChipBottomLineStartY);
                    (double bx2, double by2) = (_chipHeightParams3D.ChipBottomLineEndX, _chipHeightParams3D.ChipBottomLineEndY);
                    double distB1 = Math.Sqrt(Math.Pow(bx1 - p0x, 2) + Math.Pow(by1 - p0y, 2));
                    double distB2 = Math.Sqrt(Math.Pow(bx2 - p0x, 2) + Math.Pow(by2 - p0y, 2));
                    (double bnx, double bny) = distB1 <= distB2 ? (bx1, by1) : (bx2, by2);
                    (double bfx, double bfy) = distB1 <= distB2 ? (bx2, by2) : (bx1, by1);
                    double ubx = bfx - bnx;
                    double uby = bfy - bny;
                    double uLen = Math.Sqrt(ubx * ubx + uby * uby);
                    if (uLen > 1e-6) { ubx /= uLen; uby /= uLen; }

                    // 左边缘方向向量
                    (double lx1, double ly1) = (_chipHeightParams3D.ChipLeftLineStartX, _chipHeightParams3D.ChipLeftLineStartY);
                    (double lx2, double ly2) = (_chipHeightParams3D.ChipLeftLineEndX, _chipHeightParams3D.ChipLeftLineEndY);
                    double distL1 = Math.Sqrt(Math.Pow(lx1 - p0x, 2) + Math.Pow(ly1 - p0y, 2));
                    double distL2 = Math.Sqrt(Math.Pow(lx2 - p0x, 2) + Math.Pow(ly2 - p0y, 2));
                    (double lnx, double lny) = distL1 <= distL2 ? (lx1, ly1) : (lx2, ly2);
                    (double lfx, double lfy) = distL1 <= distL2 ? (lx2, ly2) : (lx1, ly1);
                    double vlx = lfx - lnx;
                    double vly = lfy - lny;
                    double vLen = Math.Sqrt(vlx * vlx + vly * vly);
                    if (vLen > 1e-6) { vlx /= vLen; vly /= vLen; }

                    // 观测到的边长（用于自动匹配长/宽）
                    double bottomObsLen = Math.Sqrt(Math.Pow(bx2 - bx1, 2) + Math.Pow(by2 - by1, 2));
                    double leftObsLen = Math.Sqrt(Math.Pow(lx2 - lx1, 2) + Math.Pow(ly2 - ly1, 2));
                    double costLenAsBottom = Math.Abs(bottomObsLen - chipLengthMm) + Math.Abs(leftObsLen - chipWidthMm);
                    double costWidAsBottom = Math.Abs(bottomObsLen - chipWidthMm) + Math.Abs(leftObsLen - chipLengthMm);
                    double bottomDim = chipLengthMm;
                    double leftDim = chipWidthMm;
                    if (costWidAsBottom < costLenAsBottom)
                    {
                        bottomDim = chipWidthMm;
                        leftDim = chipLengthMm;
                    }


                    // 3D四角坐标（mm）
                    // 注意：3D边缘工具的“左/下”是相对3D图像坐标的，现场坐标与晶片物理方位存在左右镜像关系。
                    // 因此这里先按3D图坐标求角点，再做一次左右翻转用于晶片方位命名。
                    (double x3dLTImg, double y3dLTImg) = (p0x + vlx * leftDim, p0y + vly * leftDim);
                    (double x3dLBImg, double y3dLBImg) = (p0x, p0y);
                    (double x3dRBImg, double y3dRBImg) = (p0x + ubx * bottomDim, p0y + uby * bottomDim);
                    (double x3dRTImg, double y3dRTImg) = (x3dLTImg + ubx * bottomDim, y3dLTImg + uby * bottomDim);

                    // 左右翻转后对应晶片物理方位
                    (double x3dLT, double y3dLT) = (x3dRTImg, y3dRTImg);
                    (double x3dRT, double y3dRT) = (x3dLTImg, y3dLTImg);
                    (double x3dLB, double y3dLB) = (x3dRBImg, y3dRBImg);
                    (double x3dRB, double y3dRB) = (x3dLBImg, y3dLBImg);

                    // 2D角点像素位置仍按原逻辑计算（用于日志/分析展示）
                    var cornerOffsets2d = new[]
                    {
                        ("左上角", -halfLengthMm, -halfWidthMm, x3dLT, y3dLT),
                        ("右上角", halfLengthMm, -halfWidthMm, x3dRT, y3dRT),
                        ("右下角", halfLengthMm, halfWidthMm, x3dRB, y3dRB),
                        ("左下角", -halfLengthMm, halfWidthMm, x3dLB, y3dLB),
                    };

                    var resultsNew = new List<(string name, double x2d, double y2d, double x3d, double y3d, double chipH, double refH, double relH)>();
                    foreach (var (name, offsetX, offsetY, x3d, y3d) in cornerOffsets2d)
                    {
                        double chipAngleRad = -_chipHeightParams2D.ChipAngle * Math.PI / 180.0;
                        double dxRot2d = offsetX * Math.Cos(chipAngleRad) - offsetY * Math.Sin(chipAngleRad);
                        double dyRot2d = offsetX * Math.Sin(chipAngleRad) + offsetY * Math.Cos(chipAngleRad);
                        double x2dPixel = _chipHeightParams2D.ChipCenterX + dxRot2d / PIXEL_SIZE_MM;
                        double y2dPixel = _chipHeightParams2D.ChipCenterY + dyRot2d / PIXEL_SIZE_MM;

                        double chipHeight = _chipHeightParams3D.ChipPlaneA * x3d + _chipHeightParams3D.ChipPlaneB * y3d + _chipHeightParams3D.ChipPlaneC;
                        double refA = double.IsNaN(_chipHeightParams3D.RefPlaneA) ? 0 : _chipHeightParams3D.RefPlaneA;
                        double refB = double.IsNaN(_chipHeightParams3D.RefPlaneB) ? 0 : _chipHeightParams3D.RefPlaneB;
                        double refHeight = refA * x3d + refB * y3d + _chipHeightParams3D.RefPlaneC;
                        double relativeHeight = chipHeight - refHeight;

                        resultsNew.Add((name, x2dPixel, y2dPixel, x3d, y3d, chipHeight, refHeight, relativeHeight));
                    }

                    //LogManager.Info("[晶片高度计算] ----- 计算结果(新策略) -----", "Page1");
                    //foreach (var r in resultsNew)
                    //{
                    //    LogManager.Info($"[晶片高度计算] {r.name}: 2D=({r.x2d:F2},{r.y2d:F2})像素, 3D=({r.x3d:F4},{r.y3d:F4})mm, " +
                    //        $"晶片高度={r.chipH:F4}mm, 参考高度={r.refH:F4}mm, 相对高度={r.relH:F4}mm", "Page1");
                    //}

                    _cachedChipHeightResults = resultsNew;
                    return;
                }
                // ===== 新策略结束，以下为旧策略（依赖PKG中心） =====

                // 计算3D PKG角度（从直线起点终点）
                double dx = _chipHeightParams3D.LineEndX - _chipHeightParams3D.LineStartX;
                double dy = _chipHeightParams3D.LineEndY - _chipHeightParams3D.LineStartY;
                double pkgAngleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                //LogManager.Info($"[晶片高度计算] PKG角度={pkgAngleDeg:F3}°（从直线起点终点计算）", "Page1");

                //LogManager.Info($"[晶片高度计算] 晶片尺寸: 长={_chipHeightParams2D.ChipLength:F2}μm({halfLengthMm * 2:F4}mm), " +
                //    $"宽={_chipHeightParams2D.ChipWidth:F2}μm({halfWidthMm * 2:F4}mm)", "Page1");

                // 四个角相对于晶片中心的初始偏移(mm)
                // 原点在左上角坐标系: X向右, Y向下
                // 顺序: 左上、右上、右下、左下
                var cornerOffsets = new[]
                {
                    ("左上角", -halfLengthMm, -halfWidthMm),
                    ("右上角", halfLengthMm, -halfWidthMm),
                    ("右下角", halfLengthMm, halfWidthMm),
                    ("左下角", -halfLengthMm, halfWidthMm),
                };

                var results = new List<(string name, double x2d, double y2d, double x3d, double y3d, double chipH, double refH, double relH)>();

                foreach (var (name, offsetX, offsetY) in cornerOffsets)
                {
                    // 步骤2: 应用晶片角度旋转(在2D坐标系中，相对于晶片中心)
                    // 角度取负值以匹配实际图像
                    double chipAngleRad = -_chipHeightParams2D.ChipAngle * Math.PI / 180.0;
                    double dxRot = offsetX * Math.Cos(chipAngleRad) - offsetY * Math.Sin(chipAngleRad);
                    double dyRot = offsetX * Math.Sin(chipAngleRad) + offsetY * Math.Cos(chipAngleRad);

                    // 2D坐标(像素) - 角点的绝对位置
                    double x2dPixel = _chipHeightParams2D.ChipCenterX + dxRot / PIXEL_SIZE_MM;
                    double y2dPixel = _chipHeightParams2D.ChipCenterY + dyRot / PIXEL_SIZE_MM;

                    // 计算角点相对于2D PKG中心的偏移(mm)
                    double offsetFromPkgXMm = (x2dPixel - _chipHeightParams2D.PkgCenterX) * PIXEL_SIZE_MM;
                    double offsetFromPkgYMm = (y2dPixel - _chipHeightParams2D.PkgCenterY) * PIXEL_SIZE_MM;

                    // 步骤3: 从2D坐标系转换到3D坐标系
                    // 关键：2D是镜像后的，3D是镜像前的，所以X方向偏移需要取反
                    double offsetXFor3d = -offsetFromPkgXMm;  // X方向取反
                    double offsetYFor3d = offsetFromPkgYMm;   // Y方向不变

                    // 应用PKG角度旋转
                    double pkgAngleRad = pkgAngleDeg * Math.PI / 180.0;
                    double dx3d = offsetXFor3d * Math.Cos(pkgAngleRad) - offsetYFor3d * Math.Sin(pkgAngleRad);
                    double dy3d = offsetXFor3d * Math.Sin(pkgAngleRad) + offsetYFor3d * Math.Cos(pkgAngleRad);

                    // 步骤4: 计算3D坐标系中的绝对位置
                    double x3d = _chipHeightParams3D.PkgCenterX + dx3d;
                    double y3d = _chipHeightParams3D.PkgCenterY + dy3d;

                    // 步骤5: 使用平面方程计算高度
                    // 平面方程: Z = α × X + β × Y + Z0

                    // 晶片平面高度
                    double chipHeight = _chipHeightParams3D.ChipPlaneA * x3d + _chipHeightParams3D.ChipPlaneB * y3d + _chipHeightParams3D.ChipPlaneC;

                    // 参考平面高度（完整平面方程），缺失斜率时退化为只用截距
                    double refA = double.IsNaN(_chipHeightParams3D.RefPlaneA) ? 0 : _chipHeightParams3D.RefPlaneA;
                    double refB = double.IsNaN(_chipHeightParams3D.RefPlaneB) ? 0 : _chipHeightParams3D.RefPlaneB;
                    double refHeight = refA * x3d + refB * y3d + _chipHeightParams3D.RefPlaneC;

                    // 相对高度 = 晶片高度 - 参考平面高度
                    double relativeHeight = chipHeight - refHeight;

                    results.Add((name, x2dPixel, y2dPixel, x3d, y3d, chipHeight, refHeight, relativeHeight));
                }

                // 输出计算结果
                LogManager.Info("[晶片高度计算] ----- 计算结果 -----", "Page1");
                foreach (var r in results)
                {
                    LogManager.Info($"[晶片高度计算] {r.name}: 2D=({r.x2d:F2},{r.y2d:F2})像素, " +
                        $"3D=({r.x3d:F4},{r.y3d:F4})mm, 晶片高度={r.chipH:F4}mm, 参考高度={r.refH:F4}mm, 相对高度={r.relH:F4}mm", "Page1");
                }

                // 计算统计信息
                double minH = results.Min(r => r.relH);
                double maxH = results.Max(r => r.relH);
                double avgH = results.Average(r => r.relH);
                double diffH = maxH - minH;

                // 缓存计算结果，等待统一判定时使用
                _cachedChipHeightResults = results;
            }
            catch (Exception ex)
            {
                LogManager.Error($"[晶片高度计算] 计算失败: {ex.Message}", "Page1");
                _cachedChipHeightResults = null;
            }
        }

        /// <summary>
        /// 构建当前晶片高度计算的分析快照，用于3D映射分析窗口展示
        /// </summary>
        internal ChipHeightAnalysisSnapshot CreateChipHeightAnalysisSnapshot()
        {
            var snapshot = new ChipHeightAnalysisSnapshot
            {
                PixelSizeMm = PIXEL_SIZE_MM
            };

            if (_chipHeightParams2D != null && _chipHeightParams2D.IsValid)
            {
                snapshot.Params2D = new ChipHeightAnalysisParams2D
                {
                    PkgCenterX = _chipHeightParams2D.PkgCenterX,
                    PkgCenterY = _chipHeightParams2D.PkgCenterY,
                    ChipCenterX = _chipHeightParams2D.ChipCenterX,
                    ChipCenterY = _chipHeightParams2D.ChipCenterY,
                    ChipLengthUm = _chipHeightParams2D.ChipLength,
                    ChipWidthUm = _chipHeightParams2D.ChipWidth,
                    ChipAngleDeg = _chipHeightParams2D.ChipAngle
                };
            }

            if (_chipHeightParams3D != null && _chipHeightParams3D.IsValid)
            {
                double dx = _chipHeightParams3D.LineEndX - _chipHeightParams3D.LineStartX;
                double dy = _chipHeightParams3D.LineEndY - _chipHeightParams3D.LineStartY;
                double pkgAngleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                snapshot.Params3D = new ChipHeightAnalysisParams3D
                {
                    PkgCenterX = _chipHeightParams3D.PkgCenterX,
                    PkgCenterY = _chipHeightParams3D.PkgCenterY,
                    LineStartX = _chipHeightParams3D.LineStartX,
                    LineStartY = _chipHeightParams3D.LineStartY,
                    LineEndX = _chipHeightParams3D.LineEndX,
                    LineEndY = _chipHeightParams3D.LineEndY,
                    ChipPlaneA = _chipHeightParams3D.ChipPlaneA,
                    ChipPlaneB = _chipHeightParams3D.ChipPlaneB,
                    ChipPlaneC = _chipHeightParams3D.ChipPlaneC,
                    RefPlaneA = _chipHeightParams3D.RefPlaneA,
                    RefPlaneB = _chipHeightParams3D.RefPlaneB,
                    RefPlaneC = _chipHeightParams3D.RefPlaneC,
                    PkgAngleDeg = pkgAngleDeg
                };
            }

            if (_cachedChipHeightResults != null && _cachedChipHeightResults.Count > 0)
            {
                snapshot.Corners = _cachedChipHeightResults.Select(r => new ChipCornerAnalysisItem
                {
                    Name = r.name,
                    X2DPixel = r.x2d,
                    Y2DPixel = r.y2d,
                    X3D = r.x3d,
                    Y3D = r.y3d,
                    ChipHeight = r.chipH,
                    RefHeight = r.refH,
                    RelativeHeight = r.relH
                }).ToList();
            }

            snapshot.GrayImagePath = ResolveGrayImagePathForAnalysis();

            return snapshot;
        }

        /// <summary>
        /// 尝试获取用于3D映射分析的灰度图路径（检测最新一组/当前组/最近存图目录）
        /// </summary>
        private string ResolveGrayImagePathForAnalysis()
        {
            try
            {
                // 1) 最新测试组
                var lastGroup = GetLastTestImageGroup();
                if (lastGroup != null && lastGroup.Has3DImages && File.Exists(lastGroup.GrayImagePath))
                {
                    LogManager.Verbose($"[3D映射分析] 使用最新组灰度图: {lastGroup.GrayImagePath}", "Page1");
                    return lastGroup.GrayImagePath;
                }

                // 2) 当前测试组
                var currentGroup = _imageTestManager?.CurrentGroup;
                if (currentGroup != null && currentGroup.Has3DImages && File.Exists(currentGroup.GrayImagePath))
                {
                    LogManager.Verbose($"[3D映射分析] 使用当前组灰度图: {currentGroup.GrayImagePath}", "Page1");
                    return currentGroup.GrayImagePath;
                }

                // 3) 从最近存图目录查找 gray* 文件
                // 真实运行时灰度图通常保存在 {finalSaveDirectory}\\3D\\gray_*.bmp/png
                if (!string.IsNullOrEmpty(_lastSavedImageSource1Path) && File.Exists(_lastSavedImageSource1Path))
                {
                    string source1Dir = Path.GetDirectoryName(_lastSavedImageSource1Path);
                    string parentDir = Path.GetDirectoryName(source1Dir);
                    if (Directory.Exists(parentDir))
                    {
                        // 优先在 3D 子目录中查找
                        string threeDDir = Path.Combine(parentDir, "3D");
                        if (Directory.Exists(threeDDir))
                        {
                            var grayCandidates3D = Directory.GetFiles(threeDDir, "gray*.png")
                                .Concat(Directory.GetFiles(threeDDir, "gray*.bmp"))
                                .OrderByDescending(f => File.GetLastWriteTime(f))
                                .ToList();
                            if (grayCandidates3D.Count > 0)
                            {
                                LogManager.Verbose($"[3D映射分析] 使用最近目录3D灰度图: {grayCandidates3D[0]}", "Page1");
                                return grayCandidates3D[0];
                            }
                        }

                        // 兼容旧结构：父目录直接放 gray* 文件
                        var grayCandidates = Directory.GetFiles(parentDir, "gray*.png")
                            .Concat(Directory.GetFiles(parentDir, "gray*.bmp"))
                            .OrderByDescending(f => File.GetLastWriteTime(f))
                            .ToList();
                        if (grayCandidates.Count > 0)
                        {
                            LogManager.Verbose($"[3D映射分析] 使用最近目录灰度图: {grayCandidates[0]}", "Page1");
                            return grayCandidates[0];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Verbose($"[3D映射分析] 获取灰度图失败: {ex.Message}", "Page1");
            }

            return null;
        }

        /// <summary>
        /// 计算综合检测项目（晶片平面估计等需要2D和3D都完成后才能计算的项目）
        /// 在ExecuteUnifiedJudgementAndIO中调用
        /// </summary>
        private void CalculateCombinedDetectionItems()
        {
            try
            {
                // 初始化综合项目缓存
                _cachedCombinedItems = new List<DetectionItem>();

                // 检查是否启用晶片平面估计
                string chipPlaneEstimationEnabled = TemplateConfigPage.Instance?.Get3DConfigParameter("晶片平面估计", "false") ?? "false";
                if (chipPlaneEstimationEnabled.ToLower() != "true")
                {
                    //LogManager.Verbose("[综合项目] 晶片平面估计未启用，跳过计算", "Page1");
                    return;
                }

                // 检查晶片高度计算结果是否有效
                if (_cachedChipHeightResults == null || _cachedChipHeightResults.Count != 4)
                {
                    LogManager.Warning("[综合项目] 晶片高度计算结果无效，无法进行晶片平面估计", "Page1");
                    return;
                }

                LogManager.Info("[综合项目] 开始计算晶片平面估计项目", "Page1");

                // 获取PKG底座高度（从3D缓存数据中查找原始值）
                double pkgBaseHeight = 0;
                if (_cached3DItems != null)
                {
                    var pkgBaseItem = _cached3DItems.FirstOrDefault(item => item.Name == "[3D]PKG底座高度");
                    if (pkgBaseItem != null && double.TryParse(pkgBaseItem.Value, out double parsedHeight))
                    {
                        pkgBaseHeight = parsedHeight;
                        LogManager.Info($"[综合项目] 获取PKG底座高度: {pkgBaseHeight:F4}mm", "Page1");
                    }
                    else
                    {
                        LogManager.Warning("[综合项目] 未找到[3D]PKG底座高度，使用0作为默认值", "Page1");
                    }
                }

                // 获取晶片平面估计参数（含补偿）
                string leftUpperLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("左端高度上限", "0.1");
                string leftLowerLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("左端高度下限", "0");
                string leftCompensationStr = TemplateConfigPage.Instance.Get3DConfigParameter("左端高度补偿", "0");
                string rightUpperLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("右端高度上限", "0.1");
                string rightLowerLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("右端高度下限", "0");
                string rightCompensationStr = TemplateConfigPage.Instance.Get3DConfigParameter("右端高度补偿", "0");
                string pitchUpperLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("俯仰值上限", "0.05");
                string pitchLowerLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("俯仰值下限", "-0.05");
                string rollUpperLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("滚转值上限", "0.05");
                string rollLowerLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("滚转值下限", "-0.05");

                // 获取4个角点是否启用NG判断的设置
                string leftTopNgEnableStr = TemplateConfigPage.Instance.Get3DConfigParameter("晶片左上启用NG判断", "true");
                string leftBottomNgEnableStr = TemplateConfigPage.Instance.Get3DConfigParameter("晶片左下启用NG判断", "true");
                string rightTopNgEnableStr = TemplateConfigPage.Instance.Get3DConfigParameter("晶片右上启用NG判断", "true");
                string rightBottomNgEnableStr = TemplateConfigPage.Instance.Get3DConfigParameter("晶片右下启用NG判断", "true");

                bool leftTopNgEnable = !bool.TryParse(leftTopNgEnableStr, out bool ltEnable) || ltEnable;
                bool leftBottomNgEnable = !bool.TryParse(leftBottomNgEnableStr, out bool lbEnable) || lbEnable;
                bool rightTopNgEnable = !bool.TryParse(rightTopNgEnableStr, out bool rtEnable) || rtEnable;
                bool rightBottomNgEnable = !bool.TryParse(rightBottomNgEnableStr, out bool rbEnable) || rbEnable;

                double.TryParse(leftUpperLimitStr, out double leftUpperLimit);
                double.TryParse(leftLowerLimitStr, out double leftLowerLimit);
                double.TryParse(leftCompensationStr, out double leftCompensation);
                double.TryParse(rightUpperLimitStr, out double rightUpperLimit);
                double.TryParse(rightLowerLimitStr, out double rightLowerLimit);
                double.TryParse(rightCompensationStr, out double rightCompensation);
                double.TryParse(pitchUpperLimitStr, out double pitchUpperLimit);
                double.TryParse(pitchLowerLimitStr, out double pitchLowerLimit);
                double.TryParse(rollUpperLimitStr, out double rollUpperLimit);
                double.TryParse(rollLowerLimitStr, out double rollLowerLimit);

                // 获取各角点的相对高度：relH 已经 = 晶片平面 - 参考平面(002/PKG)，再减 PKG底座得到最终输出
                double leftTopHeight = _cachedChipHeightResults.FirstOrDefault(r => r.name == "左上角").relH - pkgBaseHeight;
                double rightTopHeight = _cachedChipHeightResults.FirstOrDefault(r => r.name == "右上角").relH - pkgBaseHeight;
                double rightBottomHeight = _cachedChipHeightResults.FirstOrDefault(r => r.name == "右下角").relH - pkgBaseHeight;
                double leftBottomHeight = _cachedChipHeightResults.FirstOrDefault(r => r.name == "左下角").relH - pkgBaseHeight;

                // 应用补偿：在减底座后再加补偿
                double leftTopCompensated = leftTopHeight + leftCompensation;
                double rightTopCompensated = rightTopHeight + rightCompensation;
                double rightBottomCompensated = rightBottomHeight + rightCompensation;
                double leftBottomCompensated = leftBottomHeight + leftCompensation;

                // 计算俯仰值（左上 - 右上）和滚转值（左上 - 左下）
                double pitchValue = leftTopCompensated - rightTopCompensated;
                double rollValue = leftTopCompensated - leftBottomCompensated;

                // 计算各角点的实际使用上下限（未启用NG判断时使用-1~1）
                double leftTopActualLower = leftTopNgEnable ? leftLowerLimit : -1;
                double leftTopActualUpper = leftTopNgEnable ? leftUpperLimit : 1;
                double leftBottomActualLower = leftBottomNgEnable ? leftLowerLimit : -1;
                double leftBottomActualUpper = leftBottomNgEnable ? leftUpperLimit : 1;
                double rightTopActualLower = rightTopNgEnable ? rightLowerLimit : -1;
                double rightTopActualUpper = rightTopNgEnable ? rightUpperLimit : 1;
                double rightBottomActualLower = rightBottomNgEnable ? rightLowerLimit : -1;
                double rightBottomActualUpper = rightBottomNgEnable ? rightUpperLimit : 1;

                // 添加左上角高度
                _cachedCombinedItems.Add(new DetectionItem
                {
                    Name = "晶片左上高度",
                    Value = leftTopCompensated.ToString("F3"),
                    LowerLimit = leftTopActualLower.ToString("F3"),
                    UpperLimit = leftTopActualUpper.ToString("F3"),
                    IsOutOfRange = leftTopCompensated < leftTopActualLower || leftTopCompensated > leftTopActualUpper,
                    Is3DItem = false,
                    ToolIndex = 20001
                });

                // 添加左下角高度
                _cachedCombinedItems.Add(new DetectionItem
                {
                    Name = "晶片左下高度",
                    Value = leftBottomCompensated.ToString("F3"),
                    LowerLimit = leftBottomActualLower.ToString("F3"),
                    UpperLimit = leftBottomActualUpper.ToString("F3"),
                    IsOutOfRange = leftBottomCompensated < leftBottomActualLower || leftBottomCompensated > leftBottomActualUpper,
                    Is3DItem = false,
                    ToolIndex = 20004
                });

                // 添加右上角高度
                _cachedCombinedItems.Add(new DetectionItem
                {
                    Name = "晶片右上高度",
                    Value = rightTopCompensated.ToString("F3"),
                    LowerLimit = rightTopActualLower.ToString("F3"),
                    UpperLimit = rightTopActualUpper.ToString("F3"),
                    IsOutOfRange = rightTopCompensated < rightTopActualLower || rightTopCompensated > rightTopActualUpper,
                    Is3DItem = false,
                    ToolIndex = 20002
                });

                // 添加右下角高度
                _cachedCombinedItems.Add(new DetectionItem
                {
                    Name = "晶片右下高度",
                    Value = rightBottomCompensated.ToString("F3"),
                    LowerLimit = rightBottomActualLower.ToString("F3"),
                    UpperLimit = rightBottomActualUpper.ToString("F3"),
                    IsOutOfRange = rightBottomCompensated < rightBottomActualLower || rightBottomCompensated > rightBottomActualUpper,
                    Is3DItem = false,
                    ToolIndex = 20003
                });



                // 添加俯仰值
                _cachedCombinedItems.Add(new DetectionItem
                {
                    Name = "晶片俯仰值",
                    Value = pitchValue.ToString("F3"),
                    LowerLimit = pitchLowerLimit.ToString("F3"),
                    UpperLimit = pitchUpperLimit.ToString("F3"),
                    IsOutOfRange = pitchValue < pitchLowerLimit || pitchValue > pitchUpperLimit,
                    Is3DItem = false,
                    ToolIndex = 20005
                });

                // 添加滚转值
                _cachedCombinedItems.Add(new DetectionItem
                {
                    Name = "晶片滚转值",
                    Value = rollValue.ToString("F3"),
                    LowerLimit = rollLowerLimit.ToString("F3"),
                    UpperLimit = rollUpperLimit.ToString("F3"),
                    IsOutOfRange = rollValue < rollLowerLimit || rollValue > rollUpperLimit,
                    Is3DItem = false,
                    ToolIndex = 20006
                });

                LogManager.Info($"[综合项目] 晶片平面估计计算完成: " +
                    $"(相对高度->减底座->加补偿) 左上={leftTopHeight:F4}->{leftTopCompensated:F4}, " +
                    $"右上={rightTopHeight:F4}->{rightTopCompensated:F4}, " +
                    $"右下={rightBottomHeight:F4}->{rightBottomCompensated:F4}, 左下={leftBottomHeight:F4}->{leftBottomCompensated:F4}, " +
                    $"俯仰={pitchValue:F4}, 滚转={rollValue:F4}, PKG底座={pkgBaseHeight:F4}, 补偿(左/右)={leftCompensation:F4}/{rightCompensation:F4}", "Page1");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[综合项目] 计算综合检测项目失败: {ex.Message}", "Page1");
                _cachedCombinedItems = null;
            }
        }

        #if false // Legacy in-proc 3D data parsing (Keyence/Ljd types)
        /// <summary>
        /// 更新3D检测数据到DataGrid（基于OutputTargets）- 改为缓存模式
        /// </summary>
        /// <param name="result">3D检测结果</param>
        /// <param name="measureEx">3D检测系统实例</param>
        private void Update3DDetectionDataFromOutputTargets(LjdExecuteResult result, LjdMeasureEx measureEx)
        {
            try
            {
                if (result?.Results == null || measureEx == null) return;

                // ===== 提取平面、直线、图形搜索工具的参数用于晶片高度计算 =====
                _chipHeightParams3D = new ChipHeightCalcParams3D();

                // 重置G1/G2直接提取标志
                _hasExtractedG1 = false;
                _hasExtractedG2 = false;
                _extractedG1Value = 0;
                _extractedG2Value = 0;

                var allTools = result.Results;
                int toolCount = allTools.Count;
                for (int debugIdx = 0; debugIdx < toolCount; debugIdx++)
                {
                    var currentTool = allTools[debugIdx];
                    if (currentTool == null) continue;

                    // 处理平面工具 LPlaneToolInfo
                    if (currentTool is Keyence.LjDevMeasure.LPlaneToolInfo planeToolInfo)
                    {
                        try
                        {
                            var planeResult = planeToolInfo.Result as Keyence.LjDevMeasure.LPlaneResult;
                            if (planeResult?.Plane != null)
                            {
                                double planeA = planeResult.Plane.A;  // X斜率
                                double planeB = planeResult.Plane.B;  // Y斜率
                                double planeC = planeResult.Plane.C;  // Z截距
                                LogManager.Info($"[3D平面] 工具[{debugIdx}] {planeToolInfo.Name}: A(X斜率)={planeA:F6}, B(Y斜率)={planeB:F6}, C(Z截距)={planeC:F6}", "Page1");

                                // 根据工具名识别是晶片平面还是参考平面
                                string toolName = planeToolInfo.Name ?? "";
                                if (toolName.Contains("晶片平面"))
                                {
                                    _chipHeightParams3D.ChipPlaneA = planeA;
                                    _chipHeightParams3D.ChipPlaneB = planeB;
                                    _chipHeightParams3D.ChipPlaneC = planeC;
                                }
                                else if (toolName.Contains("参考平面") || toolName.Contains("基准平面") || toolName.Contains("平面"))
                                {
                                    // 002平面作为参考平面，记录完整平面
                                    _chipHeightParams3D.RefPlaneA = planeA;
                                    _chipHeightParams3D.RefPlaneB = planeB;
                                    _chipHeightParams3D.RefPlaneC = planeC;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Warning($"[3D平面] 读取工具[{debugIdx}]平面参数失败: {ex.Message}", "Page1");
                        }
                    }
                    // 处理直线工具 LLineToolInfo
                    else if (currentTool is Keyence.LjDevMeasure.LLineToolInfo lineToolInfo)
                    {
                        try
                        {
                            var lineResult = lineToolInfo.Result as Keyence.LjDevMeasure.LLineResult;
                            if (lineResult != null)
                            {
                                var line = lineResult.DetectLine.Line;
                                double dx = line.End.X - line.Start.X;
                                double dy = line.End.Y - line.Start.Y;
                                double lineAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                                LogManager.Info($"[3D直线] 工具[{debugIdx}] {lineToolInfo.Name}: Angle(直线角度)={lineAngle:F6}, Start=({line.Start.X:F3},{line.Start.Y:F3}), End=({line.End.X:F3},{line.End.Y:F3})", "Page1");

                                // 收集直线起点终点用于PKG角度计算
                                string toolName = lineToolInfo.Name ?? "";
                                if (toolName.Contains("直线"))
                                {
                                    _chipHeightParams3D.LineStartX = line.Start.X;
                                    _chipHeightParams3D.LineStartY = line.Start.Y;
                                    _chipHeightParams3D.LineEndX = line.End.X;
                                    _chipHeightParams3D.LineEndY = line.End.Y;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Warning($"[3D直线] 读取工具[{debugIdx}]直线参数失败: {ex.Message}", "Page1");
                        }
                    }
                    // 处理3D直线工具 LLine3DToolInfo（晶片边缘）
                    else if (currentTool is Keyence.LjDevMeasure.LLine3DToolInfo line3DToolInfo)
                    {
                        try
                        {
                            var line3DResult = line3DToolInfo.Result as Keyence.LjDevMeasure.LLine3DResult;
                            if (line3DResult?.GlobalDetectLine != null && line3DResult.GlobalDetectLine.Enable)
                            {
                                var line3d = line3DResult.GlobalDetectLine.Line;
                                LogManager.Info($"[3D直线3D] 工具[{debugIdx}] {line3DToolInfo.Name}: Start=({line3d.Start.X:F3},{line3d.Start.Y:F3}), End=({line3d.End.X:F3},{line3d.End.Y:F3})", "Page1");

                                string toolName = line3DToolInfo.Name ?? "";
                                if (toolName.Contains("晶片下边缘"))
                                {
                                    _chipHeightParams3D.ChipBottomLineStartX = line3d.Start.X;
                                    _chipHeightParams3D.ChipBottomLineStartY = line3d.Start.Y;
                                    _chipHeightParams3D.ChipBottomLineEndX = line3d.End.X;
                                    _chipHeightParams3D.ChipBottomLineEndY = line3d.End.Y;
                                }
                                else if (toolName.Contains("晶片左边缘"))
                                {
                                    _chipHeightParams3D.ChipLeftLineStartX = line3d.Start.X;
                                    _chipHeightParams3D.ChipLeftLineStartY = line3d.Start.Y;
                                    _chipHeightParams3D.ChipLeftLineEndX = line3d.End.X;
                                    _chipHeightParams3D.ChipLeftLineEndY = line3d.End.Y;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Warning($"[3D直线3D] 读取工具[{debugIdx}]直线3D参数失败: {ex.Message}", "Page1");
                        }
                    }
                    // 处理3D交点工具 LPointIntersection3DToolInfo（晶片交点）
                    else if (currentTool is Keyence.LjDevMeasure.LPointIntersection3DToolInfo intersection3DToolInfo)
                    {
                        try
                        {
                            var inter3DResult = intersection3DToolInfo.Result as Keyence.LjDevMeasure.LPointIntersection3DResult;
                            if (inter3DResult?.GlobalDetectPoint != null && inter3DResult.GlobalDetectPoint.Enable)
                            {
                                var p = inter3DResult.GlobalDetectPoint.Point;
                                LogManager.Info($"[3D交点3D] 工具[{debugIdx}] {intersection3DToolInfo.Name}: X={p.X:F3}, Y={p.Y:F3}", "Page1");

                                string toolName = intersection3DToolInfo.Name ?? "";
                                if (toolName.Contains("晶片交点"))
                                {
                                    _chipHeightParams3D.ChipIntersectionX = p.X;
                                    _chipHeightParams3D.ChipIntersectionY = p.Y;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Warning($"[3D交点3D] 读取工具[{debugIdx}]交点3D参数失败: {ex.Message}", "Page1");
                        }
                    }
                    // 处理图形搜索工具 LPatternMatchToolInfo
                    else if (currentTool is Keyence.LjDevMeasure.LPatternMatchToolInfo patternMatchToolInfo)
                    {
                        try
                        {
                            var patternResult = patternMatchToolInfo.Result as Keyence.LjDevMeasure.LPatternMatchResult;
                            if (patternResult != null)
                            {
                                var absPos = patternResult.AbsolutePosition;
                                double centerX = absPos.CenterPosition.X;
                                double centerY = absPos.CenterPosition.Y;
                                double angle = absPos.Angle;
                                double score = patternResult.Score;
                                LogManager.Info($"[3D图形搜索] 工具[{debugIdx}] {patternMatchToolInfo.Name}: 中心X={centerX:F3}, 中心Y={centerY:F3}, 角度={angle:F3}, 相似度={score:F3}", "Page1");

                                // 收集图形搜索中心作为PKG中心（亮度搜索中心）
                                string toolName = patternMatchToolInfo.Name ?? "";
                                if (toolName.Contains("图形搜索"))
                                {
                                    _chipHeightParams3D.PkgCenterX = centerX;
                                    _chipHeightParams3D.PkgCenterY = centerY;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Warning($"[3D图形搜索] 读取工具[{debugIdx}]图形搜索参数失败: {ex.Message}", "Page1");
                        }
                    }
                    // 处理交点工具 LPointIntersectionToolInfo（用于PKG中心点）
                    else if (currentTool is Keyence.LjDevMeasure.LPointIntersectionToolInfo intersectionToolInfo)
                    {
                        try
                        {
                            var intersectionResult = intersectionToolInfo.Result as Keyence.LjDevMeasure.LPointIntersectionResult;
                            if (intersectionResult != null)
                            {
                                var detectPoint = intersectionResult.DetectPoint;
                                if (detectPoint.Enable)
                                {
                                    double x = detectPoint.Point.X;
                                    double y = detectPoint.Point.Y;
                                    LogManager.Info($"[3D交点] 工具[{debugIdx}] {intersectionToolInfo.Name}: X={x:F3}, Y={y:F3}", "Page1");

                                    // [021]PKG中心点：暂不用于2D->3D映射（按现场反馈屏蔽），仅保留日志
                                }
                                else
                                {
                                    LogManager.Warning($"[3D交点] 工具[{debugIdx}] {intersectionToolInfo.Name}: DetectPoint未启用/无效", "Page1");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Warning($"[3D交点] 读取工具[{debugIdx}]交点参数失败: {ex.Message}", "Page1");
                        }
                    }
                    // 处理高度工具 LHeightToolInfo（用于G1、G2等高度测量）
                    else if (currentTool is Keyence.LjDevMeasure.LHeightToolInfo heightToolInfo)
                    {
                        try
                        {
                            string toolName = heightToolInfo.Name ?? "";

                            // 检查是否是G1或G2工具（支持包含G1/G2的名称）
                            bool isG1Tool = toolName.Contains("G1");
                            bool isG2Tool = toolName.Contains("G2") && !toolName.Contains("G1"); // 避免G1-G2被误认为G2

                            if (isG1Tool || isG2Tool)
                            {
                                // 从DisplayText中提取高度值
                                // DisplayText格式是多行的：
                                // [009]G1
                                // [0] 峰值高度 0.222 mm
                                string displayText = heightToolInfo.DisplayText();

                                if (!string.IsNullOrWhiteSpace(displayText))
                                {
                                    // 按换行符分割，取包含数值的行
                                    var lines = displayText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                                    // 遍历所有行，查找包含数值的行（通常是第二行，格式如 "[0] 峰值高度 0.222 mm"）
                                    foreach (var line in lines)
                                    {
                                        // 跳过工具名行（以[数字]G开头的行）
                                        if (line.Contains("G1") || line.Contains("G2")) continue;

                                        // 尝试从行中提取数值（查找数字模式）
                                        var match = System.Text.RegularExpressions.Regex.Match(line, @"[\d.]+(?=\s*mm)");
                                        if (match.Success && double.TryParse(match.Value, out double heightValue))
                                        {
                                            // 记录G1或G2的原始高度值，用于后续缓存时添加
                                            if (isG1Tool)
                                            {
                                                _extractedG1Value = heightValue;
                                                _hasExtractedG1 = true;
                                                LogManager.Info($"[3D高度] 提取G1工具值: {heightValue:F3}", "Page1");
                                            }
                                            else if (isG2Tool)
                                            {
                                                _extractedG2Value = heightValue;
                                                _hasExtractedG2 = true;
                                                LogManager.Info($"[3D高度] 提取G2工具值: {heightValue:F3}", "Page1");
                                            }
                                            break; // 找到后退出循环
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Warning($"[3D高度] 读取工具[{debugIdx}]高度参数失败: {ex.Message}", "Page1");
                        }
                    }
                }

                // 输出收集到的3D参数汇总
                if (_chipHeightParams3D.IsValid || _chipHeightParams3D.HasChipEdgeData)
                {

                    // 如果2D参数也有效，尝试计算晶片高度
                    TryCalculateChipHeight();
                }
                else
                {
                    LogManager.Warning($"[晶片高度计算] 3D参数不完整: PKG中心=({_chipHeightParams3D.PkgCenterX},{_chipHeightParams3D.PkgCenterY}), " +
                        $"直线=({_chipHeightParams3D.LineStartX},{_chipHeightParams3D.LineStartY})->({_chipHeightParams3D.LineEndX},{_chipHeightParams3D.LineEndY}), " +
                        $"晶片平面=({_chipHeightParams3D.ChipPlaneA},{_chipHeightParams3D.ChipPlaneB},{_chipHeightParams3D.ChipPlaneC}), 参考平面C={_chipHeightParams3D.RefPlaneC}", "Page1");
                }
                // ===== 3D参数提取结束 =====

                _cached3DItems = new List<DetectionItem>();

                    // 获取输出对象
                    var outputTargets = GetOutputTargets(measureEx);
                    if (outputTargets == null || outputTargets.Length == 0) 
                    {
                        LogUpdate("无法获取3D输出对象，3D数据缓存为空");
                        return;
                    }

                    // 获取输出数据
                    string[] datas = GetOutputDatasFromTargets(result, outputTargets);
                    if (datas != null && datas.Length > 0)
                    {
                        // **缓存3D检测项目，不直接更新DataGrid**
                        // 检查是否是工具名+数值模式（偶数个输出且大于0）
                        // PKG底座高度提升到外部作用域，供后续G1/G2创建使用
                        double pkgBaseHeight = 0;
                        if (datas.Length % 2 == 0 && datas.Length > 0)
                        {
                            int actualItemCount = datas.Length / 2;

                            // 首先查找PKG底座高度的值
                            for (int i = 0; i < actualItemCount; i++)
                            {
                                int nameIndex = i * 2;
                                string toolName = datas[nameIndex]?.ToString().Trim();
                                if (!string.IsNullOrWhiteSpace(toolName))
                                {
                                    string processedToolName = System.Text.RegularExpressions.Regex.Replace(toolName, @"\[\d+\]", "");
                                    if (processedToolName == "PKG底座高度")
                                    {
                                        int valueIndex = i * 2 + 1;
                                        string toolValue = valueIndex < datas.Length ? (datas[valueIndex] ?? "") : "";
                                        if (double.TryParse(toolValue, out pkgBaseHeight))
                                        {
                                            LogManager.Verbose($"[3D处理] 找到PKG底座高度: {pkgBaseHeight:F3}", "Page1");
                                            break;
                                        }
                                    }
                                }
                            }

                            // 创建3D检测项目（工具名+数值模式）
                            for (int i = 0; i < actualItemCount; i++)
                            {
                                // 工具名在偶数索引，数值在奇数索引
                                int nameIndex = i * 2;
                                int valueIndex = i * 2 + 1;

                                string toolName = datas[nameIndex]?.ToString().Trim();
                                string toolValue = valueIndex < datas.Length ? (datas[valueIndex] ?? "") : "";

                                // 处理工具名：将[xx]部分替换为空字符串
                                string processedToolName = toolName;
                                if (!string.IsNullOrWhiteSpace(toolName))
                                {
                                    // 使用正则表达式移除[xx]格式的内容
                                    processedToolName = System.Text.RegularExpressions.Regex.Replace(toolName, @"\[\d+\]", "");
                                }

                                // 初始化检测项目
                                var detectionItem = new DetectionItem
                                {
                                    RowNumber = 0, // 临时行号，将在统一更新时重新分配
                                    Name = string.IsNullOrWhiteSpace(processedToolName) ? $"[3D]工具{i + 1}" : $"[3D]{processedToolName}",
                                    Value = toolValue,
                                    Is3DItem = true,
                                    ToolIndex = nameIndex // 使用工具名的索引作为标识
                                };

                                // 在应用补偿之前，保存原始的上胶点高度和下胶点高度项目的副本
                                if (processedToolName == "上胶点高度" || processedToolName == "下胶点高度")
                                {
                                    var originalCopy = new DetectionItem
                                    {
                                        RowNumber = 0,
                                        Name = $"[3D]{processedToolName}",
                                        Value = toolValue,
                                        Is3DItem = true,
                                        ToolIndex = nameIndex + 10000 // 设置很大的索引，确保不会被Update3DJudgementInfo处理
                                    };
                                    _cached3DItems.Add(originalCopy);
                                }

                                // 根据项目名应用补偿和重新设置上下限
                                ApplyCompensationAndLimits(detectionItem, processedToolName, pkgBaseHeight);

                                _cached3DItems.Add(detectionItem);
                            }
                            
                        }
                        else
                        {
                            // fallback: 传统模式，直接创建项目
                            for (int i = 0; i < Math.Min(outputTargets.Length, datas.Length); i++)
                            {
                                var detectionItem = new DetectionItem
                                {
                                    RowNumber = 0, // 临时行号，将在统一更新时重新分配
                                    Name = $"[3D]项目{i + 1}",
                                    Value = datas[i] ?? "",
                                    Is3DItem = true,
                                    ToolIndex = i
                                };
                                
                                _cached3DItems.Add(detectionItem);
                            }
                            
                        }

                        // ===== 从直接提取的G1/G2值创建检测项目（不依赖OutputTargets配置）=====
                        AddExtractedG1G2Items(pkgBaseHeight);

                        // 计算双胶点高度差（B1与B2高度差）
                        CalculateAndAddGlueHeightDifference();

                        // 计算B1-CoverRing和B2-CoverRing间距
                        CalculateAndAddCoverRingDistances();

                        // 计算G1-G2差值
                        CalculateAndAddG1G2Difference();

                        // 获取并更新3D判定信息（上下限和NG状态）
                        Update3DJudgementInfo(measureEx, _cached3DItems);
                        
                        // 不再单独存储3D
                        //Record3DDataForAnalysis(measureEx, _cached3DItems);
                        
                    }
                    else
                    {
                        LogManager.Warning($"[3D缓存] 无法从3D检测结果中获取输出数据，3D数据缓存为空", "Page1");
                    }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[3D缓存] 缓存3D检测数据时出错: {ex.Message}", "Page1");
            }
        }

        /// <summary>
        /// 根据项目名应用补偿和重新设置上下限
        /// </summary>
        /// <param name="detectionItem">检测项目</param>
        /// <param name="processedToolName">处理后的工具名</param>
        /// <param name="pkgBaseHeight">PKG底座高度值</param>
        private void ApplyCompensationAndLimits(DetectionItem detectionItem, string processedToolName, double pkgBaseHeight = 0)
        {
            try
            {
                if (TemplateConfigPage.Instance == null)
                {
                    LogManager.Warning("TemplateConfigPage.Instance为null，无法获取补偿参数", "Page1");
                    return;
                }

                // 尝试解析原始数值
                if (!double.TryParse(detectionItem.Value, out double originalValue))
                {
                    LogManager.Warning($"无法解析3D项目[{detectionItem.Name}]的数值: {detectionItem.Value}", "Page1");
                    return;
                }

                double compensatedValue = originalValue;
                double upperLimit = double.MaxValue;
                double lowerLimit = double.MinValue;
                string newName = detectionItem.Name;
                bool isCompensationApplied = false; // 标记是否应用了补偿（含自定义上下限）
                bool isValueCompensationApplied = false; // 标记是否仅应用数值补偿（上下限沿用3D判定对象）

                // 处理G1和G2项目
                if (processedToolName == "G1" || processedToolName == "G2")
                {
                    isCompensationApplied = true;
                    
                    // 先减去PKG底座高度
                    compensatedValue = originalValue - pkgBaseHeight;
                    
                    // 获取对应补偿值（优先独立补偿，未配置时回退到历史G1G2补偿）
                    string legacyG1G2CompensationStr = TemplateConfigPage.Instance.Get3DConfigParameter("G1G2补偿", "0");
                    string compensationParamName = processedToolName == "G1" ? "G1补偿" : "G2补偿";
                    string compensationStr = TemplateConfigPage.Instance.Get3DConfigParameter(compensationParamName, legacyG1G2CompensationStr);
                    if (!double.TryParse(compensationStr, out double compensation) &&
                        double.TryParse(legacyG1G2CompensationStr, out double legacyCompensation))
                    {
                        compensation = legacyCompensation;
                    }

                    compensatedValue = compensatedValue + compensation;

                    // 获取设定高度（G1和G2使用各自的设定高度）和公差
                    string heightParamName = processedToolName == "G1" ? "G1设定高度" : "G2设定高度";
                    string heightStr = TemplateConfigPage.Instance.Get3DConfigParameter(heightParamName, "100");
                    string g1g2ToleranceStr = TemplateConfigPage.Instance.Get3DConfigParameter("G1G2公差(±mm)", "20");

                    if (double.TryParse(heightStr, out double setHeight) &&
                        double.TryParse(g1g2ToleranceStr, out double g1g2Tolerance))
                    {
                        upperLimit = setHeight + g1g2Tolerance;
                        lowerLimit = setHeight - g1g2Tolerance;
                        LogManager.Info($"[3D补偿-G1G2] {processedToolName}: 设定高度({heightParamName})={setHeight:F3}, 公差={g1g2Tolerance:F3}, 计算上限={upperLimit:F3}, 计算下限={lowerLimit:F3}", "Page1");
                    }
                    else
                    {
                        LogManager.Warning($"[3D补偿-G1G2] {processedToolName}: 无法解析设定高度或公差 - heightStr='{heightStr}', g1g2ToleranceStr='{g1g2ToleranceStr}'", "Page1");
                    }
                    
                    LogManager.Verbose($"[3D补偿-G1G2] {processedToolName}: 原值={originalValue:F3}, PKG底座高度={pkgBaseHeight:F3}, 减去底座后={originalValue - pkgBaseHeight:F3}, 使用补偿={compensation:F3}, 最终补偿后={compensatedValue:F3}", "Page1");
                }
                // 处理胶点高度项目
                else if (processedToolName == "上胶点高度" || processedToolName == "下胶点高度")
                {
                    isCompensationApplied = true;
                    // 重命名项目
                    newName = processedToolName == "上胶点高度" ? "[3D]B1高度" : "[3D]B2高度";
                    
                    // 先减去PKG底座高度
                    compensatedValue = originalValue - pkgBaseHeight;
                    
                    // 获取胶点高度补偿值
                    string glueHeightCompensationStr = TemplateConfigPage.Instance.Get3DConfigParameter("胶点高度补偿", "0");
                    if (double.TryParse(glueHeightCompensationStr, out double glueHeightCompensation))
                    {
                        compensatedValue = compensatedValue + glueHeightCompensation;
                    }

                    // 获取胶点设定高度和公差
                    string glueHeightStr = TemplateConfigPage.Instance.Get3DConfigParameter("胶点设定高度", "20");
                    string glueToleranceStr = TemplateConfigPage.Instance.Get3DConfigParameter("胶点高度公差(±mm)", "20");
                    
                    if (double.TryParse(glueHeightStr, out double glueHeight) && 
                        double.TryParse(glueToleranceStr, out double glueTolerance))
                    {
                        upperLimit = glueHeight + glueTolerance;
                        lowerLimit = glueHeight - glueTolerance;
                    }
                    
                    LogManager.Verbose($"[3D补偿-胶点] {processedToolName}: 原值={originalValue:F3}, PKG底座高度={pkgBaseHeight:F3}, 减去底座后={originalValue - pkgBaseHeight:F3}, 最终补偿后={compensatedValue:F3}", "Page1");
                }
                // 处理B1/B2边缘段差项目（只做数值补偿，上下限由3D判定对象提供）
                else if (!string.IsNullOrWhiteSpace(processedToolName) &&
                    (processedToolName.Contains("B1边缘段差") || processedToolName.Contains("B2边缘段差")))
                {
                    isCompensationApplied = true;

                    string stepCompensationStr = TemplateConfigPage.Instance.Get3DConfigParameter("段差补偿", "0");
                    if (!double.TryParse(stepCompensationStr, out double stepCompensation))
                    {
                        stepCompensation = 0;
                    }

                    compensatedValue = originalValue + stepCompensation;

                    string upperLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("B1B2边缘段差上限", "0.07");
                    string lowerLimitStr = TemplateConfigPage.Instance.Get3DConfigParameter("B1B2边缘段差下限", "0");
                    if (!double.TryParse(upperLimitStr, out upperLimit))
                    {
                        upperLimit = double.MaxValue;
                    }

                    if (!double.TryParse(lowerLimitStr, out lowerLimit))
                    {
                        lowerLimit = double.MinValue;
                    }

                    detectionItem.IsManualJudgementItem = true;
                    LogManager.Verbose($"[3D补偿-段差] {processedToolName}: 原值={originalValue:F3}, 使用补偿={stepCompensation:F3}, 最终补偿后={compensatedValue:F3}, 上限={upperLimit:F3}, 下限={lowerLimit:F3}", "Page1");
                }

                // 更新检测项目
                detectionItem.Name = newName;
                detectionItem.Value = compensatedValue.ToString("F3");

                // 如果应用了补偿，设置上下限信息
                if (isCompensationApplied)
                {
                    // 进行新的上下限判定
                    bool isWithinLimits = compensatedValue >= lowerLimit && compensatedValue <= upperLimit;
                    
                    // 设置上下限信息
                    detectionItem.UpperLimit = upperLimit.ToString("F3");
                    detectionItem.LowerLimit = lowerLimit.ToString("F3");
                    detectionItem.IsOutOfRange = !isWithinLimits;
                    
                    detectionItem.IsCompensated = true;

                    // 记录处理结果
                    LogManager.Verbose($"[3D补偿] {detectionItem.Name}: 最终值={compensatedValue:F3}, 上限={upperLimit:F3}, 下限={lowerLimit:F3}, 判定={isWithinLimits}", "Page1");
                }
                else if (isValueCompensationApplied)
                {
                    // 仅数值补偿：上下限后续由Update3DJudgementInfo写入
                    detectionItem.IsValueCompensated = true;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用3D补偿和上下限时出错: {ex.Message}", "Page1");
            }
        }

        /// <summary>
        /// 计算双胶点高度差（B1与B2高度差）并添加到检测列表
        /// </summary>
        private void CalculateAndAddGlueHeightDifference()
        {
            try
            {
                // 查找B1和B2高度值
                var b1Item = _cached3DItems?.FirstOrDefault(item => item.Name.Contains("[3D]B1高度"));
                var b2Item = _cached3DItems?.FirstOrDefault(item => item.Name.Contains("[3D]B2高度"));

                if (b1Item == null || b2Item == null)
                {
                    LogManager.Verbose("[双胶点高度差] 未找到B1或B2高度数据，跳过高度差计算", "Page1");
                    return;
                }

                // 解析B1和B2的高度值
                if (!double.TryParse(b1Item.Value, out double b1Height))
                {
                    LogManager.Warning($"[双胶点高度差] 无法解析B1高度值: {b1Item.Value}", "Page1");
                    return;
                }

                if (!double.TryParse(b2Item.Value, out double b2Height))
                {
                    LogManager.Warning($"[双胶点高度差] 无法解析B2高度值: {b2Item.Value}", "Page1");
                    return;
                }

                // 计算高度差（B1 - B2），单位为mm
                double heightDifference = b1Height - b2Height;

                // 获取双胶点高度差范围（±mm）
                string toleranceStr = TemplateConfigPage.Instance?.Get3DConfigParameter("双胶点高度差范围(±mm)", "0.01");
                if (!double.TryParse(toleranceStr, out double tolerance))
                {
                    tolerance = 0.01; // 默认值
                }

                // 计算上下限（单位：mm）
                double upperLimit = tolerance;
                double lowerLimit = -tolerance;

                // 判断是否超限
                bool isOutOfRange = heightDifference < lowerLimit || heightDifference > upperLimit;

                // 创建检测项
                var heightDiffItem = new DetectionItem
                {
                    RowNumber = 0, // 临时行号，将在统一更新时重新分配
                    Name = "[3D]双胶点高度差",
                    Value = heightDifference.ToString("F3"),
                    UpperLimit = upperLimit.ToString("F3"),
                    LowerLimit = lowerLimit.ToString("F3"),
                    IsOutOfRange = isOutOfRange,
                    Is3DItem = true,
                    ToolIndex = 9000 // 设置大的索引值，确保不会被Update3DJudgementInfo处理
                };

                // 添加到缓存列表
                _cached3DItems.Add(heightDiffItem);

                LogManager.Verbose($"[双胶点高度差] B1={b1Height:F3}mm, B2={b2Height:F3}mm, 高度差={heightDifference:F3}mm, 上限={upperLimit:F3}mm, 下限={lowerLimit:F3}mm, 判定={!isOutOfRange}", "Page1");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[双胶点高度差] 计算双胶点高度差时出错: {ex.Message}", "Page1");
            }
        }

        /// <summary>
        /// 计算B1-CoverRing和B2-CoverRing间距并添加到检测列表
        /// </summary>
        private void CalculateAndAddCoverRingDistances()
        {
            try
            {
                // 查找CoverRing、B1和B2的原始高度值（3D直接输出的值）
                var coverRingItem = _cached3DItems?.FirstOrDefault(item =>
                    item.Name.Contains("CoverRing") && !item.Name.Contains("B1-") && !item.Name.Contains("B2-"));

                // 查找原始的上胶点高度和下胶点高度（未经补偿的原始值）
                var b1Item = _cached3DItems?.FirstOrDefault(item =>
                    item.Name == "[3D]上胶点高度");
                var b2Item = _cached3DItems?.FirstOrDefault(item =>
                    item.Name == "[3D]下胶点高度");

                // 如果没有CoverRing数据，直接返回
                if (coverRingItem == null)
                {
                    LogManager.Verbose("[CoverRing间距] 未找到CoverRing数据，跳过CoverRing间距计算", "Page1");
                    return;
                }

                if (b1Item == null || b2Item == null)
                {
                    LogManager.Warning("[CoverRing间距] 未找到B1或B2高度数据，无法计算CoverRing间距", "Page1");
                    return;
                }

                // 解析CoverRing高度值
                if (!double.TryParse(coverRingItem.Value, out double coverRingHeight))
                {
                    LogManager.Warning($"[CoverRing间距] 无法解析CoverRing高度值: {coverRingItem.Value}", "Page1");
                    return;
                }

                // 解析B1和B2高度值
                if (!double.TryParse(b1Item.Value, out double b1Height))
                {
                    LogManager.Warning($"[CoverRing间距] 无法解析B1高度值: {b1Item.Value}", "Page1");
                    return;
                }

                if (!double.TryParse(b2Item.Value, out double b2Height))
                {
                    LogManager.Warning($"[CoverRing间距] 无法解析B2高度值: {b2Item.Value}", "Page1");
                    return;
                }

                // 获取胶点-CoverRing补偿值
                string compensationStr = TemplateConfigPage.Instance?.Get3DConfigParameter("胶点-CoverRing补偿(±mm)", "0");
                if (!double.TryParse(compensationStr, out double compensation))
                {
                    compensation = 0; // 默认值
                }

                // 获取胶点-CoverRing最小间距（上限）
                string minDistanceStr = TemplateConfigPage.Instance?.Get3DConfigParameter("胶点-CoverRing最小间距", "0.05");
                if (!double.TryParse(minDistanceStr, out double minDistance))
                {
                    minDistance = 0.05; // 默认值
                }

                // 计算B1-CoverRing间距 = CoverRing - B1 + 补偿值
                double b1CoverRingDistance = coverRingHeight - b1Height + compensation;

                // 计算B2-CoverRing间距 = CoverRing - B2 + 补偿值
                double b2CoverRingDistance = coverRingHeight - b2Height + compensation;

                // 判断是否超限（只有下限，没有上限）
                // 当间距小于最小间距时，判定为超限（NG）
                bool b1IsOutOfRange = b1CoverRingDistance < minDistance;
                bool b2IsOutOfRange = b2CoverRingDistance < minDistance;

                // 创建B1-CoverRing检测项
                var b1CoverRingItem = new DetectionItem
                {
                    RowNumber = 0, // 临时行号，将在统一更新时重新分配
                    Name = "[3D]B1-CoverRing",
                    Value = b1CoverRingDistance.ToString("F3"),
                    UpperLimit = "", // 没有上限
                    LowerLimit = minDistance.ToString("F3"), // 最小间距作为下限
                    IsOutOfRange = b1IsOutOfRange,
                    Is3DItem = true,
                    ToolIndex = 9001 // 设置大的索引值，确保不会被Update3DJudgementInfo处理
                };

                // 创建B2-CoverRing检测项
                var b2CoverRingItem = new DetectionItem
                {
                    RowNumber = 0, // 临时行号，将在统一更新时重新分配
                    Name = "[3D]B2-CoverRing",
                    Value = b2CoverRingDistance.ToString("F3"),
                    UpperLimit = "", // 没有上限
                    LowerLimit = minDistance.ToString("F3"), // 最小间距作为下限
                    IsOutOfRange = b2IsOutOfRange,
                    Is3DItem = true,
                    ToolIndex = 9002 // 设置大的索引值，确保不会被Update3DJudgementInfo处理
                };

                // 添加到缓存列表
                _cached3DItems.Add(b1CoverRingItem);
                _cached3DItems.Add(b2CoverRingItem);

                LogManager.Verbose($"[CoverRing间距] CoverRing={coverRingHeight:F3}mm, B1原始值={b1Height:F3}mm, B2原始值={b2Height:F3}mm, 补偿={compensation:F3}mm, 上限={minDistance:F3}mm", "Page1");
                LogManager.Verbose($"[CoverRing间距] B1-CoverRing={b1CoverRingDistance:F3}mm (计算公式: {coverRingHeight:F3} - {b1Height:F3} + {compensation:F3}), 判定={!b1IsOutOfRange}", "Page1");
                LogManager.Verbose($"[CoverRing间距] B2-CoverRing={b2CoverRingDistance:F3}mm (计算公式: {coverRingHeight:F3} - {b2Height:F3} + {compensation:F3}), 判定={!b2IsOutOfRange}", "Page1");
                LogManager.Verbose($"[CoverRing间距-调试] 找到的B1项目名称: '{b1Item?.Name}', 值: '{b1Item?.Value}'", "Page1");
                LogManager.Verbose($"[CoverRing间距-调试] 找到的B2项目名称: '{b2Item?.Name}', 值: '{b2Item?.Value}'", "Page1");
                LogManager.Verbose($"[CoverRing间距-调试] 找到的CoverRing项目名称: '{coverRingItem?.Name}', 值: '{coverRingItem?.Value}'", "Page1");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[CoverRing间距] 计算CoverRing间距时出错: {ex.Message}", "Page1");
            }
        }

        /// <summary>
        /// 从直接提取的G1/G2值创建检测项目（不依赖OutputTargets配置）
        /// </summary>
        /// <param name="pkgBaseHeight">PKG底座高度</param>
        private void AddExtractedG1G2Items(double pkgBaseHeight)
        {
            try
            {
                if (_cached3DItems == null) return;

                // 检查是否已经存在G1项目（通过OutputTargets解析出来的）
                bool hasG1InCache = _cached3DItems.Any(item => item.Name.Contains("G1") && !item.Name.Contains("G1-G2"));
                // 检查是否已经存在G2项目
                bool hasG2InCache = _cached3DItems.Any(item => item.Name.Contains("G2") && !item.Name.Contains("G1-G2"));

                // 如果已经有G1，不需要从提取值创建
                if (!hasG1InCache && _hasExtractedG1)
                {
                    var g1Item = new DetectionItem
                    {
                        RowNumber = 0,
                        Name = "[3D]G1",
                        Value = _extractedG1Value.ToString("F3"),
                        Is3DItem = true,
                        ToolIndex = 8001 // 使用8000开头的索引，避免与其他工具冲突
                    };

                    // 应用补偿和上下限
                    ApplyCompensationAndLimits(g1Item, "G1", pkgBaseHeight);

                    _cached3DItems.Add(g1Item);
                    LogManager.Verbose($"[G1G2提取] 从直接提取值创建G1: 原值={_extractedG1Value:F3}, 补偿后={g1Item.Value}", "Page1");
                }

                // 如果已经有G2，不需要从提取值创建
                if (!hasG2InCache && _hasExtractedG2)
                {
                    var g2Item = new DetectionItem
                    {
                        RowNumber = 0,
                        Name = "[3D]G2",
                        Value = _extractedG2Value.ToString("F3"),
                        Is3DItem = true,
                        ToolIndex = 8002 // 使用8000开头的索引，避免与其他工具冲突
                    };

                    // 应用补偿和上下限
                    ApplyCompensationAndLimits(g2Item, "G2", pkgBaseHeight);

                    _cached3DItems.Add(g2Item);
                    LogManager.Verbose($"[G1G2提取] 从直接提取值创建G2: 原值={_extractedG2Value:F3}, 补偿后={g2Item.Value}", "Page1");
                }

                // 记录最终结果
                if (_hasExtractedG1 || _hasExtractedG2)
                {
                    LogManager.Verbose($"[G1G2提取] 提取完成: hasG1InCache={hasG1InCache}, hasG2InCache={hasG2InCache}, " +
                        $"hasExtractedG1={_hasExtractedG1}, hasExtractedG2={_hasExtractedG2}", "Page1");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[G1G2提取] 创建G1/G2检测项时出错: {ex.Message}", "Page1");
            }
        }

        /// <summary>
        /// 计算G1-G2差值并添加到检测列表
        /// </summary>
        private void CalculateAndAddG1G2Difference()
        {
            try
            {
                // 直接从缓存中查找包含G1和G2的项目
                var g1Item = _cached3DItems?.FirstOrDefault(item => item.Name.Contains("G1") && !item.Name.Contains("G1-G2"));
                var g2Item = _cached3DItems?.FirstOrDefault(item => item.Name.Contains("G2") && !item.Name.Contains("G1-G2"));

                if (g1Item == null || g2Item == null)
                {
                    LogManager.Verbose("[G1-G2差值] 未找到G1或G2高度数据，跳过G1-G2差值计算", "Page1");
                    return;
                }

                // 直接解析已有的G1和G2值
                if (!double.TryParse(g1Item.Value, out double g1Height) ||
                    !double.TryParse(g2Item.Value, out double g2Height))
                {
                    LogManager.Warning($"[G1-G2差值] 无法解析G1或G2值: G1={g1Item.Value}, G2={g2Item.Value}", "Page1");
                    return;
                }

                // 计算差值
                double g1g2Difference = g1Height - g2Height;

                // 获取上下限
                string upperLimitStr = TemplateConfigPage.Instance?.Get3DConfigParameter("G1-G2上限", "0.07");
                string lowerLimitStr = TemplateConfigPage.Instance?.Get3DConfigParameter("G1-G2下限", "0");
                double.TryParse(upperLimitStr, out double upperLimit);
                double.TryParse(lowerLimitStr, out double lowerLimit);

                LogManager.Info($"[G1-G2差值] 读取配置: 上限字符串='{upperLimitStr}', 下限字符串='{lowerLimitStr}', 解析后上限={upperLimit:F3}, 解析后下限={lowerLimit:F3}", "Page1");

                // 判断是否超限
                bool isOutOfRange = g1g2Difference < lowerLimit || g1g2Difference > upperLimit;

                // 添加到缓存列表
                _cached3DItems.Add(new DetectionItem
                {
                    RowNumber = 0,
                    Name = "[3D]G1-G2",
                    Value = g1g2Difference.ToString("F3"),
                    UpperLimit = upperLimit.ToString("F3"),
                    LowerLimit = lowerLimit.ToString("F3"),
                    IsOutOfRange = isOutOfRange,
                    Is3DItem = true,
                    IsCompensated = true, // 标记为已补偿，避免被Update3DJudgementInfo覆盖上下限
                    ToolIndex = 9003
                });

                LogManager.Verbose($"[G1-G2差值] G1={g1Height:F3}, G2={g2Height:F3}, 差值={g1g2Difference:F3}, 判定={(isOutOfRange ? "NG" : "OK")}", "Page1");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[G1-G2差值] 计算出错: {ex.Message}", "Page1");
            }
        }

                /// <summary>
        /// 统一更新DataGrid：同时应用2D和3D缓存数据，一次性更新避免分两次刷新
        /// 只在统一判定时调用，确保2D和3D数据同时显示
        /// </summary>
        public void UnifiedUpdateDataGrid()
        {
            try
            {
                //var dispatcherTimer = System.Diagnostics.Stopwatch.StartNew();

                // 🔧 关键修复：所有UI操作都必须在UI线程上执行
                Dispatcher.BeginInvoke(new Action(() => {
                    //var uiTimer = System.Diagnostics.Stopwatch.StartNew();
                    //dispatcherTimer.Stop();
                    //LogManager.Info($"[性能监控] Dispatcher调度耗时: {dispatcherTimer.ElapsedMilliseconds}ms");
                    try
                    {
                        // 🔧 性能优化：获取现有数据源，进行增量更新而不是全量重建
                        var currentItems = _dataGridItems;

                        // 构建新的检测数据列表
                        var newItems = new List<DetectionItem>();
                        int rowNumber = 1;

                        // 🔧 移除锁：先添加2D缓存数据
                        bool has2DCachedData = _cached2DItems != null && _cached2DItems.Count > 0;
                        //LogManager.Verbose($"[UnifiedUpdate] 2D缓存状态: {(has2DCachedData ? $"有{_cached2DItems.Count}项" : "无数据")}", "Page1");
                        
                        if (has2DCachedData)
                        {
                            foreach (var item in _cached2DItems)
                            {
                                // 过滤掉仅用于晶片高度计算的隐藏2D项目（不显示在Grid中）
                                if (_hidden2DItemNames.Contains(item.Name?.Trim() ?? ""))
                                {
                                    continue; // 跳过这些仅用于计算的隐藏项目
                                }

                                // 克隆2D项目并重新分配行号
                                var clonedItem = new DetectionItem
                                {
                                    RowNumber = rowNumber++,
                                    Name = item.Name,
                                    Value = item.Value,
                                    LowerLimit = item.LowerLimit,
                                    UpperLimit = item.UpperLimit,
                                    IsOutOfRange = item.IsOutOfRange,
                                    Is3DItem = item.Is3DItem,
                                    ToolIndex = item.ToolIndex
                                };
                                newItems.Add(clonedItem);
                            }
                            //LogManager.Info($"[UnifiedUpdate] 成功添加{_cached2DItems.Count}个2D检测项目", "Page1");
                        }

                        // 使用检测管理器缓存的3D启用状态
                        bool is3DEnabled = _detectionManager?.Is3DEnabled ?? false;
                        //LogManager.Verbose($"[UnifiedUpdate] 使用缓存的3D启用状态: {is3DEnabled}", "Page1");
                        if (is3DEnabled)
                        {
                            // 🔧 移除锁：直接操作3D缓存数据
                            bool has3DCachedData = _cached3DItems != null && _cached3DItems.Count > 0;
                            //LogManager.Verbose($"[UnifiedUpdate] 3D缓存状态: {(has3DCachedData ? $"有{_cached3DItems.Count}项" : "无数据")}", "Page1");
                            
                            if (has3DCachedData)
                            {
                                foreach (var item in _cached3DItems)
                                {
                                    // 过滤掉用于计算的原始值副本（不显示在Grid中）
                                    if (item.Name == "[3D]上胶点高度" || item.Name == "[3D]下胶点高度")
                                    {
                                        continue; // 跳过这些仅用于计算的原始值副本
                                    }

                                    // 克隆3D项目并重新分配行号
                                    var clonedItem = new DetectionItem
                                    {
                                        RowNumber = rowNumber++,
                                        Name = item.Name,
                                        Value = item.Value,
                                        LowerLimit = item.LowerLimit,
                                        UpperLimit = item.UpperLimit,
                                        IsOutOfRange = item.IsOutOfRange,
                                        Is3DItem = item.Is3DItem,
                                        ToolIndex = item.ToolIndex
                                    };
                                    newItems.Add(clonedItem);
                                }
                                //LogManager.Info($"[UnifiedUpdate] 成功添加{_cached3DItems.Count}个3D检测项目", "Page1");
                            }
                            else
                            {
                                LogManager.Verbose($"[UnifiedUpdate] 3D检测已启用但无缓存数据", "Page1");
                            }
                        }
                        else
                        {
                            //LogManager.Verbose($"[UnifiedUpdate] 3D检测未启用", "Page1");
                        }

                        // 🔧 新增：添加综合检测项目（需要2D和3D都完成后才能计算的项目）
                        bool hasCombinedCachedData = _cachedCombinedItems != null && _cachedCombinedItems.Count > 0;
                        if (hasCombinedCachedData)
                        {
                            foreach (var item in _cachedCombinedItems)
                            {
                                // 克隆综合项目并重新分配行号
                                var clonedItem = new DetectionItem
                                {
                                    RowNumber = rowNumber++,
                                    Name = item.Name,
                                    Value = item.Value,
                                    LowerLimit = item.LowerLimit,
                                    UpperLimit = item.UpperLimit,
                                    IsOutOfRange = item.IsOutOfRange,
                                    Is3DItem = item.Is3DItem,
                                    ToolIndex = item.ToolIndex
                                };
                                newItems.Add(clonedItem);
                            }
                            LogManager.Verbose($"[UnifiedUpdate] 成功添加{_cachedCombinedItems.Count}个综合检测项目", "Page1");
                        }

                        // 🔧 性能优化：智能增量更新，只更新变化的项目
                        bool hasChanges = false;
                        
                        // 检查项目数量是否变化
                        if (currentItems.Count != newItems.Count)
                        {
                            hasChanges = true;
                        }
                        else
                        {
                            // 逐项比较数据是否发生变化
                            for (int i = 0; i < currentItems.Count; i++)
                            {
                                var currentItem = currentItems[i];
                                var newItem = newItems[i];
                                
                                if (currentItem.Name != newItem.Name ||
                                    currentItem.Value != newItem.Value ||
                                    currentItem.LowerLimit != newItem.LowerLimit ||
                                    currentItem.UpperLimit != newItem.UpperLimit ||
                                    currentItem.IsOutOfRange != newItem.IsOutOfRange ||
                                    currentItem.Is3DItem != newItem.Is3DItem ||
                                    currentItem.ToolIndex != newItem.ToolIndex)
                                {
                                    hasChanges = true;
                                    break;
                                }
                            }
                        }

                        // 只有当数据确实发生变化时才更新UI
                        if (hasChanges)
                        {
                            //LogManager.Info($"[UnifiedUpdate] 检测到数据变化，开始增量更新DataGrid", "Page1");
                            
                            // 🔧 新增：更新完整数据列表用于显示过滤
                            _fullDataList.Clear();
                            _fullDataList.AddRange(newItems);
                            
                            // 根据当前显示模式应用过滤
                            List<DetectionItem> displayItems;
                            if (_showFocusedOnly)
                            {
                                displayItems = newItems.Where(item => _focusedProjects.Contains(item.Name)).ToList();
                                
                                // 重新设置行号
                                for (int i = 0; i < displayItems.Count; i++)
                                {
                                    displayItems[i].RowNumber = i + 1;
                                }
                                
                                LogManager.Info($"[显示过滤] 统一更新中应用关注项过滤，显示 {displayItems.Count}/{newItems.Count} 项");
                            }
                            else
                            {
                                displayItems = newItems;
                            }

                            SyncDataGridItems(displayItems);

                            // 修复：应用完整的行颜色格式化，包括重置正常项目的背景色
                            ApplyRowColorFormatting();
                        }
                        else
                        {
                            LogManager.Verbose($"[UnifiedUpdate] 数据无变化，跳过UI更新", "Page1");
                        }
                        
                        var threeDCount = newItems.Count(x => x.Is3DItem);
                        var twoDCount = newItems.Count - threeDCount;
                        //LogManager.Info($"[UnifiedUpdate] ✅ 完成统一更新 - 总计:{newItems.Count}项 (2D:{twoDCount}, 3D:{threeDCount})", "Page1");
                        
                        //uiTimer.Stop();
                        //LogManager.Info($"[性能监控] Dispatcher内UI操作总耗时: {uiTimer.ElapsedMilliseconds}ms");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"[UnifiedUpdate] 刷新DataGrid界面时出错: {ex.Message}", "Page1");
                    }
                }), DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                LogManager.Error($"[UnifiedUpdate] 统一更新DataGrid失败: {ex.Message}", "Page1");
            }
        }

        /// <summary>
        /// 记录超限项目到JSON文件
        /// </summary>
        private void RecordOutOfRangeItems(List<DetectionItem> items, string defectType)
        {
            try
            {
                // 获取当前图号
                string imageNumber = GetCurrentImageNumberForRecord();
                
                // 筛选出超限的项目
                var outOfRangeItems = items.Where(item => item.IsOutOfRange).ToList();
                
                if (outOfRangeItems.Count > 0)
                {
                    var record = new OutOfRangeRecord
                    {
                        ImageNumber = imageNumber,
                        DefectType = defectType,
                        DetectionTime = DateTime.Now,
                        OutOfRangeItems = outOfRangeItems.Select(item => new OutOfRangeItem
                        {
                            ItemName = item.Name,
                            Value = item.Value,
                            LowerLimit = item.LowerLimit,
                            UpperLimit = item.UpperLimit,
                            IsOutOfRange = item.IsOutOfRange
                        }).ToList()
                    };

                    // 保存到JSON文件
                    SaveOutOfRangeRecord(record);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"记录超限项目失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存超限记录到JSON文件 - 每个LOT号一个文件，累积方式存储
        /// </summary>
        private void SaveOutOfRangeRecord(OutOfRangeRecord record)
        {
            try
            {
                // 【修复】超限记录直接保存到LOT号文件夹，每个LOT号一个JSON文件
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string lotDir = Path.Combine(baseDir, "原图存储", CurrentLOTNumber);
                
                // 确保目录存在
                if (!Directory.Exists(lotDir))
                {
                    Directory.CreateDirectory(lotDir);
                }
                
                // JSON文件名：超限记录_{LOT号}.json
                string fileName = $"超限记录_{CurrentLOTNumber}.json";
                string filePath = Path.Combine(lotDir, fileName);
                
                // 读取现有记录（如果存在）
                List<OutOfRangeRecord> allRecords = new List<OutOfRangeRecord>();
                if (File.Exists(filePath))
                {
                    try
                    {
                        string existingContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(existingContent))
                        {
                            allRecords = Newtonsoft.Json.JsonConvert.DeserializeObject<List<OutOfRangeRecord>>(existingContent) 
                                       ?? new List<OutOfRangeRecord>();
                        }
                    }
                    catch (Exception readEx)
                    {
                        LogManager.Warning($"读取现有超限记录文件失败，将创建新文件: {readEx.Message}");
                        allRecords = new List<OutOfRangeRecord>();
                    }
                }
                
                // 添加新记录
                allRecords.Add(record);
                
                // 保存所有记录
                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(allRecords, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, jsonContent, System.Text.Encoding.UTF8);
                
                LogManager.Info($"已添加超限记录到LOT文件: {filePath} (总计 {allRecords.Count} 条记录)");
                
                // 【清理】如果存在旧的"超限记录"文件夹，提示可以手动清理
                string oldOutOfRangeDir = Path.Combine(lotDir, "超限记录");
                if (Directory.Exists(oldOutOfRangeDir))
                {
                    LogManager.Info($"检测到旧的超限记录文件夹: {oldOutOfRangeDir}，新版本已改为单文件存储，可手动清理旧文件夹");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存超限记录失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 更新3D检测项目的判定信息（上下限和NG状态）
        /// </summary>
        /// <param name="measureEx">3D检测系统实例</param>
        /// <param name="currentItems">当前DataGrid项目列表</param>
        private void Update3DJudgementInfo(LjdMeasureEx measureEx, List<DetectionItem> currentItems)
        {
            try
            {
                if (measureEx == null || currentItems == null) return;

                // 获取3D检测的所有判定对象
                if (measureEx.TryGetJudgements(out LResultJudge[] judgements) && judgements != null)
                {
                    //LogUpdate($"获取到{judgements.Length}个3D判定项目，开始更新NG状态和上下限信息");

                    // 获取OutputTargets信息用于调试匹配
                    var outputTargets = GetOutputTargets(measureEx);
                    //LogUpdate($"调试信息：OutputTargets数量={outputTargets?.Length ?? 0}, 判定对象数量={judgements.Length}");
                    
                    // 显示OutputTargets和Judgements的对应关系
                    if (outputTargets != null)
                    {
                        for (int debugIdx = 0; debugIdx < Math.Min(outputTargets.Length, judgements.Length); debugIdx++)
                        {
                            try 
                            {
                                var outputValue = outputTargets[debugIdx]?.Value?.ToString() ?? "NULL";
                                var judgeName = judgements[debugIdx]?.ResultName ?? "NULL";
                                //LogUpdate($"[调试-索引{debugIdx}] OutputTarget.Value='{outputValue}' <-> Judgement.ResultName='{judgeName}'");
                            }
                            catch (Exception debugEx)
                            {
                                LogUpdate($"[调试-索引{debugIdx}] 调试信息获取失败: {debugEx.Message}");
                            }
                        }
                    }

                    // 尝试通过索引顺序匹配（假设判定对象和OutputTargets的顺序一致）
                    // 注意：手动判定项（例如B1/B2边缘段差）不依赖“设定判定对象”，需要从索引匹配列表中排除，避免后续移除判定对象时产生错位
                    var threeDItems = currentItems
                        .Where(x => x.Is3DItem && !x.IsManualJudgementItem)
                        .OrderBy(x => x.ToolIndex)
                        .ToList();
                    
                    for (int i = 0; i < judgements.Length && i < threeDItems.Count; i++)
                    {
                        var judgement = judgements[i];
                        var detectionItem = threeDItems[i];
                                            if (judgement == null) 
                        {
                            LogUpdate($"跳过空的判定对象 (索引 {i})");
                            continue;
                        }

                        // 通过索引顺序匹配，无需名称匹配
                        if (detectionItem != null)
                        {
                            // 检查是否已经应用了补偿
                            bool isCompensated = detectionItem.IsCompensated;
                            bool isValueCompensated = detectionItem.IsValueCompensated;

                            if (isCompensated)
                            {
                                // 🔧 修复：对于补偿项目，不覆盖已设置的上下限和IsOutOfRange
                                // 补偿项目的上下限和NG判定在ApplyCompensationAndLimits等方法中已经正确设置
                                // 不再使用3D原始判定覆盖，因为3D判定使用的是未补偿的原始值

                                LogManager.Verbose($"[3D判定] 已处理补偿项目: {detectionItem.Name}, 保留自定义上下限和判定, 当前状态: {(detectionItem.IsOutOfRange ? "NG" : "OK")}", "Page1");
                            }
                            else if (isValueCompensated)
                            {
                                // 仅数值补偿项目：上下限仍取3D判定对象，但NG用补偿后的值重新判定
                                detectionItem.LowerLimit = double.IsNaN(judgement.LowLimit) ?
                                    "" : judgement.LowLimit.ToString("F3");
                                detectionItem.UpperLimit = double.IsNaN(judgement.UpLimit) ?
                                    "" : judgement.UpLimit.ToString("F3");

                                bool hasLowerLimit = !double.IsNaN(judgement.LowLimit);
                                bool hasUpperLimit = !double.IsNaN(judgement.UpLimit);

                                if (double.TryParse(detectionItem.Value, out double compensatedValue) &&
                                    (hasLowerLimit || hasUpperLimit))
                                {
                                    bool isWithinLimits = true;
                                    if (hasLowerLimit)
                                    {
                                        isWithinLimits &= compensatedValue >= judgement.LowLimit;
                                    }

                                    if (hasUpperLimit)
                                    {
                                        isWithinLimits &= compensatedValue <= judgement.UpLimit;
                                    }

                                    detectionItem.IsOutOfRange = !isWithinLimits;
                                }
                                else
                                {
                                    // 无法获取上下限或解析失败时，回退为3D判定结果
                                    detectionItem.IsOutOfRange = !judgement.IsJudgeOK;
                                }

                                LogManager.Verbose($"[3D判定] 已处理数值补偿项目: {detectionItem.Name}, 状态: {(detectionItem.IsOutOfRange ? "NG" : "OK")}", "Page1");
                            }
                            else
                            {
                                // 🔧 修复：对于非补偿项目，设置上下限信息和NG状态
                                detectionItem.LowerLimit = double.IsNaN(judgement.LowLimit) ?
                                    "" : judgement.LowLimit.ToString("F3");
                                detectionItem.UpperLimit = double.IsNaN(judgement.UpLimit) ?
                                    "" : judgement.UpLimit.ToString("F3");
                                detectionItem.IsOutOfRange = !judgement.IsJudgeOK;
                            }

                            // 记录匹配成功的信息（基于索引匹配）
                            //LogUpdate($"[3D判定匹配-索引{i}] '{judgement.ResultName}' ↔ '{detectionItem.Name}' " +
                            //         $"结果: {(judgement.IsJudgeOK ? "OK" : "NG")}");

                            // 如果是NG，记录详细信息（包括补偿项目）
                            if (!judgement.IsJudgeOK)
                            {
                                string currentValue = judgement.Value?.ToString() ?? "N/A";
                                string itemType = (isCompensated || isValueCompensated) ? "补偿项目" : "标准项目";
                                LogUpdate($"[3D-NG-{itemType}] {judgement.ResultName}: 当前值={currentValue}, " +
                                         $"下限={detectionItem.LowerLimit}, 上限={detectionItem.UpperLimit}");
                            }
                        }
                        else
                        {
                            LogUpdate($"⚠️ 索引 {i} 对应的DataGrid项目为空，跳过判定对象: {judgement.ResultName ?? "Unknown"}");
                        }
                    }
                    
                    // 如果数量不匹配，记录警告
                    if (judgements.Length != threeDItems.Count)
                    {
                        LogUpdate($"⚠️ 判定对象数量({judgements.Length})与DataGrid 3D项目数量({threeDItems.Count})不匹配");
                    }

                    // 检查并设置DataGrid的红色显示事件
                    EnsureDataGridRedDisplaySetup();
                }
                else
                {
                    LogUpdate("无法获取3D判定信息，请检查3D检测系统的判定设置");
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"更新3D判定信息时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保DataGrid1设置了红色显示事件处理
        /// </summary>
        private void EnsureDataGridRedDisplaySetup()
        {
            try
            {
                // 移除之前的事件处理（避免重复绑定）
                DataGrid1.LoadingRow -= DataGrid1_LoadingRow;
                // 添加事件处理
                DataGrid1.LoadingRow += DataGrid1_LoadingRow;
            }
            catch (Exception ex)
            {
                LogUpdate($"设置DataGrid红色显示事件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// DataGrid1行加载事件处理 - 设置NG项目的LightCoral背景，空值的黄色背景
        /// </summary>
        private void DataGrid1_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                if (e.Row.Item is DetectionItem item)
                {
                    // 检查值是否为空（null、空字符串或仅包含空白字符）
                    bool isEmpty = string.IsNullOrWhiteSpace(item.Value);
                    
                    if (isEmpty)
                    {
                        // 设置为黄色背景（空值）
                        e.Row.Background = new SolidColorBrush(System.Windows.Media.Colors.LightYellow);
                    }
                    else if (item.IsOutOfRange)
                    {
                        // 设置为LightCoral背景（超出范围）
                        e.Row.Background = new SolidColorBrush(System.Windows.Media.Colors.LightCoral);
                    }
                    else
                    {
                        // 正常项目设置为白色背景
                        e.Row.Background = new SolidColorBrush(System.Windows.Media.Colors.White);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"设置DataGrid行背景色时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 从输出目标获取数据（实现TryGetOutputDatas的功能）
        /// </summary>
        /// <param name="result">检测结果</param>
        /// <param name="targets">输出目标</param>
        /// <returns>输出数据数组</returns>
        private string[] GetOutputDatasFromTargets(LjdExecuteResult result, LOutputTarget[] targets)
        {
            try
            {
                if (result?.Results == null || targets == null) return null;

                string[] datas = new string[targets.Length];
                for (int i = 0; i < targets.Length; i++)
                {
                    // 调用OutputTarget的UpdateData方法更新数据
                    targets[i].UpdateData(result.Results);
                    // 获取值并转换为字符串
                    datas[i] = targets[i].Value?.ToString() ?? "";
                }
                return datas;
            }
            catch (Exception ex)
            {
                LogUpdate($"获取输出数据时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取3D检测系统的输出目标设置
        /// </summary>
        /// <param name="measureEx">3D检测系统实例</param>
        /// <returns>输出目标数组</returns>
        private LOutputTarget[] GetOutputTargets(LjdMeasureEx measureEx)
        {
            try
            {
                if (measureEx == null) return null;

                // 通过反射获取私有字段OutputTargets
                var outputTargetsField = measureEx.GetType().GetField("OutputTargets", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (outputTargetsField != null)
                {
                    return outputTargetsField.GetValue(measureEx) as LOutputTarget[];
                }
                
                // 如果无法通过反射获取，记录错误信息
                LogUpdate("无法获取OutputTargets字段，请检查LjdMeasureEx类结构");
                return null;
            }
            catch (Exception ex)
            {
                LogUpdate($"获取OutputTargets时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 初始化3D检测项目到DataGrid（兼容性方法，建议使用Initialize3DDetectionItemsFromOutputTargets）
        /// </summary>
        /// <param name="toolInfos">3D检测工具信息列表</param>
        public void Initialize3DDetectionItems(IList<ILToolInfo> toolInfos)
        {
            LogUpdate("建议优先使用Initialize3DDetectionItemsFromOutputTargets进行3D检测项目初始化");

            if (toolInfos == null || toolInfos.Count == 0)
            {
                LogUpdate("工具信息为空，无法初始化3D检测项目");
                return;
            }

            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var currentItems = _dataGridItems.Where(item => !item.Is3DItem).ToList();

                        foreach (var item in currentItems)
                        {
                            if (!item.Is3DItem)
                            {
                                item.ToolIndex = -1;
                            }
                        }

                        int nextRowNumber = currentItems.Count + 1;
                        foreach (var toolInfo in toolInfos)
                        {
                            var displayText = toolInfo.DisplayText();
                            string itemName = ExtractItemName(displayText);

                            if (!string.IsNullOrWhiteSpace(itemName))
                            {
                                currentItems.Add(new DetectionItem
                                {
                                    RowNumber = nextRowNumber++,
                                    Name = $"[3D]{itemName}",
                                    Value = string.Empty,
                                    Is3DItem = true,
                                    ToolIndex = GetToolIndex(toolInfo)
                                });
                            }
                        }

                        _fullDataList = currentItems.ToList();
                        if (_showFocusedOnly)
                        {
                            var filteredItems = currentItems.Where(item => _focusedProjects.Contains(item.Name)).ToList();
                            SyncDataGridItems(filteredItems);
                        }
                        else
                        {
                            SyncDataGridItems(currentItems);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUpdate($"初始化3D检测项目时出错: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                LogUpdate($"调度3D检测项目初始化时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 从显示文本中提取项目名称
        /// </summary>
        /// <param name="displayText">显示文本</param>
        /// <returns>项目名称</returns>
        private string ExtractItemName(string displayText)
        {
            if (string.IsNullOrWhiteSpace(displayText)) return "";
            
            // 提取冒号前的部分作为项目名称
            var parts = displayText.Split(':');
            if (parts.Length > 0)
            {
                return parts[0].Trim();
            }
            
            return displayText.Trim();
        }

        /// <summary>
        /// 获取工具索引号
        /// </summary>
        /// <param name="toolInfo">工具信息</param>
        /// <returns>工具索引</returns>
        private int GetToolIndex(ILToolInfo toolInfo)
        {
            try
            {
                var displayText = toolInfo.DisplayText();
                
                // 从显示文本中提取工具索引（假设格式为 [000]、[001]等）
                if (displayText.StartsWith("[") && displayText.Contains("]"))
                {
                    var indexText = displayText.Substring(1, displayText.IndexOf("]") - 1);
                    if (int.TryParse(indexText, out int index))
                    {
                        return index;
                    }
                }
                
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        #endif
        /// <summary>
        /// 初始化图片检测卡片
        /// </summary>
        private void InitializeImageTestCard()
        {
            try
            {
                // 在C#中创建闪烁动画
                CreateFlashAnimation();

                // 初始化UI状态
                UpdateImageTestCardUI();
                
                // LogUpdate("图片检测卡片初始化完成"); // 客户日志：技术细节不显示
            }
            catch (Exception ex)
            {
                LogUpdate($"初始化图片检测卡片失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建闪烁动画
        /// </summary>
        private void CreateFlashAnimation()
        {
            try
            {
                // 创建颜色动画
                var colorAnimation = new ColorAnimation
                {
                    From = System.Windows.Media.Color.FromRgb(45, 62, 80), // 原始颜色 #FF2D3E50
                    To = System.Windows.Media.Color.FromRgb(52, 152, 219), // 闪烁颜色 #FF3498DB
                    Duration = TimeSpan.FromSeconds(1),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };

                // 创建故事板
                _flashStoryboard = new Storyboard();
                _flashStoryboard.Children.Add(colorAnimation);

                // 设置动画目标
                Storyboard.SetTarget(colorAnimation, ImageTestCard);
                Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("(Border.Background).(SolidColorBrush.Color)"));

                // LogUpdate("闪烁动画创建成功"); // 客户日志：技术细节不显示
            }
            catch (Exception ex)
            {
                LogUpdate($"创建闪烁动画失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新图片检测卡片的UI状态
        /// </summary>
        private void UpdateImageTestCardUI()
        {
            try
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    switch (_imageTestManager.CurrentState)
                    {
                        case ImageTestState.Idle:
                            // 空闲状态：只显示中间按钮（带空检查）
                            if (FirstImageButton != null)
                                FirstImageButton.Visibility = Visibility.Collapsed;
                            if (PreviousImageButton != null)
                                PreviousImageButton.Visibility = Visibility.Collapsed;
                            if (NextImageButton != null)
                                NextImageButton.Visibility = Visibility.Collapsed;
                            if (LastImageButton != null)
                                LastImageButton.Visibility = Visibility.Collapsed;
                            if (PauseResumeButtonBorder != null)
                                PauseResumeButtonBorder.Visibility = Visibility.Collapsed;
                            if (MarkButtonBorder != null)
                                MarkButtonBorder.Visibility = Visibility.Collapsed;
                            
                            // 同时控制Border的Visibility（带空检查）
                            if (FirstImageButtonBorder != null)
                                FirstImageButtonBorder.Visibility = Visibility.Collapsed;
                            if (PreviousImageButtonBorder != null)
                                PreviousImageButtonBorder.Visibility = Visibility.Collapsed;
                            if (NextImageButtonBorder != null)
                                NextImageButtonBorder.Visibility = Visibility.Collapsed;
                            if (LastImageButtonBorder != null)
                                LastImageButtonBorder.Visibility = Visibility.Collapsed;
                            
                            if (MainImageTestButton != null)
                            {
                                MainImageTestButton.Content = "图片检测";
                                MainImageTestButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)); // 绿色
                            }
                            
                            // 停止闪烁
                            if (_flashStoryboard != null)
                            {
                                _flashStoryboard.Stop();
                                if (ImageTestCard != null)
                                    ImageTestCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 62, 80)); // 恢复原色
                            }
                            break;

                        case ImageTestState.Testing:
                            // 检测状态：显示所有按钮，开始闪烁（带空检查）
                            if (FirstImageButton != null)
                                FirstImageButton.Visibility = Visibility.Visible;
                            if (PreviousImageButton != null)
                                PreviousImageButton.Visibility = Visibility.Visible;
                            if (NextImageButton != null)
                                NextImageButton.Visibility = Visibility.Visible;
                            if (LastImageButton != null)
                                LastImageButton.Visibility = Visibility.Visible;
                            if (PauseResumeButtonBorder != null)
                                PauseResumeButtonBorder.Visibility = Visibility.Visible;
                            if (MarkButtonBorder != null)
                                MarkButtonBorder.Visibility = Visibility.Visible;
                            
                            // 同时控制Border的Visibility（带空检查）
                            if (FirstImageButtonBorder != null)
                                FirstImageButtonBorder.Visibility = Visibility.Visible;
                            if (PreviousImageButtonBorder != null)
                                PreviousImageButtonBorder.Visibility = Visibility.Visible;
                            if (NextImageButtonBorder != null)
                                NextImageButtonBorder.Visibility = Visibility.Visible;
                            if (LastImageButtonBorder != null)
                                LastImageButtonBorder.Visibility = Visibility.Visible;
                            
                            // 根据连续检测模式设置主按钮文字
                            if (MainImageTestButton != null)
                            {
                                switch (_imageTestManager.AutoDetectionMode)
                                {
                                    case AutoDetectionMode.ToFirst:
                                        MainImageTestButton.Content = "停止反向连续检测";
                                        break;
                                    case AutoDetectionMode.ToLast:
                                        MainImageTestButton.Content = "停止正向连续检测";
                                        break;
                                    default:
                                        MainImageTestButton.Content = "结束检测";
                                        break;
                                }
                                MainImageTestButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0)); // 红色
                            }
                            
                            // 设置暂停按钮（保持固定的暂停符号）
                            if (PauseResumeButton != null)
                            {
                                PauseResumeButton.Content = "⏸️";
                                PauseResumeButton.ToolTip = "暂停检测";
                            }
                            
                            // 🔧 测试模式：更新记录按钮状态
                            if (_isTestModeActive)
                            {
                                UpdateMarkButtonStatus();
                            }
                            
                            // 按钮启用状态：连续检测时禁用单步按钮，只能停止
                            bool hasImages = _imageTestManager.ImageGroups.Count > 0;
                            bool isAutoDetecting = _imageTestManager.AutoDetectionMode != AutoDetectionMode.None;
                            
                            // 按钮启用状态设置（带空检查）
                            if (FirstImageButton != null)
                                FirstImageButton.IsEnabled = hasImages && !isAutoDetecting;
                            if (PreviousImageButton != null)
                                PreviousImageButton.IsEnabled = hasImages && !isAutoDetecting;
                            if (NextImageButton != null)
                                NextImageButton.IsEnabled = hasImages && !isAutoDetecting;
                            if (LastImageButton != null)
                                LastImageButton.IsEnabled = hasImages && !isAutoDetecting;
                            if (PauseResumeButton != null)
                                PauseResumeButton.IsEnabled = true;
                            
                            // 开始闪烁
                            if (_flashStoryboard != null)
                            {
                                _flashStoryboard.Begin();
                            }
                            
                            // 更新当前组信息
                            var currentGroup = _imageTestManager.CurrentGroup;
                            if (currentGroup != null)
                            {
                                string detectionMode = "";
                                switch (_imageTestManager.AutoDetectionMode)
                                {
                                    case AutoDetectionMode.ToFirst:
                                        detectionMode = " [反向连续检测中]";
                                        break;
                                    case AutoDetectionMode.ToLast:
                                        detectionMode = " [正向连续检测中]";
                                        break;
                                }
                                // LogUpdate($"当前检测组: {currentGroup.BaseName} ({_imageTestManager.CurrentIndex + 1}/{_imageTestManager.ImageGroups.Count}){detectionMode}"); // 客户日志：检测详情不显示
                            }
                            break;

                        case ImageTestState.Paused:
                            // 暂停状态：显示所有按钮，停止闪烁，但保持检测界面
                            if (FirstImageButton != null)
                                FirstImageButton.Visibility = Visibility.Visible;
                            if (PreviousImageButton != null)
                                PreviousImageButton.Visibility = Visibility.Visible;
                            if (NextImageButton != null)
                                NextImageButton.Visibility = Visibility.Visible;
                            if (LastImageButton != null)
                                LastImageButton.Visibility = Visibility.Visible;
                            if (PauseResumeButtonBorder != null)
                                PauseResumeButtonBorder.Visibility = Visibility.Visible;
                            if (MarkButtonBorder != null)
                                MarkButtonBorder.Visibility = Visibility.Visible;
                            
                            // 同时控制Border的Visibility（带空检查）
                            if (FirstImageButtonBorder != null)
                                FirstImageButtonBorder.Visibility = Visibility.Visible;
                            if (PreviousImageButtonBorder != null)
                                PreviousImageButtonBorder.Visibility = Visibility.Visible;
                            if (NextImageButtonBorder != null)
                                NextImageButtonBorder.Visibility = Visibility.Visible;
                            if (LastImageButtonBorder != null)
                                LastImageButtonBorder.Visibility = Visibility.Visible;
                            
                            // 暂停状态：主按钮显示"结束检测"
                            if (MainImageTestButton != null)
                            {
                                MainImageTestButton.Content = "结束检测";
                                MainImageTestButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0)); // 红色
                            }
                            
                            // 设置暂停按钮（保持固定的暂停符号）
                            if (PauseResumeButton != null)
                            {
                                PauseResumeButton.Content = "⏸️";
                                PauseResumeButton.ToolTip = "恢复检测";
                            }
                            
                            // 🔧 测试模式：更新记录按钮状态
                            if (_isTestModeActive)
                            {
                                UpdateMarkButtonStatus();
                            }
                            
                            // 暂停状态：启用所有导航按钮
                            bool hasImagesPaused = _imageTestManager.ImageGroups.Count > 0;
                            
                            if (FirstImageButton != null)
                                FirstImageButton.IsEnabled = hasImagesPaused;
                            if (PreviousImageButton != null)
                                PreviousImageButton.IsEnabled = hasImagesPaused;
                            if (NextImageButton != null)
                                NextImageButton.IsEnabled = hasImagesPaused;
                            if (LastImageButton != null)
                                LastImageButton.IsEnabled = hasImagesPaused;
                            if (PauseResumeButton != null)
                                PauseResumeButton.IsEnabled = true;
                            
                            // 停止闪烁，但保持暂停状态的背景色
                            if (_flashStoryboard != null)
                            {
                                _flashStoryboard.Stop();
                                if (ImageTestCard != null)
                                    ImageTestCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 156, 18)); // 橙色表示暂停
                            }
                            break;
                    }
                }));
            }
            catch (Exception ex)
            {
                LogUpdate($"更新图片检测卡片UI失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 主图片检测按钮点击事件
        /// </summary>
        private async void MainImageTestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_imageTestManager.CurrentState == ImageTestState.Idle)
                {
                    // 开始图片检测 - 显示模式选择窗口
                    await StartImageTestingWithModeSelection();
                }
                else if (_imageTestManager.CurrentState == ImageTestState.Testing || 
                         _imageTestManager.CurrentState == ImageTestState.Paused)
                {
                    // 结束图片检测
                    StopImageTesting();
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"图片检测按钮操作失败: {ex.Message}");
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 上一组按钮点击事件
        /// </summary>
        private async void PreviousImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_imageTestManager.MovePrevious())
                {
                    // LogUpdate($"切换到上一组，当前索引: {_imageTestManager.CurrentIndex}"); // 客户日志：检测索引不显示
                    
                    // 如果处于暂停状态，执行检测后保持暂停状态
                    bool wasPaused = _imageTestManager.CurrentState == ImageTestState.Paused;
                    
                    // 临时切换到检测状态以执行检测
                    if (wasPaused)
                    {
                        _imageTestManager.SetState(ImageTestState.Testing);
                    }
                    
                    await ExecuteCurrentImageGroup();
                    
                    // 如果之前是暂停状态，恢复暂停状态
                    if (wasPaused)
                    {
                        _imageTestManager.SetState(ImageTestState.Paused);
                    }
                    
                    // 检测完成后重新更新UI状态
                    UpdateImageTestCardUI();
                }
                else
                {
                    LogUpdate("无法移动到上一组");
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"切换到上一组图片失败: {ex.Message}");
                UpdateImageTestCardUI();
            }
        }

        /// <summary>
        /// 下一组按钮点击事件
        /// </summary>
        private async void NextImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_imageTestManager.MoveNext())
                {
                    // LogUpdate($"切换到下一组，当前索引: {_imageTestManager.CurrentIndex}"); // 客户日志：检测索引不显示
                    
                    // 如果处于暂停状态，执行检测后保持暂停状态
                    bool wasPaused = _imageTestManager.CurrentState == ImageTestState.Paused;
                    
                    // 临时切换到检测状态以执行检测
                    if (wasPaused)
                    {
                        _imageTestManager.SetState(ImageTestState.Testing);
                    }
                    
                    await ExecuteCurrentImageGroup();
                    
                    // 如果之前是暂停状态，恢复暂停状态
                    if (wasPaused)
                    {
                        _imageTestManager.SetState(ImageTestState.Paused);
                    }
                    
                    // 检测完成后重新更新UI状态
                    UpdateImageTestCardUI();
                }
                else
                {
                    LogUpdate("无法移动到下一组");
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"切换到下一组图片失败: {ex.Message}");
                UpdateImageTestCardUI();
            }
        }

        /// <summary>
        /// 反向连续检测到第一组按钮点击事件
        /// </summary>
        private async void FirstImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_imageTestManager.ImageGroups.Count == 0)
                {
                    LogUpdate("无图片组可执行反向连续检测");
                    return;
                }

                // 设置反向自动检测模式
                _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.ToFirst);
                
                // 从暂停状态恢复到检测状态以开始连续检测
                if (_imageTestManager.CurrentState == ImageTestState.Paused)
                {
                    _imageTestManager.SetState(ImageTestState.Testing);
                }
                
                LogUpdate("开始反向连续检测"); // 客户日志：简化为关键操作信息
                
                // 🔧 关键修复：直接移动到上一组开始连续检测，避免重复执行当前组
                if (_imageTestManager.CurrentIndex > 0)
                {
                    // 移动到上一组
                    if (_imageTestManager.MovePrevious())
                    {
                        LogUpdate($"连续检测开始: 移动到第{_imageTestManager.CurrentIndex + 1}组");
                        
                        // 更新UI状态
                        UpdateImageTestCardUI();
                        
                        // 执行上一组的检测，后续由算法回调自动继续
                        await ExecuteCurrentImageGroup();
                    }
                    else
                    {
                        LogUpdate("无法移动到上一组");
                        _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                    }
                }
                else
                {
                    // 已经是第一组，无需连续检测
                    LogUpdate("当前已是第一组，无需连续检测");
                    _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                }
                
                // 检测完成后重新更新UI状态
                UpdateImageTestCardUI();
            }
            catch (Exception ex)
            {
                _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                LogUpdate($"反向连续检测失败: {ex.Message}");
                UpdateImageTestCardUI();
            }
        }

        /// <summary>
        /// 正向连续检测到最后一组按钮点击事件
        /// </summary>
        private async void LastImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_imageTestManager.ImageGroups.Count == 0)
                {
                    LogUpdate("无图片组可执行正向连续检测");
                    return;
                }

                // 设置正向自动检测模式
                _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.ToLast);
                
                // 从暂停状态恢复到检测状态以开始连续检测
                if (_imageTestManager.CurrentState == ImageTestState.Paused)
                {
                    _imageTestManager.SetState(ImageTestState.Testing);
                }
                
                LogUpdate("开始正向连续检测"); // 客户日志：简化为关键操作信息
                
                // 🔧 关键修复：直接移动到下一组开始连续检测，避免重复执行当前组
                if (_imageTestManager.CurrentIndex < _imageTestManager.ImageGroups.Count - 1)
                {
                    // 移动到下一组
                    if (_imageTestManager.MoveNext())
                    {
                        LogUpdate($"连续检测开始: 移动到第{_imageTestManager.CurrentIndex + 1}组");
                        
                        // 更新UI状态
                        UpdateImageTestCardUI();
                        
                        // 执行下一组的检测，后续由算法回调自动继续
                        await ExecuteCurrentImageGroup();
                    }
                    else
                    {
                        LogUpdate("无法移动到下一组");
                        _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                    }
                }
                else
                {
                    // 已经是最后一组，无需连续检测
                    LogUpdate("当前已是最后一组，无需连续检测");
                    _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                }
                
                // 检测完成后重新更新UI状态
                UpdateImageTestCardUI();
            }
            catch (Exception ex)
            {
                _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                LogUpdate($"正向连续检测失败: {ex.Message}");
                UpdateImageTestCardUI();
            }
        }

        /// <summary>
        /// 开始图片检测（带模式选择）
        /// </summary>
        private async Task StartImageTestingWithModeSelection()
        {
            try
            {
                // 获取当前NG数量
                int currentNGCount = GetCurrentNGCount();
                LogManager.Info($"准备创建ImageTestModeSelectionWindow，LOT: {CurrentLotValue}, NG数量: {currentNGCount}");
                
                // 显示模式选择窗口
                var modeSelectionWindow = new ImageTestModeSelectionWindow(CurrentLotValue, currentNGCount);
                modeSelectionWindow.Owner = Application.Current.MainWindow;
                
                if (modeSelectionWindow.ShowDialog() == true)
                {
                    switch (modeSelectionWindow.SelectedMode)
                    {
                        case ImageTestModeSelectionWindow.TestMode.CategoryMode:
                            // 按类别查找（原有功能）
                            await StartImageTesting();
                            break;

                        case ImageTestModeSelectionWindow.TestMode.NGNumberMode:
                            // 按NG编号查找
                            await StartNGImageTesting(modeSelectionWindow.NGImageGroups);
                            break;

                        case ImageTestModeSelectionWindow.TestMode.ValidatorMachineDetection:
                            // 验机图片检测 - 使用验机图片集文件夹
                            await StartValidatorMachineDetectionMode(
                                modeSelectionWindow.ValidatorMachineFolderPath,
                                modeSelectionWindow.ValidatorMachineSampleCount,
                                modeSelectionWindow.ValidatorMachineLoopCycle);
                            break;

                        case ImageTestModeSelectionWindow.TestMode.SingleSampleDynamicStaticDetection:
                            await StartSingleSampleDynamicStaticDetectionMode(modeSelectionWindow.SingleSampleDynamicStaticFolderPath);
                            break;
                        
                        case ImageTestModeSelectionWindow.TestMode.CicdImageSetTest:
                            await StartCicdImageSetTestMode(modeSelectionWindow.CicdImageSetName);
                            break;

                        case ImageTestModeSelectionWindow.TestMode.ValidatorMachineCollection:
                            // 验机图片集制作
                            await StartValidatorMachineCollectionMode(modeSelectionWindow.ValidatorMachineFolderPath, modeSelectionWindow.ValidatorMachineSampleCount);
                            break;

                        case ImageTestModeSelectionWindow.TestMode.SingleSampleDynamicStaticCollection:
                            await StartSingleSampleDynamicStaticCollectionMode(modeSelectionWindow.SingleSampleDynamicStaticFolderPath);
                            break;

                        case ImageTestModeSelectionWindow.TestMode.CicdImageSetCollection:
                            await StartCicdImageSetCollectionMode(modeSelectionWindow.CicdCollectionSourceFiles);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"图片检测模式选择失败: {ex.Message}");
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取当前界面显示的NG数量
        /// </summary>
        private int GetCurrentNGCount()
        {
            try
            {
                // 尝试从TemplateConfigPage实例获取NG数量
                if (TemplateConfigPage.Instance != null)
                {
                    int ngCount = TemplateConfigPage.Instance.GetCurrentNGCount();
                    LogManager.Info($"获取到当前NG数量: {ngCount}");
                    return ngCount;
                }
                
                LogManager.Warning("TemplateConfigPage实例未找到，返回默认NG数量0");
                return 0;
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取当前NG数量失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 开始NG图片检测
        /// </summary>
        private async Task StartNGImageTesting(List<ImageGroupSet> ngImageGroups)
        {
            try
            {
                if (ngImageGroups == null || ngImageGroups.Count == 0)
                {
                    LogUpdate("未找到有效的NG图片组");
                    return;
                }

                LogUpdate($"开始NG图片检测，共 {ngImageGroups.Count} 组图片");

                if (!_isTestModeActive)
                {
                    _testModeDataManager = new TestModeDataManager();
                    _testModeDataManager.StartTestMode();
                    _isTestModeActive = true;
                    LogManager.Info("[测试模式] NG图片检测已启动，数据管理器已激活");
                }

                // 设置图片组到测试管理器
                _imageTestManager.SetImageGroups(ngImageGroups);
                _imageTestManager.SetState(ImageTestState.Testing);

                // 更新UI状态
                UpdateImageTestCardUI();

                // 开始执行第一组图片检测
                await ExecuteCurrentImageGroup();
            }
            catch (Exception ex)
            {
                LogUpdate($"NG图片检测启动失败: {ex.Message}");
                MessageBox.Show($"NG图片检测启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 开始模板图片检测
        /// </summary>
        private async Task StartTemplateImageTesting(List<ImageGroupSet> templateImageGroups, string modeName)
        {
            try
            {
                if (templateImageGroups == null || templateImageGroups.Count == 0)
                {
                    LogUpdate($"未找到有效的{modeName}图片组");
                    return;
                }

                LogUpdate($"开始{modeName}，共 {templateImageGroups.Count} 组图片");

                // 🔧 新增：启动测试模式数据管理
                if (!_isTestModeActive)
                {
                    _testModeDataManager = new TestModeDataManager();
                    _testModeDataManager.StartTestMode();
                    _isTestModeActive = true;
                    LogManager.Info($"[测试模式] {modeName}已启动，数据管理器已激活");
                }

                // 设置图片组到测试管理器
                _imageTestManager.SetImageGroups(templateImageGroups);
                _imageTestManager.SetState(ImageTestState.Testing);

                // 更新UI状态
                UpdateImageTestCardUI();

                // 开始执行第一组图片检测
                await ExecuteCurrentImageGroup();
            }
            catch (Exception ex)
            {
                LogUpdate($"{modeName}启动失败: {ex.Message}");
                MessageBox.Show($"{modeName}启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 开始图集制作模式
        /// </summary>
        private async Task StartImageCollectionMode(string categoryName, string modeName)
        {
            try
            {
                LogUpdate($"开始{modeName}");

                // 选择图片文件
                var imageGroups = await SelectImageFilesAsync();
                if (imageGroups == null || imageGroups.Count == 0)
                {
                    LogUpdate("未选择有效的图片组");
                    return;
                }

                // 保存图片到模板目录
                var savedImageGroups = await SaveImagesToTemplateDirectory(imageGroups, categoryName, modeName);
                if (savedImageGroups == null || savedImageGroups.Count == 0)
                {
                    LogUpdate($"{modeName}失败：图片保存失败");
                    return;
                }

                LogUpdate($"{modeName}完成，图片已保存到模板目录，现在开始测试");

                // 🔧 新增：启动测试模式数据管理
                if (!_isTestModeActive)
                {
                    _testModeDataManager = new TestModeDataManager();
                    _testModeDataManager.StartTestMode();
                    _isTestModeActive = true;
                    LogManager.Info($"[测试模式] {modeName}已启动，数据管理器已激活");
                }

                // 设置图片组到测试管理器
                _imageTestManager.SetImageGroups(savedImageGroups);
                _imageTestManager.SetState(ImageTestState.Testing);

                // 更新UI状态
                UpdateImageTestCardUI();

                // 开始执行第一组图片检测
                await ExecuteCurrentImageGroup();
            }
            catch (Exception ex)
            {
                LogUpdate($"{modeName}失败: {ex.Message}");
                MessageBox.Show($"{modeName}失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存图片到模板目录
        /// </summary>
        private async Task<List<ImageGroupSet>> SaveImagesToTemplateDirectory(List<ImageGroupSet> imageGroups, string categoryName, string modeName)
        {
            try
            {
                // 构建模板目录路径
                string templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", CurrentTemplateName, categoryName);
                
                // 创建主目录
                if (!Directory.Exists(templateDir))
                {
                    Directory.CreateDirectory(templateDir);
                    LogManager.Info($"创建模板目录: {templateDir}");
                }

                // 创建子目录结构（动态数量）
                int requiredSources = GetRequired2DSourceCount();
                var sourceDirs = new List<string>();
                for (int i = 0; i < requiredSources; i++)
                {
                    string sourceDir = Path.Combine(templateDir, GetPreferredSourceFolderName(i));
                    sourceDirs.Add(sourceDir);
                }
                string threeDDir = Path.Combine(templateDir, "3D");

                // 创建必需的2D目录
                foreach (var sourceDir in sourceDirs)
                {
                    Directory.CreateDirectory(sourceDir);
                }

                // 检查是否需要创建3D目录
                bool needs3DDir = imageGroups.Any(g => g.Has3DImages);
                if (needs3DDir)
                {
                    Directory.CreateDirectory(threeDDir);
                    LogManager.Info("创建3D目录，用于保存高度图和灰度图");
                }

                var savedImageGroups = new List<ImageGroupSet>();

                // 显示进度对话框
                var progressDialog = new LoadingDialog($"正在保存图片到{categoryName}目录...");
                progressDialog.Owner = Application.Current.MainWindow;
                progressDialog.Show();

                try
                {
                    await Task.Delay(100); // 让对话框显示

                    foreach (var imageGroup in imageGroups)
                    {
                        var savedGroup = new ImageGroupSet
                        {
                            BaseName = imageGroup.BaseName
                        };

                        // 复制2D图片文件到对应子目录
                        if (!string.IsNullOrEmpty(imageGroup.Source1Path))
                        {
                            savedGroup.Source1Path = await CopyImageFileToSubDirectory(imageGroup.Source1Path, sourceDirs[0], imageGroup.BaseName);
                        }
                        for (int i = 1; i < requiredSources; i++)
                        {
                            var sourcePath = imageGroup.GetPath(i);
                            if (!string.IsNullOrEmpty(sourcePath))
                            {
                                var savedPath = await CopyImageFileToSubDirectory(sourcePath, sourceDirs[i], imageGroup.BaseName);
                                savedGroup.SetSource(i, savedPath);
                            }
                        }

                        // 复制3D图片文件到3D子目录
                        if (!string.IsNullOrEmpty(imageGroup.HeightImagePath))
                        {
                            savedGroup.HeightImagePath = await CopyImageFileToSubDirectory(imageGroup.HeightImagePath, threeDDir, $"height_{imageGroup.BaseName}");
                        }
                        if (!string.IsNullOrEmpty(imageGroup.GrayImagePath))
                        {
                            savedGroup.GrayImagePath = await CopyImageFileToSubDirectory(imageGroup.GrayImagePath, threeDDir, $"gray_{imageGroup.BaseName}");
                        }

                        // 验证图片组是否有效
                        if (savedGroup.IsValid)
                        {
                            savedImageGroups.Add(savedGroup);
                            LogManager.Info($"成功保存图片组: {savedGroup.BaseName}");
                        }
                        else
                        {
                            LogManager.Warning($"保存的图片组无效: {savedGroup.BaseName}");
                        }
                    }

                    LogManager.Info($"{modeName}完成，共保存 {savedImageGroups.Count} 组图片到 {templateDir}");
                    var sourceSummary = string.Join(", ", sourceDirs.Select(d => $"{Path.GetFileName(d)}/{savedImageGroups.Count}张"));
                    LogManager.Info($"目录结构: {sourceSummary}" +
                        (needs3DDir ? $", 3D/{savedImageGroups.Count * 2}张" : ""));
                    return savedImageGroups;
                }
                finally
                {
                    progressDialog.Close();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存图片到模板目录失败: {ex.Message}");
                MessageBox.Show($"保存图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<ImageGroupSet>();
            }
        }

        /// <summary>
        /// 复制图片文件到指定子目录
        /// </summary>
        private async Task<string> CopyImageFileToSubDirectory(string sourcePath, string targetDir, string baseName)
        {
            try
            {
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    return null;
                }

                string extension = Path.GetExtension(sourcePath);
                string fileName = $"{baseName}{extension}";
                string destinationPath = Path.Combine(targetDir, fileName);

                // 如果文件已存在，添加时间戳避免覆盖
                if (File.Exists(destinationPath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    fileName = $"{baseName}_{timestamp}{extension}";
                    destinationPath = Path.Combine(targetDir, fileName);
                }

                // 异步复制文件
                await Task.Run(() => File.Copy(sourcePath, destinationPath));

                LogManager.Info($"复制图片文件: {Path.GetFileName(sourcePath)} -> {Path.GetFileName(targetDir)}/{fileName}");
                return destinationPath;
            }
            catch (Exception ex)
            {
                LogManager.Error($"复制图片文件失败: {ex.Message}");
                return null;
            }
        }



        /// <summary>
        /// 启动验机图片检测模式
        /// 从验机图片集文件夹加载图片，使用现有连续检测机制，完成后显示结果窗口
        /// </summary>
        private async Task StartValidatorMachineDetectionMode(string folderPath, int sampleCount, int loopCycle)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || sampleCount <= 0 || loopCycle <= 0)
                {
                    LogUpdate("验机参数无效");
                    return;
                }

                // 设置验机模式标志
                _isValidatorMachineMode = true;
                _validatorMachineLoopCycle = loopCycle;
                _validatorMachineSampleCount = sampleCount;
                _validatorMachineLotNumber = Path.GetFileName(folderPath);
                _validatorMachineResults.Clear();

                LogUpdate($"开始验机图片检测 - LOT: {_validatorMachineLotNumber}, 样品数: {sampleCount}, 巡回次数: {loopCycle}");

                // 显示加载对话框
                LoadingDialog loadingDialog = new LoadingDialog("正在加载验机图片集...");
                loadingDialog.Owner = Application.Current.MainWindow;
                loadingDialog.Show();
                await Task.Delay(100);

                try
                {
                    // 从验机图片集文件夹加载图片组
                    var imageGroups = await Task.Run(() => LoadValidatorMachineImageGroups(folderPath, sampleCount, loopCycle));

                    if (imageGroups == null || imageGroups.Count == 0)
                    {
                        loadingDialog.Close();
                        MessageBox.Show("未找到有效的验机图片组", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        _isValidatorMachineMode = false;
                        return;
                    }

                    loadingDialog.Close();
                    await Task.Delay(100);

                    LogUpdate($"成功加载 {imageGroups.Count} 组图片");

                    // 获取检测项目名称列表（从DataGrid列中获取）
                    var projectNames = GetDetectionProjectNames();

                    // 创建并初始化结果窗口（保存引用供回调使用）
                    _validatorMachineResultsWindow = new ValidatorMachineResultsWindow();
                    _validatorMachineResultsWindow.Owner = Application.Current.MainWindow;
                    _validatorMachineResultsWindow.InitializeResults(sampleCount, loopCycle, projectNames, _validatorMachineLotNumber);

                    // 启动测试模式（如果未启动）
                    if (!_isTestModeActive)
                    {
                        _testModeDataManager = new TestModeDataManager();
                        _testModeDataManager.StartTestMode();
                        _isTestModeActive = true;
                    }

                    // 设置图片组到测试管理器
                    _imageTestManager.SetImageGroups(imageGroups);
                    _imageTestManager.MoveToFirst();  // 移到第一组
                    _imageTestManager.SetState(ImageTestState.Testing);
                    UpdateImageTestCardUI();

                    // 先设置正向连续检测模式，这样第一组检测完成后回调能正确收集结果
                    _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.ToLast);

                    // 执行第一组检测，后续由 HandleAutoDetectionAfterCompletion 自动继续
                    LogUpdate($"开始检测第 1/{imageGroups.Count} 组: {_imageTestManager.CurrentGroup?.BaseName}");
                    await ExecuteCurrentImageGroup();
                }
                catch (Exception ex)
                {
                    if (loadingDialog.IsVisible)
                    {
                        loadingDialog.Close();
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                _isValidatorMachineMode = false;
                _validatorMachineResultsWindow = null;
                LogUpdate($"验机图片检测失败: {ex.Message}");
                LogManager.Error($"验机图片检测失败: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"验机图片检测失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从验机图片集文件夹加载图片组
        /// </summary>
        private List<ImageGroupSet> LoadValidatorMachineImageGroups(string validatorFolderPath, int sampleCount, int loopCycle)
        {
            var imageGroups = new List<ImageGroupSet>();

            try
            {
                LogManager.Info($"加载验机图片集: {validatorFolderPath}, 样品数: {sampleCount}, 巡回次数: {loopCycle}");

                // 遍历样品文件夹 (图号1, 图号2, ...)
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    string sampleDir = Path.Combine(validatorFolderPath, $"图号{sampleIndex + 1}");
                    if (!Directory.Exists(sampleDir))
                    {
                        LogManager.Warning($"样品文件夹不存在: {sampleDir}");
                        continue;
                    }

                    // 检查图像源文件夹（动态数量）
                    int requiredSources = GetRequired2DSourceCount();
                    string source1Dir = ResolveSourceFolder(sampleDir, 0);
                    string threeDDir = Path.Combine(sampleDir, "3D");

                    // 获取图像源1中的所有图片（按文件名排序）
                    var source1Files = new List<string>();
                    if (!string.IsNullOrEmpty(source1Dir) && Directory.Exists(source1Dir))
                    {
                        source1Files = Directory.GetFiles(source1Dir, "*.bmp")
                            .OrderBy(f => ExtractImageNumber(Path.GetFileName(f)))
                            .ToList();
                    }

                    // 遍历每个轮次的图片
                    for (int cycleIndex = 0; cycleIndex < loopCycle && cycleIndex < source1Files.Count; cycleIndex++)
                    {
                        string source1File = source1Files[cycleIndex];
                        string suffix = GetFileSuffix(Path.GetFileName(source1File));

                        var imageGroup = new ImageGroupSet
                        {
                            BaseName = $"图号{sampleIndex + 1}_第{cycleIndex + 1}次",
                            SampleIndex = sampleIndex,  // 保存样品索引
                            CycleIndex = cycleIndex     // 保存轮次索引
                        };
                        imageGroup.SetSource(0, source1File);

                        for (int i = 1; i < requiredSources; i++)
                        {
                            var sourceDir = ResolveSourceFolder(sampleDir, i);
                            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                            {
                                continue;
                            }

                            var sourceFile = Directory.GetFiles(sourceDir, $"*{suffix}.bmp").FirstOrDefault();
                            if (!string.IsNullOrEmpty(sourceFile))
                            {
                                imageGroup.SetSource(i, sourceFile);
                            }
                        }

                        // 查找3D图片
                        if (Directory.Exists(threeDDir))
                        {
                            var heightFile = Directory.GetFiles(threeDDir, $"height*{suffix}.*").FirstOrDefault();
                            var grayFile = Directory.GetFiles(threeDDir, $"gray*{suffix}.*").FirstOrDefault();
                            if (!string.IsNullOrEmpty(heightFile))
                                imageGroup.HeightImagePath = heightFile;
                            if (!string.IsNullOrEmpty(grayFile))
                                imageGroup.GrayImagePath = grayFile;
                        }

                        if (imageGroup.IsValid)
                        {
                            imageGroups.Add(imageGroup);
                            LogManager.Debug($"添加图片组: 样品{sampleIndex + 1}, 轮次{cycleIndex + 1}");
                        }
                    }
                }

                LogManager.Info($"共加载 {imageGroups.Count} 个图片组");
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载验机图片集失败: {ex.Message}");
            }

            return imageGroups;
        }

        /// <summary>
        /// 获取文件名后缀（如从 "a_0001.bmp" 获取 "_0001"）
        /// </summary>
        private string GetFileSuffix(string fileName)
        {
            try
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                int lastUnderscore = nameWithoutExt.LastIndexOf('_');
                if (lastUnderscore >= 0)
                {
                    return nameWithoutExt.Substring(lastUnderscore);
                }
                return nameWithoutExt;
            }
            catch
            {
                return fileName;
            }
        }

        /// <summary>
        /// 从文件名中提取图片编号
        /// </summary>
        private int ExtractImageNumber(string fileName)
        {
            try
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var match = System.Text.RegularExpressions.Regex.Match(nameWithoutExt, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                {
                    return number;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 获取检测项目名称列表（从DataGrid列中提取）
        /// </summary>
        private List<string> GetDetectionProjectNames()
        {
            var projectNames = new List<string>();
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (DataGrid1.ItemsSource is IEnumerable<DetectionItem> items)
                    {
                        foreach (var item in items)
                        {
                            if (!string.IsNullOrEmpty(item.Name))
                            {
                                projectNames.Add(item.Name);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取检测项目名称失败: {ex.Message}");
            }

            // 如果没有获取到，返回默认项目列表
            if (projectNames.Count == 0)
            {
                projectNames = new List<string>
                {
                    "BLK长度", "BLK宽度", "BLK高度",
                    "圆片位置X", "圆片位置Y",
                    "胶点面积", "胶点直径"
                };
            }

            return projectNames;
        }

        /// <summary>
        /// 获取当前检测结果（项目名-数值 字典）
        /// 从缓存的2D检测项目中读取，确保数据稳定性
        /// </summary>
        private Dictionary<string, double> GetCurrentDetectionResults()
        {
            var results = new Dictionary<string, double>();
            try
            {
                // 从缓存读取，而不是从DataGrid读取（DataGrid更新是异步的，可能不稳定）
                var cachedItems = GetCached2DItems();
                if (cachedItems != null && cachedItems.Count > 0)
                {
                    foreach (var item in cachedItems)
                    {
                        if (!string.IsNullOrEmpty(item.Name) && !string.IsNullOrEmpty(item.Value))
                        {
                            if (double.TryParse(item.Value, out double value))
                            {
                                results[item.Name] = value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取检测结果失败: {ex.Message}");
            }
            return results;
        }

        /// <summary>
        /// 开始验机图片集制作模式
        /// </summary>
        private async Task StartValidatorMachineCollectionMode(string folderPath, int sampleCount)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || sampleCount <= 0)
                {
                    LogUpdate("验机参数无效");
                    return;
                }

                // 🔧 设置验机模式标志
                _isValidatorMachineMode = true;
                _validatorMachineSampleCount = sampleCount;
                _validatorMachineResults.Clear();

                LogUpdate($"开始验机图片集制作 - 文件夹: {folderPath}, 样品数目: {sampleCount}");

                // 显示加载对话框
                LoadingDialog loadingDialog = null;
                string errorMessage = null;
                string errorTitle = null;
                try
                {
                    loadingDialog = new LoadingDialog("正在搜索验机图片并分组，请稍候...");
                    loadingDialog.Owner = Application.Current.MainWindow;
                    loadingDialog.Show();
                    await Task.Delay(100);

                    // 第一步：搜索并分组图片（传入样品数目，自动计算巡回次数）
                    var searchResult = await Task.Run(() => SearchAndGroupValidatorMachineImages(folderPath, sampleCount));

                    // 检查是否有错误信息
                    if (searchResult.HasError)
                    {
                        errorMessage = searchResult.ErrorMessage;
                        errorTitle = searchResult.ErrorTitle ?? "错误";
                        LogUpdate($"验机图片搜索失败: {errorMessage}");
                    }
                    else if (searchResult.ImageGroups == null || searchResult.ImageGroups.Count == 0)
                    {
                        var requiredSources = GetRequired2DSourceCount();
                        var sourceFolders = Enumerable.Range(0, requiredSources)
                            .Select(GetPreferredSourceFolderName);
                        errorMessage = $"未找到任何图片，请检查文件夹是否包含'{string.Join("、", sourceFolders)}'等子文件夹";
                        errorTitle = "未找到图片";
                        LogUpdate("未找到任何图片，请检查文件夹结构");
                    }

                    // 如果有错误，在关闭对话框后显示
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        _isValidatorMachineMode = false;
                        return;
                    }

                    var imageGroups = searchResult.ImageGroups;
                    int loopCycle = searchResult.LoopCycle; // 从搜索结果获取计算出的巡回次数
                    _validatorMachineLoopCycle = loopCycle;

                    LogUpdate($"成功识别 {imageGroups.Count} 个样本，巡回次数: {loopCycle}，共 {imageGroups.Sum(g => g.HasImageCount)} 张图片");

                    // 第二步：保存图片到验机模板目录
                    var savedImageGroups = await SaveValidatorMachineImageSets(imageGroups, folderPath, sampleCount, loopCycle);
                    if (savedImageGroups == null || savedImageGroups.Count == 0)
                    {
                        LogUpdate("验机图片集制作失败：图片保存失败");
                        _isValidatorMachineMode = false;
                        return;
                    }

                    LogUpdate($"验机图片集制作完成，已保存 {savedImageGroups.Count} 个样本，现在开始测试");

                    // 启动测试模式
                    if (!_isTestModeActive)
                    {
                        _testModeDataManager = new TestModeDataManager();
                        _testModeDataManager.StartTestMode();
                        _isTestModeActive = true;
                        LogManager.Info($"[测试模式] 验机图片集制作已启动，数据管理器已激活");
                    }

                    // 设置图片组到测试管理器
                    _imageTestManager.SetImageGroups(savedImageGroups);
                    _imageTestManager.SetState(ImageTestState.Testing);

                    // 更新UI状态
                    UpdateImageTestCardUI();

                    // 开始执行第一组图片检测
                    await ExecuteCurrentImageGroup();
                }
                finally
                {
                    // 先关闭Loading对话框
                    if (loadingDialog != null)
                    {
                        await Task.Delay(200);
                        loadingDialog.Close();
                    }

                    // 再显示错误消息（在UI线程中）
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        MessageBoxImage icon = errorTitle == "图片缺失警告" ? MessageBoxImage.Warning : MessageBoxImage.Information;
                        MessageBox.Show(errorMessage, errorTitle, MessageBoxButton.OK, icon);
                    }
                }
            }
            catch (Exception ex)
            {
                _isValidatorMachineMode = false;
                LogUpdate($"验机图片集制作失败: {ex.Message}");
                MessageBox.Show($"验机图片集制作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 搜索并按样品数目分组验机图片 - 返回结果包装
        /// 样品数目由用户指定，巡回次数自动计算
        /// 分组方式：跳跃式分组，如样品数10，总图片60张，则巡回次数=6
        /// 图号1: 1,11,21,31,41,51  图号2: 2,12,22,32,42,52 以此类推
        /// </summary>
        private ValidatorMachineSearchResult SearchAndGroupValidatorMachineImages(string rootPath, int sampleCount)
        {
            try
            {
                LogManager.Info($"开始搜索验机图片 - 根目录: {rootPath}, 样品数目: {sampleCount}");

                // 字典存储所有找到的 BMP 文件及其序号
                // Key: 序号（从文件名中提取），Value: 完整路径列表
                var imagesByNumber = new Dictionary<int, List<string>>();

                // 递归搜索所有子文件夹
                SearchForImageSourceFolders(rootPath, imagesByNumber);

                if (imagesByNumber.Count == 0)
                {
                    LogManager.Warning("未找到任何图片文件");
                    return ValidatorMachineSearchResult.CreateError("未找到任何图片文件，请检查文件夹结构");
                }

                // 验证序号的连续性和完整性
                var sortedNumbers = imagesByNumber.Keys.OrderBy(x => x).ToList();
                int totalImages = sortedNumbers.Count;

                LogManager.Info($"找到的图片序号范围: {sortedNumbers.First()} - {sortedNumbers.Last()}，共 {totalImages} 张");

                // 验证总图片数是否是样品数目的倍数
                if (totalImages % sampleCount != 0)
                {
                    string errorMsg = $"图片总数 ({totalImages}) 不是样品数目 ({sampleCount}) 的倍数！\n\n" +
                        $"根据样品数目 {sampleCount}，应该有 {(totalImages / sampleCount + 1) * sampleCount} 张或 {(totalImages / sampleCount) * sampleCount} 张图片。\n\n" +
                        $"当前缺少 {sampleCount - (totalImages % sampleCount)} 张图片。\n\n" +
                        $"请检查图片是否缺失。";

                    LogManager.Error($"验证失败: {errorMsg}");
                    return ValidatorMachineSearchResult.CreateError(errorMsg, "图片缺失警告");
                }

                // 计算巡回次数
                int loopCycle = totalImages / sampleCount;

                LogManager.Info($"✅ 图片数量验证通过: 总图片数={totalImages}, 样品数目={sampleCount}, 巡回次数={loopCycle}");

                // 如果序号不连续或不从1开始，需要进行映射
                if (sortedNumbers.First() != 1 || sortedNumbers.Last() != sortedNumbers.Count)
                {
                    LogManager.Info($"检测到序号不连续，正在进行重新编号...");
                    var remappedImages = new Dictionary<int, List<string>>();
                    for (int i = 0; i < sortedNumbers.Count; i++)
                    {
                        int oldNumber = sortedNumbers[i];
                        int newNumber = i + 1;
                        remappedImages[newNumber] = imagesByNumber[oldNumber];
                        LogManager.Debug($"序号映射: {oldNumber} -> {newNumber}");
                    }
                    imagesByNumber = remappedImages;
                    sortedNumbers = imagesByNumber.Keys.OrderBy(x => x).ToList();
                }

                // 按跳跃式分组：图号1获取序号1,1+sampleCount,1+2*sampleCount...
                // 例如样品数10，巡回6次：图号1获取1,11,21,31,41,51
                var imageGroups = new List<ValidatorMachineImageGroup>();
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    var group = new ValidatorMachineImageGroup
                    {
                        SampleNumber = sampleIndex + 1,
                        ImagePaths = new List<string>()
                    };

                    // 收集该样本的所有图片（跳跃式）
                    var collectedNumbers = new List<int>();
                    for (int cycleIndex = 0; cycleIndex < loopCycle; cycleIndex++)
                    {
                        int imageNumber = sampleIndex + 1 + cycleIndex * sampleCount;
                        if (imagesByNumber.ContainsKey(imageNumber))
                        {
                            group.ImagePaths.AddRange(imagesByNumber[imageNumber]);
                            collectedNumbers.Add(imageNumber);
                            LogManager.Debug($"样本 {group.SampleNumber}: 添加序号 {imageNumber} 的图片");
                        }
                        else
                        {
                            LogManager.Warning($"样本 {group.SampleNumber}: 未找到序号 {imageNumber} 的图片");
                        }
                    }

                    if (group.ImagePaths.Count > 0)
                    {
                        imageGroups.Add(group);
                        LogManager.Info($"样本 {group.SampleNumber}: {group.ImagePaths.Count} 张图片 (序号: {string.Join(",", collectedNumbers)})");
                    }
                }

                LogManager.Info($"分组完成，共 {imageGroups.Count} 个样本，每个样本 {loopCycle} 次检测");
                return ValidatorMachineSearchResult.CreateSuccess(imageGroups, loopCycle);
            }
            catch (Exception ex)
            {
                LogManager.Error($"搜索并分组图片失败: {ex.Message}\n{ex.StackTrace}");
                return ValidatorMachineSearchResult.CreateError($"搜索并分组图片失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件路径中提取序号用于日志
        /// </summary>
        private int ExtractNumberFromPath(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (ExtractImageNumber(fileName, out int number))
                    return number;
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 递归搜索图像源文件夹中的 BMP 文件
        /// </summary>
        private void SearchForImageSourceFolders(string dirPath, Dictionary<int, List<string>> imagesByNumber)
        {
            try
            {
                if (!Directory.Exists(dirPath))
                {
                    LogManager.Warning($"目录不存在: {dirPath}");
                    return;
                }

                // 检查当前目录是否是主图像源文件夹
                string folderName = Path.GetFileName(dirPath);
                var primaryCandidates = ImageSourceNaming.GetFolderCandidates(0);
                if (primaryCandidates.Any(name => string.Equals(folderName, name, StringComparison.OrdinalIgnoreCase)))
                {
                    // 搜索该目录中的 BMP 文件
                    var bmpFiles = Directory.GetFiles(dirPath, "*.bmp", SearchOption.TopDirectoryOnly);
                    LogManager.Info($"在 {dirPath} 找到 {bmpFiles.Length} 张 BMP 文件");

                    foreach (var bmpFile in bmpFiles)
                    {
                        // 从文件名中提取序号
                        // 预期格式: xxx_n 其中 n 是序号
                        string fileName = Path.GetFileNameWithoutExtension(bmpFile);
                        if (ExtractImageNumber(fileName, out int imageNumber))
                        {
                            if (!imagesByNumber.ContainsKey(imageNumber))
                            {
                                imagesByNumber[imageNumber] = new List<string>();
                            }
                            imagesByNumber[imageNumber].Add(bmpFile);
                            LogManager.Debug($"找到图片: {fileName} (序号: {imageNumber}) - 路径: {bmpFile}");
                        }
                        else
                        {
                            LogManager.Warning($"无法从文件名提取序号: {fileName}");
                        }
                    }
                }

                // 递归搜索子文件夹
                try
                {
                    var subDirs = Directory.GetDirectories(dirPath);
                    LogManager.Debug($"在 {dirPath} 找到 {subDirs.Length} 个子文件夹");

                    foreach (var subDir in subDirs)
                    {
                        SearchForImageSourceFolders(subDir, imagesByNumber);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogManager.Warning($"无权限访问目录: {dirPath} - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"搜索图像源文件夹失败: {dirPath} - {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件名中提取图片序号
        /// </summary>
        private bool ExtractImageNumber(string fileName, out int imageNumber)
        {
            imageNumber = 0;

            // 预期格式: xxx_n 其中 n 是序号
            int lastUnderscoreIndex = fileName.LastIndexOf('_');
            if (lastUnderscoreIndex < 0 || lastUnderscoreIndex == fileName.Length - 1)
            {
                return false;
            }

            string numberPart = fileName.Substring(lastUnderscoreIndex + 1);
            return int.TryParse(numberPart, out imageNumber) && imageNumber > 0;
        }

        /// <summary>
        /// 保存验机图片集到模板目录
        /// </summary>
        private async Task<List<ImageGroupSet>> SaveValidatorMachineImageSets(List<ValidatorMachineImageGroup> imageGroups, string sourcePath, int sampleCount, int loopCycle)
        {
            try
            {
                // 获取 LOT 号（使用文件夹名称）
                string lotNumber = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar));

                // 构建模板验机目录
                string templateDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "templates",
                    CurrentTemplateName,
                    "验机图片集",
                    lotNumber
                );

                LogManager.Info($"创建验机图片集目录: {templateDir}");

                // 先删除旧的验机图片集目录（如果存在）
                if (Directory.Exists(templateDir))
                {
                    LogManager.Info($"删除已存在的验机图片集目录: {templateDir}");
                    Directory.Delete(templateDir, true);
                    System.Threading.Thread.Sleep(100); // 给系统一点时间来释放文件
                }

                var savedImageGroups = new List<ImageGroupSet>();

                // 显示进度对话框
                var progressDialog = new LoadingDialog($"正在保存 {imageGroups.Count} 个样本的图片...");
                progressDialog.Owner = Application.Current.MainWindow;
                progressDialog.Show();

                try
                {
                    await Task.Delay(100);

                    foreach (var sampleGroup in imageGroups)
                    {
                        try
                        {
                            // 创建样本目录: 图号n
                            string sampleDir = Path.Combine(templateDir, $"图号{sampleGroup.SampleNumber}");
                            LogManager.Info($"创建样本目录: {sampleDir}");

                            // 对于该样本的每一张图片，找到它所在的源文件夹结构并复制
                            // 构建一个映射：源文件夹路径 -> 该样本的所有文件列表
                            var sourceStructureMap = new Dictionary<string, List<string>>();

                            foreach (var imagePath in sampleGroup.ImagePaths)
                            {
                                // 找到该图片所在的源文件夹结构根目录（包含图像源1的目录）
                                string sourceStructureRoot = FindSourceStructureRoot(imagePath);
                                if (string.IsNullOrEmpty(sourceStructureRoot))
                                {
                                    LogManager.Warning($"无法找到图片的源文件夹结构: {imagePath}");
                                    continue;
                                }

                                if (!sourceStructureMap.ContainsKey(sourceStructureRoot))
                                {
                                    sourceStructureMap[sourceStructureRoot] = new List<string>();
                                }
                                sourceStructureMap[sourceStructureRoot].Add(imagePath);
                            }

                            // 对于每个源文件夹结构，复制所有相关文件夹
                            foreach (var kvp in sourceStructureMap)
                            {
                                string sourceRoot = kvp.Key;
                                List<string> filesToCopy = kvp.Value;

                                LogManager.Info($"从源目录复制: {sourceRoot} -> {sampleDir}，复制 {filesToCopy.Count} 张图片");
                                await CopyImageSourceStructure(sourceRoot, sampleDir, filesToCopy);
                            }

                            // 创建 ImageGroupSet 对象
                            int requiredSources = GetRequired2DSourceCount();
                            var savedGroup = new ImageGroupSet
                            {
                                BaseName = $"Sample_{sampleGroup.SampleNumber}"
                            };

                            for (int i = 0; i < requiredSources; i++)
                            {
                                string sourceDir = Path.Combine(sampleDir, GetPreferredSourceFolderName(i));
                                if (Directory.Exists(sourceDir) && Directory.GetFiles(sourceDir).Length > 0)
                                {
                                    savedGroup.SetSource(i, sourceDir);
                                }
                            }

                            string threeDPath = Path.Combine(sampleDir, "3D");
                            if (Directory.Exists(threeDPath) && Directory.GetFiles(threeDPath).Length > 0)
                            {
                                var threeDFiles = Directory.GetFiles(threeDPath);
                                if (threeDFiles.Any(f => f.EndsWith("height.bmp", StringComparison.OrdinalIgnoreCase)))
                                    savedGroup.HeightImagePath = Path.Combine(threeDPath, "height.bmp");
                                if (threeDFiles.Any(f => f.EndsWith("gray.bmp", StringComparison.OrdinalIgnoreCase)))
                                    savedGroup.GrayImagePath = Path.Combine(threeDPath, "gray.bmp");
                            }

                            if (savedGroup.IsValid)
                            {
                                savedImageGroups.Add(savedGroup);
                                LogManager.Info($"成功保存样本 {sampleGroup.SampleNumber}: {Path.GetFileName(sampleDir)}");
                            }
                            else
                            {
                                LogManager.Warning($"样本 {sampleGroup.SampleNumber} 的ImageGroupSet无效");
                            }

                            // 更新进度
                            progressDialog.UpdateMessage($"已保存 {savedImageGroups.Count}/{imageGroups.Count} 个样本...");
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"保存样本 {sampleGroup.SampleNumber} 失败: {ex.Message}\n{ex.StackTrace}");
                        }
                    }

                    LogManager.Info($"验机图片集制作完成，共保存 {savedImageGroups.Count} 个样本到 {templateDir}");
                    return savedImageGroups;
                }
                finally
                {
                    progressDialog.Close();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存验机图片集失败: {ex.Message}\n{ex.StackTrace}");
                return new List<ImageGroupSet>();
            }
        }

        /// <summary>
        /// 找到包含该图像文件的源文件夹结构根目录
        /// 返回包含"图像源1"等文件夹的父目录
        /// </summary>
        private string FindSourceStructureRoot(string imagePath)
        {
            try
            {
                string currentDir = Path.GetDirectoryName(imagePath);

                // 向上查找，直到找到包含"图像源1"等子文件夹的父目录
                while (!string.IsNullOrEmpty(currentDir) && currentDir != Path.GetPathRoot(currentDir))
                {
                    string folderName = Path.GetFileName(currentDir);

                    // 如果当前目录是图像源或3D目录，返回其父目录
                    if (string.Equals(folderName, "3D", StringComparison.OrdinalIgnoreCase) ||
                        GetActiveSourceFolderCandidates().Any(name => string.Equals(folderName, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        string parentDir = Path.GetDirectoryName(currentDir);
                        LogManager.Debug($"找到源文件夹结构根: {imagePath} -> {parentDir}");
                        return parentDir;
                    }

                    currentDir = Path.GetDirectoryName(currentDir);
                }

                LogManager.Warning($"无法找到源文件夹结构根: {imagePath}");
                return null;
            }
            catch (Exception ex)
            {
                LogManager.Error($"查找源文件夹结构根失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 复制图像源文件夹结构，只包含指定的图片
        /// 会根据图像源1中的文件序号，同时复制图像源2_1、图像源2_2、3D文件夹中相同序号的文件
        /// </summary>
        private async Task CopyImageSourceStructure(string sourceParentDir, string targetSampleDir, List<string> imagesToCopy)
        {
            try
            {
                // 创建样本目录
                Directory.CreateDirectory(targetSampleDir);

                // 首先从 imagesToCopy 中提取所有需要复制的文件序号
                var imageNumbersToCopy = new HashSet<int>();
                foreach (var imagePath in imagesToCopy)
                {
                    string fileName = Path.GetFileNameWithoutExtension(imagePath);
                    if (ExtractImageNumber(fileName, out int imageNumber))
                    {
                        imageNumbersToCopy.Add(imageNumber);
                    }
                }

                LogManager.Info($"需要复制的图片序号: {string.Join(", ", imageNumbersToCopy.OrderBy(x => x))}");

                // 枚举源目录中的所有图像源文件夹（兼容动态命名）
                var candidateNames = GetActiveSourceFolderCandidates();
                var imageSourceDirs = Directory.GetDirectories(sourceParentDir)
                    .Where(d =>
                    {
                        var name = Path.GetFileName(d);
                        if (string.Equals(name, "3D", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (candidateNames.Any(candidate => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }

                        return name.StartsWith("图像源", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                foreach (var imageSourceDir in imageSourceDirs)
                {
                    string sourceFolderName = Path.GetFileName(imageSourceDir);
                    string targetImageDir = Path.Combine(targetSampleDir, sourceFolderName);

                    // 创建目标图像源文件夹
                    Directory.CreateDirectory(targetImageDir);

                    int copiedCount = 0;

                    // 复制文件夹中的文件
                    var sourceFiles = Directory.GetFiles(imageSourceDir);
                    foreach (var sourceFile in sourceFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(sourceFile);

                        // 检查该文件的序号是否在需要复制的序号列表中
                        if (ExtractImageNumber(fileName, out int fileNumber) && imageNumbersToCopy.Contains(fileNumber))
                        {
                            string targetFile = Path.Combine(targetImageDir, Path.GetFileName(sourceFile));
                            await Task.Run(() => File.Copy(sourceFile, targetFile, true));
                            copiedCount++;
                        }
                    }

                    LogManager.Info($"复制文件夹: {sourceFolderName} ({copiedCount} 张图片)");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"复制图像源文件夹结构失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 将验机图片组转换为 ImageGroupSet 格式
        /// </summary>
        private List<ImageGroupSet> ConvertValidatorMachineGroupsToImageGroupSets(List<ValidatorMachineImageGroup> validatorGroups)
        {
            try
            {
                var result = new List<ImageGroupSet>();

                foreach (var group in validatorGroups)
                {
                    if (group.ImagePaths == null || group.ImagePaths.Count == 0)
                        continue;

                    // 创建一个 ImageGroupSet 来适配现有的测试框架
                    var imageGroupSet = new ImageGroupSet
                    {
                        BaseName = $"验机_样本{group.SampleNumber}",
                        HeightImagePath = group.ImagePaths.Count > 3 ? group.ImagePaths[3] : null,
                        GrayImagePath = group.ImagePaths.Count > 4 ? group.ImagePaths[4] : null
                    };

                    int requiredSources = GetRequired2DSourceCount();
                    for (int i = 0; i < requiredSources && i < group.ImagePaths.Count; i++)
                    {
                        imageGroupSet.SetSource(i, group.ImagePaths[i]);
                    }

                    result.Add(imageGroupSet);
                }

                LogManager.Info($"成功转换 {result.Count} 个验机图片组为 ImageGroupSet 格式");
                return result;
            }
            catch (Exception ex)
            {
                LogManager.Error($"转换验机图片组格式失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 验机图片组数据结构
        /// </summary>
        private class ValidatorMachineImageGroup
        {
            public int SampleNumber { get; set; }
            public List<string> ImagePaths { get; set; }
            public int HasImageCount => ImagePaths?.Count ?? 0;
        }

        /// <summary>
        /// 验机图片搜索结果包装类 - 用于避免在后台线程中显示MessageBox
        /// </summary>
        private class ValidatorMachineSearchResult
        {
            public bool HasError { get; private set; }
            public string ErrorMessage { get; private set; }
            public string ErrorTitle { get; private set; }
            public List<ValidatorMachineImageGroup> ImageGroups { get; private set; }
            public int LoopCycle { get; private set; }

            public static ValidatorMachineSearchResult CreateSuccess(List<ValidatorMachineImageGroup> groups, int loopCycle)
            {
                return new ValidatorMachineSearchResult
                {
                    HasError = false,
                    ErrorMessage = null,
                    ErrorTitle = null,
                    ImageGroups = groups,
                    LoopCycle = loopCycle
                };
            }

            public static ValidatorMachineSearchResult CreateError(string message, string title = null)
            {
                return new ValidatorMachineSearchResult
                {
                    HasError = true,
                    ErrorMessage = message,
                    ErrorTitle = title ?? "错误",
                    ImageGroups = new List<ValidatorMachineImageGroup>(),
                    LoopCycle = 0
                };
            }
        }

        /// <summary>
        /// 开始图片检测（原有功能）
        /// </summary>
        private async Task StartImageTesting()
        {
            try
            {
                // 选择图片文件
                var imageGroups = await SelectImageFilesAsync();
                if (imageGroups == null || imageGroups.Count == 0)
                {
                    LogUpdate("未选择有效的图片组");
                    return;
                }

                // 🔧 新增：启动测试模式数据管理
                if (!_isTestModeActive)
                {
                    _testModeDataManager = new TestModeDataManager();
                    _testModeDataManager.StartTestMode();
                    _isTestModeActive = true;
                    LogManager.Info("[测试模式] 图片测试模式已启动，数据管理器已激活");
                }

                // 设置图片组
                _imageTestManager.SetImageGroups(imageGroups);
                _imageTestManager.SetState(ImageTestState.Testing);

                LogUpdate($"已加载 {imageGroups.Count} 组图片，开始连续检测");

                // 更新UI状态
                UpdateImageTestCardUI();

                // 自动执行第一组检测（相当于点击一次"下一组"）
                await Task.Delay(50); // 短暂延时确保UI更新完成
                
                // 直接执行当前组检测，不设置连续检测模式
                if (_imageTestManager.ImageGroups.Count > 0)
                {
                    LogUpdate("开始检测第一组");
                    
                    // 🔧 修复：只执行当前组的检测，不要提前移动索引
                    // 索引移动应该由用户手动操作或连续检测逻辑控制
                    await ExecuteCurrentImageGroup();
                    
                    // 检测完成后重新更新UI状态
                    UpdateImageTestCardUI();
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"开始图片检测失败: {ex.Message}");
                _imageTestManager.SetState(ImageTestState.Idle);
                UpdateImageTestCardUI();
            }
        }

        /// <summary>
        /// 停止图片检测
        /// </summary>
        private void StopImageTesting()
        {
            try
            {
                // 🔧 新增：检查是否需要导出测试数据
                if (_isCicdMode)
                {
                    _isCicdMode = false;
                    _cicdRunContext = null;
                    EndTestMode();

                    _imageTestManager.SetState(ImageTestState.Idle);
                    _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                    _imageTestManager.SetImageGroups(new List<ImageGroupSet>());
                    UpdateImageTestCardUI();
                    LogUpdate("CICD已结束：已退出图片检测模式");
                    return;
                }

                if (_isTestModeActive && _testModeDataManager != null)
                {
                    ShowTestModeExportDialog();
                }

                _imageTestManager.SetState(ImageTestState.Idle);
                _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                _imageTestManager.SetImageGroups(new List<ImageGroupSet>());
                UpdateImageTestCardUI();
                
                string message = "图片检测已结束";
                if (_imageTestManager.AutoDetectionMode != AutoDetectionMode.None)
                {
                    message = "连续检测已停止";
                }
                LogUpdate(message);
            }
            catch (Exception ex)
            {
                LogUpdate($"停止图片检测失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 暂停/恢复按钮点击事件
        /// </summary>
        private async void PauseResumeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_imageTestManager.CurrentState == ImageTestState.Testing)
                {
                    // 当前正在检测，切换到暂停状态
                    _imageTestManager.SetState(ImageTestState.Paused);
                    _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None); // 停止自动检测
                    UpdateImageTestCardUI();
                    LogUpdate("图片检测已暂停");
                }
                else if (_imageTestManager.CurrentState == ImageTestState.Paused)
                {
                    // 当前已暂停，恢复检测状态
                    _imageTestManager.SetState(ImageTestState.Testing);
                    UpdateImageTestCardUI();
                    LogUpdate("图片检测已恢复");
                    
                    // 恢复后不会自动执行检测，用户需要手动点击导航按钮
                    LogUpdate("请手动选择检测操作：单张检测或连续检测");
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"暂停/恢复操作失败: {ex.Message}");
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 记录按钮点击事件 - 标记当前图片
        /// </summary>
        private void MarkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isTestModeActive && _testModeDataManager != null)
                {
                    var currentGroup = _imageTestManager.CurrentGroup;
                    if (currentGroup != null)
                    {
                        // 标记当前图片
                        string imagePath = currentGroup.Source1Path; // 使用第一张图片路径作为标识
                        _testModeDataManager.MarkImage(imagePath);
                        
                        // 更新按钮状态
                        UpdateMarkButtonStatus();
                        
                        // 显示标记成功提示
                        ShowMarkSuccessMessage();
                        
                        LogManager.Info($"[测试模式] 图片已标记: {currentGroup.BaseName}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"标记图片失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新记录按钮状态
        /// </summary>
        private void UpdateMarkButtonStatus()
        {
            try
            {
                if (_isTestModeActive && _testModeDataManager != null)
                {
                    var currentGroup = _imageTestManager.CurrentGroup;
                    if (currentGroup != null)
                    {
                        string imagePath = currentGroup.Source1Path;
                        bool isMarked = _testModeDataManager.IsImageMarked(imagePath);
                        
                        // 🔧 修复线程访问问题：确保UI更新在UI线程中执行
                        if (Application.Current != null && Application.Current.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    if (MarkButton != null)
                                    {
                                        // 根据是否已标记设置按钮表情
                                        MarkButton.Content = isMarked ? "🙂记录" : "📝记录";
                                    }
                                }
                                catch (Exception uiEx)
                                {
                                    LogManager.Warning($"[测试模式] 更新记录按钮状态失败（界面可能已关闭）: {uiEx.Message}");
                                }
                            });
                        }
                        else
                        {
                            LogManager.Warning("[测试模式] Application.Current不可用，跳过记录按钮状态更新");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[测试模式] 更新记录按钮状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示标记成功提示（3秒后自动消失）
        /// </summary>
        private void ShowMarkSuccessMessage()
        {
            try
            {
                // 🔧 修复：使用同步Dispatcher调用，避免异步问题
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 创建小弹窗
                        var messageWindow = new Window
                        {
                            Title = "标记成功",
                            Content = new TextBlock
                            {
                                Text = "图片已Mark",
                                FontSize = 16,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                                Margin = new Thickness(20)
                            },
                            Width = 150,
                            Height = 80,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            WindowStyle = WindowStyle.None,
                            Background = new SolidColorBrush(System.Windows.Media.Colors.LightGreen),
                            Topmost = true
                        };

                        messageWindow.Show();

                        // 1.5秒后自动关闭
                        var timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(1.5)
                        };
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            messageWindow.Close();
                        };
                        timer.Start();
                    });
                }
                else
                {
                    LogManager.Warning("[测试模式] Application.Current不可用，无法显示标记成功提示");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[测试模式] 显示标记成功提示失败: {ex.Message}");
            }
        }



        /// <summary>
        /// 导出测试结果
        /// </summary>
        private void ExportTestResults(ExportMode mode)
        {
            try
            {
                if (_testModeDataManager == null)
                    return;

                var resultsToExport = mode == ExportMode.All ? 
                    _testModeDataManager.GetAllResults() : 
                    _testModeDataManager.GetMarkedResults();

                if (resultsToExport.Count == 0)
                {
                    MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 🔧 修复：导出到软件运行目录下的"图片测试与导出"目录
                string testExportBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "图片测试与导出");
                string exportDir = Path.Combine(testExportBaseDir, 
                    $"TestMode_{CurrentLotValue}_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(exportDir);

                // 导出CSV文件
                ExportTestResultsToCSV(resultsToExport, exportDir);

                // 复制图片文件
                CopyTestImages(resultsToExport, exportDir);

                MessageBox.Show($"测试数据已导出到:\n{exportDir}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                LogManager.Info($"[测试模式] 测试数据导出成功: {exportDir}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[测试模式] 导出测试结果失败: {ex.Message}");
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 结束测试模式（仅清理，不处理导出）
        /// </summary>
        private void EndTestMode()
        {
            try
            {
                if (_isTestModeActive && _testModeDataManager != null)
                {
                    _testModeDataManager.StopTestMode();
                    _testModeDataManager = null;
                    _isTestModeActive = false;
                    
                    LogManager.Info("[测试模式] 测试模式已结束");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[测试模式] 结束测试模式失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示测试模式导出选择弹窗
        /// </summary>
        private void ShowTestModeExportDialog()
        {
            try
            {
                // 🔧 修复：创建大尺寸触屏友好的导出选择弹窗
                var exportDialog = new Window
                {
                    Title = "测试数据导出选择",
                    Width = 600,
                    Height = 350,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    ResizeMode = ResizeMode.NoResize
                };

                // 主面板
                var mainPanel = new StackPanel
                {
                    Margin = new Thickness(30),
                    Orientation = System.Windows.Controls.Orientation.Vertical
                };

                // 标题
                var titleText = new TextBlock
                {
                    Text = "请选择要导出的测试数据：",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 30),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                };
                mainPanel.Children.Add(titleText);

                // 按钮面板
                var buttonPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0)
                };

                // 🔧 修复：导出所有按钮 - 绿色背景
                var exportAllButton = new Button
                {
                    Content = "📦 导出所有数据",
                    Width = 150,
                    Height = 60,
                    Margin = new Thickness(15),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(System.Windows.Media.Colors.LightGreen),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Colors.Green),
                    BorderThickness = new Thickness(2)
                };
                exportAllButton.Click += (s, e) =>
                {
                    exportDialog.Close();
                    ExportTestResults(ExportMode.All);
                    EndTestMode();
                };

                // 🔧 修复：导出标记按钮 - 橙色背景
                var exportMarkedButton = new Button
                {
                    Content = "⭐ 仅导出标记数据",
                    Width = 150,
                    Height = 60,
                    Margin = new Thickness(15),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(System.Windows.Media.Colors.LightGoldenrodYellow),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Colors.Orange),
                    BorderThickness = new Thickness(2)
                };
                exportMarkedButton.Click += (s, e) =>
                {
                    exportDialog.Close();
                    ExportTestResults(ExportMode.MarkedOnly);
                    EndTestMode();
                };

                // 🔧 修复：不导出按钮 - 灰色背景
                var noExportButton = new Button
                {
                    Content = "❌ 不导出",
                    Width = 150,
                    Height = 60,
                    Margin = new Thickness(15),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(System.Windows.Media.Colors.LightGray),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Colors.Gray),
                    BorderThickness = new Thickness(2)
                };

                noExportButton.Click += (s, e) =>
                {
                    exportDialog.Close();
                    EndTestMode();
                };

                buttonPanel.Children.Add(exportAllButton);
                buttonPanel.Children.Add(exportMarkedButton);
                buttonPanel.Children.Add(noExportButton);

                mainPanel.Children.Add(buttonPanel);
                exportDialog.Content = mainPanel;

                exportDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                LogManager.Error($"[测试模式] 显示导出选择弹窗失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出测试结果到CSV文件
        /// </summary>
        private void ExportTestResultsToCSV(List<TestModeDetectionResult> results, string exportDir)
        {
            try
            {
                string csvPath = Path.Combine(exportDir, "TestResults.csv");
                
                // 🔧 修复：复用实时CSV导出逻辑，转换为DetectionRecord格式
                var detectionRecords = new List<DetectionRecord>();
                
                foreach (var result in results)
                {
                    var record = new DetectionRecord
                    {
                        Timestamp = result.TestTime,
                        LotNumber = CurrentLotValue,
                        DefectType = result.DefectType ?? (result.IsOK ? "良品" : "不良品"),
                        DetectionItems = new Dictionary<string, DetectionItemValue>()
                    };
                    
                    // 设置图片序号
                    record.ImageNumber = result.ImageNumber ?? "";
                    
                    // 转换检测项目为DetectionItemValue格式
                    if (result.DetectionItems != null)
                    {
                        foreach (var item in result.DetectionItems)
                        {
                            double value = 0;
                            bool hasValidData = double.TryParse(item.Value, out value);
                            
                            double lowerLimit = double.MinValue;
                            double upperLimit = double.MaxValue;
                            
                            if (!string.IsNullOrEmpty(item.LowerLimit))
                                double.TryParse(item.LowerLimit, out lowerLimit);
                            if (!string.IsNullOrEmpty(item.UpperLimit))
                                double.TryParse(item.UpperLimit, out upperLimit);
                            
                            var detectionItem = new DetectionItemValue
                            {
                                Value = value,
                                HasValidData = hasValidData,
                                LowerLimit = lowerLimit,
                                UpperLimit = upperLimit,
                                IsOutOfRange = item.IsOutOfRange
                            };
                            
                            // 直接使用原始项目名，不添加图片编号后缀
                            string itemName = item.Name;
                            record.DetectionItems[itemName] = detectionItem;
                        }
                    }
                    
                    detectionRecords.Add(record);
                }
                
                // 使用RealTimeDataLogger的CSV导出逻辑
                using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
                {
                    if (detectionRecords.Count > 0)
                    {
                        // 使用第一条记录生成表头
                        var headerLine = CreateTestModeCSVHeader(detectionRecords.First());
                        writer.WriteLine(headerLine);
                        
                        // 写入数据行
                        foreach (var record in detectionRecords)
                        {
                            var csvLine = ConvertTestModeRecordToCSV(record);
                            writer.WriteLine(csvLine);
                        }
                    }
                }
                
                LogManager.Info($"[测试模式] CSV文件导出成功: {csvPath}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[测试模式] 导出CSV文件失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 生成测试模式CSV表头
        /// </summary>
        private string CreateTestModeCSVHeader(DetectionRecord record)
        {
            var headerColumns = new List<string> { "序号", "时间戳", "LOT号", "缺陷类型", "结果" };
            
            // 按字母顺序排序项目名称
            var itemNames = record.DetectionItems.Keys.OrderBy(k => k).ToList();
            
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
        
        /// <summary>
        /// 转换测试模式记录为CSV行
        /// </summary>
        private string ConvertTestModeRecordToCSV(DetectionRecord record)
        {
            var csvValues = new List<string>
            {
                $"\"{record.ImageNumber ?? ""}\"",
                $"\"{record.Timestamp:yyyy-MM-dd HH:mm:ss}\"",
                $"\"{record.LotNumber ?? ""}\"",
                $"\"{record.DefectType ?? ""}\"",
                $"\"{(record.IsOK ? "OK" : "NG")}\""
            };
            
            var itemNames = record.DetectionItems.Keys.OrderBy(k => k).ToList();
            
            foreach (var itemName in itemNames)
            {
                var item = record.DetectionItems[itemName];
                
                csvValues.Add(item.HasValidData ? item.Value.ToString("F4") : "");
                csvValues.Add(item.LowerLimit != double.MinValue ? item.LowerLimit.ToString("F4") : "");
                csvValues.Add(item.UpperLimit != double.MaxValue ? item.UpperLimit.ToString("F4") : "");
                csvValues.Add(item.HasValidData ? (item.IsOutOfRange ? "是" : "否") : "");
            }
            
            return string.Join(",", csvValues);
        }

        /// <summary>
        /// 复制测试图片到导出目录
        /// </summary>
        private void CopyTestImages(List<TestModeDetectionResult> results, string exportDir)
        {
            try
            {
                // 🔧 修复：创建图像源目录结构（动态数量）
                int requiredSources = GetRequired2DSourceCount();
                var sourceDirs = new List<string>();
                for (int i = 0; i < requiredSources; i++)
                {
                    string sourceDir = Path.Combine(exportDir, GetPreferredSourceFolderName(i));
                    Directory.CreateDirectory(sourceDir);
                    sourceDirs.Add(sourceDir);
                }
                
                // 🔧 新增：检查是否需要创建3D目录
                bool is3DEnabled = Is3DDetectionEnabled();
                string threeDDir = null;
                if (is3DEnabled)
                {
                    threeDDir = Path.Combine(exportDir, "3D");
                    Directory.CreateDirectory(threeDDir);
                    LogManager.Info("[测试模式] 3D检测已启用，创建3D图片导出目录");
                }
                
                foreach (var result in results)
                {
                    if (!string.IsNullOrEmpty(result.ImagePath) && File.Exists(result.ImagePath))
                    {
                        // 根据图片路径找到对应的图片组
                        var imageGroup = _imageTestManager.ImageGroups.FirstOrDefault(g => 
                            IsImagePathInGroup(g, result.ImagePath) ||
                            g.HeightImagePath == result.ImagePath ||
                            g.GrayImagePath == result.ImagePath);
                        
                        if (imageGroup != null)
                        {
                            for (int i = 0; i < requiredSources; i++)
                            {
                                var sourcePath = imageGroup.GetPath(i);
                                if (!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath))
                                {
                                    string fileName = Path.GetFileName(sourcePath);
                                    string destPath = Path.Combine(sourceDirs[i], fileName);
                                    File.Copy(sourcePath, destPath, true);
                                }
                            }
                            
                            // 🔧 新增：复制3D图片（如果3D使能且图片存在）
                            if (is3DEnabled && threeDDir != null)
                            {
                                // 复制高度图
                                if (!string.IsNullOrEmpty(imageGroup.HeightImagePath) && File.Exists(imageGroup.HeightImagePath))
                                {
                                    string heightFileName = Path.GetFileName(imageGroup.HeightImagePath);
                                    string heightDestPath = Path.Combine(threeDDir, heightFileName);
                                    File.Copy(imageGroup.HeightImagePath, heightDestPath, true);
                                    LogManager.Info($"[测试模式] 复制3D高度图: {heightFileName}");
                                }
                                
                                // 复制灰度图
                                if (!string.IsNullOrEmpty(imageGroup.GrayImagePath) && File.Exists(imageGroup.GrayImagePath))
                                {
                                    string grayFileName = Path.GetFileName(imageGroup.GrayImagePath);
                                    string grayDestPath = Path.Combine(threeDDir, grayFileName);
                                    File.Copy(imageGroup.GrayImagePath, grayDestPath, true);
                                    LogManager.Info($"[测试模式] 复制3D灰度图: {grayFileName}");
                                }
                            }
                        }
                    }
                }
                
                if (is3DEnabled)
                {
                    LogManager.Info($"[测试模式] 图片文件复制成功（包含3D图片）: {exportDir}");
                }
                else
                {
                    LogManager.Info($"[测试模式] 图片文件复制成功（仅2D图片）: {exportDir}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[测试模式] 复制图片文件失败: {ex.Message}");
                throw;
            }
        }

        private int GetRequired2DSourceCount()
        {
            var count = ImageSourceNaming.GetActiveSourceCount();
            if (count <= 0)
            {
                count = 1;
            }

            return Math.Min(count, 10);
        }

        private IReadOnlyList<string> GetActiveSourceFolderCandidates()
        {
            var candidates = new List<string>();
            var count = GetRequired2DSourceCount();
            for (int i = 0; i < count; i++)
            {
                candidates.AddRange(ImageSourceNaming.GetFolderCandidates(i));
            }

            return candidates
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool IsImageSourceFile(string filePath)
        {
            var parentDir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(parentDir))
            {
                return false;
            }

            var folderName = Path.GetFileName(parentDir);
            if (string.Equals(folderName, "3D", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var candidates = GetActiveSourceFolderCandidates();
            return candidates.Any(candidate => string.Equals(folderName, candidate, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsImagePathInGroup(ImageGroupSet group, string path)
        {
            if (group == null || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            int required = GetRequired2DSourceCount();
            for (int i = 0; i < required; i++)
            {
                if (string.Equals(group.GetPath(i), path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreSame2DImageGroup(ImageGroupSet left, ImageGroupSet right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            int required = GetRequired2DSourceCount();
            for (int i = 0; i < required; i++)
            {
                if (!string.Equals(left.GetPath(i), right.GetPath(i), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private string ResolveSourceFolder(string parentDir, int index)
        {
            foreach (var candidate in ImageSourceNaming.GetFolderCandidates(index))
            {
                var candidateDir = Path.Combine(parentDir, candidate);
                if (Directory.Exists(candidateDir))
                {
                    return candidateDir;
                }
            }

            return null;
        }

        private string GetPreferredSourceFolderName(int index)
        {
            var candidates = ImageSourceNaming.GetFolderCandidates(index);
            return candidates.FirstOrDefault() ?? $"图像源{index + 1}";
        }

        private bool HasRequired2DImages(ImageGroupSet group)
        {
            var required = GetRequired2DSourceCount();
            for (int i = 0; i < required; i++)
            {
                if (string.IsNullOrEmpty(group.GetPath(i)))
                {
                    return false;
                }
            }

            return required > 0;
        }

        /// <summary>
        /// 选择图片文件并匹配成组
        /// </summary>
        /// <summary>
        /// 异步选择图片文件（带加载框）
        /// </summary>
        private async Task<List<ImageGroupSet>> SelectImageFilesAsync()
        {
            LoadingDialog loadingDialog = null;
            try
            {
                // 使用当前存图目录作为默认搜寻目录
                string currentSaveDir = GetCurrentImageSaveDirectory();
                string ngDir = Path.Combine(currentSaveDir, "NG");
                
                // 如果NG目录存在，优先使用NG目录，否则使用当前存图目录
                string targetDir = Directory.Exists(ngDir) ? ngDir : currentSaveDir;

                var openFileDialog = new OpenFileDialog
                {
                    Title = "选择图片文件（可选择任意图像源文件夹中的文件）",
                    Filter = "图片文件 (*.bmp;*.png)|*.bmp;*.png|BMP图片文件 (*.bmp)|*.bmp|PNG图片文件 (*.png)|*.png|所有文件 (*.*)|*.*",
                    Multiselect = true,
                    InitialDirectory = targetDir
                };

                // LogUpdate($"文件选择目录: {openFileDialog.InitialDirectory}"); // 客户日志：技术路径不显示

                if (openFileDialog.ShowDialog() == true)
                {
                    var selectedFiles = openFileDialog.FileNames.ToList();
                    // LogUpdate($"用户选择了 {selectedFiles.Count} 个文件"); // 客户日志：技术细节不显示

                    // 如果选择了较多文件，显示加载对话框
                    if (selectedFiles.Count > 6)
                    {
                        loadingDialog = new LoadingDialog("正在匹配图片组，请稍候...");
                        loadingDialog.Show();
                        
                        // 让UI完全渲染弹窗
                        await Task.Delay(100);
                        
                        // 强制UI刷新
                        Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                    }

                    // 在后台线程执行图片匹配（不调用LogUpdate避免频繁UI更新）
                    var imageGroups = await Task.Run(() => MatchImageGroupsQuiet(selectedFiles));

                    // 匹配完成后在UI线程更新日志
                    LogUpdate($"共匹配到 {imageGroups?.Count ?? 0} 个有效图片组");

                    return imageGroups;
                }

                return null;
            }
            catch (Exception ex)
            {
                LogUpdate($"选择图片文件失败: {ex.Message}");
                return null;
            }
            finally
            {
                // 确保关闭加载对话框
                if (loadingDialog != null)
                {
                    await Task.Delay(200); // 短暂延时确保后台任务完成
                    loadingDialog.Close();
                }
            }
        }

        private List<ImageGroupSet> SelectImageFiles()
        {
            try
            {
                // 使用当前存图目录作为默认搜寻目录
                string currentSaveDir = GetCurrentImageSaveDirectory();
                string ngDir = Path.Combine(currentSaveDir, "NG");
                
                // 如果NG目录存在，优先使用NG目录，否则使用当前存图目录
                string targetDir = Directory.Exists(ngDir) ? ngDir : currentSaveDir;

                var openFileDialog = new OpenFileDialog
                {
                    Title = "选择图片文件（可选择任意图像源文件夹中的文件）",
                    Filter = "图片文件 (*.bmp;*.png)|*.bmp;*.png|BMP图片文件 (*.bmp)|*.bmp|PNG图片文件 (*.png)|*.png|所有文件 (*.*)|*.*",
                    Multiselect = true,
                    InitialDirectory = targetDir
                };

                // LogUpdate($"文件选择目录: {openFileDialog.InitialDirectory}"); // 客户日志：技术路径不显示

                if (openFileDialog.ShowDialog() == true)
                {
                    var selectedFiles = openFileDialog.FileNames.ToList();
                    // LogUpdate($"用户选择了 {selectedFiles.Count} 个文件"); // 客户日志：技术细节不显示

                    return MatchImageGroups(selectedFiles);
                }

                return null;
            }
            catch (Exception ex)
            {
                LogUpdate($"选择图片文件失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 静默匹配图片组（后台线程专用，不调用LogUpdate）
        /// </summary>
        private List<ImageGroupSet> MatchImageGroupsQuiet(List<string> selectedFiles)
        {
            var imageGroups = new List<ImageGroupSet>();

            try
            {
                // **修复：支持5张图片索引 - 按照父目录分组（包含2D和3D图片）**
                var groupedByParent = selectedFiles
                    .Where(IsImageSourceFile) // **支持动态图像源 + 3D图片**
                    .GroupBy(file => 
                    {
                        var dir = Path.GetDirectoryName(file);
                        return Path.GetDirectoryName(dir); // 获取父目录
                    })
                    .ToList();

                foreach (var group in groupedByParent)
                {
                    var parentDir = group.Key;
                    var sourceFiles = group.ToList();

                    // 从用户选择的文件中提取数字后缀
                    var suffixes = ExtractUniqueSuffixesQuiet(sourceFiles);
                    
                    foreach (var suffix in suffixes)
                    {
                        // 为每个后缀尝试创建图片组
                        var imageGroup = CreateImageGroupBySuffixQuiet(parentDir, suffix);
                        if (imageGroup != null && imageGroup.IsValid)
                        {
                            // 检查是否已经添加过这个组（避免重复）
                            var existing = imageGroups.FirstOrDefault(g => AreSame2DImageGroup(g, imageGroup));
                            
                            if (existing == null)
                            {
                                imageGroups.Add(imageGroup);
                            }
                        }
                    }
                }

                return imageGroups;
            }
            catch (Exception ex)
            {
                // 在后台线程中不调用LogUpdate，避免UI更新
                return new List<ImageGroupSet>();
            }
        }

        /// <summary>
        /// 匹配图片组（支持从任意图像源文件夹选择文件）
        /// </summary>
        private List<ImageGroupSet> MatchImageGroups(List<string> selectedFiles)
        {
            var imageGroups = new List<ImageGroupSet>();

            try
            {
                // **修复：支持5张图片索引 - 按照父目录分组（包含2D和3D图片）**
                var groupedByParent = selectedFiles
                    .Where(IsImageSourceFile) // **支持动态图像源 + 3D图片**
                    .GroupBy(file => 
                    {
                        var dir = Path.GetDirectoryName(file);
                        return Path.GetDirectoryName(dir); // 获取父目录
                    })
                    .ToList();

                foreach (var group in groupedByParent)
                {
                    var parentDir = group.Key;
                    var sourceFiles = group.ToList();

                    // LogUpdate($"处理目录: {Path.GetFileName(parentDir)}，包含 {sourceFiles.Count} 个图像源文件"); // 客户日志：技术细节不显示

                    // 从用户选择的文件中提取数字后缀
                    var suffixes = ExtractUniqueSuffixes(sourceFiles);
                    
                    foreach (var suffix in suffixes)
                    {
                        // 为每个后缀尝试创建图片组
                        var imageGroup = CreateImageGroupBySuffix(parentDir, suffix);
                        if (imageGroup != null && imageGroup.IsValid)
                        {
                            // 检查是否已经添加过这个组（避免重复）
                            var existing = imageGroups.FirstOrDefault(g => AreSame2DImageGroup(g, imageGroup));
                            
                            if (existing == null)
                            {
                                imageGroups.Add(imageGroup);
                                // LogUpdate($"成功匹配图片组: {imageGroup.BaseName} (后缀: {suffix})"); // 客户日志：技术细节不显示
                            }
                        }
                    }
                }

                LogUpdate($"共匹配到 {imageGroups.Count} 个有效图片组");
                return imageGroups;
            }
            catch (Exception ex)
            {
                LogUpdate($"匹配图片组失败: {ex.Message}");
                return new List<ImageGroupSet>();
            }
        }

        /// <summary>
        /// 静默提取数字后缀（后台线程专用）
        /// </summary>
        private List<string> ExtractUniqueSuffixesQuiet(List<string> selectedFiles)
        {
            var suffixes = new HashSet<string>();
            
            foreach (var file in selectedFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var match = Regex.Match(fileName, @"^.+(_\d+)$"); // 匹配 _数字 的模式
                if (match.Success)
                {
                    suffixes.Add(match.Groups[1].Value); // 添加后缀，如 "_1", "_2", "_3"
                }
            }
            
            return suffixes.ToList();
        }

        /// <summary>
        /// 从选择的文件中提取唯一的数字后缀
        /// </summary>
        private List<string> ExtractUniqueSuffixes(List<string> selectedFiles)
        {
            var suffixes = new HashSet<string>();
            
            foreach (var file in selectedFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var match = Regex.Match(fileName, @"^.+(_\d+)$"); // 匹配 _数字 的模式
                if (match.Success)
                {
                    suffixes.Add(match.Groups[1].Value); // 添加后缀，如 "_1", "_2", "_3"
                }
            }
            
                            // LogUpdate($"从选择文件中提取到后缀: {string.Join(", ", suffixes)}"); // 客户日志：技术细节不显示
            return suffixes.ToList();
        }

        /// <summary>
        /// 静默创建图片组（后台线程专用，不调用LogUpdate，包含2D和3D图片）
        /// </summary>
        private ImageGroupSet CreateImageGroupBySuffixQuiet(string parentDir, string suffix)
        {
            try
            {
                int requiredSources = GetRequired2DSourceCount();
                string baseName = "";
                var imageGroup = new ImageGroupSet();

                for (int i = 0; i < requiredSources; i++)
                {
                    var sourceDir = ResolveSourceFolder(parentDir, i);
                    if (string.IsNullOrEmpty(sourceDir))
                    {
                        continue;
                    }

                    var sourceFiles = Directory.GetFiles(sourceDir, $"*{suffix}.bmp");
                    if (sourceFiles.Length == 0)
                    {
                        continue;
                    }

                    var selectedPath = sourceFiles[0];
                    imageGroup.SetSource(i, selectedPath);

                    if (i == 0 && string.IsNullOrEmpty(baseName))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(selectedPath);
                        var match = Regex.Match(fileName, @"^(.+)_\d+$");
                        if (match.Success)
                        {
                            baseName = match.Groups[1].Value;
                        }
                    }
                }

                // **修复：支持5张图片索引 - 优先创建2D图片组，否则尝试仅3D图片组（静默版本）**
                bool has2DImages = HasRequired2DImages(imageGroup);

                // 先尝试创建图片组（无论是否有完整2D图片）
                imageGroup.BaseName = string.IsNullOrEmpty(baseName)
                    ? $"{Path.GetFileName(parentDir)}{suffix}"
                    : $"{baseName}{suffix}";

                // 静默查找对应的3D图片（在同级目录的3D文件夹中）
                Find3DImagesForGroupQuiet(parentDir, suffix, imageGroup);

                // **修复：如果有完整的2D图片或有3D图片，则返回图片组**
                if (has2DImages || imageGroup.Has3DImages)
                {
                    return imageGroup;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 根据指定的数字后缀创建图片组（包含2D和3D图片）
        /// </summary>
        private ImageGroupSet CreateImageGroupBySuffix(string parentDir, string suffix)
        {
            try
            {
                int requiredSources = GetRequired2DSourceCount();
                string baseName = "";
                var imageGroup = new ImageGroupSet();

                for (int i = 0; i < requiredSources; i++)
                {
                    var sourceDir = ResolveSourceFolder(parentDir, i);
                    if (string.IsNullOrEmpty(sourceDir))
                    {
                        continue;
                    }

                    var sourceFiles = Directory.GetFiles(sourceDir, $"*{suffix}.bmp");
                    if (sourceFiles.Length == 0)
                    {
                        continue;
                    }

                    var selectedPath = sourceFiles[0];
                    imageGroup.SetSource(i, selectedPath);

                    if (i == 0 && string.IsNullOrEmpty(baseName))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(selectedPath);
                        var match = Regex.Match(fileName, @"^(.+)_\d+$");
                        if (match.Success)
                        {
                            baseName = match.Groups[1].Value;
                        }
                        // LogUpdate($"找到图像源1文件: {Path.GetFileName(selectedPath)}"); // 客户日志：技术细节不显示
                    }
                }

                // **修复：支持5张图片索引 - 优先创建2D图片组，否则尝试仅3D图片组**
                bool has2DImages = HasRequired2DImages(imageGroup);

                // 先尝试创建图片组（无论是否有完整2D图片）
                imageGroup.BaseName = string.IsNullOrEmpty(baseName)
                    ? $"{Path.GetFileName(parentDir)}{suffix}"
                    : $"{baseName}{suffix}";

                // 查找对应的3D图片（在同级目录的3D文件夹中）
                Find3DImagesForGroup(parentDir, suffix, imageGroup);

                // **修复：如果有完整的2D图片或有3D图片，则返回图片组**
                if (has2DImages || imageGroup.Has3DImages)
                {
                    if (has2DImages && imageGroup.Has3DImages)
                    {
                        LogManager.Info($"[图片匹配] 创建完整图片组（5张图片）: {imageGroup.BaseName}");
                    }
                    else if (has2DImages)
                    {
                        LogManager.Info($"[图片匹配] 创建2D图片组（{requiredSources}张图片）: {imageGroup.BaseName}");
                    }
                    else if (imageGroup.Has3DImages)
                    {
                        LogManager.Info($"[图片匹配] 创建3D图片组（2张图片）: {imageGroup.BaseName}");
                    }
                    
                    return imageGroup;
                }
                else
                {
                    LogManager.Info($"[图片匹配] 目录 {Path.GetFileName(parentDir)} 中未找到有效的图片组 (后缀: {suffix})");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"根据后缀创建图片组失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 执行当前图片组的检测
        /// </summary>
        private async Task ExecuteCurrentImageGroup()
        {
            try
            {
                var currentGroup = _imageTestManager.CurrentGroup;
                if (currentGroup == null)
                {
                    LogUpdate("当前没有有效的图片组");
                    return;
                }
                
                // 配置算法流程和模块
                await ConfigureAndExecuteDetection(currentGroup);
            }
            catch (Exception ex)
            {
                LogUpdate($"执行图片组检测失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 配置算法流程和模块，并执行检测（支持2D+3D联合检测）
        /// </summary>
        private async Task ConfigureAndExecuteDetection(ImageGroupSet imageGroup)
        {
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _lastExecutedImageGroup = imageGroup;
                // 清空缓存
                Clear2DDataCache();
                Clear3DDataCache();
                
                var engine = ResolveAlgorithmEngine();
                var algorithmInput = BuildAlgorithmInput(imageGroup);
                TrackAlgorithmExecution(engine, algorithmInput);
                
                // 检查3D启用状态
                //LogManager.Info("========== 开始3D启用状态判断 ==========");
                
                bool is3DDetectionEnabled = Is3DDetectionEnabled();
                bool hasImages = imageGroup.Has3DImages;
                bool shouldExecute3D = is3DDetectionEnabled && hasImages;
                
                //LogManager.Info($"[3D启用判断] Is3DDetectionEnabled(): {is3DDetectionEnabled}");
                //LogManager.Info($"[3D启用判断] imageGroup.Has3DImages: {hasImages}");
                //LogManager.Info($"[3D启用判断] 最终shouldExecute3D: {shouldExecute3D}");
                
                //if (hasImages)
                //{
                //    LogManager.Info($"[3D启用判断] 3D图像路径: Height={imageGroup.HeightImagePath}, Gray={imageGroup.GrayImagePath}");
                //}
                //else
                //{
                //    LogManager.Warning("[3D启用判断] ⚠️ 当前图像组没有3D图像");
                //}
                
                //LogManager.Info("========== 3D启用状态判断完成 ==========");
                
                // 启动统一检测管理器
                _detectionManager.StartDetectionCycle(shouldExecute3D);
                //LogUpdate($"开始检测周期: {imageGroup.BaseName} - {_detectionManager.GetStatusDescription()}");
                
                // 5. 重置旧的状态标志（保持兼容性）
                Reset2DDetectionFlag();
                // 🔧 移除锁：直接操作
                _3DCompletionTime = null;

                Task task2D = ExecuteAlgorithmEngineDetectionAsync(engine, algorithmInput);

                // 7. 启动3D检测任务
                Task<bool> task3D = null;
                if (shouldExecute3D)
                {
                    // 🔧 修复：移除重复的日志输出，Execute3DDetection方法内会输出详细日志
                    
                    // 设置图片测试模式标识
                    ThreeDSettings.IsInImageTestMode = true;
                    
                    task3D = Task.Run(async () =>
                    {
                        try
                        {
                            bool result = await Execute3DDetection(imageGroup.HeightImagePath, imageGroup.GrayImagePath);
                            
                            // 🔧 架构修复：3D回调会自动调用统一检测管理器的Mark3DCompleted方法
                            // 移除误导性日志，因为3D回调通常比此异步任务执行更快
                            
                            return result;
                        }
                        catch (Exception ex)
                        {
                            LogUpdate($"3D检测失败: {ex.Message}");
                            return false;
                        }
                        finally
                        {
                            // 重置图片测试模式标识
                            ThreeDSettings.IsInImageTestMode = false;
                        }
                    });
                }

                // 8. 等待检测任务启动，但不在这里判断检测周期完成
                // 🔧 关键修复：移除过早的完成判断，让统一检测管理器控制检测周期
                if (shouldExecute3D && task3D != null)
                {
                    // 等待2D和3D任务启动
                    await Task.WhenAll(task2D, task3D);
                    bool result3D = await task3D;
                    
                    LogUpdate($"2D和3D检测任务完成，等待回调处理: {imageGroup.BaseName}");
                }
                else
                {
                    // 等待2D任务启动
                    await task2D;
                    LogUpdate($"2D检测任务完成，等待结果处理: {imageGroup.BaseName}");
                }
                
                // 🔧 重要：检测周期的完成由统一检测管理器在算法回调中判断
                // 不在这里输出"检测周期完成"，避免时序混乱
            }
            catch (Exception ex)
            {
                LogUpdate($"配置检测失败: {ex.Message}");
                _detectionManager.Reset();
            }
        }

        private IAlgorithmEngine ResolveAlgorithmEngine()
        {
            string preferredEngineId = TemplateConfigPage.Instance?.CurrentAlgorithmEngineId;
            if (string.IsNullOrWhiteSpace(preferredEngineId))
            {
                preferredEngineId = AlgorithmEngineSettingsManager.PreferredEngineId;
            }
            if (string.IsNullOrWhiteSpace(preferredEngineId))
            {
                preferredEngineId = AlgorithmEngineIds.OpenCvOnnx;
            }
            var engine = AlgorithmEngineRegistry.ResolveEngine(preferredEngineId);
            if (!string.Equals(engine.EngineId, preferredEngineId, StringComparison.OrdinalIgnoreCase))
            {
                LogUpdate($"算法引擎 {preferredEngineId} 不可用，已回退至 {engine.EngineName}");
            }

            return engine;
        }

        public async Task ExecuteAlgorithmPipelineForImageGroup(ImageGroupSet imageGroup, bool isTemplateConfig = false)
        {
            if (imageGroup == null)
            {
                LogUpdate("图像组为空，无法执行算法引擎");
                return;
            }

            try
            {
                _lastExecutedImageGroup = imageGroup;
                var engine = ResolveAlgorithmEngine();
                var algorithmInput = BuildAlgorithmInput(imageGroup);
                TrackAlgorithmExecution(engine, algorithmInput);
                await ExecuteAlgorithmEngineDetectionAsync(engine, algorithmInput);

                if (isTemplateConfig)
                {
                    LogUpdate($"模板配置触发算法引擎完成: {engine.EngineName}");
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"算法引擎执行失败: {ex.Message}");
            }
        }

        private AlgorithmInput BuildAlgorithmInput(ImageGroupSet imageGroup)
        {
            var input = new AlgorithmInput
            {
                TemplateName = CurrentTemplateName,
                LotNumber = CurrentLotValue,
                ImageNumber = GetCurrentImageNumberForRecord()
            };

            var template = _templateOverride ?? TryLoadCurrentTemplateParameters();
            if (template != null)
            {
                var profile = TemplateHierarchyConfig.Instance.ResolveProfile(template.ProfileId);
                input.Parameters["TemplateProfileId"] = template.ProfileId ?? string.Empty;
                input.Parameters["TemplateProfileName"] = profile?.DisplayName ?? template.ProfileId ?? string.Empty;
                input.Parameters["SampleType"] = template.SampleType.ToString();
                input.Parameters["CoatingType"] = template.CoatingType.ToString();
                PopulateAlgorithmInputParameters(input, template);
            }

            // 注入算法全局变量
            AlgorithmGlobalVariables.AppendTo(input.Parameters);

            if (imageGroup != null)
            {
                int requiredSources = GetRequired2DSourceCount();
                var sources = ImageSourceNaming.GetActiveImageSources();
                var displayNames = ImageSourceNaming.GetDisplayNames();

                input.Parameters["ImageSourceCount"] = requiredSources.ToString();
                for (int i = 0; i < requiredSources; i++)
                {
                    string name = i < displayNames.Count ? displayNames[i] : null;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        input.Parameters[$"ImageSourceName{i + 1}"] = name;
                    }
                }

                for (int i = 0; i < requiredSources; i++)
                {
                    var sourcePath = imageGroup.GetPath(i);
                    if (string.IsNullOrWhiteSpace(sourcePath))
                    {
                        continue;
                    }

                    input.ImagePaths[$"Source{i + 1}"] = sourcePath;

                    if (i < sources.Count)
                    {
                        var id = sources[i]?.Id;
                        if (!string.IsNullOrWhiteSpace(id) && !input.ImagePaths.ContainsKey(id))
                        {
                            input.ImagePaths[id] = sourcePath;
                        }
                    }

                    if (i == 0 && !input.ImagePaths.ContainsKey("Image1"))
                    {
                        input.ImagePaths["Image1"] = sourcePath;
                    }
                    else if (i == 1 && !input.ImagePaths.ContainsKey("Image2"))
                    {
                        input.ImagePaths["Image2"] = sourcePath;
                    }
                }

                if (!string.IsNullOrWhiteSpace(imageGroup.HeightImagePath))
                {
                    input.ImagePaths["Height"] = imageGroup.HeightImagePath;
                }

                if (!string.IsNullOrWhiteSpace(imageGroup.GrayImagePath))
                {
                    input.ImagePaths["Gray"] = imageGroup.GrayImagePath;
                }
            }

            return input;
        }

        private TemplateParameters TryLoadCurrentTemplateParameters()
        {
            try
            {
                string templateFilePath = TemplateConfigPage.Instance?.CurrentTemplateFilePath;
                if (string.IsNullOrWhiteSpace(templateFilePath))
                {
                    string templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                    string candidate = Path.Combine(templatesDir, $"{CurrentTemplateName}.json");
                    if (File.Exists(candidate))
                    {
                        templateFilePath = candidate;
                    }
                }

                if (string.IsNullOrWhiteSpace(templateFilePath) || !File.Exists(templateFilePath))
                {
                    return null;
                }

                return TemplateParameters.LoadFromFile(templateFilePath);
            }
            catch (Exception ex)
            {
                LogManager.Warning($"读取模板参数失败: {ex.Message}");
                return null;
            }
        }

        private void PopulateAlgorithmInputParameters(AlgorithmInput input, TemplateParameters template)
        {
            if (input == null || template?.InputParameters == null)
            {
                return;
            }

            foreach (var stepEntry in template.InputParameters)
            {
                string stepPrefix = stepEntry.Key.ToString();
                if (stepEntry.Value == null)
                {
                    continue;
                }

                foreach (var paramEntry in stepEntry.Value)
                {
                    string key = $"{stepPrefix}.{paramEntry.Key}";
                    if (!input.Parameters.ContainsKey(key))
                    {
                        input.Parameters[key] = paramEntry.Value ?? string.Empty;
                    }

                    if (!input.Parameters.ContainsKey(paramEntry.Key))
                    {
                        input.Parameters[paramEntry.Key] = paramEntry.Value ?? string.Empty;
                    }
                }
            }
        }

        private void TrackAlgorithmExecution(IAlgorithmEngine engine, AlgorithmInput input)
        {
            if (engine == null)
            {
                return;
            }

            // 平台仅依赖算法接口，这里无需额外的引擎跟踪逻辑
        }

        private async Task ExecuteAlgorithmEngineDetectionAsync(IAlgorithmEngine engine, AlgorithmInput input)
        {
            try
            {
                if (engine == null)
                {
                    LogUpdate("算法引擎未初始化，无法执行2D检测");
                    return;
                }

                var result = await engine.ExecuteAsync(input, CancellationToken.None);
                var template = _templateOverride ?? TryLoadCurrentTemplateParameters();
                var normalizedResult = NormalizeAlgorithmResult(engine, result, template);
                _lastRenderResult = normalizedResult;
                _lastAlgorithmResult = normalizedResult;
                ApplyAlgorithmResultTo2DCache(normalizedResult);
            }
            catch (Exception ex)
            {
                LogUpdate($"算法引擎执行异常: {ex.Message}");
            }
        }

        private void ApplyAlgorithmResultTo2DCache(AlgorithmResult result)
        {
            var items = new List<DetectionItem>();
            int rowIndex = 1;

            foreach (var measurement in result.Measurements)
            {
                items.Add(new DetectionItem
                {
                    RowNumber = rowIndex++,
                    Name = measurement.Name,
                    Value = measurement.ValueText ?? measurement.Value.ToString("F3"),
                    LowerLimit = measurement.LowerLimit == double.MinValue ? string.Empty : measurement.LowerLimit.ToString("F3"),
                    UpperLimit = measurement.UpperLimit == double.MaxValue ? string.Empty : measurement.UpperLimit.ToString("F3"),
                    IsOutOfRange = measurement.IsOutOfRange,
                    ToolIndex = measurement.ToolIndex
                });
            }

            SetCached2DDetectionResult(result.DefectType ?? "OpenCV结果缺失");
            Set2DDetectionCompleted();
            SetCached2DItems(items);

            LogUpdate($"算法引擎检测完成: {result.EngineId} - {(result.IsOk ? "OK" : "NG")} - {result.DefectType}");

            RefreshRenderPreviews();
        }

        private void RefreshRenderPreviews()
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(RefreshRenderPreviews);
                    return;
                }

                UpdateRenderSelectionOptions();
                UpdateRenderPreview(RenderPreviewMain, _renderMainSelectionKey);
                UpdateRenderPreview(RenderPreviewStep, _renderStepSelectionKey);
            }
            catch (Exception ex)
            {
                LogUpdate($"刷新渲染预览失败: {ex.Message}");
            }
        }

        private void UpdateRenderSelectionOptions()
        {
            if (RenderMainSelector == null || RenderStepSelector == null)
            {
                return;
            }

            _renderSelectionOptions.Clear();
            var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var displayNames = ImageSourceNaming.GetDisplayNames();
            var previewGroup = ResolvePreviewImageGroup();
            if (previewGroup != null)
            {
                int count = ImageSourceNaming.GetActiveSourceCount();
                if (count <= 0)
                {
                    count = 1;
                }

                for (int i = 0; i < count; i++)
                {
                    string name = i < displayNames.Count ? displayNames[i] : $"图像{i + 1}";
                    string path = previewGroup.GetPath(i);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    AddRenderSelectionOption(usedKeys, $"Original.{i}", $"原图-{name}");
                }
            }

            if (_lastAlgorithmResult?.RenderImages != null)
            {
                foreach (var entry in _lastAlgorithmResult.RenderImages)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    if (entry.Value == null || entry.Value.Length == 0)
                    {
                        continue;
                    }

                    var normalizedKey = NormalizeRenderSelectionKey(entry.Key);
                    if (string.IsNullOrWhiteSpace(normalizedKey))
                    {
                        continue;
                    }

                    AddRenderSelectionOption(usedKeys, normalizedKey, normalizedKey);
                }
            }

            if (_renderSelectionOptions.Count == 0)
            {
                AddRenderSelectionOption(usedKeys, "Render.Composite", "Render.Composite");
            }

            _renderMainSelectionKey = ResolveSelectionKey(_renderMainSelectionKey, "Render.Composite");
            _renderStepSelectionKey = ResolveSelectionKey(_renderStepSelectionKey, "Render.Preprocess");

            RenderMainSelector.ItemsSource = _renderSelectionOptions;
            RenderMainSelector.DisplayMemberPath = nameof(RenderSelectionOption.DisplayName);
            RenderMainSelector.SelectedValuePath = nameof(RenderSelectionOption.Key);
            RenderMainSelector.SelectedValue = _renderMainSelectionKey;

            RenderStepSelector.ItemsSource = _renderSelectionOptions;
            RenderStepSelector.DisplayMemberPath = nameof(RenderSelectionOption.DisplayName);
            RenderStepSelector.SelectedValuePath = nameof(RenderSelectionOption.Key);
            RenderStepSelector.SelectedValue = _renderStepSelectionKey;
        }

        private void AddRenderSelectionOption(ISet<string> usedKeys, string key, string displayName)
        {
            if (string.IsNullOrWhiteSpace(key) || usedKeys.Contains(key))
            {
                return;
            }

            usedKeys.Add(key);
            _renderSelectionOptions.Add(new RenderSelectionOption
            {
                Key = key,
                DisplayName = displayName
            });
        }

        private static string NormalizeRenderSelectionKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            string normalized = key.Trim();
            bool changed;
            do
            {
                changed = false;
                if (normalized.StartsWith("Result.", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring("Result.".Length);
                    changed = true;
                }
                if (normalized.StartsWith("OpenCV.", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring("OpenCV.".Length);
                    changed = true;
                }
                if (normalized.StartsWith("ONNX.", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring("ONNX.".Length);
                    changed = true;
                }
            }
            while (changed);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (!normalized.Contains(".") && !normalized.StartsWith("Render", StringComparison.OrdinalIgnoreCase))
            {
                normalized = $"Render.{normalized}";
            }

            return normalized;
        }


        private string ResolveSelectionKey(string currentKey, string fallbackKey)
        {
            if (!string.IsNullOrWhiteSpace(currentKey) &&
                _renderSelectionOptions.Any(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
            {
                return currentKey;
            }

            if (!string.IsNullOrWhiteSpace(fallbackKey) &&
                _renderSelectionOptions.Any(o => string.Equals(o.Key, fallbackKey, StringComparison.OrdinalIgnoreCase)))
            {
                return fallbackKey;
            }

            return _renderSelectionOptions.FirstOrDefault()?.Key;
        }

        private void UpdateRenderPreview(WpfApp2.UI.Controls.ImageInspectionViewer viewer, string selectionKey)
        {
            if (viewer == null)
            {
                return;
            }

            var bytes = ResolveRenderBytes(selectionKey);
            if (bytes != null && bytes.Length > 0)
            {
                viewer.LoadImage(bytes);
                return;
            }

            var path = ResolveRenderPath(selectionKey);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                viewer.Clear();
                return;
            }

            viewer.LoadImage(path);
        }

        private byte[] ResolveRenderBytes(string selectionKey)
        {
            if (string.IsNullOrWhiteSpace(selectionKey))
            {
                return null;
            }

            if (selectionKey.StartsWith("Original.", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (_lastAlgorithmResult?.RenderImages == null)
            {
                return null;
            }

            var normalizedKey = NormalizeRenderSelectionKey(selectionKey) ?? selectionKey;
            foreach (var key in BuildRenderKeyCandidates(normalizedKey))
            {
                if (_lastAlgorithmResult.RenderImages.TryGetValue(key, out var bytes) && bytes != null && bytes.Length > 0)
                {
                    return bytes;
                }
            }

            return null;
        }

        private string ResolveRenderPath(string selectionKey)
        {
            if (string.IsNullOrWhiteSpace(selectionKey))
            {
                return null;
            }

            if (selectionKey.StartsWith("Original.", StringComparison.OrdinalIgnoreCase))
            {
                var previewGroup = ResolvePreviewImageGroup();
                if (previewGroup == null)
                {
                    return null;
                }

                var parts = selectionKey.Split('.');
                if (parts.Length == 2 && int.TryParse(parts[1], out int index))
                {
                    return previewGroup.GetPath(index);
                }
            }

            return null;
        }

        private IEnumerable<string> BuildRenderKeyCandidates(string selectionKey)
        {
            var candidates = new List<string>();
            if (string.IsNullOrWhiteSpace(selectionKey))
            {
                return candidates;
            }

            string normalized = selectionKey.Trim();
            if (!normalized.Contains(".") && !normalized.StartsWith("Render", StringComparison.OrdinalIgnoreCase))
            {
                normalized = $"Render.{normalized}";
            }

            AddRenderKeyCandidate(candidates, normalized);
            AddRenderKeyCandidate(candidates, $"Result.{normalized}");
            AddRenderKeyCandidate(candidates, $"OpenCV.{normalized}");
            AddRenderKeyCandidate(candidates, $"ONNX.{normalized}");
            AddRenderKeyCandidate(candidates, $"Result.OpenCV.{normalized}");
            AddRenderKeyCandidate(candidates, $"Result.ONNX.{normalized}");

            return candidates;
        }

        private ImageGroupSet ResolvePreviewImageGroup()
        {
            if (_lastExecutedImageGroup != null)
            {
                return _lastExecutedImageGroup;
            }

            var currentGroup = _imageTestManager?.CurrentGroup;
            if (currentGroup != null)
            {
                return currentGroup;
            }

            return GetLastTestImageGroup();
        }

        private static void AddRenderKeyCandidate(ICollection<string> candidates, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || candidates.Contains(key))
            {
                return;
            }

            candidates.Add(key);
        }


        private void RenderMainSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RenderMainSelector?.SelectedValue is string key)
            {
                _renderMainSelectionKey = key;
                UpdateRenderPreview(RenderPreviewMain, _renderMainSelectionKey);
            }
        }

        private void RenderStepSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RenderStepSelector?.SelectedValue is string key)
            {
                _renderStepSelectionKey = key;
                UpdateRenderPreview(RenderPreviewStep, _renderStepSelectionKey);
            }
        }

        private AlgorithmResult NormalizeAlgorithmResult(IAlgorithmEngine engine, AlgorithmResult result, TemplateParameters template)
        {
            var placeholderMeasurements = BuildPlaceholderMeasurementsFromTemplate(template);
            var normalized = new AlgorithmResult
            {
                EngineId = engine?.EngineId ?? AlgorithmEngineIds.OpenCvOnnx,
                EngineVersion = engine?.EngineVersion ?? "unknown",
                Status = result?.Status ?? AlgorithmExecutionStatus.Success,
                IsOk = result?.IsOk ?? true,
                DefectType = string.IsNullOrWhiteSpace(result?.DefectType) ? "良品" : result.DefectType,
                Description = result?.Description ?? "参数对齐占位结果",
                ErrorMessage = result?.ErrorMessage
            };

            normalized.DebugInfo["EngineName"] = engine?.EngineName ?? "unknown";
            normalized.DebugInfo["TemplateName"] = template?.TemplateName ?? string.Empty;
            if (result?.DebugInfo != null)
            {
                foreach (var entry in result.DebugInfo)
                {
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    normalized.DebugInfo[$"Result.{entry.Key}"] = entry.Value ?? string.Empty;
                }
            }

            if (result?.RenderImages != null)
            {
                foreach (var entry in result.RenderImages)
                {
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    normalized.RenderImages[$"Result.{entry.Key}"] = entry.Value;
                }
            }

            var engineMeasurements = result?.Measurements ?? new List<AlgorithmMeasurement>();

            if (placeholderMeasurements.Count > 0)
            {
                var merged = new List<AlgorithmMeasurement>(placeholderMeasurements.Count);
                var engineMap = new Dictionary<string, AlgorithmMeasurement>(StringComparer.OrdinalIgnoreCase);

                foreach (var measurement in engineMeasurements)
                {
                    if (!string.IsNullOrWhiteSpace(measurement?.Name))
                    {
                        engineMap[measurement.Name] = measurement;
                    }
                }

                foreach (var measurement in placeholderMeasurements)
                {
                    if (measurement == null)
                    {
                        continue;
                    }

                    if (engineMap.TryGetValue(measurement.Name, out var actual))
                    {
                        measurement.Value = actual.Value;
                        measurement.ValueText = string.IsNullOrWhiteSpace(actual.ValueText)
                            ? actual.Value.ToString("F3")
                            : actual.ValueText;
                        measurement.HasValidData = actual.HasValidData;
                        if (actual.LowerLimit != double.MinValue || actual.UpperLimit != double.MaxValue)
                        {
                            measurement.LowerLimit = actual.LowerLimit;
                            measurement.UpperLimit = actual.UpperLimit;
                        }
                        measurement.IsOutOfRange = actual.IsOutOfRange;
                        measurement.Is3DItem = actual.Is3DItem;
                    }

                    merged.Add(measurement);
                }

                foreach (var measurement in engineMeasurements)
                {
                    if (measurement == null || string.IsNullOrWhiteSpace(measurement.Name))
                    {
                        continue;
                    }

                    if (!merged.Any(item => string.Equals(item.Name, measurement.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        merged.Add(measurement);
                    }
                }

                normalized.Measurements.AddRange(merged);
            }
            else if (engineMeasurements.Count > 0)
            {
                normalized.Measurements.AddRange(engineMeasurements);
            }

            return normalized;
        }

        private List<AlgorithmMeasurement> BuildPlaceholderMeasurementsFromTemplate(TemplateParameters template)
        {
            var measurements = new List<AlgorithmMeasurement>();
            if (template == null)
            {
                return measurements;
            }

            var stepConfigurations = ModuleRegistry.GetStepConfigurations(template.ProfileId);
            int toolIndex = 1;

            foreach (var stepConfig in stepConfigurations)
            {
                template.InputParameters.TryGetValue(stepConfig.StepType, out var stepParams);

                if (stepConfig.OutputParameters != null && stepConfig.OutputParameters.Count > 0)
                {
                    foreach (var outputParam in stepConfig.OutputParameters)
                    {
                        var measurement = BuildMeasurementFromStep(stepConfig.StepType, outputParam.Name, stepParams, toolIndex++);
                        measurements.Add(measurement);
                    }
                }
                else if (stepParams != null && stepParams.Count > 0)
                {
                    foreach (var paramEntry in stepParams)
                    {
                        if (!double.TryParse(paramEntry.Value, out double value))
                        {
                            continue;
                        }

                        measurements.Add(new AlgorithmMeasurement
                        {
                            Name = $"{stepConfig.StepType}.{paramEntry.Key}",
                            Value = value,
                            ValueText = value.ToString("F3"),
                            HasValidData = true,
                            LowerLimit = double.MinValue,
                            UpperLimit = double.MaxValue,
                            IsOutOfRange = false,
                            ToolIndex = toolIndex++
                        });
                    }
                }
                else
                {
                    measurements.Add(new AlgorithmMeasurement
                    {
                        Name = $"{stepConfig.StepType}对齐",
                        Value = 0,
                        ValueText = "0",
                        HasValidData = false,
                        LowerLimit = double.MinValue,
                        UpperLimit = double.MaxValue,
                        IsOutOfRange = false,
                        ToolIndex = toolIndex++
                    });
                }
            }

            return measurements;
        }

        private AlgorithmMeasurement BuildMeasurementFromStep(StepType stepType, string outputName, Dictionary<string, string> stepParams, int toolIndex)
        {
            double? setValue = TryGetNumeric(stepParams, outputName);
            if (!setValue.HasValue)
            {
                string derivedKey = BuildDerivedSetKey(outputName);
                if (!string.IsNullOrWhiteSpace(derivedKey))
                {
                    setValue = TryGetNumeric(stepParams, derivedKey);
                }
            }

            double? tolerance = TryGetNumeric(stepParams, $"{outputName}公差");
            if (!tolerance.HasValue)
            {
                string derivedToleranceKey = BuildDerivedToleranceKey(outputName);
                if (!string.IsNullOrWhiteSpace(derivedToleranceKey))
                {
                    tolerance = TryGetNumeric(stepParams, derivedToleranceKey);
                }
            }

            double lowerLimit = double.MinValue;
            double upperLimit = double.MaxValue;
            if (setValue.HasValue && tolerance.HasValue)
            {
                lowerLimit = setValue.Value - tolerance.Value;
                upperLimit = setValue.Value + tolerance.Value;
            }
            else
            {
                var lowerKey = $"{outputName}下限";
                var upperKey = $"{outputName}上限";
                var lowerValue = TryGetNumeric(stepParams, lowerKey);
                var upperValue = TryGetNumeric(stepParams, upperKey);
                if (lowerValue.HasValue)
                {
                    lowerLimit = lowerValue.Value;
                }

                if (upperValue.HasValue)
                {
                    upperLimit = upperValue.Value;
                }
            }

            var measurement = new AlgorithmMeasurement
            {
                Name = outputName,
                Value = setValue ?? 0,
                ValueText = setValue.HasValue ? setValue.Value.ToString("F3") : string.Empty,
                HasValidData = setValue.HasValue,
                LowerLimit = lowerLimit,
                UpperLimit = upperLimit,
                IsOutOfRange = false,
                ToolIndex = toolIndex
            };

            return measurement;
        }

        private static double? TryGetNumeric(Dictionary<string, string> stepParams, string key)
        {
            if (stepParams == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (stepParams.TryGetValue(key, out var rawValue) && double.TryParse(rawValue, out var value))
            {
                return value;
            }

            foreach (var entry in stepParams)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key) &&
                    entry.Key.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    double.TryParse(entry.Value, out value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string BuildDerivedSetKey(string outputName)
        {
            if (string.IsNullOrWhiteSpace(outputName))
            {
                return null;
            }

            if (outputName.Contains("宽度"))
            {
                return outputName.Replace("宽度", "设定宽度");
            }

            if (outputName.Contains("高度"))
            {
                return outputName.Replace("高度", "设定高度");
            }

            if (outputName.Contains("长度"))
            {
                return outputName.Replace("长度", "设定长度");
            }

            if (outputName.Contains("面积"))
            {
                return outputName.Replace("面积", "设定面积");
            }

            if (outputName.Contains("直径"))
            {
                return outputName.Replace("直径", "设定直径");
            }

            if (outputName.Contains("间距"))
            {
                return outputName.Replace("间距", "设定间距");
            }

            if (outputName.Contains("角度"))
            {
                return outputName.Replace("角度", "设定角度");
            }

            return null;
        }

        private static string BuildDerivedToleranceKey(string outputName)
        {
            if (string.IsNullOrWhiteSpace(outputName))
            {
                return null;
            }

            return $"{outputName}公差";
        }

        /// <summary>
        /// 处理算法完成后的连续检测逻辑
        /// 由算法引擎完成回调调用
        /// </summary>
        public async void HandleAutoDetectionAfterCompletion()
        {
            try
            {
                // 检查是否处于连续检测模式
                if (_imageTestManager.AutoDetectionMode == AutoDetectionMode.None)
                {
                    return; // 不在连续检测模式，直接返回
                }

                // 🔧 验机模式：在移动到下一组之前，先收集当前检测结果
                if (_isValidatorMachineMode && _validatorMachineResultsWindow != null)
                {
                    var currentGroup = _imageTestManager.CurrentGroup;
                    if (currentGroup != null && currentGroup.SampleIndex >= 0 && currentGroup.CycleIndex >= 0)
                    {
                        var detectionResults = GetCurrentDetectionResults();
                        if (detectionResults != null && detectionResults.Count > 0)
                        {
                            _validatorMachineResultsWindow.SetDetectionResults(
                                currentGroup.SampleIndex,
                                currentGroup.CycleIndex,
                                detectionResults);
                            LogManager.Info($"验机结果已收集: 图号{currentGroup.SampleIndex + 1}, 第{currentGroup.CycleIndex + 1}次");
                        }
                    }
                }

                bool shouldContinue = false;
                bool moveSuccess = false;

                // 根据检测模式决定下一步动作
                switch (_imageTestManager.AutoDetectionMode)
                {
                    case AutoDetectionMode.ToFirst:
                        // 反向检测：移动到上一组
                        if (_imageTestManager.CurrentIndex > 0)
                        {
                            moveSuccess = _imageTestManager.MovePrevious();
                            shouldContinue = moveSuccess;
                        }
                        else
                        {
                            // 已经到达第一组，结束连续检测
                            LogUpdate("反向连续检测完成");
                            shouldContinue = false;
                        }
                        break;

                    case AutoDetectionMode.ToLast:
                        // 正向检测：移动到下一组
                        if (_imageTestManager.CurrentIndex < _imageTestManager.ImageGroups.Count - 1)
                        {
                            moveSuccess = _imageTestManager.MoveNext();
                            shouldContinue = moveSuccess;
                        }
                        else
                        {
                            // 已经到达最后一组，结束连续检测
                            LogUpdate("正向连续检测完成");
                            shouldContinue = false;
                        }
                        break;
                }

                if (shouldContinue)
                {
                    // 继续检测下一组
                    LogUpdate($"连续检测进行中: 第{_imageTestManager.CurrentIndex + 1}组 ({_imageTestManager.CurrentGroup?.BaseName})");
                    
                    // 更新UI状态
                    UpdateImageTestCardUI();
                    
                    // 🔧 测试模式：更新记录按钮状态
                    if (_isTestModeActive)
                    {
                        UpdateMarkButtonStatus();
                    }
                    
                    // 🔧 连续模式优化：每轮间隔50ms等待界面渲染，避免UI卡顿
                    await Task.Delay(50);
                    
                    // 强制UI刷新，确保界面更新完成
                    await Application.Current.Dispatcher.BeginInvoke(new Action(() => { }), DispatcherPriority.Render);
                    
                    // 执行下一组检测
                    await ExecuteCurrentImageGroup();
                }
                else
                {
                    // 结束连续检测
                    _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                    UpdateImageTestCardUI();

                    // 🔧 测试模式：更新记录按钮状态
                    if (_isTestModeActive)
                    {
                        UpdateMarkButtonStatus();
                    }

                    LogUpdate("连续检测已完成");

                    // 🔧 验机模式：检测到所有测试完成，显示结果分析窗口
                    if (_isValidatorMachineMode && _validatorMachineResultsWindow != null)
                    {
                        LogUpdate("验机测试全部完成，准备显示结果分析");

                        // 重置验机模式标志
                        _isValidatorMachineMode = false;

                        try
                        {
                            // 刷新并显示结果窗口
                            _validatorMachineResultsWindow.RefreshDisplay();
                            _validatorMachineResultsWindow.ShowDialog();
                            LogManager.Info($"验机结果分析窗口已显示");
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"显示验机结果分析窗口失败: {ex.Message}");
                            MessageBox.Show($"显示结果分析窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        finally
                        {
                            _validatorMachineResultsWindow = null;
                        }
                    }

                    // ?? CICD模式：连续检测完成后生成/对比CSV并保存到模板目录
                    if (_isCicdMode)
                    {
                        await HandleCicdRunCompletedAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // 出错时停止连续检测
                _imageTestManager.SetAutoDetectionMode(AutoDetectionMode.None);
                UpdateImageTestCardUI();

                // 🔧 测试模式：更新记录按钮状态
                if (_isTestModeActive)
                {
                    UpdateMarkButtonStatus();
                }

                // 🔧 验机模式：出错时清理状态
                if (_isValidatorMachineMode)
                {
                    _isValidatorMachineMode = false;
                    _validatorMachineResultsWindow = null;
                }

                // ?? CICD模式：出错时清理状态
                if (_isCicdMode)
                {
                    _isCicdMode = false;
                    _cicdRunContext = null;
                }

                LogUpdate($"连续检测过程中出错，已停止: {ex.Message}");
            }
        }

        /// <summary>
        /// 硬件配置按钮点击事件处理器
        /// </summary>
        private void HardwareConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow != null)
                {
                    // 添加空引用检查
                    if (mainWindow.frame_HardwareConfigPage == null)
                    {
                        LogUpdate("系统尚未完全初始化，请稍后重试");
                        MessageBox.Show("系统尚未完全初始化，请稍等片刻后重试", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    // 💡 硬件配置界面可以正常检测，无需重置状态
                    
                    // 使用预定义的Frame，与其他页面保持一致
                    mainWindow.ContentC.Content = mainWindow.frame_HardwareConfigPage;
                    LogUpdate("已进入硬件配置页面");
                }
                else
                {
                    LogUpdate("无法找到主窗口");
                    MessageBox.Show("无法找到主窗口，请重新启动应用程序", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"打开硬件配置页面失败: {ex.Message}");
                MessageBox.Show($"打开硬件配置页面失败: {ex.Message}\n\n如果系统刚启动，请稍等片刻后重试", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 帮助按钮点击事件处理器
        /// </summary>
        /// <summary>
        /// 显示日志按钮点击事件 - 弹出独立的LOG查看窗口，支持复制功能
        /// </summary>
        private void ShowLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用统一的日志查看器管理类
                Models.LogViewerManager.ShowLogViewer(
                    ownerWindow: Window.GetWindow(this),
                    windowTitle: "主界面",
                    logItems: listViewLog.Items,
                    clearLogAction: () => ClearLog(),
                    updateLogAction: (message) => LogUpdate(message)
                );
            }
            catch (Exception ex)
            {
                LogUpdate($"显示日志失败: {ex.Message}");
                MessageBox.Show($"显示日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 错误复位按钮点击事件处理器
        /// </summary>
        private void ErrorResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 确认操作弹窗
                MessageBoxResult result = MessageBox.Show(
                    "⚠️ 错误复位操作确认\n\n此操作将执行：\n• 加载并执行算法流程\"初始化\"\n• 设置IO为NG输出\n• 清空数据队列\n\n是否确定执行错误复位？",
                    "错误复位确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result != MessageBoxResult.Yes)
                {
                    LogUpdate("用户取消错误复位操作");
                    return;
                }

                LogUpdate("开始执行错误复位操作...");

                // 🔧 关键修复：首先重置检测管理器状态，清除所有检测完成标记
                _detectionManager?.Reset();
                LogManager.Info("[错误复位] 检测管理器状态已重置");
                LogUpdate("检测管理器状态已重置");

                // 1. 设置IO为NG输出
                _ioController.SetDetectionResult(false); // false = NG
                LogUpdate("IO已设置为NG输出");

                LogUpdate("错误复位完成，算法初始化由中间层处理");
            }
            catch (Exception ex)
            {
                LogUpdate($"错误复位操作失败: {ex.Message}");
                MessageBox.Show($"错误复位操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行错误复位（无弹窗版本）- 用于自动触发
        /// </summary>
        public void ExecuteErrorResetWithoutDialog()
        {
            try
            {
                LogUpdate("[自动复位] 开始执行错误复位操作（由2D超时自动触发）...");
                LogManager.Warning("[自动复位] 由2D超时自动触发错误复位");

                // 🔧 关键修复：首先重置检测管理器状态，清除所有检测完成标记
                _detectionManager?.Reset();
                LogManager.Info("[自动复位] 检测管理器状态已重置");
                LogUpdate("[自动复位] 检测管理器状态已重置");

                // 1. 设置IO为NG输出
                _ioController.SetDetectionResult(false); // false = NG
                LogUpdate("[自动复位] IO已设置为NG输出");

                LogUpdate("[自动复位] 错误复位完成，算法初始化由中间层处理");
            }
            catch (Exception ex)
            {
                LogUpdate($"[自动复位] 错误复位操作失败: {ex.Message}");
                LogManager.Error($"[自动复位] 错误复位操作失败: {ex.Message}");
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建自定义选择窗口
                var helpMenuWindow = CreateHelpMenuWindow();
                helpMenuWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogUpdate($"显示帮助菜单失败: {ex.Message}");
                MessageBox.Show($"显示帮助菜单失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private sealed class HelpMenuItem
        {
            public string Icon { get; }
            public string Title { get; }
            public Brush Background { get; }
            public Brush Foreground { get; }
            public Func<Task> Action { get; }

            public HelpMenuItem(string icon, string title, Brush background, Brush foreground, Func<Task> action)
            {
                Icon = icon;
                Title = title;
                Background = background;
                Foreground = foreground;
                Action = action;
            }
        }

        private Button CreateHelpMenuButton(HelpMenuItem item)
        {
            var button = new Button
            {
                Background = item.Background,
                Foreground = item.Foreground,
                Margin = new Thickness(6),
                Padding = new Thickness(6),
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            var iconBlock = new TextBlock
            {
                Text = item.Icon,
                FontSize = 34,
                Margin = new Thickness(0, 6, 0, 4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var titleBlock = new TextBlock
            {
                Text = item.Title,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            contentPanel.Children.Add(iconBlock);
            contentPanel.Children.Add(titleBlock);
            button.Content = contentPanel;

            button.Click += async (s, e) =>
            {
                if (item.Action != null)
                {
                    await item.Action();
                }
            };

            return button;
        }

        /// <summary>
        /// 创建帮助菜单窗口
        /// </summary>
        private Window CreateHelpMenuWindow()
        {
            var window = new Window
            {
                Title = "帮助菜单",
                Width = 800,
                Height = 800,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94))
            };

            var mainGrid = new Grid
            {
                Margin = new Thickness(20)
            };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题
            var titleBlock = new TextBlock
            {
                Text = $"🔧 {SystemBrandingManager.GetSystemName()} - 帮助菜单",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(titleBlock, 0);
            mainGrid.Children.Add(titleBlock);

            // 说明文字
            var descBlock = new TextBlock
            {
                Text = "请选择要执行的操作：",
                FontSize = 12,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(descBlock, 1);
            mainGrid.Children.Add(descBlock);

            var helpScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(helpScrollViewer, 2);
            mainGrid.Children.Add(helpScrollViewer);

            var helpGroupPanel = new StackPanel();
            helpScrollViewer.Content = helpGroupPanel;

            var footerPanel = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0)
            };
            footerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var closeButton = new Button
            {
                Content = "关闭",
                MinWidth = 90,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                Foreground = Brushes.White,
                Margin = new Thickness(15, 0, 0, 0)
            };
            closeButton.Click += (s, e) => window.Close();
            Grid.SetColumn(closeButton, 1);
            footerPanel.Children.Add(closeButton);

            Grid.SetRow(footerPanel, 3);
            mainGrid.Children.Add(footerPanel);

            var helpGroups = new List<(string Title, List<HelpMenuItem> Items)>
            {
                ("硬件连接与配置", new List<HelpMenuItem>
                {
                    new HelpMenuItem(
                        "📷",
                        "相机参数配置",
                        new SolidColorBrush(Color.FromRgb(108, 92, 231)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            CameraConfigButton_Click(null, null);
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "⚙️",
                        "硬件配置",
                        new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                        Brushes.Black,
                        () =>
                        {
                            window.Close();
                            HardwareConfigButton_Click(null, null);
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "🧰",
                        "设备管理",
                        new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            OpenDeviceManagementWindow();
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "🔄",
                        "PLC 初始化",
                        new SolidColorBrush(Color.FromRgb(67, 56, 202)),
                        Brushes.White,
                        async () =>
                        {
                            window.Close();
                            await InitializePLC();
                        })
                }),
                ("数据分析", new List<HelpMenuItem>
                {
                    new HelpMenuItem(
                        "📊",
                        "统计",
                        new SolidColorBrush(Color.FromRgb(142, 68, 173)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            DataAnalysisButton_Click(null, null);
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "📤",
                        "实时数据导出",
                        new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            OpenRealTimeDataExportConfigWindow();
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "📑",
                        "验收标准与 CICD",
                        new SolidColorBrush(Color.FromRgb(63, 81, 181)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            OpenCicdAcceptanceCriteriaWindow();
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "📊",
                        "CICD CSV 对比",
                        new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            ImportCicdTestCsvAndCompare();
                            return Task.CompletedTask;
                        })
                }),
                ("拓展组件", new List<HelpMenuItem>
                {
                    new HelpMenuItem(
                        "📸",
                        "定拍测试",
                        new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                        Brushes.Black,
                        () =>
                        {
                            window.Close();
                            var fixedShotWindow = new FixedShotTestWindow(this)
                            {
                                Owner = window.Owner
                            };
                            fixedShotWindow.ShowDialog();
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "🗑️",
                        "自动删图配置",
                        new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                        Brushes.Black,
                        () =>
                        {
                            window.Close();
                            OpenAutoDeleteImageWindow();
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "🔗",
                        "模板与 LOT 来源",
                        new SolidColorBrush(Color.FromRgb(0, 150, 136)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            OpenRemoteSourceSettingWindow();
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "🧩",
                        "Tray 检测组件",
                        new SolidColorBrush(Color.FromRgb(26, 188, 156)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            OpenTrayDetectionWindow();
                            return Task.CompletedTask;
                        })
                }),
                ("平台信息与帮助", new List<HelpMenuItem>
                {
                    new HelpMenuItem(
                        "📋",
                        "系统版本信息",
                        new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            ShowVersionInfo();
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "🚀",
                        "开机启动设置",
                        new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            AutoStartupManager.ManageAutoStartupSetting();
                            return Task.CompletedTask;
                        }),
                    new HelpMenuItem(
                        "🔬",
                        "系统测试",
                        new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                        Brushes.White,
                        () =>
                        {
                            window.Close();
                            OpenSystemTestWindow();
                            return Task.CompletedTask;
                        })
                })
            };

            foreach (var group in helpGroups)
            {
                var groupTitleBlock = new TextBlock
                {
                    Text = group.Title,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 10, 0, 6)
                };
                helpGroupPanel.Children.Add(groupTitleBlock);

                var groupGrid = new System.Windows.Controls.Primitives.UniformGrid
                {
                    Columns = 4,
                    Margin = new Thickness(0, 0, 0, 12)
                };

                foreach (var item in group.Items)
                {
                    groupGrid.Children.Add(CreateHelpMenuButton(item));
                }

                helpGroupPanel.Children.Add(groupGrid);
            }

            window.Content = mainGrid;
            return window;
        }

        public void ShowTrayHelpWindow()
        {
            var window = new Window
            {
                Title = "Tray 检测组件",
                Width = 860,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = Brushes.White
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(24)
            };

            scrollViewer.Content = panel;

            void AddTitle(string text)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = text,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 12)
                });
            }

            void AddSection(string title, string content)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 12, 0, 6)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 20
                });
            }

            AddTitle("Tray 检测组件说明");

            AddSection("接口 API",
                "StartTray(rows, cols, batchName)\n" +
                "UpdateResult(position, result, imagePath, time)\n" +
                "CompleteTray()\n" +
                "ResetCurrentTray()\n" +
                "GetStatistics()\n" +
                "GetHistory(limit)\n" +
                "RequestManualRetest(position)");

            AddSection("数据结构",
                "TrayData: trayId、rows、cols、batchName、createdAt、completedAt、materials\n" +
                "MaterialData: row、col、result、imagePath、detectionTime\n" +
                "TrayStatistics: totalSlots、inspectedCount、okCount、ngCount、yieldRate、defectCounts\n" +
                "TrayPosition: row/col 位置对象");

            AddSection("坐标映射规则",
                "默认蛇形映射：奇数行从左到右，偶数行从右到左。\n" +
                "position 支持 \"row_col\" 或 index（0 基），转换规则通过 TrayCoordinateMapper 实现。\n" +
                "UI 坐标以左下角为 (1,1)。");

            AddSection("缺陷状态与图标",
                "状态映射默认支持 OK / NG。\n" +
                "图标文件名：ok.png、ng.png（可配置 IconFolder 路径）。\n" +
                "若图标缺失，将使用备用颜色进行显示。");

            AddSection("示例集成",
                "var tray = trayComponent.StartTray(10, 9, \"Batch-001\");\n" +
                "trayComponent.UpdateResult(\"1_1\", \"OK\", \"c:\\\\images\\\\ok.png\", DateTime.UtcNow);\n" +
                "trayComponent.UpdateResult(\"2\", \"NG\", \"c:\\\\images\\\\ng.png\", DateTime.UtcNow);\n" +
                "var stats = trayComponent.GetStatistics();\n" +
                "var history = trayComponent.GetHistory(10);");

            window.Content = scrollViewer;
            window.ShowDialog();
        }

        private void OpenTrayDetectionWindow()
        {
            if (_trayDetectionWindow == null)
            {
                _trayDetectionWindow = new TrayDetectionWindow(this)
                {
                    Owner = Window.GetWindow(this)
                };
                _trayDetectionWindow.Closed += (_, __) => _trayDetectionWindow = null;
            }

            _trayDetectionWindow.Show();
            _trayDetectionWindow.Activate();
        }

        /// <summary>
        /// 打开实时数据导出配置窗口
        /// </summary>
        private void OpenRealTimeDataExportConfigWindow()
        {
            try
            {
                var configWindow = new RealTimeDataExportConfigWindow
                {
                    Owner = Window.GetWindow(this)
                };
                configWindow.ShowDialog();
                LogUpdate("已打开实时数据导出配置窗口");
            }
            catch (Exception ex)
            {
                LogUpdate($"打开实时数据导出配置窗口失败: {ex.Message}");
                MessageBox.Show($"打开实时数据导出配置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开验收标准与CICD窗口
        /// </summary>
        private void OpenCicdAcceptanceCriteriaWindow()
        {
            try
            {
                var window = new CicdAcceptanceCriteriaWindow(CurrentTemplateName)
                {
                    Owner = Window.GetWindow(this)
                };
                window.ShowDialog();
                LogUpdate("已打开验收标准与CICD窗口");
            }
            catch (Exception ex)
            {
                LogUpdate($"打开验收标准与CICD窗口失败: {ex.Message}");
                MessageBox.Show($"打开验收标准与CICD窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开自动删图配置窗口
        /// </summary>
        private void OpenAutoDeleteImageWindow()
        {
            try
            {
                var autoDeleteWindow = new WpfApp2.UI.AutoDeleteImageWindow();
                autoDeleteWindow.ShowDialog();
                LogUpdate("已打开自动删图配置窗口");
            }
            catch (Exception ex)
            {
                LogUpdate($"打开自动删图配置窗口失败: {ex.Message}");
                MessageBox.Show($"打开自动删图配置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开远程来源设置窗口
        /// </summary>
        private void OpenRemoteSourceSettingWindow()
        {
            try
            {
                var settingWindow = new RemoteSourceSettingWindow();
                settingWindow.ShowDialog();
                LogUpdate("已打开模板与LOT号来源设置窗口");
            }
            catch (Exception ex)
            {
                LogUpdate($"打开来源设置窗口失败: {ex.Message}");
                MessageBox.Show($"打开来源设置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化远程文件监控服务
        /// </summary>
        private void InitializeRemoteFileMonitor()
        {
            try
            {
                var service = RemoteFileMonitorService.Instance;

                // 订阅LOT变更事件
                service.OnLotChanged += OnRemoteLotChanged;
                LogManager.Info("[远程监控] 已订阅OnLotChanged事件");

                // 订阅模板变更事件
                service.OnTemplateChanged += OnRemoteTemplateChanged;
                LogManager.Info("[远程监控] 已订阅OnTemplateChanged事件");

                // 订阅模板不存在事件
                service.OnTemplateNotFound += OnRemoteTemplateNotFound;
                LogManager.Info("[远程监控] 已订阅OnTemplateNotFound事件");

                // 订阅远程文件错误事件
                service.OnRemoteFileError += OnRemoteFileError;

                // 订阅状态变更事件
                service.OnStatusChanged += (status) => LogUpdate($"[远程监控] {status}");

                // 启动服务
                service.Start();

                LogUpdate("远程文件监控服务已初始化");
                LogManager.Info($"[远程监控] 服务初始化完成, IsRunning={service.IsRunning}");
            }
            catch (Exception ex)
            {
                LogUpdate($"初始化远程文件监控服务失败: {ex.Message}");
                LogManager.Error($"初始化远程文件监控服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理远程LOT变更事件
        /// </summary>
        private void OnRemoteLotChanged(string newLotValue)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    string oldLot = CurrentLotValue;

                    // 更新LOT值
                    CurrentLotValue = newLotValue;

                    // 保存LOT值到配置文件（与手动设置一致）
                    string configFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "LotConfig.txt");
                    string configDir = System.IO.Path.GetDirectoryName(configFilePath);
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }
                    File.WriteAllText(configFilePath, newLotValue);

                    // 通知实时数据记录器LOT号变更
                    try
                    {
                        RealTimeDataLogger.Instance.SetLotNumber(newLotValue);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"更新实时数据记录器LOT号失败: {ex.Message}");
                    }

                    // 重置图号并更新所有相关算法变量
                    ResetImageNumberForNewLot();

                    // 清空当前数据（调用清空按钮的核心逻辑，但不弹窗确认）
                    ClearDataForLotChange();

                    LogUpdate($"[远程监控] LOT已自动切换: {oldLot} → {newLotValue}，数据已清空");
                    LogManager.Info($"[远程监控] LOT自动切换: {oldLot} → {newLotValue}");
                }
                catch (Exception ex)
                {
                    LogUpdate($"[远程监控] 处理LOT变更失败: {ex.Message}");
                    LogManager.Error($"处理远程LOT变更失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 处理远程模板变更事件
        /// </summary>
        private void OnRemoteTemplateChanged(string templateFilePath)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    string templateName = System.IO.Path.GetFileNameWithoutExtension(templateFilePath);
                    LogUpdate($"[远程监控] 检测到模板变更，正在加载: {templateName}");

                    // 通过TemplateConfigPage加载模板（与启动时自动加载一致）
                    if (TemplateConfigPage.Instance != null)
                    {
                        TemplateConfigPage.Instance.LoadTemplate(templateFilePath, autoExecute: true);
                        LogUpdate($"[远程监控] 模板已自动加载: {templateName}");
                        LogManager.Info($"[远程监控] 模板自动加载: {templateName}");
                    }
                    else
                    {
                        // 备用方法：通过MainWindow访问TemplateConfigPage
                        var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                        if (mainWindow?.frame2?.Content is TemplateConfigPage templateConfigPage)
                        {
                            templateConfigPage.LoadTemplate(templateFilePath, autoExecute: true);
                            LogUpdate($"[远程监控] 模板已自动加载（备用方式）: {templateName}");
                        }
                        else
                        {
                            LogUpdate($"[远程监控] 无法访问TemplateConfigPage，模板加载失败");
                            LogManager.Warning("远程监控：无法访问TemplateConfigPage");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUpdate($"[远程监控] 加载模板失败: {ex.Message}");
                    LogManager.Error($"远程监控加载模板失败: {ex.Message}");
                    var mainWindow = Application.Current.MainWindow;
                    MessageBox.Show(mainWindow, $"自动加载模板失败:\n{ex.Message}", "模板加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        /// <summary>
        /// 处理远程模板不存在事件
        /// </summary>
        private void OnRemoteTemplateNotFound(string templateName)
        {
            Dispatcher.Invoke(() =>
            {
                LogUpdate($"[远程监控] 警告：模板不存在: {templateName}");
                LogManager.Warning($"远程监控：模板不存在: {templateName}");

                // 获取主窗口作为父窗口，确保弹窗显示在最顶层
                var mainWindow = Application.Current.MainWindow;
                MessageBox.Show(
                    mainWindow,
                    $"远程配置文件指定的模板不存在：\n\n{templateName}\n\n请检查模板名称是否正确，或在本地创建该模板。",
                    "模板不存在",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        }

        /// <summary>
        /// 处理远程文件错误事件
        /// </summary>
        private void OnRemoteFileError(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                LogUpdate($"[远程监控] 错误: {errorMessage}");
                LogManager.Warning($"远程监控错误: {errorMessage}");
            });
        }

        /// <summary>
        /// 清空数据（用于LOT变更时，不弹窗确认）
        /// </summary>
        private void ClearDataForLotChange()
        {
            try
            {
                bool statisticsCleared = false;
                bool qualityDashboardCleared = false;

                // 通过静态实例引用直接访问TemplateConfigPage
                if (TemplateConfigPage.Instance != null)
                {
                    TemplateConfigPage.Instance.ClearStatistics();
                    statisticsCleared = true;
                }
                else
                {
                    // 备用方法：通过MainWindow访问TemplateConfigPage
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    if (mainWindow?.frame2?.Content is TemplateConfigPage templateConfigPage)
                    {
                        templateConfigPage.ClearStatistics();
                        statisticsCleared = true;
                    }
                }

                // 清空质量分析仪表板的数据与缓存
                qualityDashboardCleared = SmartAnalysisWindowManager.ClearAnalysisData();

                // 强制清空界面显示数据
                ClearUIDisplayData();

                // 清空日志
                listViewLog.Items.Clear();

                // 清空保存的生产统计数据文件
                if (statisticsCleared)
                {
                    try
                    {
                        ProductionStatsPersistence.ClearSavedStats();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Warning($"清空生产统计数据文件时出错: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogUpdate($"[远程监控] 清空数据时出错: {ex.Message}");
                LogManager.Error($"远程监控清空数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开系统测试窗口
        /// </summary>
        private void OpenSystemTestWindow()
        {
            try
            {
                var testWindow = new SystemTestWindow();
                testWindow.Show();
                LogUpdate("已打开系统测试窗口");
            }
            catch (Exception ex)
            {
                LogUpdate($"打开系统测试窗口失败: {ex.Message}");
                MessageBox.Show($"打开系统测试窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开设备管理窗口
        /// </summary>
        private void OpenDeviceManagementWindow()
        {
            try
            {
                var deviceWindow = new DeviceManagementWindow();
                deviceWindow.Show();
                LogUpdate("已打开设备管理窗口");
            }
            catch (Exception ex)
            {
                LogUpdate($"打开设备管理窗口失败: {ex.Message}");
                MessageBox.Show($"打开设备管理窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// PLC初始化 - 置位MR011
        /// </summary>
        private async Task InitializePLC()
        {
            try
            {
                LogUpdate("开始PLC初始化...");

                // 检查PLC连接状态
                if (!_plcController.IsConnected)
                {
                    LogUpdate("⚠️ PLC未连接，尝试重新连接...");
                    MessageBox.Show("⚠️ PLC未连接，请检查PLC配置和连接状态", "PLC初始化失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 置位MR011
                LogUpdate("正在置位MR011...");
                bool result = await _plcController.SetRelayAsync("MR011");

                if (result)
                {
                    LogUpdate("✅ PLC初始化完成：MR011已成功置位");
                    MessageBox.Show("✅ PLC初始化完成\n\nMR011已成功置位", "PLC初始化成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogUpdate("❌ PLC初始化失败：MR011置位失败");
                    MessageBox.Show("❌ PLC初始化失败\n\nMR011置位失败，请检查PLC通信状态", "PLC初始化失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"PLC初始化过程中发生错误：{ex.Message}";
                LogUpdate($"❌ {errorMsg}");
                LogManager.Error(errorMsg);
                MessageBox.Show($"❌ {errorMsg}", "PLC初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示系统版本信息
        /// </summary>
        private void ShowVersionInfo()
        {
            try
            {
                // 获取软件版本信息
                string softwareVersion = AppVersionInfo.GetSoftwareVersion();
                string buildDate = DateTime.Now.ToString("yyyy-MM-dd");
                
                // 检查开机启动状态
                string autoStartStatus = AutoStartupManager.GetAutoStartupStatusDescription();
                
                // 构建版本信息
                string versionInfo = $@"🔧 {SystemBrandingManager.GetSystemName()} - 版本信息

📋 软件信息:
   • 软件版本: {softwareVersion}
   • 构建日期: {buildDate}
   • 框架版本: .NET Framework 4.7.2

📊 系统组件:
   • ScottPlot: 5.0 (图表组件)
   • WPF: Windows Presentation Foundation
   • 3D检测: 基恩士LJ Navigator

🚀 启动设置:
   • 开机启动: {autoStartStatus}

💡 技术支持:
   • 开发团队: 博信电子
   • 联系方式: liangyh@posenele.com
   • 更新日期: {DateTime.Now:yyyy-MM-dd}";

                // 显示版本信息弹窗
                MessageBox.Show(versionInfo, "系统信息", MessageBoxButton.OK, MessageBoxImage.Information);
                
                LogUpdate("已显示系统版本信息");
            }
            catch (Exception ex)
            {
                LogUpdate($"显示版本信息失败: {ex.Message}");
                MessageBox.Show($"显示版本信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 图片保存模式相关方法

        /// <summary>
        /// 初始化图片保存设置
        /// </summary>
        public void InitializeImageSaveSettings()
        {
            InitializeImageSaveSettings(false);
        }

        /// <summary>
        /// 初始化图片保存设置
        /// </summary>
        /// <param name="forceUpdateAlgorithm">是否强制更新算法全局变量</param>
        public void InitializeImageSaveSettings(bool forceUpdateAlgorithm)
        {
            try
            {
                // 加载保存的图号
                LoadImageNumber();
                
                // 初始化开关状态（默认仅存NG）
                ImageSaveModeToggle.IsChecked = false;
                
                // 初始化或强制更新算法全局变量
                if (forceUpdateAlgorithm)
                {
                    UpdateAllImageSaveSettingsToAlgorithm();
                }
                else
                {
                    // 正常初始化时只设置基本变量
                    UpdateImageSaveModeToAlgorithm();
                    // 存图路径由算法全局变量统一管理
                }
                
                LogUpdate($"图片保存设置初始化完成{(forceUpdateAlgorithm ? "（强制更新算法变量）" : "")}");
            }
            catch (Exception ex)
            {
                LogUpdate($"初始化图片保存设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔧 图片保存模式开关点击事件（修复：添加缓存更新）
        /// </summary>
        public bool GetSaveAllImagesEnabled()
        {
            try
            {
                if (ImageSaveModeToggle == null)
                {
                    return false;
                }

                if (!Dispatcher.CheckAccess())
                {
                    return (bool)Dispatcher.Invoke(() => ImageSaveModeToggle.IsChecked == true);
                }

                return ImageSaveModeToggle.IsChecked == true;
            }
            catch
            {
                return false;
            }
        }

        public void SetSaveAllImagesEnabled(bool saveAllImages)
        {
            try
            {
                if (ImageSaveModeToggle == null)
                {
                    return;
                }

                Action apply = () =>
                {
                    ImageSaveModeToggle.IsChecked = saveAllImages;
                    UpdateImageSaveModeToAlgorithm();
                };

                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(apply);
                    return;
                }

                apply();
            }
            catch (Exception ex)
            {
                LogUpdate($"设置存图模式失败: {ex.Message}");
            }
        }

        private void ImageSaveModeToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateImageSaveModeToAlgorithm();
                
                // 🔧 关键修复：更新3D图像管理器的UI状态缓存
                
                string mode = ImageSaveModeToggle.IsChecked == true ? "存储所有图片" : "仅存储NG图片";
                LogManager.Info($"存图模式已切换: {mode}，缓存已更新");
            }
            catch (Exception ex)
            {
                LogManager.Info($"切换存图模式失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新存图方式到算法全局变量
        /// </summary>
        private void UpdateImageSaveModeToAlgorithm()
        {
            try
            {
                int saveMode = ImageSaveModeToggle.IsChecked == true ? 1 : 0; // 全存储=1，只存NG=0

                // 写入算法全局变量（与算法引擎解耦）
                AlgorithmGlobalVariables.Set("存图方式", saveMode.ToString());
                LogUpdate($"已设置算法全局变量 '存图方式' = {saveMode}");
            }
            catch (Exception ex)
            {
                LogUpdate($"设置存图方式到算法全局变量失败: {ex.Message}");
            }
        }

        // 🔧 已移除 UpdateImageSaveDirectory 方法
        // 原因：与存图参数同步冲突，造成算法全局变量"存图根目录"的竞态条件

        #region 2D检测结果缓存机制

        /// <summary>
        /// 2D检测结果缓存（避免重复读取算法全局变量）
        /// </summary>
        private static string _cached2DDetectionResult = null;
        // 🔧 移除锁：private static readonly object _2DResultCacheLock = new object();

        /// <summary>
        /// 设置2D检测结果缓存（由算法引擎结果回调调用）
        /// </summary>
        public static void SetCached2DDetectionResult(string defectType)
        {
            // 🔧 移除锁：直接操作
            _cached2DDetectionResult = defectType;
            LogManager.Info($"[2D缓存] 2D检测结果已缓存: {defectType}");
        }

        /// <summary>
        /// 获取2D检测结果缓存（用于统一判定）
        /// </summary>
        public static (bool isAvailable, string defectType) GetCached2DDetectionResult()
        {
            // 🔧 移除锁：直接操作
            if (_cached2DDetectionResult != null)
            {
                return (true, _cached2DDetectionResult);
            }
            else
            {
                return (false, "2D结果未缓存");
            }
        }

        /// <summary>
        /// 重置2D检测结果缓存（每次新的检测周期开始时调用）
        /// </summary>
        public static void ResetCached2DDetectionResult()
        {
            // 🔧 移除锁：直接操作
            _cached2DDetectionResult = null;
            LogManager.Info("[2D缓存] 2D检测结果缓存已重置");
        }

        /// <summary>
        /// 重置3D检测数据缓存（每次新的检测周期开始时调用）
        /// </summary>
        public static void ResetCached3DDetectionResult()
        {
            _cached3DItems = null;
            LogManager.Info("[3D缓存] 3D检测数据缓存已重置");
        }

        #endregion


        /// <summary>
        /// 图号自增并更新到算法变量（在算法回调后调用）
        /// </summary>
        public void IncrementAndUpdateImageNumber()
        {
            try
            {
                _currentImageNumber++;
                SaveImageNumber();
                
                LogUpdate($"图号已自增: {_currentImageNumber}");
            }
            catch (Exception ex)
            {
                LogUpdate($"图号自增失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载图号从本地文件
        /// </summary>
        private void LoadImageNumber()
        {
            try
            {
                // 确保配置目录存在
                string configDir = Path.GetDirectoryName(_imageNumberConfigFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                if (File.Exists(_imageNumberConfigFile))
                {
                    string content = File.ReadAllText(_imageNumberConfigFile);
                    if (int.TryParse(content, out int imageNumber))
                    {
                        _currentImageNumber = Math.Max(0, imageNumber); // 确保非负数
                        LogUpdate($"已加载图号: {_currentImageNumber}");
                    }
                    else
                    {
                        _currentImageNumber = 0;
                        LogUpdate("图号文件格式错误，重置为0");
                    }
                }
                else
                {
                    _currentImageNumber = 0;
                    SaveImageNumber(); // 创建初始文件
                    LogUpdate("创建新的图号文件，初始值为0");
                }
            }
            catch (Exception ex)
            {
                _currentImageNumber = 0;
                LogUpdate($"加载图号失败，重置为0: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存图号到本地文件
        /// </summary>
        private void SaveImageNumber()
        {
            try
            {
                // 确保配置目录存在
                string configDir = Path.GetDirectoryName(_imageNumberConfigFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                File.WriteAllText(_imageNumberConfigFile, _currentImageNumber.ToString());
            }
            catch (Exception ex)
            {
                LogUpdate($"保存图号失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 当LOT值改变时重置图号并更新所有相关算法全局变量
        /// </summary>
        public void ResetImageNumberForNewLot()
        {
            try
            {
                // 重置图号为0
                _currentImageNumber = 0;
                SaveImageNumber();
                
                // 更新所有相关的算法全局变量（不包括序号）
                UpdateAllImageSaveSettingsToAlgorithm();
                
                LogUpdate($"新LOT已创建，图号重置为0，算法变量已更新（序号不写入算法变量）");
            }
            catch (Exception ex)
            {
                LogUpdate($"重置图号和更新算法变量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新所有图片保存相关设置到算法全局变量
        /// </summary>
        private void UpdateAllImageSaveSettingsToAlgorithm()
        {
            try
            {
                LogUpdate("开始更新存图相关算法全局变量...");
                
                // 更新存图方式
                UpdateImageSaveModeToAlgorithm();
                
                // 移除临时目录设置，算法存图路径由SetVmSaveImageParameters统一管理
                // 不再更新存图序号到算法变量
                
                // 显示当前设置摘要
                string saveMode = ImageSaveModeToggle.IsChecked == true ? "存储所有图片" : "仅存储NG图片";
                LogUpdate($"算法变量更新完成 - 存图方式:{saveMode}");
            }
            catch (Exception ex)
            {
                LogUpdate($"更新存图相关算法变量时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前存图目录路径（用于图片测试时的默认路径）
        /// </summary>
        public string GetCurrentImageSaveDirectory()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                // 修改：不再按日期分离文件夹，直接保存到LOT号文件夹下
                // 这样同一个LOT号的数据和图片都在同一个文件夹中，提高可读性
                return Path.Combine(baseDir, "原图存储", CurrentLotValue);
            }
            catch (Exception ex)
            {
                LogUpdate($"获取当前存图目录失败: {ex.Message}");
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }



        /// <summary>
        /// 获取当前图号（供外部调用）
        /// </summary>
        public int GetCurrentImageNumber()
        {
            return _currentImageNumber;
        }

        /// <summary>
        /// 记录最新保存的图像源1文件路径（在算法存图完成后从文件系统中查找）
        /// </summary>
        private void RecordLatestSavedImageSource1Path(string finalSaveDirectory, string imageNumberStr)
        {
            try
            {
                string sourceFolderName = GetPreferredSourceFolderName(0);
                string imageSource1Dir = Path.Combine(finalSaveDirectory, sourceFolderName);
                
                // 等待一小段时间，确保算法存图完成
                Task.Delay(300).Wait();
                
                if (Directory.Exists(imageSource1Dir))
                {
                    // ⚠️ 注意：不补零后如果用 "*1.bmp" 会误匹配 "*11.bmp"、"*21.bmp"...
                    // 这里改为解析文件名中的数字并按图号精确匹配，同时兼容历史 a_0001.bmp
                    if (int.TryParse(imageNumberStr, out int targetNumber))
                    {
                        var matchingFiles = Directory.GetFiles(imageSource1Dir, "*.bmp")
                            .Where(f => ExtractImageNumber(Path.GetFileName(f)) == targetNumber)
                            .OrderByDescending(File.GetCreationTime)
                            .ToList();

                        if (matchingFiles.Any())
                        {
                            _lastSavedImageSource1Path = matchingFiles.First();
                            LogManager.Info($"[存图记录] 已记录最新{sourceFolderName}路径: {_lastSavedImageSource1Path}");
                        }
                        else
                        {
                            LogManager.Warning($"[存图记录] 未找到图号为 {targetNumber} 的{sourceFolderName}文件");
                        }
                    }
                    else
                    {
                        // 兜底：若无法解析图号，仍按旧逻辑模糊查找（可能存在误匹配）
                        var matchingFiles = Directory.GetFiles(imageSource1Dir, $"*{imageNumberStr}.bmp")
                            .OrderByDescending(File.GetCreationTime)
                            .ToList();

                        if (matchingFiles.Any())
                        {
                            _lastSavedImageSource1Path = matchingFiles.First();
                            LogManager.Info($"[存图记录] 已记录最新{sourceFolderName}路径(兜底): {_lastSavedImageSource1Path}");
                        }
                        else
                        {
                            LogManager.Warning($"[存图记录] 未找到包含后缀 {imageNumberStr} 的{sourceFolderName}文件(兜底)");
                        }
                    }
                }
                else
                {
                    LogManager.Warning($"[存图记录] {sourceFolderName}目录不存在: {imageSource1Dir}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[存图记录] 记录最新图像源1路径失败: {ex.Message}");
            }
        }

        #endregion

        #region 数据队列管理相关方法

        /// <summary>
        /// 数据队列清空按钮点击事件处理器
        /// </summary>
        private void DataQueueClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogUpdate("存图队列功能未接入算法中间层");
                MessageBox.Show("存图队列功能未接入算法中间层。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogUpdate($"处理数据队列清空操作时出错: {ex.Message}");
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 显示模式切换相关方法

        /// <summary>
        /// 初始化显示模式
        /// </summary>
        private void InitializeDisplayMode()
        {
            try
            {
                // 默认显示所有项
                _showFocusedOnly = false;
                DisplayModeToggle.IsChecked = false;
                
                // 加载关注项目配置
                LoadFocusedProjects();
                
                // 更新状态显示
                UpdateDisplayModeStatus();
                
                LogManager.Info("[显示模式] 初始化完成，默认显示所有项");
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化显示模式失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 直接从FocusedProjects.json文件加载关注项目
        /// </summary>
        private void LoadFocusedProjects()
        {
            try
            {
                if (File.Exists(_focusedProjectsFile))
                {
                    var json = File.ReadAllText(_focusedProjectsFile, Encoding.UTF8);
                    var focusedList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);
                    
                    if (focusedList != null && focusedList.Count > 0)
                    {
                        _focusedProjects = new HashSet<string>(focusedList);
                        LogManager.Info($"[显示模式] 成功加载关注项目配置: {_focusedProjects.Count} 个项目");
                    }
                    else
                    {
                        _focusedProjects = new HashSet<string>();
                        LogManager.Info("[显示模式] FocusedProjects.json文件为空，将显示所有项目");
                    }
                }
                else
                {
                    _focusedProjects = new HashSet<string>();
                    LogManager.Info("[显示模式] FocusedProjects.json文件不存在，将显示所有项目");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载FocusedProjects.json文件失败: {ex.Message}");
                _focusedProjects = new HashSet<string>();
            }
        }

        /// <summary>
        /// 显示模式切换按钮点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisplayModeToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _showFocusedOnly = DisplayModeToggle.IsChecked == true;
                
                // 重新加载关注项目配置（以防用户在数据分析界面修改了配置）
                LoadFocusedProjects();
                
                // 更新状态显示
                UpdateDisplayModeStatus();
                
                // 根据当前模式过滤并更新DataGrid显示
                ApplyDisplayModeFilter();
                
                LogManager.Info($"[显示模式] 切换到: {(_showFocusedOnly ? "显示关注项" : "显示所有项")}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"切换显示模式失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新显示模式状态文本
        /// </summary>
        private void UpdateDisplayModeStatus()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_showFocusedOnly)
                    {
                        DisplayModeStatus.Text = "显示模式：显示关注项";
                    }
                    else
                    {
                        DisplayModeStatus.Text = "显示模式：显示所有项";
                    }
                }));
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新显示模式状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用显示模式过滤
        /// </summary>
        private void ApplyDisplayModeFilter()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    IList<DetectionItem> itemsToDisplay;
                    if (_showFocusedOnly)
                    {
                        // 显示关注项模式：只显示在FocusedProjects.json中配置的项目
                        var filteredItems = _fullDataList.Where(item => 
                            _focusedProjects.Contains(item.Name)).ToList();

                        // 重新设置行号
                        for (int i = 0; i < filteredItems.Count; i++)
                        {
                            filteredItems[i].RowNumber = i + 1;
                        }

                        itemsToDisplay = filteredItems;
                        LogManager.Info($"[显示过滤] 关注项模式，显示 {filteredItems.Count}/{_fullDataList.Count} 项");
                    }
                    else
                    {
                        // 显示所有项模式：显示完整列表
                        itemsToDisplay = _fullDataList;
                        LogManager.Info($"[显示过滤] 显示所有项模式，显示 {_fullDataList.Count} 项");
                    }

                    SyncDataGridItems(itemsToDisplay);

                    // 根据显示模式调整字体大小
                    AdjustDataGridFontSize();

                    // 重新应用红色显示逻辑
                    ApplyRowColorFormatting();
                }));
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用显示模式过滤失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用行颜色格式化（根据数据状态设置所有行的背景色）
        /// </summary>
        private void ApplyRowColorFormatting()
        {
            try
            {
                // 遍历所有项目，而不仅仅是超限项目
                foreach (var item in _dataGridItems)
                {
                    var container = DataGrid1.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                    if (container != null)
                    {
                        // 检查值是否为空（null、空字符串或仅包含空白字符）
                        bool isEmpty = string.IsNullOrWhiteSpace(item.Value);

                        if (isEmpty)
                        {
                            // 设置为黄色背景（空值）
                            container.Background = new SolidColorBrush(System.Windows.Media.Colors.LightYellow);
                        }
                        else if (item.IsOutOfRange)
                        {
                            // 设置为LightCoral背景（超出范围）
                            container.Background = new SolidColorBrush(System.Windows.Media.Colors.LightCoral);
                        }
                        else
                        {
                            // 正常项目设置为白色背景
                            container.Background = new SolidColorBrush(System.Windows.Media.Colors.White);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"应用行颜色格式化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据显示模式调整DataGrid字体大小
        /// </summary>
        private void AdjustDataGridFontSize()
        {
            try
            {
                if (_showFocusedOnly)
                {
                    // 显示关注项模式：字体大小变为原来的1.5倍
                    var cellStyle = DataGrid1.Resources[typeof(DataGridCell)] as Style;
                    if (cellStyle != null)
                    {
                        // 创建新的Style，基于现有样式
                        var newCellStyle = new Style(typeof(DataGridCell), cellStyle);
                        newCellStyle.Setters.Add(new Setter(DataGridCell.FontSizeProperty, 24.0)); // 16 * 1.5 = 24
                        DataGrid1.Resources[typeof(DataGridCell)] = newCellStyle;
                    }
                    else
                    {
                        // 如果没有现有样式，创建新的
                        var newCellStyle = new Style(typeof(DataGridCell));
                        newCellStyle.Setters.Add(new Setter(DataGridCell.FontSizeProperty, 24.0));
                        DataGrid1.Resources[typeof(DataGridCell)] = newCellStyle;
                    }
                }
                else
                {
                    // 显示所有项模式：恢复原来的字体大小
                    var cellStyle = DataGrid1.Resources[typeof(DataGridCell)] as Style;
                    if (cellStyle != null)
                    {
                        // 创建新的Style，恢复原来的字体大小
                        var newCellStyle = new Style(typeof(DataGridCell), cellStyle);
                        newCellStyle.Setters.Add(new Setter(DataGridCell.FontSizeProperty, 16.0)); // 原来的字体大小
                        DataGrid1.Resources[typeof(DataGridCell)] = newCellStyle;
                    }
                    else
                    {
                        // 如果没有现有样式，创建新的
                        var newCellStyle = new Style(typeof(DataGridCell));
                        newCellStyle.Setters.Add(new Setter(DataGridCell.FontSizeProperty, 16.0));
                        DataGrid1.Resources[typeof(DataGridCell)] = newCellStyle;
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"调整DataGrid字体大小失败: {ex.Message}");
            }
        }

        #endregion

        #endregion

        /// <summary>
        /// 获取当前是否处于图片测试模式
        /// </summary>
        /// <returns>true if处于测试模式，false if处于正常检测模式</returns>
        public bool IsInImageTestMode()
        {
            return _imageTestManager?.CurrentState != ImageTestState.Idle;
        }

        /// <summary>
        /// 统一判定：整合2D和3D检测结果，生成最终判定
        /// </summary>
        /// <returns>统一判定结果(true=OK, false=NG)和优先缺陷类型</returns>
        public (bool isOK, string defectType, string description) GetUnifiedJudgement()
        {
            try
            {
                // 1. 检查2D检测是否真正完成（通过算法回调标志判断）
                bool is2DCompleted = Is2DDetectionCompleted();
                bool is2DOK = true;
                string defect2D = "";
                
                if (is2DCompleted)
                {
                    // 算法回调已发生，从缓存读取2D检测结果（避免重复读取算法全局变量）
                    var (isAvailable, cachedDefectType) = GetCached2DDetectionResult();
                    if (isAvailable)
                    {
                        defect2D = cachedDefectType;
                        is2DOK = defect2D == "良品";
                        LogUpdate($"2D检测结果（从缓存获取）: {(is2DOK ? "OK" : "NG")} - {defect2D}");
                    }
                    else
                    {
                        LogUpdate("算法回调已接收，但2D检测结果未缓存");
                        defect2D = "2D结果未缓存";
                        is2DOK = false;
                    }
                }
                else
                {
                    LogUpdate("2D检测未完成：算法回调尚未接收");
                }
                // 2. 获取3D检测结果
                bool is3DOK = true;
                string defect3D = "";
                List<string> ng3DItems = new List<string>();
                bool is3DSystemEnabled = false;
                bool is3DCompleted = false;

                try
                {
                    // 3D已解耦：以检测管理器状态为准（是否启用/是否完成），并仅消费缓存数据（由Host/IPC填充）。
                    is3DSystemEnabled = _detectionManager?.Is3DEnabled ?? false;
                    is3DCompleted = _detectionManager?.Is3DCompleted ?? false;

                    if (is3DSystemEnabled && is3DCompleted)
                    {
                        if (_cached3DItems != null && _cached3DItems.Count > 0)
                        {
                            is3DOK = !_cached3DItems.Any(item => item.IsOutOfRange);
                            ng3DItems = _cached3DItems
                                .Where(item => item.IsOutOfRange)
                                .Select(item => "[3D]" + item.Name)
                                .ToList();

                            if (ng3DItems.Count > 0)
                            {
                                defect3D = string.Join(",", ng3DItems);
                            }

                            LogUpdate($"[3D] 使用缓存数据: {(is3DOK ? "OK" : "NG")}, NG项目数: {ng3DItems.Count}");
                        }
                        else
                        {
                            // 已启用且已完成，但无数据：视为3D失败（通常是Host/加密狗/执行异常）
                            is3DOK = false;
                            defect3D = "[3D]结果缺失/执行失败";
                            LogUpdate("[3D] 已启用且已完成，但无3D数据：本次判定按3D失败处理（可通过SLIDE_DISABLE_3D屏蔽3D）");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error($"获取3D检测结果失败: {ex.Message}");
                    is3DOK = false;
                }

                // 3. 优化后的异常检查：3D检测完成但2D检测未完成
                // 新策略：立即执行正常流程，异步监控2D超时（不阻塞客户获得结果）
                if (is3DSystemEnabled && is3DCompleted && !is2DCompleted)
                {
                    LogUpdate("3D检测完成但2D未完成，继续执行3D独立判定（后台监控2D超时）");
                    // 不再阻塞等待，直接继续执行下面的逻辑
                    // 如果2D真的超时，后台定时器会异步弹窗提醒
                }

                // 4. 如果只有3D启用且未完成，继续等待（这是正常情况）
                if (is3DSystemEnabled && !is3DCompleted)
                {
                    LogUpdate("3D检测尚未完成，等待3D检测结果");
                    return (true, "等待3D检测", "等待3D检测完成");
                }

                // 5. 添加3D补偿后判定逻辑
                bool is3DCompensatedOK = true;
                string defect3DCompensated = "";
                List<string> ng3DCompensatedItems = new List<string>();

                if (is3DSystemEnabled)
                {
                    try
                    {
                        // 检查当前缓存的3D项目中是否有超限的项目（包括补偿项目）
                        if (_cached3DItems != null && _cached3DItems.Count > 0)
                        {
                            var outOfRangeItems = _cached3DItems.Where(item => item.IsOutOfRange).ToList();
                            if (outOfRangeItems.Count > 0)
                            {
                                is3DCompensatedOK = false;
                                ng3DCompensatedItems = outOfRangeItems.Select(item => item.Name).ToList();
                                defect3DCompensated = string.Join(",", ng3DCompensatedItems);
                                LogUpdate($"[3D补偿后判定] 发现{outOfRangeItems.Count}个超限项目: {defect3DCompensated}");
                            }
                            else
                            {
                                LogUpdate("[3D补偿后判定] 所有3D项目均在限值范围内");
                            }
                        }
                        else
                        {
                            LogUpdate("[3D补偿后判定] 无3D缓存数据");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUpdate($"[3D补偿后判定] 检查失败: {ex.Message}");
                        is3DCompensatedOK = false;
                        defect3DCompensated = "3D补偿后判定异常";
                    }
                }

                // 6. 添加综合项目判定逻辑（晶片平面估计等需要2D和3D都完成后才能计算的项目）
                bool isCombinedOK = true;
                string defectCombined = "";
                List<string> ngCombinedItems = new List<string>();

                try
                {
                    // 检查综合项目中是否有超限的项目
                    if (_cachedCombinedItems != null && _cachedCombinedItems.Count > 0)
                    {
                        var outOfRangeItems = _cachedCombinedItems.Where(item => item.IsOutOfRange).ToList();
                        if (outOfRangeItems.Count > 0)
                        {
                            isCombinedOK = false;
                            ngCombinedItems = outOfRangeItems.Select(item => item.Name).ToList();
                            defectCombined = string.Join(",", ngCombinedItems);
                            LogUpdate($"[综合项目判定] 发现{outOfRangeItems.Count}个超限项目: {defectCombined}");
                        }
                        else
                        {
                            LogUpdate("[综合项目判定] 所有综合项目均在限值范围内");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUpdate($"[综合项目判定] 检查失败: {ex.Message}");
                    isCombinedOK = false;
                    defectCombined = "综合项目判定异常";
                }

                // 7. 正常统一判定逻辑（2D + 3D原始判定 + 3D补偿后判定 + 综合项目判定）
                bool finalOK = is2DOK && is3DOK && is3DCompensatedOK && isCombinedOK;

                // 🔧 新增：详细的判定状态日志
                LogUpdate($"[综合判定详情] 2D判定: {(is2DOK ? "OK" : "NG")}, 3D原始判定: {(is3DOK ? "OK" : "NG")}, 3D补偿后判定: {(is3DCompensatedOK ? "OK" : "NG")}, 综合项目: {(isCombinedOK ? "OK" : "NG")} -> 最终结果: {(finalOK ? "OK" : "NG")}");
                string finalDefectType = "";
                string description = "";

                if (!finalOK)
                {
                    // 缺陷优先级：2D > 3D原始 > 3D补偿后 > 综合项目
                    if (!is2DOK)
                    {
                        finalDefectType = defect2D;
                        // 构建详细描述
                        List<string> descParts = new List<string> { $"2D: {defect2D}" };
                        if (!is3DOK && is3DSystemEnabled)
                        {
                            descParts.Add($"3D: {defect3D}");
                        }
                        if (!is3DCompensatedOK && is3DSystemEnabled)
                        {
                            descParts.Add($"3D补偿后: {defect3DCompensated}");
                        }
                        if (!isCombinedOK)
                        {
                            descParts.Add($"综合项目: {defectCombined}");
                        }
                        description = string.Join("; ", descParts);
                    }
                    else if (!is3DOK && is3DSystemEnabled)
                    {
                        // 只有3D原始判定NG的情况
                        finalDefectType = ng3DItems.Count > 0 ? ng3DItems[0] : "[3D]未知缺陷";
                        List<string> descParts = new List<string> { $"3D: {defect3D}" };
                        if (!is3DCompensatedOK)
                        {
                            descParts.Add($"3D补偿后: {defect3DCompensated}");
                        }
                        if (!isCombinedOK)
                        {
                            descParts.Add($"综合项目: {defectCombined}");
                        }
                        description = string.Join("; ", descParts);
                    }
                    else if (!is3DCompensatedOK && is3DSystemEnabled)
                    {
                        // 只有3D补偿后判定NG的情况
                        finalDefectType = ng3DCompensatedItems.Count > 0 ? ng3DCompensatedItems[0] : "[3D补偿]未知缺陷";
                        List<string> descParts = new List<string> { $"3D补偿后: {defect3DCompensated}" };
                        if (!isCombinedOK)
                        {
                            descParts.Add($"综合项目: {defectCombined}");
                        }
                        description = string.Join("; ", descParts);
                    }
                    else if (!isCombinedOK)
                    {
                        // 只有综合项目判定NG的情况
                        finalDefectType = ngCombinedItems.Count > 0 ? ngCombinedItems[0] : "综合项目未知缺陷";
                        description = $"综合项目: {defectCombined}";
                    }
                }
                else
                {
                    finalDefectType = "良品";
                    if (is3DSystemEnabled)
                    {
                        description = "2D: 良品; 3D: 良品; 3D补偿后: 良品";
                    }
                    else
                    {
                        description = "2D: 良品";
                    }
                }

                // 🔧 关键修复：GetUnifiedJudgement只是获取判定结果，不应该输出"统一判定完成"
                // 真正的统一判定只在ExecuteUnifiedJudgementAndIO中执行
                LogUpdate($"判定结果: {(finalOK ? "OK" : "NG")} - {description}");
                
                return (finalOK, finalDefectType, description);
            }
            catch (Exception ex)
            {
                LogManager.Error($"统一判定处理失败: {ex.Message}, StackTrace: {ex.StackTrace}");
                return (false, "统一判定失败", ex.Message);
            }
        }

        /// <summary>
        /// 执行统一判定并设置IO输出
        /// </summary>
        public void ExecuteUnifiedJudgementAndIO()
        {
            _ = ExecuteUnifiedJudgementAndIOAsync();
        }

        public async Task ExecuteUnifiedJudgementAndIOAsync()
        {
            try
            {
                // 🔧 新增：在统一判定前，先计算综合检测项目（如晶片平面估计）
                // 这些项目需要2D和3D数据都完成后才能计算
                CalculateCombinedDetectionItems();

                var (isOK, defectType, description) = GetUnifiedJudgement();
                
                // 检查是否为2D检测系统异常
                if (defectType == "2D检测系统异常")
                {
                    LogManager.Info("2D检测系统异常，跳过统计更新和IO输出");
                    return; // 异常情况直接返回，不执行后续操作
                }

                // 🔧 新增：测试模式检测结果记录
                if (_isTestModeActive && _testModeDataManager != null)
                {
                    try
                    {
                        var currentGroup = _imageTestManager.CurrentGroup;
                        if (currentGroup != null)
                        {
                            // 提取图片编号（文件名后缀）
                            string imageNumber = GetCurrentImageNumberForRecord();
                            
                            // 获取当前检测数据
                            var currentItems = new List<DetectionItem>();
                            // 🔧 移除锁：直接操作
                            if (_cached2DItems != null)
                                currentItems.AddRange(_cached2DItems);
                            if (_cached3DItems != null)
                                currentItems.AddRange(_cached3DItems);

                            // 创建测试结果记录
                            var testResult = new TestModeDetectionResult
                            {
                                ImagePath = currentGroup.Source1Path,
                                ImageNumber = imageNumber,
                                TestTime = DateTime.Now,
                                IsOK = isOK,
                                DefectType = defectType,
                                DetectionItems = new List<DetectionItem>(currentItems),
                                IsMarked = _testModeDataManager.IsImageMarked(currentGroup.Source1Path)
                            };

                            // 添加到测试结果缓存
                            _testModeDataManager.AddTestResult(testResult);
                            
                            // 更新记录按钮状态
                            UpdateMarkButtonStatus();
                            
                            LogManager.Info($"[测试模式] 检测结果已记录: {imageNumber} - {(isOK ? "OK" : "NG")}({defectType})");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"[测试模式] 记录检测结果失败: {ex.Message}");
                    }
                }
                
	                // 综合判定完成后复位光源SEQ指针，避免下一次触发步进错乱（仅复位指针，不重写SEQ表）
	                // 必须在IO触发前执行，防止下一检测周期已开始导致SEQ指针错位
	                // 图片检测/校准(相机调试)/模板配置等模式不应触发任何光源驱动器连接/SEQ动作
	                bool isCameraAdjustingMode = DetectionManager != null && DetectionManager.SystemState == SystemDetectionState.CameraAdjusting;
	                bool isTemplateConfigMode = DetectionManager != null && DetectionManager.SystemState == SystemDetectionState.TemplateConfiguring;
	                bool isMaintenanceMode = DetectionManager != null && DetectionManager.SystemState == SystemDetectionState.Maintenance;
	                if (!IsInImageTestMode() && !isCameraAdjustingMode && !isTemplateConfigMode && !isMaintenanceMode)
	                {
	                    try
	                    {
	                        // ExecuteUnifiedJudgementAndIO 可能在后台线程运行，使用UI Dispatcher安全访问CameraConfigPage实例
	                        Application.Current?.Dispatcher.Invoke(() =>
	                        {
	                            var mainWindow = Application.Current.MainWindow as MainWindow;
	                            if (mainWindow?.frame_CameraConfigPage?.Content is CameraConfigPage cameraConfigPage)
	                            {
	                                cameraConfigPage.ResetLightControllerSeq();
	                            }
	                            else
	                            {
	                                LogManager.Warning("未找到相机配置页实例，跳过光源SEQ复位");
	                            }
	                        });
	                    }
	                    catch (Exception resetEx)
	                    {
	                        LogManager.Warning($"综合判定后复位光源SEQ指针失败: {resetEx.Message}");
	                    }
	                }

                // 🚀 第一优先级：立即执行IO输出（同步，最先执行）
                if (!IsInImageTestMode() && !isCameraAdjustingMode && !isTemplateConfigMode && !isMaintenanceMode)
                {
                try
                {
                    _ioController.SetDetectionResult(isOK);
                    LogManager.Info($"🚀 IO输出已完成: {(isOK ? "OK" : "NG")}");

                    // 🔧 新增：通知系统测试窗口IO输出完成（真实时间测量）
                    SystemTestWindow.NotifyIOOutputCompleted();
                }
                catch (Exception ioEx)
                {
                    LogManager.Info($"设置IO输出失败: {ioEx.Message}");
                }
                }
                
                // 🎯 第二优先级：统计数据更新（业务逻辑）
                // 🔧 修复：测试模式下也要正常更新统计和饼图，只在数据存储层隔离
                if (TemplateConfigPage.Instance != null)
                {
                    TemplateConfigPage.Instance.UpdateDefectStatistics(defectType);
                }
                
                // 📊 第三优先级：数据缓存更新（必要的业务数据）
                UnifiedUpdateDataGrid();
                
                // 🔧 新增：记录超限项目到JSON文件（统一判定后记录）
                var allCurrentItems = new List<DetectionItem>();
                if (_cached2DItems != null) allCurrentItems.AddRange(_cached2DItems);
                if (_cached3DItems != null) allCurrentItems.AddRange(_cached3DItems);
                if (_cachedCombinedItems != null) allCurrentItems.AddRange(_cachedCombinedItems);
                RecordOutOfRangeItems(allCurrentItems, defectType);

                var allDetectionItems = BuildDetectionItemsSnapshot();
                PublishAlgorithmResult(BuildAlgorithmResult(isOK, defectType, description, allDetectionItems));

                // 🔧 新增：统一的2D+3D数据记录逻辑
                bool isInTemplateConfigMode = DetectionManager?.SystemState == SystemDetectionState.TemplateConfiguring;
                if (!_isTestModeActive && !isInTemplateConfigMode)
                {
                    try
                    {
                        // 只有当有检测数据时才记录
                        if (allDetectionItems.Count > 0)
                        {
                            // 获取图片序号（生产模式使用当前图号，图片测试模式从当前图片提取）
                            string imageNumber = GetCurrentImageNumberForRecord();
                            
                            // 记录到DetectionDataStorage（用于数据分析和CSV导出）
                            DetectionDataStorage.AddRecord(defectType, CurrentLotValue, allDetectionItems, imageNumber);
                            LogManager.Info($"✅ 统一记录完成：缺陷类型={defectType}, 项目数={allDetectionItems.Count}, 图片序号={imageNumber}, 2D+3D数据已合并到同一行", "数据记录");
                        }
                        else
                        {
                            LogManager.Warning("没有检测数据可记录", "数据记录");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"统一数据记录失败: {ex.Message}", "数据记录");
                    }
                }
                else
                {
                    LogManager.Info("测试模式或配置模式：跳过生产数据记录", "数据记录");
                }
                
                // 🎨 第四优先级：UI更新操作（同步执行）
                UpdateDefectType(defectType);
                
                // 🔧 修复：线程安全触发TemplateConfigPage刷新ConfigDataGrid
                try
                {
                    if (TemplateConfigPage.Instance != null)
                    {
                        // 使用Dispatcher确保在UI线程中调用
                        if (Dispatcher.CheckAccess())
                        {
                            // 已在UI线程中，直接调用
                            TemplateConfigPage.Instance.RefreshConfigDataGrid();
                        }
                        else
                        {
                            // 不在UI线程中，调度到UI线程执行
                            Dispatcher.BeginInvoke(new System.Action(() =>
                            {
                                try
                                {
                                    TemplateConfigPage.Instance?.RefreshConfigDataGrid();
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Warning($"调度ConfigDataGrid刷新失败: {ex.Message}");
                                }
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Warning($"触发ConfigDataGrid刷新失败: {ex.Message}");
                }
                
                // 💾 第五优先级：图像保存操作（🔧 修复死锁：改为异步执行）
                // 检查是否在测试模式或模板配置模式，这两种模式下跳过生产模式的图像保存
                bool isInTestMode = _isTestModeActive;
                if (!isInTestMode && !isInTemplateConfigMode)
                {
                    // 🚀 新的存图优化方案：使用算法存图流程
                    bool is3DEnabled = ThreeDSettings.Is3DDetectionEnabledEffective;
                    
                    // 判断是否需要存图（使用原有的方法）
                    bool shouldSave = ShouldSaveImages(isOK);
                    
                    if (shouldSave)
                    {
                        // 计算最终存图路径和序号
                        string safeDefectType = SanitizeFileName(defectType);
                        int currentImageNumber = GetCurrentImageNumber();
                        // 存图序号不再补零：期望 a_1 而不是 a_0001
                        string imageNumberStr = currentImageNumber.ToString();
                        string rootDirectory = GetCurrentImageSaveDirectory();
                        string finalSaveDirectory = Path.Combine(rootDirectory, safeDefectType);
                        
                        //LogManager.Info($"[存图优化] 开始存图流程 - 类型: {safeDefectType}, 序号: {imageNumberStr}");
                        
                        // 设置算法存图参数
                        SetAlgorithmSaveImageParameters(finalSaveDirectory, imageNumberStr);
                        
                        // 保存当前2D图像
                        SaveCurrent2DImages(finalSaveDirectory, imageNumberStr);
                        
                        // 🔧 记录最新保存的图像源1文件路径（用于最后一组图片功能）
                        // 在算法存图完成后，从文件系统中查找最新创建的文件
                        RecordLatestSavedImageSource1Path(finalSaveDirectory, imageNumberStr);
                        
                        // 🔧 关键修复：3D图片异步处理，避免UI线程死锁
                        if (is3DEnabled)
                        {
                            try
                            {
                                //LogManager.Info($"[3D存图] 开始3D图片保存任务（异步）");
                                
                                // 线程安全获取当前存图模式设置
                                bool currentSaveAllImages = false;
                                if (Dispatcher.CheckAccess())
                                {
                                    currentSaveAllImages = ImageSaveModeToggle?.IsChecked == true;
                                }
                                else
                                {
                                    currentSaveAllImages = (bool)Dispatcher.Invoke(() => ImageSaveModeToggle?.IsChecked == true);
                                }
                                
                                // 🔧 在统一判定后保存3D图像，根据判定结果和保存模式决定是否保存
                                try
                                {
                                    using (IThreeDService threeD = new NamedPipeThreeDService())
                                    {
                                        var saveReq = new ThreeDSaveAfterJudgementRequest
                                        {
                                            DefectType = defectType,
                                            ImageNumber = currentImageNumber,
                                            RootDirectory = finalSaveDirectory,
                                            SaveAllImages = currentSaveAllImages
                                        };

                                        if (!threeD.SaveAfterJudgement(saveReq, out string saveError, timeoutMs: 30000))
                                        {
                                            LogManager.Warning("[3D存图] Host保存失败: " + (saveError ?? "unknown"));
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogManager.Error("[3D存图] Host保存异常: " + ex.Message);
                                }                            }
                            catch (Exception ex)
                            {
                                LogManager.Error($"[3D存图] 3D图片保存失败: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        //LogManager.Info($"[存图优化] 不需要存图 - OK状态: {isOK}, 存图模式: {(ImageSaveModeToggle?.IsChecked == true ? "全部" : "仅NG")}");
                    }
                }
                else
                {
                    if (isInTestMode)
                    {
                        LogManager.Info("测试模式：跳过生产模式图片保存");
                    }
                    else if (isInTemplateConfigMode)
                    {
                        LogManager.Info("模板配置模式：跳过图片保存");
                    }
                }
                
                LogManager.Info($"✅ 统一判定流程已完成: {(isOK ? "OK" : "NG")}, 缺陷类型: {defectType}");
                
                // 🚨 新增：自动告警检查（在统一判定完成后）
                if (!_isTestModeActive && !isInTemplateConfigMode)
                {
                    try
                    {
                        // 延迟执行告警检查，确保数据更新完成
                        Dispatcher.BeginInvoke(new System.Action(() =>
                        {
                            try
                            {
                                SmartAnalysisWindowManager.CheckAndTriggerAutoAlert(this);
                            }
                            catch (Exception alertEx)
                            {
                                LogManager.Error($"自动告警检查失败: {alertEx.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"调度自动告警检查失败: {ex.Message}");
                    }
                }
                
                // 🔧 优化：使用Dispatcher.BeginInvoke确保UI渲染完成通知在所有UI操作真正完成后执行
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    // 这个回调会在UI消息队列的最后执行，确保所有UI更新都已完成
                    SystemTestWindow.NotifyUIRenderCompleted();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                LogManager.Error($"执行统一判定失败: {ex.Message}");
            }
        }

        private Dictionary<string, DetectionItemValue> BuildDetectionItemsSnapshot()
        {
            var allDetectionItems = new Dictionary<string, DetectionItemValue>();
            AppendDetectionItems(allDetectionItems, _cached2DItems, false);
            AppendDetectionItems(allDetectionItems, _cached3DItems, true);
            AppendDetectionItems(allDetectionItems, _cachedCombinedItems, false);
            return allDetectionItems;
        }

        private void AppendDetectionItems(Dictionary<string, DetectionItemValue> target, List<DetectionItem> items, bool is3DItem)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            foreach (var item in items)
            {
                double numericValue = 0;
                bool isNumeric = double.TryParse(item.Value, out numericValue);
                double lowerLimitNum = 0;
                double upperLimitNum = 0;
                double.TryParse(item.LowerLimit, out lowerLimitNum);
                double.TryParse(item.UpperLimit, out upperLimitNum);

                bool hasValidData = !string.IsNullOrWhiteSpace(item.Value) && isNumeric;

                target[item.Name] = new DetectionItemValue
                {
                    Value = hasValidData ? numericValue : 0,
                    StringValue = item.Value,
                    HasValidData = hasValidData,
                    LowerLimit = lowerLimitNum,
                    UpperLimit = upperLimitNum,
                    IsOutOfRange = hasValidData ? item.IsOutOfRange : false,
                    Is3DItem = is3DItem,
                    ToolIndex = item.ToolIndex
                };
            }
        }

        private AlgorithmResult BuildAlgorithmResult(bool isOk, string defectType, string description, Dictionary<string, DetectionItemValue> detectionItems)
        {
            string requestedEngineId = TemplateConfigPage.Instance?.CurrentAlgorithmEngineId;
            if (string.IsNullOrWhiteSpace(requestedEngineId))
            {
                requestedEngineId = AlgorithmEngineSettingsManager.PreferredEngineId;
            }
            if (string.IsNullOrWhiteSpace(requestedEngineId))
            {
                requestedEngineId = AlgorithmEngineIds.OpenCvOnnx;
            }
            var engine = AlgorithmEngineRegistry.ResolveEngine(requestedEngineId);

            var result = new AlgorithmResult
            {
                EngineId = engine.EngineId,
                EngineVersion = engine.EngineVersion,
                Status = detectionItems.Count > 0
                    ? AlgorithmExecutionStatus.Success
                    : AlgorithmExecutionStatus.Skipped,
                IsOk = isOk,
                DefectType = defectType,
                Description = description
            };

            foreach (var item in detectionItems)
            {
                result.Measurements.Add(new AlgorithmMeasurement
                {
                    Name = item.Key,
                    Value = item.Value.Value,
                    ValueText = item.Value.StringValue,
                    HasValidData = item.Value.HasValidData,
                    LowerLimit = item.Value.LowerLimit,
                    UpperLimit = item.Value.UpperLimit,
                    IsOutOfRange = item.Value.IsOutOfRange,
                    Is3DItem = item.Value.Is3DItem,
                    ToolIndex = item.Value.ToolIndex
                });
            }

            result.DebugInfo["TemplateName"] = CurrentTemplateName ?? string.Empty;
            result.DebugInfo["LotNumber"] = CurrentLotValue ?? string.Empty;
            result.DebugInfo["RequestedEngineId"] = requestedEngineId;

            return result;
        }

        private void PublishAlgorithmResult(AlgorithmResult result)
        {
            if (result == null)
            {
                return;
            }

            MergeRenderPayload(result, _lastRenderResult ?? _lastAlgorithmResult);
            _lastAlgorithmResult = result;
            AlgorithmResultProduced?.Invoke(this, new AlgorithmResultEventArgs(result));
        }

        private static void MergeRenderPayload(AlgorithmResult target, AlgorithmResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (source.RenderImages != null && source.RenderImages.Count > 0)
            {
                if (target.RenderImages == null)
                {
                    target.RenderImages = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var entry in source.RenderImages)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    if (entry.Value == null || entry.Value.Length == 0)
                    {
                        continue;
                    }

                    if (!target.RenderImages.ContainsKey(entry.Key))
                    {
                        target.RenderImages[entry.Key] = entry.Value;
                    }
                }
            }

            if (source.DebugInfo != null && source.DebugInfo.Count > 0)
            {
                if (target.DebugInfo == null)
                {
                    target.DebugInfo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var entry in source.DebugInfo)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    if (entry.Key.IndexOf("render", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (!target.DebugInfo.ContainsKey(entry.Key))
                    {
                        target.DebugInfo[entry.Key] = entry.Value ?? string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// 统一的3D图片查找方法（复用代码，避免不一致）
        /// </summary>
        /// <param name="parentDir">父目录路径</param>
        /// <param name="suffix">文件后缀（如_1、_2等）</param>
        /// <param name="imageGroup">要设置3D图片路径的图片组</param>
        /// <param name="enableLogging">是否启用日志输出</param>
        public static void FindAndSet3DImagesForGroup(string parentDir, string suffix, ImageGroupSet imageGroup, bool enableLogging = false)
        {
            try
            {
                // 在同级目录中查找3D文件夹
                var threeDDir = Path.Combine(parentDir, "3D");
                
                if (Directory.Exists(threeDDir))
                {
                    // 查找高度图（height_*.png 或 *.bmp）
                    var heightFiles = Directory.GetFiles(threeDDir, $"height*{suffix}.png")
                        .Concat(Directory.GetFiles(threeDDir, $"height*{suffix}.bmp"))
                        .ToArray();
                    
                    if (heightFiles.Length > 0)
                    {
                        imageGroup.HeightImagePath = heightFiles[0];
                        if (enableLogging)
                        {
                            var page1 = PageManager.Page1Instance;
                        }
                    }
                    
                    // 查找灰度图（gray_*.png 或 *.bmp）
                    var grayFiles = Directory.GetFiles(threeDDir, $"gray*{suffix}.png")
                        .Concat(Directory.GetFiles(threeDDir, $"gray*{suffix}.bmp"))
                        .ToArray();
                    
                    if (grayFiles.Length > 0)
                    {
                        imageGroup.GrayImagePath = grayFiles[0];
                        if (enableLogging)
                        {
                            var page1 = PageManager.Page1Instance;
                        }
                    }
                    
                    if (enableLogging)
                    {
                        var page1 = PageManager.Page1Instance;
                        if (imageGroup.Has3DImages)
                        {
                            //page1?.LogUpdate($"成功匹配3D图片组: {imageGroup.BaseName}（包含高度图和灰度图）");
                        }
                        else if (!string.IsNullOrEmpty(imageGroup.HeightImagePath) || !string.IsNullOrEmpty(imageGroup.GrayImagePath))
                        {
                            page1?.LogUpdate($"⚠️ 3D图片组不完整: {imageGroup.BaseName}（缺少高度图或灰度图）");
                        }
                    }
                }
                else
                {
                    if (enableLogging)
                    {
                        // LogManager.Info($"目录 {Path.GetFileName(parentDir)} 中未找到3D文件夹"); // 客户日志：技术细节不显示
                    }
                }
            }
            catch (Exception ex)
            {
                if (enableLogging)
                {
                    var page1 = PageManager.Page1Instance;
                    page1?.LogUpdate($"查找3D图片失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 为图片组查找对应的3D图片（高度图和灰度图）
        /// </summary>
        /// <param name="parentDir">父目录路径</param>
        /// <param name="suffix">文件后缀（如_1、_2等）</param>
        /// <param name="imageGroup">要设置3D图片路径的图片组</param>
        private void Find3DImagesForGroup(string parentDir, string suffix, ImageGroupSet imageGroup)
        {
            FindAndSet3DImagesForGroup(parentDir, suffix, imageGroup, enableLogging: true);
        }

        /// <summary>
        /// 静默为图片组查找对应的3D图片（后台线程专用，不调用LogUpdate）
        /// </summary>
        /// <param name="parentDir">父目录路径</param>
        /// <param name="suffix">文件后缀（如_1、_2等）</param>
        /// <param name="imageGroup">要设置3D图片路径的图片组</param>
        private void Find3DImagesForGroupQuiet(string parentDir, string suffix, ImageGroupSet imageGroup)
        {
            FindAndSet3DImagesForGroup(parentDir, suffix, imageGroup, enableLogging: false);
        }

        /// <summary>
        /// 检查当前模板是否启用了3D检测
        /// </summary>
        /// <returns>true if 3D检测已启用</returns>
        public bool Is3DDetectionEnabled()
        {
            return ThreeDSettings.Is3DDetectionEnabledEffective;
        }

        /// <summary>
        /// 触发3D检测（使用指定的高度图和灰度图）
        /// </summary>
        /// <param name="heightImagePath">高度图路径</param>
        /// <param name="grayImagePath">灰度图路径</param>
        /// <returns>true if 3D检测触发成功</returns>
        public async Task<bool> Execute3DDetection(string heightImagePath, string grayImagePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(heightImagePath) || string.IsNullOrEmpty(grayImagePath))
                    {
                        LogManager.Warning("3D图片路径为空，无法执行3D检测");
                        return false;
                    }

                    if (!ThreeDSettings.Is3DDetectionEnabledEffective)
                    {
                        return false;
                    }

                    ThreeDSettings.IsInImageTestMode = true;

                    var cfg = new ThreeDConfig
                    {
                        Enable3DDetection = ThreeDSettings.CurrentDetection3DParams.Enable3DDetection,
                        ProjectName = ThreeDSettings.CurrentDetection3DParams.ProjectName,
                        ProjectFolder = ThreeDSettings.CurrentDetection3DParams.ProjectFolder,
                        ReCompile = ThreeDSettings.CurrentDetection3DParams.ReCompile
                    };

                    using (IThreeDService threeD = new NamedPipeThreeDService())
                    {
                        var req = new ThreeDExecuteLocalImagesRequest
                        {
                            Config = cfg,
                            HeightImagePath = heightImagePath,
                            GrayImagePath = grayImagePath
                        };

                        var result = threeD.ExecuteLocalImages(req, timeoutMs: 30000);
                        if (result != null && result.Success)
                        {
                            Cache3DItemsFromHostResult(result);
                            _detectionManager?.Mark3DCompleted();
                            return true;
                        }

                        Cache3DItemsFromHostResult(null);
                        LogUpdate("3D检测失败/不可用: " + (result?.ErrorMessage ?? "unknown"));
                        _detectionManager?.Mark3DCompleted();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error("执行3D检测失败: " + ex.Message);
                    Cache3DItemsFromHostResult(null);
                    _detectionManager?.Mark3DCompleted();
                    return false;
                }
                finally
                {
                    ThreeDSettings.IsInImageTestMode = false;
                }
            });
        }

        /// <summary>
        /// 判断是否应该保存图片（基于存图策略）
        /// </summary>

        private static void Cache3DItemsFromHostResult(ThreeDExecuteResult result)
        {
            if (result == null)
            {
                _cached3DItems = null;
                return;
            }

            var items = new List<DetectionItem>();
            foreach (var it in result.Items ?? new List<ThreeDDetectionItem>())
            {
                items.Add(new DetectionItem
                {
                    RowNumber = 0,
                    Name = string.IsNullOrWhiteSpace(it.Name) ? "[3D]Unknown" : "[3D]" + it.Name,
                    Value = it.ValueString ?? string.Empty,
                    LowerLimit = string.Empty,
                    UpperLimit = string.Empty,
                    IsOutOfRange = it.IsOutOfRange,
                    Is3DItem = true,
                    ToolIndex = it.ToolIndex
                });
            }

            _cached3DItems = items;
            Set3DCompletionTime();
        }

        private bool ShouldSaveImages(bool isOK)
        {
            try
            {
                bool saveAllImages = false;
                
                // 线程安全的UI访问
                if (Dispatcher.CheckAccess())
                {
                    saveAllImages = ImageSaveModeToggle?.IsChecked == true;
                }
                else
                {
                    saveAllImages = (bool)Dispatcher.Invoke(() => ImageSaveModeToggle?.IsChecked == true);
                }
                
                return saveAllImages || !isOK; // 保存所有图片 或 仅保存NG图片
            }
            catch (Exception ex)
            {
                LogManager.Error($"判断存图策略失败: {ex.Message}");
                return !isOK; // 出错时默认仅保存NG图片
            }
        }

        // 🔧 简化：存图参数直接写入算法全局变量

        /// <summary>
        /// 设置算法存图参数
        /// </summary>
        private void SetAlgorithmSaveImageParameters(string saveDirectory, string imageNumber)
        {
            try
            {
                AlgorithmGlobalVariables.Set("存图根目录", saveDirectory);
                AlgorithmGlobalVariables.Set("存图序号", imageNumber);
            }
            catch (Exception ex)
            {
                LogManager.Error($"设置算法存图参数失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前2D图像到存图目录
        /// </summary>
        private void SaveCurrent2DImages(string saveDirectory, string imageNumber)
        {
            try
            {
                var currentGroup = _imageTestManager?.CurrentGroup;
                if (currentGroup == null)
                {
                    LogManager.Warning("当前没有可保存的图像组，跳过2D存图");
                    return;
                }

                int requiredSources = GetRequired2DSourceCount();
                for (int i = 0; i < requiredSources; i++)
                {
                    var sourcePath = currentGroup.GetPath(i);
                    SaveImageToSubDirectory(sourcePath, Path.Combine(saveDirectory, GetPreferredSourceFolderName(i)), imageNumber);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存2D图像失败: {ex.Message}");
            }
        }

        private void SaveImageToSubDirectory(string sourcePath, string targetDir, string imageNumber)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return;
            }

            Directory.CreateDirectory(targetDir);
            string extension = Path.GetExtension(sourcePath);
            string fileName = $"{imageNumber}{extension}";
            string destinationPath = Path.Combine(targetDir, fileName);

            if (File.Exists(destinationPath))
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                destinationPath = Path.Combine(targetDir, $"{imageNumber}_{timestamp}{extension}");
            }

            File.Copy(sourcePath, destinationPath, overwrite: false);
        }

        /// <summary>
        /// 文件名安全化处理
        /// </summary>
        public string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "Unknown";

            // 替换不安全的字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }

        #region 开机启动管理（简化版）

        /// <summary>
        /// 初始化开机启动检测（延迟执行，避免影响程序启动速度）
        /// </summary>
        private void InitializeAutoStartupCheck()
        {
            try
            {
                // 延迟检测，确保主界面完全加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    AutoStartupManager.CheckAndPromptAutoStartup();
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LogUpdate($"开机启动检测初始化失败: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// 🔧 新增：初始化系统检测管理器的公共接口
        /// </summary>
        public void InitializeDetectionManager()
        {
            try
            {
                _detectionManager?.InitializeSystem();
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化系统检测管理器失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 诊断：显示当前3D配置状态（仅用于问题分析，不进行任何修改）
        /// </summary>
        public void Show3DConfigurationStatus()
        {
            try
            {
                LogManager.Info("========== 【诊断】当前3D配置状态 ==========");
                
                // 检查CurrentDetection3DParams
                bool currentParamsExists = ThreeDSettings.CurrentDetection3DParams != null;
                LogManager.Info($"[诊断] CurrentDetection3DParams存在: {currentParamsExists}");
                
                if (currentParamsExists)
                {
                    var currentParams = ThreeDSettings.CurrentDetection3DParams;
                }
                // 3D静态实例已迁移到Host进程，此处不再直接探测。
                
                // 检查检测管理器状态
                //LogManager.Info($"[诊断] 统一检测管理器3D启用: {_detectionManager.Is3DEnabled}");
                //LogManager.Info($"[诊断] 当前运行模式: {(IsInImageTestMode() ? "图片测试模式" : "生产模式")}");
                
                //LogManager.Info("========== 【诊断】3D配置状态检查完成 ==========");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[诊断] 显示3D配置状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前记录的图片序号
        /// 生产模式：复用TemplateConfigPage.GetCurrentImageNumber()
        /// 图片测试模式：从当前图片名提取纯数字序号
        /// </summary>
        public string GetCurrentImageNumberForRecord()
        {
            try
            {
                // 生产模式：复用现有的GetCurrentImageNumber方法
                if (!_isTestModeActive)
                {
                    return TemplateConfigPage.GetCurrentImageNumber().ToString();
                }
                
                // 图片测试模式：从当前图片名提取纯数字序号
                var currentGroup = _imageTestManager?.CurrentGroup;
                if (currentGroup != null)
                {
                    // 从BaseName中提取纯数字序号
                    if (!string.IsNullOrEmpty(currentGroup.BaseName))
                    {
                        // 修正的正则表达式：匹配最后的数字部分，无论前面有什么字母或下划线
                        var match = System.Text.RegularExpressions.Regex.Match(
                            currentGroup.BaseName, @"(\d+)$");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                    
                    // 如果无法从BaseName提取，尝试从具体图片路径提取
                    string imagePath = currentGroup.Source1Path ?? currentGroup.HeightImagePath;
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(imagePath);
                        var match = System.Text.RegularExpressions.Regex.Match(
                            fileName, @"(\d+)$");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
                
                // 如果都无法提取，返回空字符串
                return "";
            }
            catch (Exception ex)
            {
                LogManager.Warning($"获取图片序号失败: {ex.Message}");
                return "";
            }
        }

        public string GetCurrentTrayImagePath()
        {
            var group = ResolvePreviewImageGroup();
            if (group == null)
            {
                return null;
            }

            return group.Source1Path ?? group.HeightImagePath ?? group.GrayImagePath;
        }

        /// <summary>
        /// 当前LOT号（用于图片存储目录管理）
        /// </summary>
        public string CurrentLOTNumber
        {
            get
            {
                try
                {
                    // 【修复】不再从界面读取，直接使用存储的CurrentLotValue避免读取竞争
                    return CurrentLotValue ?? "";
                }
                catch (Exception ex)
                {
                    LogManager.Error($"获取当前LOT号失败: {ex.Message}");
                    return "";
                }
            }
        }

        /// <summary>
        /// 获取最新测试的图片组
        /// </summary>
        public ImageGroupSet GetLastTestImageGroup()
        {
            try
            {
                // 从图片测试管理器获取最新的图片组
                //if (_imageTestManager != null && _imageTestManager.ImageGroups.Count > 0)
                //{
                //    var lastGroup = _imageTestManager.ImageGroups.LastOrDefault();
                //    if (lastGroup != null && lastGroup.IsValid)
                //    {
                //        return lastGroup;
                //    }
                //}
                
                // 使用记录的最新图像源1路径
                if (!string.IsNullOrEmpty(_lastSavedImageSource1Path) && File.Exists(_lastSavedImageSource1Path))
                {
                    LogManager.Info($"[最后一组图] 使用记录的图像源1路径: {_lastSavedImageSource1Path}");
                    
                    // 从图像源1路径中提取父目录和后缀
                    string fileName = Path.GetFileNameWithoutExtension(_lastSavedImageSource1Path);
                    var suffixMatch = Regex.Match(fileName, @"(\d{4})$");
                    
                    if (suffixMatch.Success)
                    {
                        string suffix = suffixMatch.Value;
                        string imageSource1Dir = Path.GetDirectoryName(_lastSavedImageSource1Path);
                        string parentDir = Path.GetDirectoryName(imageSource1Dir);
                        
                        LogManager.Info($"[最后一组图] 提取参数 - 父目录: {parentDir}, 后缀: {suffix}");
                        
                        // 复用现有的图片匹配逻辑
                        var imageGroup = CreateImageGroupBySuffix(parentDir, suffix);
                        
                        if (imageGroup != null && imageGroup.IsValid)
                        {
                            LogManager.Info($"[最后一组图] 成功创建图片组: {imageGroup.BaseName}");
                            return imageGroup;
                        }
                        else
                        {
                            LogManager.Warning($"[最后一组图] 创建图片组失败");
                        }
                    }
                    else
                    {
                        LogManager.Warning($"[最后一组图] 无法从文件名提取后缀: {fileName}");
                    }
                }
                else
                {
                    LogManager.Warning($"[最后一组图] 没有记录的图像源1路径或文件不存在: {_lastSavedImageSource1Path}");
                }
                
                return null;
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取最新测试图片组失败: {ex.Message}");
                return null;
            }
        }

    }

    public class DetectionItem : INotifyPropertyChanged
    {
        private int _rowNumber;
        public int RowNumber
        {
            get => _rowNumber;
            set => SetProperty(ref _rowNumber, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _value;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>
        /// 下限值
        /// </summary>
        private string _lowerLimit;
        public string LowerLimit
        {
            get => _lowerLimit;
            set => SetProperty(ref _lowerLimit, value);
        }

        /// <summary>
        /// 上限值
        /// </summary>
        private string _upperLimit;
        public string UpperLimit
        {
            get => _upperLimit;
            set => SetProperty(ref _upperLimit, value);
        }

        /// <summary>
        /// 标识数值是否超出范围（用于设置行背景色）
        /// </summary>
        private bool _isOutOfRange;
        public bool IsOutOfRange
        {
            get => _isOutOfRange;
            set => SetProperty(ref _isOutOfRange, value);
        }

        /// <summary>
        /// 标识是否为3D检测项目
        /// </summary>
        private bool _is3DItem;
        public bool Is3DItem
        {
            get => _is3DItem;
            set => SetProperty(ref _is3DItem, value);
        }

        /// <summary>
        /// 标识是否为带自定义上下限的补偿项目
        /// </summary>
        private bool _isCompensated;
        public bool IsCompensated
        {
            get => _isCompensated;
            set => SetProperty(ref _isCompensated, value);
        }

        /// <summary>
        /// 标识是否为仅数值补偿项目（上下限由3D判定对象提供）
        /// </summary>
        private bool _isValueCompensated;
        public bool IsValueCompensated
        {
            get => _isValueCompensated;
            set => SetProperty(ref _isValueCompensated, value);
        }

        /// <summary>
        /// 标识是否为手动判定项目（不依赖“设定判定对象”）
        /// </summary>
        private bool _isManualJudgementItem;
        public bool IsManualJudgementItem
        {
            get => _isManualJudgementItem;
            set => SetProperty(ref _isManualJudgementItem, value);
        }

        /// <summary>
        /// 3D检测工具的索引，用于数据更新时匹配
        /// </summary>
        private int _toolIndex;
        public int ToolIndex
        {
            get => _toolIndex;
            set => SetProperty(ref _toolIndex, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 图片组数据结构，包含一次检测所需的三张2D图片路径和可选的3D图片路径
    /// </summary>
    public class ImageGroupSet
    {
        private readonly string[] _sourcePaths = new string[10];

        public string Source1Path
        {
            get => _sourcePaths[0];
            set => _sourcePaths[0] = value;
        }

        public string Source2_1Path
        {
            get => _sourcePaths[1];
            set => _sourcePaths[1] = value;
        }

        public string Source2_2Path
        {
            get => _sourcePaths[2];
            set => _sourcePaths[2] = value;
        }

        public string Source4Path
        {
            get => _sourcePaths[3];
            set => _sourcePaths[3] = value;
        }
        public string BaseName { get; set; }       // 基础名称（xx部分）

        // 兼容旧字段命名
        public string Source2Path
        {
            get => Source2_1Path;
            set => Source2_1Path = value;
        }

        public string Source3Path
        {
            get => Source2_2Path;
            set => Source2_2Path = value;
        }

        public string GetPath(int index)
        {
            if (index < 0 || index >= _sourcePaths.Length)
            {
                return null;
            }

            return _sourcePaths[index];
        }

        public void SetSource(int index, string path, string id = null, string displayName = null)
        {
            if (index < 0 || index >= _sourcePaths.Length)
            {
                return;
            }

            _sourcePaths[index] = path;
        }

        // 验机检测用：样品索引和轮次索引
        public int SampleIndex { get; set; } = -1;  // 样品索引 (0-based)
        public int CycleIndex { get; set; } = -1;   // 轮次索引 (0-based)

        // 3D检测相关路径
        public string HeightImagePath { get; set; } // 3D高度图路径
        public string GrayImagePath { get; set; }   // 3D灰度图路径
        public bool Has3DImages => !string.IsNullOrEmpty(HeightImagePath) && !string.IsNullOrEmpty(GrayImagePath);
        
        // 2D图片完整性检查（用于模板配置阶段）
        public bool Has2DImages => HasRequired2DImages();

        private int GetRequired2DSourceCount()
        {
            var count = ImageSourceNaming.GetActiveSourceCount();
            if (count <= 0)
            {
                count = 1;
            }

            return Math.Min(count, 10);
        }

        private bool HasRequired2DImages()
        {
            var required = GetRequired2DSourceCount();
            for (int i = 0; i < required; i++)
            {
                if (string.IsNullOrEmpty(GetPath(i)))
                {
                    return false;
                }
            }

            return required > 0;
        }
        
        // **修复：支持5张图片索引 - 如果有完整的2D图片或有3D图片，则有效**
        /// <summary>
        /// 检查图片组是否有效
        /// 在模板配置模式时只需要2D图片完整，在正常检测模式时根据3D使能情况判断
        /// </summary>
        public bool IsValid 
        { 
            get 
            {
                // 检查2D图片是否完整（3张）
                bool has2DImages = HasRequired2DImages();
                
                // 🔧 修复：配置模式下也需要根据3D使能状态检查图片完整性
                // 当3D检测启用时，配置模式也需要3D图片进行检测和告警
                var page1Instance = Page1.PageManager.Page1Instance;
                bool isInTemplateConfigMode = page1Instance?.DetectionManager?.SystemState == SystemDetectionState.TemplateConfiguring;
                
                if (isInTemplateConfigMode)
                {
                    // 模板配置模式：如果启用了3D检测，也需要检查3D图片
                    if (page1Instance?.Is3DDetectionEnabled() == true)
                    {
                        return has2DImages && Has3DImages;
                    }
                    else
                    {
                        // 未启用3D检测时，只需要2D图片完整即可
                        return has2DImages;
                    }
                }
                
                // 正常检测模式：如果3D使能，则需要5张图片都存在
                if (page1Instance?.Is3DDetectionEnabled() == true)
                {
                    return has2DImages && Has3DImages;
                }
                
                // 如果3D未使能，则只需要3张2D图片
                return has2DImages;
            }
        }


    }

    /// <summary>
    /// 图片检测状态枚举
    /// </summary>
    public enum ImageTestState
    {
        Idle,           // 空闲状态
        Testing,        // 检测状态（卡片闪烁）
        Paused,         // 暂停状态
        Completed       // 检测完成
    }

    /// <summary>
    /// 自动检测模式枚举
    /// </summary>
    public enum AutoDetectionMode
    {
        None,           // 无自动检测
        ToFirst,        // 反向检测到第一组
        ToLast          // 正向检测到最后一组
    }

    /// <summary>
    /// 图片测试管理器
    /// </summary>
    public class ImageTestManager
    {
        private List<ImageGroupSet> _imageGroups = new List<ImageGroupSet>();
        private int _currentIndex = 0;
        private ImageTestState _currentState = ImageTestState.Idle;
        private AutoDetectionMode _autoDetectionMode = AutoDetectionMode.None;

        public List<ImageGroupSet> ImageGroups => _imageGroups;
        public int CurrentIndex => _currentIndex;
        public ImageTestState CurrentState => _currentState;
        public AutoDetectionMode AutoDetectionMode => _autoDetectionMode;
        public ImageGroupSet CurrentGroup => _imageGroups.Count > 0 && _currentIndex >= 0 && _currentIndex < _imageGroups.Count 
                                           ? _imageGroups[_currentIndex] : null;

        public void SetImageGroups(List<ImageGroupSet> groups)
        {
            _imageGroups = groups ?? new List<ImageGroupSet>();
            
            // 只在以下情况下重置索引：
            // 1. 新的图片组列表为空
            // 2. 当前索引超出了新列表的范围
            if (_imageGroups.Count == 0 || _currentIndex >= _imageGroups.Count)
            {
                _currentIndex = 0;
            }
            // 否则保持当前索引不变，这样用户可以继续从当前位置操作
        }

        public void SetState(ImageTestState state)
        {
            _currentState = state;
        }

        public void SetAutoDetectionMode(AutoDetectionMode mode)
        {
            _autoDetectionMode = mode;
        }

        public bool MoveNext()
        {
            if (_imageGroups.Count > 0)
            {
                _currentIndex = (_currentIndex + 1) % _imageGroups.Count;
                return true;
            }
            return false;
        }

        public bool MovePrevious()
        {
            if (_imageGroups.Count > 0)
            {
                _currentIndex = (_currentIndex - 1 + _imageGroups.Count) % _imageGroups.Count;
                return true;
            }
            return false;
        }

        public bool MoveToFirst()
        {
            if (_imageGroups.Count > 0)
            {
                _currentIndex = 0;
                return true;
            }
            return false;
        }

        public bool MoveToLast()
        {
            if (_imageGroups.Count > 0)
            {
                _currentIndex = _imageGroups.Count - 1;
                return true;
            }
            return false;
        }

        public bool MoveTo(int index)
        {
            if (_imageGroups.Count > 0 && index >= 0 && index < _imageGroups.Count)
            {
                _currentIndex = index;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            _currentIndex = 0;
            _currentState = ImageTestState.Idle;
            _autoDetectionMode = AutoDetectionMode.None;
        }
    }

     /// <summary>
    /// 统一检测管理器：主动管理2D和3D检测的完整周期，只有它能调用统一判定
    /// </summary>

    /// <summary>
    /// 测试模式数据管理器 - 独立管理测试模式的检测数据和状态
    /// </summary>
    public class TestModeDataManager
    {
        // 测试模式检测结果缓存
        public List<TestModeDetectionResult> TestResults { get; set; } = new List<TestModeDetectionResult>();
        
        // 被Mark的图片集合（图片路径作为键）
        public HashSet<string> MarkedImages { get; set; } = new HashSet<string>();
        
        // 生产模式数据缓存（用于恢复）
        public ProductionModeDataCache ProductionDataCache { get; set; }

        /// <summary>
        /// 初始化测试模式，缓存生产数据并重置为测试模式
        /// </summary>
        public void StartTestMode()
        {
            // 🔧 正确策略：同时缓存UI和StatisticsManager的生产数据，重置为测试模式
            var page1Instance = Page1.PageManager.Page1Instance;
            var templateConfigInstance = TemplateConfigPage.Instance;
            
            if (page1Instance != null && templateConfigInstance != null)
            {
                // 缓存StatisticsManager中的生产数据（这是最准确的数据源）
                int totalCount = TemplateConfigPage.StatisticsManager.TotalCount;
                int okCount = TemplateConfigPage.StatisticsManager.OkCount;
                int ngCount = totalCount - okCount;
                double ngRate = totalCount > 0 ? (double)ngCount / totalCount * 100 : 0.0;
                
                // 同时缓存UI显示的数据作为备份验证
                int.TryParse(page1Instance.Total_num.Text, out int uiTotalCount);
                int.TryParse(page1Instance.OK_num.Text, out int uiOkCount);
                int.TryParse(page1Instance.NG_num.Text, out int uiNgCount);
                
                ProductionDataCache = new ProductionModeDataCache
                {
                    TotalCount = totalCount,
                    NgCount = ngCount,
                    OkCount = okCount,
                    NgRate = ngRate,
                    // 🔧 修复：深度复制DefectTypeCounter以避免引用问题
                    DefectTypeCounter = new Dictionary<string, int>(TemplateConfigPage.StatisticsManager.DefectTypeCounter)
                };
                
                // 重置StatisticsManager为测试模式初始状态
                TemplateConfigPage.StatisticsManager.TotalCount = 0;
                TemplateConfigPage.StatisticsManager.OkCount = 0;
                TemplateConfigPage.StatisticsManager.DefectTypeCounter.Clear();
                
                // 重置UI为测试模式的初始状态
                page1Instance.Dispatcher.BeginInvoke(new Action(() =>
                {
                    page1Instance.Total_num.Text = "0";
                    page1Instance.OK_num.Text = "0";
                    page1Instance.NG_num.Text = "0";
                }));
                
                LogManager.Info($"[测试模式] 已缓存生产数据并重置 - 缓存: Total={totalCount}, OK={okCount}, NG={ngCount} | UI验证: Total={uiTotalCount}, OK={uiOkCount}, NG={uiNgCount}");
            }
            else
            {
                ProductionDataCache = new ProductionModeDataCache
                {
                    TotalCount = 0,
                    NgCount = 0,
                    OkCount = 0,
                    NgRate = 0.0,
                    // 🔧 修复：确保DefectTypeCounter在任何情况下都有初始值
                    DefectTypeCounter = new Dictionary<string, int>()
                };
                LogManager.Warning("[测试模式] 无法获取Page1或TemplateConfig实例");
            }
            
            // 清空测试结果
            TestResults.Clear();
            MarkedImages.Clear();
            
            LogManager.Info("[测试模式] 测试模式已启动，统计数据已重置，开始测试统计");
        }

        /// <summary>
        /// 结束测试模式，恢复生产数据和UI
        /// </summary>
        public void StopTestMode()
        {
            // 🔧 正确策略：恢复StatisticsManager和UI为缓存的生产数据
            try
            {
                if (ProductionDataCache != null)
                {
                    var page1Instance = Page1.PageManager.Page1Instance;
                    var templateConfigInstance = TemplateConfigPage.Instance;
                    
                    if (page1Instance != null && templateConfigInstance != null)
                    {
                        // 恢复StatisticsManager的生产数据
                        TemplateConfigPage.StatisticsManager.TotalCount = ProductionDataCache.TotalCount;
                        TemplateConfigPage.StatisticsManager.OkCount = ProductionDataCache.OkCount;
                        
                        // 🔧 修复：重新计算并恢复良率
                        if (ProductionDataCache.TotalCount > 0)
                        {
                            double calculatedYieldRate = (double)ProductionDataCache.OkCount / ProductionDataCache.TotalCount * 100;
                            TemplateConfigPage.StatisticsManager.YieldRate = calculatedYieldRate;
                            LogManager.Info($"[测试模式] 重新计算良率: {calculatedYieldRate:F2}%");
                        }
                        else
                        {
                            TemplateConfigPage.StatisticsManager.YieldRate = 100.0; // 无数据时默认100%
                        }
                        
                        // 🔧 修复：恢复DefectTypeCounter以修复饼图显示问题
                        TemplateConfigPage.StatisticsManager.DefectTypeCounter.Clear();
                        foreach (var kvp in ProductionDataCache.DefectTypeCounter)
                        {
                            TemplateConfigPage.StatisticsManager.DefectTypeCounter[kvp.Key] = kvp.Value;
                        }
                        
                                                                // 恢复UI显示为缓存的生产数据 - 使用同步调用确保数据恢复的原子性
                        if (Application.Current != null && Application.Current.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    if (page1Instance.Total_num != null)
                                        page1Instance.Total_num.Text = ProductionDataCache.TotalCount.ToString();
                                    if (page1Instance.OK_num != null)
                                        page1Instance.OK_num.Text = ProductionDataCache.OkCount.ToString();
                                    if (page1Instance.NG_num != null)
                                        page1Instance.NG_num.Text = ProductionDataCache.NgCount.ToString();
                                    
                                    // 🔧 修复：同时更新良率显示
                                    if (page1Instance.yieldRate != null)
                                        page1Instance.yieldRate.Text = $"{TemplateConfigPage.StatisticsManager.YieldRate:F2}%";
                                }
                                catch (Exception uiEx)
                                {
                                    LogManager.Warning($"[测试模式] UI更新失败（界面可能已关闭）: {uiEx.Message}");
                                }
                            });
                        }
                        else
                        {
                            LogManager.Warning("[测试模式] Application.Current不可用，跳过UI更新");
                        }
                        
                        // 🔧 修复：强制刷新饼图显示恢复的数据
                        try
                        {
                            // 使用反射调用私有的UpdatePieChart方法
                            var updatePieChartMethod = typeof(TemplateConfigPage).GetMethod("UpdatePieChart", 
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (updatePieChartMethod != null)
                            {
                                updatePieChartMethod.Invoke(templateConfigInstance, null);
                                LogManager.Info("[测试模式] 饼图已强制刷新");
                            }
                            else
                            {
                                LogManager.Warning("[测试模式] 未找到UpdatePieChart方法，饼图将在下次检测时刷新");
                            }
                        }
                        catch (Exception pieEx)
                        {
                            LogManager.Warning($"[测试模式] 强制刷新饼图失败: {pieEx.Message}，饼图将在下次检测时刷新");
                        }
                        
                        LogManager.Info($"[测试模式] 已恢复生产数据 - StatisticsManager和UI: Total={ProductionDataCache.TotalCount}, OK={ProductionDataCache.OkCount}, NG={ProductionDataCache.NgCount}, DefectTypes={ProductionDataCache.DefectTypeCounter.Count}");
                    }
                    else
                    {
                        LogManager.Warning("[测试模式] 无法获取Page1或TemplateConfig实例，无法恢复数据");
                    }
                }
                else
                {
                    LogManager.Warning("[测试模式] 没有缓存的生产数据，无法恢复");
                }
                
                // 清空测试结果和标记
                TestResults.Clear();
                MarkedImages.Clear();
                
                // 清空缓存引用
                ProductionDataCache = null;
                
                LogManager.Info("[测试模式] 测试模式已结束，生产数据已完全恢复");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[测试模式] 结束测试模式时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加测试检测结果
        /// </summary>
        public void AddTestResult(TestModeDetectionResult result)
        {
            if (result != null)
            {
                TestResults.Add(result);
                // 注意：统计更新由正常的UpdateDefectStatistics流程处理，不需要额外更新
            }
        }

        /// <summary>
        /// 标记图片
        /// </summary>
        public void MarkImage(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                MarkedImages.Add(imagePath);
                
                // 更新对应的检测结果标记状态
                var result = TestResults.FirstOrDefault(r => r.ImagePath == imagePath);
                if (result != null)
                {
                    result.IsMarked = true;
                }
            }
        }

        /// <summary>
        /// 检查图片是否已被标记
        /// </summary>
        public bool IsImageMarked(string imagePath)
        {
            return !string.IsNullOrEmpty(imagePath) && MarkedImages.Contains(imagePath);
        }

        /// <summary>
        /// 获取所有测试结果
        /// </summary>
        public List<TestModeDetectionResult> GetAllResults()
        {
            return new List<TestModeDetectionResult>(TestResults);
        }

        /// <summary>
        /// 获取被标记的测试结果
        /// </summary>
        public List<TestModeDetectionResult> GetMarkedResults()
        {
            return TestResults.Where(r => r.IsMarked).ToList();
        }

        /// <summary>
        /// 根据图片路径获取对应的图片组
        /// </summary>
        public ImageGroupSet GetImageGroupByPath(string imagePath)
        {
            // 这里需要从图片测试管理器中获取图片组
            var page1Instance = Page1.PageManager.Page1Instance;
            if (page1Instance?._imageTestManager?.ImageGroups != null)
            {
                return page1Instance._imageTestManager.ImageGroups.FirstOrDefault(g => g.Source1Path == imagePath);
            }
            return null;
        }
    }

    /// <summary>
    /// 测试模式检测结果
    /// </summary>
    public class TestModeDetectionResult
    {
        public string ImagePath { get; set; }
        public string ImageNumber { get; set; } // 图片编号（后缀）
        public DateTime TestTime { get; set; }
        public bool IsMarked { get; set; }
        public bool IsOK { get; set; }
        public string DefectType { get; set; }
        public List<DetectionItem> DetectionItems { get; set; } = new List<DetectionItem>();
    }

    /// <summary>
    /// 生产模式数据缓存
    /// </summary>
    public class ProductionModeDataCache
    {
        public int TotalCount { get; set; }
        public int NgCount { get; set; }
        public int OkCount { get; set; }
        public double NgRate { get; set; }
        // 🔧 修复：添加缺陷类型计数器缓存，用于恢复饼图数据
        public Dictionary<string, int> DefectTypeCounter { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// 导出模式枚举
    /// </summary>
    public enum ExportMode
    {
        All,        // 导出所有测试结果
        MarkedOnly  // 仅导出被标记的测试结果
    }

    /// <summary>
    /// 生产统计数据持久化管理器 - 复用测试模式的数据结构
    /// </summary>
    public static class ProductionStatsPersistence
    {
        private static readonly string _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ProductionStats.json");
        
        /// <summary>
        /// 保存当前生产统计数据到文件
        /// </summary>
        public static void SaveProductionStats()
        {
            try
            {
                // 确保Config目录存在
                var configDir = Path.GetDirectoryName(_configFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                // 获取当前生产统计数据
                var currentStats = new ProductionModeDataCache
                {
                    TotalCount = TemplateConfigPage.StatisticsManager.TotalCount,
                    OkCount = TemplateConfigPage.StatisticsManager.OkCount,
                    NgCount = TemplateConfigPage.StatisticsManager.TotalCount - TemplateConfigPage.StatisticsManager.OkCount,
                    NgRate = TemplateConfigPage.StatisticsManager.TotalCount > 0 ? 
                             (double)(TemplateConfigPage.StatisticsManager.TotalCount - TemplateConfigPage.StatisticsManager.OkCount) / TemplateConfigPage.StatisticsManager.TotalCount * 100 : 0,
                    // 深度复制DefectTypeCounter
                    DefectTypeCounter = new Dictionary<string, int>(TemplateConfigPage.StatisticsManager.DefectTypeCounter)
                };
                
                // 序列化为JSON并保存
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(currentStats, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(_configFile, json, Encoding.UTF8);
                
                LogManager.Info($"[生产数据持久化] 生产统计数据已保存: Total={currentStats.TotalCount}, OK={currentStats.OkCount}, NG={currentStats.NgCount}, 缺陷类型={currentStats.DefectTypeCounter.Count}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[生产数据持久化] 保存生产统计数据失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从文件加载生产统计数据
        /// </summary>
        public static void LoadProductionStats()
        {
            try
            {
                if (!File.Exists(_configFile))
                {
                    LogManager.Info("[生产数据持久化] 生产统计数据文件不存在，使用默认值");
                    return;
                }
                
                // 读取并反序列化JSON
                string json = File.ReadAllText(_configFile, Encoding.UTF8);
                var savedStats = Newtonsoft.Json.JsonConvert.DeserializeObject<ProductionModeDataCache>(json);
                
                if (savedStats != null)
                {
                    // 恢复StatisticsManager数据
                    TemplateConfigPage.StatisticsManager.TotalCount = savedStats.TotalCount;
                    TemplateConfigPage.StatisticsManager.OkCount = savedStats.OkCount;
                    
                    // 🔧 修复：重新计算良率（软件重启时）
                    if (savedStats.TotalCount > 0)
                    {
                        double calculatedYieldRate = (double)savedStats.OkCount / savedStats.TotalCount * 100;
                        TemplateConfigPage.StatisticsManager.YieldRate = calculatedYieldRate;
                        LogManager.Info($"[生产数据持久化] 重新计算良率: {calculatedYieldRate:F2}%");
                    }
                    else
                    {
                        TemplateConfigPage.StatisticsManager.YieldRate = 100.0; // 无数据时默认100%
                    }
                    
                    // 恢复DefectTypeCounter
                    TemplateConfigPage.StatisticsManager.DefectTypeCounter.Clear();
                    foreach (var kvp in savedStats.DefectTypeCounter ?? new Dictionary<string, int>())
                    {
                        TemplateConfigPage.StatisticsManager.DefectTypeCounter[kvp.Key] = kvp.Value;
                    }
                    
                    // 更新Page1 UI显示
                    var page1Instance = Page1.PageManager.Page1Instance;
                    if (page1Instance != null && Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (page1Instance.Total_num != null)
                                    page1Instance.Total_num.Text = savedStats.TotalCount.ToString();
                                if (page1Instance.OK_num != null)
                                    page1Instance.OK_num.Text = savedStats.OkCount.ToString();
                                if (page1Instance.NG_num != null)
                                    page1Instance.NG_num.Text = savedStats.NgCount.ToString();
                                
                                // 🔧 修复：更新良率显示（软件重启时）
                                if (page1Instance.yieldRate != null)
                                    page1Instance.yieldRate.Text = $"{TemplateConfigPage.StatisticsManager.YieldRate:F2}%";
                                    
                                // 强制刷新饼图显示
                                var templateConfigInstance = TemplateConfigPage.Instance;
                                if (templateConfigInstance != null)
                                {
                                    var updatePieChartMethod = typeof(TemplateConfigPage).GetMethod("UpdatePieChart", 
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    updatePieChartMethod?.Invoke(templateConfigInstance, null);
                                }
                            }
                            catch (Exception uiEx)
                            {
                                LogManager.Warning($"[生产数据持久化] UI更新失败: {uiEx.Message}");
                            }
                        });
                    }
                    
                    LogManager.Info($"[生产数据持久化] 生产统计数据已加载: Total={savedStats.TotalCount}, OK={savedStats.OkCount}, NG={savedStats.NgCount}, 缺陷类型={savedStats.DefectTypeCounter.Count}");
                }
                else
                {
                    LogManager.Warning("[生产数据持久化] 数据格式无效或StatisticsManager未初始化");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[生产数据持久化] 加载生产统计数据失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清空保存的生产统计数据文件
        /// </summary>
        public static void ClearSavedStats()
        {
            try
            {
                if (File.Exists(_configFile))
                {
                    File.Delete(_configFile);
                    LogManager.Info("[生产数据持久化] 已清空保存的生产统计数据");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[生产数据持久化] 清空保存的生产统计数据失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 超限项目信息记录类
    /// </summary>
    public class OutOfRangeRecord
    {
        public string ImageNumber { get; set; }
        public string DefectType { get; set; }
        public DateTime DetectionTime { get; set; }
        public List<OutOfRangeItem> OutOfRangeItems { get; set; } = new List<OutOfRangeItem>();
    }

    /// <summary>
    /// 超限项目详情类
    /// </summary>
    public class OutOfRangeItem
    {
        public string ItemName { get; set; }
        public string Value { get; set; }
        public string LowerLimit { get; set; }
        public string UpperLimit { get; set; }
        public bool IsOutOfRange { get; set; }
    }
}


