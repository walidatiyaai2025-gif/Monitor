using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed record SqlTransactionLogDatabaseRow(
    string DatabaseKey,
    string RecoveryModel,
    long? TotalLogSizeBytes,
    long? ActiveLogSizeBytes,
    long? TotalVlfCount,
    long? ActiveVlfCount,
    string? ReuseWait,
    long? LogBackupAgeSeconds,
    bool HasDetailedStats);

internal sealed record SqlTransactionLogRow(
    int TotalDatabases,
    IReadOnlyList<SqlTransactionLogDatabaseRow>? Databases = null);

internal static class TransactionLogEvidenceMapper
{
    public static TransactionLogHealthSnapshot Map(SqlTransactionLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var databases = row.Databases ?? [];
        var expectedRows = Math.Min(row.TotalDatabases, TransactionLogSnapshotQuery.MaxDatabases);
        if (row.TotalDatabases < 0 ||
            databases.Count != expectedRows ||
            databases.Select(database => database.DatabaseKey).Distinct(StringComparer.Ordinal).Count() != databases.Count ||
            databases.Any(IsInvalid))
        {
            throw new InvalidDataException("Invalid transaction-log evidence row.");
        }

        return new TransactionLogHealthSnapshot(
            row.TotalDatabases,
            databases.Select(database => new TransactionLogDatabaseSnapshot(
                database.DatabaseKey,
                database.RecoveryModel,
                database.TotalLogSizeBytes,
                database.ActiveLogSizeBytes,
                database.TotalVlfCount,
                database.ActiveVlfCount,
                database.ReuseWait,
                database.LogBackupAgeSeconds,
                database.HasDetailedStats)).ToArray(),
            row.TotalDatabases > TransactionLogSnapshotQuery.MaxDatabases);
    }

    private static bool IsInvalid(SqlTransactionLogDatabaseRow database)
    {
        var hasAllDetailedStats = database.TotalLogSizeBytes.HasValue &&
                                  database.ActiveLogSizeBytes.HasValue &&
                                  database.TotalVlfCount.HasValue &&
                                  database.ActiveVlfCount.HasValue &&
                                  !string.IsNullOrWhiteSpace(database.ReuseWait);

        return string.IsNullOrWhiteSpace(database.DatabaseKey) ||
               database.DatabaseKey.Length > 128 ||
               string.IsNullOrWhiteSpace(database.RecoveryModel) ||
               database.RecoveryModel.Length > 60 ||
               database.TotalLogSizeBytes is <= 0 ||
               database.ActiveLogSizeBytes is < 0 ||
               (database.TotalLogSizeBytes.HasValue && database.ActiveLogSizeBytes > database.TotalLogSizeBytes) ||
               database.TotalVlfCount is <= 0 ||
               database.ActiveVlfCount is < 0 ||
               (database.TotalVlfCount.HasValue && database.ActiveVlfCount > database.TotalVlfCount) ||
               (database.ReuseWait?.Length ?? 0) > 60 ||
               database.LogBackupAgeSeconds is < 0 ||
               database.HasDetailedStats != hasAllDetailedStats;
    }
}

internal sealed class TransactionLogSnapshotQuery(PerformanceScaleOptions? performance = null)
{
    internal const int MaxDatabases = 50;
    internal const string CommandText = """
        DECLARE @TransactionLogEvidence TABLE
        (
            DatabaseKey nvarchar(128) NOT NULL,
            RecoveryModel nvarchar(60) NOT NULL,
            TotalLogSizeBytes bigint NULL,
            ActiveLogSizeBytes bigint NULL,
            TotalVlfCount bigint NULL,
            ActiveVlfCount bigint NULL,
            ReuseWait nvarchar(60) NULL,
            LogBackupAgeSeconds bigint NULL,
            HasDetailedStats bit NOT NULL
        );

        INSERT INTO @TransactionLogEvidence
        (
            DatabaseKey,
            RecoveryModel,
            TotalLogSizeBytes,
            ActiveLogSizeBytes,
            TotalVlfCount,
            ActiveVlfCount,
            ReuseWait,
            LogBackupAgeSeconds,
            HasDetailedStats
        )
        SELECT
            CONVERT(nvarchar(128), d.name) AS DatabaseKey,
            CONVERT(nvarchar(60), COALESCE(ls.recovery_model, d.recovery_model_desc)) AS RecoveryModel,
            TRY_CONVERT(bigint, ROUND(ls.total_log_size_mb * 1048576.0, 0)) AS TotalLogSizeBytes,
            TRY_CONVERT(bigint, ROUND(ls.active_log_size_mb * 1048576.0, 0)) AS ActiveLogSizeBytes,
            CONVERT(bigint, ls.total_vlf_count) AS TotalVlfCount,
            CONVERT(bigint, ls.active_vlf_count) AS ActiveVlfCount,
            CONVERT(nvarchar(60), ls.log_truncation_holdup_reason) AS ReuseWait,
            CASE
                WHEN ls.log_backup_time IS NULL THEN NULL
                WHEN ls.log_backup_time > SYSDATETIME() THEN CONVERT(bigint, 0)
                ELSE CONVERT(bigint, DATEDIFF_BIG(SECOND, ls.log_backup_time, SYSDATETIME()))
            END AS LogBackupAgeSeconds,
            CONVERT(bit, CASE
                WHEN ls.total_log_size_mb IS NOT NULL
                 AND ls.active_log_size_mb IS NOT NULL
                 AND ls.total_vlf_count IS NOT NULL
                 AND ls.active_vlf_count IS NOT NULL
                 AND ls.log_truncation_holdup_reason IS NOT NULL
                THEN 1 ELSE 0 END) AS HasDetailedStats
        FROM sys.databases AS d
        OUTER APPLY sys.dm_db_log_stats(d.database_id) AS ls
        WHERE d.database_id > 4 AND d.state = 0;

        SELECT
            (SELECT COUNT(*) FROM @TransactionLogEvidence) AS TotalDatabases,
            (SELECT TOP (50)
                  DatabaseKey,
                  RecoveryModel,
                  TotalLogSizeBytes,
                  ActiveLogSizeBytes,
                  TotalVlfCount,
                  ActiveVlfCount,
                  ReuseWait,
                  LogBackupAgeSeconds,
                  HasDetailedStats
              FROM @TransactionLogEvidence
              ORDER BY CASE
                           WHEN ReuseWait IS NULL THEN 2
                           WHEN ReuseWait IN (N'NOTHING', N'CHECKPOINT') THEN 1
                           ELSE 0
                       END ASC,
                       CASE
                           WHEN TotalLogSizeBytes > 0 AND ActiveLogSizeBytes IS NOT NULL
                           THEN CONVERT(float, ActiveLogSizeBytes) / CONVERT(float, TotalLogSizeBytes)
                           ELSE -1
                       END DESC,
                       DatabaseKey ASC
              FOR JSON PATH) AS TransactionLogsJson;
        """;

    public async Task<SqlTransactionLogRow> ExecuteAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = SqlConnectionStringFactory.Create(
                registration,
                secret,
                "Monitor/TransactionLogEvidence",
                performance);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = CommandText;
            command.CommandTimeout = 2;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidDataException("Transaction-log evidence query returned no row.");
            }

            return new SqlTransactionLogRow(
                reader.GetInt32(0),
                ReadDatabases(reader, 1));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }

    private static IReadOnlyList<SqlTransactionLogDatabaseRow> ReadDatabases(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        var json = reader.GetString(ordinal);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<SqlTransactionLogDatabaseRow[]>(json) ?? [];
    }
}
