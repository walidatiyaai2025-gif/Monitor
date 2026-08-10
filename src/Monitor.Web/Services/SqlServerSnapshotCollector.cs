using System.Data;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed record SqlSnapshotRow(
    string ServerName,
    string ProductVersion,
    string Edition,
    string? InstanceName,
    long UptimeSeconds,
    long DatabaseTotal,
    long DatabaseOnline,
    SqlMemoryRow? Memory = null,
    SqlDatabaseHealthRow? DatabaseHealth = null);

internal sealed record SqlMemoryRow(
    long TotalPhysicalMemoryKb,
    long AvailablePhysicalMemoryKb,
    long SqlProcessPhysicalMemoryKb,
    int SqlProcessMemoryUtilizationPercent,
    bool IsPhysicalMemoryLow,
    bool IsVirtualMemoryLow,
    string SystemMemoryState);

internal sealed record SqlDatabaseHealthRow(
    long OnlineCount,
    long RestoringCount,
    long RecoveringCount,
    long RecoveryPendingCount,
    long SuspectCount,
    long EmergencyCount,
    long OfflineCount,
    long CopyingCount,
    long OfflineSecondaryCount,
    long OtherCount,
    long ReadOnlyCount);

internal interface ISqlSnapshotQuery
{
    Task<SqlSnapshotRow> ExecuteAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken);
}

public interface ISqlServerSnapshotCollector
{
    Task<ServerHealthSnapshot> CollectAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default);
}

internal sealed class SqlServerSnapshotCollector(
    IConnectionSecretStore secretStore,
    ISqlSnapshotQuery query,
    TimeProvider timeProvider) : ISqlServerSnapshotCollector
{
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(7);

    public async Task<ServerHealthSnapshot> CollectAsync(
        ServerRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!registration.IsEnabled)
        {
            throw Failure(SnapshotCollectionFailure.Disabled, "Server registration is disabled.");
        }

        SqlLoginSecret? secret = null;
        if (registration.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
        {
            secret = await secretStore.ResolveAsync(registration.SecretReference!.Value, cancellationToken);
            if (secret is null)
            {
                throw Failure(SnapshotCollectionFailure.SecretUnavailable, "Connection credentials are unavailable.");
            }
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(OverallTimeout);

        try
        {
            var row = await query.ExecuteAsync(registration, secret, timeoutSource.Token);
            var total = checked((int)row.DatabaseTotal);
            var online = checked((int)row.DatabaseOnline);
            if (row.UptimeSeconds < 0 || total < 0 || online < 0 || online > total)
            {
                throw new InvalidDataException("Invalid snapshot row.");
            }

            MemoryHealthSnapshot? memory = null;
            if (row.Memory is not null)
            {
                var value = row.Memory;
                if (value.TotalPhysicalMemoryKb < 0 ||
                    value.AvailablePhysicalMemoryKb < 0 ||
                    value.AvailablePhysicalMemoryKb > value.TotalPhysicalMemoryKb ||
                    value.SqlProcessPhysicalMemoryKb < 0 ||
                    value.SqlProcessMemoryUtilizationPercent is < 0 or > 100 ||
                    string.IsNullOrWhiteSpace(value.SystemMemoryState))
                {
                    throw new InvalidDataException("Invalid memory snapshot row.");
                }

                memory = new MemoryHealthSnapshot(
                    value.TotalPhysicalMemoryKb,
                    value.AvailablePhysicalMemoryKb,
                    value.SqlProcessPhysicalMemoryKb,
                    value.SqlProcessMemoryUtilizationPercent,
                    value.IsPhysicalMemoryLow,
                    value.IsVirtualMemoryLow,
                    value.SystemMemoryState);
            }

            DatabaseHealthSnapshot? databaseHealth = null;
            if (row.DatabaseHealth is not null)
            {
                var value = row.DatabaseHealth;
                var counts = new[]
                {
                    value.OnlineCount,
                    value.RestoringCount,
                    value.RecoveringCount,
                    value.RecoveryPendingCount,
                    value.SuspectCount,
                    value.EmergencyCount,
                    value.OfflineCount,
                    value.CopyingCount,
                    value.OfflineSecondaryCount,
                    value.OtherCount,
                    value.ReadOnlyCount
                };

                if (counts.Any(count => count < 0))
                {
                    throw new InvalidDataException("Invalid database health snapshot row.");
                }

                var stateTotal = checked(
                    value.OnlineCount +
                    value.RestoringCount +
                    value.RecoveringCount +
                    value.RecoveryPendingCount +
                    value.SuspectCount +
                    value.EmergencyCount +
                    value.OfflineCount +
                    value.CopyingCount +
                    value.OfflineSecondaryCount +
                    value.OtherCount);

                if (stateTotal != row.DatabaseTotal ||
                    value.OnlineCount != row.DatabaseOnline ||
                    value.ReadOnlyCount > row.DatabaseTotal)
                {
                    throw new InvalidDataException("Database health state counts are inconsistent.");
                }

                databaseHealth = new DatabaseHealthSnapshot(
                    checked((int)value.OnlineCount),
                    checked((int)value.RestoringCount),
                    checked((int)value.RecoveringCount),
                    checked((int)value.RecoveryPendingCount),
                    checked((int)value.SuspectCount),
                    checked((int)value.EmergencyCount),
                    checked((int)value.OfflineCount),
                    checked((int)value.CopyingCount),
                    checked((int)value.OfflineSecondaryCount),
                    checked((int)value.OtherCount),
                    checked((int)value.ReadOnlyCount));
            }

            return new ServerHealthSnapshot(
                registration.Id,
                row.ServerName,
                row.ProductVersion,
                row.Edition,
                row.InstanceName,
                row.UptimeSeconds,
                total,
                online,
                timeProvider.GetUtcNow(),
                memory,
                databaseHealth);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Failure(SnapshotCollectionFailure.TimedOut, "Snapshot collection timed out.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlProbeException exception)
        {
            throw exception.Kind switch
            {
                SqlProbeFailureKind.Authentication => Failure(SnapshotCollectionFailure.AuthenticationFailed, "Authentication failed."),
                SqlProbeFailureKind.Timeout => Failure(SnapshotCollectionFailure.TimedOut, "Snapshot collection timed out."),
                SqlProbeFailureKind.Network => Failure(SnapshotCollectionFailure.NetworkUnavailable, "The SQL Server could not be reached."),
                SqlProbeFailureKind.Certificate => Failure(SnapshotCollectionFailure.CertificateRejected, "SQL Server certificate validation failed."),
                _ => Failure(SnapshotCollectionFailure.Failed, "Snapshot collection failed.")
            };
        }
        catch (SnapshotCollectionException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Failure(SnapshotCollectionFailure.Failed, "Snapshot collection failed.");
        }
    }

    private static SnapshotCollectionException Failure(
        SnapshotCollectionFailure failure,
        string message) => new(failure, message);
}

internal sealed class SqlSnapshotQuery : ISqlSnapshotQuery
{
    internal const string CommandText = """
        SELECT
            CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) AS ServerName,
            CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
            CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
            CAST(SERVERPROPERTY('InstanceName') AS nvarchar(128)) AS InstanceName,
            DATEDIFF_BIG(SECOND, osi.sqlserver_start_time, SYSDATETIME()) AS UptimeSeconds,
            COUNT_BIG(*) AS DatabaseTotal,
            SUM(CASE WHEN d.state = 0 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DatabaseOnline,
            osm.total_physical_memory_kb AS TotalPhysicalMemoryKb,
            osm.available_physical_memory_kb AS AvailablePhysicalMemoryKb,
            pm.physical_memory_in_use_kb AS SqlProcessPhysicalMemoryKb,
            pm.memory_utilization_percentage AS SqlProcessMemoryUtilizationPercent,
            pm.process_physical_memory_low AS IsPhysicalMemoryLow,
            pm.process_virtual_memory_low AS IsVirtualMemoryLow,
            osm.system_memory_state_desc AS SystemMemoryState,
            SUM(CASE WHEN d.state = 0 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbOnlineCount,
            SUM(CASE WHEN d.state = 1 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbRestoringCount,
            SUM(CASE WHEN d.state = 2 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbRecoveringCount,
            SUM(CASE WHEN d.state = 3 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbRecoveryPendingCount,
            SUM(CASE WHEN d.state = 4 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbSuspectCount,
            SUM(CASE WHEN d.state = 5 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbEmergencyCount,
            SUM(CASE WHEN d.state = 6 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbOfflineCount,
            SUM(CASE WHEN d.state = 7 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbCopyingCount,
            SUM(CASE WHEN d.state = 10 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbOfflineSecondaryCount,
            SUM(CASE WHEN d.state NOT IN (0,1,2,3,4,5,6,7,10) THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbOtherCount,
            SUM(CASE WHEN d.is_read_only = 1 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DbReadOnlyCount
        FROM sys.databases AS d
        CROSS JOIN sys.dm_os_sys_info AS osi
        CROSS JOIN sys.dm_os_sys_memory AS osm
        CROSS JOIN sys.dm_os_process_memory AS pm
        GROUP BY osi.sqlserver_start_time,
            osm.total_physical_memory_kb,
            osm.available_physical_memory_kb,
            pm.physical_memory_in_use_kb,
            pm.memory_utilization_percentage,
            pm.process_physical_memory_low,
            pm.process_virtual_memory_low,
            osm.system_memory_state_desc
        """;

    public async Task<SqlSnapshotRow> ExecuteAsync(
        ServerRegistration registration,
        SqlLoginSecret? secret,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = SqlConnectionStringFactory.Create(
                registration,
                secret,
                "Monitor/LightweightCollector");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = CommandText;
            command.CommandTimeout = 2;
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidDataException("Snapshot query returned no row.");
            }

            return new SqlSnapshotRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                new SqlMemoryRow(
                    reader.GetInt64(7),
                    reader.GetInt64(8),
                    reader.GetInt64(9),
                    reader.GetInt32(10),
                    reader.GetBoolean(11),
                    reader.GetBoolean(12),
                    reader.GetString(13)),
                new SqlDatabaseHealthRow(
                    reader.GetInt64(14),
                    reader.GetInt64(15),
                    reader.GetInt64(16),
                    reader.GetInt64(17),
                    reader.GetInt64(18),
                    reader.GetInt64(19),
                    reader.GetInt64(20),
                    reader.GetInt64(21),
                    reader.GetInt64(22),
                    reader.GetInt64(23),
                    reader.GetInt64(24)));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }
}
