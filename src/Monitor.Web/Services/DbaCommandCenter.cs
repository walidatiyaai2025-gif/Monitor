using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record DbaQueryHotspot(
    string DatabaseName,
    string QueryHash,
    long ExecutionCount,
    long TotalCpuMs,
    long AverageCpuMs,
    long TotalElapsedMs,
    long LogicalReads,
    long LogicalWrites,
    DateTimeOffset? LastExecutionAtUtc,
    string QuerySnippet);

public sealed record DbaMemoryGrant(
    int SessionId,
    string DatabaseName,
    long RequestedKb,
    long GrantedKb,
    long UsedKb,
    long IdealKb,
    long WaitTimeMs,
    string QuerySnippet);

public sealed record DbaDatabaseResource(
    string DatabaseName,
    string State,
    string RecoveryModel,
    decimal DataSizeMb,
    decimal LogSizeMb,
    decimal BufferPoolMb,
    DateTimeOffset? LastFullBackupAtUtc,
    bool FullBackupOverdue);

public sealed record DbaMissingIndexCandidate(
    string DatabaseName,
    string SchemaName,
    string TableName,
    string EqualityColumns,
    string InequalityColumns,
    string IncludedColumns,
    long UserSeeks,
    long UserScans,
    decimal AverageImpact,
    decimal Score,
    string ReviewSql);

public sealed record DbaDiagnosticsSnapshot(
    Guid RegistrationId,
    DateTimeOffset CollectedAtUtc,
    bool IsAvailable,
    string StatusMessage,
    IReadOnlyList<DbaQueryHotspot> QueryHotspots,
    IReadOnlyList<DbaMemoryGrant> MemoryGrants,
    IReadOnlyList<DbaDatabaseResource> Databases,
    IReadOnlyList<DbaMissingIndexCandidate> MissingIndexes);

public sealed record DbaRecommendation(
    string Severity,
    string Category,
    string Title,
    string Evidence,
    string Recommendation,
    string FixSql,
    string ValidationSql,
    string Safety = "REVIEW ONLY - NEVER AUTO-EXECUTE");

public sealed record DbaTodoItem(
    string Priority,
    string What,
    string When,
    string Where,
    string Reason);

public sealed record DbaServerCommandViewModel(
    Guid RegistrationId,
    string DisplayName,
    string Endpoint,
    bool IsEnabled,
    ServerHealthSnapshot? BaseSnapshot,
    DbaDiagnosticsSnapshot? Diagnostics,
    IReadOnlyList<DbaRecommendation> Recommendations,
    IReadOnlyList<DbaTodoItem> TodoItems);

public sealed record DbaCommandCenterViewModel(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<DbaServerCommandViewModel> Servers,
    int CriticalOrHighRecommendations,
    int QueryHotspots,
    int OpenTodoItems);

public sealed record DbaInspectionResult(bool Success, string Message, DbaDiagnosticsSnapshot? Snapshot);

public interface IDbaCommandCenterService
{
    DbaCommandCenterViewModel GetCached();
    Task<DbaInspectionResult> InspectAsync(Guid registrationId, CancellationToken cancellationToken = default);
    string BuildBackupManagementPlan(Guid registrationId, string? databaseName = null);
}

internal sealed class DbaCommandCenterService(
    IServerRegistrationRepository registrations,
    IServerHealthSnapshotCache snapshots,
    IConnectionSecretStore secretStore,
    TimeProvider timeProvider) : IDbaCommandCenterService
{
    private const int MaxCachedServers = 200;
    private static readonly TimeSpan InspectionTimeout = TimeSpan.FromSeconds(8);
    private static readonly ConcurrentDictionary<Guid, DbaDiagnosticsSnapshot> Diagnostics = new();
    private static readonly Regex StringLiteral = new(@"N?'(?:''|[^'])*'", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NumericLiteral = new(@"(?<![\w])[-+]?\d+(?:\.\d+)?(?![\w])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public DbaCommandCenterViewModel GetCached()
    {
        var now = timeProvider.GetUtcNow();
        var servers = registrations.GetAll()
            .OrderBy(server => server.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(server =>
            {
                var baseSnapshot = snapshots.Peek(server.Id)?.Snapshot;
                Diagnostics.TryGetValue(server.Id, out var diagnostics);
                var recommendations = BuildRecommendations(baseSnapshot, diagnostics);
                return new DbaServerCommandViewModel(
                    server.Id,
                    server.DisplayName,
                    server.Endpoint,
                    server.IsEnabled,
                    baseSnapshot,
                    diagnostics,
                    recommendations,
                    BuildTodos(server.DisplayName, recommendations));
            })
            .ToArray();

        return new(
            now,
            servers,
            servers.Sum(server => server.Recommendations.Count(item => item.Severity is "Critical" or "High")),
            servers.Sum(server => server.Diagnostics?.QueryHotspots.Count ?? 0),
            servers.Sum(server => server.TodoItems.Count));
    }

    public async Task<DbaInspectionResult> InspectAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        var registration = registrations.GetById(registrationId);
        if (registration is null)
            return new(false, "Server registration was not found.", null);
        if (!registration.IsEnabled)
            return new(false, "Server registration is disabled.", null);

        SqlLoginSecret? secret = null;
        if (registration.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
        {
            secret = await secretStore.ResolveAsync(registration.SecretReference!.Value, cancellationToken);
            if (secret is null)
                return CacheUnavailable(registrationId, "Protected SQL credentials are unavailable.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(InspectionTimeout);

        try
        {
            var snapshot = await CollectAsync(registration, secret, timeout.Token);
            Diagnostics[registration.Id] = snapshot;
            TrimCache();
            return new(true, "DBA inspection completed and cached.", snapshot);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CacheUnavailable(registrationId, "DBA inspection timed out. Existing base monitoring remains available.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException)
        {
            return CacheUnavailable(registrationId, "DBA inspection could not read the required DMVs. Verify connectivity and VIEW SERVER STATE / VIEW SERVER PERFORMANCE STATE permissions.");
        }
        catch (InvalidDataException)
        {
            return CacheUnavailable(registrationId, "DBA inspection returned invalid or unsupported diagnostic data.");
        }
    }

    public string BuildBackupManagementPlan(Guid registrationId, string? databaseName = null)
    {
        var registration = registrations.GetById(registrationId) ?? throw new KeyNotFoundException("Server registration was not found.");
        Diagnostics.TryGetValue(registrationId, out var diagnostic);
        var databases = diagnostic?.Databases
            .Where(database => string.IsNullOrWhiteSpace(databaseName) || string.Equals(database.DatabaseName, databaseName, StringComparison.OrdinalIgnoreCase))
            .ToArray() ?? [];

        if (!string.IsNullOrWhiteSpace(databaseName) && databases.Length == 0)
            throw new KeyNotFoundException("The requested database is not present in the latest DBA inspection.");

        var builder = new StringBuilder();
        builder.AppendLine("/* MONITOR DBA BACKUP MANAGEMENT PLAN");
        builder.AppendLine($"   Server: {registration.DisplayName}");
        builder.AppendLine($"   Scope: {(string.IsNullOrWhiteSpace(databaseName) ? "all inspected user databases" : databaseName)}");
        builder.AppendLine("   SAFETY: REVIEW ONLY. Validate paths, retention, encryption, credentials and restore procedures before scheduling.");
        builder.AppendLine("   Recommended cadence: FULL daily 23:00; DIFFERENTIAL every 6 hours; LOG every 15 minutes for FULL/BULK_LOGGED; CHECKDB weekly; restore test weekly.");
        builder.AppendLine("*/");
        builder.AppendLine("DECLARE @BackupRoot nvarchar(4000) = N'<BACKUP_ROOT>'; -- CHANGE THIS");
        builder.AppendLine();

        foreach (var database in databases)
        {
            var quoted = QuoteIdentifier(database.DatabaseName);
            var literal = EscapeLiteral(database.DatabaseName);
            builder.AppendLine($"-- {database.DatabaseName} | Recovery={database.RecoveryModel} | Last full={(database.LastFullBackupAtUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "not observed")}");
            builder.AppendLine($"BACKUP DATABASE {quoted} TO DISK = @BackupRoot + N'\\{literal}_FULL_' + CONVERT(nvarchar(8), GETDATE(), 112) + N'.bak' WITH CHECKSUM, COMPRESSION, STATS = 10;");
            builder.AppendLine($"RESTORE VERIFYONLY FROM DISK = @BackupRoot + N'\\{literal}_FULL_' + CONVERT(nvarchar(8), GETDATE(), 112) + N'.bak' WITH CHECKSUM;");
            builder.AppendLine($"-- Differential job: BACKUP DATABASE {quoted} TO DISK = N'<DIFF_FILE>' WITH DIFFERENTIAL, CHECKSUM, COMPRESSION, STATS = 10;");
            if (database.RecoveryModel is "FULL" or "BULK_LOGGED")
                builder.AppendLine($"-- Log job (15 min): BACKUP LOG {quoted} TO DISK = N'<LOG_FILE>' WITH CHECKSUM, COMPRESSION, STATS = 10;");
            builder.AppendLine($"-- Weekly integrity job: DBCC CHECKDB ({quoted}) WITH NO_INFOMSGS, ALL_ERRORMSGS;");
            builder.AppendLine();
        }

        if (databases.Length == 0)
            builder.AppendLine("-- Run a DBA inspection first so Monitor can enumerate the intended user-database scope safely.");

        return builder.ToString();
    }

    internal static string SanitizeQueryText(string? queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText)) return "Query text unavailable";
        var value = StringLiteral.Replace(queryText, "?");
        value = NumericLiteral.Replace(value, "?");
        value = Whitespace.Replace(value, " ").Trim();
        return value.Length <= 600 ? value : value[..600] + "…";
    }

    private async Task<DbaDiagnosticsSnapshot> CollectAsync(ServerRegistration registration, SqlLoginSecret? secret, CancellationToken cancellationToken)
    {
        var connectionString = SqlConnectionStringFactory.Create(registration, secret, "Monitor/DBACommandCenter");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 5;
        command.CommandText = DiagnosticSql;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        var queries = new List<DbaQueryHotspot>(20);
        while (await reader.ReadAsync(cancellationToken))
        {
            queries.Add(new(
                Safe(reader, 0, "unknown"), Safe(reader, 1, "unknown"), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7),
                reader.IsDBNull(8) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc)),
                SanitizeQueryText(reader.IsDBNull(9) ? null : reader.GetString(9))));
        }

        var grants = new List<DbaMemoryGrant>(20);
        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                grants.Add(new(
                    reader.GetInt32(0), Safe(reader, 1, "unknown"), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6),
                    SanitizeQueryText(reader.IsDBNull(7) ? null : reader.GetString(7))));
            }
        }

        var databases = new List<DbaDatabaseResource>(100);
        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var lastFull = reader.IsDBNull(6) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc));
                databases.Add(new(
                    Safe(reader, 0, "unknown"), Safe(reader, 1, "UNKNOWN"), Safe(reader, 2, "UNKNOWN"), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), lastFull,
                    !lastFull.HasValue || timeProvider.GetUtcNow() - lastFull.Value > TimeSpan.FromHours(24)));
            }
        }

        var indexes = new List<DbaMissingIndexCandidate>(20);
        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var db = Safe(reader, 0, "unknown");
                var schema = Safe(reader, 1, "dbo");
                var table = Safe(reader, 2, "unknown");
                var equality = Safe(reader, 3, string.Empty);
                var inequality = Safe(reader, 4, string.Empty);
                var included = Safe(reader, 5, string.Empty);
                var seeks = reader.GetInt64(6);
                var scans = reader.GetInt64(7);
                var impact = reader.GetDecimal(8);
                var score = reader.GetDecimal(9);
                indexes.Add(new(db, schema, table, equality, inequality, included, seeks, scans, impact, score, BuildIndexReviewSql(db, schema, table, equality, inequality, included)));
            }
        }

        return new(registration.Id, timeProvider.GetUtcNow(), true, "Inspection available", queries, grants, databases, indexes);
    }

    private DbaInspectionResult CacheUnavailable(Guid registrationId, string message)
    {
        var snapshot = new DbaDiagnosticsSnapshot(registrationId, timeProvider.GetUtcNow(), false, message, [], [], [], []);
        Diagnostics[registrationId] = snapshot;
        TrimCache();
        return new(false, message, snapshot);
    }

    private static IReadOnlyList<DbaRecommendation> BuildRecommendations(ServerHealthSnapshot? snapshot, DbaDiagnosticsSnapshot? diagnostic)
    {
        var list = new List<DbaRecommendation>();
        if (snapshot is null)
        {
            list.Add(new("High", "Collection", "Refresh base server evidence", "No base health snapshot is cached.", "Refresh the registered server before making tuning decisions.", "-- Use the visible 'Refresh base snapshot' action in Monitor.", "SELECT @@SERVERNAME AS ServerName, SYSDATETIMEOFFSET() AS CheckedAt;"));
            return list;
        }

        if (snapshot.Memory is { } memory && (memory.IsPhysicalMemoryLow || memory.IsVirtualMemoryLow || memory.MemoryGrantsPending is > 0 || memory.SqlProcessMemoryUtilizationPercent >= 90))
            list.Add(new("High", "Memory", "Investigate SQL memory pressure", $"SQL utilization {memory.SqlProcessMemoryUtilizationPercent}%; grants pending {memory.MemoryGrantsPending ?? 0}; OS state: {memory.SystemMemoryState}.", "Correlate max server memory, OS headroom, memory clerks and grant-heavy queries. Change max server memory only after capacity review.", "SELECT name, value_in_use FROM sys.configurations WHERE name = N'max server memory (MB)'; -- REVIEW ONLY; do not RECONFIGURE automatically", "SELECT total_physical_memory_kb, available_physical_memory_kb, system_memory_state_desc FROM sys.dm_os_sys_memory;"));

        if (snapshot.Blocking is { BlockedRequests: > 0 } blocking)
            list.Add(new(blocking.MaxWaitMilliseconds >= 30000 ? "Critical" : "High", "Blocking", "Resolve blocking chain", $"{blocking.BlockedRequests} blocked request(s); maximum observed wait {blocking.MaxWaitMilliseconds} ms.", "Identify the head blocker, transaction owner and application path before killing a session or changing isolation.", "SELECT session_id, blocking_session_id, wait_type, wait_time, wait_resource FROM sys.dm_exec_requests WHERE blocking_session_id > 0;", "SELECT session_id, open_transaction_count, status FROM sys.dm_exec_sessions WHERE is_user_process = 1;"));

        if (snapshot.Performance is { } performance && (performance.RunnableTasks > 2 || performance.PendingIoRequests > 0))
            list.Add(new("High", "CPU / IO", "Investigate scheduler or pending I/O pressure", $"Runnable tasks {performance.RunnableTasks}; pending I/O requests {performance.PendingIoRequests}; active requests {performance.ActiveRequests}.", "Correlate top CPU queries, waits and file latency before changing CPU or storage configuration.", "SELECT scheduler_id, runnable_tasks_count, current_tasks_count, active_workers_count FROM sys.dm_os_schedulers WHERE status = 'VISIBLE ONLINE';", "SELECT TOP (20) wait_type, wait_time_ms, signal_wait_time_ms, waiting_tasks_count FROM sys.dm_os_wait_stats ORDER BY wait_time_ms DESC;"));

        var slowFiles = snapshot.Storage?.IoFiles?.Where(file => AverageIoLatency(file) >= 20m).Take(5).ToArray() ?? [];
        if (slowFiles.Length > 0)
            list.Add(new("High", "Storage", "Review high-latency database files", string.Join("; ", slowFiles.Select(file => $"{file.FileKey} {AverageIoLatency(file):0.0}ms avg")), "Check storage tier, queueing, competing workloads, file placement and growth events. Do not move files until restore/rollback is proven.", "SELECT DB_NAME(vfs.database_id) AS DatabaseName, mf.name, vfs.num_of_reads, vfs.num_of_writes, vfs.io_stall_read_ms, vfs.io_stall_write_ms FROM sys.dm_io_virtual_file_stats(NULL,NULL) vfs JOIN sys.master_files mf ON mf.database_id=vfs.database_id AND mf.file_id=vfs.file_id;", "SELECT SYSDATETIMEOFFSET() AS RecheckAt;"));

        if (snapshot.Backups is { MissingFullBackupLast24Hours: > 0 } backup)
            list.Add(new("Critical", "Backup", "Restore full-backup compliance", $"{backup.MissingFullBackupLast24Hours} online database(s) have no observed non-copy-only full backup in 24 hours.", "Generate the database/all-databases backup management plan, validate the target path and run a restore test.", "-- Use 'Backup plan - all databases' or the per-database Backup Plan button. Review path and retention before execution.", "SELECT database_name, MAX(backup_finish_date) AS LastFullBackup FROM msdb.dbo.backupset WHERE type='D' AND is_copy_only=0 GROUP BY database_name;"));

        if (snapshot.Memory?.MemoryGrantsPending is > 0 && diagnostic?.MemoryGrants.Count > 0)
        {
            var largest = diagnostic.MemoryGrants.OrderByDescending(grant => grant.RequestedKb).First();
            list.Add(new("High", "Query memory", "Tune grant-heavy query workload", $"Largest observed requested grant {largest.RequestedKb / 1024m:0.0} MB in {largest.DatabaseName}; query: {largest.QuerySnippet}", "Review estimates, statistics, joins/sorts and plan shape for the largest grants before adding server memory.", "-- REVIEW query plan in SSMS/Query Store for the query shown above; Monitor intentionally does not execute tuning changes.", "SELECT * FROM sys.dm_exec_query_memory_grants ORDER BY requested_memory_kb DESC;"));
        }

        if (diagnostic is { IsAvailable: true })
        {
            var topCpu = diagnostic.QueryHotspots.OrderByDescending(query => query.TotalCpuMs).FirstOrDefault();
            if (topCpu is not null)
                list.Add(new("Medium", "Query CPU", "Review top cumulative CPU query", $"{topCpu.DatabaseName} query hash {topCpu.QueryHash}: {topCpu.TotalCpuMs:N0} ms CPU across {topCpu.ExecutionCount:N0} execution(s); {topCpu.LogicalReads:N0} logical reads.", "Inspect Query Store/actual plan, parameter sensitivity, indexes and statistics before rewriting or forcing a plan.", $"-- Query hash {topCpu.QueryHash}; sanitized text: {topCpu.QuerySnippet}", "SELECT TOP (20) query_hash, execution_count, total_worker_time, total_logical_reads FROM sys.dm_exec_query_stats ORDER BY total_worker_time DESC;"));

            var missing = diagnostic.MissingIndexes.OrderByDescending(index => index.Score).FirstOrDefault();
            if (missing is not null)
                list.Add(new("Medium", "Index", "Validate missing-index candidate", $"{missing.DatabaseName}.{missing.SchemaName}.{missing.TableName}; seeks {missing.UserSeeks:N0}; estimated impact {missing.AverageImpact:0.0}%; score {missing.Score:0.0}.", "Compare with existing indexes, write overhead and Query Store plans. Consolidate overlapping candidates before creating anything.", missing.ReviewSql, "SELECT i.name, i.type_desc, i.is_disabled FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'<schema.table>');"));

            foreach (var database in diagnostic.Databases.Where(database => database.FullBackupOverdue).Take(5))
                list.Add(new("High", "Backup", $"Backup overdue: {database.DatabaseName}", $"Latest observed full backup: {(database.LastFullBackupAtUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "none")}; recovery model {database.RecoveryModel}.", "Review the per-database backup plan and restore-test evidence.", $"-- Open the visible Backup Plan action for {database.DatabaseName}; no backup is auto-run by Monitor.", $"SELECT MAX(backup_finish_date) FROM msdb.dbo.backupset WHERE database_name=N'{EscapeLiteral(database.DatabaseName)}' AND type='D' AND is_copy_only=0;"));
        }

        return list.Take(20).ToArray();
    }

    private static IReadOnlyList<DbaTodoItem> BuildTodos(string server, IReadOnlyList<DbaRecommendation> recommendations) =>
        recommendations.Select((item, index) => new DbaTodoItem(
            item.Severity is "Critical" ? "P0" : item.Severity is "High" ? "P1" : "P2",
            item.Title,
            item.Severity is "Critical" ? "Now" : item.Severity is "High" ? "Today" : "This maintenance cycle",
            $"{server} / {item.Category}",
            item.Evidence)).Take(12).ToArray();

    private static decimal AverageIoLatency(IoFileSnapshot file)
    {
        var operations = file.Reads + file.Writes;
        return operations <= 0 ? 0 : (decimal)(file.ReadStallMs + file.WriteStallMs) / operations;
    }

    private static string BuildIndexReviewSql(string db, string schema, string table, string equality, string inequality, string included)
    {
        var keys = string.Join(", ", new[] { equality, inequality }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(keys)) keys = "<REVIEW_KEY_COLUMNS>";
        var includes = string.IsNullOrWhiteSpace(included) ? string.Empty : $" INCLUDE ({included})";
        return $"-- REVIEW ONLY: compare existing indexes and write overhead first.\nUSE {QuoteIdentifier(db)};\nCREATE INDEX [IX_Monitor_Review] ON {QuoteIdentifier(schema)}.{QuoteIdentifier(table)} ({keys}){includes};";
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static string Safe(SqlDataReader reader, int ordinal, string fallback) => reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);

    private static void TrimCache()
    {
        if (Diagnostics.Count <= MaxCachedServers) return;
        foreach (var key in Diagnostics.OrderBy(pair => pair.Value.CollectedAtUtc).Take(Diagnostics.Count - MaxCachedServers).Select(pair => pair.Key))
            Diagnostics.TryRemove(key, out _);
    }

    private const string DiagnosticSql = """
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT TOP (20)
    COALESCE(DB_NAME(st.dbid), N'unknown') AS DatabaseName,
    CONVERT(varchar(18), qs.query_hash, 1) AS QueryHash,
    CONVERT(bigint, qs.execution_count) AS ExecutionCount,
    CONVERT(bigint, qs.total_worker_time / 1000) AS TotalCpuMs,
    CONVERT(bigint, CASE WHEN qs.execution_count = 0 THEN 0 ELSE qs.total_worker_time / qs.execution_count / 1000 END) AS AverageCpuMs,
    CONVERT(bigint, qs.total_elapsed_time / 1000) AS TotalElapsedMs,
    CONVERT(bigint, qs.total_logical_reads) AS LogicalReads,
    CONVERT(bigint, qs.total_logical_writes) AS LogicalWrites,
    qs.last_execution_time AS LastExecutionAtUtc,
    CONVERT(nvarchar(4000), st.text) AS QueryText
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE st.text IS NOT NULL
ORDER BY qs.total_worker_time DESC, qs.total_logical_reads DESC;

SELECT TOP (20)
    CONVERT(int, mg.session_id) AS SessionId,
    COALESCE(DB_NAME(r.database_id), N'unknown') AS DatabaseName,
    CONVERT(bigint, mg.requested_memory_kb) AS RequestedKb,
    CONVERT(bigint, mg.granted_memory_kb) AS GrantedKb,
    CONVERT(bigint, mg.used_memory_kb) AS UsedKb,
    CONVERT(bigint, mg.ideal_memory_kb) AS IdealKb,
    CONVERT(bigint, mg.wait_time_ms) AS WaitTimeMs,
    CONVERT(nvarchar(4000), st.text) AS QueryText
FROM sys.dm_exec_query_memory_grants mg
LEFT JOIN sys.dm_exec_requests r ON r.session_id = mg.session_id
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) st
ORDER BY mg.requested_memory_kb DESC, mg.session_id ASC;

;WITH FileSizes AS (
    SELECT database_id,
           SUM(CASE WHEN type = 0 THEN CONVERT(decimal(19,2), size) * 8 / 1024 ELSE 0 END) AS DataSizeMb,
           SUM(CASE WHEN type = 1 THEN CONVERT(decimal(19,2), size) * 8 / 1024 ELSE 0 END) AS LogSizeMb
    FROM sys.master_files GROUP BY database_id
), BufferUsage AS (
    SELECT database_id, CONVERT(decimal(19,2), COUNT_BIG(*) * 8.0 / 1024.0) AS BufferPoolMb
    FROM sys.dm_os_buffer_descriptors WHERE database_id > 4 GROUP BY database_id
), LastFull AS (
    SELECT database_name, MAX(backup_finish_date) AS LastFullBackupAtUtc
    FROM msdb.dbo.backupset WHERE type = 'D' AND is_copy_only = 0 GROUP BY database_name
)
SELECT TOP (100)
    CONVERT(nvarchar(128), d.name) AS DatabaseName,
    CONVERT(nvarchar(60), d.state_desc) AS StateDescription,
    CONVERT(nvarchar(60), d.recovery_model_desc) AS RecoveryModel,
    CONVERT(decimal(19,2), COALESCE(f.DataSizeMb, 0)) AS DataSizeMb,
    CONVERT(decimal(19,2), COALESCE(f.LogSizeMb, 0)) AS LogSizeMb,
    CONVERT(decimal(19,2), COALESCE(b.BufferPoolMb, 0)) AS BufferPoolMb,
    lf.LastFullBackupAtUtc
FROM sys.databases d
LEFT JOIN FileSizes f ON f.database_id = d.database_id
LEFT JOIN BufferUsage b ON b.database_id = d.database_id
LEFT JOIN LastFull lf ON lf.database_name = d.name
WHERE d.database_id > 4
ORDER BY COALESCE(b.BufferPoolMb, 0) DESC, d.name ASC;

SELECT TOP (20)
    CONVERT(nvarchar(128), DB_NAME(mid.database_id)) AS DatabaseName,
    CONVERT(nvarchar(128), OBJECT_SCHEMA_NAME(mid.object_id, mid.database_id)) AS SchemaName,
    CONVERT(nvarchar(128), OBJECT_NAME(mid.object_id, mid.database_id)) AS TableName,
    CONVERT(nvarchar(1000), COALESCE(mid.equality_columns, N'')) AS EqualityColumns,
    CONVERT(nvarchar(1000), COALESCE(mid.inequality_columns, N'')) AS InequalityColumns,
    CONVERT(nvarchar(1000), COALESCE(mid.included_columns, N'')) AS IncludedColumns,
    CONVERT(bigint, migs.user_seeks) AS UserSeeks,
    CONVERT(bigint, migs.user_scans) AS UserScans,
    CONVERT(decimal(9,2), migs.avg_user_impact) AS AverageImpact,
    CONVERT(decimal(19,2), (migs.user_seeks + migs.user_scans) * migs.avg_total_user_cost * (migs.avg_user_impact / 100.0)) AS Score
FROM sys.dm_db_missing_index_group_stats migs
JOIN sys.dm_db_missing_index_groups mig ON mig.index_group_handle = migs.group_handle
JOIN sys.dm_db_missing_index_details mid ON mid.index_handle = mig.index_handle
WHERE mid.database_id > 4
ORDER BY Score DESC;
""";
}
