using System;
using Slide.Platform.Runtime.Tray;
using Xunit;

namespace Slide.Platform.Runtime.Tests.Tray
{
    public class TrayComponentTests
    {
        [Fact]
        public void UpdateResult_RaisesEventsAndPersists()
        {
            var repo = new TrayMemoryRepository();
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

        [Fact]
        public void UpdateResult_RaisesErrorForInvalidPosition()
        {
            var repo = new TrayMemoryRepository();
            var manager = new TrayDataManager();
            var component = new TrayComponent(manager, repo);

            var errorRaised = false;
            component.OnError += (_, __) => errorRaised = true;

            component.StartTray(1, 1, "batch-a");

            Assert.Throws<ArgumentException>(() =>
                component.UpdateResult("", "OK", null, DateTime.UtcNow));
            Assert.True(errorRaised);
        }

        [Fact]
        public void RequestManualRetest_RaisesEvent()
        {
            var repo = new TrayMemoryRepository();
            var manager = new TrayDataManager();
            var component = new TrayComponent(manager, repo);
            component.StartTray(2, 2, "batch-a");

            TrayRetestEventArgs received = null;
            component.OnManualRetestRequested += (_, args) => received = args;

            component.RequestManualRetest("1_2");

            Assert.NotNull(received);
            Assert.Equal(1, received.Position.Row);
            Assert.Equal(2, received.Position.Col);
        }
    }
}
