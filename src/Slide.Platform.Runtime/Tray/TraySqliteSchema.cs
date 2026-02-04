using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Slide.Platform.Runtime.Tray
{
    public static class TraySqliteSchema
    {
        public const string TrayHeadersTable = "tray_headers";
        public const string TrayItemsTable = "tray_items";

        private static readonly string[] SchemaStatements =
        {
            "PRAGMA foreign_keys = ON;",
            $@"CREATE TABLE IF NOT EXISTS {TrayHeadersTable} (
    tray_id TEXT NOT NULL PRIMARY KEY,
    rows INTEGER NOT NULL,
    cols INTEGER NOT NULL,
    batch_name TEXT,
    created_at TEXT NOT NULL,
    completed_at TEXT
);",
            $@"CREATE TABLE IF NOT EXISTS {TrayItemsTable} (
    tray_id TEXT NOT NULL,
    row INTEGER NOT NULL,
    col INTEGER NOT NULL,
    result TEXT NOT NULL,
    image_path TEXT,
    detection_time TEXT NOT NULL,
    PRIMARY KEY (tray_id, row, col),
    FOREIGN KEY (tray_id) REFERENCES {TrayHeadersTable}(tray_id) ON DELETE CASCADE
);",
            $"CREATE INDEX IF NOT EXISTS idx_{TrayItemsTable}_tray_id ON {TrayItemsTable} (tray_id);",
            $"CREATE INDEX IF NOT EXISTS idx_{TrayItemsTable}_detection_time ON {TrayItemsTable} (detection_time);"
        };

        public static void EnsureCreated(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            using (var connection = new SqliteConnection(connectionString))
            {
                EnsureCreated(connection);
            }
        }

        public static void EnsureCreated(SqliteConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            var wasClosed = connection.State == ConnectionState.Closed;
            if (wasClosed)
            {
                connection.Open();
            }

            using (var transaction = connection.BeginTransaction())
            {
                foreach (var statement in SchemaStatements)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = statement;
                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }

            if (wasClosed)
            {
                connection.Close();
            }
        }
    }
}
