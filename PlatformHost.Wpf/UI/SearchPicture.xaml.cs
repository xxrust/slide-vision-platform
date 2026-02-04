using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using WpfApp2.Models;
using Newtonsoft.Json;
using System.Text;

namespace WpfApp2.UI
{
    public partial class SearchPicture : Page, INotifyPropertyChanged
    {
        private List<string> imagePaths = new List<string>(); // 图片路径列表
        private int currentImageIndex = 0;
        private int currentPage = 1;
        private int itemsPerPage = 9;

        public ObservableCollection<string> DisplayedImages { get; set; } = new ObservableCollection<string>();

        public string PageInfo => $"第 {currentPage} 页 / 共 {TotalPages} 页";

        public int TotalPages => (int)Math.Ceiling((double)imagePaths.Count / itemsPerPage);

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<DefectType> DefectTypes { get; set; } = new ObservableCollection<DefectType>();
        private DefectType _selectedDefect; // 当前选中的缺陷
        private Dictionary<string, DefectData> _defectData = new Dictionary<string, DefectData>();
        // 改为存储 AxisSpan 对象，而不是 AxisSpanUnderMouse
        //private Dictionary<ScottPlot.WPF.WpfPlot, AxisSpan> _spanStates = new Dictionary<ScottPlot.WPF.WpfPlot, AxisSpan>();
        //private AxisSpan SpanBeingDragged; // 当前拖拽的 AxisSpan
        AxisSpanUnderMouse SpanBeingDragged = null;

        public SearchPicture()
        {
            InitializeComponent();
            DataContext = this;
            LoadDefectTypes();
            LoadDefectData();  // 加载缺陷数据
            // 默认选中第一个缺陷并更新 TabControl 和 WpfPlot
            if (DefectTypes.Any())
            {
                _selectedDefect = DefectTypes[0];
                UpdateTabs(_selectedDefect); // 初始化 TabControl
                PlotTabControl.SelectedIndex = 0; // 默认选中第一个 Tab
                UpdatePlot(0); // 初始化第一个 Tab 的 WpfPlot
            }
            LoadImages();
            UpdateDisplayedImages();
            ImageItemsControl.ItemsSource = DisplayedImages;

            // 注册窗口关闭事件,程序退出后保存缺陷检索范围
           // Window.GetWindow(this).Closing += (s, e) => SaveDefectTypes();
        }


        private void LoadDefectTypes()
        {
            string jsonFilePath = "defectTypes.json";
            if (File.Exists(jsonFilePath))
            {
                string jsonContent = File.ReadAllText(jsonFilePath, Encoding.UTF8);
                var defectTypes = JsonConvert.DeserializeObject<List<DefectType>>(jsonContent);
                if (defectTypes != null)
                {
                    foreach (var defectType in defectTypes)
                    {
                        // 如果 JSON 中未定义 TabRanges，初始化为空字典
                        if (defectType.TabRanges == null)
                        {
                            defectType.TabRanges = new Dictionary<string, double[]>();
                        }
                        foreach (var tab in defectType.Tabs)
                        {
                            if (!defectType.TabRanges.ContainsKey(tab))
                            {
                                defectType.TabRanges[tab] = new double[] { 10, 100 }; // 默认值
                            }
                        }
                        DefectTypes.Add(defectType);
                    }
                }
            }
            else
            {
                MessageBox.Show($"JSON 文件 {jsonFilePath} 不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDefectTypes()
        {
            string jsonFilePath = "defectTypes.json";
            try
            {
                string jsonContent = JsonConvert.SerializeObject(DefectTypes, Formatting.Indented);
                File.WriteAllText(jsonFilePath, jsonContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存缺陷类型失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDefectData()
        {
            string dataFilePath = "defectData.json"; // 请替换为实际路径
            if (File.Exists(dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(dataFilePath);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, DefectData>>(json);
                    if (data != null)
                    {
                        _defectData = data;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载数据文件 {dataFilePath} 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"数据文件 {dataFilePath} 不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void DefectButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag is DefectType defectType)
            {
                _selectedDefect = defectType;
                UpdateTabs(defectType); // 更新 TabControl
                PlotTabControl.SelectedIndex = 0; // 默认选中第一个 Tab
                UpdatePlot(0); // 更新第一个 Tab 的 WpfPlot
            }
        }

        private void UpdateTabs(DefectType defectType)
        {
            PlotTabControl.Items.Clear();
            foreach (var tab in defectType.Tabs)
            {
                var tabItem = new TabItem
                {
                    Header = tab,
                    FontSize = 30,
                    Height = 100
                };
                var plot = new ScottPlot.WPF.WpfPlot();
                plot.MouseDown += FormsPlot1_MouseDown;
                plot.MouseUp += FormsPlot1_MouseUp;
                plot.MouseMove += FormsPlot1_MouseMove;
                tabItem.Content = plot;
                PlotTabControl.Items.Add(tabItem);
            }
        }

        private void PlotTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlotTabControl.SelectedIndex >= 0 && _selectedDefect != null)
            {
                UpdatePlot(PlotTabControl.SelectedIndex);
            }
        }

        private void UpdatePlot(int tabIndex)
        {
            if (_selectedDefect == null || tabIndex < 0 || tabIndex >= PlotTabControl.Items.Count) return;

            var tabItem = PlotTabControl.Items[tabIndex] as TabItem;
            if (tabItem == null) return;

            var plot = tabItem.Content as ScottPlot.WPF.WpfPlot;
            if (plot == null)
            {
                Console.WriteLine($"Content is not WpfPlot, actual type: {tabItem.Content?.GetType()?.FullName}");
                return;
            }

            plot.Plot.Clear();
            string tabName = _selectedDefect.Tabs[tabIndex];
            double[] values = null;
            if (_defectData != null &&
                !string.IsNullOrEmpty(_selectedDefect.Name) &&
                _defectData.ContainsKey(_selectedDefect.Name) &&
                _defectData[_selectedDefect.Name].Tabs != null &&
                _defectData[_selectedDefect.Name].Tabs.ContainsKey(tabName))
            {
                values = _defectData[_selectedDefect.Name].Tabs[tabName];
            }

            if (values != null && values.Length > 0)
            {
                var hist = ScottPlot.Statistics.Histogram.WithBinCount(100, values);
                var barPlot = plot.Plot.Add.Bars(hist.Bins, hist.Counts);
                foreach (var bar in barPlot.Bars)
                {
                    bar.Size = hist.FirstBinSize;
                    bar.LineWidth = 0;
                    bar.FillStyle.AntiAlias = false;
                    bar.FillColor = ScottPlot.Colors.C0.Lighten(.3);
                }

                ScottPlot.Statistics.ProbabilityDensity pd = new ScottPlot.Statistics.ProbabilityDensity(values);
                double[] xs = ScottPlot.Generate.Range(values.Min(), values.Max(), 1);
                double[] ys = pd.GetYs(xs, 100);
                var curve = plot.Plot.Add.ScatterLine(xs, ys);
                curve.Axes.YAxis = plot.Plot.Axes.Right;
                curve.LineWidth = 2;
                curve.LineColor = ScottPlot.Colors.Black;
                curve.LinePattern = ScottPlot.LinePattern.DenselyDashed;

                plot.Plot.YLabel("Number of Items");
                plot.Plot.XLabel($"{tabName} (单位)");
                plot.Plot.Axes.Right.Label.Text = "Probability (%)";
                //plot.Plot.Axes.Margins(custom: 0);

                double x1 = _selectedDefect.TabRanges[tabName][0];
                double x2 = _selectedDefect.TabRanges[tabName][1];
                if (x1 == 0 && x2 == 0)
                {
                    x1 = values.Min();
                    x2 = x1 + (values.Max() - values.Min()) * 0.2;
                    _selectedDefect.TabRanges[tabName] = new double[] { x1, x2 };
                    //SaveDefectTypes();
                }
                var hs = plot.Plot.Add.HorizontalSpan(x1, x2);
                hs.IsDraggable = true;
                hs.IsResizable = true;
            }
            else
            {
                plot.Plot.YLabel("Number of Items");
                plot.Plot.XLabel($"{tabName} (单位)");
                plot.Plot.Axes.Right.Label.Text = "Probability (%)";
                MessageBox.Show($"未找到 {_selectedDefect.Name} 的 {tabName} 数据", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            plot.Refresh();
        }


        private void FormsPlot1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScottPlot.WPF.WpfPlot plot)
            {
                var position = e.GetPosition(plot);
                var spanUnderMouse = GetSpanUnderMouse(plot, (float)position.X, (float)position.Y);
                if (spanUnderMouse != null)
                {
                    SpanBeingDragged = spanUnderMouse;
                    plot.UserInputProcessor.Disable();
                }
            }
        }

        private void FormsPlot1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScottPlot.WPF.WpfPlot plot)
            {  
                plot.UserInputProcessor.Enable(); // enable panning
                plot.Refresh();

                // 保留保存 TabRanges 的逻辑
                int tabIndex = PlotTabControl.SelectedIndex;
                if (tabIndex >= 0 && _selectedDefect != null)
                {
                    string tabName = _selectedDefect.Tabs[tabIndex];
                    var span = plot.Plot.GetPlottables<AxisSpan>().FirstOrDefault() as ScottPlot.Plottables.HorizontalSpan; ;
                    if (span != null)
                    {
                        _selectedDefect.TabRanges[tabName] = new double[] { span.X1, span.X2 };
                    }
                }
                SpanBeingDragged = null;
            }
        }

        private void FormsPlot1_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is ScottPlot.WPF.WpfPlot plot)
            {
                if (SpanBeingDragged != null)
                {
                    // 正在拖拽，更新范围
                    Coordinates mouseNow = plot.Plot.GetCoordinates((float)e.GetPosition(plot).X, (float)e.GetPosition(plot).Y);
                    SpanBeingDragged.DragTo(mouseNow);
                    plot.Refresh();
                }
                else
                {
                    // 未拖拽，设置光标
                    var position = e.GetPosition(plot);
                    var spanUnderMouse = GetSpanUnderMouse(plot, (float)position.X, (float)position.Y);
                    if (spanUnderMouse == null) Cursor = Cursors.Arrow;
                    else if (spanUnderMouse.IsResizingHorizontally) Cursor = Cursors.SizeWE;
                    else if (spanUnderMouse.IsResizingVertically) Cursor = Cursors.SizeNS;
                    else if (spanUnderMouse.IsMoving) Cursor = Cursors.SizeAll;
                }
            }
        }

        private AxisSpanUnderMouse GetSpanUnderMouse(ScottPlot.WPF.WpfPlot plot, float x, float y)
        {
            CoordinateRect rect = plot.Plot.GetCoordinateRect(x, y, radius: 10);
            foreach (AxisSpan span in plot.Plot.GetPlottables<AxisSpan>().Reverse())
            {
                AxisSpanUnderMouse spanUnderMouse = span.UnderMouse(rect);
                if (spanUnderMouse != null)
                    return spanUnderMouse;
            }
            return null;
        }


        private void LoadImages()
        {
            // 加载图片路径到 imagePaths 列表
            for (int i = 1; i <= 2; i++)
            {
                string absolutePath = Path.GetFullPath($"Images/image{i}.bmp");
                imagePaths.Add(absolutePath); // 示例图片路径
                //如果图片不存在，则弹窗报错

                if (!File.Exists(imagePaths[i - 1]))
                {
                    MessageBox.Show($"图片 {absolutePath} 不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateDisplayedImages()
        {
            DisplayedImages.Clear();
            int startIndex = (currentPage - 1) * itemsPerPage;
            for (int i = startIndex; i < startIndex + itemsPerPage && i < imagePaths.Count; i++)
            {
                DisplayedImages.Add(imagePaths[i]);
            }
            OnPropertyChanged(nameof(PageInfo));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                UpdateDisplayedImages();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < TotalPages)
            {
                currentPage++;
                UpdateDisplayedImages();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            //显示fram3
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.ContentC.Content = mainWindow.frame1; // 设置内容为 Page1

        }

        private void ClearImage_Click(object sender, RoutedEventArgs e)
        { }

    }
}
