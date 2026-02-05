using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Slide.Platform.Runtime.Tray
{
    public sealed class TraySqliteRepository : ITrayRepository
    {
        private readonly string _connectionString;

        public TraySqliteRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            _connectionString = connectionString;
            TraySqliteSchema.EnsureCreated(_connectionString);
        }

        public void SaveTrayHeader(TrayData tray)
        {
            if (tray == null)
            {
                throw new ArgumentNullException(nameof(tray));
            }

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                TraySqliteSchema.EnsureCreated(connection);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
INSERT INTO {TraySqliteSchema.TrayHeadersTable} (tray_id, rows, cols, batch_name, created_at, completed_at)
VALUES ($tray_id, $rows, $cols, $batch_name, $created_at, $completed_at)
ON CONFLICT(tray_id) DO UPDATE SET
    rows = excluded.rows,
    cols = excluded.cols,
    batch_name = excluded.batch_name,
    created_at = excluded.created_at,
    completed_at = excluded.completed_at;";
                    command.Parameters.AddWithValue("$tray_id", tray.TrayId);
                    command.Parameters.AddWithValue("$rows", tray.Rows);
                    command.Parameters.AddWithValue("$cols", tray.Cols);
                    command.Parameters.AddWithValue("$batch_name", (object)tray.BatchName ?? DBNull.Value);
                    command.Parameters.AddWithValue("$created_at", FormatDateTime(tray.CreatedAt));
                    command.Parameters.AddWithValue("$completed_at", tray.CompletedAt.HasValue ? FormatDateTime(tray.CompletedAt.Value) : (object)DBNull.Value);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void UpdateTrayCompletion(string trayId, DateTime completedAt)
        {
            if (string.IsNullOrWhiteSpace(trayId))
            {
                throw new ArgumentException("Tray id is required.", nameof(trayId));
            }

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                TraySqliteSchema.EnsureCreated(connection);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
UPDATE {TraySqliteSchema.TrayHeadersTable}
SET completed_at = $completed_at
WHERE tray_id = $tray_id;";
                    command.Parameters.AddWithValue("$tray_id", trayId);
                    command.Parameters.AddWithValue("$completed_at", FormatDateTime(completedAt));
                    command.ExecuteNonQuery();
                }
            }
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

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                TraySqliteSchema.EnsureCreated(connection);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
INSERT INTO {TraySqliteSchema.TrayItemsTable} (tray_id, row, col, result, image_path, detection_time)
VALUES ($tray_id, $row, $col, $result, $image_path, $detection_time)
ON CONFLICT(tray_id, row, col) DO UPDATE SET
    result = excluded.result,
    image_path = excluded.image_path,
    detection_time = excluded.detection_time;";
                    command.Parameters.AddWithValue("$tray_id", trayId);
                    command.Parameters.AddWithValue("$row", material.Row);
                    command.Parameters.AddWithValue("$col", material.Col);
                    command.Parameters.AddWithValue("$result", material.Result);
                    command.Parameters.AddWithValue("$image_path", (object)material.ImagePath ?? DBNull.Value);
                    command.Parameters.AddWithValue("$detection_time", FormatDateTime(material.DetectionTime));
                    command.ExecuteNonQuery();
                }
            }
        }

        public IReadOnlyList<TrayData> LoadRecentTrays(int limit)
        {
            if (limit <= 0)
            {
                return Array.Empty<TrayData>();
            }

            var trays = new List<TrayData>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                TraySqliteSchema.EnsureCreated(connection);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
SELECT tray_id, rows, cols, batch_name, created_at, completed_at
FROM {TraySqliteSchema.TrayHeadersTable}
ORDER BY created_at DESC
LIMIT $limit;";
                    command.Parameters.AddWithValue("$limit", limit);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var trayId = reader.GetString(0);
                            var rows = reader.GetInt32(1);
                            var cols = reader.GetInt32(2);
                            var batchName = reader.IsDBNull(3) ? null : reader.GetString(3);
                            var createdAt = ParseDateTime(reader.GetString(4));
                            var tray = new TrayData(trayId, rows, cols, batchName, createdAt);
                            tray.CompletedAt = reader.IsDBNull(5) ? (DateTime?)null : ParseDateTime(reader.GetString(5));
                            trays.Add(tray);
                        }
                    }
                }

                if (trays.Count == 0)
                {
                    return trays;
                }

                var trayLookup = trays.ToDictionary(tray => tray.TrayId, tray => tray, StringComparer.OrdinalIgnoreCase);
                var trayIdParams = new List<string>();

                using (var itemCommand = connection.CreateCommand())
                {
                    var index = 0;
                    foreach (var trayId in trayLookup.Keys)
                    {
                        var paramName = $"$tray_id_{index}";
                        trayIdParams.Add(paramName);
                        itemCommand.Parameters.AddWithValue(paramName, trayId);
                        index++;
                    }

                    itemCommand.CommandText = $@"
SELECT tray_id, row, col, result, image_path, detection_time
FROM {TraySqliteSchema.TrayItemsTable}
WHERE tray_id IN ({string.Join(", ", trayIdParams)})
ORDER BY detection_time ASC;";

                    using (var reader = itemCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var trayId = reader.GetString(0);
                            if (!trayLookup.TryGetValue(trayId, out var tray))
                            {
                                continue;
                            }

                            var row = reader.GetInt32(1);
                            var col = reader.GetInt32(2);
                            var result = reader.GetString(3);
                            var imagePath = reader.IsDBNull(4) ? null : reader.GetString(4);
                            var detectionTime = ParseDateTime(reader.GetString(5));
                            tray.AddOrUpdateMaterial(new MaterialData(row, col, result, imagePath, detectionTime));
                        }
                    }
                }
            }

            return trays;
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static DateTime ParseDateTime(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
    }
}
