using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfApp2.UI.Controls
{
    /// <summary>
    /// SmartAnalysisHelpPage.xaml 的交互逻辑
    /// </summary>
    public partial class SmartAnalysisHelpPage : UserControl
    {
        public event EventHandler BackRequested;

        public SmartAnalysisHelpPage()
        {
            InitializeComponent();
            LoadHelpContent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LoadHelpContent()
        {
            try
            {
                HelpContent.Children.Clear();
                CreateHelpContent();
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock
                {
                    Text = $"加载帮助内容失败: {ex.Message}",
                    Foreground = Brushes.Red,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                HelpContent.Children.Add(errorText);
            }
        }

        private void CreateHelpContent()
        {
            // 添加返回按钮
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var backButton = new Button
            {
                Content = "返回",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 0, 5, 0)
            };
            backButton.Click += BackButton_Click;
            
            buttonPanel.Children.Add(backButton);
            HelpContent.Children.Add(buttonPanel);

            // 标题
            var titleBlock = new TextBlock
            {
                Text = "智能分析组件使用指南",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            HelpContent.Children.Add(titleBlock);

            // 功能概述
            AddSection("功能概述", 
                "智能分析组件是一个专业的质量分析工具，提供多种统计图表和告警功能，帮助您实时监控生产质量状况。");

            // 主要功能
            AddSection("主要功能",
                "• 盒须图：显示数据分布的五数概括\n" +
                "• 控制图：监控过程稳定性\n" +
                "• 直方图：显示数据频率分布\n" +
                "• 统计分析：自动计算均值、标准差、CPK等指标\n" +
                "• 智能告警：支持多种告警策略");

            // 操作说明
            AddSection("操作说明",
                "1. 项目选择：在右侧项目列表中选择要分析的检测项目\n" +
                "2. 图表切换：使用顶部按钮切换不同类型的图表\n" +
                "3. 数据量控制：通过下拉框选择显示的数据量\n" +
                "4. 刷新数据：点击刷新按钮更新最新数据\n" +
                "5. 导出功能：点击导出按钮将数据导出为Excel文件");

            // 图表说明
            AddSection("图表说明",
                "盒须图：\n" +
                "• 箱体表示四分位距（Q1-Q3）\n" +
                "• 中线表示中位数\n" +
                "• 须线表示数据范围\n" +
                "• 可快速识别异常值\n\n" +
                "控制图：\n" +
                "• 蓝色虚线：数据均值\n" +
                "• 绿色实线：上下限中心\n" +
                "• 红色虚线：上下限\n" +
                "• 用于监控过程稳定性\n\n" +
                "直方图：\n" +
                "• 蓝色虚线：数据均值\n" +
                "• 绿色实线：上下限中心\n" +
                "• 红色虚线：上下限\n" +
                "• 显示数据分布形状\n\n" +
                "线条标准说明：\n" +
                "• 蓝色虚线：实际数据的统计均值\n" +
                "• 绿色实线：规格上下限的中心值\n" +
                "• 红色虚线：规格上限和下限");

            // 告警功能
            AddSection("告警功能",
                "支持三种告警策略：\n" +
                "1. 数量分析：超限数量达到阈值时告警\n" +
                "2. 过程能力分析：CPK值低于阈值时告警\n" +
                "3. 连续NG分析：连续NG次数达到阈值时告警");

            // 快捷键
            AddSection("快捷键",
                "• F5：刷新数据\n" +
                "• Ctrl+E：导出数据\n" +
                "• Ctrl+S：打开设置\n" +
                "• F1：显示帮助");

            // 注意事项
            AddSection("注意事项",
                "• 确保检测数据正常采集\n" +
                "• 定期检查告警设置是否合适\n" +
                "• 建议根据实际情况调整统计周期\n" +
                "• 异常情况下请及时联系技术支持");

        }

        private void AddSection(string title, string content)
        {
            // 添加标题
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(74, 144, 226)),
                Margin = new Thickness(0, 15, 0, 5)
            };
            HelpContent.Children.Add(titleBlock);

            // 添加内容
            var contentBlock = new TextBlock
            {
                Text = content,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                LineHeight = 18
            };
            HelpContent.Children.Add(contentBlock);
        }
    }
} 