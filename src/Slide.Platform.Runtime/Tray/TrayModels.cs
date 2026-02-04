using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Slide.Platform.Runtime.Tray
{
    public sealed class TrayData
    {
        private readonly Dictionary<(int Row, int Col), MaterialData> _materials;

        public TrayData(string trayId, int rows, int cols, string batchName, DateTime createdAt)
        {
            if (string.IsNullOrWhiteSpace(trayId))
            {
                throw new ArgumentException("Tray id is required.", nameof(trayId));
            }

            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be positive.");
            }

            if (cols <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cols), "Cols must be positive.");
            }

            TrayId = trayId;
            Rows = rows;
            Cols = cols;
            BatchName = batchName;
            CreatedAt = createdAt;
            _materials = new Dictionary<(int Row, int Col), MaterialData>();
        }

        public string TrayId { get; }
        public int Rows { get; }
        public int Cols { get; }
        public string BatchName { get; }
        public DateTime CreatedAt { get; }
        public DateTime? CompletedAt { get; internal set; }
        public IReadOnlyCollection<MaterialData> Materials => _materials.Values;
        public int TotalSlots => Rows * Cols;

        public bool TryGetMaterial(int row, int col, out MaterialData material)
        {
            return _materials.TryGetValue((row, col), out material);
        }

        internal int MaterialCount => _materials.Count;

        internal MaterialData AddOrUpdateMaterial(MaterialData material)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            _materials[(material.Row, material.Col)] = material;
            return material;
        }
    }

    public sealed class MaterialData
    {
        public MaterialData(int row, int col, string result, string imagePath, DateTime detectionTime)
        {
            Row = row;
            Col = col;
            Result = result ?? throw new ArgumentNullException(nameof(result));
            ImagePath = imagePath;
            DetectionTime = detectionTime;
        }

        public int Row { get; }
        public int Col { get; }
        public string Result { get; }
        public string ImagePath { get; }
        public DateTime DetectionTime { get; }
    }

    public sealed class TrayStatistics
    {
        public TrayStatistics(int totalSlots, int inspectedCount, int okCount, int ngCount, double yieldRate, IReadOnlyDictionary<string, int> defectCounts)
        {
            TotalSlots = totalSlots;
            InspectedCount = inspectedCount;
            OkCount = okCount;
            NgCount = ngCount;
            YieldRate = yieldRate;
            DefectCounts = defectCounts ?? new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
        }

        public int TotalSlots { get; }
        public int InspectedCount { get; }
        public int OkCount { get; }
        public int NgCount { get; }
        public double YieldRate { get; }
        public IReadOnlyDictionary<string, int> DefectCounts { get; }

        public static TrayStatistics Empty => new TrayStatistics(0, 0, 0, 0, 0d, new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()));

        public static TrayStatistics FromTray(TrayData tray)
        {
            if (tray == null)
            {
                return Empty;
            }

            var defects = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var inspected = 0;
            var okCount = 0;

            foreach (var material in tray.Materials)
            {
                if (material == null)
                {
                    continue;
                }

                inspected++;
                if (IsOkResult(material.Result))
                {
                    okCount++;
                }
                else
                {
                    var defectKey = string.IsNullOrWhiteSpace(material.Result) ? "NG" : material.Result;
                    if (defects.TryGetValue(defectKey, out var count))
                    {
                        defects[defectKey] = count + 1;
                    }
                    else
                    {
                        defects[defectKey] = 1;
                    }
                }
            }

            var ngCount = inspected - okCount;
            var yieldRate = inspected == 0 ? 0d : (double)okCount / inspected;
            return new TrayStatistics(tray.TotalSlots, inspected, okCount, ngCount, yieldRate, new ReadOnlyDictionary<string, int>(defects));
        }

        private static bool IsOkResult(string result)
        {
            return string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase);
        }
    }
}
