using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class CicdAcceptanceCriteriaWindow : Window
    {
        public sealed class CicdItemToleranceRow
        {
            public string ItemName { get; set; }
            public double ToleranceAbs { get; set; }
            public double ToleranceRatio { get; set; }
        }

        private bool _isLoading;
        private CicdAcceptanceCriteriaConfig _config;
        private JToken _originalSnapshot;

        private CicdTemplateCriteriaConfig _currentTemplateConfig;
        private string _currentTemplateName;
        private CicdAcceptanceCriteriaStandard _currentStandard;
        private readonly string _fixedTemplateName;

        public ObservableCollection<CicdAcceptanceCriteriaStandard> Standards { get; } = new ObservableCollection<CicdAcceptanceCriteriaStandard>();
        public ObservableCollection<CicdItemToleranceConfig> ItemTolerances { get; } = new ObservableCollection<CicdItemToleranceConfig>();
        public ObservableCollection<CicdItemToleranceRow> ItemToleranceRows { get; } = new ObservableCollection<CicdItemToleranceRow>();
        public ObservableCollection<string> AvailableItemNames { get; } = new ObservableCollection<string>();

        public CicdAcceptanceCriteriaWindow()
            : this(null)
        {
        }

        public CicdAcceptanceCriteriaWindow(string fixedTemplateName)
        {
            _fixedTemplateName = fixedTemplateName;

            InitializeComponent();
            DataContext = this;
            Closing += CicdAcceptanceCriteriaWindow_Closing;

            LoadConfigToUi();
        }

        private void LoadConfigToUi()
        {
            _isLoading = true;
            try
            {
                _config = CicdAcceptanceCriteriaConfigManager.Load();
                _originalSnapshot = JToken.FromObject(_config ?? new CicdAcceptanceCriteriaConfig());

                LoadGlobalStandards();
                LoadTemplateList();

                if (!string.IsNullOrWhiteSpace(_fixedTemplateName))
                {
                    SelectTemplateByName(_fixedTemplateName);
                    if (TemplateComboBox != null)
                    {
                        TemplateComboBox.IsEnabled = false;
                    }
                }
                else
                {
                    SelectTemplateByName(Page1.PageManager.Page1Instance?.CurrentTemplateName ?? string.Empty);
                }

                LoadTemplateCriteria();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void LoadGlobalStandards()
        {
            Standards.Clear();
            foreach (var standard in _config?.Standards ?? new List<CicdAcceptanceCriteriaStandard>())
            {
                if (standard != null)
                {
                    Standards.Add(standard);
                }
            }
        }

        private void LoadTemplateList()
        {
            TemplateComboBox.ItemsSource = GetTemplateNames();
        }

        private List<string> GetTemplateNames()
        {
            try
            {
                string templateRoot = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
                if (!System.IO.Directory.Exists(templateRoot))
                {
                    return new List<string>();
                }

                return System.IO.Directory.GetDirectories(templateRoot)
                    .Select(System.IO.Path.GetFileName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void SelectTemplateByName(string templateName)
        {
            var templates = TemplateComboBox.ItemsSource as IEnumerable<string>;
            var selected = templates?.FirstOrDefault(t => string.Equals(t, templateName, StringComparison.OrdinalIgnoreCase))
                           ?? templates?.FirstOrDefault();
            TemplateComboBox.SelectedItem = selected;
        }

        private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            CommitCurrentStandardEdits();
            LoadTemplateCriteria();
        }

        private void LoadTemplateCriteria()
        {
            _isLoading = true;
            try
            {
                _currentTemplateName = TemplateComboBox.SelectedItem as string ?? string.Empty;
                _currentTemplateConfig = CicdAcceptanceCriteriaConfigManager.GetOrCreateTemplateConfig(_config, _currentTemplateName);

                ActiveStandardComboBox.ItemsSource = Standards;

                string boundName = _currentTemplateConfig.BoundStandardName ?? "默认标准";
                var selected = Standards.FirstOrDefault(s => s != null && string.Equals(s.Name, boundName, StringComparison.OrdinalIgnoreCase))
                               ?? Standards.FirstOrDefault(s => s != null && string.Equals(s.Name, "默认标准", StringComparison.OrdinalIgnoreCase))
                               ?? Standards.FirstOrDefault();

                RefreshAvailableItemNames(_currentTemplateName, selected);
                ActiveStandardComboBox.SelectedItem = selected;
                StandardsDataGrid.SelectedItem = selected;
                SetCurrentStandard(selected);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void RefreshAvailableItemNames(string templateName, CicdAcceptanceCriteriaStandard standard)
        {
            AvailableItemNames.Clear();

            LogManager.Info($"[CICD][Criteria] RefreshAvailableItemNames template={templateName}, baseDir={AppDomain.CurrentDomain.BaseDirectory}", "CicdAcceptanceCriteriaWindow");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in GetItemNamesFromCicdReferCsvs(templateName))
            {
                names.Add(name);
            }

            foreach (var name in GetItemNamesFromCurrentDetectionSnapshot())
            {
                names.Add(name);
            }

            foreach (var name in standard?.ItemTolerances?.Select(t => t?.ItemName).Where(n => !string.IsNullOrWhiteSpace(n)) ?? Enumerable.Empty<string>())
            {
                names.Add(name.Trim());
            }

            foreach (var name in names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                AvailableItemNames.Add(name);
            }

            LogManager.Info($"[CICD][Criteria] AvailableItemNames count={AvailableItemNames.Count}", "CicdAcceptanceCriteriaWindow");
        }

        private IEnumerable<string> GetItemNamesFromCicdReferCsvs(string templateName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templateName))
                {
                    return Enumerable.Empty<string>();
                }

                string templatesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
                if (!Directory.Exists(templatesRoot))
                {
                    templatesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                }

                string cicdRoot = Path.Combine(templatesRoot, templateName, "CICD检测");
                if (!Directory.Exists(cicdRoot))
                {
                    LogManager.Warning($"[CICD][Criteria] CICD root not found: {cicdRoot}", "CicdAcceptanceCriteriaWindow");
                    return Enumerable.Empty<string>();
                }

                var allFiles = Directory.GetFiles(cicdRoot, "cicd_refer.csv", SearchOption.AllDirectories)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                if (allFiles.Count == 0)
                {
                    LogManager.Warning($"[CICD][Criteria] No cicd_refer.csv found under: {cicdRoot}", "CicdAcceptanceCriteriaWindow");
                    return Enumerable.Empty<string>();
                }

                var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "组名",
                    "序号",
                    "时间戳",
                    "结果",
                    "缺陷类型"
                };

                var nonTestFiles = allFiles
                    .Where(p => p.IndexOf(Path.DirectorySeparatorChar + "测试" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                    .ToList();
                var files = nonTestFiles.Count > 0 ? nonTestFiles : allFiles;

                LogManager.Info($"[CICD][Criteria] Found cicd_refer.csv count={allFiles.Count}, example={allFiles[0]}", "CicdAcceptanceCriteriaWindow");

                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files.OrderByDescending(p => File.GetLastWriteTimeUtc(p)))
                {
                    var headerLine = ReadFirstLineSmart(file);
                    if (string.IsNullOrWhiteSpace(headerLine))
                    {
                        LogManager.Warning($"[CICD][Criteria] Empty header in: {file}", "CicdAcceptanceCriteriaWindow");
                        continue;
                    }

                    var header = SplitCsvLine(headerLine).Select(h => h != null ? h.Trim() : "").ToList();
                    if (header.Count <= ignore.Count)
                    {
                        string preview = headerLine.Length > 160 ? headerLine.Substring(0, 160) + "..." : headerLine;
                        LogManager.Warning($"[CICD][Criteria] Header parsed but too short ({header.Count} cols): {preview}", "CicdAcceptanceCriteriaWindow");
                    }
                    foreach (var col in header)
                    {
                        if (string.IsNullOrWhiteSpace(col))
                        {
                            continue;
                        }

                        if (ignore.Contains(col))
                        {
                            continue;
                        }

                        result.Add(col.Trim());
                    }
                }

                return result.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static string ReadFirstLineSmart(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                // 1) 严格UTF8（可捕获非UTF8字节）
                try
                {
                    using (var reader = new StreamReader(path, new UTF8Encoding(false, true), true))
                    {
                        return reader.ReadLine();
                    }
                }
                catch
                {
                    // ignore
                }

                // 2) GB18030（现场CSV常见编码）
                try
                {
                    using (var reader = new StreamReader(path, Encoding.GetEncoding("GB18030"), true))
                    {
                        return reader.ReadLine();
                    }
                }
                catch
                {
                    // ignore
                }

                // 3) 最后兜底
                using (var reader = new StreamReader(path, Encoding.Default, true))
                {
                    return reader.ReadLine();
                }
            }
            catch
            {
                return null;
            }
        }

        private void UpdateItemToleranceColumnItemsSource()
        {
            try
            {
                if (ItemToleranceDataGrid == null || ItemToleranceDataGrid.Columns == null || ItemToleranceDataGrid.Columns.Count == 0)
                {
                    LogManager.Warning("[CICD][Criteria] ItemToleranceDataGrid not ready when updating column ItemsSource", "CicdAcceptanceCriteriaWindow");
                    return;
                }

                DataGridComboBoxColumn col = null;
                foreach (var c in ItemToleranceDataGrid.Columns)
                {
                    var combo = c as DataGridComboBoxColumn;
                    if (combo != null && string.Equals(combo.Header as string ?? "", "检测项", StringComparison.OrdinalIgnoreCase))
                    {
                        col = combo;
                        break;
                    }
                }

                if (col == null)
                {
                    col = ItemToleranceDataGrid.Columns[0] as DataGridComboBoxColumn;
                }

                if (col != null)
                {
                    col.ItemsSource = AvailableItemNames;
                    LogManager.Info($"[CICD][Criteria] Set ItemTolerance combo ItemsSource, count={AvailableItemNames.Count}", "CicdAcceptanceCriteriaWindow");
                }
            }
            catch
            {
                // ignore
            }
        }

        private IEnumerable<string> GetItemNamesFromCurrentDetectionSnapshot()
        {
            try
            {
                var page = Page1.PageManager.Page1Instance;
                if (page == null)
                {
                    return Enumerable.Empty<string>();
                }

                var snapshot = page.GetAllDetectionItemsSnapshot();
                return snapshot
                    .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                    .Select(i => i.Name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static List<string> SplitCsvLine(string line)
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

        private void ActiveStandardComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            if (_currentTemplateConfig == null)
            {
                return;
            }

            if (ActiveStandardComboBox.SelectedItem is CicdAcceptanceCriteriaStandard selected)
            {
                CommitCurrentStandardEdits();
                _currentTemplateConfig.BoundStandardName = selected.Name ?? "默认标准";
                StandardsDataGrid.SelectedItem = selected;
                SetCurrentStandard(selected);
            }
        }

        private void StandardsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            var selected = StandardsDataGrid.SelectedItem as CicdAcceptanceCriteriaStandard;
            if (selected == null)
            {
                return;
            }

            _isLoading = true;
            try
            {
                if (ActiveStandardComboBox != null && !ReferenceEquals(ActiveStandardComboBox.SelectedItem, selected))
                {
                    ActiveStandardComboBox.SelectedItem = selected;
                }
            }
            finally
            {
                _isLoading = false;
            }

            if (_currentTemplateConfig != null)
            {
                CommitCurrentStandardEdits();
                _currentTemplateConfig.BoundStandardName = selected.Name ?? "默认标准";
                SetCurrentStandard(selected);
            }
        }

        private void SetCurrentStandard(CicdAcceptanceCriteriaStandard standard)
        {
            _currentStandard = standard;
            LoadItemTolerancesFromStandard(_currentStandard);
            if (!_isLoading)
            {
                RefreshAvailableItemNames(_currentTemplateName ?? string.Empty, _currentStandard);
            }
            BuildItemToleranceRows();
        }

        private void LoadItemTolerancesFromStandard(CicdAcceptanceCriteriaStandard standard)
        {
            ItemTolerances.Clear();
            if (standard?.ItemTolerances == null)
            {
                return;
            }

            foreach (var item in standard.ItemTolerances)
            {
                if (item != null)
                {
                    ItemTolerances.Add(item);
                }
            }
        }

        private void CommitCurrentStandardEdits()
        {
            if (_currentStandard == null)
            {
                return;
            }

            if (ItemToleranceRows != null && ItemToleranceRows.Count > 0)
            {
                _currentStandard.ItemTolerances = ItemToleranceRows
                    .Where(r => r != null && !string.IsNullOrWhiteSpace(r.ItemName))
                    .Select(r => new CicdItemToleranceConfig
                    {
                        ItemName = r.ItemName.Trim(),
                        ToleranceAbs = r.ToleranceAbs,
                        ToleranceRatio = r.ToleranceRatio < 0 ? 0 : r.ToleranceRatio
                    })
                    .GroupBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .OrderBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return;
            }

            _currentStandard.ItemTolerances = ItemTolerances
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.ItemName))
                .Select(i =>
                {
                    i.ItemName = i.ItemName.Trim();
                    return i;
                })
                .GroupBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void BuildItemToleranceRows()
        {
            ItemToleranceRows.Clear();
            if (_currentStandard == null)
            {
                return;
            }

            var map = new Dictionary<string, CicdItemToleranceConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var tol in _currentStandard.ItemTolerances ?? new List<CicdItemToleranceConfig>())
            {
                if (tol == null || string.IsNullOrWhiteSpace(tol.ItemName))
                {
                    continue;
                }
                map[tol.ItemName.Trim()] = tol;
            }

            double defaultAbs = _currentStandard.DefaultNumericToleranceAbs;
            double defaultRatio = _currentStandard.DefaultNumericToleranceRatio;

            foreach (var name in AvailableItemNames.ToList())
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string itemName = name.Trim();
                var row = new CicdItemToleranceRow { ItemName = itemName };
                if (map.TryGetValue(itemName, out var tol) && tol != null)
                {
                    row.ToleranceAbs = tol.ToleranceAbs;
                    row.ToleranceRatio = tol.ToleranceRatio;
                }
                else
                {
                    row.ToleranceAbs = defaultAbs;
                    row.ToleranceRatio = defaultRatio;
                }

                ItemToleranceRows.Add(row);
            }
        }

        private bool IsDirty()
        {
            var currentSnapshot = JToken.FromObject(_config ?? new CicdAcceptanceCriteriaConfig());
            return !JToken.DeepEquals(_originalSnapshot, currentSnapshot);
        }

        private string PromptForText(string title, string message, string defaultValue)
        {
            var window = new Window
            {
                Title = title,
                Width = 420,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var text = new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(text, 0);
            root.Children.Add(text);

            var textBox = new TextBox { Text = defaultValue ?? string.Empty, Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(textBox, 1);
            root.Children.Add(textBox);

            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            string result = null;

            var okButton = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            okButton.Click += (s, e) =>
            {
                result = textBox.Text;
                window.DialogResult = true;
                window.Close();
            };

            var cancelButton = new Button { Content = "取消", Width = 80, IsCancel = true };
            cancelButton.Click += (s, e) =>
            {
                result = null;
                window.DialogResult = false;
                window.Close();
            };

            panel.Children.Add(okButton);
            panel.Children.Add(cancelButton);
            Grid.SetRow(panel, 2);
            root.Children.Add(panel);

            window.Content = root;
            window.ShowDialog();
            return result;
        }

        private string EnsureUniqueStandardName(string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "标准";
            }

            string name = baseName;
            int i = 1;
            while (Standards.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"{baseName}_{i++}";
            }
            return name;
        }

        private void NewStandardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplateConfig == null)
            {
                MessageBox.Show("请先选择一个模板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var name = PromptForText("新建标准", "请输入标准名称：", "新标准");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            name = EnsureUniqueStandardName(name.Trim());
            var standard = new CicdAcceptanceCriteriaStandard { Name = name };
            if (_config != null)
            {
                _config.Standards.Add(standard);
            }
            Standards.Add(standard);

            ActiveStandardComboBox.SelectedItem = standard;
        }

        private void CopyStandardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplateConfig == null)
            {
                MessageBox.Show("请先选择一个模板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!(StandardsDataGrid.SelectedItem is CicdAcceptanceCriteriaStandard selected))
            {
                return;
            }

            string name = EnsureUniqueStandardName($"{selected.Name}_复制");
            var copied = new CicdAcceptanceCriteriaStandard
            {
                Name = name,
                AllowedOkNgMismatchCount = selected.AllowedOkNgMismatchCount,
                AllowedDefectTypeMismatchCount = selected.AllowedDefectTypeMismatchCount,
                AllowedNumericRangeMismatchCount = selected.AllowedNumericRangeMismatchCount,
                NumericRangeToleranceAbs = selected.NumericRangeToleranceAbs,
                NumericRangeToleranceRatio = selected.NumericRangeToleranceRatio,
                ItemTolerances = selected.ItemTolerances != null ? selected.ItemTolerances.ToList() : new List<CicdItemToleranceConfig>()
            };

            if (_config != null)
            {
                _config.Standards.Add(copied);
            }
            Standards.Add(copied);
            ActiveStandardComboBox.SelectedItem = copied;
        }

        private void RenameStandardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplateConfig == null)
            {
                MessageBox.Show("请先选择一个模板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!(StandardsDataGrid.SelectedItem is CicdAcceptanceCriteriaStandard selected))
            {
                return;
            }

            if (string.Equals(selected.Name, "默认标准", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("默认标准不能重命名", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var name = PromptForText("重命名标准", "请输入新的标准名称：", selected.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            name = name.Trim();
            if (!string.Equals(name, selected.Name, StringComparison.OrdinalIgnoreCase))
            {
                name = EnsureUniqueStandardName(name);
            }

            string oldName = selected.Name;
            selected.Name = name;

            foreach (var template in _config?.Templates ?? new List<CicdTemplateCriteriaConfig>())
            {
                if (template != null && string.Equals(template.BoundStandardName, oldName, StringComparison.OrdinalIgnoreCase))
                {
                    template.BoundStandardName = name;
                }
            }

            ActiveStandardComboBox.ItemsSource = null;
            ActiveStandardComboBox.ItemsSource = Standards;
            ActiveStandardComboBox.SelectedItem = Standards.FirstOrDefault(s => string.Equals(s.Name, _currentTemplateConfig.BoundStandardName, StringComparison.OrdinalIgnoreCase))
                                                  ?? Standards.FirstOrDefault();
        }

        private void DeleteStandardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplateConfig == null)
            {
                MessageBox.Show("请先选择一个模板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!(StandardsDataGrid.SelectedItem is CicdAcceptanceCriteriaStandard selected))
            {
                return;
            }

            if (Standards.Count <= 1)
            {
                MessageBox.Show("至少需要保留一个标准", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.Equals(selected.Name, "默认标准", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("默认标准不能删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"确定删除标准：{selected.Name}？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            CommitCurrentStandardEdits();
            Standards.Remove(selected);
            _config?.Standards?.Remove(selected);

            foreach (var template in _config?.Templates ?? new List<CicdTemplateCriteriaConfig>())
            {
                if (template != null && string.Equals(template.BoundStandardName, selected.Name, StringComparison.OrdinalIgnoreCase))
                {
                    template.BoundStandardName = "默认标准";
                }
            }

            var fallback = Standards.FirstOrDefault(s => string.Equals(s.Name, _currentTemplateConfig.BoundStandardName, StringComparison.OrdinalIgnoreCase))
                           ?? Standards.FirstOrDefault(s => string.Equals(s.Name, "默认标准", StringComparison.OrdinalIgnoreCase))
                           ?? Standards.FirstOrDefault();
            ActiveStandardComboBox.SelectedItem = fallback;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            CommitCurrentStandardEdits();
            if (CicdAcceptanceCriteriaConfigManager.TrySave(_config, out var savedPath, out var errorMessage))
            {
                _originalSnapshot = JToken.FromObject(_config ?? new CicdAcceptanceCriteriaConfig());
                MessageBox.Show($"已保存到：{savedPath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show($"保存失败：{errorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CommitCurrentStandardEdits();
            if (IsDirty())
            {
                var result = MessageBox.Show("配置已修改，是否保存？", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel)
                {
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    if (!CicdAcceptanceCriteriaConfigManager.TrySave(_config, out _, out var error))
                    {
                        MessageBox.Show($"保存失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }

            Close();
        }

        private void CicdAcceptanceCriteriaWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            CommitCurrentStandardEdits();
            if (!IsDirty())
            {
                return;
            }

            var result = MessageBox.Show("配置已修改，是否保存？", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                if (!CicdAcceptanceCriteriaConfigManager.TrySave(_config, out _, out var error))
                {
                    MessageBox.Show($"保存失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                }
            }
        }
    }
}
