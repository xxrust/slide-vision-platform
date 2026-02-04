using System;
using Slide.Platform.Runtime.Tray;
using Xunit;

namespace Slide.Platform.Runtime.Tests.Tray
{
    public class TrayDataManagerTests
    {
        [Fact]
        public void CreateTray_SetsCurrentTray()
        {
            var manager = new TrayDataManager();

            var tray = manager.CreateTray(2, 3, "batch-a", "tray-1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.NotNull(manager.CurrentTray);
            Assert.Equal("tray-1", tray.TrayId);
            Assert.Equal(2, tray.Rows);
            Assert.Equal(3, tray.Cols);
            Assert.Equal("batch-a", tray.BatchName);
            Assert.Equal(6, tray.TotalSlots);
        }

        [Fact]
        public void UpdateResult_UpdatesStatistics()
        {
            var manager = new TrayDataManager();
            manager.CreateTray(2, 2, "batch-a", "tray-1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            manager.UpdateResult(1, 1, "OK", "ok.png", new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc));
            manager.UpdateResult(1, 2, "NG", "ng.png", new DateTime(2026, 1, 1, 1, 1, 0, DateTimeKind.Utc));

            var stats = manager.GetStatistics();

            Assert.Equal(4, stats.TotalSlots);
            Assert.Equal(2, stats.InspectedCount);
            Assert.Equal(1, stats.OkCount);
            Assert.Equal(1, stats.NgCount);
            Assert.Equal(0.5, stats.YieldRate, 3);
            Assert.True(stats.DefectCounts.ContainsKey("NG"));
            Assert.Equal(1, stats.DefectCounts["NG"]);
        }

        [Fact]
        public void UpdateResult_AutoCompletesTrayWhenFull()
        {
            var manager = new TrayDataManager();
            manager.CreateTray(1, 1, "batch-a", "tray-1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            manager.UpdateResult(1, 1, "OK", null, new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc));

            Assert.Null(manager.CurrentTray);
            var history = manager.GetHistory(1);
            Assert.Single(history);
            Assert.NotNull(history[0].CompletedAt);
        }

        [Fact]
        public void ResetCurrentTray_ClearsCurrentWithoutHistory()
        {
            var manager = new TrayDataManager();
            manager.CreateTray(1, 1, "batch-a", "tray-1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            manager.ResetCurrentTray();

            Assert.Null(manager.CurrentTray);
            Assert.Empty(manager.GetHistory(10));
        }

        [Fact]
        public void UpdateResult_ThrowsWhenOutOfBounds()
        {
            var manager = new TrayDataManager();
            manager.CreateTray(2, 2, "batch-a", "tray-1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                manager.UpdateResult(3, 1, "OK", null, DateTime.UtcNow));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                manager.UpdateResult(1, 3, "OK", null, DateTime.UtcNow));
        }
    }
}
