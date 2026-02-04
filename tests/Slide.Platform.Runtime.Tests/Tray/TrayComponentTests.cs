using System;
using System.IO;
using Slide.Platform.Runtime.Tray;
using Xunit;

namespace Slide.Platform.Runtime.Tests.Tray
{
    public class TrayComponentTests
    {
        [Fact]
        public void UpdateResult_RaisesEventsAndPersists()
        {
            var path = CreateTempDbPath();

            try
            {
                var repo = new TraySqliteRepository($"Data Source={path}");
                var manager = new TrayDataManager();
                var component = new TrayComponent(manager, repo);

                TrayResultEventArgs lastProcessed = null;
                TrayCompletedEventArgs completedArgs = null;

                component.OnResultProcessed += (_, args) => lastProcessed = args;
                component.OnTrayCompleted += (_, args) => completedArgs = args;

                var tray = component.StartTray(1, 2, "batch-a");
                component.UpdateResult("1_1", "OK", "ok.png", new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc));
                component.UpdateResult("1_2", "NG", "ng.png", new DateTime(2026, 1, 1, 1, 5, 0, DateTimeKind.Utc));

                Assert.NotNull(lastProcessed);
                Assert.Equal(1, lastProcessed.Position?.Row);
                Assert.Equal(2, lastProcessed.Position?.Col);
                Assert.Equal("NG", lastProcessed.Result);

                Assert.NotNull(completedArgs);
                Assert.Equal(tray.TrayId, completedArgs.Tray.TrayId);

                var history = component.GetHistory(1);
                Assert.Single(history);
                Assert.Equal(tray.TrayId, history[0].TrayId);
                Assert.NotNull(history[0].CompletedAt);
            }
            finally
            {
                DeleteTempDb(path);
            }
        }

        [Fact]
        public void UpdateResult_RaisesErrorForInvalidPosition()
        {
            var path = CreateTempDbPath();

            try
            {
                var repo = new TraySqliteRepository($"Data Source={path}");
                var manager = new TrayDataManager();
                var component = new TrayComponent(manager, repo);

                var errorRaised = false;
                component.OnError += (_, __) => errorRaised = true;

                component.StartTray(1, 1, "batch-a");

                Assert.Throws<ArgumentException>(() =>
                    component.UpdateResult("", "OK", null, DateTime.UtcNow));
                Assert.True(errorRaised);
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
