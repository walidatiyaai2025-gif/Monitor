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
    SqlHealthModulesRow? Modules = null);

internal sealed record SqlHealthModulesRow(
    long Restoring, long Recovering, long RecoveryPending, long Suspect, long Emergency, long OfflineOrOther,
    long BackedUpLast24Hours, long MissingFullBackupLast24Hours, DateTimeOffset? LastFullBackupAtUtc,
    long TotalJobs, long EnabledJobs, long FailedLastRun,
    long TotalAllocatedBytes, long DataAllocatedBytes, long LogAllocatedBytes,
    long BlockedRequests, long MaxWaitMilliseconds);

internal sealed record SqlMemoryRow(
    long TotalPhysicalMemoryKb,
    long AvailablePhysicalMemoryKb,
    long SqlProcessPhysicalMemoryKb,
    int SqlProcessMemoryUtilizationPercent,
    bool IsPhysicalMemoryLow,
    bool IsVirtualMemoryLow,
    string SystemMemoryState);

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

            DatabaseHealthDetailSnapshot? databases = null;
            BackupHealthSnapshot? backups = null;
            SqlAgentHealthSnapshot? jobs = null;
            StorageHealthSnapshot? storage = null;
            BlockingHealthSnapshot? blocking = null;
            if (row.Modules is not null)
            {
                var m = row.Modules;
                var states = new[] { m.Restoring, m.Recovering, m.RecoveryPending, m.Suspect, m.Emergency, m.OfflineOrOther };
                if (states.Any(value => value < 0) || states.Sum() > total ||
                    m.BackedUpLast24Hours < 0 || m.MissingFullBackupLast24Hours < 0 || m.BackedUpLast24Hours + m.MissingFullBackupLast24Hours > total ||
                    m.TotalJobs < 0 || m.EnabledJobs < 0 || m.EnabledJobs > m.TotalJobs || m.FailedLastRun < 0 || m.FailedLastRun > m.TotalJobs ||
                    m.TotalAllocatedBytes < 0 || m.DataAllocatedBytes < 0 || m.LogAllocatedBytes < 0 || m.DataAllocatedBytes + m.LogAllocatedBytes > m.TotalAllocatedBytes ||
                    m.BlockedRequests < 0 || m.MaxWaitMilliseconds < 0)
                {
                    throw new InvalidDataException("Invalid health module row.");
                }

                databases = new(checked((int)m.Restoring), checked((int)m.Recovering), checked((int)m.RecoveryPending), checked((int)m.Suspect), checked((int)m.Emergency), checked((int)m.OfflineOrOther));
                backups = new(checked((int)m.BackedUpLast24Hours), checked((int)m.MissingFullBackupLast24Hours), m.LastFullBackupAtUtc);
                jobs = new(checked((int)m.TotalJobs), checked((int)m.EnabledJobs), checked((int)m.FailedLastRun));
                storage = new(m.TotalAllocatedBytes, m.DataAllocatedBytes, m.LogAllocatedBytes);
                blocking = new(checked((int)m.BlockedRequests), m.MaxWaitMilliseconds);
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
                memory, databases, backups, jobs, storage, blocking);
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
            SUM(CASE WHEN d.state = 0 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS DatabaseOnline
            ,osm.total_physical_memory_kb AS TotalPhysicalMemoryKb
            ,osm.available_physical_memory_kb AS AvailablePhysicalMemoryKb
            ,pm.physical_memory_in_use_kb AS SqlProcessPhysicalMemoryKb
            ,pm.memory_utilization_percentage AS SqlProcessMemoryUtilizationPercent
            ,pm.process_physical_memory_low AS IsPhysicalMemoryLow
            ,pm.process_virtual_memory_low AS IsVirtualMemoryLow
            ,osm.system_memory_state_desc AS SystemMemoryState
            ,SUM(CASE WHEN d.state = 1 THEN CONVERT(bigint, 1) ELSE 0 END) AS Restoring
            ,SUM(CASE WHEN d.state = 2 THEN CONVERT(bigint, 1) ELSE 0 END) AS Recovering
            ,SUM(CASE WHEN d.state = 3 THEN CONVERT(bigint, 1) ELSE 0 END) AS RecoveryPending
            ,SUM(CASE WHEN d.state = 4 THEN CONVERT(bigint, 1) ELSE 0 END) AS Suspect
            ,SUM(CASE WHEN d.state = 5 THEN CONVERT(bigint, 1) ELSE 0 END) AS Emergency
            ,SUM(CASE WHEN d.state IN (6,7,10) THEN CONVERT(bigint, 1) ELSE 0 END) AS OfflineOrOther
            ,(select COUNT_BIG(*) FROM sys.databases x WHERE x.database_id > 4 AND x.state = 0 AND EXISTS (select 1 FROM msdb.dbo.backupset b WHERE b.database_name = x.name AND b.type = 'D' AND b.is_copy_only = 0 AND b.backup_finish_date >= DATEADD(HOUR, -24, SYSUTCDATETIME()))) AS BackedUpLast24Hours
            ,(select COUNT_BIG(*) FROM sys.databases x WHERE x.database_id > 4 AND x.state = 0 AND NOT EXISTS (select 1 FROM msdb.dbo.backupset b WHERE b.database_name = x.name AND b.type = 'D' AND b.is_copy_only = 0 AND b.backup_finish_date >= DATEADD(HOUR, -24, SYSUTCDATETIME()))) AS MissingFullBackupLast24Hours
            ,(select MAX(backup_finish_date) FROM msdb.dbo.backupset WHERE type = 'D' AND is_copy_only = 0) AS LastFullBackupAtUtc
            ,(select COUNT_BIG(*) FROM msdb.dbo.sysjobs) AS TotalJobs
            ,(select COUNT_BIG(*) FROM msdb.dbo.sysjobs WHERE enabled = 1) AS EnabledJobs
            ,(select COUNT_BIG(*) FROM msdb.dbo.sysjobservers WHERE last_run_outcome = 0) AS FailedLastRun
            ,(select COALESCE(SUM(CONVERT(bigint, size)) * 8192, 0) FROM sys.master_files) AS TotalAllocatedBytes
            ,(select COALESCE(SUM(CONVERT(bigint, size)) * 8192, 0) FROM sys.master_files WHERE type = 0) AS DataAllocatedBytes
            ,(select COALESCE(SUM(CONVERT(bigint, size)) * 8192, 0) FROM sys.master_files WHERE type = 1) AS LogAllocatedBytes
            ,(select COUNT_BIG(*) FROM sys.dm_exec_requests WHERE blocking_session_id > 0 AND session_id <> @@SPID) AS BlockedRequests
            ,(select COALESCE(MAX(CONVERT(bigint, wait_time)), 0) FROM sys.dm_exec_requests WHERE blocking_session_id > 0 AND session_id <> @@SPID) AS MaxWaitMilliseconds
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
                new SqlHealthModulesRow(
                    reader.GetInt64(14), reader.GetInt64(15), reader.GetInt64(16), reader.GetInt64(17), reader.GetInt64(18), reader.GetInt64(19),
                    reader.GetInt64(20), reader.GetInt64(21), reader.IsDBNull(22) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(22), DateTimeKind.Utc)),
                    reader.GetInt64(23), reader.GetInt64(24), reader.GetInt64(25),
                    reader.GetInt64(26), reader.GetInt64(27), reader.GetInt64(28),
                    reader.GetInt64(29), reader.GetInt64(30)));
        }
        catch (SqlException exception)
        {
            throw new SqlProbeException(SqlErrorClassifier.Classify(exception.Number));
        }
    }
}
