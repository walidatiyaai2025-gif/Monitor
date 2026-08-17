namespace Monitor.Web.Services;

public enum MaintenanceDecisionSupportStatus
{
    NotEvaluated,
    Blocked,
    Ready
}

public sealed record MaintenanceDecisionEvidence(
    MaintenanceOperation Operation,
    bool IsProduction,
    bool ObservedMaintenanceWindowActive,
    bool? InApprovedWindow,
    bool? HasApproval,
    bool? HasRollbackPlan,
    int? ActiveCriticalIncidents,
    bool? ReplicaHealthy,
    bool? RecentBackupAvailable);

public sealed record MaintenanceDecisionSupportResult(
    MaintenanceDecisionSupportStatus Status,
    MaintenanceDecisionEvidence Evidence,
    MaintenanceDecision? Decision,
    IReadOnlyList<string> MissingInputs,
    string Message)
{
    public bool IsEvaluated => Decision is not null && MissingInputs.Count == 0;
}

public static class MaintenanceDecisionSupport
{
    public static MaintenanceOperation NormalizeOperation(string? input) =>
        Batch400MaintenanceSafety.NormalizeOperation(input);

    public static MaintenanceDecisionSupportResult Evaluate(MaintenanceDecisionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var missing = MissingInputs(evidence);
        if (missing.Count > 0)
        {
            return new(
                MaintenanceDecisionSupportStatus.NotEvaluated,
                evidence,
                null,
                missing,
                $"Maintenance readiness is not evaluated because required evidence is unavailable: {string.Join(", ", missing)}.");
        }

        var context = new MaintenanceContext(
            evidence.Operation,
            evidence.IsProduction,
            evidence.InApprovedWindow ?? false,
            evidence.HasApproval ?? false,
            evidence.HasRollbackPlan ?? false,
            Math.Max(0, evidence.ActiveCriticalIncidents ?? 0),
            evidence.ReplicaHealthy ?? false,
            evidence.RecentBackupAvailable ?? false);
        var decision = Batch400MaintenanceSafety.Decide(context);
        return new(
            decision.Allowed ? MaintenanceDecisionSupportStatus.Ready : MaintenanceDecisionSupportStatus.Blocked,
            evidence,
            decision,
            [],
            decision.Reason);
    }

    public static IReadOnlyList<string> MissingInputs(MaintenanceDecisionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var missing = new List<string>();
        if (evidence.Operation == MaintenanceOperation.Unknown)
        {
            missing.Add("operation");
            return missing;
        }

        if (!evidence.ActiveCriticalIncidents.HasValue) missing.Add("active-critical-incidents");

        var probe = new MaintenanceContext(
            evidence.Operation,
            evidence.IsProduction,
            false,
            false,
            false,
            0,
            false,
            false);

        if (Batch400MaintenanceSafety.ApprovalRequired(probe) && !evidence.HasApproval.HasValue)
            missing.Add("approval");
        if (Batch400MaintenanceSafety.RollbackRequired(probe) && !evidence.HasRollbackPlan.HasValue)
            missing.Add("rollback-plan");
        if (Batch400MaintenanceSafety.WindowRequired(probe) && !evidence.InApprovedWindow.HasValue)
            missing.Add("approved-window");
        if (evidence.Operation == MaintenanceOperation.Failover && !evidence.ReplicaHealthy.HasValue)
            missing.Add("replica-health");
        if (evidence.Operation is MaintenanceOperation.Restore or MaintenanceOperation.Patch && !evidence.RecentBackupAvailable.HasValue)
            missing.Add("recent-backup");

        return missing.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
