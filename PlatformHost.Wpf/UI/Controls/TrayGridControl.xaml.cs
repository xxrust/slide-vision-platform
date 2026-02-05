using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp2.UI;

namespace WpfApp2.UI.Controls
{
    public partial class TrayGridControl : UserControl
    {
        private static readonly Brush GridBorderBrush = new SolidColorBrush(Color.FromRgb(93, 109, 126));
        private static readonly Brush GridHeaderForeground = new SolidColorBrush(Color.FromRgb(236, 240, 241));
        private static readonly Brush EmptyCellBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80));
        private static readonly Brush HiddenOkBrush = new SolidColorBrush(Color.FromRgb(52, 73, 94));
        private static readonly Brush UnknownStateBrush = new SolidColorBrush(Color.FromRgb(127, 140, 141));

        public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
            nameof(Rows),
            typeof(int),
            typeof(TrayGridControl),
            new PropertyMetadata(0, OnLayoutChanged));

        public static readonly DependencyProperty ColsProperty = DependencyProperty.Register(
            nameof(Cols),
            typeof(int),
            typeof(TrayGridControl),
            new PropertyMetadata(0, OnLayoutChanged));

        public static readonly DependencyProperty IconFolderProperty = DependencyProperty.Register(
            nameof(IconFolder),
            typeof(string),
            typeof(TrayGridControl),
            new PropertyMetadata(null, OnIconFolderChanged));

        public static readonly DependencyProperty ShowOkCellsProperty = DependencyProperty.Register(
            nameof(ShowOkCells),
            typeof(bool),
            typeof(TrayGridControl),
            new PropertyMetadata(true, OnShowOkCellsChanged));

        public static readonly DependencyProperty Rotate90Property = DependencyProperty.Register(
            nameof(Rotate90),
            typeof(bool),
            typeof(TrayGridControl),
            new PropertyMetadata(false, OnRotateChanged));

        private readonly Dictionary<(int Row, int Col), CellVisual> _cells = new Dictionary<(int Row, int Col), CellVisual>();
        private readonly Dictionary<(int Row, int Col), string> _cellStates = new Dictionary<(int Row, int Col), string>();
        private readonly Dictionary<(int Row, int Col), TrayCellInfo> _cellInfo = new Dictionary<(int Row, int Col), TrayCellInfo>();
        private readonly Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private TrayCellImageWindow _viewerWindow;
        private (int Row, int Col)? _viewerPosition;

        public event EventHandler<TrayCellClickedEventArgs> CellClicked;

        public TrayGridControl()
        {
            InitializeComponent();
            DefectStates = new Dictionary<string, TrayDefectVisual>(StringComparer.OrdinalIgnoreCase)
            {
                ["OK"] = new TrayDefectVisual("ok.png", Color.FromRgb(40, 167, 69)),
                ["NG"] = new TrayDefectVisual("ng.png", Color.FromRgb(231, 76, 60))
            };
        }

        public int Rows
        {
            get => (int)GetValue(RowsProperty);
            set => SetValue(RowsProperty, value);
        }

        public int Cols
        {
            get => (int)GetValue(ColsProperty);
            set => SetValue(ColsProperty, value);
        }

        public string IconFolder
        {
            get => (string)GetValue(IconFolderProperty);
            set => SetValue(IconFolderProperty, value);
        }

        public bool ShowOkCells
        {
            get => (bool)GetValue(ShowOkCellsProperty);
            set => SetValue(ShowOkCellsProperty, value);
        }

        public bool Rotate90
        {
            get => (bool)GetValue(Rotate90Property);
            set => SetValue(Rotate90Property, value);
        }

        public IDictionary<string, TrayDefectVisual> DefectStates { get; }

        public (int Row, int Col)? LastClickedPosition { get; private set; }

        public void SetCellStatus(int row, int col, string state)
        {
            if (!_cells.TryGetValue((row, col), out var cell))
            {
                return;
            }

            _cellStates[(row, col)] = state;
            ApplyCellVisual(cell, state);
        }

        public void UpdateCellInfo(int row, int col, string result, string imagePath, DateTime detectionTime)
        {
            var info = new TrayCellInfo(row, col, result, imagePath, detectionTime);
            _cellInfo[(row, col)] = info;
            SetCellStatus(row, col, result);
            RefreshViewerIfNeeded(info);
        }

        public void ClearCells()
        {
            _cellStates.Clear();
            _cellInfo.Clear();
            LastClickedPosition = null;
            RefreshCellVisuals();
        }

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrayGridControl control)
            {
                control.RebuildGrid();
            }
        }

        private static void OnIconFolderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrayGridControl control)
            {
                control._iconCache.Clear();
                control.RefreshCellVisuals();
            }
        }

        private static void OnShowOkCellsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrayGridControl control)
            {
                control.RefreshCellVisuals();
            }
        }

        private static void OnRotateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrayGridControl control)
            {
                control.ApplyRotation();
            }
        }

        private void RebuildGrid()
        {
            if (GridRoot == null)
            {
                return;
            }

            GridRoot.Children.Clear();
            GridRoot.RowDefinitions.Clear();
            GridRoot.ColumnDefinitions.Clear();
            _cells.Clear();

            if (Rows <= 0 || Cols <= 0)
            {
                return;
            }

            GridRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int row = 0; row < Rows; row++)
            {
                GridRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            GridRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int col = 0; col < Cols; col++)
            {
                GridRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            AddColumnHeaders();
            AddRowHeaders();
            AddCells();
            RefreshCellVisuals();
            ApplyRotation();
        }

        private void AddColumnHeaders()
        {
            for (int col = 1; col <= Cols; col++)
            {
                var header = new TextBlock
                {
                    Text = col.ToString(),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4),
                    Foreground = GridHeaderForeground
                };

                Grid.SetRow(header, 0);
                Grid.SetColumn(header, col);
                GridRoot.Children.Add(header);
            }
        }

        private void AddRowHeaders()
        {
            for (int row = 1; row <= Rows; row++)
            {
                var logicalRow = Rows - row + 1;
                var header = new TextBlock
                {
                    Text = logicalRow.ToString(),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4),
                    Foreground = GridHeaderForeground
                };

                Grid.SetRow(header, row);
                Grid.SetColumn(header, 0);
                GridRoot.Children.Add(header);
            }
        }

        private void AddCells()
        {
            for (int row = 1; row <= Rows; row++)
            {
                var logicalRow = Rows - row + 1;
                for (int col = 1; col <= Cols; col++)
                {
                    var border = new Border
                    {
                        BorderBrush = GridBorderBrush,
                        BorderThickness = new Thickness(1),
                        Background = EmptyCellBrush
                    };

                    var image = new Image
                    {
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Collapsed
                    };

                    border.Child = image;
                    border.ToolTip = $"{logicalRow},{col}";
                    border.Tag = new CellPosition(logicalRow, col);
                    border.MouseLeftButtonUp += OnCellClicked;
                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    GridRoot.Children.Add(border);

                    _cells[(logicalRow, col)] = new CellVisual(border, image);
                }
            }
        }

        private void OnCellClicked(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border border))
            {
                return;
            }

            var position = border.Tag as CellPosition;
            if (position == null)
            {
                return;
            }

            ShowViewer(position.Row, position.Col);
            LastClickedPosition = (position.Row, position.Col);
            CellClicked?.Invoke(this, new TrayCellClickedEventArgs(position.Row, position.Col));
        }

        private void ShowViewer(int row, int col)
        {
            if (!_cellInfo.TryGetValue((row, col), out var info))
            {
                _cellStates.TryGetValue((row, col), out var state);
                info = new TrayCellInfo(row, col, state, null, DateTime.UtcNow);
            }

            if (_viewerWindow == null)
            {
                _viewerWindow = new TrayCellImageWindow
                {
                    Owner = Window.GetWindow(this)
                };
                _viewerWindow.Closed += (_, __) =>
                {
                    _viewerWindow = null;
                    _viewerPosition = null;
                };
            }

            if (!_viewerWindow.IsVisible)
            {
                _viewerWindow.Show();
            }

            _viewerWindow.UpdateInfo(info);
            _viewerWindow.Activate();
            _viewerPosition = (row, col);
        }

        private void RefreshViewerIfNeeded(TrayCellInfo info)
        {
            if (_viewerWindow == null || !_viewerWindow.IsVisible || !_viewerPosition.HasValue)
            {
                return;
            }

            var position = _viewerPosition.Value;
            if (position.Row == info.Row && position.Col == info.Col)
            {
                _viewerWindow.UpdateInfo(info);
            }
        }

        private void RefreshCellVisuals()
        {
            foreach (var kvp in _cellStates)
            {
                if (_cells.TryGetValue(kvp.Key, out var cell))
                {
                    ApplyCellVisual(cell, kvp.Value);
                }
            }
        }

        private void ApplyCellVisual(CellVisual cell, string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                cell.Border.Background = EmptyCellBrush;
                cell.Image.Source = null;
                cell.Image.Visibility = Visibility.Collapsed;
                cell.Border.Opacity = 1;
                cell.Border.IsEnabled = true;
                return;
            }

            if (!ShowOkCells && string.Equals(state, "OK", StringComparison.OrdinalIgnoreCase))
            {
                cell.Border.Background = HiddenOkBrush;
                cell.Image.Source = null;
                cell.Image.Visibility = Visibility.Collapsed;
                cell.Border.Opacity = 0.4;
                cell.Border.IsEnabled = false;
                return;
            }

            if (!DefectStates.TryGetValue(state, out var visual))
            {
                cell.Border.Background = UnknownStateBrush;
                cell.Image.Source = null;
                cell.Image.Visibility = Visibility.Collapsed;
                cell.Border.Opacity = 1;
                cell.Border.IsEnabled = true;
                return;
            }

            cell.Border.Background = new SolidColorBrush(visual.FallbackColor);
            cell.Border.Opacity = 1;
            cell.Border.IsEnabled = true;

            var iconPath = ResolveIconPath(visual.IconFileName);
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
            {
                cell.Image.Source = null;
                cell.Image.Visibility = Visibility.Collapsed;
                return;
            }

            if (!_iconCache.TryGetValue(iconPath, out var image))
            {
                image = LoadIcon(iconPath);
                if (image != null)
                {
                    _iconCache[iconPath] = image;
                }
            }

            cell.Image.Source = image;
            cell.Image.Visibility = image == null ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ApplyRotation()
        {
            if (GridRoot == null)
            {
                return;
            }

            GridRoot.LayoutTransform = Rotate90 ? new RotateTransform(90) : Transform.Identity;
        }

        private string ResolveIconPath(string iconFileName)
        {
            if (string.IsNullOrWhiteSpace(iconFileName) || string.IsNullOrWhiteSpace(IconFolder))
            {
                return null;
            }

            return Path.Combine(IconFolder, iconFileName);
        }

        private static ImageSource LoadIcon(string iconPath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private sealed class CellVisual
        {
            public CellVisual(Border border, Image image)
            {
                Border = border;
                Image = image;
            }

            public Border Border { get; }
            public Image Image { get; }
        }

        private sealed class CellPosition
        {
            public CellPosition(int row, int col)
            {
                Row = row;
                Col = col;
            }

            public int Row { get; }
            public int Col { get; }
        }
    }

    public sealed class TrayCellClickedEventArgs : EventArgs
    {
        public TrayCellClickedEventArgs(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public int Row { get; }
        public int Col { get; }
    }
}
