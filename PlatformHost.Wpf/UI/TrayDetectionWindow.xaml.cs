using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using Slide.Algorithm.Contracts;
using Slide.Platform.Runtime.Tray;
using WpfApp2.Algorithms;
using WpfApp2.UI.Controls;

namespace WpfApp2.UI
{
    public partial class TrayDetectionWindow : Window
    {
        private const int DefaultRows = 10;
        private const int DefaultCols = 9;
        private const int DefaultHistoryLimit = 20;

        private readonly Page1 _page;
        private readonly TrayDataManager _trayManager;
        private readonly TraySqliteRepository _trayRepository;
        private readonly TrayComponent _trayComponent;
        private TrayNgBrowserWindow _ngBrowser;
        private int _fallbackIndex;

        public TrayDetectionWindow(Page1 page)
        {
            InitializeComponent();

            _page = page;
            _trayManager = new TrayDataManager();
            _trayRepository = new TraySqliteRepository(BuildConnectionString());
            _trayComponent = new TrayComponent(_trayManager, _trayRepository);

            RowsBox.Text = DefaultRows.ToString(CultureInfo.InvariantCulture);
            ColsBox.Text = DefaultCols.ToString(CultureInfo.InvariantCulture);
            BatchBox.Text = _page?.CurrentLotValue ?? string.Empty;

            IconFolderBox.Text = ResolveDefaultIconFolder();
            TrayGrid.IconFolder = IconFolderBox.Text;
            TrayGrid.ShowOkCells = true;

            _trayComponent.OnTrayCompleted += (_, __) => Dispatcher.BeginInvoke(new Action(UpdateStatistics));

            if (_page != null)
            {
                _page.AlgorithmResultProduced += OnAlgorithmResultProduced;
            }

            Closed += OnTrayWindowClosed;
        }

        private static string BuildConnectionString()
        {
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "tray.db");
            return $"Data Source={dbPath}";
        }

        private static string ResolveDefaultIconFolder()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "TrayIcons");
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetLayout(out var rows, out var cols))
            {
                MessageBox.Show("请输入有效的托盘行列。", "Tray", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var batchName = BatchBox.Text?.Trim();
            _trayComponent.StartTray(rows, cols, batchName);
            TrayGrid.Rows = rows;
            TrayGrid.Cols = cols;
            TrayGrid.ClearCells();
            _fallbackIndex = 0;
            UpdateStatistics();
            RefreshNgBrowser();
        }

        private void CompleteButton_Click(object sender, RoutedEventArgs e)
        {
            _trayComponent.CompleteTray();
            UpdateStatistics();
            RefreshNgBrowser();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _trayComponent.ResetCurrentTray();
            TrayGrid.ClearCells();
            UpdateStatistics();
            RefreshNgBrowser();
        }

        private void ShowOkCheck_Changed(object sender, RoutedEventArgs e)
        {
            TrayGrid.ShowOkCells = ShowOkCheck.IsChecked == true;
        }

        private void RotateCheck_Changed(object sender, RoutedEventArgs e)
        {
            TrayGrid.Rotate90 = RotateCheck.IsChecked == true;
        }

        private void BrowseIconButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.SelectedPath = IconFolderBox.Text;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    IconFolderBox.Text = dialog.SelectedPath;
                    TrayGrid.IconFolder = dialog.SelectedPath;
                }
            }
        }

        private void NgBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ngBrowser == null)
            {
                _ngBrowser = new TrayNgBrowserWindow
                {
                    Owner = this
                };
                _ngBrowser.Closed += (_, __) => _ngBrowser = null;
            }

            RefreshNgBrowser();
            _ngBrowser.Show();
            _ngBrowser.Activate();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            _page?.ShowTrayHelpWindow();
        }

        private void OnTrayWindowClosed(object sender, EventArgs e)
        {
            if (_page != null)
            {
                _page.AlgorithmResultProduced -= OnAlgorithmResultProduced;
            }
        }

        private void OnAlgorithmResultProduced(object sender, AlgorithmResultEventArgs e)
        {
            if (e?.Result == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() => ProcessAlgorithmResult(e.Result)));
        }

        private void ProcessAlgorithmResult(AlgorithmResult result)
        {
            if (_trayManager.CurrentTray == null)
            {
                return;
            }

            if (!TryResolveTrayPosition(result, out var row, out var col))
            {
                return;
            }

            var resultLabel = ResolveResultLabel(result);
            var imagePath = _page?.GetCurrentTrayImagePath();
            var detectionTime = result.Timestamp;

            try
            {
                _trayComponent.UpdateResult($"{row}_{col}", resultLabel, imagePath, detectionTime);
                TrayGrid.UpdateCellInfo(row, col, resultLabel, imagePath, detectionTime);
                UpdateStatistics();
                RefreshNgBrowser();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tray 更新失败: {ex.Message}", "Tray", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryResolveTrayPosition(AlgorithmResult result, out int row, out int col)
        {
            row = 0;
            col = 0;

            var tray = _trayManager.CurrentTray;
            if (tray == null)
            {
                return false;
            }

            if (result.DebugInfo != null && result.DebugInfo.TryGetValue("TrayPosition", out var positionValue))
            {
                try
                {
                    var parsed = TrayPosition.Parse(positionValue, tray.Rows, tray.Cols, _trayComponent.MappingMode);
                    row = parsed.Row;
                    col = parsed.Col;
                    return true;
                }
                catch
                {
                    // Fall back to index mapping.
                }
            }

            var imageNumber = ResolveImageNumber(result);
            var index = ResolveIndex(imageNumber, tray.TotalSlots);
            if (index < 0 || index >= tray.TotalSlots)
            {
                return false;
            }

            var mapped = TrayCoordinateMapper.IndexToPosition(index, tray.Rows, tray.Cols, _trayComponent.MappingMode);
            row = mapped.Row;
            col = mapped.Col;
            return true;
        }

        private string ResolveImageNumber(AlgorithmResult result)
        {
            if (result.DebugInfo != null && result.DebugInfo.TryGetValue("ImageNumber", out var imageNumber))
            {
                if (!string.IsNullOrWhiteSpace(imageNumber))
                {
                    return imageNumber;
                }
            }

            return _page?.GetCurrentImageNumberForRecord();
        }

        private int ResolveIndex(string imageNumber, int totalSlots)
        {
            if (!string.IsNullOrWhiteSpace(imageNumber)
                && int.TryParse(imageNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                var index = number > 0 ? number - 1 : 0;
                return index >= totalSlots ? -1 : index;
            }

            return _fallbackIndex++;
        }

        private static string ResolveResultLabel(AlgorithmResult result)
        {
            if (result.IsOk)
            {
                return "OK";
            }

            return string.IsNullOrWhiteSpace(result.DefectType) ? "NG" : result.DefectType;
        }

        private bool TryGetLayout(out int rows, out int cols)
        {
            rows = 0;
            cols = 0;

            return int.TryParse(RowsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out rows)
                && int.TryParse(ColsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out cols)
                && rows > 0
                && cols > 0;
        }

        private void UpdateStatistics()
        {
            var stats = _trayManager.GetStatistics();
            TotalText.Text = stats.TotalSlots.ToString(CultureInfo.InvariantCulture);
            OkText.Text = stats.OkCount.ToString(CultureInfo.InvariantCulture);
            NgText.Text = stats.NgCount.ToString(CultureInfo.InvariantCulture);
            YieldText.Text = stats.TotalSlots == 0
                ? "0%"
                : string.Format(CultureInfo.InvariantCulture, "{0:P1}", stats.YieldRate);
        }

        private void RefreshNgBrowser()
        {
            if (_ngBrowser == null)
            {
                return;
            }

            _ngBrowser.SetCurrentTrayItems(BuildNgItems(_trayManager.CurrentTray));

            var history = _trayRepository.LoadRecentTrays(DefaultHistoryLimit);
            foreach (var tray in history)
            {
                _ngBrowser.SetHistoryTrayItems(tray.TrayId, BuildNgItems(tray));
            }

            _ngBrowser.SetAllHistoryItems(history.SelectMany(BuildNgItems).ToList());
        }

        private static IEnumerable<TrayNgItem> BuildNgItems(TrayData tray)
        {
            if (tray == null)
            {
                return Enumerable.Empty<TrayNgItem>();
            }

            return tray.Materials
                .Where(material => material != null && !IsOkResult(material.Result))
                .Select(material => new TrayNgItem(tray.TrayId, material.Row, material.Col, material.Result, material.ImagePath, material.DetectionTime))
                .ToList();
        }

        private static bool IsOkResult(string result)
        {
            return string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase);
        }
    }
}
