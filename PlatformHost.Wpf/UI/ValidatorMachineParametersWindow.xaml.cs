using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 验机图片集参数窗口
    /// 支持两种模式：制作模式和检测模式
    /// </summary>
    public partial class ValidatorMachineParametersWindow : Window
    {
        public enum WindowMode
        {
            Collection,  // 验机图片集制作模式
            Detection    // 验机图片检测模式
        }

        // 公开属性
        public string SelectedFolderPath { get; private set; } = string.Empty;
        public int LoopCycle { get; private set; } = 0;
        public int SampleCount { get; private set; } = 0;

        // 私有字段
        private WindowMode _windowMode = WindowMode.Collection;
        private string _currentTemplateName = string.Empty;
        private List<ValidatorFolderInfo> _validatorFolders = new List<ValidatorFolderInfo>();

        /// <summary>
        /// 用于验机图片集制作模式
        /// </summary>
        public ValidatorMachineParametersWindow()
        {
            InitializeComponent();
            _windowMode = WindowMode.Collection;
            SetupCollectionMode();
            ApplyImageSourceHints();
        }

        /// <summary>
        /// 用于验机图片检测模式 - 显示可用的验机图片集文件夹列表
        /// </summary>
        /// <param name="currentTemplateName">当前模板名称</param>
        public ValidatorMachineParametersWindow(string currentTemplateName)
        {
            InitializeComponent();
            _windowMode = WindowMode.Detection;
            _currentTemplateName = currentTemplateName;

            this.Loaded += (s, e) => SetupDetectionMode();
            ApplyImageSourceHints();
        }

        private void ApplyImageSourceHints()
        {
            if (ImageSourceHintTextBlock == null)
            {
                return;
            }

            var displayNames = ImageSourceNaming.GetDisplayNames();
            if (displayNames == null || displayNames.Count == 0)
            {
                ImageSourceHintTextBlock.Text = "2. 系统会递归搜索子文件夹中的图像源文件夹";
                return;
            }

            var namesText = string.Join("、", displayNames.Select(name => $"'{name}'"));
            ImageSourceHintTextBlock.Text = $"2. 系统会递归搜索子文件夹中的 {namesText} 文件夹";
        }

        /// <summary>
        /// 设置为制作模式界面
        /// </summary>
        private void SetupCollectionMode()
        {
            TitleTextBlock.Text = "验机图片集制作参数设置";
            this.Title = "验机图片集制作参数";

            // 显示制作模式控件
            CollectionModePanel.Visibility = Visibility.Visible;
            CollectionHelpPanel.Visibility = Visibility.Visible;

            // 隐藏检测模式控件
            DetectionModePanel.Visibility = Visibility.Collapsed;
            ValidatorFolderListBox.Visibility = Visibility.Collapsed;
            SelectedFolderInfoPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 设置为检测模式界面
        /// </summary>
        private void SetupDetectionMode()
        {
            try
            {
                TitleTextBlock.Text = "选择验机图片集进行检测";
                this.Title = "验机图片检测 - 选择验机图片集";

                // 隐藏制作模式控件
                CollectionModePanel.Visibility = Visibility.Collapsed;
                CollectionHelpPanel.Visibility = Visibility.Collapsed;

                // 显示检测模式控件
                DetectionModePanel.Visibility = Visibility.Visible;
                ValidatorFolderListBox.Visibility = Visibility.Visible;
                SelectedFolderInfoPanel.Visibility = Visibility.Visible;

                // 加载验机图片集列表
                LoadValidatorFolders();
            }
            catch (Exception ex)
            {
                LogManager.Error($"设置检测模式失败: {ex.Message}");
                System.Windows.MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载验机图片集文件夹列表
        /// </summary>
        private void LoadValidatorFolders()
        {
            try
            {
                _validatorFolders.Clear();

                // 获取验机图片集目录
                string validatorMachineDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "templates",
                    _currentTemplateName,
                    "验机图片集"
                );

                LogManager.Info($"搜索验机图片集目录: {validatorMachineDir}");

                if (!Directory.Exists(validatorMachineDir))
                {
                    System.Windows.MessageBox.Show($"未找到验机图片集目录:\n{validatorMachineDir}\n\n请先使用「验机图片集制作」功能创建验机图片集。",
                        "目录不存在", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = false;
                    Close();
                    return;
                }

                // 获取所有LOT号对应的文件夹
                var lotFolders = Directory.GetDirectories(validatorMachineDir);

                if (lotFolders.Length == 0)
                {
                    System.Windows.MessageBox.Show("未找到任何验机图片集。\n\n请先使用「验机图片集制作」功能创建验机图片集。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = false;
                    Close();
                    return;
                }

                // 遍历每个LOT文件夹，获取信息
                foreach (var lotFolder in lotFolders)
                {
                    var folderInfo = GetValidatorFolderInfo(lotFolder);
                    if (folderInfo != null)
                    {
                        _validatorFolders.Add(folderInfo);
                    }
                }

                // 按名称排序
                _validatorFolders = _validatorFolders.OrderByDescending(f => f.FolderName).ToList();

                LogManager.Info($"找到 {_validatorFolders.Count} 个验机图片集");

                // 绑定到ListBox
                ValidatorFolderListBox.ItemsSource = _validatorFolders;

                // 默认选择第一个
                if (_validatorFolders.Count > 0)
                {
                    ValidatorFolderListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载验机图片集列表失败: {ex.Message}");
                System.Windows.MessageBox.Show($"加载验机图片集失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取验机图片集文件夹信息
        /// </summary>
        private ValidatorFolderInfo GetValidatorFolderInfo(string folderPath)
        {
            try
            {
                string folderName = Path.GetFileName(folderPath);

                // 计算样品数量（图号n的文件夹数）
                var sampleDirs = Directory.GetDirectories(folderPath)
                    .Where(d => Path.GetFileName(d).StartsWith("图号"))
                    .ToList();

                int sampleCount = sampleDirs.Count;

                // 计算巡回次数（第一个样本的图片组数）
                int loopCycle = 0;
                if (sampleDirs.Count > 0)
                {
                    // 检查第一个样本目录下的图像源文件夹中的图片数量
                    string firstSampleDir = sampleDirs[0];
                    string source1Dir = Path.Combine(firstSampleDir, ImageSourceNaming.GetDisplayName(0));
                    if (Directory.Exists(source1Dir))
                    {
                        loopCycle = Directory.GetFiles(source1Dir, "*.bmp").Length;
                        if (loopCycle == 0)
                        {
                            loopCycle = Directory.GetFiles(source1Dir, "*.png").Length;
                        }
                    }
                }

                if (sampleCount == 0)
                {
                    LogManager.Warning($"验机图片集 {folderName} 没有样品文件夹");
                    return null;
                }

                return new ValidatorFolderInfo
                {
                    FolderPath = folderPath,
                    FolderName = folderName,
                    SampleCount = sampleCount,
                    LoopCycle = loopCycle,
                    Description = $"样品: {sampleCount} 个, 巡回: {loopCycle} 次"
                };
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取验机图片集信息失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 验机图片集列表选择改变
        /// </summary>
        private void ValidatorFolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ValidatorFolderListBox.SelectedItem is ValidatorFolderInfo selectedFolder)
                {
                    SelectedFolderPath = selectedFolder.FolderPath;
                    SampleCount = selectedFolder.SampleCount;
                    LoopCycle = selectedFolder.LoopCycle;

                    // 更新显示信息
                    SampleCountTextBlock.Text = selectedFolder.SampleCount.ToString();
                    LoopCycleInfoTextBlock.Text = selectedFolder.LoopCycle.ToString();
                    FolderPathInfoTextBlock.Text = selectedFolder.FolderPath;

                    LogManager.Info($"选择验机图片集: {selectedFolder.FolderName}, 样品: {selectedFolder.SampleCount}, 巡回: {selectedFolder.LoopCycle}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"选择验机图片集失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 浏览按钮点击事件（制作模式）
        /// </summary>
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "选择包含图片的文件夹";
                    folderDialog.ShowNewFolderButton = false;

                    if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        SelectedFolderPath = folderDialog.SelectedPath;
                        FolderPathTextBox.Text = SelectedFolderPath;
                        LogManager.Info($"用户选择验机文件夹: {SelectedFolderPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"浏览文件夹失败: {ex.Message}");
                System.Windows.MessageBox.Show($"浏览文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_windowMode == WindowMode.Collection)
                {
                    // 制作模式验证
                    if (string.IsNullOrEmpty(SelectedFolderPath))
                    {
                        System.Windows.MessageBox.Show("请选择一个有效的文件夹", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!Directory.Exists(SelectedFolderPath))
                    {
                        System.Windows.MessageBox.Show("选择的文件夹不存在，请重新选择", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 验证样品数目
                    if (!int.TryParse(SampleCountInputTextBox.Text, out int sampleCount) || sampleCount < 1 || sampleCount > 12)
                    {
                        System.Windows.MessageBox.Show("样品数目必须是1到12之间的整数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    SampleCount = sampleCount;
                    // LoopCycle 将在 Page1 中根据总图片数自动计算
                    LogManager.Info($"验机制作参数验证成功 - 文件夹: {SelectedFolderPath}, 样品数目: {SampleCount}");
                }
                else
                {
                    // 检测模式验证
                    if (string.IsNullOrEmpty(SelectedFolderPath))
                    {
                        System.Windows.MessageBox.Show("请选择一个验机图片集", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    LogManager.Info($"验机检测参数验证成功 - 文件夹: {SelectedFolderPath}, 样品数: {SampleCount}, 巡回次数: {LoopCycle}");
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LogManager.Error($"验证参数失败: {ex.Message}");
                System.Windows.MessageBox.Show($"验证参数失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }

    /// <summary>
    /// 验机图片集文件夹信息
    /// </summary>
    public class ValidatorFolderInfo
    {
        public string FolderPath { get; set; }
        public string FolderName { get; set; }
        public int SampleCount { get; set; }
        public int LoopCycle { get; set; }
        public string Description { get; set; }
    }
}
