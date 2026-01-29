using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using WpfApp2.UI.Models;

namespace WpfApp2.UI
{
    public partial class RealTimeDataExportConfigWindow : Window
    {
        private bool _isLoading;
        private RealTimeDataExportConfig _config;
        private JToken _originalConfigSnapshot;

        public ObservableCollection<AvailableItemRow> AvailableItems { get; } = new ObservableCollection<AvailableItemRow>();
        public ObservableCollection<TemplateColumnRow> TemplateColumns { get; } = new ObservableCollection<TemplateColumnRow>();
        public ObservableCollection<RealTimeDataExportTemplate> Templates { get; } = new ObservableCollection<RealTimeDataExportTemplate>();

        public RealTimeDataExportConfigWindow()
        {
            InitializeComponent();
            DataContext = this;
            Closing += RealTimeDataExportConfigWindow_Closing;

            LoadConfigToUi();
            RefreshAvailableItems();
            UpdateUiEnabledState();
        }

        private void LoadConfigToUi()
        {
            _isLoading = true;
            try
            {
                _config = RealTimeDataExportConfigManager.Load();

                Templates.Clear();
                foreach (var template in _config.Templates ?? new List<RealTimeDataExportTemplate>())
                {
                    Templates.Add(template);
                }

                TemplateComboBox.ItemsSource = Templates;

                if (_config.Mode == RealTimeDataExportMode.Custom)
                {
                    CustomModeRadio.IsChecked = true;
                }
                else
                {
                    DefaultModeRadio.IsChecked = true;
                }

                var selected = Templates.FirstOrDefault(t => string.Equals(t.Name, _config.ActiveTemplateName, StringComparison.OrdinalIgnoreCase))
                               ?? Templates.FirstOrDefault();
                TemplateComboBox.SelectedItem = selected;

                RebuildTemplateColumns();
                _originalConfigSnapshot = JToken.FromObject(_config ?? RealTimeDataExportConfigManager.CreateDefault());
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void RefreshAvailableItems()
        {
            AvailableItems.Clear();
            foreach (var item in GetAvailableDetectionItems().OrderBy(i => i.Name))
            {
                AvailableItems.Add(item);
            }
        }

        private List<AvailableItemRow> GetAvailableDetectionItems()
        {
            try
            {
                var page1 = Page1.PageManager.Page1Instance;
                if (page1 != null)
                {
                    var snapshot = page1.GetAllDetectionItemsSnapshot();
                    if (snapshot != null && snapshot.Count > 0)
                    {
                        return snapshot
                            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                            .Select(i => new AvailableItemRow
                            {
                                Name = i.Name,
                                UpperLimit = i.UpperLimit ?? string.Empty,
                                LowerLimit = i.LowerLimit ?? string.Empty
                            })
                            .ToList();
                    }
                }
            }
            catch
            {
                // ignore
            }

            var latest = DetectionDataStorage.GetRecentRecords(1).FirstOrDefault();
            if (latest?.DetectionItems != null && latest.DetectionItems.Count > 0)
            {
                return latest.DetectionItems
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                    .Select(kvp => new AvailableItemRow
                    {
                        Name = kvp.Key,
                        UpperLimit = kvp.Value != null ? kvp.Value.UpperLimit.ToString("F4") : string.Empty,
                        LowerLimit = kvp.Value != null ? kvp.Value.LowerLimit.ToString("F4") : string.Empty
                    })
                    .ToList();
            }

            return DetectionDataStorage.GetAllItemNames()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new AvailableItemRow { Name = name, UpperLimit = string.Empty, LowerLimit = string.Empty })
                .ToList();
        }

        private void RebuildTemplateColumns()
        {
            TemplateColumns.Clear();

            var template = TemplateComboBox.SelectedItem as RealTimeDataExportTemplate;
            if (template?.Columns == null)
            {
                return;
            }

            int order = 1;
            foreach (var column in template.Columns)
            {
                TemplateColumns.Add(new TemplateColumnRow(order++, column));
            }
        }

        private void UpdateUiEnabledState()
        {
            bool customEnabled = CustomModeRadio.IsChecked == true;
            TemplateComboBox.IsEnabled = customEnabled;
            TemplateColumnsDataGrid.IsEnabled = customEnabled;
        }

        private RealTimeDataExportTemplate GetSelectedTemplate()
        {
            return TemplateComboBox.SelectedItem as RealTimeDataExportTemplate;
        }

        private void AddMetaColumn(RealTimeDataExportMetaField field)
        {
            var template = GetSelectedTemplate();
            if (template == null)
            {
                return;
            }

            template.Columns.Add(new RealTimeDataExportColumn
            {
                Kind = RealTimeDataExportColumnKind.Meta,
                MetaField = field
            });

            RebuildTemplateColumns();
            TemplateColumnsDataGrid.SelectedIndex = TemplateColumns.Count - 1;
        }

        private void AddItemColumn(string itemName, RealTimeDataExportItemField field)
        {
            var template = GetSelectedTemplate();
            if (template == null || string.IsNullOrWhiteSpace(itemName))
            {
                return;
            }

            template.Columns.Add(new RealTimeDataExportColumn
            {
                Kind = RealTimeDataExportColumnKind.Item,
                ItemName = itemName,
                ItemField = field
            });

            RebuildTemplateColumns();
            TemplateColumnsDataGrid.SelectedIndex = TemplateColumns.Count - 1;
        }

        private static RealTimeDataExportColumn CloneColumn(RealTimeDataExportColumn column)
        {
            if (column == null)
            {
                return null;
            }

            return new RealTimeDataExportColumn
            {
                Kind = column.Kind,
                MetaField = column.MetaField,
                ItemName = column.ItemName,
                ItemField = column.ItemField
            };
        }

        private RealTimeDataExportConfig BuildConfigFromUi()
        {
            var config = new RealTimeDataExportConfig
            {
                Mode = CustomModeRadio.IsChecked == true ? RealTimeDataExportMode.Custom : RealTimeDataExportMode.Default
            };

            var templates = Templates
                .Where(t => t != null)
                .Select(t => new RealTimeDataExportTemplate
                {
                    Name = t.Name,
                    Columns = (t.Columns ?? new List<RealTimeDataExportColumn>())
                        .Select(CloneColumn)
                        .Where(c => c != null)
                        .ToList()
                })
                .ToList();

            config.Templates = templates;
            config.ActiveTemplateName = (TemplateComboBox.SelectedItem as RealTimeDataExportTemplate)?.Name
                                        ?? templates.FirstOrDefault()?.Name
                                        ?? config.ActiveTemplateName;

            return config;
        }

        private bool IsDirty()
        {
            if (_originalConfigSnapshot == null)
            {
                return true;
            }

            var current = BuildConfigFromUi();
            var currentSnapshot = JToken.FromObject(current);
            return !JToken.DeepEquals(_originalConfigSnapshot, currentSnapshot);
        }

        private bool TrySaveCurrent(bool showSuccessMessage)
        {
            var configToSave = BuildConfigFromUi();
            if (RealTimeDataExportConfigManager.TrySave(configToSave, out var savedPath, out var errorMessage))
            {
                _config = configToSave;
                _originalConfigSnapshot = JToken.FromObject(_config);

                if (showSuccessMessage)
                {
                    MessageBox.Show($"已保存到：{savedPath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return true;
            }

            MessageBox.Show($"保存实时数据导出配置失败：{errorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        private void RealTimeDataExportConfigWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            if (!IsDirty())
            {
                return;
            }

            var result = MessageBox.Show("实时数据导出配置已修改，是否保存？", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                if (!TrySaveCurrent(showSuccessMessage: false))
                {
                    e.Cancel = true;
                }
            }
        }

        private string EnsureUniqueTemplateName(string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "自定义模板";
            }

            string name = baseName;
            int i = 1;
            while (Templates.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"{baseName}_{i++}";
            }
            return name;
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

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancelButton = new Button { Content = "取消", Width = 80, IsCancel = true };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            root.Children.Add(buttonPanel);

            string result = null;
            okButton.Click += (s, e) =>
            {
                result = textBox.Text;
                window.DialogResult = true;
                window.Close();
            };

            window.Content = root;
            window.ShowDialog();
            return result;
        }

        private void DefaultModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateUiEnabledState();
        }

        private void CustomModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateUiEnabledState();
        }

        private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            RebuildTemplateColumns();
        }

        private void RefreshItemsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshAvailableItems();
        }

        private void AddValueButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AvailableItemRow row)
            {
                AddItemColumn(row.Name, RealTimeDataExportItemField.Value);
            }
        }

        private void AddUpperLimitButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AvailableItemRow row)
            {
                AddItemColumn(row.Name, RealTimeDataExportItemField.UpperLimit);
            }
        }

        private void AddLowerLimitButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AvailableItemRow row)
            {
                AddItemColumn(row.Name, RealTimeDataExportItemField.LowerLimit);
            }
        }

        private void AddOutOfRangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AvailableItemRow row)
            {
                AddItemColumn(row.Name, RealTimeDataExportItemField.IsOutOfRange);
            }
        }

        private void AddMetaImageNumberButton_Click(object sender, RoutedEventArgs e) => AddMetaColumn(RealTimeDataExportMetaField.ImageNumber);
        private void AddMetaTimestampButton_Click(object sender, RoutedEventArgs e) => AddMetaColumn(RealTimeDataExportMetaField.Timestamp);
        private void AddMetaLotNumberButton_Click(object sender, RoutedEventArgs e) => AddMetaColumn(RealTimeDataExportMetaField.LotNumber);
        private void AddMetaDefectTypeButton_Click(object sender, RoutedEventArgs e) => AddMetaColumn(RealTimeDataExportMetaField.DefectType);
        private void AddMetaResultButton_Click(object sender, RoutedEventArgs e) => AddMetaColumn(RealTimeDataExportMetaField.Result);

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            var template = GetSelectedTemplate();
            int index = TemplateColumnsDataGrid.SelectedIndex;
            if (template?.Columns == null || index <= 0 || index >= template.Columns.Count)
            {
                return;
            }

            (template.Columns[index - 1], template.Columns[index]) = (template.Columns[index], template.Columns[index - 1]);
            RebuildTemplateColumns();
            TemplateColumnsDataGrid.SelectedIndex = index - 1;
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            var template = GetSelectedTemplate();
            int index = TemplateColumnsDataGrid.SelectedIndex;
            if (template?.Columns == null || index < 0 || index >= template.Columns.Count - 1)
            {
                return;
            }

            (template.Columns[index + 1], template.Columns[index]) = (template.Columns[index], template.Columns[index + 1]);
            RebuildTemplateColumns();
            TemplateColumnsDataGrid.SelectedIndex = index + 1;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var template = GetSelectedTemplate();
            int index = TemplateColumnsDataGrid.SelectedIndex;
            if (template?.Columns == null || index < 0 || index >= template.Columns.Count)
            {
                return;
            }

            template.Columns.RemoveAt(index);
            RebuildTemplateColumns();
            TemplateColumnsDataGrid.SelectedIndex = Math.Min(index, TemplateColumns.Count - 1);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var template = GetSelectedTemplate();
            if (template?.Columns == null)
            {
                return;
            }

            if (MessageBox.Show("确定要清空当前模板的所有列吗？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return;
            }

            template.Columns.Clear();
            RebuildTemplateColumns();
        }

        private void GenerateDefaultTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var template = GetSelectedTemplate();
            if (template == null)
            {
                return;
            }

            var itemNames = AvailableItems.Select(i => i.Name).Where(n => !string.IsNullOrWhiteSpace(n)).OrderBy(n => n).ToList();

            template.Columns.Clear();
            template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.ImageNumber });
            template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Timestamp });
            template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.LotNumber });
            template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.DefectType });
            template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Result });

            foreach (var itemName in itemNames)
            {
                template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = itemName, ItemField = RealTimeDataExportItemField.Value });
                template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = itemName, ItemField = RealTimeDataExportItemField.LowerLimit });
                template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = itemName, ItemField = RealTimeDataExportItemField.UpperLimit });
                template.Columns.Add(new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Item, ItemName = itemName, ItemField = RealTimeDataExportItemField.IsOutOfRange });
            }

            RebuildTemplateColumns();
        }

        private void NewTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            string name = PromptForText("新建模板", "请输入模板名称：", "自定义模板");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            name = EnsureUniqueTemplateName(name.Trim());
            var template = new RealTimeDataExportTemplate
            {
                Name = name,
                Columns = new List<RealTimeDataExportColumn>
                {
                    new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.ImageNumber },
                    new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Timestamp },
                    new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.LotNumber },
                    new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.DefectType },
                    new RealTimeDataExportColumn { Kind = RealTimeDataExportColumnKind.Meta, MetaField = RealTimeDataExportMetaField.Result }
                }
            };

            Templates.Add(template);
            TemplateComboBox.SelectedItem = template;
            RebuildTemplateColumns();
        }

        private void CopyTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var source = GetSelectedTemplate();
            if (source == null)
            {
                return;
            }

            string name = PromptForText("复制模板", "请输入新模板名称：", source.Name + "_复制");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            name = EnsureUniqueTemplateName(name.Trim());
            var copied = new RealTimeDataExportTemplate
            {
                Name = name,
                Columns = source.Columns.Select(CloneColumn).Where(c => c != null).ToList()
            };

            Templates.Add(copied);
            TemplateComboBox.SelectedItem = copied;
            RebuildTemplateColumns();
        }

        private void RenameTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var template = GetSelectedTemplate();
            if (template == null)
            {
                return;
            }

            string name = PromptForText("重命名模板", "请输入新的模板名称：", template.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            name = name.Trim();
            if (!string.Equals(name, template.Name, StringComparison.OrdinalIgnoreCase))
            {
                name = EnsureUniqueTemplateName(name);
            }

            template.Name = name;
            TemplateComboBox.Items.Refresh();
        }

        private void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var template = GetSelectedTemplate();
            if (template == null)
            {
                return;
            }

            if (Templates.Count <= 1)
            {
                MessageBox.Show("至少需要保留一个模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"确定要删除模板“{template.Name}”吗？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return;
            }

            int index = TemplateComboBox.SelectedIndex;
            Templates.Remove(template);
            TemplateComboBox.SelectedIndex = Math.Max(0, Math.Min(index, Templates.Count - 1));
            RebuildTemplateColumns();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            TrySaveCurrent(showSuccessMessage: true);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TemplateColumnsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        public sealed class AvailableItemRow
        {
            public string Name { get; set; }
            public string UpperLimit { get; set; }
            public string LowerLimit { get; set; }
        }

        public sealed class TemplateColumnRow
        {
            public int Order { get; }
            public RealTimeDataExportColumn Column { get; }
            public string DisplayText => BuildDisplayText(Column);

            public TemplateColumnRow(int order, RealTimeDataExportColumn column)
            {
                Order = order;
                Column = column;
            }

            private static string BuildDisplayText(RealTimeDataExportColumn column)
            {
                if (column == null)
                {
                    return string.Empty;
                }

                if (column.Kind == RealTimeDataExportColumnKind.Meta)
                {
                    switch (column.MetaField)
                    {
                        case RealTimeDataExportMetaField.ImageNumber:
                            return "序号";
                        case RealTimeDataExportMetaField.Timestamp:
                            return "时间戳";
                        case RealTimeDataExportMetaField.LotNumber:
                            return "LOT号";
                        case RealTimeDataExportMetaField.DefectType:
                            return "缺陷类型";
                        case RealTimeDataExportMetaField.Result:
                            return "结果";
                        default:
                            return "基础字段";
                    }
                }

                string itemName = column.ItemName ?? string.Empty;
                switch (column.ItemField)
                {
                    case RealTimeDataExportItemField.Value:
                        return $"{itemName}";
                    case RealTimeDataExportItemField.UpperLimit:
                        return $"{itemName}_上限";
                    case RealTimeDataExportItemField.LowerLimit:
                        return $"{itemName}_下限";
                    case RealTimeDataExportItemField.IsOutOfRange:
                        return $"{itemName}_超限";
                    default:
                        return itemName;
                }
            }
        }
    }
}
