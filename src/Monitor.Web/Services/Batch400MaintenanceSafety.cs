using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public enum MaintenanceOperation { Unknown, IndexRebuild, StatisticsUpdate, Backup, Restore, Failover, Configuration, Patch }
public enum MaintenanceRisk { Low, Moderate, High, Critical }
public sealed record MaintenanceContext(MaintenanceOperation Operation, bool IsProduction, bool InApprovedWindow, bool HasApproval, bool HasRollbackPlan, int ActiveCriticalIncidents, bool ReplicaHealthy, bool RecentBackupAvailable);
public sealed record MaintenanceDecision(MaintenanceOperation Operation, MaintenanceRisk Risk, bool ApprovalRequired, bool RollbackRequired, bool WindowRequired, IReadOnlyList<string> Blockers, bool Allowed, double Score, string Reason, string Fingerprint);

public static class Batch400MaintenanceSafety
{
    public static MaintenanceOperation NormalizeOperation(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return normalized switch
        {
            "INDEXREBUILD" => MaintenanceOperation.IndexRebuild,
            "STATISTICSUPDATE" or "UPDATESTATISTICS" => MaintenanceOperation.StatisticsUpdate,
            "BACKUP" => MaintenanceOperation.Backup,
            "RESTORE" => MaintenanceOperation.Restore,
            "FAILOVER" => MaintenanceOperation.Failover,
            "CONFIGURATION" or "CONFIGCHANGE" => MaintenanceOperation.Configuration,
            "PATCH" or "PATCHING" => MaintenanceOperation.Patch,
            _ => MaintenanceOperation.Unknown
        };
    }

    public static MaintenanceRisk BaseRisk(MaintenanceOperation operation, bool isProduction)
    {
        var risk = operation switch
        {
            MaintenanceOperation.Backup or MaintenanceOperation.StatisticsUpdate => MaintenanceRisk.Low,
            MaintenanceOperation.IndexRebuild => MaintenanceRisk.Moderate,
            MaintenanceOperation.Configuration or MaintenanceOperation.Patch => MaintenanceRisk.High,
            MaintenanceOperation.Restore or MaintenanceOperation.Failover or MaintenanceOperation.Unknown => MaintenanceRisk.Critical,
            _ => MaintenanceRisk.Critical
        };
        if (!isProduction || risk == MaintenanceRisk.Critical) return risk;
        return (MaintenanceRisk)Math.Min((int)MaintenanceRisk.Critical, (int)risk + 1);
    }

    public static bool ApprovalRequired(MaintenanceContext context) => context.IsProduction || BaseRisk(context.Operation, context.IsProduction) is MaintenanceRisk.High or MaintenanceRisk.Critical;

    public static bool RollbackRequired(MaintenanceContext context) => context.Operation is MaintenanceOperation.Restore or MaintenanceOperation.Failover or MaintenanceOperation.Configuration or MaintenanceOperation.Patch || context.IsProduction;

    public static bool WindowRequired(MaintenanceContext context) => context.IsProduction && context.Operation is not MaintenanceOperation.Backup;

    public static IReadOnlyList<string> Blockers(MaintenanceContext context)
    {
        var blockers = new List<string>();
        if (context.ActiveCriticalIncidents > 0) blockers.Add("active-critical-incidents");
        if (ApprovalRequired(context) && !context.HasApproval) blockers.Add("approval-required");
        if (RollbackRequired(context) && !context.HasRollbackPlan) blockers.Add("rollback-plan-required");
        if (WindowRequired(context) && !context.InApprovedWindow) blockers.Add("approved-window-required");
        if (context.Operation is MaintenanceOperation.Failover && !context.ReplicaHealthy) blockers.Add("replica-not-ready");
        if (context.Operation is MaintenanceOperation.Restore or MaintenanceOperation.Patch && !context.RecentBackupAvailable) blockers.Add("recent-backup-required");
        if (context.Operation == MaintenanceOperation.Unknown) blockers.Add("unknown-operation");
        return blockers.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public static bool Allowed(MaintenanceContext context) => Blockers(context).Count == 0;

    public static double Score(MaintenanceContext context)
    {
        var baseScore = BaseRisk(context.Operation, context.IsProduction) switch { MaintenanceRisk.Low => 20, MaintenanceRisk.Moderate => 45, MaintenanceRisk.High => 70, _ => 90 };
        return Math.Round(Math.Clamp(baseScore + Blockers(context).Count * 5d, 0, 100), 2);
    }

    public static string SafeReason(MaintenanceContext context)
    {
        var blockers = Blockers(context);
        return blockers.Count == 0 ? "Maintenance preconditions are satisfied." : $"Maintenance is blocked by {string.Join(", ", blockers.Take(4))}.";
    }

    public static string Fingerprint(MaintenanceContext context)
    {
        var canonical = $"{context.Operation}|{context.IsProduction}|{context.InApprovedWindow}|{context.HasApproval}|{context.HasRollbackPlan}|{Math.Max(0, context.ActiveCriticalIncidents)}|{context.ReplicaHealthy}|{context.RecentBackupAvailable}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }

    public static MaintenanceDecision Decide(MaintenanceContext context) => new(context.Operation, BaseRisk(context.Operation, context.IsProduction), ApprovalRequired(context), RollbackRequired(context), WindowRequired(context), Blockers(context), Allowed(context), Score(context), SafeReason(context), Fingerprint(context));
}
