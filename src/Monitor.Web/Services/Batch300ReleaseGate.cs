namespace Monitor.Web.Services;

public enum Batch300GateStatus
{
    Blocked,
    Ready
}

public sealed record Batch300Invariant(string Name, bool Passed, string Detail);
public sealed record Batch300GateResult(Batch300GateStatus Status, int Passed, int Total, Batch300Invariant[] Invariants);

public static class Batch300ReleaseGate
{
    public const string BatchName = "BATCH-300";
    public const int TaskCount = 100;

    public static string TaskId(int number)
    {
        if (number is < 1 or > TaskCount) throw new ArgumentOutOfRangeException(nameof(number));
        return $"B300-{number:000}";
    }

    public static bool TryParseTaskId(string? value, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 8 || !value.StartsWith("B300-", StringComparison.Ordinal)) return false;
        return int.TryParse(value.AsSpan(5), out number) && number is >= 1 and <= TaskCount;
    }

    public static bool HasCompleteTaskSet(IEnumerable<string> taskIds)
    {
        ArgumentNullException.ThrowIfNull(taskIds);
        var numbers = taskIds.Select(value => TryParseTaskId(value, out var number) ? number : 0).Where(number => number > 0).ToHashSet();
        return numbers.Count == TaskCount && Enumerable.Range(1, TaskCount).All(numbers.Contains);
    }

    public static bool IsCompatibleWithBatch200(string? previousStatus) => string.Equals(previousStatus?.Trim(), "BATCH-200 COMPLETE", StringComparison.OrdinalIgnoreCase) || string.Equals(previousStatus?.Trim(), "BATCH-200 100/100 COMPLETE", StringComparison.OrdinalIgnoreCase);

    public static int ReadinessPercent(IEnumerable<Batch300Invariant> invariants)
    {
        ArgumentNullException.ThrowIfNull(invariants);
        var values = invariants.ToArray();
        if (values.Length == 0) return 0;
        return (int)Math.Round(values.Count(item => item.Passed) * 100d / values.Length, MidpointRounding.AwayFromZero);
    }

    public static Batch300Invariant GuardrailNoAutonomousRemediation(bool enabled) => new("NoAutonomousRemediation", !enabled, enabled ? "Autonomous remediation must remain disabled." : "Disabled.");

    public static Batch300Invariant GuardrailNoBrowserSql(bool browserSqlEnabled) => new("NoBrowserSql", !browserSqlEnabled, browserSqlEnabled ? "Browser-to-SQL is not allowed." : "Disabled.");

    public static Batch300Invariant GuardrailSecretsRedacted(bool secretCanaryFound) => new("SecretsRedacted", !secretCanaryFound, secretCanaryFound ? "Secret canary was detected." : "No secret canary detected.");

    public static Batch300GateResult Evaluate(IEnumerable<Batch300Invariant> invariants)
    {
        ArgumentNullException.ThrowIfNull(invariants);
        var values = invariants.ToArray();
        var passed = values.Count(item => item.Passed);
        return new(values.Length > 0 && passed == values.Length ? Batch300GateStatus.Ready : Batch300GateStatus.Blocked, passed, values.Length, values);
    }

    public static IReadOnlyDictionary<string, object> ContractManifest() => new Dictionary<string, object>(StringComparer.Ordinal)
    {
        ["batch"] = BatchName,
        ["tasks"] = TaskCount,
        ["exportSchema"] = Batch300ExportContracts.SchemaVersion,
        ["autonomousRemediation"] = false,
        ["browserSql"] = false,
        ["completionRequiresCi"] = true
    };
}
