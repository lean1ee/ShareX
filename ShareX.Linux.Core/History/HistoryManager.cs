using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ShareX.Linux.Core.Helpers;

namespace ShareX.Linux.Core.History;

public class HistoryEntry
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? ThumbnailPath { get; set; }
    public string Type { get; set; } = "Image"; // Image, Video, GIF
    public long FileSizeBytes { get; set; }
}

public class HistoryManager
{
    private static readonly Lazy<HistoryManager> _instance = new(() => new HistoryManager());
    public static HistoryManager Instance => _instance.Value;

    private readonly string _connectionString;

    public HistoryManager()
    {
        var dbPath = PathsHelper.HistoryDbPath;
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS History (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    Url TEXT,
                    ThumbnailPath TEXT,
                    Type TEXT NOT NULL,
                    FileSizeBytes INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_history_timestamp ON History(Timestamp DESC);
            ";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HistoryManager] Failed to init DB: {ex.Message}");
        }
    }

    public async Task AddEntryAsync(HistoryEntry entry)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO History (Timestamp, FileName, FilePath, Url, ThumbnailPath, Type, FileSizeBytes)
                VALUES ($timestamp, $fileName, $filePath, $url, $thumbPath, $type, $size);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("$timestamp", entry.Timestamp.ToString("o"));
            cmd.Parameters.AddWithValue("$fileName", entry.FileName);
            cmd.Parameters.AddWithValue("$filePath", entry.FilePath);
            cmd.Parameters.AddWithValue("$url", (object?)entry.Url ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$thumbPath", (object?)entry.ThumbnailPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$type", entry.Type);
            cmd.Parameters.AddWithValue("$size", entry.FileSizeBytes);

            var id = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            if (id != null && long.TryParse(id.ToString(), out var longId))
            {
                entry.Id = longId;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HistoryManager] AddEntryAsync failed: {ex.Message}");
        }
    }

    public async Task<List<HistoryEntry>> GetRecentEntriesAsync(int limit = 100, int offset = 0)
    {
        var list = new List<HistoryEntry>();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Timestamp, FileName, FilePath, Url, ThumbnailPath, Type, FileSizeBytes
                FROM History
                ORDER BY Id DESC
                LIMIT $limit OFFSET $offset;
            ";
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.Parameters.AddWithValue("$offset", offset);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new HistoryEntry
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    FileName = reader.GetString(2),
                    FilePath = reader.GetString(3),
                    Url = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ThumbnailPath = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Type = reader.GetString(6),
                    FileSizeBytes = reader.GetInt64(7)
                });
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HistoryManager] GetRecentEntriesAsync failed: {ex.Message}");
        }
        return list;
    }

    public async Task<bool> DeleteEntryAsync(long id)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM History WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HistoryManager] DeleteEntryAsync failed: {ex.Message}");
            return false;
        }
    }
}
