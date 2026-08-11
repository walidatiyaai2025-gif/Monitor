using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public sealed record B600ReadinessSummary(bool Ready, int Score, IReadOnlyList<string> Blockers);

public static class Batch600EvidenceFreshness
{
    public static string NormalizeKind(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "health" or "readiness" => "health",
        "auth" or "authentication" => "auth",
        "iis" or "hosting" => "iis",
        "sql" or "database" => "sql",
        "backup" => "backup",
        "rollback" or "recovery" => "rollback",
        _ => "unknown"
    };

    public static DateTimeOffset ClampFuture(DateTimeOffset now, DateTimeOffset capturedAt) => capturedAt > now ? now : capturedAt;
    public static double AgeMinutes(DateTimeOffset now, DateTimeOffset capturedAt) => Math.Round(Math.Max(0, (now - ClampFuture(now, capturedAt)).TotalMinutes), 2);
    public static bool IsFresh(DateTimeOffset now, DateTimeOffset capturedAt, double maxAgeMinutes) => maxAgeMinutes >= 0 && AgeMinutes(now, capturedAt) <= maxAgeMinutes;
    public static int FreshnessScore(DateTimeOffset now, DateTimeOffset capturedAt, double maxAgeMinutes)
    {
        if (maxAgeMinutes <= 0) return IsFresh(now, capturedAt, 0) ? 100 : 0;
        var ratio = Math.Clamp(AgeMinutes(now, capturedAt) / maxAgeMinutes, 0, 1);
        return (int)Math.Round((1 - ratio) * 100, MidpointRounding.AwayFromZero);
    }
    public static string NormalizeSource(string? value) => new string((value ?? string.Empty).Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.').Take(96).ToArray());
    public static bool IsSameEnvironment(string? left, string? right) => string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
    public static IReadOnlyList<string> MissingKinds(IEnumerable<string?> kinds, IEnumerable<string> requiredKinds)
    {
        var present = kinds.Select(NormalizeKind).ToHashSet(StringComparer.Ordinal);
        return requiredKinds.Select(NormalizeKind).Where(x => x != "unknown" && !present.Contains(x)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
    public static string Fingerprint(string? kind, string? source, DateTimeOffset capturedAt)
    {
        var canonical = $"{NormalizeKind(kind)}|{NormalizeSource(source)}|{capturedAt.ToUniversalTime():O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    public static bool IsUsable(string? kind, string? source, DateTimeOffset now, DateTimeOffset capturedAt, double maxAgeMinutes) => NormalizeKind(kind) != "unknown" && !string.IsNullOrWhiteSpace(NormalizeSource(source)) && IsFresh(now, capturedAt, maxAgeMinutes);
}

public static class Batch600DependencyGraph
{
    public static string NormalizeGate(string? value) => new string((value ?? string.Empty).Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').Take(64).ToArray());
    public static bool HasSelfDependency(string? gate, IEnumerable<string?> dependencies)
    {
        var normalized = NormalizeGate(gate);
        return normalized.Length > 0 && dependencies.Any(x => NormalizeGate(x) == normalized);
    }
    public static IReadOnlyList<string> NormalizeDependencies(IEnumerable<string?> dependencies) => dependencies.Select(NormalizeGate).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    public static bool DependenciesSatisfied(IEnumerable<string?> dependencies, IEnumerable<string?> completed)
    {
        var done = completed.Select(NormalizeGate).ToHashSet(StringComparer.Ordinal);
        return NormalizeDependencies(dependencies).All(done.Contains);
    }
    public static IReadOnlyList<string> MissingDependencies(IEnumerable<string?> dependencies, IEnumerable<string?> completed)
    {
        var done = completed.Select(NormalizeGate).ToHashSet(StringComparer.Ordinal);
        return NormalizeDependencies(dependencies).Where(x => !done.Contains(x)).ToArray();
    }
    public static bool HasDuplicateEdges(IEnumerable<string?> dependencies)
    {
        var normalized = dependencies.Select(NormalizeGate).Where(x => x.Length > 0).ToArray();
        return normalized.Length != normalized.Distinct(StringComparer.Ordinal).Count();
    }
    public static int DependencyDepth(int parentDepth) => Math.Clamp(parentDepth + 1, 0, 32);
    public static bool IsDepthSafe(int depth) => depth is >= 0 and <= 16;
    public static int CompletionPercent(IEnumerable<string?> dependencies, IEnumerable<string?> completed)
    {
        var deps = NormalizeDependencies(dependencies);
        if (deps.Count == 0) return 100;
        var missing = MissingDependencies(deps, completed).Count;
        return (int)Math.Round(((deps.Count - missing) * 100.0) / deps.Count, MidpointRounding.AwayFromZero);
    }
    public static bool GateReady(string? gate, IEnumerable<string?> dependencies, IEnumerable<string?> completed, int depth) => NormalizeGate(gate).Length > 0 && !HasSelfDependency(gate, dependencies) && IsDepthSafe(depth) && DependenciesSatisfied(dependencies, completed);
}

public static class Batch600OperatorQueue
{
    public static string NormalizeAction(string? value) => new string((value ?? string.Empty).Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').Take(80).ToArray());
    public static string NormalizeOwner(string? value) => new string((value ?? string.Empty).Trim().Where(ch => !char.IsControl(ch)).Take(96).ToArray());
    public static int PriorityScore(string? severity, bool blocksRelease, bool overdue)
    {
        var score = (severity ?? string.Empty).Trim().ToLowerInvariant() switch { "critical" => 60, "warning" => 35, "info" => 10, _ => 0 };
        if (blocksRelease) score += 25;
        if (overdue) score += 15;
        return Math.Clamp(score, 0, 100);
    }
    public static bool IsOverdue(DateTimeOffset now, DateTimeOffset dueAt) => dueAt < now;
    public static bool HasOwner(string? owner) => NormalizeOwner(owner).Length > 0;
    public static bool IsActionable(string? action, string? owner, bool dependencyReady) => NormalizeAction(action).Length > 0 && HasOwner(owner) && dependencyReady;
    public static IReadOnlyList<string> Blockers(string? action, string? owner, bool dependencyReady)
    {
        var blockers = new List<string>();
        if (NormalizeAction(action).Length == 0) blockers.Add("action-missing");
        if (!HasOwner(owner)) blockers.Add("owner-missing");
        if (!dependencyReady) blockers.Add("dependency-not-ready");
        return blockers;
    }
    public static string StableKey(string? action, string? owner)
    {
        var canonical = $"{NormalizeAction(action)}|{NormalizeOwner(owner).ToLowerInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    public static int ComparePriority(int left, int right) => right.CompareTo(left);
    public static bool CanAcknowledge(bool actionable, bool alreadyCompleted) => actionable && !alreadyCompleted;
    public static bool CanComplete(bool actionable, bool evidenceAttached) => actionable && evidenceAttached;
}

public static class Batch600ChangeWindow
{
    public static bool IsValidWindow(DateTimeOffset start, DateTimeOffset end) => end > start && (end - start) <= TimeSpan.FromHours(12);
    public static double DurationMinutes(DateTimeOffset start, DateTimeOffset end) => Math.Round(Math.Max(0, (end - start).TotalMinutes), 2);
    public static bool Contains(DateTimeOffset start, DateTimeOffset end, DateTimeOffset instant) => IsValidWindow(start, end) && instant >= start && instant <= end;
    public static bool HasFreezeConflict(DateTimeOffset start, DateTimeOffset end, DateTimeOffset freezeStart, DateTimeOffset freezeEnd) => IsValidWindow(start, end) && freezeEnd > freezeStart && start < freezeEnd && end > freezeStart;
    public static int RemainingMinutes(DateTimeOffset now, DateTimeOffset end) => Math.Max(0, (int)Math.Floor((end - now).TotalMinutes));
    public static bool HasApprovalQuorum(int approvals, int required) => required > 0 && approvals >= required;
    public static bool BackupReady(bool validated, DateTimeOffset now, DateTimeOffset createdAt, double maxAgeHours) => validated && createdAt <= now && maxAgeHours >= 0 && (now - createdAt).TotalHours <= maxAgeHours;
    public static bool RollbackOwnerReady(string? owner) => !string.IsNullOrWhiteSpace(owner);
    public static IReadOnlyList<string> Blockers(bool windowValid, bool freezeConflict, bool approvalsReady, bool backupReady, bool rollbackOwnerReady)
    {
        var blockers = new List<string>();
        if (!windowValid) blockers.Add("window-invalid");
        if (freezeConflict) blockers.Add("freeze-conflict");
        if (!approvalsReady) blockers.Add("approval-quorum-missing");
        if (!backupReady) blockers.Add("backup-not-ready");
        if (!rollbackOwnerReady) blockers.Add("rollback-owner-missing");
        return blockers;
    }
    public static bool Go(bool windowValid, bool freezeConflict, bool approvalsReady, bool backupReady, bool rollbackOwnerReady) => Blockers(windowValid, freezeConflict, approvalsReady, backupReady, rollbackOwnerReady).Count == 0;
}

public static class Batch600CandidatePromotion
{
    public static string NormalizeVersion(string? value) => new string((value ?? string.Empty).Trim().Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_').Take(64).ToArray());
    public static bool IsSha256(string? value) => value is not null && value.Length == 64 && value.All(Uri.IsHexDigit);
    public static bool IsCommitSha(string? value) => value is not null && value.Length is >= 7 and <= 40 && value.All(Uri.IsHexDigit);
    public static bool ArtifactMatches(string? expected, string? actual) => string.Equals((expected ?? string.Empty).Trim(), (actual ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
    public static bool IsNewerBuild(int candidateBuild, int currentBuild) => candidateBuild > currentBuild;
    public static bool SameTopology(string? candidate, string? expected) => string.Equals((candidate ?? string.Empty).Trim(), (expected ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
    public static bool ExternalAcceptanceClaimAllowed(bool externalEvidencePresent) => externalEvidencePresent;
    public static IReadOnlyList<string> Blockers(string? version, string? sha256, string? commitSha, bool artifactMatch, bool newerBuild, bool sameTopology, bool externalClaimRequested, bool externalEvidencePresent)
    {
        var blockers = new List<string>();
        if (NormalizeVersion(version).Length == 0) blockers.Add("version-missing");
        if (!IsSha256(sha256)) blockers.Add("sha256-invalid");
        if (!IsCommitSha(commitSha)) blockers.Add("commit-sha-invalid");
        if (!artifactMatch) blockers.Add("artifact-mismatch");
        if (!newerBuild) blockers.Add("candidate-not-newer");
        if (!sameTopology) blockers.Add("topology-mismatch");
        if (externalClaimRequested && !externalEvidencePresent) blockers.Add("external-acceptance-unproven");
        return blockers;
    }
    public static int PromotionScore(bool artifactMatch, bool newerBuild, bool sameTopology, bool externalClaimSafe)
    {
        var score = 0;
        if (artifactMatch) score += 30;
        if (newerBuild) score += 25;
        if (sameTopology) score += 25;
        if (externalClaimSafe) score += 20;
        return score;
    }
    public static string CandidateId(string? version, string? sha256)
    {
        var canonical = $"{NormalizeVersion(version)}|{(sha256 ?? string.Empty).Trim().ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    public static bool CanPromote(IReadOnlyList<string> blockers) => blockers.Count == 0;
}

public static class Batch600Completeness
{
    public static int BoundedPercent(int completed, int required)
    {
        if (required <= 0) return completed <= 0 ? 100 : 0;
        return Math.Clamp((int)Math.Round((Math.Max(0, completed) * 100.0) / required, MidpointRounding.AwayFromZero), 0, 100);
    }
    public static bool RequiredCountMet(int completed, int required) => required >= 0 && completed >= required;
    public static int WeightedScore(int freshness, int dependencies, int operatorQueue, int changeWindow, int candidate)
    {
        var values = new[] { freshness, dependencies, operatorQueue, changeWindow, candidate }.Select(x => Math.Clamp(x, 0, 100)).ToArray();
        return (int)Math.Round(values.Average(), MidpointRounding.AwayFromZero);
    }
    public static string Severity(int score) => score switch { >= 90 => "Ready", >= 70 => "Warning", _ => "Blocked" };
    public static bool Ready(int score, IEnumerable<string> blockers) => score >= 90 && !blockers.Any();
    public static IReadOnlyList<string> MergeBlockers(params IEnumerable<string>[] groups) => groups.SelectMany(x => x).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    public static int MissingCount(IEnumerable<string> blockers) => blockers.Distinct(StringComparer.Ordinal).Count();
    public static int Confidence(int evidenceCount, int expectedCount) => BoundedPercent(evidenceCount, expectedCount);
    public static bool ContradictionFree(IEnumerable<(string Key, string Value)> values) => values.GroupBy(x => x.Key, StringComparer.Ordinal).All(g => g.Select(x => x.Value).Distinct(StringComparer.Ordinal).Count() <= 1);
    public static B600ReadinessSummary Summary(int score, IEnumerable<string> blockers)
    {
        var normalized = blockers.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return new B600ReadinessSummary(Ready(score, normalized), Math.Clamp(score, 0, 100), normalized);
    }
}

public static class Batch600SafeSummary
{
    private static readonly string[] Forbidden = { "password=", "pwd=", "server=", "data source=", "select ", "insert ", "update ", "delete ", "exception:" };
    public static string NormalizeText(string? value) => string.Join(" ", (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    public static bool ContainsForbidden(string? value)
    {
        var text = NormalizeText(value).ToLowerInvariant();
        return Forbidden.Any(text.Contains);
    }
    public static string SafeLabel(string? value)
    {
        var normalized = NormalizeText(value);
        var safe = new string(normalized.Where(ch => !char.IsControl(ch)).Take(120).ToArray());
        return ContainsForbidden(safe) ? "[redacted]" : safe;
    }
    public static string OpaqueId(string? value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeText(value))))[..24];
    public static IReadOnlyDictionary<string, string> Allowlist(IReadOnlyDictionary<string, string?> source, IEnumerable<string> allowed)
    {
        var allow = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return source.Where(x => allow.Contains(x.Key) && !ContainsForbidden(x.Value)).OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => SafeLabel(x.Value), StringComparer.Ordinal);
    }
    public static bool IsSafeKey(string? key)
    {
        var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
        return normalized.Length > 0 && !normalized.Contains("password", StringComparison.Ordinal) && !normalized.Contains("secret", StringComparison.Ordinal) && !normalized.Contains("connection", StringComparison.Ordinal);
    }
    public static bool IsSafeValue(string? value) => !ContainsForbidden(value);
    public static string SafeHost(string? value) => new string((value ?? string.Empty).Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_').Take(128).ToArray());
    public static bool Exportable(IReadOnlyDictionary<string, string?> source) => source.All(x => IsSafeKey(x.Key) && IsSafeValue(x.Value));
    public static IReadOnlyList<string> UnsafeKeys(IReadOnlyDictionary<string, string?> source) => source.Where(x => !IsSafeKey(x.Key) || !IsSafeValue(x.Value)).Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();
}

public static class Batch600FleetReadiness
{
    public static string NormalizeNode(string? value) => new string((value ?? string.Empty).Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.').Take(96).ToArray());
    public static int AverageScore(IEnumerable<int> scores)
    {
        var values = scores.Select(x => Math.Clamp(x, 0, 100)).ToArray();
        return values.Length == 0 ? 0 : (int)Math.Round(values.Average(), MidpointRounding.AwayFromZero);
    }
    public static int MinimumScore(IEnumerable<int> scores)
    {
        var values = scores.Select(x => Math.Clamp(x, 0, 100)).ToArray();
        return values.Length == 0 ? 0 : values.Min();
    }
    public static int ReadyPercent(IEnumerable<bool> readiness)
    {
        var values = readiness.ToArray();
        return values.Length == 0 ? 0 : (int)Math.Round(values.Count(x => x) * 100.0 / values.Length, MidpointRounding.AwayFromZero);
    }
    public static bool AnyBlocked(IEnumerable<bool> readiness) => readiness.Any(x => !x);
    public static string FleetSeverity(int minScore, int readyPercent) => minScore < 70 ? "Blocked" : readyPercent < 100 ? "Warning" : "Ready";
    public static IReadOnlyList<string> BlockedNodes(IEnumerable<(string Node, bool Ready)> nodes) => nodes.Where(x => !x.Ready).Select(x => NormalizeNode(x.Node)).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    public static bool AllReady(IEnumerable<bool> readiness)
    {
        var values = readiness.ToArray();
        return values.Length > 0 && values.All(x => x);
    }
    public static int BlastRadius(IEnumerable<bool> readiness) => readiness.Count(x => !x);
    public static string Fingerprint(IEnumerable<(string Node, int Score)> nodes)
    {
        var canonical = string.Join("|", nodes.Select(x => $"{NormalizeNode(x.Node)}:{Math.Clamp(x.Score, 0, 100)}").OrderBy(x => x, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    public static B600ReadinessSummary Summary(IEnumerable<(string Node, int Score, bool Ready)> nodes)
    {
        var values = nodes.ToArray();
        var score = AverageScore(values.Select(x => x.Score));
        var blockers = BlockedNodes(values.Select(x => (x.Node, x.Ready))).Select(x => $"node:{x}").ToArray();
        return new B600ReadinessSummary(values.Length > 0 && blockers.Length == 0 && score >= 90, score, blockers);
    }
}

public static class Batch600Snapshot
{
    public static string NormalizeVersion(string? value) => Batch600CandidatePromotion.NormalizeVersion(value);
    public static long NormalizeSequence(long sequence) => Math.Max(0, sequence);
    public static bool IsMonotonic(long previous, long current) => previous >= 0 && current >= previous;
    public static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp) => timestamp.ToUniversalTime();
    public static string ETag(string? version, long sequence, DateTimeOffset capturedAt)
    {
        var canonical = $"{NormalizeVersion(version)}|{NormalizeSequence(sequence)}|{NormalizeTimestamp(capturedAt):O}";
        return "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..32] + "\"";
    }
    public static bool ETagMatches(string? provided, string? current) => string.Equals((provided ?? string.Empty).Trim(), (current ?? string.Empty).Trim(), StringComparison.Ordinal);
    public static bool NotModified(string? provided, string current) => ETagMatches(provided, current);
    public static bool VersionChanged(string? previous, string? current) => !string.Equals(NormalizeVersion(previous), NormalizeVersion(current), StringComparison.Ordinal);
    public static bool SequenceAdvanced(long previous, long current) => current > previous && current >= 0;
    public static string SnapshotId(string? version, long sequence)
    {
        var canonical = $"{NormalizeVersion(version)}|{NormalizeSequence(sequence)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..24];
    }
    public static bool Cacheable(bool secretSafe, bool deterministic) => secretSafe && deterministic;
}

public static class Batch600ReleaseGate
{
    public static string TaskId(int number) => number is >= 1 and <= 100 ? $"B600-{number:000}" : throw new ArgumentOutOfRangeException(nameof(number));
    public static bool TryParseTaskId(string? value, out int number)
    {
        number = 0;
        if (value is null || value.Length != 8 || !value.StartsWith("B600-", StringComparison.Ordinal)) return false;
        return int.TryParse(value.AsSpan(5), out number) && number is >= 1 and <= 100 && value == TaskId(number);
    }
    public static bool IsComplete(IEnumerable<string> ids)
    {
        var set = ids.Where(x => TryParseTaskId(x, out _)).ToHashSet(StringComparer.Ordinal);
        return Enumerable.Range(1, 100).All(i => set.Contains(TaskId(i)));
    }
    public static int SchemaVersion() => 1;
    public static IReadOnlyList<string> FeatureGroups() => new[] { "evidence-freshness", "dependency-graph", "operator-action-queue", "change-window", "candidate-promotion", "completeness", "safe-summaries", "fleet-readiness", "snapshot-etag", "release-contract" };
    public static IReadOnlyList<string> Guardrails() => new[] { "read-policy-only", "no-browser-to-sql", "no-autonomous-remediation", "no-ai-sql", "secret-safe-contracts", "fail-closed", "singlenode-first", "external-iis-acceptance-not-implied" };
    public static IReadOnlyDictionary<string, object> ContractManifest() => new Dictionary<string, object>(StringComparer.Ordinal)
    {
        ["schemaVersion"] = SchemaVersion(), ["batch"] = "B600", ["taskCount"] = 100, ["taskRange"] = "B600-001..100", ["featureGroups"] = FeatureGroups(), ["guardrails"] = Guardrails(), ["externalAcceptanceImplied"] = false
    };
    public static string ContractHash()
    {
        var canonical = $"v{SchemaVersion()}|B600|100|{string.Join(",", FeatureGroups())}|{string.Join(",", Guardrails())}|false";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    public static B600ReadinessSummary Evaluate(IEnumerable<string> completedTaskIds, bool repositoryCiGreen, bool externalAcceptanceClaimed)
    {
        var blockers = new List<string>();
        if (!IsComplete(completedTaskIds)) blockers.Add("task-set-incomplete");
        if (!repositoryCiGreen) blockers.Add("repository-ci-not-green");
        if (externalAcceptanceClaimed) blockers.Add("external-acceptance-must-not-be-inferred");
        var complete = IsComplete(completedTaskIds);
        return new B600ReadinessSummary(complete && repositoryCiGreen && !externalAcceptanceClaimed, complete ? 100 : 0, blockers);
    }
    public static bool ReadPolicyOnly(bool isGet, bool readPolicyAuthorized) => isGet && readPolicyAuthorized;
    public static bool RejectsExternalAcceptanceClaim(bool externalAcceptanceClaimed) => !externalAcceptanceClaimed;
    public static IReadOnlyList<string> AllTaskIds() => Enumerable.Range(1, 100).Select(TaskId).ToArray();
}
