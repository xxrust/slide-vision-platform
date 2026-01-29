using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.UI.Models;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WpfApp2.UI
{
    /// <summary>
    /// 图片测试模式选择窗口
    /// </summary>
    public partial class ImageTestModeSelectionWindow : Window
    {
        public enum TestMode
        {
            CategoryMode,           // 按类别查找（原有功能）
            NGNumberMode,           // 按NG编号查找
            SevereNGDetection,      // 严重NG检测
            ChallengeDetection,     // 挑战件检测
            GoodDetection,          // 良品检测
            ValidatorMachineDetection, // 验机图片检测
            SingleSampleDynamicStaticDetection, // 单片动态/静态测试
            CicdImageSetTest,       // CICD图片集测试
            SevereNGCollection,     // 严重NG图集制作
            ChallengeCollection,    // 挑战件图集制作
            GoodCollection,         // 良品图集制作
            ValidatorMachineCollection, // 验机图片集制作
            SingleSampleDynamicStaticCollection, // 单片动态/静态测试集制作
            CicdImageSetCollection  // CICD图片集制作
        }

        public TestMode SelectedMode { get; private set; } = TestMode.CategoryMode;
        public int NGCount { get; private set; } = 0;
        public List<ImageGroupSet> NGImageGroups { get; private set; } = new List<ImageGroupSet>();

        // 验机参数
        public string ValidatorMachineFolderPath { get; set; } = string.Empty;
        public int ValidatorMachineLoopCycle { get; set; } = 0;
        public int ValidatorMachineSampleCount { get; set; } = 0;

        // 单片动态/静态参数
        public string SingleSampleDynamicStaticFolderPath { get; set; } = string.Empty;

        // CICD参数
        public string CicdImageSetName { get; private set; } = string.Empty;
        public List<string> CicdCollectionSourceFiles { get; private set; } = new List<string>();

        private string _currentLotValue;
        private int _currentNGCountFromUI;

        public ImageTestModeSelectionWindow(string lotValue, int currentNGCountFromUI = 0)
        {
            InitializeComponent();
            _currentLotValue = lotValue;
            _currentNGCountFromUI = currentNGCountFromUI;
            
            LogManager.Info($"ImageTestModeSelectionWindow构造函数 - LOT: {_currentLotValue}, NG数量: {_currentNGCountFromUI}");
            
            // 在窗口完全加载后设置值
            this.Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 设置NG数量文本框
                LogManager.Info($"Window_Loaded: 设置NGCountTextBox为 {_currentNGCountFromUI}");
                
                // 确保文本框可用且可编辑
                NGCountTextBox.IsEnabled = true;
                NGCountTextBox.IsReadOnly = false;
                
                // 多种方式设置文本值
                NGCountTextBox.Text = _currentNGCountFromUI.ToString();
                NGCountTextBox.SetValue(TextBox.TextProperty, _currentNGCountFromUI.ToString());
                
                // 强制刷新UI
                NGCountTextBox.UpdateLayout();
                NGCountTextBox.InvalidateVisual();
                
                // 验证设置是否成功
                LogManager.Info($"Window_Loaded: NGCountTextBox.Text = '{NGCountTextBox.Text}'");
                LogManager.Info($"Window_Loaded: NGCountTextBox.IsEnabled = {NGCountTextBox.IsEnabled}");
                LogManager.Info($"Window_Loaded: NGCountTextBox.IsReadOnly = {NGCountTextBox.IsReadOnly}");
                
                // 初始化NG图片数量信息
                InitializeNGCountInfo();
                
                // 尝试聚焦到文本框验证是否可编辑
                NGCountTextBox.Focus();
            }
            catch (Exception ex)
            {
                LogManager.Error($"Window_Loaded失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化NG数量信息
        /// </summary>
        private void InitializeNGCountInfo()
        {
            try
            {
                var availableNGImages = FindNGImagesInCurrentLot();
                int availableCount = availableNGImages.Count;
                
                // 更新界面上的信息提示
                if (NGInfoTextBlock != null)
                {
                    if (availableCount > 0)
                    {
                        NGInfoTextBlock.Text = $"当前LOT共找到 {availableCount} 张NG图片可用于测试";
                        LogManager.Info($"当前LOT共找到 {availableCount} 组NG图片");
                    }
                    else
                    {
                        NGInfoTextBlock.Text = $"当前LOT ({_currentLotValue}) 未找到NG图片";
                        LogManager.Warning($"当前LOT ({_currentLotValue}) 未找到NG图片");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"初始化NG数量信息失败: {ex.Message}");
                if (NGInfoTextBlock != null)
                {
                    NGInfoTextBlock.Text = $"检查NG图片时出错: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CategoryModeRadio.IsChecked == true)
                {
                    // 按类别查找模式（原有功能）
                    SelectedMode = TestMode.CategoryMode;
                    LogManager.Info("用户选择按类别查找模式");
                }
                else if (NGNumberModeRadio.IsChecked == true)
                {
                    // 按NG编号查找模式
                    SelectedMode = TestMode.NGNumberMode;
                    
                    // 验证输入的数量
                    if (!int.TryParse(NGCountTextBox.Text, out int ngCount) || ngCount <= 0)
                    {
                        MessageBox.Show("请输入有效的NG图片数量（大于0的整数）", "输入错误", 
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    NGCount = ngCount;
                    
                    // 显示LoadingDialog并异步查找NG图片
                    LoadingDialog loadingDialog = null;
                    try
                    {
                        // 显示加载对话框
                        loadingDialog = new LoadingDialog($"正在查找前 {NGCount} 个NG图片，请稍候...");
                        loadingDialog.Owner = this;
                        loadingDialog.Show();
                        
                        // 让LoadingDialog完全渲染
                        await System.Threading.Tasks.Task.Delay(100);
                        Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                        
                        LogManager.Info($"开始异步查找NG图片，数量: {NGCount}");
                        
                        // 在后台线程异步执行耗时的文件搜索操作
                        NGImageGroups = await System.Threading.Tasks.Task.Run(() => FindAndSortNGImages(NGCount));
                        
                        if (NGImageGroups.Count == 0)
                        {
                            MessageBox.Show($"未找到当前LOT ({_currentLotValue}) 的NG图片，请检查存图目录", 
                                          "未找到图片", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        
                        LogManager.Info($"用户选择按NG编号查找模式，数量: {NGCount}，实际找到: {NGImageGroups.Count} 组");
                    }
                    finally
                    {
                        // 确保关闭LoadingDialog
                        if (loadingDialog != null)
                        {
                            await System.Threading.Tasks.Task.Delay(200); // 确保后台任务完成
                            loadingDialog.Close();
                        }
                    }
                }
                else if (SevereNGDetectionRadio.IsChecked == true)
                {
                    // 严重NG检测模式
                    SelectedMode = TestMode.SevereNGDetection;
                    NGImageGroups = await LoadTemplateImages("严重NG", "严重NG检测");
                    if (NGImageGroups.Count == 0) return;
                    LogManager.Info($"用户选择严重NG检测模式，找到 {NGImageGroups.Count} 组图片");
                }
                else if (ChallengeDetectionRadio.IsChecked == true)
                {
                    // 挑战件检测模式
                    SelectedMode = TestMode.ChallengeDetection;
                    NGImageGroups = await LoadTemplateImages("挑战件", "挑战件检测");
                    if (NGImageGroups.Count == 0) return;
                    LogManager.Info($"用户选择挑战件检测模式，找到 {NGImageGroups.Count} 组图片");
                }
                else if (GoodDetectionRadio.IsChecked == true)
                {
                    // 良品检测模式
                    SelectedMode = TestMode.GoodDetection;
                    NGImageGroups = await LoadTemplateImages("良品", "良品检测");
                    if (NGImageGroups.Count == 0) return;
                    LogManager.Info($"用户选择良品检测模式，找到 {NGImageGroups.Count} 组图片");
                }
                else if (ValidatorMachineDetectionRadio.IsChecked == true)
                {
                    // 验机图片检测模式 - 从验机图片集目录中选择
                    SelectedMode = TestMode.ValidatorMachineDetection;
                    LogManager.Info("用户选择验机图片检测模式");

                    // 显示验机图片集选择对话框（检测模式）
                    // 获取当前模板名称（需要从Page1传递）
                    string currentTemplateName = GetCurrentTemplateName();
                    if (string.IsNullOrEmpty(currentTemplateName))
                    {
                        LogManager.Warning("无法获取当前模板名称");
                        MessageBox.Show("无法获取当前模板名称，请先配置模板", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ValidatorMachineParametersWindow paramWindow = new ValidatorMachineParametersWindow(currentTemplateName);
                    paramWindow.Owner = this;

                    if (paramWindow.ShowDialog() == true)
                    {
                        // 用户确认了参数，保存参数
                        ValidatorMachineFolderPath = paramWindow.SelectedFolderPath;
                        ValidatorMachineLoopCycle = paramWindow.LoopCycle;
                        ValidatorMachineSampleCount = paramWindow.SampleCount;
                        LogManager.Info($"验机参数已确认 - 文件夹: {ValidatorMachineFolderPath}, 样品数: {ValidatorMachineSampleCount}, 巡回周期: {ValidatorMachineLoopCycle}");
                        DialogResult = true;
                        Close();
                        return;
                    }
                    else
                    {
                        // 用户取消了选择，不关闭当前窗口
                        LogManager.Info("用户取消了验机图片集选择");
                        return;
                    }
                }
                else if (SingleSampleDynamicStaticDetectionRadio.IsChecked == true)
                {
                    SelectedMode = TestMode.SingleSampleDynamicStaticDetection;
                    LogManager.Info("用户选择单片动态/静态测试模式");

                    string currentTemplateName = GetCurrentTemplateName();
                    if (string.IsNullOrEmpty(currentTemplateName))
                    {
                        LogManager.Warning("无法获取当前模板名称");
                        MessageBox.Show("无法获取当前模板名称，请先配置模板", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    SingleSampleDynamicStaticParametersWindow paramWindow = new SingleSampleDynamicStaticParametersWindow(currentTemplateName);
                    paramWindow.Owner = this;

                    if (paramWindow.ShowDialog() == true)
                    {
                        SingleSampleDynamicStaticFolderPath = paramWindow.SelectedFolderPath;
                        LogManager.Info($"单片动态/静态图片集已选择: {SingleSampleDynamicStaticFolderPath}");
                        DialogResult = true;
                        Close();
                        return;
                    }

                    LogManager.Info("用户取消了单片动态/静态图片集选择");
                    return;
                }
                else if (CicdImageSetTestRadio.IsChecked == true)
                {
                    SelectedMode = TestMode.CicdImageSetTest;
                    LogManager.Info("用户选择CICD图片集测试模式");

                    string currentTemplateName = GetCurrentTemplateName();
                    if (string.IsNullOrEmpty(currentTemplateName))
                    {
                        LogManager.Warning("无法获取当前模板名称");
                        MessageBox.Show("无法获取当前模板名称，请先配置模板", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var paramWindow = new CicdImageSetParametersWindow(currentTemplateName);
                    paramWindow.Owner = this;

                    if (paramWindow.ShowDialog() == true)
                    {
                        CicdImageSetName = paramWindow.SelectedImageSetName;
                        LogManager.Info($"CICD图片集已选择: {CicdImageSetName}");
                        DialogResult = true;
                        Close();
                        return;
                    }

                    LogManager.Info("用户取消了CICD图片集选择");
                    return;
                }
                else if (SevereNGCollectionRadio.IsChecked == true)
                {
                    // 严重NG图集制作模式
                    SelectedMode = TestMode.SevereNGCollection;
                    LogManager.Info("用户选择严重NG图集制作模式");
                }
                else if (ChallengeCollectionRadio.IsChecked == true)
                {
                    // 挑战件图集制作模式
                    SelectedMode = TestMode.ChallengeCollection;
                    LogManager.Info("用户选择挑战件图集制作模式");
                }
                else if (GoodCollectionRadio.IsChecked == true)
                {
                    // 良品图集制作模式
                    SelectedMode = TestMode.GoodCollection;
                    LogManager.Info("用户选择良品图集制作模式");
                }
                else if (ValidatorMachineCollectionRadio.IsChecked == true)
                {
                    // 验机图片集制作模式 - 弹出参数输入对话框
                    SelectedMode = TestMode.ValidatorMachineCollection;
                    LogManager.Info("用户选择验机图片集制作模式");

                    // 显示参数输入对话框
                    ValidatorMachineParametersWindow paramWindow = new ValidatorMachineParametersWindow();
                    paramWindow.Owner = this;

                    if (paramWindow.ShowDialog() == true)
                    {
                        // 用户确认了参数，保存参数
                        ValidatorMachineFolderPath = paramWindow.SelectedFolderPath;
                        ValidatorMachineSampleCount = paramWindow.SampleCount;
                        // LoopCycle 将在 Page1 中根据总图片数自动计算
                        LogManager.Info($"验机参数已确认 - 文件夹: {ValidatorMachineFolderPath}, 样品数目: {ValidatorMachineSampleCount}");
                        DialogResult = true;
                        Close();
                        return;
                    }
                    else
                    {
                        // 用户取消了参数输入，不关闭当前窗口
                        LogManager.Info("用户取消了验机参数输入");
                        return;
                    }
                }
                else if (SingleSampleDynamicStaticCollectionRadio.IsChecked == true)
                {
                    SelectedMode = TestMode.SingleSampleDynamicStaticCollection;
                    LogManager.Info("用户选择单片动态/静态测试集制作模式");

                    using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        folderDialog.Description = "选择单片动态/静态测试集源文件夹";
                        folderDialog.ShowNewFolderButton = false;

                        if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            SingleSampleDynamicStaticFolderPath = folderDialog.SelectedPath;
                            LogManager.Info($"单片动态/静态测试集源文件夹已选择: {SingleSampleDynamicStaticFolderPath}");
                            DialogResult = true;
                            Close();
                            return;
                        }

                        LogManager.Info("用户取消了单片动态/静态测试集源文件夹选择");
                        return;
                    }
                }
                else if (CicdImageSetCollectionRadio.IsChecked == true)
                {
                    SelectedMode = TestMode.CicdImageSetCollection;
                    LogManager.Info("用户选择CICD图片集制作模式");

                    var paramWindow = new CicdImageSetCollectionWindow();
                    paramWindow.Owner = this;

                    if (paramWindow.ShowDialog() == true)
                    {
                        CicdCollectionSourceFiles = paramWindow.SelectedFiles?.ToList() ?? new List<string>();
                        if (CicdCollectionSourceFiles.Count == 0)
                        {
                            MessageBox.Show("请至少添加一个图片文件", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        DialogResult = true;
                        Close();
                        return;
                    }

                    LogManager.Info("用户取消了CICD图片集制作文件夹选择");
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LogManager.Error($"确认测试模式选择失败: {ex.Message}");
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 刷新数量按钮点击事件
        /// </summary>
        private void RefreshCountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogManager.Info("手动刷新NGCountTextBox");
                
                // 清空后重新设置
                NGCountTextBox.Clear();
                NGCountTextBox.Text = "";
                NGCountTextBox.UpdateLayout();
                
                // 重新设置值
                string newValue = _currentNGCountFromUI.ToString();
                NGCountTextBox.Text = newValue;
                NGCountTextBox.SetValue(TextBox.TextProperty, newValue);
                
                LogManager.Info($"刷新后 NGCountTextBox.Text = '{NGCountTextBox.Text}'");
                
                // 强制聚焦和选中
                NGCountTextBox.Focus();
                NGCountTextBox.SelectAll();
            }
            catch (Exception ex)
            {
                LogManager.Error($"刷新数量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找并排序NG图片（按编号从大到小）
        /// </summary>
        private List<ImageGroupSet> FindAndSortNGImages(int maxCount)
        {
            var ngImageGroups = new List<ImageGroupSet>();
            var invalidGroups = new List<(string groupName, List<string> missingFiles)>();
            
            try
            {
                var availableNGImages = FindNGImagesInCurrentLot();
                
                if (availableNGImages.Count == 0)
                {
                    LogManager.Warning($"当前LOT ({_currentLotValue}) 未找到NG图片");
                    return ngImageGroups;
                }
                
                // 按编号从大到小排序（近期NG优先）
                var sortedNGImages = availableNGImages
                    .OrderByDescending(img => img.ImageNumber)
                    .Take(maxCount)
                    .ToList();
                
                bool is3DEnabled = Page1.PageManager.Page1Instance?.Is3DDetectionEnabled() == true;
                LogManager.Info($"当前3D检测状态: {(is3DEnabled ? "使能" : "未使能")}");
                
                foreach (var ngImageInfo in sortedNGImages)
                {
                    var imageGroup = CreateImageGroupFromNGImage(ngImageInfo);
                    if (imageGroup != null)
                    {
                        if (imageGroup.IsValid)
                        {
                            ngImageGroups.Add(imageGroup);
                        }
                        else
                        {
                            // 记录无效组的信息
                            var displaySuffix = GetNgDisplaySuffix(ngImageInfo);
                            var missingFiles = GetMissingFiles(imageGroup, is3DEnabled, displaySuffix);
                            invalidGroups.Add((imageGroup.BaseName, missingFiles));
                        }
                    }
                }
                
                LogManager.Info($"成功创建 {ngImageGroups.Count} 个有效的NG图片组");
                
                // 如果有无效的图片组，弹窗告知用户
                if (invalidGroups.Count > 0)
                {
                    ShowMissingFilesWarning(invalidGroups, is3DEnabled);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"查找和排序NG图片失败: {ex.Message}");
            }
            
            return ngImageGroups;
        }

        /// <summary>
        /// 查找当前LOT的所有NG图片信息
        /// </summary>
        private List<NGImageInfo> FindNGImagesInCurrentLot()
        {
            var ngImages = new List<NGImageInfo>();
            
            try
            {
                // 获取当前LOT的存图根目录
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string lotRootDir = Path.Combine(baseDir, "原图存储", _currentLotValue);
                
                LogManager.Info($"查找NG图片，LOT根目录: {lotRootDir}");
                
                if (!Directory.Exists(lotRootDir))
                {
                    LogManager.Warning($"LOT根目录不存在: {lotRootDir}");
                    return ngImages;
                }
                
                // 直接查找所有NG类型文件夹（不是固定的"NG"文件夹，而是各种缺陷类型文件夹）
                var ngTypeFolders = Directory.GetDirectories(lotRootDir)
                    .Where(dir => 
                    {
                        string folderName = Path.GetFileName(dir);
                        // 排除良品文件夹，只查找NG类型文件夹
                        return !folderName.Equals("良品", StringComparison.OrdinalIgnoreCase) &&
                               !folderName.Equals("OK", StringComparison.OrdinalIgnoreCase);
                    });
                
                LogManager.Info($"在LOT根目录中找到NG类型文件夹: {string.Join(", ", ngTypeFolders.Select(Path.GetFileName))}");
                
                foreach (var ngTypeFolder in ngTypeFolders)
                {
                    string ngTypeName = Path.GetFileName(ngTypeFolder);
                    LogManager.Info($"检查NG类型文件夹: {ngTypeName}");
                    
                    // 查找图像源2_1文件夹中的NG图片
                    string source2_1Folder = Path.Combine(ngTypeFolder, "图像源2_1");
                    LogManager.Info($"检查图像源2_1文件夹: {source2_1Folder}");
                    
                    if (!Directory.Exists(source2_1Folder))
                    {
                        LogManager.Info($"图像源2_1文件夹不存在: {source2_1Folder}");
                        continue;
                    }
                    
                    // 查找所有NG图片文件
                    var ngFiles = Directory.GetFiles(source2_1Folder, "*.bmp")
                        .Concat(Directory.GetFiles(source2_1Folder, "*.png"))
                        .ToList();
                    
                    LogManager.Info($"在 {source2_1Folder} 中找到图片文件: {ngFiles.Count} 个");
                    
                    foreach (var ngFile in ngFiles)
                    {
                        var imageNumber = ExtractImageNumberFromFilename(Path.GetFileName(ngFile));
                        if (imageNumber.HasValue)
                        {
                            ngImages.Add(new NGImageInfo
                            {
                                ImageNumber = imageNumber.Value,
                                DateFolder = string.Empty, // 不再使用日期文件夹
                                NgTypeName = ngTypeName,
                                Source2_1Path = ngFile,
                                NGFolderPath = ngTypeFolder
                            });
                            
                            LogManager.Info($"添加NG图片: 编号={imageNumber.Value}, 类型={ngTypeName}, 路径={ngFile}");
                        }
                        else
                        {
                            LogManager.Warning($"无法从文件名提取图片编号: {Path.GetFileName(ngFile)}");
                        }
                    }
                }
                
                LogManager.Info($"在LOT {_currentLotValue} 中总共找到 {ngImages.Count} 张NG图片");
            }
            catch (Exception ex)
            {
                LogManager.Error($"查找LOT NG图片失败: {ex.Message}");
            }
            
            return ngImages;
        }

        /// <summary>
        /// 从NG图片信息创建ImageGroupSet
        /// 复用现有的成功模式：使用Directory.GetFiles通配符搜索
        /// </summary>
        private ImageGroupSet CreateImageGroupFromNGImage(NGImageInfo ngImageInfo)
        {
            try
            {
                string ngTypeFolderPath = ngImageInfo.NGFolderPath; 
                string imageNumberStr = ngImageInfo.ImageNumber.ToString();

                // 关键修复：按NG编号查找时，应使用“图片名后缀”匹配，而不是强制PadLeft(4)。
                // 例如 a_11 / b_0443，这里的后缀应分别为 _11 / _0443。
                var suffixCandidates = BuildNgSuffixCandidates(imageNumberStr, ngImageInfo.Source2_1Path);
                string displaySuffix = suffixCandidates.FirstOrDefault() ?? ("_" + imageNumberStr);
                
                LogManager.Info($"创建ImageGroup - 编号: {imageNumberStr}, NG类型: {ngImageInfo.NgTypeName}, 文件夹: {ngTypeFolderPath}, 后缀候选: {string.Join(", ", suffixCandidates)}");
                
                // 使用现有的成功模式：在三个图像源目录中分别查找指定后缀的图片文件（支持bmp/png）
                var source1Dir = Path.Combine(ngTypeFolderPath, "图像源1");
                var source2_1Dir = Path.Combine(ngTypeFolderPath, "图像源2_1");
                var source2_2Dir = Path.Combine(ngTypeFolderPath, "图像源2_2");

                string source1Path = null;
                string source2_1Path = null;
                string source2_2Path = null;

                // 查找图像源1文件
                if (Directory.Exists(source1Dir))
                {
                    source1Path = FindFirstImageBySuffixCandidates(source1Dir, suffixCandidates);
                    if (!string.IsNullOrEmpty(source1Path))
                    {
                        LogManager.Info($"找到图像源1文件: {Path.GetFileName(source1Path)}");
                    }
                }

                // 查找图像源2_1文件
                if (Directory.Exists(source2_1Dir))
                {
                    source2_1Path = FindFirstImageBySuffixCandidates(source2_1Dir, suffixCandidates);
                    if (!string.IsNullOrEmpty(source2_1Path))
                    {
                        LogManager.Info($"找到图像源2_1文件: {Path.GetFileName(source2_1Path)}");
                    }
                }

                // 查找图像源2_2文件
                if (Directory.Exists(source2_2Dir))
                {
                    source2_2Path = FindFirstImageBySuffixCandidates(source2_2Dir, suffixCandidates);
                    if (!string.IsNullOrEmpty(source2_2Path))
                    {
                        LogManager.Info($"找到图像源2_2文件: {Path.GetFileName(source2_2Path)}");
                    }
                }

                var imageGroup = new ImageGroupSet
                {
                    Source1Path = source1Path,
                    Source2_1Path = source2_1Path,
                    Source2_2Path = source2_2Path,
                    BaseName = $"NG_{ngImageInfo.NgTypeName}_{ngImageInfo.DateFolder}_{imageNumberStr}"
                };

                // 查找对应的3D图片（如果3D使能）- 🔧 修复：使用统一的3D图片查找方法
                bool is3DEnabled = Page1.PageManager.Page1Instance?.Is3DDetectionEnabled() == true;
                if (is3DEnabled)
                {
                    // 🔧 修复：使用统一的查找逻辑，避免文件夹结构假设不一致
                    foreach (var candidateSuffix in suffixCandidates)
                    {
                        if (imageGroup.Has3DImages)
                            break;

                        Page1.FindAndSet3DImagesForGroup(ngTypeFolderPath, candidateSuffix, imageGroup, enableLogging: false);
                    }
                    
                    if (imageGroup.Has3DImages)
                    {
                        LogManager.Info($"找到完整3D图片: {Path.GetFileName(imageGroup.HeightImagePath)}, {Path.GetFileName(imageGroup.GrayImagePath)}");
                    }
                    else if (!string.IsNullOrEmpty(imageGroup.HeightImagePath) || !string.IsNullOrEmpty(imageGroup.GrayImagePath))
                    {
                        LogManager.Info($"3D图片不完整: Height={!string.IsNullOrEmpty(imageGroup.HeightImagePath)}, Gray={!string.IsNullOrEmpty(imageGroup.GrayImagePath)}");
                    }
                }
                else
                {
                    LogManager.Info($"3D未使能，跳过3D图片查找");
                }
                
                // 检查并记录缺失的文件
                var missingFiles = GetMissingFiles(imageGroup, is3DEnabled, displaySuffix);
                if (missingFiles.Count > 0)
                {
                    LogManager.Warning($"图片组 {imageGroup.BaseName} 缺失文件: {string.Join(", ", missingFiles)}");
                }
                
                LogManager.Info($"创建的ImageGroup BaseName: {imageGroup.BaseName}, IsValid: {imageGroup.IsValid}");
                
                return imageGroup;
            }
            catch (Exception ex)
            {
                LogManager.Error($"创建NG图片组失败 (编号: {ngImageInfo.ImageNumber}, 类型: {ngImageInfo.NgTypeName}): {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 获取缺失的文件列表
        /// </summary>
        private List<string> GetMissingFiles(ImageGroupSet imageGroup, bool is3DEnabled, string suffixForDisplay)
        {
            var missingFiles = new List<string>();
            
            if (string.IsNullOrEmpty(imageGroup.Source1Path))
                missingFiles.Add($"图像源1/*{suffixForDisplay}");
            
            if (string.IsNullOrEmpty(imageGroup.Source2_1Path))
                missingFiles.Add($"图像源2_1/*{suffixForDisplay}");
            
            if (string.IsNullOrEmpty(imageGroup.Source2_2Path))
                missingFiles.Add($"图像源2_2/*{suffixForDisplay}");
            
            if (is3DEnabled)
            {
                if (string.IsNullOrEmpty(imageGroup.HeightImagePath))
                    missingFiles.Add($"3D/height*{suffixForDisplay}");
                
                if (string.IsNullOrEmpty(imageGroup.GrayImagePath))
                    missingFiles.Add($"3D/gray*{suffixForDisplay}");
            }
            
            return missingFiles;
        }

        private string GetNgDisplaySuffix(NGImageInfo ngImageInfo)
        {
            var suffixCandidates = BuildNgSuffixCandidates(ngImageInfo.ImageNumber.ToString(), ngImageInfo.Source2_1Path);
            return suffixCandidates.FirstOrDefault() ?? ("_" + ngImageInfo.ImageNumber);
        }

        private List<string> BuildNgSuffixCandidates(string imageNumberStr, string source2_1Path)
        {
            var candidates = new List<string>();

            if (!string.IsNullOrEmpty(source2_1Path))
            {
                var extracted = ExtractSuffixFromFilename(Path.GetFileNameWithoutExtension(source2_1Path));
                if (!string.IsNullOrEmpty(extracted))
                {
                    candidates.Add(extracted);
                }
            }

            candidates.Add("_" + imageNumberStr);

            var padded4 = "_" + imageNumberStr.PadLeft(4, '0');
            if (!string.Equals(padded4, "_" + imageNumberStr, StringComparison.Ordinal))
            {
                candidates.Add(padded4);
            }

            return candidates
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string FindFirstImageBySuffixCandidates(string directory, List<string> suffixCandidates)
        {
            if (suffixCandidates == null || suffixCandidates.Count == 0)
                return null;

            foreach (var suffix in suffixCandidates)
            {
                var bmp = FindFirstImageBySuffix(directory, suffix, "bmp");
                if (!string.IsNullOrEmpty(bmp))
                    return bmp;

                var png = FindFirstImageBySuffix(directory, suffix, "png");
                if (!string.IsNullOrEmpty(png))
                    return png;
            }

            return null;
        }

        private static string FindFirstImageBySuffix(string directory, string suffix, string extension)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return null;

                return Directory.EnumerateFiles(directory, $"*{suffix}.{extension}").FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
         
        /// <summary>
        /// 显示缺失文件的警告对话框
        /// </summary>
        private void ShowMissingFilesWarning(List<(string groupName, List<string> missingFiles)> invalidGroups, bool is3DEnabled)
        {
            var message = new StringBuilder();
            message.AppendLine($"检测到 {invalidGroups.Count} 组NG图片文件不完整，将不纳入检测范围：");
            message.AppendLine();
            
            string requiredFiles = is3DEnabled ? "5张图片（图像源1、图像源2_1、图像源2_2、3D高度图、3D灰度图）" : "3张图片（图像源1、图像源2_1、图像源2_2）";
            message.AppendLine($"当前模式需要每组有 {requiredFiles}");
            message.AppendLine();
            
            int displayCount = Math.Min(invalidGroups.Count, 10); // 最多显示10组
            for (int i = 0; i < displayCount; i++)
            {
                var (groupName, missingFiles) = invalidGroups[i];
                message.AppendLine($"• {groupName}");
                message.AppendLine($"  缺失: {string.Join(", ", missingFiles)}");
                message.AppendLine();
            }
            
            if (invalidGroups.Count > displayCount)
            {
                message.AppendLine($"... 还有 {invalidGroups.Count - displayCount} 组文件不完整");
            }
            
            message.AppendLine("建议：");
            message.AppendLine("1. 检查原图存储目录中对应的文件夹结构");
            message.AppendLine("2. 确认图片文件是否正确保存");
            if (is3DEnabled)
            {
                message.AppendLine("3. 如不需要3D检测，可在配置中关闭3D功能");
            }
            
            MessageBox.Show(message.ToString(), "文件缺失警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// 从文件名提取图片编号
        /// </summary>
        private int? ExtractImageNumberFromFilename(string filename)
        {
            try
            {
                // 提取文件名中的数字部分（不包含扩展名）
                string nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
                
                // 尝试直接解析为数字
                if (int.TryParse(nameWithoutExt, out int number))
                {
                    return number;
                }
                
                // 如果直接解析失败，尝试提取数字
                var match = Regex.Match(nameWithoutExt, @"\d+");
                if (match.Success && int.TryParse(match.Value, out number))
                {
                    return number;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 加载模板图片
        /// </summary>
        private async Task<List<ImageGroupSet>> LoadTemplateImages(string categoryName, string modeName)
        {
            LoadingDialog loadingDialog = null;
            try
            {
                // 显示加载对话框
                loadingDialog = new LoadingDialog($"正在加载{modeName}图片，请稍候...");
                loadingDialog.Owner = this;
                loadingDialog.Show();
                
                // 让LoadingDialog完全渲染
                await Task.Delay(100);
                Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                
                // 获取当前模板名称
                string templateName = GetCurrentTemplateName();
                if (string.IsNullOrEmpty(templateName))
                {
                    MessageBox.Show("无法获取当前模板名称", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return new List<ImageGroupSet>();
                }
                
                // 构建模板图片目录路径
                string templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", templateName, categoryName);
                LogManager.Info($"查找模板图片目录: {templateDir}");
                
                if (!Directory.Exists(templateDir))
                {
                    MessageBox.Show($"模板目录不存在:\n{templateDir}", "目录不存在", MessageBoxButton.OK, MessageBoxImage.Information);
                    return new List<ImageGroupSet>();
                }
                
                // 在后台线程异步查找图片
                var imageGroups = await Task.Run(() => FindImagesInTemplateDirectory(templateDir));
                
                if (imageGroups.Count == 0)
                {
                    MessageBox.Show($"在模板目录中未找到有效的图片组:\n{templateDir}", 
                                  "未找到图片", MessageBoxButton.OK, MessageBoxImage.Information);
                    return new List<ImageGroupSet>();
                }
                
                LogManager.Info($"在模板目录中找到 {imageGroups.Count} 组图片");
                return imageGroups;
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载模板图片失败: {ex.Message}");
                MessageBox.Show($"加载模板图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<ImageGroupSet>();
            }
            finally
            {
                // 确保关闭LoadingDialog
                if (loadingDialog != null)
                {
                    await Task.Delay(200);
                    loadingDialog.Close();
                }
            }
        }

        /// <summary>
        /// 在模板目录中查找图片（按正确的目录结构）
        /// </summary>
        private List<ImageGroupSet> FindImagesInTemplateDirectory(string templateDir)
        {
            var imageGroups = new List<ImageGroupSet>();
            
            try
            {
                // 检查必需的2D目录结构
                var source1Dir = Path.Combine(templateDir, "图像源1");
                var source2_1Dir = Path.Combine(templateDir, "图像源2_1");
                var source2_2Dir = Path.Combine(templateDir, "图像源2_2");
                var threeDDir = Path.Combine(templateDir, "3D");

                // 如果关键目录不存在，尝试扁平化查找（兼容旧格式）
                if (!Directory.Exists(source1Dir) && !Directory.Exists(source2_1Dir) && !Directory.Exists(source2_2Dir))
                {
                    LogManager.Warning($"未找到标准目录结构，尝试扁平化查找: {templateDir}");
                    return FindImagesInTemplateDirectoryFlat(templateDir);
                }

                // 收集所有图片文件的后缀
                var allSuffixes = new HashSet<string>();
                
                // 从图像源2_1目录收集后缀（通常最全）
                if (Directory.Exists(source2_1Dir))
                {
                    var source2_1Files = Directory.GetFiles(source2_1Dir, "*.bmp");
                    foreach (var file in source2_1Files)
                    {
                        string suffix = ExtractSuffixFromFilename(Path.GetFileNameWithoutExtension(file));
                        if (!string.IsNullOrEmpty(suffix))
                        {
                            allSuffixes.Add(suffix);
                        }
                    }
                }

                LogManager.Info($"在模板目录中找到 {allSuffixes.Count} 个不同的图片后缀");

                // 为每个后缀创建图片组
                foreach (string suffix in allSuffixes)
                {
                    var imageGroup = CreateImageGroupFromTemplateDirectories(templateDir, suffix);
                    if (imageGroup != null && imageGroup.IsValid)
                    {
                        imageGroups.Add(imageGroup);
                        LogManager.Info($"创建模板图片组: {imageGroup.BaseName} (2D: {imageGroup.Has2DImages}, 3D: {imageGroup.Has3DImages})");
                    }
                }

                LogManager.Info($"在模板目录中共找到 {imageGroups.Count} 个有效图片组");
            }
            catch (Exception ex)
            {
                LogManager.Error($"在模板目录中查找图片失败: {ex.Message}");
            }

            return imageGroups;
        }

        /// <summary>
        /// 从模板目录结构创建图片组
        /// </summary>
        private ImageGroupSet CreateImageGroupFromTemplateDirectories(string templateDir, string suffix)
        {
            try
            {
                var imageGroup = new ImageGroupSet
                {
                    BaseName = suffix
                };

                // 查找2D图片
                var source1Dir = Path.Combine(templateDir, "图像源1");
                var source2_1Dir = Path.Combine(templateDir, "图像源2_1");
                var source2_2Dir = Path.Combine(templateDir, "图像源2_2");

                if (Directory.Exists(source1Dir))
                {
                    var source1Files = Directory.GetFiles(source1Dir, $"*{suffix}.bmp");
                    if (source1Files.Length > 0)
                    {
                        imageGroup.Source1Path = source1Files[0];
                    }
                }

                if (Directory.Exists(source2_1Dir))
                {
                    var source2_1Files = Directory.GetFiles(source2_1Dir, $"*{suffix}.bmp");
                    if (source2_1Files.Length > 0)
                    {
                        imageGroup.Source2_1Path = source2_1Files[0];
                    }
                }

                if (Directory.Exists(source2_2Dir))
                {
                    var source2_2Files = Directory.GetFiles(source2_2Dir, $"*{suffix}.bmp");
                    if (source2_2Files.Length > 0)
                    {
                        imageGroup.Source2_2Path = source2_2Files[0];
                    }
                }

                // 查找3D图片
                var threeDDir = Path.Combine(templateDir, "3D");
                if (Directory.Exists(threeDDir))
                {
                    var heightFiles = Directory.GetFiles(threeDDir, $"height*{suffix}.png");
                    if (heightFiles.Length > 0)
                    {
                        imageGroup.HeightImagePath = heightFiles[0];
                    }

                    var grayFiles = Directory.GetFiles(threeDDir, $"gray*{suffix}.png");
                    if (grayFiles.Length > 0)
                    {
                        imageGroup.GrayImagePath = grayFiles[0];
                    }
                }

                return imageGroup;
            }
            catch (Exception ex)
            {
                LogManager.Error($"从模板目录创建图片组失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 扁平化查找图片（兼容旧格式）
        /// </summary>
        private List<ImageGroupSet> FindImagesInTemplateDirectoryFlat(string templateDir)
        {
            var imageGroups = new List<ImageGroupSet>();
            
            try
            {
                // 查找所有图片文件
                var imageFiles = Directory.GetFiles(templateDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                               f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                               f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || 
                               f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (imageFiles.Count == 0)
                {
                    LogManager.Warning($"模板目录中没有找到图片文件: {templateDir}");
                    return imageGroups;
                }

                // 按文件名提取后缀进行分组
                var suffixGroups = imageFiles
                    .Select(f => new { File = f, Suffix = ExtractSuffixFromPath(f) })
                    .Where(x => !string.IsNullOrEmpty(x.Suffix))
                    .GroupBy(x => x.Suffix)
                    .ToList();

                foreach (var group in suffixGroups)
                {
                    var imageGroup = new ImageGroupSet
                    {
                        BaseName = group.Key
                    };

                    // 按文件名模式分配图片路径
                    foreach (var file in group)
                    {
                        string fileName = Path.GetFileName(file.File);
                        
                        if (fileName.Contains("source1") || fileName.Contains("图像源1"))
                        {
                            imageGroup.Source1Path = file.File;
                        }
                        else if (fileName.Contains("source2_1") || fileName.Contains("图像源2_1"))
                        {
                            imageGroup.Source2_1Path = file.File;
                        }
                        else if (fileName.Contains("source2_2") || fileName.Contains("图像源2_2"))
                        {
                            imageGroup.Source2_2Path = file.File;
                        }
                        else if (fileName.Contains("height") || fileName.Contains("高度"))
                        {
                            imageGroup.HeightImagePath = file.File;
                        }
                        else if (fileName.Contains("gray") || fileName.Contains("灰度"))
                        {
                            imageGroup.GrayImagePath = file.File;
                        }
                    }

                    // 验证图片组是否有效
                    if (imageGroup.IsValid)
                    {
                        imageGroups.Add(imageGroup);
                        LogManager.Info($"找到有效图片组（扁平化）: {imageGroup.BaseName}");
                    }
                    else
                    {
                        LogManager.Warning($"跳过无效图片组（扁平化）: {imageGroup.BaseName}");
                    }
                }

                LogManager.Info($"扁平化查找共找到 {imageGroups.Count} 个有效图片组");
            }
            catch (Exception ex)
            {
                LogManager.Error($"扁平化查找图片失败: {ex.Message}");
            }

            return imageGroups;
        }

        /// <summary>
        /// 从文件路径提取后缀
        /// </summary>
        private string ExtractSuffixFromPath(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                
                // 尝试匹配各种后缀模式
                var patterns = new[]
                {
                    @".*_(\d+)$",           // 以数字结尾
                    @".*_([a-zA-Z]+\d+)$",  // 字母+数字结尾
                    @".*_([^_]+)$"          // 任何非下划线字符结尾
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(fileName, pattern);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                // 如果没有匹配到后缀，使用整个文件名
                return fileName;
            }
            catch (Exception ex)
            {
                LogManager.Error($"提取文件后缀失败: {ex.Message}");
                return Path.GetFileNameWithoutExtension(filePath);
            }
        }

        /// <summary>
        /// 从文件名提取后缀
        /// </summary>
        private string ExtractSuffixFromFilename(string filename)
        {
            try
            {
                // 匹配形如 xxx_0, xxx_1 等格式
                var match = Regex.Match(filename, @".*_(\d+)$");
                if (match.Success)
                {
                    return "_" + match.Groups[1].Value;
                }

                // 如果没有匹配，返回空
                return "";
            }
            catch (Exception ex)
            {
                LogManager.Error($"提取文件名后缀失败: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取当前模板名称
        /// </summary>
        private string GetCurrentTemplateName()
        {
            try
            {
                // 从MainWindow的frame1获取Page1实例
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow?.frame1?.Content is Page1 page1Instance)
                {
                    return page1Instance.CurrentTemplateName;
                }

                // 如果无法获取，返回默认模板名称
                LogManager.Warning("无法获取当前模板名称，使用默认值");
                return "MESA-25"; // 默认模板名称
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取当前模板名称失败: {ex.Message}");
                return "MESA-25"; // 默认模板名称
            }
        }

        /// <summary>
        /// NG图片信息类
        /// </summary>
        private class NGImageInfo
        {
            public int ImageNumber { get; set; }        // 图片编号
            public string DateFolder { get; set; }      // 日期文件夹名
            public string NgTypeName { get; set; }      // NG类型名称
            public string Source2_1Path { get; set; }   // 图像源2_1路径
            public string NGFolderPath { get; set; }    // NG类型文件夹路径
        }

    }
} 
