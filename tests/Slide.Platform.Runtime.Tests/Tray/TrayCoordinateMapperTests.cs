using System;
using Slide.Platform.Runtime.Tray;
using Xunit;

namespace Slide.Platform.Runtime.Tests.Tray
{
    public class TrayCoordinateMapperTests
    {
        [Fact]
        public void IndexToPosition_SnakeMapping_HandlesOddEvenRows()
        {
            var rows = 2;
            var cols = 3;

            Assert.Equal((1, 1), TrayCoordinateMapper.IndexToPosition(0, rows, cols));
            Assert.Equal((1, 2), TrayCoordinateMapper.IndexToPosition(1, rows, cols));
            Assert.Equal((1, 3), TrayCoordinateMapper.IndexToPosition(2, rows, cols));
            Assert.Equal((2, 3), TrayCoordinateMapper.IndexToPosition(3, rows, cols));
            Assert.Equal((2, 2), TrayCoordinateMapper.IndexToPosition(4, rows, cols));
            Assert.Equal((2, 1), TrayCoordinateMapper.IndexToPosition(5, rows, cols));
        }

        [Fact]
        public void PositionToIndex_SnakeMapping_HandlesOddEvenRows()
        {
            var rows = 2;
            var cols = 3;

            Assert.Equal(0, TrayCoordinateMapper.PositionToIndex(1, 1, rows, cols));
            Assert.Equal(1, TrayCoordinateMapper.PositionToIndex(1, 2, rows, cols));
            Assert.Equal(2, TrayCoordinateMapper.PositionToIndex(1, 3, rows, cols));
            Assert.Equal(3, TrayCoordinateMapper.PositionToIndex(2, 3, rows, cols));
            Assert.Equal(4, TrayCoordinateMapper.PositionToIndex(2, 2, rows, cols));
            Assert.Equal(5, TrayCoordinateMapper.PositionToIndex(2, 1, rows, cols));
        }

        [Fact]
        public void IndexToPosition_RowWiseMapping_UsesSequentialRows()
        {
            var rows = 2;
            var cols = 3;

            Assert.Equal((1, 1), TrayCoordinateMapper.IndexToPosition(0, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal((1, 2), TrayCoordinateMapper.IndexToPosition(1, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal((1, 3), TrayCoordinateMapper.IndexToPosition(2, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal((2, 1), TrayCoordinateMapper.IndexToPosition(3, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal((2, 2), TrayCoordinateMapper.IndexToPosition(4, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal((2, 3), TrayCoordinateMapper.IndexToPosition(5, rows, cols, TrayMappingMode.RowWise));
        }

        [Fact]
        public void PositionToIndex_RowWiseMapping_UsesSequentialRows()
        {
            var rows = 2;
            var cols = 3;

            Assert.Equal(0, TrayCoordinateMapper.PositionToIndex(1, 1, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal(1, TrayCoordinateMapper.PositionToIndex(1, 2, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal(2, TrayCoordinateMapper.PositionToIndex(1, 3, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal(3, TrayCoordinateMapper.PositionToIndex(2, 1, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal(4, TrayCoordinateMapper.PositionToIndex(2, 2, rows, cols, TrayMappingMode.RowWise));
            Assert.Equal(5, TrayCoordinateMapper.PositionToIndex(2, 3, rows, cols, TrayMappingMode.RowWise));
        }

        [Fact]
        public void IndexToPosition_ThrowsForOutOfRangeIndex()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrayCoordinateMapper.IndexToPosition(6, 2, 3));

            Assert.Equal("index", exception.ParamName);
        }

        [Fact]
        public void PositionToIndex_ThrowsForOutOfRangeRowCol()
        {
            var rowException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrayCoordinateMapper.PositionToIndex(0, 1, 2, 3));
            Assert.Equal("row", rowException.ParamName);

            var colException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrayCoordinateMapper.PositionToIndex(1, 4, 2, 3));
            Assert.Equal("col", colException.ParamName);
        }

        [Fact]
        public void Mapping_RejectsInvalidDimensions()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrayCoordinateMapper.IndexToPosition(0, 0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TrayCoordinateMapper.PositionToIndex(1, 1, 3, 0));
        }
    }
}
