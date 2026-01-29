using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class CicdCompareDetailsWindow : Window
    {
        private bool _isInitializing;
        private bool _isPathVisible;
        private const int MatrixFixedColumnCount = 2;
        private static readonly HashSet<string> IgnoredItemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "时间戳"
        };
        private sealed class CicdCsvRow
        {
            public string GroupName { get; set; }
            public string ImageNumber { get; set; }
            public bool IsOK { get; set; }
            public string DefectType { get; set; }
            public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public string Key => $"{GroupName ?? ""}#{ImageNumber ?? ""}";
        }

        private sealed class SampleOption
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public override string ToString() => DisplayName ?? Key ?? "";
        }

        private sealed class SampleItemDiffRow
        {
            public string ItemName { get; set; }
            public string ReferValue { get; set; }
            public string TestValue { get; set; }
            public string Diff { get; set; }
            public string Threshold { get; set; }
            public string LowerLimit { get; set; }
            public string UpperLimit { get; set; }
            public string Reason { get; set; }
            public bool IsMismatch { get; set; }
        }

        private sealed class ItemSampleDiffRow
        {
            public string GroupName { get; set; }
            public string ImageNumber { get; set; }
            public string ReferResult { get; set; }
            public string TestResult { get; set; }
            public string ReferDefectType { get; set; }
            public string TestDefectType { get; set; }
            public string ReferValue { get; set; }
            public string TestValue { get; set; }
            public string Diff { get; set; }
            public string Threshold { get; set; }
            public string LowerLimit { get; set; }
            public string UpperLimit { get; set; }
            public string Reason { get; set; }
            public bool IsMismatch { get; set; }
        }

        private sealed class MatrixCell
        {
            public string Display { get; set; }
            public bool IsMismatch { get; set; }
        }

        private sealed class MatrixRow
        {
            public string Key { get; set; }
            public string GroupName { get; set; }
            public string ImageNumber { get; set; }
            public string Status { get; set; }
            public Dictionary<string, MatrixCell> Cells { get; set; } = new Dictionary<string, MatrixCell>(StringComparer.OrdinalIgnoreCase);

            public bool HasAnyMismatch
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(Status))
                    {
                        return true;
                    }

                    foreach (var cell in Cells.Values)
                    {
                        if (cell != null && cell.IsMismatch)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        private string _templateName;
        private string _referCsvPath;
        private string _testCsvPath;
        private double _toleranceAbs;
        private double _toleranceRatio;
        private string _standardName;
        private Dictionary<string, CicdItemToleranceConfig> _toleranceByItem = new Dictionary<string, CicdItemToleranceConfig>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, CicdItemLimitInfo> _limitMap;

        private readonly List<CicdCsvRow> _referRows = new List<CicdCsvRow>();
        private readonly List<CicdCsvRow> _testRows = new List<CicdCsvRow>();
        private readonly Dictionary<string, CicdCsvRow> _referByKey = new Dictionary<string, CicdCsvRow>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CicdCsvRow> _testByKey = new Dictionary<string, CicdCsvRow>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _allKeys = new List<string>();
        private readonly List<string> _allItemNames = new List<string>();
        private readonly List<MatrixRow> _matrixRows = new List<MatrixRow>();

        private readonly MatrixCellTextConverter _matrixTextConverter = new MatrixCellTextConverter();
        private readonly MatrixCellForegroundConverter _matrixForegroundConverter = new MatrixCellForegroundConverter();
        private readonly MatrixCellBackgroundConverter _matrixBackgroundConverter = new MatrixCellBackgroundConverter();

        public CicdCompareDetailsWindow()
        {
            _isInitializing = true;
            InitializeComponent();
            _isInitializing = false;
        }

        public void LoadComparison(
            string templateName,
            string referCsvPath,
            string testCsvPath,
            CicdAcceptanceCriteriaStandard standard,
            Dictionary<string, CicdItemLimitInfo> limitMap)
        {
            _isInitializing = true;

            _templateName = templateName ?? "";
            _referCsvPath = referCsvPath ?? "";
            _testCsvPath = testCsvPath ?? "";
            _standardName = standard != null ? (standard.Name ?? "") : "";
            _toleranceAbs = standard != null ? standard.DefaultNumericToleranceAbs : 0.0;
            _toleranceRatio = standard != null ? standard.DefaultNumericToleranceRatio : 0.0;
            _toleranceByItem = BuildToleranceMap(standard);
            _limitMap = limitMap ?? new Dictionary<string, CicdItemLimitInfo>(StringComparer.OrdinalIgnoreCase);

            Title = string.IsNullOrWhiteSpace(_standardName)
                ? $"CICD 对比明细 - {_templateName}"
                : $"CICD 对比明细 - {_templateName} ({_standardName})";
            _isPathVisible = false;
            UpdatePathDisplay();

            // 默认显示矩阵模式（窗口复用时也要重置）
            if (MatrixModeRadio != null)
            {
                MatrixModeRadio.IsChecked = true;
            }

            LoadData();
            InitializeSelectors();
            _isInitializing = false;
            ApplyModeAndRefresh();
        }

        private void TogglePathsButton_Click(object sender, RoutedEventArgs e)
        {
            _isPathVisible = !_isPathVisible;
            UpdatePathDisplay();
        }

        private void UpdatePathDisplay()
        {
            if (PathTextBlock != null)
            {
                PathTextBlock.Text = $"基准: {_referCsvPath}\n测试: {_testCsvPath}";
                PathTextBlock.Visibility = _isPathVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            if (TogglePathsButton != null)
            {
                TogglePathsButton.Content = _isPathVisible ? "隐藏路径" : "显示路径";
            }
        }

        private static Dictionary<string, CicdItemToleranceConfig> BuildToleranceMap(CicdAcceptanceCriteriaStandard standard)
        {
            var map = new Dictionary<string, CicdItemToleranceConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var tol in standard?.ItemTolerances ?? new List<CicdItemToleranceConfig>())
            {
                if (tol == null || string.IsNullOrWhiteSpace(tol.ItemName))
                {
                    continue;
                }

                string name = tol.ItemName.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                map[name] = tol;
            }
            return map;
        }

        private void GetToleranceForItem(string itemName, out double toleranceAbs, out double toleranceRatio)
        {
            toleranceAbs = _toleranceAbs;
            toleranceRatio = _toleranceRatio;

            if (string.IsNullOrWhiteSpace(itemName) || _toleranceByItem == null)
            {
                return;
            }

            if (_toleranceByItem.TryGetValue(itemName.Trim(), out var tol) && tol != null)
            {
                toleranceAbs = tol.ToleranceAbs;
                toleranceRatio = tol.ToleranceRatio < 0 ? 0 : tol.ToleranceRatio;
            }
        }

        private void LoadData()
        {
            _referRows.Clear();
            _testRows.Clear();
            _referByKey.Clear();
            _testByKey.Clear();
            _allKeys.Clear();
            _allItemNames.Clear();
            _matrixRows.Clear();

            if (File.Exists(_referCsvPath))
            {
                _referRows.AddRange(ParseCicdCsv(_referCsvPath));
            }

            if (File.Exists(_testCsvPath))
            {
                _testRows.AddRange(ParseCicdCsv(_testCsvPath));
            }

            foreach (var row in _referRows)
            {
                if (row == null)
                {
                    continue;
                }

                string key = row.Key ?? "";
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                _referByKey[key] = row;
            }

            foreach (var row in _testRows)
            {
                if (row == null)
                {
                    continue;
                }

                string key = row.Key ?? "";
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                _testByKey[key] = row;
            }

            _allKeys.AddRange(_referByKey.Keys.Union(_testByKey.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => GetGroupNameFromKey(k), StringComparer.OrdinalIgnoreCase)
                .ThenBy(k => GetImageNumberSortKey(GetImageNumberFromKey(k)))
                .ThenBy(k => k, StringComparer.OrdinalIgnoreCase));

            var itemSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _referRows)
            {
                foreach (var name in row?.Values?.Keys ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string normalized = name.Trim();
                        if (!IgnoredItemNames.Contains(normalized))
                        {
                            itemSet.Add(normalized);
                        }
                    }
                }
            }

            foreach (var row in _testRows)
            {
                foreach (var name in row?.Values?.Keys ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string normalized = name.Trim();
                        if (!IgnoredItemNames.Contains(normalized))
                        {
                            itemSet.Add(normalized);
                        }
                    }
                }
            }

            _allItemNames.AddRange(itemSet.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

            if (SummaryTextBlock != null)
            {
                string standardText = string.IsNullOrWhiteSpace(_standardName) ? "" : $"  标准: {_standardName}";
                int overrideCount = _toleranceByItem != null ? _toleranceByItem.Count : 0;
                SummaryTextBlock.Text = $"样品: {_allKeys.Count}（基准{_referByKey.Count} / 测试{_testByKey.Count}）  项目: {_allItemNames.Count}{standardText}  默认容差: abs={_toleranceAbs}, ratio={_toleranceRatio}  覆盖项: {overrideCount}";
            }

            BuildMatrixRows();
            EnsureMatrixColumns();
        }

        private void InitializeSelectors()
        {
            if (SampleComboBox != null)
            {
                SampleComboBox.ItemsSource = _allKeys.Select(k => new SampleOption
                {
                    Key = k,
                    DisplayName = $"{GetGroupNameFromKey(k)} / {GetImageNumberFromKey(k)}"
                }).ToList();

                if (SampleComboBox.Items.Count > 0)
                {
                    SampleComboBox.SelectedIndex = 0;
                }
            }

            if (ItemComboBox != null)
            {
                ItemComboBox.ItemsSource = _allItemNames.ToList();
                if (ItemComboBox.Items.Count > 0)
                {
                    ItemComboBox.SelectedIndex = 0;
                }
            }
        }

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            ApplyModeAndRefresh();
        }

        private void FilterOption_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            ApplyModeAndRefresh();
        }

        private void SampleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            if (SampleModeRadio.IsChecked == true)
            {
                RefreshSampleView();
            }
        }

        private void ItemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            if (ItemModeRadio.IsChecked == true)
            {
                RefreshItemView();
            }
        }

        private void PrevSampleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || SampleComboBox == null || SampleComboBox.Items.Count <= 0)
            {
                return;
            }

            int idx = SampleComboBox.SelectedIndex;
            if (idx > 0)
            {
                SampleComboBox.SelectedIndex = idx - 1;
            }
        }

        private void NextSampleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || SampleComboBox == null || SampleComboBox.Items.Count <= 0)
            {
                return;
            }

            int idx = SampleComboBox.SelectedIndex;
            if (idx < SampleComboBox.Items.Count - 1)
            {
                SampleComboBox.SelectedIndex = idx + 1;
            }
        }

        private void ApplyModeAndRefresh()
        {
            if (_isInitializing)
            {
                return;
            }

            if (SampleModeRadio == null || ItemModeRadio == null || MatrixModeRadio == null)
            {
                return;
            }

            bool sampleMode = SampleModeRadio.IsChecked == true;
            bool itemMode = ItemModeRadio.IsChecked == true;
            bool matrixMode = MatrixModeRadio.IsChecked == true;

            if (SampleDataGrid != null)
            {
                SampleDataGrid.Visibility = sampleMode ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ItemDataGrid != null)
            {
                ItemDataGrid.Visibility = itemMode ? Visibility.Visible : Visibility.Collapsed;
            }

            if (MatrixDataGrid != null)
            {
                MatrixDataGrid.Visibility = matrixMode ? Visibility.Visible : Visibility.Collapsed;
            }

            if (SampleComboBox != null)
            {
                SampleComboBox.IsEnabled = sampleMode;
            }

            if (ItemComboBox != null)
            {
                ItemComboBox.IsEnabled = itemMode;
            }

            UpdateColumnVisibility();

            if (sampleMode)
            {
                RefreshSampleView();
            }
            else if (itemMode)
            {
                RefreshItemView();
            }
            else if (matrixMode)
            {
                RefreshMatrixView();
            }
        }

        private void UpdateColumnVisibility()
        {
            bool showLimits = ShowLimitsCheckBox != null && ShowLimitsCheckBox.IsChecked == true;
            bool showNgInfo = ShowNgInfoCheckBox != null && ShowNgInfoCheckBox.IsChecked == true;

            if (SampleLowerLimitColumn != null)
            {
                SampleLowerLimitColumn.Visibility = showLimits ? Visibility.Visible : Visibility.Collapsed;
            }

            if (SampleUpperLimitColumn != null)
            {
                SampleUpperLimitColumn.Visibility = showLimits ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ItemLowerLimitColumn != null)
            {
                ItemLowerLimitColumn.Visibility = showLimits ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ItemUpperLimitColumn != null)
            {
                ItemUpperLimitColumn.Visibility = showLimits ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ItemReferResultColumn != null)
            {
                ItemReferResultColumn.Visibility = showNgInfo ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ItemTestResultColumn != null)
            {
                ItemTestResultColumn.Visibility = showNgInfo ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ItemReferDefectColumn != null)
            {
                ItemReferDefectColumn.Visibility = showNgInfo ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ItemTestDefectColumn != null)
            {
                ItemTestDefectColumn.Visibility = showNgInfo ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void BuildMatrixRows()
        {
            foreach (var key in _allKeys)
            {
                _referByKey.TryGetValue(key, out CicdCsvRow refer);
                _testByKey.TryGetValue(key, out CicdCsvRow test);

                var row = new MatrixRow
                {
                    Key = key,
                    GroupName = refer != null ? refer.GroupName : (test != null ? test.GroupName : GetGroupNameFromKey(key)),
                    ImageNumber = refer != null ? refer.ImageNumber : (test != null ? test.ImageNumber : GetImageNumberFromKey(key)),
                    Status = ""
                };

                if (refer == null && test != null)
                {
                    row.Status = "新增";
                }
                else if (refer != null && test == null)
                {
                    row.Status = "缺失";
                }

                foreach (var itemName in _allItemNames)
                {
                    var cell = new MatrixCell { Display = "", IsMismatch = false };

                    if (refer == null || test == null)
                    {
                        // 仅用状态列提示即可
                        row.Cells[itemName] = cell;
                        continue;
                    }

                    string referRaw = refer.Values.TryGetValue(itemName, out string rv) ? rv ?? "" : "";
                    string testRaw = test.Values.TryGetValue(itemName, out string tv) ? tv ?? "" : "";

                    bool referOk = TryParseDouble(referRaw, out double referNum);
                    bool testOk = TryParseDouble(testRaw, out double testNum);

                    if (referOk && testOk)
                    {
                        double delta = testNum - referNum;
                        GetToleranceForItem(itemName, out double absTol, out double ratioTol);
                        double threshold = Math.Max(absTol, Math.Abs(referNum) * ratioTol);
                        cell.Display = FormatMatrixDelta(delta);
                        cell.IsMismatch = Math.Abs(delta) > threshold;
                    }
                    else
                    {
                        // 非数值：若内容不同，用红色标记
                        string a = (referRaw ?? "").Trim();
                        string b = (testRaw ?? "").Trim();
                        if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                        {
                            cell.Display = "!=";
                            cell.IsMismatch = true;
                        }
                    }

                    row.Cells[itemName] = cell;
                }

                _matrixRows.Add(row);
            }
        }

        private void EnsureMatrixColumns()
        {
            if (MatrixDataGrid == null)
            {
                return;
            }

            // 保留前三列（组名/序号/状态），其余动态列重建
            while (MatrixDataGrid.Columns.Count > MatrixFixedColumnCount)
            {
                MatrixDataGrid.Columns.RemoveAt(MatrixDataGrid.Columns.Count - 1);
            }

            foreach (var itemName in _allItemNames)
            {
                var col = CreateMatrixItemColumn(itemName);
                MatrixDataGrid.Columns.Add(col);
            }
        }

        private DataGridTemplateColumn CreateMatrixItemColumn(string itemName)
        {
            string headerDisplay = FormatMatrixHeaderText(itemName);
            var headerText = new TextBlock
            {
                Text = headerDisplay,
                ToolTip = itemName,
                Tag = itemName,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 14,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight
            };

            var col = new DataGridTemplateColumn
            {
                Header = headerText,
                Width = new DataGridLength(70),
                MinWidth = 60
            };

            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.SetValue(TextBlock.PaddingProperty, new Thickness(0));

            var textBinding = new Binding
            {
                Path = new PropertyPath("."),
                Converter = _matrixTextConverter,
                ConverterParameter = itemName
            };
            factory.SetBinding(TextBlock.TextProperty, textBinding);

            var fgBinding = new Binding
            {
                Path = new PropertyPath("."),
                Converter = _matrixForegroundConverter,
                ConverterParameter = itemName
            };
            factory.SetBinding(TextBlock.ForegroundProperty, fgBinding);

            var bgBinding = new Binding
            {
                Path = new PropertyPath("."),
                Converter = _matrixBackgroundConverter,
                ConverterParameter = itemName
            };
            factory.SetBinding(TextBlock.BackgroundProperty, bgBinding);

            col.CellTemplate = new DataTemplate { VisualTree = factory };
            return col;
        }

        private void RefreshMatrixView()
        {
            if (_isInitializing || MatrixDataGrid == null)
            {
                return;
            }

            bool onlyMismatch = OnlyMismatchCheckBox != null && OnlyMismatchCheckBox.IsChecked == true;
            var rows = onlyMismatch ? _matrixRows.Where(r => r != null && r.HasAnyMismatch).ToList() : _matrixRows.ToList();
            MatrixDataGrid.ItemsSource = rows;

            // Matrix模式不使用顶部OK/NG摘要
            if (ReferResultTextBlock != null) ReferResultTextBlock.Text = "--";
            if (TestResultTextBlock != null) TestResultTextBlock.Text = "--";
            if (ReferDefectTextBlock != null) ReferDefectTextBlock.Text = "";
            if (TestDefectTextBlock != null) TestDefectTextBlock.Text = "";
        }

        private void MatrixDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_isInitializing || MatrixDataGrid == null)
                {
                    return;
                }

                var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
                if (cell == null)
                {
                    return;
                }

                if (cell.Column == null || cell.Column.DisplayIndex < MatrixFixedColumnCount)
                {
                    return;
                }

                var row = cell.DataContext as MatrixRow;
                string itemName = GetColumnHeaderText(cell.Column);
                if (row == null || string.IsNullOrWhiteSpace(itemName))
                {
                    return;
                }

                if (string.Equals(itemName, "组名", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemName, "序号", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemName, "状态", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(row.Key))
                {
                    return;
                }

                JumpToSampleDetail(row.Key, itemName);
            }
            catch
            {
                // ignore
            }
        }

        private static string GetColumnHeaderText(DataGridColumn column)
        {
            if (column == null)
            {
                return "";
            }

            var header = column.Header;
            var s = header as string;
            if (!string.IsNullOrWhiteSpace(s))
            {
                return s;
            }

            var textBlock = header as TextBlock;
            if (textBlock != null)
            {
                var tagText = textBlock.Tag as string;
                if (!string.IsNullOrWhiteSpace(tagText))
                {
                    return tagText;
                }

                if (!string.IsNullOrWhiteSpace(textBlock.Text))
                {
                    return textBlock.Text;
                }
            }

            return header != null ? header.ToString() : "";
        }

        private static string FormatMatrixHeaderText(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return "";
            }

            string text = itemName.Trim();
            bool hasCjk = false;
            bool hasLatin = false;
            foreach (char c in text)
            {
                var cls = ClassifyMatrixHeaderChar(c);
                if (cls == 1) hasCjk = true;
                else if (cls == 2) hasLatin = true;
            }

            if (!hasCjk || !hasLatin)
            {
                return text;
            }

            int boundary = -1;
            int prevClass = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int cls = ClassifyMatrixHeaderChar(text[i]);
                if (cls == 0)
                {
                    continue;
                }

                if (prevClass == 0)
                {
                    prevClass = cls;
                    continue;
                }

                if (cls != prevClass)
                {
                    boundary = i;
                    break;
                }

                prevClass = cls;
            }

            if (boundary <= 0 || boundary >= text.Length)
            {
                return text;
            }

            string left = text.Substring(0, boundary).TrimEnd();
            string right = text.Substring(boundary).TrimStart();
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return text;
            }

            return left + "\n" + right;
        }

        // 0=Other, 1=CJK, 2=Latin/Digit
        private static int ClassifyMatrixHeaderChar(char c)
        {
            if ((c >= '\u4e00' && c <= '\u9fff') || (c >= '\u3400' && c <= '\u4dbf'))
            {
                return 1;
            }

            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                return 2;
            }

            return 0;
        }

        private static string FormatMatrixDelta(double delta)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta))
            {
                return "";
            }

            double abs = Math.Abs(delta);
            int digitsBeforeDecimal;
            if (abs < 1.0)
            {
                digitsBeforeDecimal = 1; // "0"
            }
            else
            {
                digitsBeforeDecimal = (int)Math.Floor(Math.Log10(abs)) + 1;
                if (digitsBeforeDecimal < 1)
                {
                    digitsBeforeDecimal = 1;
                }
            }

            // 小数点前后加起来一共4位（不含符号/小数点）
            int decimals = 4 - digitsBeforeDecimal;
            if (decimals < 0)
            {
                decimals = 0;
            }
            else if (decimals > 6)
            {
                decimals = 6;
            }

            string text = delta.ToString("F" + decimals, CultureInfo.InvariantCulture);
            if (decimals > 0)
            {
                text = text.TrimEnd('0').TrimEnd('.');
            }

            return text;
        }

        private void JumpToSampleDetail(string key, string itemName)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (SampleModeRadio == null || SampleComboBox == null || SampleDataGrid == null)
            {
                return;
            }

            _isInitializing = true;
            SampleModeRadio.IsChecked = true;
            _isInitializing = false;

            // 选中样品
            int targetIndex = -1;
            for (int i = 0; i < SampleComboBox.Items.Count; i++)
            {
                var opt = SampleComboBox.Items[i] as SampleOption;
                if (opt != null && string.Equals(opt.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex >= 0)
            {
                SampleComboBox.SelectedIndex = targetIndex;
            }

            ApplyModeAndRefresh();

            if (!string.IsNullOrWhiteSpace(itemName))
            {
                var rows = SampleDataGrid.ItemsSource as IEnumerable<SampleItemDiffRow>;
                if (rows != null)
                {
                    var target = rows.FirstOrDefault(r => r != null && string.Equals(r.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
                    if (target != null)
                    {
                        SampleDataGrid.SelectedItem = target;
                        SampleDataGrid.ScrollIntoView(target);
                    }
                }
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var current = child;
            while (current != null)
            {
                if (current is T t)
                {
                    return t;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private sealed class MatrixCellTextConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var row = value as MatrixRow;
                var itemName = parameter as string;
                if (row == null || string.IsNullOrWhiteSpace(itemName))
                {
                    return "";
                }

                if (row.Cells != null && row.Cells.TryGetValue(itemName, out MatrixCell cell) && cell != null)
                {
                    return cell.Display ?? "";
                }

                return "";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        }

        private sealed class MatrixCellForegroundConverter : IValueConverter
        {
            private static readonly Brush MismatchBrush = new SolidColorBrush(Color.FromRgb(176, 0, 32));

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var row = value as MatrixRow;
                var itemName = parameter as string;
                if (row == null || string.IsNullOrWhiteSpace(itemName))
                {
                    return Brushes.Black;
                }

                if (row.Cells != null && row.Cells.TryGetValue(itemName, out MatrixCell cell) && cell != null && cell.IsMismatch)
                {
                    return MismatchBrush;
                }

                return Brushes.Black;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        }

        private sealed class MatrixCellBackgroundConverter : IValueConverter
        {
            private static readonly Brush MismatchBg = new SolidColorBrush(Color.FromArgb(0x11, 0xB0, 0x00, 0x20));

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var row = value as MatrixRow;
                var itemName = parameter as string;
                if (row == null || string.IsNullOrWhiteSpace(itemName))
                {
                    return Brushes.Transparent;
                }

                if (row.Cells != null && row.Cells.TryGetValue(itemName, out MatrixCell cell) && cell != null && cell.IsMismatch)
                {
                    return MismatchBg;
                }

                return Brushes.Transparent;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        }

        private void RefreshSampleView()
        {
            if (_isInitializing || SampleComboBox == null || SampleDataGrid == null)
            {
                return;
            }

            var opt = SampleComboBox.SelectedItem as SampleOption;
            string key = opt != null ? opt.Key : null;
            if (string.IsNullOrWhiteSpace(key))
            {
                SampleDataGrid.ItemsSource = null;
                return;
            }

            _referByKey.TryGetValue(key, out CicdCsvRow refer);
            _testByKey.TryGetValue(key, out CicdCsvRow test);

            UpdateSampleHeader(refer, test);

            var rows = BuildSampleRows(refer, test);
            bool onlyMismatch = OnlyMismatchCheckBox.IsChecked == true;
            if (onlyMismatch)
            {
                rows = rows.Where(r => r.IsMismatch).ToList();
            }

            SampleDataGrid.ItemsSource = rows;
        }

        private void UpdateSampleHeader(CicdCsvRow refer, CicdCsvRow test)
        {
            if (ReferResultTextBlock == null || TestResultTextBlock == null || ReferDefectTextBlock == null || TestDefectTextBlock == null)
            {
                return;
            }

            bool referOk = refer != null && refer.IsOK;
            bool testOk = test != null && test.IsOK;

            ReferResultTextBlock.Text = refer == null ? "--" : (referOk ? "OK" : "NG");
            TestResultTextBlock.Text = test == null ? "--" : (testOk ? "OK" : "NG");
            ReferDefectTextBlock.Text = refer != null ? (refer.DefectType ?? "") : "";
            TestDefectTextBlock.Text = test != null ? (test.DefectType ?? "") : "";

            bool resultMismatch = refer != null && test != null && referOk != testOk;
            bool defectMismatch = refer != null && test != null && !referOk && !testOk &&
                                  !string.Equals(refer.DefectType ?? "", test.DefectType ?? "", StringComparison.OrdinalIgnoreCase);

            ReferResultTextBlock.Foreground = resultMismatch ? new SolidColorBrush(Color.FromRgb(176, 0, 32)) : Brushes.Black;
            TestResultTextBlock.Foreground = resultMismatch ? new SolidColorBrush(Color.FromRgb(176, 0, 32)) : Brushes.Black;
            ReferDefectTextBlock.Foreground = defectMismatch ? new SolidColorBrush(Color.FromRgb(176, 0, 32)) : Brushes.Black;
            TestDefectTextBlock.Foreground = defectMismatch ? new SolidColorBrush(Color.FromRgb(176, 0, 32)) : Brushes.Black;
        }

        private List<SampleItemDiffRow> BuildSampleRows(CicdCsvRow refer, CicdCsvRow test)
        {
            var list = new List<SampleItemDiffRow>();
            bool referExists = refer != null;
            bool testExists = test != null;

            if (!referExists || !testExists)
            {
                list.Add(new SampleItemDiffRow
                {
                    ItemName = "样品存在性",
                    ReferValue = referExists ? "存在" : "缺失",
                    TestValue = testExists ? "存在" : "缺失",
                    Reason = referExists && !testExists ? "测试缺失该样品" : (!referExists && testExists ? "测试新增该样品" : "样品缺失"),
                    IsMismatch = true
                });
            }

            // 结果/分类作为对比“项目”
            if (refer != null || test != null)
            {
                string referResult = refer == null ? "" : (refer.IsOK ? "OK" : "NG");
                string testResult = test == null ? "" : (test.IsOK ? "OK" : "NG");
                bool mismatch = refer != null && test != null && refer.IsOK != test.IsOK;
                list.Add(new SampleItemDiffRow
                {
                    ItemName = "结果(OK/NG)",
                    ReferValue = referResult,
                    TestValue = testResult,
                    Reason = mismatch ? "OK/NG不一致" : "",
                    IsMismatch = mismatch
                });

                string referDefect = refer != null ? (refer.DefectType ?? "") : "";
                string testDefect = test != null ? (test.DefectType ?? "") : "";
                bool defectMismatch = refer != null && test != null && !refer.IsOK && !test.IsOK &&
                                      !string.Equals(referDefect, testDefect, StringComparison.OrdinalIgnoreCase);
                list.Add(new SampleItemDiffRow
                {
                    ItemName = "缺陷类型",
                    ReferValue = referDefect,
                    TestValue = testDefect,
                    Reason = defectMismatch ? "分类不一致" : "",
                    IsMismatch = defectMismatch
                });
            }

            foreach (var itemName in _allItemNames)
            {
                string referRaw = refer != null && refer.Values.TryGetValue(itemName, out string rv) ? rv ?? "" : "";
                string testRaw = test != null && test.Values.TryGetValue(itemName, out string tv) ? tv ?? "" : "";

                var row = CompareValue(itemName, referRaw, testRaw);
                list.Add(row);
            }

            return list;
        }

        private SampleItemDiffRow CompareValue(string itemName, string referRaw, string testRaw)
        {
            var row = new SampleItemDiffRow
            {
                ItemName = itemName,
                ReferValue = referRaw ?? "",
                TestValue = testRaw ?? ""
            };

            if (_limitMap != null && _limitMap.TryGetValue(itemName, out CicdItemLimitInfo limitInfo))
            {
                row.LowerLimit = limitInfo.LowerLimit ?? "";
                row.UpperLimit = limitInfo.UpperLimit ?? "";
            }

            bool referOk = TryParseDouble(referRaw, out double referVal);
            bool testOk = TryParseDouble(testRaw, out double testVal);

            if (!referOk && !testOk)
            {
                if (!string.Equals((referRaw ?? "").Trim(), (testRaw ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    row.IsMismatch = true;
                    row.Reason = "值不一致（非数值）";
                }
                return row;
            }

            if (referOk && testOk)
            {
                double diff = Math.Abs(testVal - referVal);
                GetToleranceForItem(itemName, out double absTol, out double ratioTol);
                double threshold = Math.Max(absTol, Math.Abs(referVal) * ratioTol);
                row.Diff = diff.ToString("F4", CultureInfo.InvariantCulture);
                row.Threshold = threshold.ToString("F4", CultureInfo.InvariantCulture);
                if (diff > threshold)
                {
                    row.IsMismatch = true;
                    row.Reason = "超出容差";
                }

                return row;
            }

            row.IsMismatch = true;
            row.Reason = referOk ? "测试值非数值/缺失" : "基准值非数值/缺失";
            return row;
        }

        private void RefreshItemView()
        {
            if (_isInitializing || ItemComboBox == null || ItemDataGrid == null)
            {
                return;
            }

            string itemName = ItemComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(itemName))
            {
                ItemDataGrid.ItemsSource = null;
                return;
            }

            var rows = new List<ItemSampleDiffRow>();
            foreach (var key in _allKeys)
            {
                _referByKey.TryGetValue(key, out CicdCsvRow refer);
                _testByKey.TryGetValue(key, out CicdCsvRow test);

                string groupName = refer != null ? refer.GroupName : (test != null ? test.GroupName : GetGroupNameFromKey(key));
                string imageNumber = refer != null ? refer.ImageNumber : (test != null ? test.ImageNumber : GetImageNumberFromKey(key));

                string referVal = refer != null && refer.Values.TryGetValue(itemName, out string rv) ? rv ?? "" : "";
                string testVal = test != null && test.Values.TryGetValue(itemName, out string tv) ? tv ?? "" : "";

                var row = new ItemSampleDiffRow
                {
                    GroupName = groupName ?? "",
                    ImageNumber = imageNumber ?? "",
                    ReferResult = refer == null ? "--" : (refer.IsOK ? "OK" : "NG"),
                    TestResult = test == null ? "--" : (test.IsOK ? "OK" : "NG"),
                    ReferDefectType = refer != null ? (refer.DefectType ?? "") : "",
                    TestDefectType = test != null ? (test.DefectType ?? "") : "",
                    ReferValue = referVal,
                    TestValue = testVal
                };

                if (_limitMap != null && _limitMap.TryGetValue(itemName, out CicdItemLimitInfo limitInfo))
                {
                    row.LowerLimit = limitInfo.LowerLimit ?? "";
                    row.UpperLimit = limitInfo.UpperLimit ?? "";
                }

                // 样品存在性差异
                if (refer == null || test == null)
                {
                    row.IsMismatch = true;
                    row.Reason = refer != null && test == null ? "测试缺失该样品" : "测试新增该样品";
                    rows.Add(row);
                    continue;
                }

                // OK/NG差异
                if (refer.IsOK != test.IsOK)
                {
                    row.IsMismatch = true;
                    row.Reason = "OK/NG不一致";
                }

                // 分类差异（双方均NG时）
                if (!refer.IsOK && !test.IsOK &&
                    !string.Equals(refer.DefectType ?? "", test.DefectType ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    row.IsMismatch = true;
                    row.Reason = string.IsNullOrWhiteSpace(row.Reason) ? "分类不一致" : $"{row.Reason}; 分类不一致";
                }

                bool referOk = TryParseDouble(referVal, out double referNum);
                bool testOk = TryParseDouble(testVal, out double testNum);
                if (referOk && testOk)
                {
                    double diff = Math.Abs(testNum - referNum);
                    GetToleranceForItem(itemName, out double absTol, out double ratioTol);
                    double threshold = Math.Max(absTol, Math.Abs(referNum) * ratioTol);
                    row.Diff = diff.ToString("F4", CultureInfo.InvariantCulture);
                    row.Threshold = threshold.ToString("F4", CultureInfo.InvariantCulture);
                    if (diff > threshold)
                    {
                        row.IsMismatch = true;
                        row.Reason = string.IsNullOrWhiteSpace(row.Reason) ? "超出容差" : $"{row.Reason}; 超出容差";
                    }
                }
                else
                {
                    if (!string.Equals((referVal ?? "").Trim(), (testVal ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        row.IsMismatch = true;
                        row.Reason = string.IsNullOrWhiteSpace(row.Reason) ? "值不一致（非数值）" : $"{row.Reason}; 值不一致（非数值）";
                    }
                }

                rows.Add(row);
            }

            bool onlyMismatch = OnlyMismatchCheckBox.IsChecked == true;
            if (onlyMismatch)
            {
                rows = rows.Where(r => r.IsMismatch).ToList();
            }

            ItemDataGrid.ItemsSource = rows;

            // 项目视图的头部显示为当前选择项的总体信息
            ReferResultTextBlock.Text = "--";
            TestResultTextBlock.Text = "--";
            ReferDefectTextBlock.Text = "";
            TestDefectTextBlock.Text = "";
            ReferResultTextBlock.Foreground = Brushes.Black;
            TestResultTextBlock.Foreground = Brushes.Black;
            ReferDefectTextBlock.Foreground = Brushes.Black;
            TestDefectTextBlock.Foreground = Brushes.Black;
        }

        private static string GetGroupNameFromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "";
            }

            int idx = key.IndexOf('#');
            return idx >= 0 ? key.Substring(0, idx) : key;
        }

        private static string GetImageNumberFromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "";
            }

            int idx = key.IndexOf('#');
            return idx >= 0 && idx + 1 < key.Length ? key.Substring(idx + 1) : "";
        }

        private static int GetImageNumberSortKey(string imageNumber)
        {
            if (string.IsNullOrWhiteSpace(imageNumber))
            {
                return int.MinValue;
            }

            if (int.TryParse(imageNumber, out int n))
            {
                return n;
            }

            var digits = new string(imageNumber.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out n))
            {
                return n;
            }

            return int.MinValue;
        }

        private List<CicdCsvRow> ParseCicdCsv(string csvPath)
        {
            var rows = new List<CicdCsvRow>();
            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return rows;
            }

            var header = SplitCsvLine(lines[0]).Select(h => h != null ? h.Trim() : "").ToList();
            int idxGroup = header.FindIndex(h => string.Equals(h, "组名", StringComparison.OrdinalIgnoreCase));
            int idxNumber = header.FindIndex(h => string.Equals(h, "序号", StringComparison.OrdinalIgnoreCase));
            int idxResult = header.FindIndex(h => string.Equals(h, "结果", StringComparison.OrdinalIgnoreCase));
            int idxType = header.FindIndex(h => string.Equals(h, "缺陷类型", StringComparison.OrdinalIgnoreCase));
            int idxTimestamp = header.FindIndex(h => string.Equals(h, "时间戳", StringComparison.OrdinalIgnoreCase));

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                var parts = SplitCsvLine(lines[i]);
                var row = new CicdCsvRow
                {
                    GroupName = idxGroup >= 0 && idxGroup < parts.Count ? parts[idxGroup] : "",
                    ImageNumber = idxNumber >= 0 && idxNumber < parts.Count ? parts[idxNumber] : "",
                    IsOK = idxResult >= 0 && idxResult < parts.Count && string.Equals(parts[idxResult], "OK", StringComparison.OrdinalIgnoreCase),
                    DefectType = idxType >= 0 && idxType < parts.Count ? parts[idxType] : ""
                };

                for (int c = 0; c < header.Count && c < parts.Count; c++)
                {
                    string col = header[c];
                    if (string.IsNullOrWhiteSpace(col))
                    {
                        continue;
                    }

                    if (c == idxGroup || c == idxNumber || c == idxResult || c == idxType)
                    {
                        continue;
                    }

                    if (c == idxTimestamp)
                    {
                        continue;
                    }

                    if (IgnoredItemNames.Contains(col))
                    {
                        continue;
                    }

                    row.Values[col] = parts[c];
                }

                rows.Add(row);
            }

            return rows;
        }

        private List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null)
            {
                return result;
            }

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(ch);
            }

            result.Add(sb.ToString());
            return result;
        }

        private bool TryParseDouble(string raw, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            raw = raw.Trim();
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}
