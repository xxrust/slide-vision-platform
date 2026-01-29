using System;
using System.Windows;

namespace WpfApp2.UI
{
    public partial class Create3DTemplateGuideWindow : Window
    {
        private readonly string _threeDDirectory;

        public Create3DTemplateGuideWindow(string templateName, string threeDDirectory)
        {
            InitializeComponent();

            _threeDDirectory = threeDDirectory ?? string.Empty;
            PathTextBox.Text = _threeDDirectory;

            if (!string.IsNullOrWhiteSpace(templateName))
            {
                TemplateNameText.Text = $"模板：{templateName}";
            }
            else
            {
                TemplateNameText.Text = "模板：未命名";
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_threeDDirectory ?? string.Empty);
                CopyStatusText.Text = "已复制到剪贴板，可在 LJ Developer 中直接粘贴。";
            }
            catch (Exception ex)
            {
                CopyStatusText.Text = $"复制失败: {ex.Message}";
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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

