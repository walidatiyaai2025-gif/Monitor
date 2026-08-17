using System.Globalization;

namespace Monitor.Web.Services;

public static class MaintenanceDecisionSupportExport
{
    private static readonly IReadOnlyList<string> Headers = ["Section", "Metric", "Value"];

    public static byte[] Build(
        ServerOperatorPolicyState policy,
        BoundedIncidentReadResult incidentRead,
        MaintenanceDecisionSupportResult result)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(incidentRead);
        ArgumentNullException.ThrowIfNull(result);

        var evidence = result.Evidence;
        var rows = new List<IReadOnlyList<string?>>();

        Add(rows, "Evidence", "PolicyReadable", Boolean(policy.PolicyReadable));
        Add(rows, "Evidence", "IncidentEvidenceComplete", Boolean(incidentRead.IsComplete));
        Add(rows, "Evidence", "IncidentEvidenceLimit", Number(incidentRead.Limit));
        Add(rows, "Evidence", "IsProduction", NullableBoolean(evidence.IsProduction));
        Add(rows, "Evidence", "ObservedMaintenanceWindowActive", NullableBoolean(evidence.ObservedMaintenanceWindowActive));
        Add(rows, "Evidence", "ActiveCriticalIncidents", NullableNumber(evidence.ActiveCriticalIncidents));
        Add(rows, "Evidence", "InApprovedWindow", NullableBoolean(evidence.InApprovedWindow));
        Add(rows, "Evidence", "HasApproval", NullableBoolean(evidence.HasApproval));
        Add(rows, "Evidence", "HasRollbackPlan", NullableBoolean(evidence.HasRollbackPlan));
        Add(rows, "Evidence", "ReplicaHealthy", NullableBoolean(evidence.ReplicaHealthy));
        Add(rows, "Evidence", "RecentBackupAvailable", NullableBoolean(evidence.RecentBackupAvailable));

        Add(rows, "Decision", "Operation", evidence.Operation.ToString());
        Add(rows, "Decision", "Status", result.Status.ToString());
        Add(rows, "Decision", "IsEvaluated", Boolean(result.IsEvaluated));
        Add(rows, "Decision", "MissingInputs", result.MissingInputs.Count == 0 ? string.Empty : string.Join('|', result.MissingInputs));

        if (result.Decision is null)
        {
            Add(rows, "Decision", "State", "Unavailable");
        }
        else
        {
            var decision = result.Decision;
            Add(rows, "Decision", "Risk", decision.Risk.ToString());
            Add(rows, "Decision", "Allowed", Boolean(decision.Allowed));
            Add(rows, "Decision", "ApprovalRequired", Boolean(decision.ApprovalRequired));
            Add(rows, "Decision", "RollbackRequired", Boolean(decision.RollbackRequired));
            Add(rows, "Decision", "WindowRequired", Boolean(decision.WindowRequired));
            Add(rows, "Decision", "Score", decision.Score.ToString("0.##", CultureInfo.InvariantCulture));
            Add(rows, "Decision", "Blockers", decision.Blockers.Count == 0 ? string.Empty : string.Join('|', decision.Blockers));
            Add(rows, "Decision", "Reason", decision.Reason);
        }

        return EnterpriseReportContract.Csv(Headers, rows);
    }

    private static void Add(List<IReadOnlyList<string?>> rows, string section, string metric, string? value) =>
        rows.Add([section, metric, value]);

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string NullableBoolean(bool? value) => value.HasValue ? Boolean(value.Value) : "Unavailable";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string NullableNumber(int? value) => value.HasValue ? Number(value.Value) : "Unavailable";
}
