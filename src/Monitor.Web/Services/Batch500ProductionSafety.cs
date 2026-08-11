using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public sealed record B500GateEvaluation(
    bool Ready,
    int CompletedTasks,
    double ReadinessPercent,
    IReadOnlyList<string> Blockers);

public static class Batch500DeploymentEvidence
{
    public static string NormalizeEnvironment(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "prod" or "production" => "production",
            "stage" or "staging" => "staging",
            "dev" or "development" => "development",
            "test" or "testing" => "testing",
            _ => "unknown"
        };
    }

    public static bool IsHttpsUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeArtifactName(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
        var name = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        return name.Length <= 160 ? name : name[..160];
    }

    public static bool IsValidSha256(string? value) =>
        value is not null && value.Length == 64 && value.All(Uri.IsHexDigit);

    public static bool IsValidCommitSha(string? value) =>
        value is not null && value.Length is >= 7 and <= 40 && value.All(Uri.IsHexDigit);

    public static double AgeMinutes(DateTimeOffset now, DateTimeOffset capturedAt) =>
        Math.Round(Math.Max(0, (now - capturedAt).TotalMinutes), 2);

    public static bool IsFresh(DateTimeOffset now, DateTimeOffset capturedAt, double maxAgeMinutes) =>
        maxAgeMinutes >= 0 && AgeMinutes(now, capturedAt) <= maxAgeMinutes;

    public static IReadOnlyList<string> MissingFields(
        IReadOnlyDictionary<string, string?> evidence,
        IEnumerable<string> requiredFields) =>
        requiredFields
            .Where(field => !evidence.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();

    public static string HostLabel(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length > 128) normalized = normalized[..128];
        return new string(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_').ToArray());
    }

    public static string Fingerprint(string? environment, string? artifact, string? sha256, string? commitSha)
    {
        var canonical = string.Join('|',
            NormalizeEnvironment(environment),
            NormalizeArtifactName(artifact),
            (sha256 ?? string.Empty).Trim().ToUpperInvariant(),
            (commitSha ?? string.Empty).Trim().ToLowerInvariant());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public static class Batch500IisReadiness
{
    public static string NormalizeIdentity(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= 128 ? normalized : normalized[..128];
    }

    public static bool IsIntegratedPipeline(string? value) =>
        string.Equals((value ?? string.Empty).Trim(), "Integrated", StringComparison.OrdinalIgnoreCase);

    public static bool IsNoManagedCode(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrEmpty(normalized) ||
               string.Equals(normalized, "No Managed Code", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAlwaysRunning(string? value) =>
        string.Equals((value ?? string.Empty).Trim(), "AlwaysRunning", StringComparison.OrdinalIgnoreCase);

    public static bool IsPreloadEnabled(bool enabled) => enabled;

    public static bool Is64Bit(bool enable32Bit) => !enable32Bit;

    public static bool IsIdleTimeoutSafe(int idleTimeoutMinutes) => idleTimeoutMinutes <= 0;

    public static bool IsHttpsBinding(string? scheme, int port) =>
        string.Equals((scheme ?? string.Empty).Trim(), "https", StringComparison.OrdinalIgnoreCase) &&
        port is >= 1 and <= 65535;

    public static bool IsHostHeaderPresent(string? host) =>
        !string.IsNullOrWhiteSpace(Batch500DeploymentEvidence.HostLabel(host));

    public static IReadOnlyList<string> Blockers(
        string? identity,
        string? pipelineMode,
        string? managedRuntime,
        string? startMode,
        bool preloadEnabled,
        bool enable32Bit,
        int idleTimeoutMinutes,
        string? scheme,
        int port,
        string? host)
    {
        var blockers = new List<string>();
        if (string.IsNullOrWhiteSpace(NormalizeIdentity(identity))) blockers.Add("app-pool-identity-missing");
        if (!IsIntegratedPipeline(pipelineMode)) blockers.Add("pipeline-not-integrated");
        if (!IsNoManagedCode(managedRuntime)) blockers.Add("managed-runtime-not-empty");
        if (!IsAlwaysRunning(startMode)) blockers.Add("start-mode-not-always-running");
        if (!IsPreloadEnabled(preloadEnabled)) blockers.Add("preload-disabled");
        if (!Is64Bit(enable32Bit)) blockers.Add("32-bit-enabled");
        if (!IsIdleTimeoutSafe(idleTimeoutMinutes)) blockers.Add("idle-timeout-enabled");
        if (!IsHttpsBinding(scheme, port)) blockers.Add("https-binding-invalid");
        if (!IsHostHeaderPresent(host)) blockers.Add("host-header-missing");
        return blockers;
    }
}

public static class Batch500CertificateReadiness
{
    public static string NormalizeHostname(string? value) =>
        Batch500DeploymentEvidence.HostLabel(value);

    public static int RemainingDays(DateTimeOffset now, DateTimeOffset notAfter) =>
        Math.Max(0, (int)Math.Floor((notAfter - now).TotalDays));

    public static string ExpiryRisk(DateTimeOffset now, DateTimeOffset notAfter)
    {
        if (notAfter <= now) return "Expired";
        var days = RemainingDays(now, notAfter);
        if (days < 7) return "Critical";
        if (days < 30) return "Warning";
        return "Healthy";
    }

    public static bool IsStrongRsaKey(int keySizeBits) => keySizeBits >= 2048;

    public static bool IsAllowedSignature(string? algorithm)
    {
        var value = (algorithm ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("sha1", StringComparison.Ordinal) || value.Contains("md5", StringComparison.Ordinal)) return false;
        return value.Contains("sha256", StringComparison.Ordinal) ||
               value.Contains("sha384", StringComparison.Ordinal) ||
               value.Contains("sha512", StringComparison.Ordinal);
    }

    public static bool SanMatches(string? host, IEnumerable<string> sans)
    {
        var normalizedHost = NormalizeHostname(host);
        if (string.IsNullOrEmpty(normalizedHost)) return false;

        foreach (var san in sans)
        {
            var normalizedSan = NormalizeHostname(san);
            if (string.Equals(normalizedSan, normalizedHost, StringComparison.OrdinalIgnoreCase)) return true;
            if (normalizedSan.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = normalizedSan[1..];
                if (normalizedHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    normalizedHost.Count(ch => ch == '.') == normalizedSan.Count(ch => ch == '.'))
                    return true;
            }
        }
        return false;
    }

    public static string NormalizeThumbprint(string? value)
    {
        var chars = (value ?? string.Empty)
            .Where(Uri.IsHexDigit)
            .Select(char.ToUpperInvariant)
            .ToArray();
        return new string(chars);
    }

    public static bool IsChainHealthy(bool trustedRoot, int chainErrors) =>
        trustedRoot && chainErrors == 0;

    public static int RiskScore(
        DateTimeOffset now,
        DateTimeOffset notAfter,
        int keySizeBits,
        string? signatureAlgorithm,
        bool sanMatches,
        bool trustedRoot,
        int chainErrors)
    {
        var score = 0;
        var expiry = ExpiryRisk(now, notAfter);
        score += expiry switch { "Expired" => 60, "Critical" => 40, "Warning" => 20, _ => 0 };
        if (!IsStrongRsaKey(keySizeBits)) score += 20;
        if (!IsAllowedSignature(signatureAlgorithm)) score += 20;
        if (!sanMatches) score += 25;
        if (!IsChainHealthy(trustedRoot, chainErrors)) score += 35;
        return Math.Clamp(score, 0, 100);
    }

    public static bool IsCertificateReady(
        DateTimeOffset now,
        DateTimeOffset notAfter,
        int keySizeBits,
        string? signatureAlgorithm,
        bool sanMatches,
        bool trustedRoot,
        int chainErrors) =>
        ExpiryRisk(now, notAfter) == "Healthy" &&
        IsStrongRsaKey(keySizeBits) &&
        IsAllowedSignature(signatureAlgorithm) &&
        sanMatches &&
        IsChainHealthy(trustedRoot, chainErrors);
}

public static class Batch500Durability
{
    private static string NormalizePath(string? value) =>
        (value ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/').ToLowerInvariant();

    public static bool IsExternalStatePath(string? releaseRoot, string? statePath)
    {
        var root = NormalizePath(releaseRoot);
        var state = NormalizePath(statePath);
        return !string.IsNullOrEmpty(root) &&
               !string.IsNullOrEmpty(state) &&
               !state.Equals(root, StringComparison.Ordinal) &&
               !state.StartsWith(root + "/", StringComparison.Ordinal);
    }

    public static bool IsExternalKeyRingPath(string? releaseRoot, string? keyRingPath) =>
        IsExternalStatePath(releaseRoot, keyRingPath);

    public static bool RegistrationCountPreserved(int before, int after) =>
        before >= 0 && after >= before;

    public static bool SnapshotEvidencePreserved(int before, int after) =>
        before >= 0 && after >= before;

    public static bool AuditMonotonic(long before, long after) =>
        before >= 0 && after >= before;

    public static bool IncidentMonotonic(long before, long after) =>
        before >= 0 && after >= before;

    public static bool CredentialResolved(bool resolved) => resolved;

    public static bool HealthRecovered(bool live, bool ready) => live && ready;

    public static bool RestartWithinSla(TimeSpan duration, int maxSeconds) =>
        maxSeconds > 0 && duration >= TimeSpan.Zero && duration.TotalSeconds <= maxSeconds;

    public static IReadOnlyList<string> Evaluate(
        bool externalStatePath,
        bool externalKeyRingPath,
        bool registrationsPreserved,
        bool snapshotsPreserved,
        bool auditMonotonic,
        bool incidentsMonotonic,
        bool credentialResolved,
        bool healthRecovered,
        bool restartWithinSla)
    {
        var blockers = new List<string>();
        if (!externalStatePath) blockers.Add("state-path-not-external");
        if (!externalKeyRingPath) blockers.Add("keyring-path-not-external");
        if (!registrationsPreserved) blockers.Add("registrations-not-preserved");
        if (!snapshotsPreserved) blockers.Add("snapshots-not-preserved");
        if (!auditMonotonic) blockers.Add("audit-regressed");
        if (!incidentsMonotonic) blockers.Add("incidents-regressed");
        if (!credentialResolved) blockers.Add("credential-not-resolved");
        if (!healthRecovered) blockers.Add("health-not-recovered");
        if (!restartWithinSla) blockers.Add("restart-sla-missed");
        return blockers;
    }
}

public static class Batch500RollbackSafety
{
    public static bool IsBackupFresh(DateTimeOffset now, DateTimeOffset createdAt, double maxAgeHours) =>
        maxAgeHours >= 0 && createdAt <= now && (now - createdAt).TotalHours <= maxAgeHours;

    public static bool ChecksumPresent(string? sha256) =>
        Batch500DeploymentEvidence.IsValidSha256((sha256 ?? string.Empty).Trim());

    public static bool ManifestPresent(bool present) => present;

    public static bool PreviousReleasePreserved(bool preserved) => preserved;

    public static bool DurableStateIncluded(bool included) => included;

    public static bool KeyRingIncluded(bool included) => included;

    public static bool RestoreValidationPassed(bool passed) => passed;

    public static bool RollbackSmokePassed(bool passed) => passed;

    public static bool RollbackWithinSla(TimeSpan duration, int maxMinutes) =>
        maxMinutes > 0 && duration >= TimeSpan.Zero && duration.TotalMinutes <= maxMinutes;

    public static IReadOnlyList<string> Evaluate(
        bool freshBackup,
        bool checksumPresent,
        bool manifestPresent,
        bool previousReleasePreserved,
        bool durableStateIncluded,
        bool keyRingIncluded,
        bool restoreValidationPassed,
        bool rollbackSmokePassed,
        bool rollbackWithinSla)
    {
        var blockers = new List<string>();
        if (!freshBackup) blockers.Add("backup-stale-or-missing");
        if (!checksumPresent) blockers.Add("backup-checksum-missing");
        if (!manifestPresent) blockers.Add("backup-manifest-missing");
        if (!previousReleasePreserved) blockers.Add("previous-release-missing");
        if (!durableStateIncluded) blockers.Add("durable-state-not-backed-up");
        if (!keyRingIncluded) blockers.Add("keyring-not-backed-up");
        if (!restoreValidationPassed) blockers.Add("restore-validation-failed");
        if (!rollbackSmokePassed) blockers.Add("rollback-smoke-failed");
        if (!rollbackWithinSla) blockers.Add("rollback-sla-missed");
        return blockers;
    }
}

public static class Batch500LeastPrivilege
{
    public static bool IsNonSysAdmin(bool isSysAdmin) => !isSysAdmin;
    public static bool HasServerStateRead(bool granted) => granted;
    public static bool HasViewAnyDatabase(bool granted) => granted;
    public static bool HasDefinitionMetadata(bool granted) => granted;
    public static bool HasAgentMetadataRead(bool granted) => granted;
    public static bool NoTargetDml(bool hasTargetDml) => !hasTargetDml;
    public static bool NoTargetDdl(bool hasTargetDdl) => !hasTargetDdl;
    public static bool NoImpersonation(bool hasImpersonate) => !hasImpersonate;
    public static bool CollectionSucceeded(bool succeeded) => succeeded;

    public static IReadOnlyList<string> Evaluate(
        bool nonSysAdmin,
        bool serverStateRead,
        bool viewAnyDatabase,
        bool definitionMetadata,
        bool agentMetadataRead,
        bool noTargetDml,
        bool noTargetDdl,
        bool noImpersonation,
        bool collectionSucceeded)
    {
        var blockers = new List<string>();
        if (!nonSysAdmin) blockers.Add("sysadmin-not-allowed");
        if (!serverStateRead) blockers.Add("server-state-read-missing");
        if (!viewAnyDatabase) blockers.Add("view-any-database-missing");
        if (!definitionMetadata) blockers.Add("definition-metadata-missing");
        if (!agentMetadataRead) blockers.Add("agent-metadata-read-missing");
        if (!noTargetDml) blockers.Add("target-dml-present");
        if (!noTargetDdl) blockers.Add("target-ddl-present");
        if (!noImpersonation) blockers.Add("impersonation-present");
        if (!collectionSucceeded) blockers.Add("collection-did-not-succeed");
        return blockers;
    }
}

public static class Batch500ProductionSmoke
{
    public static string NormalizeHealthStatus(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "live" => "Live",
            "ready" => "Ready",
            "degraded" => "Degraded",
            _ => "Unknown"
        };
    }

    public static bool LivePassed(int statusCode, string? status) =>
        statusCode == 200 && NormalizeHealthStatus(status) is "Live" or "Ready";

    public static bool ReadyPassed(int statusCode, string? status) =>
        statusCode == 200 && NormalizeHealthStatus(status) == "Ready";

    public static bool HealthPassed(int statusCode, string? status) =>
        statusCode == 200 && NormalizeHealthStatus(status) is "Ready" or "Live";

    public static bool LoginPassed(bool authenticated) => authenticated;

    public static bool ProtectedRoutePassed(int statusCode) => statusCode == 200;

    public static bool AntiforgeryEnforced(bool enforced) => enforced;

    public static bool SecureCookieEnforced(bool secure) => secure;

    public static bool HttpsOnly(string? baseUri) => Batch500DeploymentEvidence.IsHttpsUri(baseUri);

    public static IReadOnlyList<string> Evaluate(
        bool livePassed,
        bool readyPassed,
        bool healthPassed,
        bool loginPassed,
        bool protectedRoutePassed,
        bool antiforgeryEnforced,
        bool secureCookieEnforced,
        bool httpsOnly)
    {
        var blockers = new List<string>();
        if (!livePassed) blockers.Add("live-health-failed");
        if (!readyPassed) blockers.Add("readiness-health-failed");
        if (!healthPassed) blockers.Add("aggregate-health-failed");
        if (!loginPassed) blockers.Add("admin-login-failed");
        if (!protectedRoutePassed) blockers.Add("protected-route-failed");
        if (!antiforgeryEnforced) blockers.Add("antiforgery-not-enforced");
        if (!secureCookieEnforced) blockers.Add("secure-cookie-not-enforced");
        if (!httpsOnly) blockers.Add("https-required");
        return blockers;
    }
}

public static class Batch500CutoverSafety
{
    public static double WindowDurationMinutes(DateTimeOffset start, DateTimeOffset end) =>
        Math.Round(Math.Max(0, (end - start).TotalMinutes), 2);

    public static bool WindowValid(DateTimeOffset start, DateTimeOffset end, double maxMinutes) =>
        maxMinutes > 0 && end > start && WindowDurationMinutes(start, end) <= maxMinutes;

    public static string NormalizeTicket(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized.Length <= 64 ? normalized : normalized[..64];
    }

    public static bool ValidTicket(string? value)
    {
        var ticket = NormalizeTicket(value);
        return ticket.Length >= 3 &&
               ticket.Contains('-', StringComparison.Ordinal) &&
               ticket.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
    }

    public static bool ApprovalCountEnough(int approvals, int required) =>
        required >= 0 && approvals >= required;

    public static bool RollbackOwnerPresent(string? owner) =>
        !string.IsNullOrWhiteSpace(owner);

    public static bool NoFreezeConflict(bool freezeConflict) => !freezeConflict;

    public static bool BackupGatePassed(bool passed) => passed;

    public static IReadOnlyList<string> Blockers(
        bool validWindow,
        bool validTicket,
        bool enoughApprovals,
        bool rollbackOwnerPresent,
        bool noFreezeConflict,
        bool backupGatePassed)
    {
        var blockers = new List<string>();
        if (!validWindow) blockers.Add("change-window-invalid");
        if (!validTicket) blockers.Add("change-ticket-invalid");
        if (!enoughApprovals) blockers.Add("approvals-missing");
        if (!rollbackOwnerPresent) blockers.Add("rollback-owner-missing");
        if (!noFreezeConflict) blockers.Add("change-freeze-conflict");
        if (!backupGatePassed) blockers.Add("backup-gate-failed");
        return blockers;
    }

    public static bool GoNoGo(IEnumerable<string> blockers) => !blockers.Any();
}

public static class Batch500EvidenceSafety
{
    public static bool ContainsPasswordAssignment(string? value)
    {
        var text = (value ?? string.Empty).ToLowerInvariant();
        return text.Contains("password=", StringComparison.Ordinal) ||
               text.Contains("pwd=", StringComparison.Ordinal) ||
               text.Contains("\"password\":", StringComparison.Ordinal) ||
               text.Contains("'password':", StringComparison.Ordinal);
    }

    public static bool ContainsConnectionStringShape(string? value)
    {
        var text = (value ?? string.Empty).ToLowerInvariant();
        var hasServer = text.Contains("server=", StringComparison.Ordinal) ||
                        text.Contains("data source=", StringComparison.Ordinal);
        var hasSeparator = text.Contains(';', StringComparison.Ordinal);
        var hasIdentity = text.Contains("user id=", StringComparison.Ordinal) ||
                          text.Contains("uid=", StringComparison.Ordinal) ||
                          ContainsPasswordAssignment(text);
        return hasServer && hasSeparator && hasIdentity;
    }

    public static bool ContainsRawProviderError(string? value)
    {
        var text = (value ?? string.Empty).ToLowerInvariant();
        return text.Contains("sqlexception", StringComparison.Ordinal) ||
               text.Contains("microsoft.data.sqlclient", StringComparison.Ordinal) ||
               text.Contains("system.data.sqlclient", StringComparison.Ordinal) ||
               text.Contains("stack trace", StringComparison.Ordinal);
    }

    public static bool ContainsSqlText(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        return (text.StartsWith("select ", StringComparison.Ordinal) && text.Contains(" from ", StringComparison.Ordinal)) ||
               text.StartsWith("insert ", StringComparison.Ordinal) ||
               text.StartsWith("update ", StringComparison.Ordinal) ||
               text.StartsWith("delete ", StringComparison.Ordinal) ||
               text.StartsWith("alter ", StringComparison.Ordinal) ||
               text.StartsWith("drop ", StringComparison.Ordinal) ||
               text.StartsWith("create ", StringComparison.Ordinal);
    }

    public static string NormalizeKey(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        var safe = new string(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.').ToArray());
        return safe.Length <= 64 ? safe : safe[..64];
    }

    public static string ClampValue(string? value, int maxLength = 256)
    {
        if (maxLength < 0) throw new ArgumentOutOfRangeException(nameof(maxLength));
        var normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    public static string OpaqueId(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes)[..16];
    }

    public static string SafeHost(string? value) => Batch500DeploymentEvidence.HostLabel(value);

    public static IReadOnlyDictionary<string, string> FilterAllowedFields(
        IReadOnlyDictionary<string, string?> input,
        IEnumerable<string> allowlist)
    {
        var allowed = allowlist.Select(NormalizeKey).ToHashSet(StringComparer.Ordinal);
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in input)
        {
            var key = NormalizeKey(pair.Key);
            if (allowed.Contains(key))
                result[key] = ClampValue(pair.Value);
        }
        return result;
    }

    public static bool IsSafeEvidence(string? value) =>
        !ContainsPasswordAssignment(value) &&
        !ContainsConnectionStringShape(value) &&
        !ContainsRawProviderError(value) &&
        !ContainsSqlText(value);
}

public static class Batch500ReleaseGate
{
    public const string SchemaVersion = "monitor-production-safety-b500-v1";
    public const int Start = 1;
    public const int End = 100;
    public const int TaskCount = 100;

    public static string TaskId(int number)
    {
        if (number is < Start or > End) throw new ArgumentOutOfRangeException(nameof(number));
        return $"B500-{number:000}";
    }

    public static bool TryParseTaskId(string? value, out int number)
    {
        number = 0;
        if (value is null || value.Length != 8 || !value.StartsWith("B500-", StringComparison.Ordinal)) return false;
        return int.TryParse(value.AsSpan(5), out number) && number is >= Start and <= End;
    }

    public static bool HasAllTasks(IEnumerable<string> taskIds)
    {
        var set = taskIds.Where(value => TryParseTaskId(value, out _)).ToHashSet(StringComparer.Ordinal);
        return Enumerable.Range(Start, TaskCount).Select(TaskId).All(set.Contains);
    }

    public static IReadOnlyList<string> FeatureGroups() =>
    [
        "deployment-evidence",
        "iis-readiness",
        "certificate-readiness",
        "restart-durability",
        "backup-rollback",
        "least-privilege",
        "production-smoke",
        "cutover-safety",
        "evidence-safety",
        "release-contract"
    ];

    public static IReadOnlyList<string> Guardrails() =>
    [
        "external-iis-acceptance-remains-required",
        "no-autonomous-remediation",
        "no-browser-to-sql",
        "no-ai-sql-execution",
        "no-plaintext-credentials",
        "no-full-connection-strings",
        "no-raw-provider-errors",
        "no-arbitrary-sql-text",
        "single-node-first",
        "fail-closed"
    ];

    public static IReadOnlyDictionary<string, object> ContractManifest()
    {
        var tasks = Enumerable.Range(Start, TaskCount).Select(TaskId).ToArray();
        return new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = SchemaVersion,
            ["taskCount"] = TaskCount,
            ["rangeStart"] = TaskId(Start),
            ["rangeEnd"] = TaskId(End),
            ["tasks"] = tasks,
            ["featureGroups"] = FeatureGroups(),
            ["guardrails"] = Guardrails(),
            ["externalAcceptance"] = "required"
        };
    }

    public static string ContractHash()
    {
        var canonical = string.Join('|',
            new[] { SchemaVersion, TaskId(Start), TaskId(End), "external-acceptance-required" }
                .Concat(FeatureGroups())
                .Concat(Guardrails())
                .Concat(Enumerable.Range(Start, TaskCount).Select(TaskId)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static B500GateEvaluation Evaluate(
        bool releaseBuildGreen,
        int passedTests,
        int failedTests,
        IEnumerable<string> taskIds,
        bool guardrailsIntact,
        bool externalAcceptanceClaimed)
    {
        var ids = taskIds.ToArray();
        var blockers = new List<string>();
        if (!releaseBuildGreen) blockers.Add("release-build-not-green");
        if (passedTests <= 0) blockers.Add("no-passing-tests");
        if (failedTests != 0) blockers.Add("test-failures-present");
        if (!HasAllTasks(ids)) blockers.Add("task-ledger-incomplete");
        if (!guardrailsIntact) blockers.Add("guardrail-invariant-failed");
        if (externalAcceptanceClaimed) blockers.Add("external-acceptance-must-not-be-claimed-by-ci");

        var completed = ids.Where(value => TryParseTaskId(value, out _)).Distinct(StringComparer.Ordinal).Count();
        var readiness = Math.Round(Math.Clamp(completed * 100d / TaskCount, 0, 100), 2);
        return new(blockers.Count == 0, completed, readiness, blockers);
    }
}
