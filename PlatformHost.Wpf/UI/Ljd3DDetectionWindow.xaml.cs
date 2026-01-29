using System;
using System.Collections.Generic;
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
using LjDevExt;
using LjdSampleWrapper;
using Keyence.LjDevCommon;
using Keyence.LjDevMeasure;
using Keyence.LjDev3dView;
using Microsoft.Win32;
using System.IO;
using WpfApp2.Models;
using WpfApp2.UI.Models;
using Path = System.IO.Path;
using PageManager = WpfApp2.UI.Page1.PageManager;

namespace WpfApp2.UI
{
    /// <summary>
    /// Ljd3DDetectionWindow.xaml 的交互逻辑
    /// 
    /// **性能问题修复说明:**
    /// 原问题：程序重启后3D检测性能大幅下降
    /// 根本原因：3D视图、GPU资源和激光头硬件资源释放不完全
    /// 
    /// **修复措施:**
    /// 1. 强制清理3D视图资源，包括GPU缓存
    /// 2. 添加垃圾回收确保非托管资源释放
    /// 3. 增加硬件资源释放等待时间
    /// 4. 简化资源释放逻辑，模仿官方示例
    /// 5. 确保事件订阅完全取消，避免内存泄漏
    /// </summary>
    public partial class Ljd3DDetectionWindow : Window
    {
        private LjdevExt2dView _View2D = new LjdevExt2dView();
        private Ljd3DViewEx _View3D = new Ljd3DViewEx();
        private LjdMeasureEx _MeasureEx = null;

        // 硬编码激光头IP端口配置
        private const string LASER_IP_PORT = "192.168.0.1:24691:24692";

        /// <summary>
        /// 内存中的3D检测参数（用于后续保存到模板）
        /// </summary>
        public static Detection3DParameters CurrentDetection3DParams { get; set; } = new Detection3DParameters();

        /// <summary>
        /// 标识是否正在初始化窗口（防止初始化期间触发事件处理）
        /// </summary>
        private bool _isInitializing = true;

        // 静态的MeasureEx实例，用于自动启动模式
        private static LjdMeasureEx _StaticMeasureEx = null;

        // 标记是否已初始化3D检测项目
        private static bool _Is3DItemsInitialized = false;

        /// <summary>
        /// 标识当前是否处于3D配置模式（用于跳过统一判定和复杂的图像保存逻辑）
        /// </summary>
        public static bool IsIn3DConfigurationMode { get; set; } = false;

        /// <summary>
        /// 标识当前是否处于图片测试模式（用于区分图片测试和生产模式的回调处理）
        /// </summary>
        public static bool IsInImageTestMode { get; set; } = false;

        public Ljd3DDetectionWindow()
        {
            InitializeComponent();
            InitializeViews();
            InitializeDefaultValues();
            
            // 初始化完成，可以开始响应事件
            _isInitializing = false;
            
            // 设置3D配置模式状态
            IsIn3DConfigurationMode = true;
            LogMessage("已进入3D配置模式，将跳过统一判定和复杂的图像保存逻辑");
            
            // 注册窗口到主窗口管理器
            MainWindow.RegisterDetectionWindow(this);
        }

        private void InitializeViews()
        {
            try
            {
                _2DViewHost.Child = _View2D;
                _3DViewHost.Child = _View3D;
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化视图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeDefaultValues()
        {
            // 自动设置项目名为当前模板名
            string currentTemplateName = PageManager.Page1Instance?.CurrentTemplateName ?? "Default";
            CurrentDetection3DParams.ProjectName = currentTemplateName;
            tbx_ProjectName.Text = currentTemplateName;
            
            // **重要**：ProjectFolder从JSON配置加载，只在没有配置时才设置智能目录
            if (string.IsNullOrEmpty(CurrentDetection3DParams.ProjectFolder))
            {
                // 仅在配置为空时设置智能配置目录
                UpdateProjectConfigDirectory();
            }
            else
            {
                // 使用JSON配置中的ProjectFolder
                tbx_ProjectFolder.Text = CurrentDetection3DParams.ProjectFolder;
            }
            
            cb_ReCompile.IsChecked = CurrentDetection3DParams.ReCompile;
            
            LogMessage($"已初始化3D检测参数: 项目名={currentTemplateName}");
        }

        /// <summary>
        /// 更新项目配置目录到智能目录（Templates/当前模板名/3DConfig）
        /// </summary>
        private void UpdateProjectConfigDirectory()
        {
            try
            {
                // 获取当前模板名
                string currentTemplate = PageManager.Page1Instance?.CurrentTemplateName ?? "Default";
                
                // 使用Smart3DImageManager获取智能配置目录
                string smartConfigDir = Smart3DImageManager.Instance.Get3DProjectConfigDirectory(currentTemplate);
                
                // 更新界面和内存参数
                tbx_ProjectFolder.Text = smartConfigDir;
                CurrentDetection3DParams.ProjectFolder = smartConfigDir;
                
                LogMessage($"3D项目配置目录已设置为: Templates/{currentTemplate}/3DConfig");
            }
            catch (Exception ex)
            {
                LogMessage($"设置智能配置目录失败: {ex.Message}");
                // 使用默认目录作为备用
                tbx_ProjectFolder.Text = CurrentDetection3DParams.ProjectFolder;
            }
        }

        /// <summary>
        /// 执行3D检测（手动模式，不自动保存）
        /// </summary>
        private async void ExecuteWithSmartSaving(LjdMeasureEx measureEx, LHeightImage[] heightImages, LGrayImage[] grayImages)
        {
            try
            {
                LogMessage("开始执行3D检测（手动模式）");
                
                if (measureEx == null || !measureEx.IsEnable)
                {
                    LogMessage("3D检测系统未启动");
                    return;
                }
                
                // 只执行检测，不保存图像
                bool result = await Smart3DImageManager.Instance.ExecuteDetectionOnly(measureEx, heightImages, grayImages);
                
                LogMessage($"3D检测完成，结果: {result}");
            }
            catch (Exception ex)
            {
                LogMessage($"3D检测执行失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 从模板参数中加载3D检测配置
        /// </summary>
        /// <param name="templateParams">模板参数</param>
        public static void LoadFromTemplate(TemplateParameters templateParams)
        {
            if (templateParams?.Detection3DParams != null)
            {
                CurrentDetection3DParams = new Detection3DParameters
                {
                    Enable3DDetection = templateParams.Detection3DParams.Enable3DDetection,
                    ProjectName = templateParams.Detection3DParams.ProjectName,
                    ProjectFolder = templateParams.Detection3DParams.ProjectFolder,
                    HeightImagePath = templateParams.Detection3DParams.HeightImagePath,
                    ReCompile = templateParams.Detection3DParams.ReCompile
                };
            }
        }

        /// <summary>
        /// 将当前3D检测配置应用到模板参数
        /// </summary>
        /// <param name="templateParams">模板参数</param>
        public static void ApplyToTemplate(TemplateParameters templateParams)
        {
            if (templateParams != null)
            {
                templateParams.Detection3DParams = new Detection3DParameters
                {
                    Enable3DDetection = CurrentDetection3DParams.Enable3DDetection,
                    ProjectName = CurrentDetection3DParams.ProjectName,
                    ProjectFolder = CurrentDetection3DParams.ProjectFolder,
                    HeightImagePath = CurrentDetection3DParams.HeightImagePath,
                    ReCompile = CurrentDetection3DParams.ReCompile
                };
            }
        }

        /// <summary>
        /// 保存当前界面的参数到内存
        /// </summary>
        private void SaveCurrentParametersToMemory()
        {
            try
            {
                // 检查控件是否已经初始化
                if (tbx_ProjectName == null || tbx_ProjectFolder == null || cb_ReCompile == null)
                {
                    LogMessage("控件尚未完全初始化，跳过参数保存");
                    return;
                }

                // 检查文本框的值是否为null（项目名现在是只读的，不需要保存）
                string projectFolder = tbx_ProjectFolder.Text ?? "";
                bool reCompile = cb_ReCompile.IsChecked ?? false;

                CurrentDetection3DParams.ProjectFolder = projectFolder;
                CurrentDetection3DParams.ReCompile = reCompile;
                
                LogMessage("已保存3D检测参数到内存");
            }
            catch (Exception ex)
            {
                LogMessage($"保存3D参数到内存失败: {ex.Message}");
            }
        }



        /// <summary>
        /// 选择项目文件夹
        /// </summary>
        private void btn_SelectProjectFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 项目文件夹：不再弹出路径选择器，改为枚举LJ Developer User下的项目并自动定位到 source
                if (TrySelectLJDeveloperUserProjectSourcePath(out string projectSourcePath))
                {
                    tbx_ProjectFolder.Text = projectSourcePath;
                    LogMessage($"已选择项目文件夹(source): {projectSourcePath}");

                    // 自动保存参数到内存（这里不需要检查初始化状态，因为是用户主动操作）
                    SaveCurrentParametersToMemory();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择项目文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TrySelectLJDeveloperUserProjectSourcePath(out string projectSourcePath)
        {
            projectSourcePath = null;

            string userRoot = @"C:\Users\Public\Documents\KEYENCE\LJ Developer\User";
            string useRootFallback = @"C:\Users\Public\Documents\KEYENCE\LJ Developer\Use";
            if (!Directory.Exists(userRoot) && Directory.Exists(useRootFallback))
            {
                userRoot = useRootFallback;
            }

            if (!Directory.Exists(userRoot))
            {
                MessageBox.Show(
                    $"未找到LJ Developer的User目录：\n{userRoot}",
                    "路径不存在",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            string[] projectDirs;
            try
            {
                projectDirs = Directory.GetDirectories(userRoot);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"读取User目录失败: {ex.Message}\n\n{userRoot}",
                    "读取失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            var projectNames = new List<string>();
            foreach (var dir in projectDirs)
            {
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    projectNames.Add(name);
                }
            }

            if (projectNames.Count == 0)
            {
                MessageBox.Show(
                    $"未在目录中发现任何项目文件夹：\n{userRoot}",
                    "未找到项目",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }

            projectNames.Sort(StringComparer.OrdinalIgnoreCase);

            var selectWindow = new LJDeveloperUserProjectSelectWindow(projectNames)
            {
                Owner = this
            };

            if (selectWindow.ShowDialog() != true)
            {
                return false;
            }

            string selectedProjectName = selectWindow.SelectedProjectName;
            if (string.IsNullOrWhiteSpace(selectedProjectName))
            {
                return false;
            }

            string sourcePath = Path.Combine(userRoot, selectedProjectName, "source");
            if (!Directory.Exists(sourcePath))
            {
                MessageBox.Show(
                    $"已选择项目：{selectedProjectName}\n\n但未找到source目录：\n{sourcePath}",
                    "source不存在",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            projectSourcePath = sourcePath;
            return true;
        }

        /// <summary>
        /// 选择测试图像文件
        /// </summary>
        private void btn_SelectTestImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog();
                dialog.Title = "选择测试图像文件";
                dialog.Filter = "图像文件|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|所有文件|*.*";
                dialog.InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MESA检测", "RawImage");
                
                if (dialog.ShowDialog() == true)
                {
                    tbx_SelectedImageFile.Text = dialog.FileName;
                    LogMessage($"已选择测试图像文件: {Path.GetFileName(dialog.FileName)}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择图像文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"选择图像文件失败: {ex.Message}");
            }
        }

        private void btn_Run_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // **修复：先保存当前参数到内存**
                SaveCurrentParametersToMemory();

                // **修复：在手动启动前，先停止自动启动的静态实例以避免硬件资源冲突**
                if (_StaticMeasureEx != null)
                {
                    LogMessage("检测到自动启动的3D检测实例正在运行，正在停止以避免资源冲突...");
                    try
                    {
                        _StaticMeasureEx.ImageExecuted -= StaticMeasureEx_ImageExecuted;
                        _StaticMeasureEx.StopImageReceiving();
                        _StaticMeasureEx.Dispose();
                        _StaticMeasureEx = null;
                        
                        // 强制垃圾回收，确保硬件资源释放
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        
                        // 等待硬件资源释放
                        System.Threading.Thread.Sleep(500);
                        
                        LogMessage("已停止自动启动的3D检测实例，硬件资源已释放");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"停止自动启动实例时出错: {ex.Message}");
                    }
                }

                // **修复：使用官方示例的简洁方式释放之前的手动模式实例**
                if (_MeasureEx != null)
                {
                    try
                    {
                        // 取消事件订阅
                        _MeasureEx.ImageExecuted -= _MeasureEx_ImageExecuted;
                        // 直接释放，让Dispose内部处理停止和清理
                        _MeasureEx.Dispose();
                        _MeasureEx = null;
                        LogMessage("已释放之前的手动模式3D检测实例");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"释放之前手动实例时出错: {ex.Message}");
                    }
                }
                
                LogMessage($"开始创建新的手动3D检测实例 - 项目: {tbx_ProjectName.Text}");
                
                // 使用硬编码的激光头IP端口配置，根据项目规则不使用LJS和TCP服务
                _MeasureEx = new LjdMeasureEx(
                    tbx_ProjectName.Text, 
                    tbx_ProjectFolder.Text,
                    LASER_IP_PORT, // 使用硬编码的激光头配置
                    false, // 不使用LJS（根据项目规则）
                    0,     // 不使用TCP端口（根据项目规则）
                    (bool)cb_ReCompile.IsChecked, 
                    true, 
                    ""
                );

                // 订阅事件
                _MeasureEx.ImageExecuted += _MeasureEx_ImageExecuted;
                
                LogMessage($"手动3D检测系统启动成功 - 激光头配置: {LASER_IP_PORT}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动系统失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"手动3D检测系统启动失败: {ex.Message}");
                
                // 清理失败的资源
                if (_MeasureEx != null)
                {
                    try
                    {
                        _MeasureEx.ImageExecuted -= _MeasureEx_ImageExecuted;
                        _MeasureEx.Dispose();
                        _MeasureEx = null;
                    }
                    catch (Exception cleanupEx)
                    {
                        LogMessage($"清理失败的手动实例时出错: {cleanupEx.Message}");
                    }
                }
            }
        }

        private void _MeasureEx_ImageExecuted(LjdMeasureEx sender)
        {
            if (_MeasureEx == null || _MeasureEx.ExecuteResult == null) return;
            
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.Invoke(() => _MeasureEx_ImageExecuted(sender));
                return;
            }

            if (_MeasureEx == null || _MeasureEx.ExecuteResult == null || !_MeasureEx.ExecuteResult.IsEnable) return;

            try
            {
                // 更新执行统计信息
                lbl_ExecuteCount.Content = _MeasureEx.ExecuteCount.ToString();
                lbl_ExecuteTimeCost.Content = string.Format("{0:F2} ms", _MeasureEx.ExecuteTimeCost.TotalMilliseconds);

                // **新增：同时更新Page1的3D检测结果**
                var page1Instance = WpfApp2.UI.Page1.PageManager.Page1Instance;
                if (page1Instance != null)
                {
                    LogMessage("已找到Page1实例，正在同步3D检测结果...");
                }

                // 更新检测结果文本
                tbx_ExecuteResultText.Text = _MeasureEx.GetDisplayText(out string[] resultText, out string[] judgeText) ?
                    string.Format("{0}\r\n{1}",
                    judgeText != null && judgeText.Length > 0 ? string.Join("\r\n", judgeText) : "",
                    resultText != null && resultText.Length > 0 ? string.Join("\r\n", resultText) : "") : "";

                // 更新总体判定
                lbl_JudgeAll.Content = _MeasureEx.IsJudgeAllOK ? "OK" : "NG";
                lbl_JudgeAll.Background = new SolidColorBrush(_MeasureEx.IsJudgeAllOK ? Colors.LimeGreen : Colors.Red);

                // 更新图像显示
                LjdExecuteResult result = _MeasureEx.ExecuteResult;
                if (result.DstHeightImages.Length > 1)
                {
                    _View2D.SetImage(result.DstHeightImages, result.DstGrayImages);
                    _View3D.SetImageEx(result.DstHeightImages, result.DstGrayImages);
                }
                else
                {
                    _View2D.SetImage(result.DstHeightImage, result.DstGrayImage);
                    _View3D.SetImageEx(result.DstHeightImage, result.DstGrayImage);
                }

                _View2D.ColorRangeFitCommand();
                _View2D.SetToolInfo(result.Results);
                _View3D.LJView3D.ColorRangeFitCommand.Execute();
                _View3D.LJView3D.SetToolInfo(result.Results);

                LogMessage($"检测完成，结果: {(_MeasureEx.IsJudgeAllOK ? "OK" : "NG")}");
            }
            catch (Exception ex)
            {
                LogMessage($"处理检测结果时出错: {ex.Message}");
            }
        }

        private void btn_ExecuteLocalImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_MeasureEx == null || !_MeasureEx.IsEnable)
                {
                    MessageBox.Show("请先启动系统", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string selectedImageFile = tbx_SelectedImageFile.Text?.Trim();
                if (string.IsNullOrEmpty(selectedImageFile) || !File.Exists(selectedImageFile))
                {
                    MessageBox.Show("请先选择要测试的图像文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 自动查找配对的高度图和灰度图
                var (heightImagePath, grayImagePath) = FindPairedImages(selectedImageFile);
                
                if (string.IsNullOrEmpty(heightImagePath) || string.IsNullOrEmpty(grayImagePath))
                {
                    MessageBox.Show("无法找到配对的高度图和灰度图，请确保文件名格式正确（height_xxx.png 和 gray_xxx.png）", 
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                LogMessage($"找到配对图像: {Path.GetFileName(heightImagePath)} 和 {Path.GetFileName(grayImagePath)}");

                // 加载图像
                    LHeightImage heightImg = new LHeightImage();
                    LGrayImage grayImg = new LGrayImage();
                    
                heightImg.Read(heightImagePath);
                grayImg.Read(grayImagePath);
                    
                if (!heightImg.IsEnable() || !grayImg.IsEnable())
                    {
                    MessageBox.Show("图像加载失败，请检查文件格式是否正确", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage("图像加载失败");
                    return;
                }

                        // 使用智能保存方式执行3D检测
                        ExecuteWithSmartSaving(_MeasureEx, new LHeightImage[] { heightImg }, new LGrayImage[] { grayImg });
                        _View2D.SetImage(heightImg, grayImg);
                LogMessage($"执行本地图像检测（智能保存）: {Path.GetFileName(heightImagePath)} 和 {Path.GetFileName(grayImagePath)}");
                    }
            catch (Exception ex)
            {
                MessageBox.Show($"执行本地图像检测失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"执行本地图像检测失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据选中的图像文件查找配对的高度图和灰度图
        /// </summary>
        /// <param name="selectedImageFile">选中的图像文件路径</param>
        /// <returns>配对的高度图和灰度图路径</returns>
        private (string heightImagePath, string grayImagePath) FindPairedImages(string selectedImageFile)
        {
            try
            {
                string directory = Path.GetDirectoryName(selectedImageFile);
                string fileName = Path.GetFileNameWithoutExtension(selectedImageFile);
                string extension = Path.GetExtension(selectedImageFile);
                
                string heightImagePath = "";
                string grayImagePath = "";
                
                // 根据选中文件的类型确定配对文件
                if (fileName.StartsWith("height_"))
                {
                    // 选中的是高度图，查找对应的灰度图
                    heightImagePath = selectedImageFile;
                    string grayFileName = fileName.Replace("height_", "gray_") + extension;
                    grayImagePath = Path.Combine(directory, grayFileName);
                }
                else if (fileName.StartsWith("gray_"))
                {
                    // 选中的是灰度图，查找对应的高度图
                    grayImagePath = selectedImageFile;
                    string heightFileName = fileName.Replace("gray_", "height_") + extension;
                    heightImagePath = Path.Combine(directory, heightFileName);
                }
                else
                {
                    // 如果文件名不符合标准格式，尝试智能匹配
                    LogMessage($"文件名不符合标准格式，尝试智能匹配: {fileName}");
                    
                    // 尝试在同目录下找到匹配的height_和gray_文件
                    var allFiles = Directory.GetFiles(directory, "*" + extension);
                    var heightFiles = allFiles.Where(f => Path.GetFileName(f).StartsWith("height_")).ToArray();
                    var grayFiles = allFiles.Where(f => Path.GetFileName(f).StartsWith("gray_")).ToArray();
                    
                    if (heightFiles.Length > 0 && grayFiles.Length > 0)
                    {
                        // 使用第一对找到的文件
                        heightImagePath = heightFiles[0];
                        grayImagePath = grayFiles[0];
                        LogMessage($"智能匹配找到: {Path.GetFileName(heightImagePath)} 和 {Path.GetFileName(grayImagePath)}");
                    }
                }

                // 验证文件是否存在
                if (!string.IsNullOrEmpty(heightImagePath) && !string.IsNullOrEmpty(grayImagePath) &&
                    File.Exists(heightImagePath) && File.Exists(grayImagePath))
                {
                    return (heightImagePath, grayImagePath);
                }
                
                LogMessage($"无法找到配对图像，高度图: {heightImagePath}，灰度图: {grayImagePath}");
                return ("", "");
            }
            catch (Exception ex)
            {
                LogMessage($"查找配对图像时出错: {ex.Message}");
                return ("", "");
            }
        }

        private void btn_StartImageReceiving_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_StaticMeasureEx != null)
                {
                    MessageBox.Show("检测到自动启动的3D检测实例正在运行，请先点击'运行'按钮停止自动模式", 
                                  "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    LogMessage("用户尝试启动图像接收，但自动模式正在运行");
                    return;
                }

                lbl_ReturnCode.Content = _MeasureEx == null ? "----" : _MeasureEx.StartImageReceiving().ToString();
            }
            catch (Exception ex)
            {
                LogMessage($"启动图像接收失败: {ex.Message}");
                MessageBox.Show($"启动图像接收失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btn_StartMeasure_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_StaticMeasureEx != null)
                {
                    MessageBox.Show("检测到自动启动的3D检测实例正在运行，请先点击'运行'按钮停止自动模式", 
                                  "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    LogMessage("用户尝试启动测量，但自动模式正在运行");
                    return;
                }

                if (_MeasureEx != null)
                {
                    var result = _MeasureEx.StartMeasure();
                    lbl_ReturnCode.Content = result.ToString();
                    LogMessage($"开始测量，返回值: {result}");
                }
                else
                {
                    lbl_ReturnCode.Content = "系统未启动";
                    LogMessage("系统未启动，无法开始测量");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"开始测量失败: {ex.Message}");
                MessageBox.Show($"开始测量失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btn_StopImageReceiving_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_MeasureEx != null)
                {
                    var result = _MeasureEx.StopImageReceiving();
                    lbl_ReturnCode.Content = result.ToString();
                    LogMessage($"停止接收图像，返回值: {result}");
                }
                else
                {
                    lbl_ReturnCode.Content = "系统未启动";
                    LogMessage("系统未启动，无法停止接收图像");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"停止接收图像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 返回按钮点击事件
        /// </summary>
        private void btn_Return_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("用户点击返回按钮，正在关闭3D检测窗口");
                this.Close();
            }
            catch (Exception ex)
            {
                LogMessage($"关闭窗口时出错: {ex.Message}");
            }
        }

        private void btn_SetToolParameter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_MeasureEx != null)
                {
                    _MeasureEx.SetToolParameter();
                    LogMessage("打开工具参数设置窗口");
                }
                else
                {
                    MessageBox.Show("请先启动系统", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"打开工具参数设置失败: {ex.Message}");
            }
        }

        private void btn_SetJudgement_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_MeasureEx != null)
                {
                    _MeasureEx.SetJudgement();
                    LogMessage("打开判定设置窗口");
                }
                else
                {
                    MessageBox.Show("请先启动系统", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"打开判定设置失败: {ex.Message}");
            }
        }

        private void btn_SetDataExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_MeasureEx != null)
                {
                    _MeasureEx.SetDataExport();
                    LogMessage("打开数据输出设置窗口");
                }
                else
                {
                    MessageBox.Show("请先启动系统", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"打开数据输出设置失败: {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            // 使用统一日志管理器
            if (message.Contains("失败") || message.Contains("错误") || message.Contains("异常"))
            {
                LogManager.Error(message, "3D检测");
            }
            else if (message.Contains("[3D调试]") || message.Contains("[3D保存]"))
            {
                LogManager.Verbose(message, "3D检测"); // 详细日志，生产模式下不显示
            }
            else
            {
                LogManager.Info(message, "3D检测");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                LogMessage("开始关闭3D检测窗口，正在释放资源...");

                // 重置3D配置模式状态
                IsIn3DConfigurationMode = false;
                LogMessage("已退出3D配置模式，恢复正常统一判定和图像保存逻辑");

                // **修复1: 立即停止所有3D检测活动，避免资源竞争**
                if (_StaticMeasureEx != null)
                {
                    try
                    {
                        _StaticMeasureEx.ImageExecuted -= StaticMeasureEx_ImageExecuted;
                        _StaticMeasureEx.StopImageReceiving();
                        _StaticMeasureEx.Dispose();
                        _StaticMeasureEx = null;
                        LogMessage("静态3D检测实例已释放");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"释放静态3D实例时出错: {ex.Message}");
                    }
                }

                // **修复2: 完整释放手动模式实例**
                if (_MeasureEx != null)
                {
                    try
                    {
                        _MeasureEx.ImageExecuted -= _MeasureEx_ImageExecuted;
                        _MeasureEx.StopImageReceiving();
                        _MeasureEx.Dispose();
                        _MeasureEx = null;
                        LogMessage("手动模式3D检测实例已释放");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"释放手动模式3D实例时出错: {ex.Message}");
                    }
                }
                
                // **修复3: 强制释放3D视图资源，包括GPU资源**
                Force3DViewCleanup();
                
                // 保存当前参数到内存
                SaveCurrentParametersToMemory();
                
                LogMessage("3D检测窗口资源已完全释放");

                // **修复4: 简化重启逻辑，增加更长的延迟确保资源完全释放**
                if (CurrentDetection3DParams.Enable3DDetection)
                {
                    LogMessage("3D检测功能已启用，将在5秒后重新启动自动模式...");
                    
                    // 增加延迟时间到5秒，确保所有GPU和3D资源完全释放
                    Task.Delay(5000).ContinueWith(_ => 
                    {
                        try
                        {
                            // 强制垃圾回收，释放未管理资源
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                            
                            LogMessageStatic("开始重新启动3D检测自动模式...");
                            AutoStart3DSystem();
                        }
                        catch (Exception ex)
                        {
                            LogMessageStatic($"重新启动自动模式时出错: {ex.Message}");
                        }
                    });
                }
                else
                {
                    LogMessage("3D检测功能已禁用，不重启自动模式");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"关闭3D检测系统时出错: {ex.Message}");
                LogMessage($"关闭3D检测系统时出错: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        /// <summary>
        /// **修复方法: 强制清理3D视图相关资源，包括GPU资源**
        /// </summary>
        private void Force3DViewCleanup()
        {
            try
            {
                LogMessage("开始强制清理3D视图资源...");

                // **关键修复: 按正确顺序清理3D视图资源**
                if (_View3D != null)
                {
                    try
                    {
                        // 1. 首先清理3D视图中的图像数据和GPU资源
                        if (_View3D.LJView3D != null)
                        {
                            _View3D.LJView3D.ClearImage();
                            LogMessage("3D视图图像数据已清理");
                        }
                        
                        // 2. 从宿主控件中移除（断开WPF与WinForms的连接）
                        if (_3DViewHost != null)
                        {
                            _3DViewHost.Child = null;
                            LogMessage("3D视图已从宿主控件移除");
                        }
                        
                        // 3. 强制调用Dispose释放所有资源
                        _View3D.Dispose();
                        _View3D = null;
                        
                        LogMessage("3D视图控件已完全释放");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"清理3D视图时出错: {ex.Message}");
                    }
                }

                // **同样处理2D视图**
                if (_View2D != null)
                {
                    try
                    {
                        if (_2DViewHost != null)
                        {
                            _2DViewHost.Child = null;
                        }
                        _View2D.Dispose();
                        _View2D = null;
                        
                        LogMessage("2D视图控件已完全释放");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"清理2D视图时出错: {ex.Message}");
                    }
                }

                // **修复: 强制垃圾回收，确保GPU和非托管资源释放**
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                LogMessage("3D视图资源强制清理完成，已执行垃圾回收");
            }
            catch (Exception ex)
            {
                LogMessage($"强制清理3D视图资源时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动启动3D检测系统（用于软件启动时调用）
        /// </summary>
        /// <returns>启动是否成功</returns>
        public static bool AutoStart3DSystem()
        {
            try
            {
                // 检查是否启用3D检测
                if (!CurrentDetection3DParams.Enable3DDetection)
                {
                    LogMessageStatic("3D检测功能未启用，跳过自动启动");
                    return false;
                }

                // 自动设置项目名为当前模板名
                string currentTemplateName = PageManager.Page1Instance?.CurrentTemplateName ?? "Default";
                if (!string.IsNullOrEmpty(currentTemplateName))
                {
                    CurrentDetection3DParams.ProjectName = currentTemplateName;
                    LogMessageStatic($"3D项目名已设置为当前模板名: {currentTemplateName}");
                }

                // 验证必要参数
                if (string.IsNullOrWhiteSpace(CurrentDetection3DParams.ProjectName) ||
                    string.IsNullOrWhiteSpace(CurrentDetection3DParams.ProjectFolder))
                {
                    LogMessageStatic("3D检测参数不完整，无法自动启动");
                    return false;
                }

                // **修复: 如果已经有实例在运行，完全释放资源并等待**
                if (_StaticMeasureEx != null)
                {
                    try
                    {
                        LogMessageStatic("发现已存在的3D检测实例，正在完全释放...");
                        _StaticMeasureEx.ImageExecuted -= StaticMeasureEx_ImageExecuted;
                        _StaticMeasureEx.StopImageReceiving();
                        _StaticMeasureEx.Dispose();
                        _StaticMeasureEx = null;
                        
                        // **关键修复: 强制垃圾回收并等待，确保硬件资源完全释放**
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        
                        // 等待硬件资源完全释放
                        System.Threading.Thread.Sleep(1000);
                        
                        LogMessageStatic("之前的3D检测实例已完全释放，等待硬件资源释放完成");
                    }
                    catch (Exception ex)
                    {
                        LogMessageStatic($"释放之前3D检测实例时出错: {ex.Message}");
                    }
                }

                LogMessageStatic($"开始创建新的3D检测实例 - 项目: {CurrentDetection3DParams.ProjectName}");

                // **重要**：自动启动时只在ProjectFolder为空时才设置智能目录，否则使用JSON配置
                if (string.IsNullOrEmpty(CurrentDetection3DParams.ProjectFolder))
                {
                    // 获取当前模板名
                    string currentTemplate = PageManager.Page1Instance?.CurrentTemplateName ?? "Default";
                    
                    // 使用Smart3DImageManager获取智能配置目录
                    string smartConfigDir = Smart3DImageManager.Instance.Get3DProjectConfigDirectory(currentTemplate);
                    
                    // 更新内存参数
                    CurrentDetection3DParams.ProjectFolder = smartConfigDir;
                    
                    LogMessageStatic($"自动启动模式：3D项目配置目录已设置为: Templates/{currentTemplate}/3DConfig");
                }

                // 创建 LjdMeasureEx 实例并启动系统
                _StaticMeasureEx = new LjdMeasureEx(
                    CurrentDetection3DParams.ProjectName,
                    CurrentDetection3DParams.ProjectFolder,
                    LASER_IP_PORT, // 使用硬编码的激光头配置
                    false, // 不使用LJS（根据项目规则）
                    0,     // 不使用TCP端口（根据项目规则）
                    CurrentDetection3DParams.ReCompile,
                    true,
                    ""
                );

                // 为自动启动的系统添加图像处理事件，将结果同步到Page1
                _StaticMeasureEx.ImageExecuted += StaticMeasureEx_ImageExecuted;

                LogMessageStatic("3D检测实例创建成功，开始连接激光头...");

                // 启动图像接收（连接激光头）
                var startResult = _StaticMeasureEx.StartImageReceiving();
                LogMessageStatic($"激光头连接结果，返回值: {startResult}");

                // **修复：检查激光头连接是否成功，失败时弹出提示**
                if (startResult != 0)
                {
                    string errorMessage = $"激光头连接失败！激光头地址: {LASER_IP_PORT}, 错误代码: {startResult}。请检查: 1.激光头是否正常开机 2.网络连接是否正常 3.IP地址设置是否正确 4.是否有其他程序占用激光头 5.程序重启后可能需要等待硬件资源完全释放";

                    // 使用统一的Critical级别日志，自动弹窗
                    LogManager.Critical(errorMessage, "3D检测-激光头连接");

                    //LogMessageStatic("激光头连接失败，但仍尝试启动测量以监听硬件触发");
                }
                
                // 🔧 关键修复：无论连接是否成功，都强制启动测量
                // 这确保3D系统能够监听硬件触发，即使网络连接有问题
                var measureResult = _StaticMeasureEx.StartMeasure();
                LogMessageStatic($"启动测量结果: {measureResult}");

                //// **修复：检查测量启动是否成功**
                //if (measureResult != 0)
                //{
                //    string measureErrorMessage = $"🔥 关键错误：3D检测测量启动失败！错误代码: {measureResult}。这将导致硬件触发无法被监听，3D回调永远不会触发！";
                    
                //    // 测量启动失败是严重问题，直接使用Critical级别
                //    LogManager.Critical(measureErrorMessage, "3D检测-测量启动");
                    
                //    LogMessageStatic("警告：测量启动失败可能导致3D回调无法触发！");
                //}

                // 初始化3D检测项目到Page1的DataGrid
                Initialize3DDetectionItemsToPage1();

                LogMessageStatic("3D检测系统自动启动完成");
                return true;
            }
            catch (Exception ex)
            {
                // 使用Critical级别日志，系统启动异常是严重错误
                LogManager.Critical($"3D检测系统启动异常！错误信息: {ex.Message}。建议重启程序并等待硬件资源完全释放。", "3D检测-系统启动");
                
                // 清理失败的资源
                try
                {
                    if (_StaticMeasureEx != null)
                    {
                        _StaticMeasureEx.ImageExecuted -= StaticMeasureEx_ImageExecuted;
                        _StaticMeasureEx.Dispose();
                        _StaticMeasureEx = null;
                    }
                }
                catch (Exception cleanupEx)
                {
                    LogMessageStatic($"清理异常启动的资源时出错: {cleanupEx.Message}");
                }
                
                return false;
            }
        }

        /// <summary>
        /// 初始化3D检测项目到Page1的DataGrid
        /// </summary>
        private static void Initialize3DDetectionItemsToPage1()
        {
            try
            {
                if (_StaticMeasureEx == null || !_StaticMeasureEx.IsEnable)
                {
                    LogMessageStatic("3D检测系统未启用，无法初始化检测项目");
                    return;
                }

                // 获取Page1实例
                var page1Instance = WpfApp2.UI.Page1.PageManager.Page1Instance;
                if (page1Instance == null)
                {
                    LogMessageStatic("Page1实例不存在，无法初始化3D检测项目");
                    return;
                }

                // **注释：改为动态创建3D项目，不再需要预先初始化**
                // page1Instance.Initialize3DDetectionItemsFromOutputTargets(_StaticMeasureEx);
                LogMessageStatic("3D检测项目将在检测执行时动态创建");
            }
            catch (Exception ex)
            {
                LogMessageStatic($"初始化3D检测项目到Page1时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止自动启动的3D系统
        /// </summary>
        public static void StopAutoStart3DSystem()
        {
            try
            {
                LogMessageStatic("正在停止自动启动的3D系统...");
                
                if (_StaticMeasureEx != null)
                {
                    try
                    {
                        // 注销事件监听器
                        _StaticMeasureEx.ImageExecuted -= StaticMeasureEx_ImageExecuted;
                        
                        // 停止图像接收
                        var stopResult = _StaticMeasureEx.StopImageReceiving();
                        LogMessageStatic($"停止图像接收: {(stopResult == 0 ? "成功" : "失败")}");
                        
                        // 释放资源
                        _StaticMeasureEx.Dispose();
                        _StaticMeasureEx = null;
                        
                        // 更新状态
                        LogManager.Info("3D自动检测系统已停止");
                        LogMessageStatic("3D自动检测系统已成功停止");
                    }
                    catch (Exception ex)
                    {
                        LogMessageStatic($"停止3D系统时出错: {ex.Message}");
                        LogManager.Info($"停止3D系统失败: {ex.Message}");
                    }
                }
                else
                {
                    LogMessageStatic("3D系统未运行，无需停止");
                }
                
                // 重置初始化状态
                _Is3DItemsInitialized = false;
            }
            catch (Exception ex)
            {
                LogMessageStatic($"停止自动启动3D系统失败: {ex.Message}");
                LogManager.Info($"停止3D系统时发生异常: {ex.Message}");
            }
            finally
            {
                // 确保静态实例被清空
                _StaticMeasureEx = null;
            }
        }

        /// <summary>
        /// 获取静态MeasureEx实例，用于统一判定等外部访问
        /// </summary>
        /// <returns>静态MeasureEx实例，如果未初始化则返回null</returns>
        public static LjdMeasureEx GetStaticMeasureExInstance()
        {
            return _StaticMeasureEx;
        }
        
        /// <summary>
        /// 暂时移除静态实例的原有回调，用于外部隔离调用
        /// </summary>
        public static void RemoveStaticCallback()
        {
            if (_StaticMeasureEx != null)
            {
                try
                {
                    _StaticMeasureEx.ImageExecuted -= StaticMeasureEx_ImageExecuted;
                    LogMessageStatic("已暂时移除静态3D回调（用于外部隔离调用）");
                }
                catch (Exception ex)
                {
                    LogMessageStatic($"移除静态3D回调失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 恢复静态实例的原有回调，外部调用完成后恢复正常流程
        /// </summary>
        public static void RestoreStaticCallback()
        {
            if (_StaticMeasureEx != null)
            {
                try
                {
                    _StaticMeasureEx.ImageExecuted += StaticMeasureEx_ImageExecuted;
                    LogMessageStatic("已恢复静态3D回调（外部调用完成）");
                }
                catch (Exception ex)
                {
                    LogMessageStatic($"恢复静态3D回调失败: {ex.Message}");
                }
            }
        }
        

        
        /// <summary>
        /// 获取当前窗口实例的MeasureEx（用于图片检测）
        /// </summary>
        /// <returns>当前MeasureEx实例，如果未初始化则返回null</returns>
        public static LjdMeasureEx GetCurrentMeasureExInstance()
        {
            // 优先返回静态实例（如果已启动）
            if (_StaticMeasureEx != null && _StaticMeasureEx.IsEnable)
            {
                return _StaticMeasureEx;
            }
            
            // 否则尝试获取窗口实例
            var window = Application.Current.Windows.OfType<Ljd3DDetectionWindow>().FirstOrDefault();
            return window?._MeasureEx;
        }

        /// <summary>
        /// 获取3D检测系统的输出目标设置（用于友好名称转换）
        /// </summary>
        private static LOutputTarget[] GetStaticOutputTargets(LjdMeasureEx measureEx)
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
                LogMessageStatic("[3D保存] 无法获取OutputTargets字段，请检查LjdMeasureEx类结构");
                return null;
            }
            catch (Exception ex)
            {
                LogMessageStatic($"[3D保存] 获取OutputTargets时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 静态日志方法（用于在没有窗口实例时记录日志）
        /// </summary>
        /// <param name="message">日志消息</param>
        private static void LogMessageStatic(string message)
        {
            // 🔧 重要修复：只有真正的系统错误才使用Error级别，缺陷类型名称不是错误
            if ((message.Contains("失败") || message.Contains("错误") || message.Contains("异常")) 
                && !IsDefectTypeName(message))
            {
                LogManager.Error(message, "3D检测-自动启动");
            }
            else if (message.Contains("[3D保存]"))
            {
                // 3D保存日志使用Info级别，确保用户能看到存图过程
                LogManager.Info(message, "3D检测-自动启动");
            }
            else if (message.Contains("[3D调试]"))
            {
                LogManager.Verbose(message, "3D检测-自动启动"); // 详细日志，生产模式下不显示
            }
            else
            {
                LogManager.Info(message, "3D检测-自动启动");
            }
        }

        /// <summary>
        /// 判断消息是否包含缺陷类型名称（而不是真正的异常错误）
        /// </summary>
        private static bool IsDefectTypeName(string message)
        {
            // 🔧 扩展缺陷类型识别模式，包括文件路径中的缺陷类型
            return message.Contains("PKG匹配异常") || 
                   message.Contains("缺陷类型: ") ||
                   message.Contains("统一判定缺陷类型: ") ||
                   message.Contains("使用统一判定的缺陷类型: ") ||
                   message.Contains("开始移动3D图像，缺陷类型: ") ||
                   message.Contains("开始移动2D图片，缺陷类型: ") ||
                   // 🎯 关键修复：识别文件路径中的缺陷类型（2D和3D存图路径）
                   (message.Contains("\\PKG匹配异常\\") || message.Contains("/PKG匹配异常/")) ||
                   (message.Contains("原图存储") && message.Contains("异常")) ||
                   (message.Contains("[2D保存]") && message.Contains("异常")) ||
                   (message.Contains("[3D保存]") && message.Contains("异常")) ||
                   // 通用模式：包含缺陷相关关键词的"异常"
                   (message.Contains("异常") && (message.Contains("缺陷") || message.Contains("匹配") || message.Contains("检测") || message.Contains("验证成功") || message.Contains("移动成功")));
        }

        /// <summary>
        /// 静态3D检测系统的图像处理事件，将结果同步到Page1
        /// </summary>
        /// <param name="sender"></param>
        private static void StaticMeasureEx_ImageExecuted(LjdMeasureEx sender)
        {
            if (_StaticMeasureEx == null || _StaticMeasureEx.ExecuteResult == null || !_StaticMeasureEx.ExecuteResult.IsEnable) 
                return;

            // 在基恩士SDK线程中快速获取必要数据
            var result = _StaticMeasureEx.ExecuteResult;
            var processTimeMs = _StaticMeasureEx.ExecuteTimeCost.TotalMilliseconds;
            var isImageTestMode = IsInImageTestMode;
            var isConfigMode = IsIn3DConfigurationMode;

            // 🔧 关键修复：在SDK线程中立即通知系统测试窗口（避免UI线程死锁）
            SystemTestWindow.Notify3DCallbackCompleted();

            // 使用Dispatcher将所有UI操作和复杂逻辑调度到UI线程执行
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                try
                {
                    // 记录3D检测完成时间戳
                    WpfApp2.UI.Page1.Set3DCompletionTime();
                    LogMessageStatic("3D检测完成，时间戳已记录");
                    
                    var page1Instance = WpfApp2.UI.Page1.PageManager.Page1Instance;
                    if (page1Instance == null)
                    {
                        LogMessageStatic("Page1实例不存在，无法同步3D检测结果");
                        return;
                    }

                    LogMessageStatic("Page1实例存在，正在同步3D检测结果...");

                    // 更新3D检测结果到缓存
                    //LogMessageStatic("开始更新3D检测结果到缓存...");
                    page1Instance.Update3DDetectionResult(result, _StaticMeasureEx);
                    //LogMessageStatic("3D检测结果缓存更新完成");
                    
                    // 跳过3D配置模式的复杂逻辑
                    if (isConfigMode)
                    {
                        LogMessageStatic("当前处于3D配置模式，跳过后续处理");
                        return;
                    }
                    
                    // 注释：3D数据记录现在统一在ExecuteUnifiedJudgementAndIO中处理，这里不再单独记录
                    // 避免重复记录和数据分行问题
                    
                    // 🔧 关键修复：只有在真正的检测周期中才通知检测管理器
                    // 防止3D系统启动后的意外回调影响图片测试模式的索引管理
                    bool shouldNotifyDetectionManager = false;
                    
                    if (isImageTestMode)
                    {
                        // 图片测试模式：检查是否处于有效的检测周期
                        var detectionManager = page1Instance.DetectionManager;
                        if (detectionManager != null)
                        {
                            // 使用反射检查检测管理器的状态
                            var shouldProcessMethod = detectionManager.GetType().GetMethod("ShouldProcessDetection");
                            if (shouldProcessMethod != null)
                            {
                                shouldNotifyDetectionManager = (bool)shouldProcessMethod.Invoke(detectionManager, null);
                                LogMessageStatic($"图片测试模式：检测管理器状态检查 - 应该处理: {shouldNotifyDetectionManager}");
                            }
                            else
                            {
                                LogMessageStatic("图片测试模式：无法获取检测管理器状态，跳过3D完成通知");
                            }
                        }
                    }
                    else
                    {
                        // 生产模式：始终通知检测管理器
                        shouldNotifyDetectionManager = true;
                        LogMessageStatic("生产模式：始终通知检测管理器");
                    }
                    
                    if (shouldNotifyDetectionManager)
                    {
                        // 通知统一检测管理器3D检测完成
                        NotifyDetectionManagerForThreeDCompletion(page1Instance);
                    }
                    else
                    {
                        LogMessageStatic("3D检测完成，但当前不在有效检测周期中，跳过检测管理器通知");
                    }
                }
                catch (Exception ex)
                {
                    LogMessageStatic($"同步3D检测结果到Page1时出错: {ex.Message}");
                }
            }));
        }

        /// <summary>
        /// 通知统一检测管理器3D检测完成
        /// </summary>
        private static void NotifyDetectionManagerForThreeDCompletion(WpfApp2.UI.Page1 page1Instance)
        {
            try
            {
                var field = page1Instance.GetType().GetField("_detectionManager", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field == null)
                {
                    LogMessageStatic("⚠️ 无法找到检测管理器字段，这是严重错误！");
                    return;
                }

                var detectionManager = field.GetValue(page1Instance);
                if (detectionManager == null)
                {
                    LogMessageStatic("⚠️ 检测管理器实例为空，这是严重错误！");
                    return;
                }

                // 检查是否应该处理检测结果
                var shouldProcessMethod = detectionManager.GetType().GetMethod("ShouldProcessDetection");
                if (shouldProcessMethod != null)
                {
                    bool shouldProcess = (bool)shouldProcessMethod.Invoke(detectionManager, null);
                    if (!shouldProcess)
                    {
                        // 尝试启动检测周期
                        LogMessageStatic("检测管理器当前状态不允许处理检测结果，尝试启动检测周期...");
                        
                        var startCycleMethod = detectionManager.GetType().GetMethod("StartDetectionCycle");
                        if (startCycleMethod != null)
                        {
                            bool enable3D = page1Instance.Is3DDetectionEnabled();
                            startCycleMethod.Invoke(detectionManager, new object[] { enable3D });
                            LogMessageStatic($"已启动检测周期，3D启用: {enable3D}");
                            
                            // 重新检查是否可以处理
                            shouldProcess = (bool)shouldProcessMethod.Invoke(detectionManager, null);
                            if (!shouldProcess)
                            {
                                LogMessageStatic("启动检测周期后仍无法处理，跳过3D完成通知");
                                return;
                            }
                            LogMessageStatic("启动检测周期后现在可以处理3D结果");
                        }
                        else
                        {
                            LogMessageStatic("无法找到启动检测周期方法，跳过3D完成通知");
                            return;
                        }
                    }
                }
                
                // 通知管理器3D检测完成（管理器会自动执行统一判定和图像保存决策）
                var mark3DMethod = detectionManager.GetType().GetMethod("Mark3DCompleted");
                if (mark3DMethod != null)
                {
                    mark3DMethod.Invoke(detectionManager, null);
                    LogMessageStatic("已通知统一检测管理器3D完成");
                }
                else
                {
                    LogMessageStatic("⚠️ 无法找到Mark3DCompleted方法，这是严重错误！");
                }
            }
            catch (Exception ex)
            {
                LogMessageStatic($"通知检测管理器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 供外部调用的3D检测执行方法（静态方法，用于图片测试模式）
        /// </summary>
        /// <param name="heightImagePath">高度图路径</param>
        /// <param name="grayImagePath">灰度图路径</param>
        /// <returns>true if 3D检测执行成功</returns>
        public static async Task<bool> ExecuteStaticLocalImageDetection(string heightImagePath, string grayImagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(heightImagePath) || string.IsNullOrEmpty(grayImagePath))
                {
                    LogMessageStatic("3D图片路径为空，无法执行3D检测");
                    return false;
                }

                if (!File.Exists(heightImagePath) || !File.Exists(grayImagePath))
                {
                    LogMessageStatic($"3D图片文件不存在: {Path.GetFileName(heightImagePath)}, {Path.GetFileName(grayImagePath)}");
                    return false;
                }

                if (_StaticMeasureEx == null || !_StaticMeasureEx.IsEnable)
                {
                    LogMessageStatic("静态3D检测系统未启动，无法执行3D检测");
                    return false;
                }

                LogMessageStatic($"开始执行静态3D检测（图片测试模式）: {Path.GetFileName(heightImagePath)}, {Path.GetFileName(grayImagePath)}");

                // 加载3D图像
                LHeightImage heightImg = new LHeightImage();
                LGrayImage grayImg = new LGrayImage();
                
                heightImg.Read(heightImagePath);
                grayImg.Read(grayImagePath);
                
                if (!heightImg.IsEnable() || !grayImg.IsEnable())
                {
                    LogMessageStatic("3D图像加载失败");
                    return false;
                }

                // 只执行检测，不保存图像（图片测试模式）
                bool result = await Smart3DImageManager.Instance.ExecuteDetectionOnly(
                    _StaticMeasureEx, 
                    new LHeightImage[] { heightImg }, 
                    new LGrayImage[] { grayImg });
                
                LogMessageStatic($"静态3D检测执行完成: {result}");
                return result;
            }
            catch (Exception ex)
            {
                LogMessageStatic($"静态3D检测执行失败: {ex.Message}");
                return false;
            }
        }
    }
} 
