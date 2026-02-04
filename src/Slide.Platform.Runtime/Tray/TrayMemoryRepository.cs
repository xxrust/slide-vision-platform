using System;
using System.Collections.Generic;
using System.Linq;

namespace Slide.Platform.Runtime.Tray
{
    public sealed class TrayMemoryRepository : ITrayRepository
    {
        private readonly Dictionary<string, TrayData> _trays = new Dictionary<string, TrayData>(StringComparer.OrdinalIgnoreCase);

        public void SaveTrayHeader(TrayData tray)
        {
            if (tray == null)
            {
                throw new ArgumentNullException(nameof(tray));
            }

            if (_trays.TryGetValue(tray.TrayId, out var existing))
            {
                CopyTrayMetadata(tray, existing);
                return;
            }

            _trays[tray.TrayId] = CloneTray(tray);
        }

        public void UpdateTrayCompletion(string trayId, DateTime completedAt)
        {
            if (string.IsNullOrWhiteSpace(trayId))
            {
                throw new ArgumentException("Tray id is required.", nameof(trayId));
            }

            if (!_trays.TryGetValue(trayId, out var tray))
            {
                throw new InvalidOperationException($"Tray {trayId} not found.");
            }

            tray.CompletedAt = completedAt;
        }

        public void SaveMaterial(string trayId, MaterialData material)
        {
            if (string.IsNullOrWhiteSpace(trayId))
            {
                throw new ArgumentException("Tray id is required.", nameof(trayId));
            }

            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            if (!_trays.TryGetValue(trayId, out var tray))
            {
                throw new InvalidOperationException($"Tray {trayId} not found.");
            }

            tray.AddOrUpdateMaterial(new MaterialData(material.Row, material.Col, material.Result, material.ImagePath, material.DetectionTime));
        }

        public IReadOnlyList<TrayData> LoadRecentTrays(int limit)
        {
            if (limit <= 0)
            {
                return Array.Empty<TrayData>();
            }

            return _trays.Values
                .OrderByDescending(tray => tray.CreatedAt)
                .Take(limit)
                .Select(CloneTray)
                .ToList();
        }

        private static void CopyTrayMetadata(TrayData source, TrayData target)
        {
            if (source.CompletedAt.HasValue)
            {
                target.CompletedAt = source.CompletedAt.Value;
            }
        }

        private static TrayData CloneTray(TrayData tray)
        {
            var clone = new TrayData(tray.TrayId, tray.Rows, tray.Cols, tray.BatchName, tray.CreatedAt)
            {
                CompletedAt = tray.CompletedAt
            };

            foreach (var material in tray.Materials.OrderBy(item => item?.DetectionTime ?? DateTime.MinValue))
            {
                if (material == null)
                {
                    continue;
                }

                clone.AddOrUpdateMaterial(new MaterialData(material.Row, material.Col, material.Result, material.ImagePath, material.DetectionTime));
            }

            return clone;
        }
    }
}
