using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfApp2.UI
{
    /// <summary>
    /// LJ Developer User项目选择窗口
    /// </summary>
    public partial class LJDeveloperUserProjectSelectWindow : Window
    {
        public string SelectedProjectName { get; private set; }

        public LJDeveloperUserProjectSelectWindow(IEnumerable<string> projectNames)
        {
            InitializeComponent();
            BuildButtons(projectNames);
        }

        private void BuildButtons(IEnumerable<string> projectNames)
        {
            if (projectNames == null)
            {
                return;
            }

            foreach (var name in projectNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(8),
                    Width = 160,
                    Height = 44,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var button = new Button
                {
                    Content = name,
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                button.Click += (s, e) =>
                {
                    SelectedProjectName = name;
                    DialogResult = true;
                    Close();
                };

                border.Child = button;
                ProjectsPanel.Children.Add(border);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

