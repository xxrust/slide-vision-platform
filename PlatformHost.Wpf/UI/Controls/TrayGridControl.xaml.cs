using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfApp2.UI.Controls
{
    public partial class TrayGridControl : UserControl
    {
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

        public TrayGridControl()
        {
            InitializeComponent();
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

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrayGridControl control)
            {
                control.RebuildGrid();
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
                    Margin = new Thickness(4)
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
                    Margin = new Thickness(4)
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
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(0.5),
                        Background = Brushes.White
                    };

                    var text = new TextBlock
                    {
                        Text = $"{logicalRow},{col}",
                        FontSize = 10,
                        Foreground = Brushes.Gray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    border.Child = text;
                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    GridRoot.Children.Add(border);
                }
            }
        }
    }
}
