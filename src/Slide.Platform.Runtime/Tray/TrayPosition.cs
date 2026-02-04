using System;
using System.Globalization;

namespace Slide.Platform.Runtime.Tray
{
    public readonly struct TrayPosition
    {
        public TrayPosition(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public int Row { get; }
        public int Col { get; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}_{1}", Row, Col);
        }

        public static TrayPosition Parse(string value, int rows, int cols, TrayMappingMode mode)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Position is required.", nameof(value));
            }

            var trimmed = value.Trim();
            if (TryParseRowCol(trimmed, out var row, out var col))
            {
                EnsureInRange(row, col, rows, cols);
                return new TrayPosition(row, col);
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                var (mappedRow, mappedCol) = TrayCoordinateMapper.IndexToPosition(index, rows, cols, mode);
                return new TrayPosition(mappedRow, mappedCol);
            }

            throw new FormatException($"Invalid position format: '{value}'. Expected 'row_col' or index.");
        }

        private static bool TryParseRowCol(string value, out int row, out int col)
        {
            row = 0;
            col = 0;

            var separatorIndex = value.IndexOf('_');
            if (separatorIndex < 0)
            {
                separatorIndex = value.IndexOf(',');
            }

            if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
            {
                return false;
            }

            var rowPart = value.Substring(0, separatorIndex);
            var colPart = value.Substring(separatorIndex + 1);

            return int.TryParse(rowPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out row)
                && int.TryParse(colPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out col);
        }

        private static void EnsureInRange(int row, int col, int rows, int cols)
        {
            if (rows <= 0 || cols <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), "Tray dimensions must be positive.");
            }

            if (row < 1 || row > rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), $"Row must be between 1 and {rows}.");
            }

            if (col < 1 || col > cols)
            {
                throw new ArgumentOutOfRangeException(nameof(col), $"Col must be between 1 and {cols}.");
            }
        }
    }
}
