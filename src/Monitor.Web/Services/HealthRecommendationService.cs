using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IHealthRecommendationService
{
    HealthRecommendation? Create(HealthIncident incident);
}

public sealed class HealthRecommendationService : IHealthRecommendationService
{
    public HealthRecommendation? Create(HealthIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        return incident.RuleId switch
        {
            "snapshot.stale" => Recommendation(
                incident,
                "Monitoring evidence is older than the fresh snapshot window, so the current server condition cannot be confirmed.",
                "Fresh evidence is required before treating later healthy readings as incident resolution.",
                RecommendationConfidence.High,
                [
                    Step(1, "Check the collection path", "Verify the registered SQL endpoint is reachable and that the configured monitoring identity still has the required permissions."),
                    Step(2, "Request one controlled refresh", "Use the backend-controlled refresh action after its throttle window. Do not add browser polling or repeated manual refresh loops."),
                    Step(3, "Escalate persistent staleness", "If fresh collection still fails, inspect Test Connection and collector diagnostics before changing SQL Server configuration.")]),

            "database.unavailable" => Recommendation(
                incident,
                "One or more databases are not ONLINE in the latest snapshot.",
                "Availability state is a direct catalog fact; the next step is to identify the affected database state before choosing restore, recovery or availability-group actions.",
                RecommendationConfidence.High,
                [
                    Step(1, "Identify non-online databases", "Run the read-only diagnostic query and review each database state and access mode."),
                    Step(2, "Classify the recovery path", "For RESTORING/RECOVERING/RECOVERY_PENDING, validate restore/recovery or HA progress. For OFFLINE, confirm the administrative reason before bringing it online."),
                    Step(3, "Require change approval", "Any ALTER DATABASE, RESTORE, failover or recovery command must be reviewed and executed manually under the environment change procedure.", true)],
                DatabaseStateSql()),

            "database.suspect" => Recommendation(
                incident,
                "At least one database reports SUSPECT state.",
                "SUSPECT is a critical database state. Repair choices depend on error-log evidence, backup quality and corruption scope; automatic repair would be unsafe.",
                RecommendationConfidence.High,
                [
                    Step(1, "Preserve evidence", "Review SQL Server error logs and storage/infrastructure events around the state transition before attempting recovery."),
                    Step(2, "Validate recovery options", "Confirm the newest known-good backups and determine whether restore/failover is safer than repair."),
                    Step(3, "Avoid automatic repair", "Do not run emergency-mode repair or data-loss repair automatically. Any repair/restore action requires explicit DBA approval and a recovery plan.", true)],
                SuspectDatabaseSql()),

            "backup.full-gap" => Recommendation(
                incident,
                "One or more online user databases do not have a qualifying full backup inside the 24-hour policy window.",
                "The snapshot contains aggregate coverage only. A read-only msdb lookup is needed to identify which databases are outside policy before scheduling backup work.",
                RecommendationConfidence.High,
                [
                    Step(1, "Identify backup gaps", "Run the read-only backup-history diagnostic to compare online user databases with their latest non-copy-only full backup."),
                    Step(2, "Validate the backup policy", "Confirm whether each database is expected to receive full backups and whether another approved backup platform owns the schedule."),
                    Step(3, "Schedule the missing backup", "If the gap is real, run or schedule the approved backup job manually and verify completion plus recoverability evidence.", true)],
                BackupGapSql()),

            "agent.failed-job" => Recommendation(
                incident,
                "One or more SQL Agent jobs report a failed last run.",
                "The health snapshot intentionally stores only aggregate counts. Job names and command text are not collected, so failure triage must remain a deliberate DBA action.",
                RecommendationConfidence.High,
                [
                    Step(1, "List failed last-run jobs", "Run the read-only msdb diagnostic to identify affected jobs without retrieving job-step commands."),
                    Step(2, "Review job history", "Open the affected SQL Agent history and determine whether the failure is transient, dependency-related or repeatable."),
                    Step(3, "Rerun only after diagnosis", "Do not automatically start jobs. Correct the dependency/configuration under change control, then rerun the approved job manually.", true)],
                AgentFailureSql()),

            "blocking.active" => Recommendation(
                incident,
                "Active requests are blocked by other sessions.",
                "Blocking may be normal and short-lived or may indicate an application/transaction problem. The snapshot deliberately excludes SQL text and client identity.",
                RecommendationConfidence.High,
                [
                    Step(1, "Confirm the blocking chain", "Run the bounded read-only request diagnostic to capture session IDs, blocker IDs, wait type and wait duration."),
                    Step(2, "Assess business impact", "Correlate the blocked sessions with the owning workload using approved DBA tooling before terminating anything."),
                    Step(3, "Do not kill sessions automatically", "KILL, rollback, index changes and application transaction changes require explicit DBA/change approval.", true)],
                BlockingSql()),

            "memory.pressure" => Recommendation(
                incident,
                "SQL Server reports a physical or virtual low-memory signal.",
                "The signal is authoritative for pressure, but root cause can involve max server memory, OS competition, workload shape or external processes.",
                RecommendationConfidence.High,
                [
                    Step(1, "Confirm OS and SQL process memory", "Run the read-only memory diagnostic and compare available physical memory with SQL process usage and low-memory flags."),
                    Step(2, "Check configuration and competition", "Review max server memory, other services on the host and recent workload changes before changing memory settings."),
                    Step(3, "Change memory only with evidence", "Any sp_configure or service-level change requires capacity analysis and explicit approval.", true)],
                MemorySql()),

            "performance.runnable" => Recommendation(
                incident,
                "Runnable task count is above the current deterministic pressure threshold.",
                "Runnable tasks can indicate CPU scheduler pressure, but the aggregate count alone does not establish the root cause.",
                RecommendationConfidence.Medium,
                [
                    Step(1, "Confirm scheduler pressure", "Run the read-only scheduler diagnostic and compare runnable/current task counts across visible online schedulers."),
                    Step(2, "Correlate with workload timing", "Check whether the pressure is sustained and aligns with known batch, reporting, ETL or maintenance windows."),
                    Step(3, "Tune only after attribution", "Query/index/configuration changes require workload evidence; do not infer a specific tuning action from runnable count alone.", true)],
                SchedulerSql()),

            _ => null
        };
    }

    private static HealthRecommendation Recommendation(
        HealthIncident incident,
        string problem,
        string rationale,
        RecommendationConfidence confidence,
        IReadOnlyList<RemediationStep> steps,
        DiagnosticSqlProposal? sql = null) =>
        new(
            incident.RuleId,
            incident.Severity,
            problem,
            incident.Evidence,
            rationale,
            confidence,
            steps,
            sql);

    private static RemediationStep Step(int order, string title, string detail, bool requiresChangeApproval = false) =>
        new(order, title, detail, requiresChangeApproval);

    private static DiagnosticSqlProposal DatabaseStateSql() => new(
        "Database state inventory",
        "Identify databases that are not ONLINE and capture their current state without changing anything.",
        """
        SELECT name, state_desc, user_access_desc, is_read_only
        FROM sys.databases
        WHERE state_desc <> N'ONLINE'
        ORDER BY name;
        """,
        "Read-only catalog query. Review results before proposing any ALTER DATABASE, RESTORE or failover action.");

    private static DiagnosticSqlProposal SuspectDatabaseSql() => new(
        "Critical database state inventory",
        "Confirm SUSPECT/EMERGENCY and other non-online states with recovery-model context.",
        """
        SELECT name, state_desc, recovery_model_desc, page_verify_option_desc, is_read_only
        FROM sys.databases
        WHERE state_desc <> N'ONLINE'
        ORDER BY name;
        """,
        "Read-only catalog query. It does not run CHECKDB, repair, restore, ALTER DATABASE or failover commands.");

    private static DiagnosticSqlProposal BackupGapSql() => new(
        "Latest full backup by online user database",
        "Identify online user databases whose latest non-copy-only full backup is outside the 24-hour policy window.",
        """
        SELECT d.name,
               MAX(b.backup_finish_date) AS last_full_backup_utc
        FROM sys.databases AS d
        LEFT JOIN msdb.dbo.backupset AS b
          ON b.database_name = d.name
         AND b.type = 'D'
         AND b.is_copy_only = 0
        WHERE d.database_id > 4
          AND d.state = 0
        GROUP BY d.name
        ORDER BY last_full_backup_utc, d.name;
        """,
        "Read-only catalog/msdb history query. It does not start a backup or modify backup jobs.");

    private static DiagnosticSqlProposal AgentFailureSql() => new(
        "SQL Agent failed last-run inventory",
        "Identify jobs whose recorded last run outcome is failed without reading job-step command text.",
        """
        SELECT j.name, j.enabled, js.last_run_date, js.last_run_time, js.last_run_outcome
        FROM msdb.dbo.sysjobs AS j
        INNER JOIN msdb.dbo.sysjobservers AS js ON js.job_id = j.job_id
        WHERE js.last_run_outcome = 0
        ORDER BY j.name;
        """,
        "Read-only msdb metadata query. It does not expose job-step commands or start/stop any job.");

    private static DiagnosticSqlProposal BlockingSql() => new(
        "Active blocking inventory",
        "Capture bounded request-level blocking metadata without SQL text, query plans or client identity.",
        """
        SELECT session_id, blocking_session_id, status, wait_type, wait_time
        FROM sys.dm_exec_requests
        WHERE blocking_session_id > 0
          AND session_id <> @@SPID
        ORDER BY wait_time DESC;
        """,
        "Read-only DMV query. It does not retrieve SQL text or execute KILL/rollback commands.");

    private static DiagnosticSqlProposal MemorySql() => new(
        "SQL/OS memory pressure snapshot",
        "Confirm SQL process memory, available physical memory and low-memory flags.",
        """
        SELECT sm.total_physical_memory_kb,
               sm.available_physical_memory_kb,
               pm.physical_memory_in_use_kb,
               pm.memory_utilization_percentage,
               pm.process_physical_memory_low,
               pm.process_virtual_memory_low,
               sm.system_memory_state_desc
        FROM sys.dm_os_sys_memory AS sm
        CROSS JOIN sys.dm_os_process_memory AS pm;
        """,
        "Read-only DMV query. It does not change max server memory or any operating-system setting.");

    private static DiagnosticSqlProposal SchedulerSql() => new(
        "Visible scheduler pressure",
        "Inspect runnable and current task counts on online visible schedulers.",
        """
        SELECT scheduler_id, current_tasks_count, runnable_tasks_count, pending_disk_io_count
        FROM sys.dm_os_schedulers
        WHERE status = N'VISIBLE ONLINE'
        ORDER BY runnable_tasks_count DESC, scheduler_id;
        """,
        "Read-only DMV query. Runnable count is diagnostic evidence, not a tuning command or root-cause conclusion.");
}
