using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WpfApp2.UI
{
    public partial class TrayNgBrowserWindow : Window
    {
        private const int DefaultPageSize = 12;
        private readonly ObservableCollection<TrayNgDisplayItem> _displayItems = new ObservableCollection<TrayNgDisplayItem>();
        private readonly Dictionary<string, List<TrayNgItem>> _historyItems = new Dictionary<string, List<TrayNgItem>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<TrayNgItem> _currentItems = new List<TrayNgItem>();
        private readonly List<TrayNgItem> _allHistoryItems = new List<TrayNgItem>();
        private int _currentPage = 1;
        private NgMode _currentMode = NgMode.CurrentTray;

        public TrayNgBrowserWindow()
        {
            InitializeComponent();
            ItemsList.ItemsSource = _displayItems;
            InitializeModeSelector();
            RefreshHistorySelector();
            RefreshPage();
        }

        public int PageSize { get; set; } = DefaultPageSize;

        public void SetCurrentTrayItems(IEnumerable<TrayNgItem> items)
        {
            _currentItems.Clear();
            if (items != null)
            {
                _currentItems.AddRange(items);
            }

            if (_currentMode == NgMode.CurrentTray)
            {
                ResetToFirstPage();
            }
        }

        public void SetHistoryTrayItems(string trayId, IEnumerable<TrayNgItem> items)
        {
            if (string.IsNullOrWhiteSpace(trayId))
            {
                return;
            }

            _historyItems[trayId] = items?.ToList() ?? new List<TrayNgItem>();
            RefreshHistorySelector();
            if (_currentMode == NgMode.HistoryTray && string.Equals(GetSelectedHistoryTrayId(), trayId, StringComparison.OrdinalIgnoreCase))
            {
                ResetToFirstPage();
            }
        }

        public void SetAllHistoryItems(IEnumerable<TrayNgItem> items)
        {
            _allHistoryItems.Clear();
            if (items != null)
            {
                _allHistoryItems.AddRange(items);
            }

            if (_currentMode == NgMode.AllHistory)
            {
                ResetToFirstPage();
            }
        }

        private void InitializeModeSelector()
        {
            var options = new[]
            {
                new ModeOption("当前托盘", NgMode.CurrentTray),
                new ModeOption("历史托盘", NgMode.HistoryTray),
                new ModeOption("全部历史", NgMode.AllHistory)
            };

            ModeSelector.ItemsSource = options;
            ModeSelector.DisplayMemberPath = nameof(ModeOption.Label);
            ModeSelector.SelectedIndex = 0;
        }

        private void RefreshHistorySelector()
        {
            var keys = _historyItems.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
            HistorySelector.ItemsSource = keys;
            HistorySelector.Visibility = _currentMode == NgMode.HistoryTray ? Visibility.Visible : Visibility.Collapsed;

            if (keys.Count > 0 && HistorySelector.SelectedIndex < 0)
            {
                HistorySelector.SelectedIndex = 0;
            }
        }

        private string GetSelectedHistoryTrayId()
        {
            return HistorySelector.SelectedItem as string;
        }

        private IReadOnlyList<TrayNgItem> GetCurrentModeItems()
        {
            switch (_currentMode)
            {
                case NgMode.CurrentTray:
                    return _currentItems;
                case NgMode.HistoryTray:
                {
                    var trayId = GetSelectedHistoryTrayId();
                    if (string.IsNullOrWhiteSpace(trayId))
                    {
                        return Array.Empty<TrayNgItem>();
                    }

                    return _historyItems.TryGetValue(trayId, out var items)
                        ? items
                        : Array.Empty<TrayNgItem>();
                }
                case NgMode.AllHistory:
                    return _allHistoryItems;
                default:
                    return Array.Empty<TrayNgItem>();
            }
        }

        private void ResetToFirstPage()
        {
            _currentPage = 1;
            RefreshPage();
        }

        private void RefreshPage()
        {
            var items = GetCurrentModeItems();
            var totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)PageSize));
            _currentPage = Math.Min(_currentPage, totalPages);

            var pageItems = items
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .Select(CreateDisplayItem)
                .ToList();

            _displayItems.Clear();
            foreach (var item in pageItems)
            {
                _displayItems.Add(item);
            }

            PageIndicator.Text = string.Format(CultureInfo.InvariantCulture, "{0} / {1}", _currentPage, totalPages);
            PrevButton.IsEnabled = _currentPage > 1;
            NextButton.IsEnabled = _currentPage < totalPages;
        }

        private static TrayNgDisplayItem CreateDisplayItem(TrayNgItem item)
        {
            var title = string.Format(CultureInfo.InvariantCulture, "{0} ({1},{2}) {3}", item.TrayId, item.Row, item.Col, item.Result);
            return new TrayNgDisplayItem(title, item.ImagePath, LoadImage(item.ImagePath));
        }

        private static BitmapImage LoadImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModeSelector.SelectedItem is ModeOption option)
            {
                _currentMode = option.Mode;
                RefreshHistorySelector();
                ResetToFirstPage();
            }
        }

        private void HistorySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentMode == NgMode.HistoryTray)
            {
                ResetToFirstPage();
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                RefreshPage();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            var items = GetCurrentModeItems();
            var totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)PageSize));
            if (_currentPage < totalPages)
            {
                _currentPage++;
                RefreshPage();
            }
        }

        private enum NgMode
        {
            CurrentTray,
            HistoryTray,
            AllHistory
        }

        private sealed class ModeOption
        {
            public ModeOption(string label, NgMode mode)
            {
                Label = label;
                Mode = mode;
            }

            public string Label { get; }
            public NgMode Mode { get; }
        }
    }

    public sealed class TrayNgItem
    {
        public TrayNgItem(string trayId, int row, int col, string result, string imagePath, DateTime detectionTime)
        {
            TrayId = trayId;
            Row = row;
            Col = col;
            Result = result;
            ImagePath = imagePath;
            DetectionTime = detectionTime;
        }

        public string TrayId { get; }
        public int Row { get; }
        public int Col { get; }
        public string Result { get; }
        public string ImagePath { get; }
        public DateTime DetectionTime { get; }
    }

    public sealed class TrayNgDisplayItem
    {
        public TrayNgDisplayItem(string title, string imagePath, BitmapImage image)
        {
            Title = title;
            ImagePath = imagePath;
            Image = image;
        }

        public string Title { get; }
        public string ImagePath { get; }
        public BitmapImage Image { get; }
    }
}
