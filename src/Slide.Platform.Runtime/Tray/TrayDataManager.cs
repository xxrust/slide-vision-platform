using System;
using System.Collections.Generic;

namespace Slide.Platform.Runtime.Tray
{
    public sealed class TrayDataManager
    {
        private readonly List<TrayData> _history = new List<TrayData>();

        public TrayData CurrentTray { get; private set; }

        public TrayData CreateTray(int rows, int cols, string batchName = null, string trayId = null, DateTime? createdAt = null)
        {
            var resolvedTrayId = string.IsNullOrWhiteSpace(trayId) ? Guid.NewGuid().ToString("N") : trayId;
            var tray = new TrayData(resolvedTrayId, rows, cols, batchName, createdAt ?? DateTime.UtcNow);
            CurrentTray = tray;
            return tray;
        }

        public MaterialData UpdateResult(int row, int col, string result, string imagePath, DateTime detectionTime)
        {
            if (CurrentTray == null)
            {
                throw new InvalidOperationException("No active tray. Call CreateTray first.");
            }

            if (row < 1 || row > CurrentTray.Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), $"Row must be between 1 and {CurrentTray.Rows}.");
            }

            if (col < 1 || col > CurrentTray.Cols)
            {
                throw new ArgumentOutOfRangeException(nameof(col), $"Col must be between 1 and {CurrentTray.Cols}.");
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                throw new ArgumentException("Result is required.", nameof(result));
            }

            var material = new MaterialData(row, col, result, imagePath, detectionTime);
            CurrentTray.AddOrUpdateMaterial(material);

            if (CurrentTray.MaterialCount >= CurrentTray.TotalSlots)
            {
                CompleteCurrentTray(DateTime.UtcNow);
            }

            return material;
        }

        public TrayData CompleteCurrentTray(DateTime? completedAt = null)
        {
            if (CurrentTray == null)
            {
                return null;
            }

            CurrentTray.CompletedAt = completedAt ?? DateTime.UtcNow;
            _history.Insert(0, CurrentTray);
            var completed = CurrentTray;
            CurrentTray = null;
            return completed;
        }

        public void ResetCurrentTray()
        {
            CurrentTray = null;
        }

        public TrayStatistics GetStatistics()
        {
            return TrayStatistics.FromTray(CurrentTray);
        }

        public IReadOnlyList<TrayData> GetHistory(int limit)
        {
            if (limit <= 0)
            {
                return Array.Empty<TrayData>();
            }

            var count = Math.Min(limit, _history.Count);
            return _history.GetRange(0, count).AsReadOnly();
        }
    }
}
