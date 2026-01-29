using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    /// <summary>
    /// 单片动态/静态测试参数窗口（检测模式：选择图片集）
    /// </summary>
    public partial class SingleSampleDynamicStaticParametersWindow : Window
    {
        private const string SingleSampleDynamicStaticImageSetFolderName = "单片动态静态图片集";

        public string SelectedFolderPath { get; private set; } = string.Empty;
        public int LoopCycle { get; private set; } = 0;

        private readonly string _currentTemplateName = string.Empty;
        private List<SingleSampleImageSetInfo> _imageSets = new List<SingleSampleImageSetInfo>();

        public SingleSampleDynamicStaticParametersWindow(string currentTemplateName)
        {
            InitializeComponent();
            _currentTemplateName = currentTemplateName ?? string.Empty;
            this.Loaded += (s, e) => LoadImageSets();
        }

        private void LoadImageSets()
        {
            try
            {
                _imageSets.Clear();
                SelectedFolderPath = string.Empty;
                LoopCycle = 0;

                string imageSetRootDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "templates",
                    _currentTemplateName,
                    SingleSampleDynamicStaticImageSetFolderName
                );

                LogManager.Info($"搜索单片动态/静态图片集目录: {imageSetRootDir}");

                if (!Directory.Exists(imageSetRootDir))
                {
                    MessageBox.Show(
                        $"未找到单片动态/静态图片集目录:\n{imageSetRootDir}\n\n请先使用「单片动态/静态测试集制作」功能创建图片集。",
                        "目录不存在",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    DialogResult = false;
                    Close();
                    return;
                }

                var setFolders = Directory.GetDirectories(imageSetRootDir);
                if (setFolders.Length == 0)
                {
                    MessageBox.Show(
                        "未找到任何单片动态/静态图片集。\n\n请先使用「单片动态/静态测试集制作」功能创建图片集。",
                        "提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    DialogResult = false;
                    Close();
                    return;
                }

                foreach (var setFolder in setFolders)
                {
                    var folderInfo = GetImageSetInfo(setFolder);
                    if (folderInfo != null)
                    {
                        _imageSets.Add(folderInfo);
                    }
                }

                _imageSets = _imageSets
                    .OrderByDescending(f => f.FolderName)
                    .ToList();

                ImageSetListBox.ItemsSource = _imageSets;
                if (_imageSets.Count > 0)
                {
                    ImageSetListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载单片动态/静态图片集失败: {ex.Message}");
                MessageBox.Show($"加载单片动态/静态图片集失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private SingleSampleImageSetInfo GetImageSetInfo(string folderPath)
        {
            try
            {
                string folderName = Path.GetFileName(folderPath);
                int loopCycle = 0;
                string errorMessage = null;

                string source1Dir = Path.Combine(folderPath, "图号1", "图像源1");
                if (!Directory.Exists(source1Dir))
                {
                    errorMessage = "缺少 图号1/图像源1";
                }
                else
                {
                    loopCycle = Directory.GetFiles(source1Dir, "*.bmp").Length;
                    if (loopCycle <= 0)
                    {
                        loopCycle = Directory.GetFiles(source1Dir, "*.png").Length;
                    }

                    if (loopCycle <= 0)
                    {
                        errorMessage = "图像源1内无图片";
                    }
                }

                return new SingleSampleImageSetInfo
                {
                    FolderPath = folderPath,
                    FolderName = folderName,
                    LoopCycle = loopCycle,
                    IsValid = string.IsNullOrEmpty(errorMessage),
                    Description = string.IsNullOrEmpty(errorMessage)
                        ? $"巡回: {loopCycle} 次"
                        : $"无效: {errorMessage}"
                };
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取单片动态/静态图片集信息失败: {ex.Message}");
                return null;
            }
        }

        private void ImageSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ImageSetListBox.SelectedItem is SingleSampleImageSetInfo selectedFolder)
                {
                    SelectedFolderPath = selectedFolder.FolderPath;
                    LoopCycle = selectedFolder.LoopCycle;

                    LoopCycleInfoTextBlock.Text = selectedFolder.IsValid ? selectedFolder.LoopCycle.ToString() : "--";
                    FolderPathInfoTextBlock.Text = selectedFolder.FolderPath;

                    LogManager.Info($"选择单片动态/静态图片集: {selectedFolder.FolderName}, 巡回: {selectedFolder.LoopCycle}, 有效: {selectedFolder.IsValid}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"选择单片动态/静态图片集失败: {ex.Message}");
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ImageSetListBox.SelectedItem is SingleSampleImageSetInfo selectedFolder)
                {
                    if (!selectedFolder.IsValid)
                    {
                        MessageBox.Show("所选图片集结构不完整，请重新选择或重新制作。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    SelectedFolderPath = selectedFolder.FolderPath;
                    LoopCycle = selectedFolder.LoopCycle;

                    DialogResult = true;
                    Close();
                    return;
                }

                MessageBox.Show("请选择一个单片动态/静态图片集", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                LogManager.Error($"确认选择失败: {ex.Message}");
                MessageBox.Show($"确认选择失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class SingleSampleImageSetInfo
    {
        public string FolderPath { get; set; }
        public string FolderName { get; set; }
        public int LoopCycle { get; set; }
        public bool IsValid { get; set; }
        public string Description { get; set; }
    }
}
