using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public sealed record B400ReleaseEvaluation(bool Ready, int CompletedTasks, double ReadinessPercent, IReadOnlyList<string> Failures);

public static class Batch400ReleaseGate
{
    public const string SchemaVersion = "monitor-intelligence-b400-v1";

    public static string TaskId(int number)
    {
        if (number is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(number));
        return $"B400-{number:000}";
    }

    public static bool TryParseTaskId(string? value, out int number)
    {
        number = 0;
        if (value is null || value.Length != 8 || !value.StartsWith("B400-", StringComparison.Ordinal)) return false;
        return int.TryParse(value.AsSpan(5), out number) && number is >= 1 and <= 100;
    }

    public static bool HasAllTasks(IEnumerable<string> taskIds)
    {
        var set = taskIds.Where(value => TryParseTaskId(value, out _)).ToHashSet(StringComparer.Ordinal);
        return Enumerable.Range(1, 100).All(number => set.Contains(TaskId(number)));
    }

    public static IReadOnlyList<string> FeatureGroups() =>
    [
        "wait-stat-intelligence",
        "query-regression",
        "tempdb-pressure",
        "transaction-log-health",
        "io-latency",
        "agent-reliability",
        "ha-readiness",
        "maintenance-safety",
        "fleet-correlation",
        "release-contract"
    ];

    public static IReadOnlyList<string> Guardrails() =>
    [
        "no-autonomous-remediation",
        "no-browser-to-sql",
        "no-ai-sql-execution",
        "no-plaintext-credentials",
        "no-raw-provider-errors",
        "read-models-are-side-effect-free"
    ];

    public static IReadOnlyDictionary<string, object> ContractManifest()
    {
        var tasks = Enumerable.Range(1, 100).Select(TaskId).ToArray();
        return new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = SchemaVersion,
            ["taskCount"] = tasks.Length,
            ["tasks"] = tasks,
            ["featureGroups"] = FeatureGroups(),
            ["guardrails"] = Guardrails()
        };
    }

    public static string ContractHash()
    {
        var canonical = string.Join('|', new[] { SchemaVersion }.Concat(FeatureGroups()).Concat(Guardrails()).Concat(Enumerable.Range(1, 100).Select(TaskId)));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    public static double ReadinessPercent(IEnumerable<string> taskIds)
    {
        var count = taskIds.Where(value => TryParseTaskId(value, out _)).Distinct(StringComparer.Ordinal).Count();
        return Math.Round(Math.Clamp(count * 100d / 100d, 0, 100), 2);
    }

    public static B400ReleaseEvaluation Evaluate(bool releaseBuildGreen, int passedTests, int failedTests, IEnumerable<string> taskIds, bool guardrailsIntact)
    {
        var materialized = taskIds.ToArray();
        var failures = new List<string>();
        if (!releaseBuildGreen) failures.Add("release-build-not-green");
        if (failedTests != 0) failures.Add("test-failures-present");
        if (passedTests <= 0) failures.Add("no-passing-tests");
        if (!HasAllTasks(materialized)) failures.Add("task-ledger-incomplete");
        if (!guardrailsIntact) failures.Add("guardrail-invariant-failed");
        var completed = materialized.Where(value => TryParseTaskId(value, out _)).Distinct(StringComparer.Ordinal).Count();
        return new(failures.Count == 0, completed, ReadinessPercent(materialized), failures);
    }
}
