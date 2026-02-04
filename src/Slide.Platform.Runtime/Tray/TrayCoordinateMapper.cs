using System;

namespace Slide.Platform.Runtime.Tray
{
    public enum TrayMappingMode
    {
        Snake,
        RowWise
    }

    public static class TrayCoordinateMapper
    {
        public static (int Row, int Col) IndexToPosition(int index, int rows, int cols, TrayMappingMode mode = TrayMappingMode.Snake)
        {
            ValidateDimensions(rows, cols);

            var total = rows * cols;
            if (index < 0 || index >= total)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {total - 1}.");
            }

            var row = (index / cols) + 1;
            var colOffset = index % cols;
            var col = ResolveColumnForIndex(row, colOffset, cols, mode);

            return (row, col);
        }

        public static int PositionToIndex(int row, int col, int rows, int cols, TrayMappingMode mode = TrayMappingMode.Snake)
        {
            ValidateDimensions(rows, cols);

            if (row < 1 || row > rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), $"Row must be between 1 and {rows}.");
            }

            if (col < 1 || col > cols)
            {
                throw new ArgumentOutOfRangeException(nameof(col), $"Col must be between 1 and {cols}.");
            }

            var colOffset = ResolveColumnOffset(row, col, cols, mode);
            return (row - 1) * cols + colOffset;
        }

        private static void ValidateDimensions(int rows, int cols)
        {
            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be positive.");
            }

            if (cols <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cols), "Cols must be positive.");
            }
        }

        private static int ResolveColumnForIndex(int row, int colOffset, int cols, TrayMappingMode mode)
        {
            switch (mode)
            {
                case TrayMappingMode.Snake:
                    return row % 2 == 1 ? colOffset + 1 : cols - colOffset;
                case TrayMappingMode.RowWise:
                    return colOffset + 1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown tray mapping mode.");
            }
        }

        private static int ResolveColumnOffset(int row, int col, int cols, TrayMappingMode mode)
        {
            switch (mode)
            {
                case TrayMappingMode.Snake:
                    return row % 2 == 1 ? col - 1 : cols - col;
                case TrayMappingMode.RowWise:
                    return col - 1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown tray mapping mode.");
            }
        }
    }
}
