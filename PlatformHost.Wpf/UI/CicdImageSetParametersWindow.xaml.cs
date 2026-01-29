using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class CicdImageSetParametersWindow : Window
    {
        private const string CicdFolderName = "CICD检测";
        private const string CicdReferCsvName = "cicd_refer.csv";

        public string SelectedImageSetName { get; private set; } = string.Empty;

        private readonly string _currentTemplateName;
        private List<CicdImageSetInfo> _imageSets = new List<CicdImageSetInfo>();

        public CicdImageSetParametersWindow(string currentTemplateName)
        {
            InitializeComponent();
            _currentTemplateName = currentTemplateName ?? string.Empty;
            Loaded += (s, e) => LoadImageSets();
        }

        private void LoadImageSets()
        {
            try
            {
                _imageSets.Clear();
                SelectedImageSetName = string.Empty;

                string cicdRootDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "templates",
                    _currentTemplateName,
                    CicdFolderName);

                LogManager.Info($"搜索CICD图片集目录: {cicdRootDir}");

                if (!Directory.Exists(cicdRootDir))
                {
                    MessageBox.Show(
                        $"未找到CICD检测目录:\n{cicdRootDir}\n\n请先使用「CICD图片集制作」创建图片集。",
                        "目录不存在",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    DialogResult = false;
                    Close();
                    return;
                }

                var setFolders = Directory.GetDirectories(cicdRootDir)
                    .Where(d => !string.Equals(Path.GetFileName(d), "测试", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (setFolders.Count == 0)
                {
                    MessageBox.Show(
                        "未找到任何CICD图片集。\n\n请先使用「CICD图片集制作」创建图片集。",
                        "提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    DialogResult = false;
                    Close();
                    return;
                }

                foreach (var setFolder in setFolders)
                {
                    var info = GetImageSetInfo(setFolder);
                    if (info != null)
                    {
                        _imageSets.Add(info);
                    }
                }

                _imageSets = _imageSets
                    .OrderByDescending(f => f.ImageSetName)
                    .ToList();

                ImageSetListBox.ItemsSource = _imageSets;
                if (_imageSets.Count > 0)
                {
                    ImageSetListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载CICD图片集失败: {ex.Message}");
                MessageBox.Show($"加载CICD图片集失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private CicdImageSetInfo GetImageSetInfo(string folderPath)
        {
            try
            {
                string folderName = Path.GetFileName(folderPath);
                string referCsvPath = Path.Combine(folderPath, CicdReferCsvName);
                bool hasRefer = File.Exists(referCsvPath);

                int groupCount = 0;
                string error = null;

                var groupDirs = Directory.GetDirectories(folderPath);
                groupCount = groupDirs.Length;
                if (groupCount == 0)
                {
                    error = "未找到图片组文件夹";
                }
                else if (!hasRefer)
                {
                    error = $"缺少 {CicdReferCsvName}";
                }

                return new CicdImageSetInfo
                {
                    FolderPath = folderPath,
                    ImageSetName = folderName,
                    GroupCount = groupCount,
                    IsValid = string.IsNullOrEmpty(error),
                    Description = string.IsNullOrEmpty(error)
                        ? $"图片组: {groupCount} 个"
                        : $"无效: {error}"
                };
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取CICD图片集信息失败: {ex.Message}");
                return null;
            }
        }

        private void ImageSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageSetListBox.SelectedItem is CicdImageSetInfo selected)
            {
                SelectedImageSetName = selected.ImageSetName;
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageSetListBox.SelectedItem is CicdImageSetInfo selected)
            {
                if (!selected.IsValid)
                {
                    MessageBox.Show("所选图片集结构不完整，请重新选择或重新制作。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedImageSetName = selected.ImageSetName;
                DialogResult = true;
                Close();
                return;
            }

            MessageBox.Show("请选择一个CICD图片集", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    internal class CicdImageSetInfo
    {
        public string FolderPath { get; set; }
        public string ImageSetName { get; set; }
        public int GroupCount { get; set; }
        public bool IsValid { get; set; }
        public string Description { get; set; }
    }
}

