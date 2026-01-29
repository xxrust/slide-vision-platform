using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp2.UI.Models;
using LjdSampleWrapper;
using Keyence.LjDev3dView;
using Keyence.LjDevMeasure;
using ImageSourceModuleCs;
using Newtonsoft.Json;
using System.Threading.Tasks;
using VM.Core;
using Keyence.LjDevCommon;
using LjDevExt;

namespace WpfApp2.UI
{
    /// <summary>
    /// 渲染图查看器窗口 - 独立工具，用于查看NG渲染图及其检测详情
    /// </summary>
    public partial class RenderViewerWindow : Window
    {
        // 【新增】渲染图查看器专用的静态3D实例（所有渲染图查看器窗口共享）
        private static LjdMeasureEx _StaticRenderViewerMeasureEx = null;
        private static readonly object _StaticInstanceLock = new object();

        private string _currentLotValue;
        private List<RenderImageItem> _renderImageItems = new List<RenderImageItem>();
        private List<RenderImageItem> _filteredRenderImageItems = new List<RenderImageItem>(); // 过滤后的渲染图项目
        private int _currentIndex = 0;
        private Ljd3DViewEx _View3D;
        private ImageSourceModuleTool _imageSource2D;

        // 项目选择相关
        private List<string> _allProjects = new List<string>(); // 所有项目名称
        private HashSet<string> _selectedProjects = new HashSet<string>(); // 选中的项目名称
        private Dictionary<string, Button> _projectButtons = new Dictionary<string, Button>(); // 项目按键映射

        // 数据源模式相关
        private DataSourceMode _currentDataSourceMode = DataSourceMode.CurrentStatistics; // 默认使用当前统计

        // 自动播放相关
        private System.Threading.CancellationTokenSource _autoPlayCancellationTokenSource;
        private bool _isAutoPlaying = false;
        private const int AUTO_PLAY_INTERVAL_MS = 500; // 自动播放间隔（毫秒）


        public RenderViewerWindow(string lotValue)
        {
            try
            {
                LogManager.Info($"开始初始化渲染图查看器，LOT号: {lotValue}");
                InitializeComponent();
                _currentLotValue = lotValue;
                
                LogManager.Info("XAML组件初始化完成，开始初始化3D和2D组件");
                InitializeComponents();
                
                LogManager.Info("组件初始化完成，开始加载渲染图数据");
                LoadRenderImages();
            }
            catch (Exception ex)
            {
                LogManager.Error($"渲染图查看器构造函数异常: {ex.Message}");
                MessageBox.Show($"渲染图查看器初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            try
            {
                // 初始化3D视图
                _View3D = new Ljd3DViewEx();
                _3DViewHost.Child = _View3D;
                
                // 初始化2D图像源 - 使用"渲染图显示.图像源1"
                _imageSource2D = VM.Core.VmSolution.Instance["渲染图显示.图像源1"] as ImageSourceModuleCs.ImageSourceModuleTool;
                if (_imageSource2D != null)
                {
                    VmRender2D.ModuleSource = _imageSource2D;
                }
                else
                {
                    LogManager.Warning("未找到'渲染图显示.图像源1'模块");
                }

                // 【修复】仅在3D使能时初始化静态实例
                bool is3DEnabled = Ljd3DDetectionWindow.CurrentDetection3DParams?.Enable3DDetection ?? false;

                if (is3DEnabled)
                {
                    // 确保静态实例已初始化（第一次创建，后续复用）
                    EnsureStaticMeasureExInstance();
                }
                else
                {
                    LogManager.Info("3D未使能，跳过LJD静态实例初始化");
                }

                LogManager.Info("渲染图查看器组件初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Error($"渲染图查看器组件初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保静态MeasureEx实例已初始化（线程安全，单例模式）
        /// </summary>
        private void EnsureStaticMeasureExInstance()
        {
            lock (_StaticInstanceLock)
            {
                // 如果静态实例已存在且可用，直接返回
                if (_StaticRenderViewerMeasureEx != null && _StaticRenderViewerMeasureEx.IsEnable)
                {
                    LogManager.Info("渲染图查看器静态3D实例已存在，复用该实例");
                    return;
                }

                try
                {
                    // 获取当前3D配置参数
                    string projectName = Ljd3DDetectionWindow.CurrentDetection3DParams.ProjectName;
                    string projectFolder = Ljd3DDetectionWindow.CurrentDetection3DParams.ProjectFolder;
                    bool reCompile = Ljd3DDetectionWindow.CurrentDetection3DParams.ReCompile;

                    LogManager.Info($"首次创建渲染图查看器静态3D实例 - 项目名: {projectName}, 配置目录: {projectFolder}, 重编译: {reCompile}");

                    // 创建渲染图查看器专用的静态LjdMeasureEx实例（不连接硬件）
                    _StaticRenderViewerMeasureEx = new LjdMeasureEx(
                        projectName,
                        projectFolder,
                        "",    // 不连接激光头（空字符串）
                        false, // 不使用LJS
                        0,     // 不使用TCP端口
                        reCompile, // 使用相同的编译设置
                        true,  // 使用3D
                        ""     // 空字符串
                    );

                    if (_StaticRenderViewerMeasureEx != null && _StaticRenderViewerMeasureEx.IsEnable)
                    {
                        // 绑定静态回调
                        _StaticRenderViewerMeasureEx.ImageExecuted += StaticRenderViewer_Callback;
                        LogManager.Info("渲染图查看器静态3D实例创建成功");
                    }
                    else
                    {
                        LogManager.Warning("渲染图查看器静态3D实例创建失败");
                        _StaticRenderViewerMeasureEx = null;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error($"创建渲染图查看器静态3D实例异常: {ex.Message}");
                    _StaticRenderViewerMeasureEx = null;
                }
            }
        }

        /// <summary>
        /// 渲染图查看器静态回调 - 处理所有渲染图查看器窗口的3D检测结果
        /// </summary>
        private static void StaticRenderViewer_Callback(LjdMeasureEx sender)
        {
            if (sender?.ExecuteResult == null)
                return;

            if (!sender.ExecuteResult.IsEnable)
                return;

            try
            {
                // 在UI线程中执行所有窗口查找和更新操作
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 找到当前活动的渲染图查看器窗口
                    var activeWindow = Application.Current.Windows.OfType<RenderViewerWindow>()
                        .FirstOrDefault(w => w.IsActive || w.IsFocused);

                    // 如果没有活动窗口，尝试找到最后创建的窗口
                    if (activeWindow == null)
                    {
                        activeWindow = Application.Current.Windows.OfType<RenderViewerWindow>().LastOrDefault();
                    }

                    if (activeWindow != null)
                    {
                        activeWindow.UpdateRenderViewer3DView(sender);
                    }
                });
            }
            catch (Exception ex)
            {
                LogManager.Error($"渲染图查看器静态回调处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新渲染图查看器的3D视图显示
        /// </summary>
        private void UpdateRenderViewer3DView(LjdMeasureEx sender)
        {
            try
            {
                LogManager.Info("渲染图查看器3D视图开始更新");

                LjdExecuteResult result = sender.ExecuteResult;

                // 更新渲染图查看器的3D视图显示
                if (result.DstHeightImages.Length > 1)
                {
                    _View3D.SetImageEx(result.DstHeightImages, result.DstGrayImages);
                }
                else
                {
                    _View3D.SetImageEx(result.DstHeightImage, result.DstGrayImage);
                }

                // 设置工具信息和颜色配置
                if (_View3D?.LJView3D != null)
                {
                    ApplyPage1ColorConfig(result);
                    if (result.Results != null && result.Results.Count > 0)
                    {
                        _View3D.LJView3D.SetToolInfo(result.Results);
                    }
                }

                // 更新状态信息
                string judgeResult = sender.IsJudgeAllOK ? "OK" : "NG";
                double processTimeMs = sender.ExecuteTimeCost.TotalMilliseconds;

                LogManager.Info($"渲染图查看器3D视图更新完成 - 结果: {judgeResult}, 耗时: {processTimeMs:F2}ms");
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新渲染图查看器3D视图失败: {ex.Message}");
            }
        }



        /// <summary>
        /// 加载渲染图数据并生成项目选择按键
        /// </summary>
        private async void LoadRenderImages()
        {
            try
            {
                LogManager.Info("LoadRenderImages方法开始执行");
                
                // 构建基础路径：原图存储/LOT号/
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string lotDir = Path.Combine(baseDir, "原图存储", _currentLotValue);
                
                LogManager.Info($"基础目录: {baseDir}");
                LogManager.Info($"LOT目录: {lotDir}");
                LogManager.Info($"LOT目录存在: {Directory.Exists(lotDir)}");
                
                if (!Directory.Exists(lotDir))
                {
                    string errorMsg = $"LOT文件夹不存在: {lotDir}";
                    LogManager.Warning(errorMsg);
                    MessageBox.Show(errorMsg, "路径错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 第一步：扫描所有项目文件夹，生成项目按键
                await ScanAndGenerateProjectButtons(lotDir);

                // 第二步：根据数据源模式扫描渲染图数据
                await ScanRenderImagesByMode(lotDir);

                // 第三步：默认全选所有项目并更新复选框状态
                SelectAllProjects();
                SelectAllCheckBox.IsChecked = true;
                
                // 第四步：自动显示最后一次结果（如果有数据）
                if (_filteredRenderImageItems.Count > 0)
                {
                    // _currentIndex已经在UpdateFilteredRenderImages中设为0，对应最新的图片
                    _ = DisplayCurrentImage();
                }
                
                LogManager.Info($"渲染图查看器初始化完成，模式: {_currentDataSourceMode}，共 {_allProjects.Count} 个项目，{_renderImageItems.Count} 组渲染图");
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载渲染图失败: {ex.Message}");
                MessageBox.Show($"加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 扫描项目文件夹并生成项目选择按键
        /// </summary>
        private async Task ScanAndGenerateProjectButtons(string lotDir)
        {
            try
            {
                _allProjects.Clear();

                if (_currentDataSourceMode == DataSourceMode.CurrentStatistics)
                {
                    // 当前统计模式：只为有统计数据的项目生成按键
                    var statisticsData = GetCurrentStatisticsData();
                    foreach (var kvp in statisticsData)
                    {
                        string ngType = kvp.Key;
                        int count = kvp.Value;
                        
                        if (count > 0)
                        {
                            string projectDir = Path.Combine(lotDir, ngType);
                            if (Directory.Exists(projectDir))
                            {
                                _allProjects.Add(ngType);
                            }
                        }
                    }
                    
                    _allProjects.Sort();
                    LogManager.Info($"当前统计模式：找到 {_allProjects.Count} 个有数据的项目: {string.Join(", ", _allProjects)}");
                }
                else
                {
                    // LOT内所有模式：扫描所有项目文件夹
                    var allDirs = Directory.GetDirectories(lotDir);
                    LogManager.Info($"LOT目录下所有文件夹: {string.Join(", ", allDirs.Select(Path.GetFileName))}");
                    
                    var projectDirs = allDirs
                        .Where(dir => !Path.GetFileName(dir).Equals("OK", StringComparison.OrdinalIgnoreCase))
                        .Select(Path.GetFileName)
                        .OrderBy(name => name)
                        .ToList();

                    _allProjects.AddRange(projectDirs);
                    LogManager.Info($"LOT内所有模式：找到 {_allProjects.Count} 个项目: {string.Join(", ", _allProjects)}");
                }
                
                // 生成项目按键
                await Dispatcher.InvokeAsync(() => GenerateProjectButtons());
            }
            catch (Exception ex)
            {
                LogManager.Error($"扫描项目文件夹失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 生成项目选择按键
        /// </summary>
        private void GenerateProjectButtons()
        {
            try
            {
                ProjectButtonsPanel.Children.Clear();
                _projectButtons.Clear();

                foreach (string project in _allProjects)
                {
                    Button projectButton = new Button
                    {
                        Content = project,
                        FontSize = 12,
                        Width = 100,
                        Height = 35,
                        Margin = new Thickness(5),
                        Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)), // 默认灰色
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        ToolTip = $"点击切换项目'{project}'的选择状态"
                    };

                    projectButton.Click += (s, e) => ToggleProjectSelection(project);
                    
                    _projectButtons[project] = projectButton;
                    ProjectButtonsPanel.Children.Add(projectButton);
                }

                LogManager.Info($"生成了 {_projectButtons.Count} 个项目按键");
            }
            catch (Exception ex)
            {
                LogManager.Error($"生成项目按键失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据数据源模式扫描渲染图数据
        /// </summary>
        private async Task ScanRenderImagesByMode(string lotDir)
        {
            try
            {
                _renderImageItems.Clear();
                
                if (_currentDataSourceMode == DataSourceMode.CurrentStatistics)
                {
                    await ScanRenderImagesByCurrentStatistics(lotDir);
                }
                else
                {
                    await ScanAllRenderImagesInLot(lotDir);
                }

                // 严格按图号（int）降序排列（最新的在前），不按NgType分组
                _renderImageItems = _renderImageItems
                    .OrderByDescending(x => int.TryParse(x.ImageNumber, out int num) ? num : 0)
                    .ToList();

                LogManager.Info($"扫描完成({_currentDataSourceMode})，共找到 {_renderImageItems.Count} 组渲染图");
            }
            catch (Exception ex)
            {
                LogManager.Error($"扫描渲染图数据失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 扫描LOT内所有渲染图数据
        /// </summary>
        private async Task ScanAllRenderImagesInLot(string lotDir)
        {
            try
            {
                // 直接扫描LOT文件夹下的所有项目目录，不依赖_allProjects
                var allDirs = Directory.GetDirectories(lotDir);
                var projectDirs = allDirs
                    .Where(dir => !Path.GetFileName(dir).Equals("OK", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                LogManager.Info($"LOT内所有模式：扫描 {projectDirs.Count} 个项目目录");

                foreach (string projectDir in projectDirs)
                {
                    if (Directory.Exists(projectDir))
                    {
                        LogManager.Info($"正在扫描项目目录(LOT内所有): {projectDir}");
                        await ScanNGDirectory(projectDir);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"扫描LOT内所有图片失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 根据当前统计扫描渲染图数据
        /// </summary>
        private async Task ScanRenderImagesByCurrentStatistics(string lotDir)
        {
            try
            {
                // 获取当前统计数据
                var statisticsData = GetCurrentStatisticsData();
                LogManager.Info($"获取到当前统计数据: {string.Join(", ", statisticsData.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

                foreach (var kvp in statisticsData)
                {
                    string ngType = kvp.Key;
                    int count = kvp.Value;
                    
                    if (count > 0)
                    {
                        string projectDir = Path.Combine(lotDir, ngType);
                        if (Directory.Exists(projectDir))
                        {
                            LogManager.Info($"正在扫描项目目录(当前统计-{count}张): {projectDir}");
                            await ScanNGDirectoryByCount(projectDir, count);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"根据当前统计扫描图片失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取当前统计数据
        /// </summary>
        private Dictionary<string, int> GetCurrentStatisticsData()
        {
            try
            {
                // 从TemplateConfigPage.StatisticsManager获取当前统计数据
                var defectTypeCounter = TemplateConfigPage.StatisticsManager.DefectTypeCounter;
                LogManager.Info($"StatisticsManager数据源: {string.Join(", ", defectTypeCounter.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");
                
                return new Dictionary<string, int>(defectTypeCounter);
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取当前统计数据失败: {ex.Message}");
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// 按数量扫描NG目录（取后缀最大的N张图片）
        /// </summary>
        private async Task ScanNGDirectoryByCount(string ngDir, int maxCount)
        {
            try
            {
                string ngType = Path.GetFileName(ngDir);
                string renderDir = Path.Combine(ngDir, "渲染图");
                
                if (!Directory.Exists(renderDir))
                {
                    LogManager.Warning($"渲染图目录不存在: {renderDir}");
                    return;
                }

                // 获取所有2D渲染图文件
                var allFiles = Directory.GetFiles(renderDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => !Path.GetFileName(file).StartsWith("gray_") && 
                                   !Path.GetFileName(file).StartsWith("height_"))
                    .ToList();

                if (allFiles.Count == 0)
                {
                    LogManager.Warning($"未找到2D渲染图文件: {renderDir}");
                    return;
                }

                // 解析文件名并按图号排序，取最大的N个
                var fileInfos = allFiles
                    .Select(filePath => new
                    {
                        FilePath = filePath,
                        ImageNumber = ExtractImageNumberFromFileName(Path.GetFileNameWithoutExtension(filePath))
                    })
                    .Where(info => info.ImageNumber > 0)
                    .OrderByDescending(info => info.ImageNumber) // 降序排列，取最大的
                    .Take(maxCount)
                    .ToList();

                LogManager.Info($"找到 {allFiles.Count} 个文件，按统计数量筛选出最新的 {fileInfos.Count} 个文件");

                foreach (var fileInfo in fileInfos)
                {
                    try
                    {
                        // 【统一】使用统一的创建方法
                        var renderItem = CreateRenderImageItem(ngType, fileInfo.ImageNumber.ToString(), fileInfo.FilePath, ngDir);
                        if (renderItem != null)
                        {
                            _renderImageItems.Add(renderItem);
                        }
                        LogManager.Info($"添加渲染图项目: {ngType}_{fileInfo.ImageNumber}");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"处理渲染图文件失败 {fileInfo.FilePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"按数量扫描NG目录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件名中提取图号
        /// </summary>
        private int ExtractImageNumberFromFileName(string fileNameWithoutExtension)
        {
            try
            {
                // 使用下划线分割，取最后一部分作为图号
                var parts = fileNameWithoutExtension.Split('_');
                if (parts.Length >= 2)
                {
                    string lastPart = parts[parts.Length - 1];
                    if (int.TryParse(lastPart, out int imageNumber))
                    {
                        return imageNumber;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 【统一】创建渲染图项目（包含3D图片查找和超限记录加载）
        /// </summary>
        private RenderImageItem CreateRenderImageItem(string ngType, string imageNumber, string renderImagePath, string ngDir)
        {
            try
            {
                var item = new RenderImageItem
                {
                    NgType = ngType,
                    ImageNumber = imageNumber,
                    RenderImagePath = renderImagePath
                };

                // 统一的3D图片查找（在NG目录下找3D文件夹）
                Find3DImages(ngDir, imageNumber, item);
                
                // 统一的超限记录加载
                LoadOutOfRangeRecord(ngDir, imageNumber, item);

                return item;
            }
            catch (Exception ex)
            {
                LogManager.Error($"创建渲染图项目失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 【统一】加载超限记录 - 适配新的存储结构（每个LOT号一个文件）
        /// </summary>
        private void LoadOutOfRangeRecord(string ngDir, string imageNumber, RenderImageItem renderItem)
        {
            try
            {
                // 适配新的存储结构：LOT号文件夹/超限记录_{LOT号}.json
                string lotDir = Path.GetDirectoryName(ngDir);
                string recordFile = Path.Combine(lotDir, $"超限记录_{_currentLotValue}.json");
                
                if (File.Exists(recordFile))
                {
                    string jsonContent = File.ReadAllText(recordFile, System.Text.Encoding.UTF8);
                    
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        // 解析所有超限记录
                        var allRecords = JsonConvert.DeserializeObject<List<OutOfRangeRecord>>(jsonContent);
                        
                        if (allRecords != null && allRecords.Count > 0)
                        {
                            // 【统一】图号匹配：转换为int比较，自动处理前导0
                            var matchingRecord = allRecords.Find(r => 
                            {
                                if (int.TryParse(imageNumber, out int targetNumber) && 
                                    int.TryParse(r.ImageNumber, out int recordNumber))
                                {
                                    return targetNumber == recordNumber;
                                }
                                return false;
                            });
                            
                            if (matchingRecord != null)
                            {
                                renderItem.OutOfRangeRecord = matchingRecord;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning($"加载超限记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 扫描NG目录中的渲染图
        /// </summary>
        private async Task ScanNGDirectory(string ngDir)
        {
            try
            {
                string ngType = Path.GetFileName(ngDir);
                string renderDir = Path.Combine(ngDir, "渲染图");
                
                LogManager.Info($"扫描NG类型: {ngType}");
                LogManager.Info($"渲染图目录: {renderDir}");
                LogManager.Info($"渲染图目录存在: {Directory.Exists(renderDir)}");
                
                if (!Directory.Exists(renderDir))
                {
                    LogManager.Info($"NG类型 {ngType} 没有渲染图文件夹，跳过");
                    return; // 没有渲染图文件夹
                }

                // 扫描渲染图文件
                var renderFiles = Directory.GetFiles(renderDir, "*.png")
                    .Concat(Directory.GetFiles(renderDir, "*.jpg"))
                    .Concat(Directory.GetFiles(renderDir, "*.bmp"))
                    .ToList();

                LogManager.Info($"在 {ngType} 中找到 {renderFiles.Count} 个渲染图文件");
                LogManager.Info($"渲染图文件列表: {string.Join(", ", renderFiles.Select(Path.GetFileName))}");

                foreach (var renderFile in renderFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(renderFile);
                    LogManager.Info($"处理渲染图文件: {fileName}");
                    
                    // 【统一】使用统一的图号提取和处理逻辑
                    int imageNumber = ExtractImageNumberFromFileName(fileName);
                    if (imageNumber > 0)
                    {
                        // 【统一】使用统一的创建方法  
                        var item = CreateRenderImageItem(ngType, imageNumber.ToString(), renderFile, ngDir);
                        if (item != null)
                        {
                            _renderImageItems.Add(item);
                            LogManager.Info($"成功添加渲染图项目: {ngType} - 图号{imageNumber}");
                        }
                    }
                    else
                    {
                        LogManager.Warning($"渲染图文件名无法提取图号: {fileName}，期望格式: 前缀_图号");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"扫描NG目录失败 {ngDir}: {ex.Message}");
            }
        }

        /// <summary>
        /// 【统一】查找3D图像 - 通过int匹配找到真实文件名
        /// </summary>
        private void Find3DImages(string ngDir, string imageNumber, RenderImageItem item)
        {
            try
            {
                string threeDDir = Path.Combine(ngDir, "3D");
                
                if (!Directory.Exists(threeDDir))
                    return;

                // 【修复】通过int匹配找到真实文件名
                if (!int.TryParse(imageNumber, out int targetNumber))
                    return;

                var allFiles = Directory.GetFiles(threeDDir, "*.png");
                
                // 查找匹配的灰度图和高度图
                string actualGrayFile = null;
                string actualHeightFile = null;
                
                foreach (var file in allFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    
                    if (fileName.StartsWith("gray_"))
                    {
                        string numberPart = fileName.Substring(5); // 去掉"gray_"前缀
                        if (int.TryParse(numberPart, out int fileNumber) && fileNumber == targetNumber)
                        {
                            actualGrayFile = file;
                        }
                    }
                    else if (fileName.StartsWith("height_"))
                    {
                        string numberPart = fileName.Substring(7); // 去掉"height_"前缀
                        if (int.TryParse(numberPart, out int fileNumber) && fileNumber == targetNumber)
                        {
                            actualHeightFile = file;
                        }
                    }
                }
                
                // 如果找到了匹配的文件对
                if (!string.IsNullOrEmpty(actualGrayFile) && !string.IsNullOrEmpty(actualHeightFile))
                {
                    item.GrayImagePath = actualGrayFile;
                    item.HeightImagePath = actualHeightFile;
                    item.Has3DImages = true;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"查找3D图像失败: {ex.Message}");
            }
        }



        /// <summary>
        /// 显示当前图像
        /// </summary>
        private async Task DisplayCurrentImage()
        {
            if (_filteredRenderImageItems.Count == 0 || _currentIndex < 0 || _currentIndex >= _filteredRenderImageItems.Count)
            {
                return;
            }

            try
            {
                var currentItem = _filteredRenderImageItems[_currentIndex];
                
                // 更新图片信息
                UpdateImageInfo(currentItem);
                
                // 显示2D渲染图
                await Display2DImage(currentItem);
                
                // 显示3D图像
                if (currentItem.Has3DImages)
                {
                    await Display3DImage(currentItem);
                }
                
                // 显示检测结果和超限项目
                DisplayDetectionInfo(currentItem);
                
                LogManager.Info($"已显示图像 {_currentIndex + 1}/{_filteredRenderImageItems.Count}: {currentItem.NgType}_{currentItem.ImageNumber}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示图像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示2D渲染图 - 使用SDK正确的SetImagePath方法
        /// </summary>
        private async Task Display2DImage(RenderImageItem item)
        {
            try
            {
                LogManager.Info($"[2D显示调试] 开始显示2D图像: {item.RenderImagePath}");
                LogManager.Info($"[2D显示调试] 文件存在: {File.Exists(item.RenderImagePath)}, 图像源模块: {(_imageSource2D != null ? "已初始化" : "未初始化")}");
                
                if (File.Exists(item.RenderImagePath))
                {
                    // 使用SDK正确的方法设置图像路径显示
                    if (_imageSource2D != null)
                    {
                        _imageSource2D.SetImagePath(item.RenderImagePath);
                        var imagePathProcedure = VmSolution.Instance["渲染图显示"] as VmProcedure;
                        if (imagePathProcedure != null)
                        {
                            imagePathProcedure.Run();
                            LogManager.Info($"[2D显示调试] ✅ 已设置2D渲染图路径并执行VM流程: {item.RenderImagePath}");
                        }
                        else
                        {
                            LogManager.Warning("[2D显示调试] ❌ VM流程'渲染图显示'未找到");
                        }
                    }
                    else
                    {
                        LogManager.Warning("[2D显示调试] ❌ 2D图像源模块未初始化");
                    }
                }
                else
                {
                    LogManager.Warning($"[2D显示调试] ❌ 2D渲染图文件不存在: {item.RenderImagePath}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[2D显示调试] ❌ 显示2D图像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示3D图像 - Review模式，使用渲染图查看器专用静态实例
        /// </summary>
        private async Task Display3DImage(RenderImageItem item)
        {
            try
            {
                if (item.Has3DImages && File.Exists(item.GrayImagePath) && File.Exists(item.HeightImagePath))
                {
                    LogManager.Info($"[3D显示调试] 开始显示3D图像，高度图: {Path.GetFileName(item.HeightImagePath)}，灰度图: {Path.GetFileName(item.GrayImagePath)}");
                    LogManager.Info($"[3D显示调试] 高度图路径: {item.HeightImagePath}");
                    LogManager.Info($"[3D显示调试] 灰度图路径: {item.GrayImagePath}");
                    LogManager.Info($"[3D显示调试] 高度图存在: {File.Exists(item.HeightImagePath)}, 灰度图存在: {File.Exists(item.GrayImagePath)}");
                    LogManager.Info($"[3D显示调试] 静态实例状态: {(_StaticRenderViewerMeasureEx != null ? "已创建" : "未创建")}, 使能状态: {(_StaticRenderViewerMeasureEx?.IsEnable ?? false)}");

                    // 检查静态3D实例是否可用
                    if (_StaticRenderViewerMeasureEx == null || !_StaticRenderViewerMeasureEx.IsEnable)
                    {
                        LogManager.Warning("[3D显示调试] ❌ 静态3D实例未启用或不可用，跳过3D图像显示");
                        return;
                    }

                    // 加载3D图像
                    LHeightImage heightImg = new LHeightImage();
                    LGrayImage grayImg = new LGrayImage();

                    heightImg.Read(item.HeightImagePath);
                    grayImg.Read(item.GrayImagePath);

                    if (!heightImg.IsEnable() || !grayImg.IsEnable())
                    {
                        LogManager.Error("高度图或灰度图加载失败");
                        return;
                    }

                    LogManager.Info("3D图像加载成功，开始执行Review模式3D检测（静态实例）");

                    // 【修复】使用静态实例的Execute方法，触发静态回调
                    await Task.Run(() =>
                    {
                        try
                        {
                            LHeightImage[] heightImages = { heightImg };
                            LGrayImage[] grayImages = { grayImg };

                            // 直接调用静态实例的Execute方法（不保存，不导出）
                            bool executeResult = _StaticRenderViewerMeasureEx.Execute(heightImages, grayImages,
                                                                          exportData: false,
                                                                          saveImage: false);

                            LogManager.Info($"渲染图查看器Review模式3D检测完成，结果: {executeResult}");
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"渲染图查看器Review模式3D检测执行失败: {ex.Message}");
                        }
                    });
                }
                else
                {
                    LogManager.Warning($"3D图像文件检查失败 - Has3DImages:{item.Has3DImages}, GrayExists:{File.Exists(item.GrayImagePath)}, HeightExists:{File.Exists(item.HeightImagePath)}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"显示3D图像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示检测信息
        /// </summary>
        private void DisplayDetectionInfo(RenderImageItem item)
        {
            try
            {
                LogManager.Info($"[检测信息调试] 显示检测信息 - 缺陷类型: {item.NgType}, 图号: {item.ImageNumber}");
                LogManager.Info($"[检测信息调试] 超限记录状态: {(item.OutOfRangeRecord != null ? "有记录" : "无记录")}");
                
                // 更新检测结果信息
                DefectTypeText.Text = item.NgType;
                ImageNumberText.Text = item.ImageNumber;
                
                if (item.OutOfRangeRecord != null)
                {
                    DetectionTimeText.Text = item.OutOfRangeRecord.DetectionTime.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    // 显示超限项目
                    OutOfRangeDataGrid.ItemsSource = item.OutOfRangeRecord.OutOfRangeItems;
                    
                    LogManager.Info($"[检测信息调试] ✅ 已设置检测时间: {item.OutOfRangeRecord.DetectionTime:yyyy-MM-dd HH:mm:ss}");
                    LogManager.Info($"[检测信息调试] ✅ 已设置超限项目数量: {item.OutOfRangeRecord.OutOfRangeItems?.Count ?? 0}");
                }
                else
                {
                    DetectionTimeText.Text = "--";
                    OutOfRangeDataGrid.ItemsSource = null;
                    
                    LogManager.Warning("[检测信息调试] ❌ 无超限记录，显示默认值");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"[检测信息调试] ❌ 显示检测信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新图片信息显示
        /// </summary>
        private void UpdateImageInfo(RenderImageItem item)
        {
            ImageIndexText.Text = $"{_currentIndex + 1} / {_filteredRenderImageItems.Count}";
            CurrentImagePathText.Text = Path.GetFileName(item.RenderImagePath);
            
            // 更新项目选择栏中的序号显示
            UpdateCurrentImageIndexDisplay();
        }

        /// <summary>
        /// 更新图片信息显示（无参数版本）
        /// </summary>
        private void UpdateImageInfo()
        {
            if (_filteredRenderImageItems.Count > 0 && _currentIndex >= 0 && _currentIndex < _filteredRenderImageItems.Count)
            {
                UpdateImageInfo(_filteredRenderImageItems[_currentIndex]);
            }
            else
            {
                ImageIndexText.Text = "0 / 0";
                CurrentImagePathText.Text = "--";
                
                // 更新项目选择栏中的序号显示
                UpdateCurrentImageIndexDisplay();
            }
        }

        /// <summary>
        /// 更新项目选择栏中的当前图序号显示
        /// </summary>
        private void UpdateCurrentImageIndexDisplay()
        {
            if (_filteredRenderImageItems.Count > 0)
            {
                CurrentImageIndexText.Text = $"{_currentIndex + 1}/{_filteredRenderImageItems.Count}";
            }
            else
            {
                CurrentImageIndexText.Text = "0/0";
            }
        }

        #region 按钮事件处理

        private async void FirstImageButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止当前的自动播放（如果有）
            StopAutoPlay();
            
            // 开始向前自动播放到第一张
            if (_filteredRenderImageItems.Count > 0 && _currentIndex > 0)
            {
                await StartAutoPlayToFirst();
            }
        }

        private async void PreviousImageButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止自动播放
            StopAutoPlay();
            
            if (_currentIndex > 0)
            {
                _currentIndex--;
                await DisplayCurrentImage();
            }
        }

        private async void NextImageButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止自动播放
            StopAutoPlay();
            
            if (_currentIndex < _filteredRenderImageItems.Count - 1)
            {
                _currentIndex++;
                await DisplayCurrentImage();
            }
        }

        private async void LastImageButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止当前的自动播放（如果有）
            StopAutoPlay();
            
            // 开始向后自动播放到最后一张
            if (_filteredRenderImageItems.Count > 0 && _currentIndex < _filteredRenderImageItems.Count - 1)
            {
                await StartAutoPlayToLast();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止自动播放
            StopAutoPlay();
        }

        #endregion

        #region 自动播放功能

        /// <summary>
        /// 开始向前自动播放到第一张图片
        /// </summary>
        private async Task StartAutoPlayToFirst()
        {
            if (_isAutoPlaying) return;
            
            try
            {
                _isAutoPlaying = true;
                _autoPlayCancellationTokenSource = new System.Threading.CancellationTokenSource();
                
                LogManager.Info($"开始向前自动播放，从索引 {_currentIndex} 到第一张");
                
                while (_currentIndex > 0 && !_autoPlayCancellationTokenSource.Token.IsCancellationRequested)
                {
                    _currentIndex--;
                    await DisplayCurrentImage();
                    
                    // 等待指定间隔
                    await Task.Delay(AUTO_PLAY_INTERVAL_MS, _autoPlayCancellationTokenSource.Token);
                }
                
                LogManager.Info("向前自动播放完成");
            }
            catch (OperationCanceledException)
            {
                LogManager.Info("向前自动播放被取消");
            }
            catch (Exception ex)
            {
                LogManager.Error($"向前自动播放异常: {ex.Message}");
            }
            finally
            {
                _isAutoPlaying = false;
                _autoPlayCancellationTokenSource?.Dispose();
                _autoPlayCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 开始向后自动播放到最后一张图片
        /// </summary>
        private async Task StartAutoPlayToLast()
        {
            if (_isAutoPlaying) return;
            
            try
            {
                _isAutoPlaying = true;
                _autoPlayCancellationTokenSource = new System.Threading.CancellationTokenSource();
                
                LogManager.Info($"开始向后自动播放，从索引 {_currentIndex} 到最后一张");
                
                while (_currentIndex < _filteredRenderImageItems.Count - 1 && !_autoPlayCancellationTokenSource.Token.IsCancellationRequested)
                {
                    _currentIndex++;
                    await DisplayCurrentImage();
                    
                    // 等待指定间隔
                    await Task.Delay(AUTO_PLAY_INTERVAL_MS, _autoPlayCancellationTokenSource.Token);
                }
                
                LogManager.Info("向后自动播放完成");
            }
            catch (OperationCanceledException)
            {
                LogManager.Info("向后自动播放被取消");
            }
            catch (Exception ex)
            {
                LogManager.Error($"向后自动播放异常: {ex.Message}");
            }
            finally
            {
                _isAutoPlaying = false;
                _autoPlayCancellationTokenSource?.Dispose();
                _autoPlayCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 停止自动播放
        /// </summary>
        private void StopAutoPlay()
        {
            if (_isAutoPlaying && _autoPlayCancellationTokenSource != null)
            {
                _autoPlayCancellationTokenSource.Cancel();
                LogManager.Info("停止自动播放");
            }
        }

        #endregion



        /// <summary>
        /// 应用Page1的3D颜色配置到渲染图查看器
        /// </summary>
        private void ApplyPage1ColorConfig(LjdExecuteResult result)
        {
            try
            {
                var page1Instance = Page1.PageManager.Page1Instance;
                if (page1Instance?._3DColorConfig == null) return;

                var lj3DView = _View3D.LJView3D;
                var colorConfig = page1Instance._3DColorConfig;

                if (colorConfig.UseCustomColorRange)
                {
                    var customColorRange = LColorRange.Create(
                        lj3DView.ColorRange.UpperLimit / 32768,
                        colorConfig.ColorRangeMin,
                        colorConfig.ColorRangeMax
                    );
                    lj3DView.ColorRange = customColorRange;
                }
                else
                {
                    lj3DView.ColorRange = result.DstHeightImages.Length > 1 
                        ? Lj3DView.GetFitRange(result.DstHeightImages)
                        : Lj3DView.GetFitRange(new LHeightImage[] { result.DstHeightImage });
                }

                lj3DView.MeshTransparent = colorConfig.MeshTransparent;
                lj3DView.BlendWeight = colorConfig.BlendWeight;
                lj3DView.DisplayColorBar = colorConfig.DisplayColorBar;
                lj3DView.DisplayGrid = colorConfig.DisplayGrid;
                lj3DView.DisplayAxis = colorConfig.DisplayAxis;
            }
            catch (Exception ex)
            {
                LogManager.Warning($"应用Page1颜色配置失败: {ex.Message}");
                _View3D.LJView3D.ColorRangeFitCommand.Execute(); // 降级到自适应
            }
        }

        /// <summary>
        /// 窗口关闭时的清理工作
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 停止自动播放
                StopAutoPlay();

                // 清理视图资源（不需要清理静态3D实例，它会被其他窗口复用）
                _View3D?.Dispose();
                _imageSource2D = null;

                LogManager.Info("渲染图查看器已关闭，视图资源已清理（静态3D实例保留）");
            }
            catch (Exception ex)
            {
                LogManager.Error($"清理资源失败: {ex.Message}");
            }

            base.OnClosed(e);
        }
        /// <summary>
        /// 切换项目选择状态
        /// </summary>
        private void ToggleProjectSelection(string projectName)
        {
            try
            {
                bool wasSelected = _selectedProjects.Contains(projectName);
                
                if (wasSelected)
                {
                    _selectedProjects.Remove(projectName);
                    UpdateProjectButtonAppearance(projectName, false);
                }
                else
                {
                    _selectedProjects.Add(projectName);
                    UpdateProjectButtonAppearance(projectName, true);
                }

                // 更新全选复选框状态
                UpdateSelectAllCheckBoxState();

                // 如果是单独选择一个项目（从全选状态切换到单选），自动跳转到该项目的最后一张图片
                bool isSingleProjectSelected = _selectedProjects.Count == 1 && !wasSelected;

                // 更新过滤后的渲染图列表
                UpdateFilteredRenderImages();
                
                // 如果是单选状态，跳转到该项目的最新图片
                if (isSingleProjectSelected && _filteredRenderImageItems.Count > 0)
                {
                    // 找到该项目的第一张图片索引（因为已按时间降序排列，第一张就是最新的）
                    int targetIndex = _filteredRenderImageItems.FindIndex(item => item.NgType == projectName);
                    if (targetIndex >= 0)
                    {
                        _currentIndex = targetIndex;
                        _ = DisplayCurrentImage();
                        LogManager.Info($"自动跳转到项目'{projectName}'的最新图片: 索引{targetIndex}");
                    }
                }
                
                LogManager.Info($"项目'{projectName}'选择状态已切换，当前选中项目数: {_selectedProjects.Count}");
            }
            catch (Exception ex)
            {
                LogManager.Error($"切换项目选择状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新全选复选框状态
        /// </summary>
        private void UpdateSelectAllCheckBoxState()
        {
            try
            {
                if (_allProjects.Count == 0)
                {
                    SelectAllCheckBox.IsChecked = false;
                }
                else if (_selectedProjects.Count == _allProjects.Count)
                {
                    SelectAllCheckBox.IsChecked = true;
                }
                else if (_selectedProjects.Count == 0)
                {
                    SelectAllCheckBox.IsChecked = false;
                }
                else
                {
                    SelectAllCheckBox.IsChecked = null; // 部分选择状态
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新全选复选框状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新项目按键外观
        /// </summary>
        private void UpdateProjectButtonAppearance(string projectName, bool isSelected)
        {
            if (_projectButtons.TryGetValue(projectName, out Button button))
            {
                button.Background = new SolidColorBrush(isSelected 
                    ? Color.FromRgb(52, 152, 219)  // 选中：蓝色
                    : Color.FromRgb(149, 165, 166)); // 未选中：灰色
            }
        }

        /// <summary>
        /// 数据源模式切换事件
        /// </summary>
        private async void DataSourceModeToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 切换数据源模式
                _currentDataSourceMode = DataSourceModeToggle.IsChecked == true 
                    ? DataSourceMode.AllInLot 
                    : DataSourceMode.CurrentStatistics;

                LogManager.Info($"数据源模式切换为: {_currentDataSourceMode}");

                // 重新扫描数据
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string lotDir = Path.Combine(baseDir, "原图存储", _currentLotValue);
                
                if (Directory.Exists(lotDir))
                {
                    // 先重新生成项目按键（因为不同模式下可用项目可能不同）
                    await ScanAndGenerateProjectButtons(lotDir);
                    
                    // 再根据新的项目列表扫描数据
                    await ScanRenderImagesByMode(lotDir);
                    
                    // 重新全选并更新显示
                    SelectAllProjects();
                    SelectAllCheckBox.IsChecked = true;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"切换数据源模式失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 全选复选框选中事件
        /// </summary>
        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SelectAllProjects();
        }

        /// <summary>
        /// 全选复选框取消选中事件
        /// </summary>
        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ClearAllSelection();
        }

        /// <summary>
        /// 全选所有项目
        /// </summary>
        private void SelectAllProjects()
        {
            try
            {
                _selectedProjects.Clear();
                _selectedProjects.UnionWith(_allProjects);

                // 更新所有按键外观
                foreach (string project in _allProjects)
                {
                    UpdateProjectButtonAppearance(project, true);
                }

                // 更新过滤后的渲染图列表
                UpdateFilteredRenderImages();
                LogManager.Info($"已全选所有项目，共 {_selectedProjects.Count} 个项目");
            }
            catch (Exception ex)
            {
                LogManager.Error($"全选项目失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消所有选择
        /// </summary>
        private void ClearAllSelection()
        {
            try
            {
                _selectedProjects.Clear();

                // 更新所有按键外观
                foreach (string project in _allProjects)
                {
                    UpdateProjectButtonAppearance(project, false);
                }

                // 更新过滤后的渲染图列表
                UpdateFilteredRenderImages();
                LogManager.Info("已取消所有项目选择");
            }
            catch (Exception ex)
            {
                LogManager.Error($"取消全选失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新过滤后的渲染图列表
        /// </summary>
        private void UpdateFilteredRenderImages()
        {
            try
            {
                if (_selectedProjects.Count == 0)
                {
                    // 没有选择任何项目
                    _filteredRenderImageItems.Clear();
                    _currentIndex = 0;
                    ClearDisplay();
                    UpdateImageInfo();
                    LogManager.Info("未选择任何项目，已清空显示");
                }
                else
                {
                    // 过滤出选中项目的渲染图，严格按时间排序（最新的在前）
                    _filteredRenderImageItems = _renderImageItems
                        .Where(item => _selectedProjects.Contains(item.NgType))
                        .OrderByDescending(x => int.TryParse(x.ImageNumber, out int num) ? num : 0)
                        .ToList();

                    LogManager.Info($"过滤后的渲染图数量: {_filteredRenderImageItems.Count}");

                    if (_filteredRenderImageItems.Count > 0)
                    {
                        // 自动显示最新的图片（索引0，因为已经按时间降序排列）
                        _currentIndex = 0;
                        _ = DisplayCurrentImage(); // 异步显示，不等待
                    }
                    else
                    {
                        ClearDisplay();
                        _currentIndex = 0;
                        // 即使没有图片也要更新序号显示
                        UpdateCurrentImageIndexDisplay();
                    }
                    
                    UpdateImageInfo();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"更新过滤后的渲染图列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清空显示内容
        /// </summary>
        private void ClearDisplay()
        {
            try
            {

                // 清空检测结果
                DefectTypeText.Text = "--";
                DetectionTimeText.Text = "--";
                ImageNumberText.Text = "--";

                // 清空超限项目详情
                OutOfRangeDataGrid.ItemsSource = null;
            }
            catch (Exception ex)
            {
                LogManager.Error($"清空显示内容失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 数据源模式枚举
    /// </summary>
    public enum DataSourceMode
    {
        CurrentStatistics,  // 当前统计：从Page1获取各类NG数目，收集最新的N张图片
        AllInLot           // LOT内所有：收集LOT文件夹内所有图片
    }

    /// <summary>
    /// 渲染图项目数据类
    /// </summary>
    public class RenderImageItem
    {
        public string NgType { get; set; }
        public string ImageNumber { get; set; }
        public string RenderImagePath { get; set; }
        public string GrayImagePath { get; set; }
        public string HeightImagePath { get; set; }
        public bool Has3DImages { get; set; }
        public OutOfRangeRecord OutOfRangeRecord { get; set; }
    }
} 