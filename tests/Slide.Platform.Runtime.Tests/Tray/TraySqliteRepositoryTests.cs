using System;
using System.IO;
using Slide.Platform.Runtime.Tray;
using Xunit;

namespace Slide.Platform.Runtime.Tests.Tray
{
    public class TraySqliteRepositoryTests
    {
        [Fact]
        public void SaveAndLoadTray_PersistsHeaderAndItems()
        {
            var path = CreateTempDbPath();

            try
            {
                var repo = new TraySqliteRepository($"Data Source={path}");
                var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var tray = new TrayData("tray-1", 2, 2, "batch-a", createdAt);

                repo.SaveTrayHeader(tray);
                repo.SaveMaterial(tray.TrayId, new MaterialData(1, 1, "OK", "ok.png", new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc)));
                repo.SaveMaterial(tray.TrayId, new MaterialData(1, 2, "NG", "ng.png", new DateTime(2026, 1, 1, 1, 5, 0, DateTimeKind.Utc)));

                var completedAt = new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc);
                repo.UpdateTrayCompletion(tray.TrayId, completedAt);

                var trays = repo.LoadRecentTrays(5);

                Assert.Single(trays);
                var loaded = trays[0];
                Assert.Equal("tray-1", loaded.TrayId);
                Assert.Equal(2, loaded.Rows);
                Assert.Equal(2, loaded.Cols);
                Assert.Equal("batch-a", loaded.BatchName);
                Assert.Equal(createdAt, loaded.CreatedAt);
                Assert.Equal(completedAt, loaded.CompletedAt);
                Assert.True(loaded.TryGetMaterial(1, 1, out var okMaterial));
                Assert.Equal("OK", okMaterial.Result);
                Assert.True(loaded.TryGetMaterial(1, 2, out var ngMaterial));
                Assert.Equal("NG", ngMaterial.Result);
            }
            finally
            {
                DeleteTempDb(path);
            }
        }

        [Fact]
        public void SaveMaterial_UpdatesExistingPosition()
        {
            var path = CreateTempDbPath();

            try
            {
                var repo = new TraySqliteRepository($"Data Source={path}");
                var tray = new TrayData("tray-1", 1, 1, "batch-a", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                repo.SaveTrayHeader(tray);

                repo.SaveMaterial(tray.TrayId, new MaterialData(1, 1, "OK", null, new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc)));
                repo.SaveMaterial(tray.TrayId, new MaterialData(1, 1, "NG", "ng.png", new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc)));

                var trays = repo.LoadRecentTrays(1);
                Assert.Single(trays);
                Assert.True(trays[0].TryGetMaterial(1, 1, out var material));
                Assert.Equal("NG", material.Result);
                Assert.Equal("ng.png", material.ImagePath);
            }
            finally
            {
                DeleteTempDb(path);
            }
        }

        [Fact]
        public void LoadRecentTrays_RespectsLimitAndOrder()
        {
            var path = CreateTempDbPath();

            try
            {
                var repo = new TraySqliteRepository($"Data Source={path}");
                var older = new TrayData("tray-1", 1, 1, "batch-a", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                var newer = new TrayData("tray-2", 1, 1, "batch-b", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

                repo.SaveTrayHeader(older);
                repo.SaveTrayHeader(newer);

                var trays = repo.LoadRecentTrays(1);

                Assert.Single(trays);
                Assert.Equal("tray-2", trays[0].TrayId);
            }
            finally
            {
                DeleteTempDb(path);
            }
        }

        private static string CreateTempDbPath()
        {
            return Path.Combine(Path.GetTempPath(), $"tray-{Guid.NewGuid():N}.db");
        }

        private static void DeleteTempDb(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup failures for temp test files.
            }
        }
    }
}
