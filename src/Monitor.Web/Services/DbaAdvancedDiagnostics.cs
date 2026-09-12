using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.SqlClient;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record DbaWaitStat(string WaitType, long WaitingTasks, long WaitTimeMs, long SignalWaitMs, decimal ResourceWaitPercent);
public sealed record DbaMemoryClerk(string ClerkType, decimal MemoryMb);
public sealed record DbaTempDbFile(int FileId, string LogicalName, string FileType, decimal SizeMb, decimal UsedMb, decimal FreeMb, decimal GrowthMb, bool IsPercentGrowth);
public sealed record DbaActiveRequest(int SessionId, string DatabaseName, string Status, string Command, long CpuMs, long ElapsedMs, long LogicalReads, long Writes, string WaitType, int BlockingSessionId, string QuerySnippet);
public sealed record DbaConfigValue(string Name, int ConfiguredValue, int InUseValue, string Recommendation);

public sealed record DbaAdvancedSnapshot(
    Guid RegistrationId,
    DateTimeOffset CollectedAtUtc,
    bool IsAvailable,
    string StatusMessage,
    IReadOnlyList<DbaWaitStat> Waits,
    IReadOnlyList<DbaMemoryClerk> MemoryClerks,
    IReadOnlyList<DbaTempDbFile> TempDbFiles,
    IReadOnlyList<DbaActiveRequest> ActiveRequests,
    IReadOnlyList<DbaConfigValue> Configuration,
    decimal PlanCacheMb,
    decimal SingleUsePlanCacheMb,
    int SingleUsePlanCount);

public sealed record DbaAdvancedRecommendation(
    string Severity,
    string Category,
    string Title,
    string Evidence,
    string Action,
    string InvestigationOrFixSql,
    string ValidationSql,
    string Safety = "REVIEW ONLY - NEVER AUTO-EXECUTE");

public sealed record DbaAdvancedViewModel(
    Guid RegistrationId,
    string DisplayName,
    string Endpoint,
    DbaAdvancedSnapshot? Snapshot,
    IReadOnlyList<DbaAdvancedRecommendation> Recommendations);

public sealed class DbaAdvancedDiagnosticsService(
    IServerRegistrationRepository registrations,
    IConnectionSecretStore secretStore,
    TimeProvider timeProvider)
{
    private static readonly ConcurrentDictionary<Guid, DbaAdvancedSnapshot> Cache = new();
    private static readonly TimeSpan InspectionTimeout = TimeSpan.FromSeconds(10);

    public DbaAdvancedViewModel GetCached(Guid registrationId)
    {
        var registration = registrations.GetById(registrationId) ?? throw new KeyNotFoundException("Server registration was not found.");
        Cache.TryGetValue(registrationId, out var snapshot);
        return new(registration.Id, registration.DisplayName, registration.Endpoint, snapshot, BuildRecommendations(snapshot));
    }

    public async Task<DbaAdvancedSnapshot> InspectAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        var registration = registrations.GetById(registrationId) ?? throw new KeyNotFoundException("Server registration was not found.");
        if (!registration.IsEnabled)
            return CacheUnavailable(registrationId, "Server registration is disabled.");

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
            var connectionString = SqlConnectionStringFactory.Create(registration, secret, "Monitor/DBAAdvancedDiagnostics");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(timeout.Token);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 7;
            command.CommandText = AdvancedSql;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, timeout.Token);

            var waits = new List<DbaWaitStat>(20);
            while (await reader.ReadAsync(timeout.Token))
                waits.Add(new(Safe(reader, 0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetDecimal(4)));

            var clerks = new List<DbaMemoryClerk>(20);
            if (await reader.NextResultAsync(timeout.Token))
                while (await reader.ReadAsync(timeout.Token)) clerks.Add(new(Safe(reader, 0), reader.GetDecimal(1)));

            var tempDb = new List<DbaTempDbFile>(16);
            if (await reader.NextResultAsync(timeout.Token))
                while (await reader.ReadAsync(timeout.Token))
                    tempDb.Add(new(reader.GetInt32(0), Safe(reader, 1), Safe(reader, 2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetBoolean(7)));

            var requests = new List<DbaActiveRequest>(20);
            if (await reader.NextResultAsync(timeout.Token))
                while (await reader.ReadAsync(timeout.Token))
                    requests.Add(new(
                        reader.GetInt32(0), Safe(reader, 1, "unknown"), Safe(reader, 2), Safe(reader, 3), reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7),
                        Safe(reader, 8, string.Empty), reader.GetInt32(9), DbaCommandCenterService.SanitizeQueryText(reader.IsDBNull(10) ? null : reader.GetString(10))));

            var config = new List<DbaConfigValue>(10);
            if (await reader.NextResultAsync(timeout.Token))
                while (await reader.ReadAsync(timeout.Token))
                {
                    var name = Safe(reader, 0);
                    var configured = Convert.ToInt32(reader.GetValue(1));
                    var inUse = Convert.ToInt32(reader.GetValue(2));
                    config.Add(new(name, configured, inUse, ConfigurationGuidance(name, inUse)));
                }

            decimal planCacheMb = 0;
            decimal singleUseMb = 0;
            var singleUseCount = 0;
            if (await reader.NextResultAsync(timeout.Token) && await reader.ReadAsync(timeout.Token))
            {
                planCacheMb = reader.GetDecimal(0);
                singleUseMb = reader.GetDecimal(1);
                singleUseCount = reader.GetInt32(2);
            }

            var snapshot = new DbaAdvancedSnapshot(registrationId, timeProvider.GetUtcNow(), true, "Advanced DBA inspection available", waits, clerks, tempDb, requests, config, planCacheMb, singleUseMb, singleUseCount);
            Cache[registrationId] = snapshot;
            TrimCache();
            return snapshot;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CacheUnavailable(registrationId, "Advanced DBA inspection timed out. Existing cached evidence remains available.");
        }
        catch (SqlException)
        {
            return CacheUnavailable(registrationId, "Advanced diagnostics could not read one or more server DMVs. Verify VIEW SERVER STATE / VIEW SERVER PERFORMANCE STATE permissions and SQL Server version support.");
        }
    }

    private DbaAdvancedSnapshot CacheUnavailable(Guid registrationId, string message)
    {
        var snapshot = new DbaAdvancedSnapshot(registrationId, timeProvider.GetUtcNow(), false, message, [], [], [], [], [], 0, 0, 0);
        Cache[registrationId] = snapshot;
        TrimCache();
        return snapshot;
    }

    private static IReadOnlyList<DbaAdvancedRecommendation> BuildRecommendations(DbaAdvancedSnapshot? snapshot)
    {
        if (snapshot is null || !snapshot.IsAvailable) return [];
        var list = new List<DbaAdvancedRecommendation>();

        var topWait = snapshot.Waits.FirstOrDefault();
        if (topWait is not null && topWait.WaitTimeMs > 0)
        {
            var guidance = topWait.WaitType switch
            {
                var value when value.StartsWith("PAGEIOLATCH_", StringComparison.OrdinalIgnoreCase) => "Investigate storage latency, buffer-pool pressure and high-read queries before changing storage or memory.",
                var value when value.StartsWith("WRITELOG", StringComparison.OrdinalIgnoreCase) => "Investigate transaction-log latency, log growth, long transactions and log backup cadence.",
                var value when value.StartsWith("LCK_", StringComparison.OrdinalIgnoreCase) => "Trace the blocking chain and transaction ownership before killing sessions or changing isolation.",
                var value when value.StartsWith("CX", StringComparison.OrdinalIgnoreCase) => "Correlate parallel waits with CPU pressure, query plans, MAXDOP and cost threshold; do not tune from the wait name alone.",
                var value when value.StartsWith("RESOURCE_SEMAPHORE", StringComparison.OrdinalIgnoreCase) => "Review memory grants, cardinality estimates, sorts/hashes and max server memory headroom.",
                _ => "Correlate this wait with CPU, I/O, memory grants, blocking and workload timing before changing configuration."
            };
            list.Add(new("High", "Waits", $"Review dominant wait: {topWait.WaitType}", $"{topWait.WaitTimeMs:N0} ms wait time; {topWait.ResourceWaitPercent:N1}% resource-wait share in the filtered sample.", guidance,
                "SELECT TOP (30) wait_type, waiting_tasks_count, wait_time_ms, signal_wait_time_ms FROM sys.dm_os_wait_stats ORDER BY wait_time_ms DESC;",
                "-- Re-sample after the DBA-approved change and compare wait mix plus user-facing latency."));
        }

        if (snapshot.TempDbFiles.Count > 0)
        {
            var dataFiles = snapshot.TempDbFiles.Where(file => string.Equals(file.FileType, "ROWS", StringComparison.OrdinalIgnoreCase)).ToArray();
            var unequal = dataFiles.Length > 1 && dataFiles.Max(file => file.SizeMb) - dataFiles.Min(file => file.SizeMb) > 64m;
            var percentGrowth = snapshot.TempDbFiles.Any(file => file.IsPercentGrowth);
            var lowFree = snapshot.TempDbFiles.Where(file => file.FreeMb < Math.Max(256m, file.SizeMb * 0.10m)).ToArray();
            if (unequal || percentGrowth || lowFree.Length > 0)
                list.Add(new("High", "TempDB", "Normalize TempDB capacity and growth", $"Data files={dataFiles.Length}; unequal sizes={unequal}; percent growth={percentGrowth}; low-free files={lowFree.Length}.", "Pre-size TempDB for the workload, keep data files equally sized with fixed-MB growth, and validate storage latency. Add files only from measured allocation contention evidence.",
                    "USE [master]; SELECT file_id,name,size*8.0/1024 AS SizeMB,growth,is_percent_growth,physical_name FROM tempdb.sys.database_files; -- generate ALTER DATABASE tempdb MODIFY FILE only after capacity review",
                    "SELECT name,size*8.0/1024 AS SizeMB,growth,is_percent_growth FROM tempdb.sys.database_files;"));
        }

        if (snapshot.PlanCacheMb > 0 && snapshot.SingleUsePlanCacheMb / snapshot.PlanCacheMb >= 0.30m && snapshot.SingleUsePlanCount >= 100)
            list.Add(new("Medium", "Plan cache", "Review single-use plan cache pressure", $"Single-use plans consume {snapshot.SingleUsePlanCacheMb:N1} MB of {snapshot.PlanCacheMb:N1} MB cached plans across {snapshot.SingleUsePlanCount:N0} entries.", "Confirm ad-hoc workload behavior and parameterization. Consider 'optimize for ad hoc workloads' only after validating application behavior and memory pressure.",
                "EXEC sys.sp_configure N'optimize for ad hoc workloads'; -- REVIEW ONLY. If approved: RECONFIGURE after setting the intended value.",
                "SELECT objtype,usecounts,SUM(size_in_bytes)/1048576.0 AS MB,COUNT(*) AS Plans FROM sys.dm_exec_cached_plans GROUP BY objtype,usecounts ORDER BY MB DESC;"));

        var longRequest = snapshot.ActiveRequests.OrderByDescending(request => request.ElapsedMs).FirstOrDefault(request => request.ElapsedMs >= 30000);
        if (longRequest is not null)
            list.Add(new(longRequest.BlockingSessionId > 0 ? "Critical" : "High", "Active workload", $"Investigate long-running session {longRequest.SessionId}", $"DB={longRequest.DatabaseName}; elapsed={longRequest.ElapsedMs:N0} ms; CPU={longRequest.CpuMs:N0} ms; reads={longRequest.LogicalReads:N0}; blocker={longRequest.BlockingSessionId}; query={longRequest.QuerySnippet}", "Capture the execution plan and transaction context. Fix root cause in query/index/application behavior before terminating a session.",
                $"SELECT r.session_id,r.blocking_session_id,r.status,r.command,r.cpu_time,r.total_elapsed_time,r.logical_reads,r.writes,r.wait_type,r.wait_resource FROM sys.dm_exec_requests r WHERE r.session_id={longRequest.SessionId};",
                "SELECT session_id,status,cpu_time,total_elapsed_time,logical_reads,writes,wait_type,blocking_session_id FROM sys.dm_exec_requests WHERE session_id > 50 ORDER BY total_elapsed_time DESC;"));

        var maxMemory = snapshot.Configuration.FirstOrDefault(item => string.Equals(item.Name, "max server memory (MB)", StringComparison.OrdinalIgnoreCase));
        if (maxMemory is not null && maxMemory.InUseValue >= int.MaxValue / 4)
            list.Add(new("High", "Memory configuration", "Set an intentional max server memory ceiling", $"max server memory (MB) is effectively unbounded at {maxMemory.InUseValue:N0} MB.", "Calculate a ceiling from physical RAM, OS/agent/backup/SSIS/AV needs and non-buffer-pool workload; change only during an approved capacity window.",
                "EXEC sys.sp_configure N'max server memory (MB)', <APPROVED_MB>; RECONFIGURE; -- REVIEW/APPROVAL REQUIRED",
                "SELECT name,value_in_use FROM sys.configurations WHERE name=N'max server memory (MB)'; SELECT total_physical_memory_kb,available_physical_memory_kb,system_memory_state_desc FROM sys.dm_os_sys_memory;"));

        return list;
    }

    private static string ConfigurationGuidance(string name, int inUse) => name switch
    {
        "max server memory (MB)" => "Reserve explicit OS and non-SQL headroom; never set from a generic percentage alone.",
        "max degree of parallelism" => "Validate against NUMA topology, workload and Query Store evidence before changing MAXDOP.",
        "cost threshold for parallelism" => "Review parallel plan quality and CPU pressure; tune together with MAXDOP, not in isolation.",
        "optimize for ad hoc workloads" => "Useful only when single-use ad-hoc plans materially consume plan cache.",
        _ => $"Current in-use value: {inUse}."
    };

    private static string Safe(SqlDataReader reader, int ordinal, string fallback = "unknown") => reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);

    private static void TrimCache()
    {
        if (Cache.Count <= 200) return;
        foreach (var key in Cache.OrderBy(pair => pair.Value.CollectedAtUtc).Take(Cache.Count - 200).Select(pair => pair.Key)) Cache.TryRemove(key, out _);
    }

    private const string AdvancedSql = """
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

;WITH waits AS
(
    SELECT TOP (20)
        wait_type,
        waiting_tasks_count,
        wait_time_ms,
        signal_wait_time_ms,
        wait_time_ms - signal_wait_time_ms AS resource_wait_ms
    FROM sys.dm_os_wait_stats
    WHERE wait_type NOT IN
    (
        N'BROKER_EVENTHANDLER',N'BROKER_RECEIVE_WAITFOR',N'BROKER_TASK_STOP',N'BROKER_TO_FLUSH',
        N'CHECKPOINT_QUEUE',N'CLR_AUTO_EVENT',N'CLR_MANUAL_EVENT',N'DIRTY_PAGE_POLL',N'DISPATCHER_QUEUE_SEMAPHORE',
        N'FT_IFTS_SCHEDULER_IDLE_WAIT',N'HADR_FILESTREAM_IOMGR_IOCOMPLETION',N'LAZYWRITER_SLEEP',N'LOGMGR_QUEUE',
        N'ONDEMAND_TASK_QUEUE',N'QDS_ASYNC_QUEUE',N'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP',N'REQUEST_FOR_DEADLOCK_SEARCH',
        N'RESOURCE_QUEUE',N'SERVER_IDLE_CHECK',N'SLEEP_BPOOL_FLUSH',N'SLEEP_DBSTARTUP',N'SLEEP_DCOMSTARTUP',N'SLEEP_MASTERDBREADY',
        N'SLEEP_MASTERMDREADY',N'SLEEP_MASTERUPGRADED',N'SLEEP_MSDBSTARTUP',N'SLEEP_SYSTEMTASK',N'SLEEP_TASK',N'SLEEP_TEMPDBSTARTUP',
        N'SNI_HTTP_ACCEPT',N'SP_SERVER_DIAGNOSTICS_SLEEP',N'SQLTRACE_BUFFER_FLUSH',N'SQLTRACE_INCREMENTAL_FLUSH_SLEEP',N'WAITFOR',
        N'XE_DISPATCHER_JOIN',N'XE_DISPATCHER_WAIT',N'XE_TIMER_EVENT'
    )
      AND wait_time_ms > 0
    ORDER BY wait_time_ms DESC
), totals AS (SELECT SUM(resource_wait_ms) AS total_resource_wait_ms FROM waits)
SELECT w.wait_type, CONVERT(bigint,w.waiting_tasks_count), CONVERT(bigint,w.wait_time_ms), CONVERT(bigint,w.signal_wait_time_ms),
       CONVERT(decimal(9,2), CASE WHEN t.total_resource_wait_ms > 0 THEN 100.0*w.resource_wait_ms/t.total_resource_wait_ms ELSE 0 END)
FROM waits w CROSS JOIN totals t ORDER BY w.wait_time_ms DESC;

SELECT TOP (20) type, CONVERT(decimal(18,1), SUM(pages_kb)/1024.0) AS MemoryMb
FROM sys.dm_os_memory_clerks
GROUP BY type
HAVING SUM(pages_kb) > 0
ORDER BY SUM(pages_kb) DESC;

;WITH usage AS
(
    SELECT file_id,
           CONVERT(decimal(18,1),(unallocated_extent_page_count + user_object_reserved_page_count + internal_object_reserved_page_count + version_store_reserved_page_count + mixed_extent_page_count)*8.0/1024.0) AS TrackedMb,
           CONVERT(decimal(18,1),(user_object_reserved_page_count + internal_object_reserved_page_count + version_store_reserved_page_count + mixed_extent_page_count)*8.0/1024.0) AS UsedMb
    FROM tempdb.sys.dm_db_file_space_usage
), files AS
(
    SELECT file_id,name,type_desc,size,growth,is_percent_growth
    FROM tempdb.sys.database_files
)
SELECT f.file_id,f.name,f.type_desc,
       CONVERT(decimal(18,1),f.size*8.0/1024.0) AS SizeMb,
       CONVERT(decimal(18,1),COALESCE(u.UsedMb,0)) AS UsedMb,
       CONVERT(decimal(18,1),CASE WHEN f.size*8.0/1024.0-COALESCE(u.UsedMb,0) < 0 THEN 0 ELSE f.size*8.0/1024.0-COALESCE(u.UsedMb,0) END) AS FreeMb,
       CONVERT(decimal(18,1),CASE WHEN f.is_percent_growth=1 THEN f.growth ELSE f.growth*8.0/1024.0 END) AS GrowthValue,
       CONVERT(bit,f.is_percent_growth)
FROM files f LEFT JOIN usage u ON u.file_id=f.file_id
ORDER BY f.type,f.file_id;

SELECT TOP (20)
    r.session_id,
    COALESCE(DB_NAME(r.database_id),N'unknown') AS DatabaseName,
    r.status,
    r.command,
    CONVERT(bigint,r.cpu_time),
    CONVERT(bigint,r.total_elapsed_time),
    CONVERT(bigint,r.logical_reads),
    CONVERT(bigint,r.writes),
    COALESCE(r.wait_type,N''),
    COALESCE(r.blocking_session_id,0),
    CONVERT(nvarchar(max),SUBSTRING(t.text,(r.statement_start_offset/2)+1,CASE WHEN r.statement_end_offset=-1 THEN LEN(CONVERT(nvarchar(max),t.text))*2 ELSE r.statement_end_offset-r.statement_start_offset END/2+1))
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.session_id <> @@SPID AND r.session_id > 50
ORDER BY r.total_elapsed_time DESC;

SELECT name, CONVERT(int,value), CONVERT(int,value_in_use)
FROM sys.configurations
WHERE name IN (N'max server memory (MB)',N'max degree of parallelism',N'cost threshold for parallelism',N'optimize for ad hoc workloads')
ORDER BY name;

SELECT
    CONVERT(decimal(18,1),SUM(CONVERT(bigint,size_in_bytes))/1048576.0) AS PlanCacheMb,
    CONVERT(decimal(18,1),SUM(CASE WHEN objtype=N'Adhoc' AND usecounts=1 THEN CONVERT(bigint,size_in_bytes) ELSE 0 END)/1048576.0) AS SingleUsePlanCacheMb,
    CONVERT(int,SUM(CASE WHEN objtype=N'Adhoc' AND usecounts=1 THEN 1 ELSE 0 END)) AS SingleUsePlanCount
FROM sys.dm_exec_cached_plans;
""";
}
