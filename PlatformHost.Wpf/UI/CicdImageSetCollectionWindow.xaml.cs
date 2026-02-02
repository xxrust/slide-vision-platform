using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace WpfApp2.UI
{
    public partial class CicdImageSetCollectionWindow : Window
    {
        public ObservableCollection<string> SelectedFiles { get; } = new ObservableCollection<string>();

        public CicdImageSetCollectionWindow()
        {
            InitializeComponent();
            DataContext = this;
            ApplyHints();
        }

        private void ApplyHints()
        {
            var displayNames = ImageSourceNaming.GetDisplayNames();
            if (HintTextBlock != null)
            {
                HintTextBlock.Text = BuildHintText(displayNames);
            }
        }

        private static string BuildHintText(IReadOnlyList<string> displayNames)
        {
            if (displayNames == null || displayNames.Count == 0)
            {
                return "添加多个图片文件作为样本，会自动按文件名后缀匹配同组内的其它图像。可跨不同分类挑选并组合样本，避免样品数目不均衡；测试完成后再命名并生成基准 cicd_refer.csv。";
            }

            var firstName = displayNames[0];
            if (displayNames.Count == 1)
            {
                return $"添加多个图片文件作为样本（建议从“{firstName}”中选择），会自动按文件名后缀匹配同组内的其它图像。可跨不同分类挑选并组合样本，避免样品数目不均衡；测试完成后再命名并生成基准 cicd_refer.csv。";
            }

            var rest = string.Join("、", displayNames.Skip(1).Select(name => $"“{name}”"));
            return $"添加多个图片文件作为样本（建议从“{firstName}”中选择），会自动按文件名后缀匹配同组内的 {rest} 图片。可跨不同分类挑选并组合样本，避免样品数目不均衡；测试完成后再命名并生成基准 cicd_refer.csv。";
        }

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var firstName = ImageSourceNaming.GetDisplayName(0);
                var dialog = new OpenFileDialog
                {
                    Title = string.IsNullOrWhiteSpace(firstName)
                        ? "选择要加入CICD图片集的图片文件"
                        : $"选择要加入CICD图片集的图片文件（建议从“{firstName}”选择）",
                    Filter = "图片文件 (*.bmp;*.png)|*.bmp;*.png|BMP图片文件 (*.bmp)|*.bmp|PNG图片文件 (*.png)|*.png|所有文件 (*.*)|*.*",
                    Multiselect = true
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                foreach (var file in dialog.FileNames ?? new string[0])
                {
                    string selected = file;
                    if (string.IsNullOrWhiteSpace(selected))
                    {
                        continue;
                    }

                    if (SelectedFiles.Any(p => string.Equals(p, selected, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    SelectedFiles.Add(selected);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"添加文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (FileListBox.SelectedItem is string selected)
            {
                SelectedFiles.Remove(selected);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedFiles.Clear();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFiles.Count == 0)
            {
                MessageBox.Show("请至少添加一个图片文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
