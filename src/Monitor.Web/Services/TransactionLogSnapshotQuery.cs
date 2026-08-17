using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed record SqlTransactionLogDatabaseRow(
    string DatabaseName,
    string RecoveryModel,
    string LogReuseWait,
    long TotalLogBytes,
    long? FullBackupAgeSeconds,
    long? LogBackupAgeSeconds);

internal sealed record SqlTransactionLogRow(
    int TotalUserDatabases,
    IReadOnlyList<SqlTransactionLogDatabaseRow>? Databases = null);

internal static class TransactionLogEvidenceMapper
{
    public static TransactionLogHealthSnapshot Map(SqlTransactionLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var databases = row.Databases ?? [];
        var expectedRows = Math.Min(row.TotalUserDatabases, TransactionLogSnapshotQuery.MaxDatabases);
        if (row.TotalUserDatabases < 0 ||
            databases.Count != expectedRows ||
            databases.Select(database => database.DatabaseName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != databases.Count ||
            databases.Any(database =>
                string.IsNullOrWhiteSpace(database.DatabaseName) ||
                database.DatabaseName.Length > 128 ||
                string.IsNullOrWhiteSpace(database.RecoveryModel) ||
                database.RecoveryModel.Length > 60 ||
                string.IsNullOrWhiteSpace(database.LogReuseWait) ||
                database.LogReuseWait.Length > 60 ||
                database.TotalLogBytes <= 0 ||
                database.FullBackupAgeSeconds is < 0 ||
                database.LogBackupAgeSeconds is < 0))
        {
            throw new InvalidDataException("Invalid transaction-log evidence row.");
        }

        return new TransactionLogHealthSnapshot(
            row.TotalUserDatabases,
            databases.Select(database => new TransactionLogDatabaseSnapshot(
                database.DatabaseName,
                database.RecoveryModel,
                database.LogReuseWait,
                database.TotalLogBytes,
                database.FullBackupAgeSeconds,
                database.LogBackupAgeSeconds)).ToArray(),
            row.TotalUserDatabases > TransactionLogSnapshotQuery.MaxDatabases);
    }
}

internal sealed class TransactionLogSnapshotQuery(PerformanceScaleOptions? performance = null)
{
    internal const int MaxDatabases = 50;
    internal const string CommandText = """
        SELECT
            (SELECT COUNT(*) FROM sys.databases WHERE database_id > 4) AS TotalUserDatabases,
            (SELECT TOP (50)
                  CONVERT(nvarchar(128), d.name) AS DatabaseName,
                  CONVERT(nvarchar(60), d.recovery_model_desc) AS RecoveryModel,
                  CONVERT(nvarchar(60), d.log_reuse_wait_desc) AS LogReuseWait,
                  CONVERT(bigint, COALESCE(log_size.TotalLogBytes, 0)) AS TotalLogBytes,
                  CASE WHEN backups.LastFullBackupLocal IS NULL THEN NULL
                       ELSE CONVERT(bigint, DATEDIFF_BIG(SECOND, backups.LastFullBackupLocal, GETDATE())) END AS FullBackupAgeSeconds,
                  CASE WHEN backups.LastLogBackupLocal IS NULL THEN NULL
                       ELSE CONVERT(bigint, DATEDIFF_BIG(SECOND, backups.LastLogBackupLocal, GETDATE())) END AS LogBackupAgeSeconds
              FROM sys.databases AS d
              OUTER APPLY (
                  SELECT SUM(CONVERT(bigint, mf.size)) * 8192 AS TotalLogBytes
                  FROM sys.master_files AS mf
                  WHERE mf.database_id = d.database_id AND mf.type = 1
              ) AS log_size
              OUTER APPLY (
                  SELECT
                      MAX(CASE WHEN b.type = 'D' AND b.is_copy_only = 0 THEN b.backup_finish_date END) AS LastFullBackupLocal,
                      MAX(CASE WHEN b.type = 'L' THEN b.backup_finish_date END) AS LastLogBackupLocal
                  FROM msdb.dbo.backupset AS b
                  WHERE b.database_name = d.name
              ) AS backups
              WHERE d.database_id > 4
              ORDER BY CASE WHEN d.log_reuse_wait_desc IN (N'NOTHING', N'CHECKPOINT') THEN 1 ELSE 0 END ASC,
                       CASE WHEN d.recovery_model_desc IN (N'FULL', N'BULK_LOGGED') THEN 0 ELSE 1 END ASC,
                       d.name ASC
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
