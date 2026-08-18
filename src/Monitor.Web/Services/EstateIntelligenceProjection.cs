using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record EstateIntelligenceSignal(
    string Label,
    string Value,
    string Detail,
    string State);

public sealed record EstateIntelligenceSummary(
    string ContextLabel,
    string Severity,
    string Headline,
    string ContextMessage,
    string NextAction,
    int RegisteredTargets,
    int ObservedTargets,
    int FreshTargets,
    int StaleTargets,
    int UnavailableTargets,
    int EvidenceCoveragePercent,
    int DatabaseOnline,
    int DatabaseTotal,
    int DatabaseRiskCount,
    int BackupGapCount,
    int JobFailureCount,
    int MemoryPressureCount,
    int BlockingServerCount,
    int BlockedRequestCount,
    int PerformancePressureCount,
    int RunnableTaskCount,
    int PendingIoRequestCount,
    long TotalAllocatedBytes,
    long DataAllocatedBytes,
    long LogAllocatedBytes,
    int? OldestSnapshotAgeSeconds,
    IReadOnlyList<EstateIntelligenceSignal> Signals)
{
    public static EstateIntelligenceSummary Unavailable(string? path) => new(
        EstateIntelligenceProjection.ContextFor(path),
        "unknown",
        "Estate intelligence unavailable",
        "The shared intelligence surface could not read cached operational state. No healthy state is inferred.",
        "Review application/shared-state readiness before acting on missing intelligence.",
        0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, null,
        [new("EVIDENCE", "Unavailable", "Cached intelligence state could not be read", "unknown")]);
}

public static class EstateIntelligenceProjection
{
    public static EstateIntelligenceSummary Build(
        int registeredTargets,
        IReadOnlyList<HealthModuleServerViewModel> servers,
        string? path)
    {
        registeredTargets = Math.Max(0, registeredTargets);
        servers ??= [];

        var observed = servers.Count;
        var fresh = servers.Count(server => server.Source == ServerDataSource.LiveFresh);
        var stale = servers.Count(server => server.Source == ServerDataSource.LiveStale);
        var unavailable = Math.Max(0, registeredTargets - observed);
        var coverage = registeredTargets == 0
            ? 0
            : Math.Clamp((int)Math.Round(observed * 100d / registeredTargets), 0, 100);

        var databaseOnline = servers.Sum(server => Math.Max(0, server.DatabaseOnline));
        var databaseTotal = servers.Sum(server => Math.Max(0, server.DatabaseTotal));
        var databaseRisk = servers.Sum(server =>
        {
            var detail = server.Databases;
            var aggregateOffline = Math.Max(0, server.DatabaseTotal - server.DatabaseOnline);
            if (detail is null) return aggregateOffline;
            var explicitRisk = Math.Max(0, detail.Suspect)
                + Math.Max(0, detail.Emergency)
                + Math.Max(0, detail.RecoveryPending)
                + Math.Max(0, detail.OfflineOrOther);
            return Math.Max(aggregateOffline, explicitRisk);
        });

        var backupGap = servers.Sum(server => Math.Max(0, server.Backups?.MissingFullBackupLast24Hours ?? 0));
        var jobsFailed = servers.Sum(server => Math.Max(0, server.Jobs?.FailedLastRun ?? 0));
        var memoryPressure = servers.Count(server =>
            server.Memory is not null && MemoryIntelligenceProjection.Build(server.Memory).NeedsAttention);
        var blockingServers = servers.Count(server => server.Blocking?.BlockedRequests is > 0);
        var blockedRequests = servers.Sum(server => Math.Max(0, server.Blocking?.BlockedRequests ?? 0));
        var performancePressure = servers.Count(server =>
            server.Performance is not null
            && (server.Performance.RunnableTasks > 4 || server.Performance.PendingIoRequests > 0));
        var runnableTasks = servers.Sum(server => Math.Max(0, server.Performance?.RunnableTasks ?? 0));
        var pendingIo = servers.Sum(server => Math.Max(0, server.Performance?.PendingIoRequests ?? 0));
        var totalAllocated = servers.Sum(server => Math.Max(0L, server.Storage?.TotalAllocatedBytes ?? 0L));
        var dataAllocated = servers.Sum(server => Math.Max(0L, server.Storage?.DataAllocatedBytes ?? 0L));
        var logAllocated = servers.Sum(server => Math.Max(0L, server.Storage?.LogAllocatedBytes ?? 0L));
        int? oldestAge = observed == 0 ? null : servers.Max(server => Math.Max(0, server.AgeSeconds));

        var severity = registeredTargets == 0 || observed == 0
            ? "unknown"
            : servers.Any(server => server.Databases is { Suspect: > 0 } or { Emergency: > 0 } or { RecoveryPending: > 0 })
                ? "critical"
                : unavailable > 0
                    || stale > 0
                    || databaseRisk > 0
                    || backupGap > 0
                    || jobsFailed > 0
                    || memoryPressure > 0
                    || blockedRequests > 0
                    || performancePressure > 0
                        ? "warning"
                        : "healthy";

        var headline = severity switch
        {
            "critical" => "Critical database-state evidence requires DBA review",
            "warning" => "Estate evidence contains conditions that need attention",
            "healthy" => "Collected estate evidence is within current intelligence thresholds",
            _ when registeredTargets == 0 => "No active SQL targets are registered",
            _ => "Registered targets do not yet have usable cached evidence"
        };

        var nextAction = BuildNextAction(
            registeredTargets,
            unavailable,
            stale,
            databaseRisk,
            backupGap,
            jobsFailed,
            memoryPressure,
            blockedRequests,
            performancePressure);

        var context = ContextFor(path);
        var message = ContextMessage(path, coverage, fresh, stale, unavailable);
        var signals = BuildSignals(
            path,
            registeredTargets,
            observed,
            fresh,
            stale,
            unavailable,
            coverage,
            databaseOnline,
            databaseTotal,
            databaseRisk,
            backupGap,
            jobsFailed,
            memoryPressure,
            blockingServers,
            blockedRequests,
            performancePressure,
            runnableTasks,
            pendingIo,
            totalAllocated,
            dataAllocated,
            logAllocated,
            oldestAge);

        return new(
            context,
            severity,
            headline,
            message,
            nextAction,
            registeredTargets,
            observed,
            fresh,
            stale,
            unavailable,
            coverage,
            databaseOnline,
            databaseTotal,
            databaseRisk,
            backupGap,
            jobsFailed,
            memoryPressure,
            blockingServers,
            blockedRequests,
            performancePressure,
            runnableTasks,
            pendingIo,
            totalAllocated,
            dataAllocated,
            logAllocated,
            oldestAge,
            signals);
    }

    public static string ContextFor(string? path)
    {
        var normalized = Normalize(path);
        if (normalized.StartsWith("/database-health", StringComparison.Ordinal)) return "DATABASE INTELLIGENCE";
        if (normalized.StartsWith("/memory-health", StringComparison.Ordinal)) return "MEMORY INTELLIGENCE";
        if (normalized.StartsWith("/performance-health", StringComparison.Ordinal)) return "PERFORMANCE INTELLIGENCE";
        if (normalized.StartsWith("/backups", StringComparison.Ordinal)) return "BACKUP INTELLIGENCE";
        if (normalized.StartsWith("/jobs", StringComparison.Ordinal)) return "SQL AGENT INTELLIGENCE";
        if (normalized.StartsWith("/storage", StringComparison.Ordinal)) return "STORAGE INTELLIGENCE";
        if (normalized.StartsWith("/blocking", StringComparison.Ordinal)) return "BLOCKING INTELLIGENCE";
        if (normalized.StartsWith("/alerts", StringComparison.Ordinal)) return "INCIDENT INTELLIGENCE";
        if (normalized.StartsWith("/servers", StringComparison.Ordinal)) return "SERVER ESTATE INTELLIGENCE";
        if (normalized.StartsWith("/recommendations", StringComparison.Ordinal)) return "RECOMMENDATION INTELLIGENCE";
        if (normalized.StartsWith("/reports", StringComparison.Ordinal)) return "REPORTING INTELLIGENCE";
        if (normalized.StartsWith("/enterprise", StringComparison.Ordinal)) return "ENTERPRISE INTELLIGENCE";
        if (normalized.StartsWith("/observability", StringComparison.Ordinal)) return "OBSERVABILITY INTELLIGENCE";
        if (normalized.StartsWith("/audit", StringComparison.Ordinal)) return "AUDIT INTELLIGENCE";
        if (normalized.StartsWith("/settings", StringComparison.Ordinal)) return "READINESS INTELLIGENCE";
        return "ESTATE INTELLIGENCE";
    }

    private static string ContextMessage(string? path, int coverage, int fresh, int stale, int unavailable)
    {
        var normalized = Normalize(path);
        var evidence = $"Evidence coverage {coverage}% · {fresh} fresh · {stale} stale · {unavailable} unavailable.";

        if (normalized.StartsWith("/database-health", StringComparison.Ordinal))
            return $"Database availability and retained state evidence are prioritized here. {evidence}";
        if (normalized.StartsWith("/memory-health", StringComparison.Ordinal))
            return $"Memory pressure combines SQL/OS flags, grants and bounded configuration evidence. {evidence}";
        if (normalized.StartsWith("/performance-health", StringComparison.Ordinal))
            return $"Scheduler, pending-I/O and bounded wait evidence are interpreted without collecting on navigation. {evidence}";
        if (normalized.StartsWith("/backups", StringComparison.Ordinal))
            return $"Backup coverage highlights missing full-backup evidence in the current policy window. {evidence}";
        if (normalized.StartsWith("/jobs", StringComparison.Ordinal))
            return $"SQL Agent reliability focuses on enabled jobs and failed last-run evidence. {evidence}";
        if (normalized.StartsWith("/storage", StringComparison.Ordinal))
            return $"Storage intelligence reports SQL allocation and bounded I/O evidence only; disk free space is not inferred. {evidence}";
        if (normalized.StartsWith("/blocking", StringComparison.Ordinal))
            return $"Blocking intelligence uses bounded request counts and maximum observed wait only. {evidence}";
        if (normalized.StartsWith("/alerts", StringComparison.Ordinal))
            return $"Incident workflow should be interpreted alongside the latest cached estate posture. {evidence}";
        if (normalized.StartsWith("/recommendations", StringComparison.Ordinal))
            return $"Recommendations remain evidence-backed and advisory; no automatic SQL configuration change is authorized. {evidence}";
        if (normalized.StartsWith("/reports", StringComparison.Ordinal))
            return $"Reports inherit the same cached-evidence truth used by operational pages. {evidence}";
        if (normalized.StartsWith("/observability", StringComparison.Ordinal) || normalized.StartsWith("/audit", StringComparison.Ordinal) || normalized.StartsWith("/settings", StringComparison.Ordinal))
            return $"Operational readiness is shown together with monitored-estate evidence quality. {evidence}";
        return $"Cross-estate intelligence is derived from cached real SQL snapshots only. {evidence}";
    }

    private static IReadOnlyList<EstateIntelligenceSignal> BuildSignals(
        string? path,
        int registered,
        int observed,
        int fresh,
        int stale,
        int unavailable,
        int coverage,
        int databaseOnline,
        int databaseTotal,
        int databaseRisk,
        int backupGap,
        int jobsFailed,
        int memoryPressure,
        int blockingServers,
        int blockedRequests,
        int performancePressure,
        int runnableTasks,
        int pendingIo,
        long totalAllocated,
        long dataAllocated,
        long logAllocated,
        int? oldestAge)
    {
        var normalized = Normalize(path);
        var evidenceState = unavailable > 0 ? "warning" : stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown";
        var evidenceDetail = registered == 0
            ? "No active registered targets"
            : $"{observed}/{registered} observed · {fresh} fresh · {stale} stale";
        var age = oldestAge.HasValue ? FormatAge(oldestAge.Value) : "Not collected";
        var evidenceSignal = new EstateIntelligenceSignal("EVIDENCE", $"{coverage}%", $"{evidenceDetail} · oldest {age}", evidenceState);

        if (normalized.StartsWith("/database-health", StringComparison.Ordinal))
        {
            return
            [
                new("DATABASES ONLINE", databaseTotal > 0 ? $"{databaseOnline}/{databaseTotal}" : "Not collected", "Latest cached aggregate database state", databaseRisk > 0 ? "warning" : databaseTotal > 0 ? "healthy" : "unknown"),
                new("DATABASE RISK", databaseRisk.ToString(), "Offline/non-online or retained actionable state evidence", databaseRisk > 0 ? "critical" : databaseTotal > 0 ? "healthy" : "unknown"),
                new("BACKUP GAPS", backupGap.ToString(), "Missing full backup in current 24h evidence", backupGap > 0 ? "warning" : "healthy"),
                new("STALE TARGETS", stale.ToString(), "Cached snapshot freshness", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/memory-health", StringComparison.Ordinal))
        {
            return
            [
                new("MEMORY PRESSURE", memoryPressure.ToString(), "Servers crossing collected memory-pressure rules", memoryPressure > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                new("BLOCKING", blockedRequests.ToString(), $"Blocked requests across {blockingServers} server(s)", blockedRequests > 0 ? "warning" : "healthy"),
                new("PERF PRESSURE", performancePressure.ToString(), "Servers with runnable queue or pending I/O pressure", performancePressure > 0 ? "warning" : "healthy"),
                new("STALE TARGETS", stale.ToString(), "Refresh stale evidence before a tuning decision", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/performance-health", StringComparison.Ordinal))
        {
            return
            [
                new("PRESSURED SERVERS", performancePressure.ToString(), "Runnable tasks > 4 or pending I/O > 0", performancePressure > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                new("RUNNABLE TASKS", runnableTasks.ToString(), "Aggregate cached scheduler queue evidence", runnableTasks > 0 ? "warning" : "healthy"),
                new("PENDING I/O", pendingIo.ToString(), "Aggregate current pending-I/O evidence", pendingIo > 0 ? "warning" : "healthy"),
                new("BLOCKED REQUESTS", blockedRequests.ToString(), "Bounded blocking evidence", blockedRequests > 0 ? "warning" : "healthy"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/backups", StringComparison.Ordinal))
        {
            return
            [
                new("BACKUP GAPS", backupGap.ToString(), "Missing full backup in current 24h evidence", backupGap > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                new("DATABASE RISK", databaseRisk.ToString(), "Database-state risk that can affect recovery posture", databaseRisk > 0 ? "critical" : "healthy"),
                new("UNAVAILABLE", unavailable.ToString(), "Registered targets with no usable cached snapshot", unavailable > 0 ? "warning" : "healthy"),
                new("OLDEST SNAPSHOT", age, "Evidence age, not backup age", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/jobs", StringComparison.Ordinal))
        {
            return
            [
                new("FAILED LAST RUN", jobsFailed.ToString(), "Aggregate SQL Agent failure evidence", jobsFailed > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                new("BLOCKING", blockedRequests.ToString(), "Current blocking can affect scheduled work", blockedRequests > 0 ? "warning" : "healthy"),
                new("PERF PRESSURE", performancePressure.ToString(), "Current workload-pressure servers", performancePressure > 0 ? "warning" : "healthy"),
                new("STALE TARGETS", stale.ToString(), "Refresh before interpreting current job posture", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/storage", StringComparison.Ordinal))
        {
            return
            [
                new("SQL ALLOCATED", FormatBytes(totalAllocated), "Database allocation only; not disk capacity", totalAllocated > 0 ? "healthy" : "unknown"),
                new("DATA ALLOCATED", FormatBytes(dataAllocated), "Allocated database data bytes", dataAllocated > 0 ? "healthy" : "unknown"),
                new("LOG ALLOCATED", FormatBytes(logAllocated), "Allocated transaction-log bytes", logAllocated > 0 ? "healthy" : "unknown"),
                new("PERF PRESSURE", performancePressure.ToString(), "Use I/O evidence with allocation; free disk is not collected", performancePressure > 0 ? "warning" : "healthy"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/blocking", StringComparison.Ordinal))
        {
            return
            [
                new("BLOCKED REQUESTS", blockedRequests.ToString(), $"Across {blockingServers} server(s)", blockedRequests > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                new("PERF PRESSURE", performancePressure.ToString(), "Scheduler or pending-I/O pressure servers", performancePressure > 0 ? "warning" : "healthy"),
                new("MEMORY PRESSURE", memoryPressure.ToString(), "Memory-pressure servers", memoryPressure > 0 ? "warning" : "healthy"),
                new("STALE TARGETS", stale.ToString(), "Blocking evidence should be current before escalation", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        return
        [
            new("ESTATE COVERAGE", registered == 0 ? "No targets" : $"{observed}/{registered}", "Registered active targets with cached evidence", evidenceState),
            new("DATABASE RISK", databaseRisk.ToString(), databaseTotal > 0 ? $"{databaseOnline}/{databaseTotal} online" : "Database evidence not collected", databaseRisk > 0 ? "critical" : databaseTotal > 0 ? "healthy" : "unknown"),
            new("BACKUP GAPS", backupGap.ToString(), "Missing full backup in current 24h evidence", backupGap > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
            new("MEMORY / PERF", $"{memoryPressure} / {performancePressure}", "Memory-pressure / workload-pressure server count", memoryPressure > 0 || performancePressure > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
            new("BLOCKING / JOBS", $"{blockedRequests} / {jobsFailed}", "Blocked requests / SQL Agent failed last run", blockedRequests > 0 || jobsFailed > 0 ? "warning" : observed > 0 ? "healthy" : "unknown")
        ];
    }

    private static string BuildNextAction(
        int registered,
        int unavailable,
        int stale,
        int databaseRisk,
        int backupGap,
        int jobsFailed,
        int memoryPressure,
        int blockedRequests,
        int performancePressure)
    {
        if (registered == 0) return "Register at least one real SQL target from Connections, then collect the first bounded snapshot.";
        if (databaseRisk > 0) return "Open Database Health first and review retained non-online/actionable database-state evidence.";
        if (unavailable > 0) return "Use Refresh All Connections, then review targets that remain unavailable in Connections.";
        if (backupGap > 0) return "Open Backup Health and review databases missing full-backup evidence in the current policy window.";
        if (memoryPressure > 0) return "Open Memory Health and validate OS headroom, grants and SQL memory evidence before any tuning decision.";
        if (performancePressure > 0) return "Open Performance Health and correlate runnable tasks, pending I/O and bounded waits.";
        if (blockedRequests > 0) return "Open Blocking and correlate current blocked-request evidence with workload pressure.";
        if (jobsFailed > 0) return "Open SQL Agent and review failed-last-run evidence before the next schedule window.";
        if (stale > 0) return "Use Refresh All Connections to replace stale snapshots before interpreting current health.";
        return "No current collected threshold is crossed. Continue trend, incident and backup-policy review.";
    }

    private static string Normalize(string? path) => (path ?? string.Empty).Trim().ToLowerInvariant();

    private static string FormatAge(int seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        if (seconds < 3600) return $"{seconds / 60}m";
        if (seconds < 86400) return $"{seconds / 3600}h";
        return $"{seconds / 86400}d";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "Not collected";
        const double gb = 1024d * 1024d * 1024d;
        return bytes >= gb ? $"{bytes / gb:0.0} GB" : $"{bytes / (1024d * 1024d):0.0} MB";
    }
}
