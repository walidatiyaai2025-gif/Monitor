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

        var databaseEvidenceTargets = servers.Count(server => server.DatabaseTotal > 0 || server.Databases is not null);
        var backupEvidenceTargets = servers.Count(server => server.Backups is not null);
        var jobEvidenceTargets = servers.Count(server => server.Jobs is not null);
        var memoryEvidenceTargets = servers.Count(server => server.Memory is not null);
        var blockingEvidenceTargets = servers.Count(server => server.Blocking is not null);
        var performanceEvidenceTargets = servers.Count(server => server.Performance is not null);
        var storageEvidenceTargets = servers.Count(server => server.Storage is not null);

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

        var backupGap = servers
            .Where(server => server.Backups is not null)
            .Sum(server => Math.Max(0, server.Backups!.MissingFullBackupLast24Hours));
        var jobsFailed = servers
            .Where(server => server.Jobs is not null)
            .Sum(server => Math.Max(0, server.Jobs!.FailedLastRun));
        var memoryPressure = servers.Count(server =>
            server.Memory is not null && MemoryIntelligenceProjection.Build(server.Memory).NeedsAttention);
        var blockingServers = servers.Count(server => server.Blocking?.BlockedRequests is > 0);
        var blockedRequests = servers
            .Where(server => server.Blocking is not null)
            .Sum(server => Math.Max(0, server.Blocking!.BlockedRequests));
        var performancePressure = servers.Count(server =>
            server.Performance is not null
            && (server.Performance.RunnableTasks > 4 || server.Performance.PendingIoRequests > 0));
        var runnableTasks = servers
            .Where(server => server.Performance is not null)
            .Sum(server => Math.Max(0, server.Performance!.RunnableTasks));
        var pendingIo = servers
            .Where(server => server.Performance is not null)
            .Sum(server => Math.Max(0, server.Performance!.PendingIoRequests));
        var totalAllocated = servers
            .Where(server => server.Storage is not null)
            .Sum(server => Math.Max(0L, server.Storage!.TotalAllocatedBytes));
        var dataAllocated = servers
            .Where(server => server.Storage is not null)
            .Sum(server => Math.Max(0L, server.Storage!.DataAllocatedBytes));
        var logAllocated = servers
            .Where(server => server.Storage is not null)
            .Sum(server => Math.Max(0L, server.Storage!.LogAllocatedBytes));
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
                        : FocusedEvidenceComplete(
                            path,
                            observed,
                            databaseEvidenceTargets,
                            backupEvidenceTargets,
                            jobEvidenceTargets,
                            memoryEvidenceTargets,
                            blockingEvidenceTargets,
                            performanceEvidenceTargets,
                            storageEvidenceTargets)
                            ? "healthy"
                            : "unknown";

        var headline = severity switch
        {
            "critical" => "Critical database-state evidence requires DBA review",
            "warning" => "Estate evidence contains conditions that need attention",
            "healthy" => "Collected estate evidence is within current intelligence thresholds",
            _ when registeredTargets == 0 => "No active SQL targets are registered",
            _ when observed == 0 => "Registered targets do not yet have usable cached evidence",
            _ => "Focused intelligence is incomplete for one or more observed targets"
        };

        var nextAction = BuildNextAction(
            path,
            registeredTargets,
            observed,
            unavailable,
            stale,
            databaseRisk,
            backupGap,
            jobsFailed,
            memoryPressure,
            blockedRequests,
            performancePressure,
            databaseEvidenceTargets,
            backupEvidenceTargets,
            jobEvidenceTargets,
            memoryEvidenceTargets,
            blockingEvidenceTargets,
            performanceEvidenceTargets,
            storageEvidenceTargets);

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
            databaseEvidenceTargets,
            backupGap,
            backupEvidenceTargets,
            jobsFailed,
            jobEvidenceTargets,
            memoryPressure,
            memoryEvidenceTargets,
            blockingServers,
            blockedRequests,
            blockingEvidenceTargets,
            performancePressure,
            runnableTasks,
            pendingIo,
            performanceEvidenceTargets,
            totalAllocated,
            dataAllocated,
            logAllocated,
            storageEvidenceTargets,
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
        if (normalized.StartsWith("/observability", StringComparison.Ordinal)
            || normalized.StartsWith("/audit", StringComparison.Ordinal)
            || normalized.StartsWith("/settings", StringComparison.Ordinal))
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
        int databaseEvidenceTargets,
        int backupGap,
        int backupEvidenceTargets,
        int jobsFailed,
        int jobEvidenceTargets,
        int memoryPressure,
        int memoryEvidenceTargets,
        int blockingServers,
        int blockedRequests,
        int blockingEvidenceTargets,
        int performancePressure,
        int runnableTasks,
        int pendingIo,
        int performanceEvidenceTargets,
        long totalAllocated,
        long dataAllocated,
        long logAllocated,
        int storageEvidenceTargets,
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
                new("DATABASES ONLINE", FormatRatio(databaseOnline, databaseTotal, databaseEvidenceTargets, observed), WithCoverage("Latest cached aggregate database state", databaseEvidenceTargets, observed), MetricState(databaseEvidenceTargets, observed, databaseRisk > 0)),
                new("DATABASE RISK", FormatCount(databaseRisk, databaseEvidenceTargets, observed), WithCoverage("Offline/non-online or retained actionable state evidence", databaseEvidenceTargets, observed), MetricState(databaseEvidenceTargets, observed, databaseRisk > 0, "critical")),
                new("BACKUP GAPS", FormatCount(backupGap, backupEvidenceTargets, observed), WithCoverage("Missing full backup in current 24h evidence", backupEvidenceTargets, observed), MetricState(backupEvidenceTargets, observed, backupGap > 0)),
                new("STALE TARGETS", stale.ToString(), "Cached snapshot freshness", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/memory-health", StringComparison.Ordinal))
        {
            return
            [
                new("MEMORY PRESSURE", FormatCount(memoryPressure, memoryEvidenceTargets, observed), WithCoverage("Servers crossing collected memory-pressure rules", memoryEvidenceTargets, observed), MetricState(memoryEvidenceTargets, observed, memoryPressure > 0)),
                new("BLOCKING", FormatCount(blockedRequests, blockingEvidenceTargets, observed), WithCoverage($"Blocked requests across {blockingServers} server(s)", blockingEvidenceTargets, observed), MetricState(blockingEvidenceTargets, observed, blockedRequests > 0)),
                new("PERF PRESSURE", FormatCount(performancePressure, performanceEvidenceTargets, observed), WithCoverage("Servers with runnable queue or pending I/O pressure", performanceEvidenceTargets, observed), MetricState(performanceEvidenceTargets, observed, performancePressure > 0)),
                new("STALE TARGETS", stale.ToString(), "Refresh stale evidence before a tuning decision", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/performance-health", StringComparison.Ordinal))
        {
            return
            [
                new("PRESSURED SERVERS", FormatCount(performancePressure, performanceEvidenceTargets, observed), WithCoverage("Runnable tasks > 4 or pending I/O > 0", performanceEvidenceTargets, observed), MetricState(performanceEvidenceTargets, observed, performancePressure > 0)),
                new("RUNNABLE TASKS", FormatCount(runnableTasks, performanceEvidenceTargets, observed), WithCoverage("Aggregate cached scheduler queue evidence", performanceEvidenceTargets, observed), MetricState(performanceEvidenceTargets, observed, runnableTasks > 0)),
                new("PENDING I/O", FormatCount(pendingIo, performanceEvidenceTargets, observed), WithCoverage("Aggregate current pending-I/O evidence", performanceEvidenceTargets, observed), MetricState(performanceEvidenceTargets, observed, pendingIo > 0)),
                new("BLOCKED REQUESTS", FormatCount(blockedRequests, blockingEvidenceTargets, observed), WithCoverage("Bounded blocking evidence", blockingEvidenceTargets, observed), MetricState(blockingEvidenceTargets, observed, blockedRequests > 0)),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/backups", StringComparison.Ordinal))
        {
            return
            [
                new("BACKUP GAPS", FormatCount(backupGap, backupEvidenceTargets, observed), WithCoverage("Missing full backup in current 24h evidence", backupEvidenceTargets, observed), MetricState(backupEvidenceTargets, observed, backupGap > 0)),
                new("DATABASE RISK", FormatCount(databaseRisk, databaseEvidenceTargets, observed), WithCoverage("Database-state risk that can affect recovery posture", databaseEvidenceTargets, observed), MetricState(databaseEvidenceTargets, observed, databaseRisk > 0, "critical")),
                new("UNAVAILABLE", unavailable.ToString(), "Registered targets with no usable cached snapshot", unavailable > 0 ? "warning" : registered > 0 ? "healthy" : "unknown"),
                new("OLDEST SNAPSHOT", age, "Evidence age, not backup age", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/jobs", StringComparison.Ordinal))
        {
            return
            [
                new("FAILED LAST RUN", FormatCount(jobsFailed, jobEvidenceTargets, observed), WithCoverage("Aggregate SQL Agent failure evidence", jobEvidenceTargets, observed), MetricState(jobEvidenceTargets, observed, jobsFailed > 0)),
                new("BLOCKING", FormatCount(blockedRequests, blockingEvidenceTargets, observed), WithCoverage("Current blocking can affect scheduled work", blockingEvidenceTargets, observed), MetricState(blockingEvidenceTargets, observed, blockedRequests > 0)),
                new("PERF PRESSURE", FormatCount(performancePressure, performanceEvidenceTargets, observed), WithCoverage("Current workload-pressure servers", performanceEvidenceTargets, observed), MetricState(performanceEvidenceTargets, observed, performancePressure > 0)),
                new("STALE TARGETS", stale.ToString(), "Refresh before interpreting current job posture", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/storage", StringComparison.Ordinal))
        {
            return
            [
                new("SQL ALLOCATED", FormatBytes(totalAllocated, storageEvidenceTargets, observed), WithCoverage("Database allocation only; not disk capacity", storageEvidenceTargets, observed), MetricState(storageEvidenceTargets, observed, false)),
                new("DATA ALLOCATED", FormatBytes(dataAllocated, storageEvidenceTargets, observed), WithCoverage("Allocated database data bytes", storageEvidenceTargets, observed), MetricState(storageEvidenceTargets, observed, false)),
                new("LOG ALLOCATED", FormatBytes(logAllocated, storageEvidenceTargets, observed), WithCoverage("Allocated transaction-log bytes", storageEvidenceTargets, observed), MetricState(storageEvidenceTargets, observed, false)),
                new("PERF PRESSURE", FormatCount(performancePressure, performanceEvidenceTargets, observed), WithCoverage("Use I/O evidence with allocation; free disk is not collected", performanceEvidenceTargets, observed), MetricState(performanceEvidenceTargets, observed, performancePressure > 0)),
                evidenceSignal
            ];
        }

        if (normalized.StartsWith("/blocking", StringComparison.Ordinal))
        {
            return
            [
                new("BLOCKED REQUESTS", FormatCount(blockedRequests, blockingEvidenceTargets, observed), WithCoverage($"Across {blockingServers} server(s)", blockingEvidenceTargets, observed), MetricState(blockingEvidenceTargets, observed, blockedRequests > 0)),
                new("PERF PRESSURE", FormatCount(performancePressure, performanceEvidenceTargets, observed), WithCoverage("Scheduler or pending-I/O pressure servers", performanceEvidenceTargets, observed), MetricState(performanceEvidenceTargets, observed, performancePressure > 0)),
                new("MEMORY PRESSURE", FormatCount(memoryPressure, memoryEvidenceTargets, observed), WithCoverage("Memory-pressure servers", memoryEvidenceTargets, observed), MetricState(memoryEvidenceTargets, observed, memoryPressure > 0)),
                new("STALE TARGETS", stale.ToString(), "Blocking evidence should be current before escalation", stale > 0 ? "warning" : observed > 0 ? "healthy" : "unknown"),
                evidenceSignal
            ];
        }

        return
        [
            new("ESTATE COVERAGE", registered == 0 ? "No targets" : $"{observed}/{registered}", "Registered active targets with cached evidence", evidenceState),
            new("DATABASE RISK", FormatCount(databaseRisk, databaseEvidenceTargets, observed), WithCoverage(databaseTotal > 0 ? $"{databaseOnline}/{databaseTotal} online" : "Database evidence not collected", databaseEvidenceTargets, observed), MetricState(databaseEvidenceTargets, observed, databaseRisk > 0, "critical")),
            new("BACKUP GAPS", FormatCount(backupGap, backupEvidenceTargets, observed), WithCoverage("Missing full backup in current 24h evidence", backupEvidenceTargets, observed), MetricState(backupEvidenceTargets, observed, backupGap > 0)),
            new("MEMORY / PERF", $"{FormatCount(memoryPressure, memoryEvidenceTargets, observed)} / {FormatCount(performancePressure, performanceEvidenceTargets, observed)}", $"Memory-pressure / workload-pressure server count · memory evidence {memoryEvidenceTargets}/{observed} · performance evidence {performanceEvidenceTargets}/{observed}", CombinedMetricState(memoryEvidenceTargets, performanceEvidenceTargets, observed, memoryPressure > 0 || performancePressure > 0)),
            new("BLOCKING / JOBS", $"{FormatCount(blockedRequests, blockingEvidenceTargets, observed)} / {FormatCount(jobsFailed, jobEvidenceTargets, observed)}", $"Blocked requests / SQL Agent failed last run · blocking evidence {blockingEvidenceTargets}/{observed} · job evidence {jobEvidenceTargets}/{observed}", CombinedMetricState(blockingEvidenceTargets, jobEvidenceTargets, observed, blockedRequests > 0 || jobsFailed > 0))
        ];
    }

    private static string BuildNextAction(
        string? path,
        int registered,
        int observed,
        int unavailable,
        int stale,
        int databaseRisk,
        int backupGap,
        int jobsFailed,
        int memoryPressure,
        int blockedRequests,
        int performancePressure,
        int databaseEvidenceTargets,
        int backupEvidenceTargets,
        int jobEvidenceTargets,
        int memoryEvidenceTargets,
        int blockingEvidenceTargets,
        int performanceEvidenceTargets,
        int storageEvidenceTargets)
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

        var missingFocused = MissingFocusedEvidenceAction(
            path,
            observed,
            databaseEvidenceTargets,
            backupEvidenceTargets,
            jobEvidenceTargets,
            memoryEvidenceTargets,
            blockingEvidenceTargets,
            performanceEvidenceTargets,
            storageEvidenceTargets);
        if (missingFocused is not null) return missingFocused;

        return "No current collected threshold is crossed. Continue trend, incident and backup-policy review.";
    }

    private static bool FocusedEvidenceComplete(
        string? path,
        int observed,
        int databaseEvidenceTargets,
        int backupEvidenceTargets,
        int jobEvidenceTargets,
        int memoryEvidenceTargets,
        int blockingEvidenceTargets,
        int performanceEvidenceTargets,
        int storageEvidenceTargets)
    {
        if (observed <= 0) return false;
        var normalized = Normalize(path);
        if (normalized.StartsWith("/database-health", StringComparison.Ordinal)) return databaseEvidenceTargets == observed;
        if (normalized.StartsWith("/memory-health", StringComparison.Ordinal)) return memoryEvidenceTargets == observed;
        if (normalized.StartsWith("/performance-health", StringComparison.Ordinal)) return performanceEvidenceTargets == observed;
        if (normalized.StartsWith("/backups", StringComparison.Ordinal)) return backupEvidenceTargets == observed;
        if (normalized.StartsWith("/jobs", StringComparison.Ordinal)) return jobEvidenceTargets == observed;
        if (normalized.StartsWith("/storage", StringComparison.Ordinal)) return storageEvidenceTargets == observed;
        if (normalized.StartsWith("/blocking", StringComparison.Ordinal)) return blockingEvidenceTargets == observed;
        return true;
    }

    private static string? MissingFocusedEvidenceAction(
        string? path,
        int observed,
        int databaseEvidenceTargets,
        int backupEvidenceTargets,
        int jobEvidenceTargets,
        int memoryEvidenceTargets,
        int blockingEvidenceTargets,
        int performanceEvidenceTargets,
        int storageEvidenceTargets)
    {
        if (observed <= 0) return null;
        var normalized = Normalize(path);
        if (normalized.StartsWith("/database-health", StringComparison.Ordinal) && databaseEvidenceTargets < observed)
            return MissingEvidenceAction("Database Health", databaseEvidenceTargets, observed);
        if (normalized.StartsWith("/memory-health", StringComparison.Ordinal) && memoryEvidenceTargets < observed)
            return MissingEvidenceAction("Memory Health", memoryEvidenceTargets, observed);
        if (normalized.StartsWith("/performance-health", StringComparison.Ordinal) && performanceEvidenceTargets < observed)
            return MissingEvidenceAction("Performance Health", performanceEvidenceTargets, observed);
        if (normalized.StartsWith("/backups", StringComparison.Ordinal) && backupEvidenceTargets < observed)
            return MissingEvidenceAction("Backup Health", backupEvidenceTargets, observed);
        if (normalized.StartsWith("/jobs", StringComparison.Ordinal) && jobEvidenceTargets < observed)
            return MissingEvidenceAction("SQL Agent", jobEvidenceTargets, observed);
        if (normalized.StartsWith("/storage", StringComparison.Ordinal) && storageEvidenceTargets < observed)
            return MissingEvidenceAction("Storage", storageEvidenceTargets, observed);
        if (normalized.StartsWith("/blocking", StringComparison.Ordinal) && blockingEvidenceTargets < observed)
            return MissingEvidenceAction("Blocking", blockingEvidenceTargets, observed);
        return null;
    }

    private static string MissingEvidenceAction(string domain, int evidenceTargets, int observed) =>
        $"{domain} evidence is available for {evidenceTargets}/{observed} observed target(s). Use Refresh All Connections, then review collection permissions/diagnostics for targets that remain uncollected.";

    private static string FormatCount(int value, int evidenceTargets, int observed)
    {
        if (evidenceTargets <= 0) return "Not collected";
        return evidenceTargets < observed ? $"{value} · {evidenceTargets}/{observed}" : value.ToString();
    }

    private static string FormatRatio(int numerator, int denominator, int evidenceTargets, int observed)
    {
        if (evidenceTargets <= 0) return "Not collected";
        var value = $"{numerator}/{denominator}";
        return evidenceTargets < observed ? $"{value} · {evidenceTargets}/{observed}" : value;
    }

    private static string MetricState(int evidenceTargets, int observed, bool attention, string attentionState = "warning")
    {
        if (attention) return attentionState;
        if (evidenceTargets <= 0 || observed <= 0 || evidenceTargets < observed) return "unknown";
        return "healthy";
    }

    private static string CombinedMetricState(int firstEvidenceTargets, int secondEvidenceTargets, int observed, bool attention)
    {
        if (attention) return "warning";
        if (observed <= 0 || firstEvidenceTargets < observed || secondEvidenceTargets < observed) return "unknown";
        return "healthy";
    }

    private static string WithCoverage(string detail, int evidenceTargets, int observed)
    {
        if (observed <= 0) return detail;
        return $"{detail} · evidence {evidenceTargets}/{observed} targets";
    }

    private static string Normalize(string? path) => (path ?? string.Empty).Trim().ToLowerInvariant();

    private static string FormatAge(int seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        if (seconds < 3600) return $"{seconds / 60}m";
        if (seconds < 86400) return $"{seconds / 3600}h";
        return $"{seconds / 86400}d";
    }

    private static string FormatBytes(long bytes, int evidenceTargets, int observed)
    {
        if (evidenceTargets <= 0) return "Not collected";

        string value;
        if (bytes <= 0)
        {
            value = "0 B";
        }
        else
        {
            const double gb = 1024d * 1024d * 1024d;
            value = bytes >= gb
                ? $"{bytes / gb:0.0} GB"
                : $"{bytes / (1024d * 1024d):0.0} MB";
        }

        return evidenceTargets < observed ? $"{value} · {evidenceTargets}/{observed}" : value;
    }
}
