using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Monitor.Web.Services;

internal sealed record SqlTempDbFileRow(
    int FileId,
    string FileKey,
    long SizeBytes,
    long? UsedBytes,
    long Reads,
    long Writes,
    long ReadStallMs,
    long WriteStallMs);

internal sealed record SqlTempDbRow(
    int LogicalCpuCount,
    int TotalDataFiles,
    IReadOnlyList<SqlTempDbFileRow>? DataFiles = null);

internal sealed class TempDbSnapshotQuery(PerformanceScaleOptions? performance = null)
{
    internal const int MaxFiles = 32;
    internal const string CommandText = """
        SELECT
            (SELECT TOP (1) CONVERT(int, cpu_count) FROM sys.dm_os_sys_info) AS LogicalCpuCount,
            (SELECT COUNT(*) FROM tempdb.sys.database_files WHERE type = 0) AS TotalDataFiles,
            (SELECT TOP (32)
                  CONVERT(int, df.file_id) AS FileId,
                  CONVERT(nvarchar(128), df.name) AS FileKey,
                  CONVERT(bigint, df.size) * 8192 AS SizeBytes,
                  CASE
                      WHEN fs.file_id IS NULL THEN NULL
                      ELSE (CONVERT(bigint, df.size) - CONVERT(bigint, fs.unallocated_extent_page_count)) * 8192
                  END AS UsedBytes,
                  CONVERT(bigint, COALESCE(vfs.num_of_reads, 0)) AS Reads,
                  CONVERT(bigint, COALESCE(vfs.num_of_writes, 0)) AS Writes,
                  CONVERT(bigint, COALESCE(vfs.io_stall_read_ms, 0)) AS ReadStallMs,
                  CONVERT(bigint, COALESCE(vfs.io_stall_write_ms, 0)) AS WriteStallMs
              FROM tempdb.sys.database_files AS df
              LEFT JOIN tempdb.sys.dm_db_file_space_usage AS fs ON fs.file_id = df.file_id
              LEFT JOIN sys.dm_io_virtual_file_stats(DB_ID(N'tempdb'), NULL) AS vfs ON vfs.file_id = df.file_id
              WHERE df.type = 0
              ORDER BY df.file_id ASC
              FOR JSON PATH) AS TempDbFilesJson;
        """;

    public async Task<SqlTempDbRow> ExecuteAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = SqlConnectionStringFactory.Create(
                registration,
                secret,
                "Monitor/TempDbEvidence",
                performance);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = CommandText;
            command.CommandTimeout = 2;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidDataException("TempDB evidence query returned no row.");
            }

            return new SqlTempDbRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                ReadFiles(reader, 2));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }

    private static IReadOnlyList<SqlTempDbFileRow> ReadFiles(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        var json = reader.GetString(ordinal);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<SqlTempDbFileRow[]>(json) ?? [];
    }
}
