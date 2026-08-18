using System.Text.Json;

namespace Monitor.Web.Services;

public enum GovernancePruneKind
{
    Server,
    Incident,
    Note
}

public sealed record GovernancePruneMarker(GovernancePruneKind Kind, string Target);

public interface IGovernancePruneStateStore
{
    bool Contains(GovernancePruneKind kind, string target);
    void MarkPruned(GovernancePruneKind kind, string target);
    void Synchronize(EnterpriseOperatorSnapshot metadata, IEnumerable<GovernancePruneMarker> retainedLegacyMarkers);
}

internal static class GovernancePruneStatePolicy
{
    private const int FormatVersion = 1;
    private const int MaxTargetLength = 160;

    public static int Version => FormatVersion;

    public static int MaxCount(GovernancePruneKind kind) => kind switch
    {
        GovernancePruneKind.Server => OperatorMetadataSnapshotValidator.MaxServers,
        GovernancePruneKind.Incident => OperatorMetadataSnapshotValidator.MaxIncidents,
        GovernancePruneKind.Note => OperatorMetadataSnapshotValidator.MaxIncidents * EnterpriseOperatorValidation.MaxNotesPerIncident,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string Normalize(GovernancePruneKind kind, string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Length > MaxTargetLength)
        {
            throw new ArgumentException("Governance prune target is outside the bounded metadata contract.", nameof(target));
        }

        var value = target.Trim();
        if (kind is GovernancePruneKind.Server or GovernancePruneKind.Note)
        {
            if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            {
                throw new ArgumentException("Governance prune target must be a non-empty GUID.", nameof(target));
            }

            return id.ToString("D");
        }

        if (kind != GovernancePruneKind.Incident)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return EnterpriseOperatorValidation.NormalizeIncidentId(value);
    }

    public static Dictionary<GovernancePruneKind, HashSet<string>> AllowedTargets(EnterpriseOperatorSnapshot metadata)
    {
        var validated = OperatorMetadataSnapshotValidator.Validate(metadata);
        return new Dictionary<GovernancePruneKind, HashSet<string>>
        {
            [GovernancePruneKind.Server] = validated.Servers
                .Select(item => item.RegistrationId.ToString("D"))
                .ToHashSet(StringComparer.Ordinal),
            [GovernancePruneKind.Incident] = validated.Incidents
                .Select(item => Normalize(GovernancePruneKind.Incident, item.IncidentId))
                .ToHashSet(StringComparer.Ordinal),
            [GovernancePruneKind.Note] = validated.Incidents
                .SelectMany(item => item.Notes)
                .Select(item => item.Id.ToString("D"))
                .ToHashSet(StringComparer.Ordinal)
        };
    }

    public static HashSet<string> MergeBounded(
        GovernancePruneKind kind,
        IEnumerable<string> current,
        IEnumerable<GovernancePruneMarker> legacy,
        IReadOnlySet<string> allowed)
    {
        var merged = current
            .Select(target => Normalize(kind, target))
            .Where(allowed.Contains)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var marker in legacy.Where(item => item.Kind == kind))
        {
            var target = Normalize(kind, marker.Target);
            if (allowed.Contains(target))
            {
                merged.Add(target);
            }
        }

        ValidateBound(kind, merged);
        return merged;
    }

    public static void ValidateBound(GovernancePruneKind kind, IReadOnlyCollection<string> targets)
    {
        if (targets.Count > MaxCount(kind))
        {
            throw new InvalidDataException("Governance prune state exceeds bounded operator metadata capacity.");
        }
    }
}

public sealed class InMemoryGovernancePruneStateStore : IGovernancePruneStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<GovernancePruneKind, HashSet<string>> _targets = CreateEmpty();

    public bool Contains(GovernancePruneKind kind, string target)
    {
        var normalized = GovernancePruneStatePolicy.Normalize(kind, target);
        lock (_gate)
        {
            return _targets[kind].Contains(normalized);
        }
    }

    public void MarkPruned(GovernancePruneKind kind, string target)
    {
        var normalized = GovernancePruneStatePolicy.Normalize(kind, target);
        lock (_gate)
        {
            var candidate = new HashSet<string>(_targets[kind], StringComparer.Ordinal) { normalized };
            GovernancePruneStatePolicy.ValidateBound(kind, candidate);
            _targets[kind] = candidate;
        }
    }

    public void Synchronize(EnterpriseOperatorSnapshot metadata, IEnumerable<GovernancePruneMarker> retainedLegacyMarkers)
    {
        ArgumentNullException.ThrowIfNull(retainedLegacyMarkers);
        var legacy = retainedLegacyMarkers.ToArray();
        var allowed = GovernancePruneStatePolicy.AllowedTargets(metadata);
        lock (_gate)
        {
            foreach (var kind in Enum.GetValues<GovernancePruneKind>())
            {
                _targets[kind] = GovernancePruneStatePolicy.MergeBounded(kind, _targets[kind], legacy, allowed[kind]);
            }
        }
    }

    private static Dictionary<GovernancePruneKind, HashSet<string>> CreateEmpty() =>
        Enum.GetValues<GovernancePruneKind>()
            .ToDictionary(kind => kind, _ => new HashSet<string>(StringComparer.Ordinal));
}

public sealed class FileGovernancePruneStateStore : IGovernancePruneStateStore
{
    private readonly object _gate = new();
    private readonly string _rootPath;
    private readonly Dictionary<GovernancePruneKind, HashSet<string>> _targets;

    public FileGovernancePruneStateStore(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Governance prune state root path is required.", nameof(rootPath));
        }

        _rootPath = Path.GetFullPath(rootPath);
        _targets = Enum.GetValues<GovernancePruneKind>()
            .ToDictionary(kind => kind, _ => new HashSet<string>(StringComparer.Ordinal));
        foreach (var kind in Enum.GetValues<GovernancePruneKind>())
        {
            using var lease = CrossProcessFileLease.Acquire(LeasePathFor(kind), "Governance prune state");
            _targets[kind] = Load(kind);
        }
    }

    public bool Contains(GovernancePruneKind kind, string target)
    {
        var normalized = GovernancePruneStatePolicy.Normalize(kind, target);
        lock (_gate)
        {
            using var lease = CrossProcessFileLease.Acquire(LeasePathFor(kind), "Governance prune state");
            ReloadFromDisk(kind);
            return _targets[kind].Contains(normalized);
        }
    }

    public void MarkPruned(GovernancePruneKind kind, string target)
    {
        var normalized = GovernancePruneStatePolicy.Normalize(kind, target);
        lock (_gate)
        {
            using var lease = CrossProcessFileLease.Acquire(LeasePathFor(kind), "Governance prune state");
            ReloadFromDisk(kind);
            if (_targets[kind].Contains(normalized))
            {
                return;
            }

            var candidate = new HashSet<string>(_targets[kind], StringComparer.Ordinal) { normalized };
            GovernancePruneStatePolicy.ValidateBound(kind, candidate);
            Persist(kind, candidate);
            _targets[kind] = candidate;
        }
    }

    public void Synchronize(EnterpriseOperatorSnapshot metadata, IEnumerable<GovernancePruneMarker> retainedLegacyMarkers)
    {
        ArgumentNullException.ThrowIfNull(retainedLegacyMarkers);
        var legacy = retainedLegacyMarkers.ToArray();
        var allowed = GovernancePruneStatePolicy.AllowedTargets(metadata);
        lock (_gate)
        {
            foreach (var kind in Enum.GetValues<GovernancePruneKind>())
            {
                using var lease = CrossProcessFileLease.Acquire(LeasePathFor(kind), "Governance prune state");
                ReloadFromDisk(kind);
                var candidate = GovernancePruneStatePolicy.MergeBounded(kind, _targets[kind], legacy, allowed[kind]);
                if (_targets[kind].SetEquals(candidate))
                {
                    continue;
                }

                Persist(kind, candidate);
                _targets[kind] = candidate;
            }
        }
    }

    private void ReloadFromDisk(GovernancePruneKind kind) => _targets[kind] = Load(kind);

    private HashSet<string> Load(GovernancePruneKind kind)
    {
        var envelope = AtomicJsonFile.Load<PruneEnvelope>(PathFor(kind));
        if (envelope is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (envelope.Version != GovernancePruneStatePolicy.Version || envelope.Targets is null)
        {
            throw new InvalidDataException("Governance prune state file format is not supported.");
        }

        var targets = envelope.Targets
            .Select(target => GovernancePruneStatePolicy.Normalize(kind, target))
            .ToHashSet(StringComparer.Ordinal);
        if (targets.Count != envelope.Targets.Length)
        {
            throw new InvalidDataException("Governance prune state file contains duplicate targets.");
        }

        GovernancePruneStatePolicy.ValidateBound(kind, targets);
        return targets;
    }

    private void Persist(GovernancePruneKind kind, HashSet<string> targets) =>
        AtomicJsonFile.Save(
            PathFor(kind),
            new PruneEnvelope(
                GovernancePruneStatePolicy.Version,
                targets.OrderBy(item => item, StringComparer.Ordinal).ToArray()));

    private string PathFor(GovernancePruneKind kind) =>
        Path.Combine(_rootPath, $"governance-prune-{kind.ToString().ToLowerInvariant()}.json");

    private string LeasePathFor(GovernancePruneKind kind) => $"{PathFor(kind)}.lock";

    private sealed record PruneEnvelope(int Version, string[]? Targets);
}

public sealed class SharedGovernancePruneStateStore : IGovernancePruneStateStore
{
    private const string KeyPrefix = "monitor:governance-prune:v1";
    private readonly ISharedStateDocumentStore _store;

    public SharedGovernancePruneStateStore(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public bool Contains(GovernancePruneKind kind, string target)
    {
        var normalized = GovernancePruneStatePolicy.Normalize(kind, target);
        return Read(kind).Contains(normalized);
    }

    public void MarkPruned(GovernancePruneKind kind, string target)
    {
        var normalized = GovernancePruneStatePolicy.Normalize(kind, target);
        SharedStateDocumentMutation.Mutate(
            _store,
            Key(kind),
            payload => Deserialize(kind, payload),
            state =>
            {
                if (state.Contains(normalized))
                {
                    return SharedStateDocumentMutation.MutationResult<HashSet<string>, bool>.Unchanged(state, false);
                }

                state.Add(normalized);
                GovernancePruneStatePolicy.ValidateBound(kind, state);
                return SharedStateDocumentMutation.MutationResult<HashSet<string>, bool>.Applied(state, true);
            },
            state => Serialize(kind, state));
    }

    public void Synchronize(EnterpriseOperatorSnapshot metadata, IEnumerable<GovernancePruneMarker> retainedLegacyMarkers)
    {
        ArgumentNullException.ThrowIfNull(retainedLegacyMarkers);
        var legacy = retainedLegacyMarkers.ToArray();
        var allowed = GovernancePruneStatePolicy.AllowedTargets(metadata);
        foreach (var kind in Enum.GetValues<GovernancePruneKind>())
        {
            SharedStateDocumentMutation.Mutate(
                _store,
                Key(kind),
                payload => Deserialize(kind, payload),
                state =>
                {
                    var candidate = GovernancePruneStatePolicy.MergeBounded(kind, state, legacy, allowed[kind]);
                    if (state.SetEquals(candidate))
                    {
                        return SharedStateDocumentMutation.MutationResult<HashSet<string>, bool>.Unchanged(state, false);
                    }

                    return SharedStateDocumentMutation.MutationResult<HashSet<string>, bool>.Applied(candidate, true);
                },
                state => Serialize(kind, state));
        }
    }

    private HashSet<string> Read(GovernancePruneKind kind) =>
        SharedStateDocumentMutation.ReadState(_store, Key(kind), payload => Deserialize(kind, payload));

    private static HashSet<string> Deserialize(GovernancePruneKind kind, string? payload)
    {
        if (payload is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<PruneEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared governance prune state is invalid.");
            if (envelope.Version != GovernancePruneStatePolicy.Version || envelope.Targets is null)
            {
                throw new InvalidDataException("Shared governance prune state format is not supported.");
            }

            var targets = envelope.Targets
                .Select(target => GovernancePruneStatePolicy.Normalize(kind, target))
                .ToHashSet(StringComparer.Ordinal);
            if (targets.Count != envelope.Targets.Length)
            {
                throw new InvalidDataException("Shared governance prune state contains duplicate targets.");
            }

            GovernancePruneStatePolicy.ValidateBound(kind, targets);
            return targets;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Shared governance prune state is corrupt.", exception);
        }
    }

    private static string Serialize(GovernancePruneKind kind, HashSet<string> targets)
    {
        GovernancePruneStatePolicy.ValidateBound(kind, targets);
        return JsonSerializer.Serialize(
            new PruneEnvelope(
                GovernancePruneStatePolicy.Version,
                targets.OrderBy(item => item, StringComparer.Ordinal).ToArray()),
            SharedStateDocumentMutation.JsonOptions);
    }

    private static string Key(GovernancePruneKind kind) =>
        $"{KeyPrefix}:{kind.ToString().ToLowerInvariant()}";

    private sealed record PruneEnvelope(int Version, string[]? Targets);
}

public static class GovernancePruneStateMigration
{
    private const int AuditScanLimit = 1000;
    private const int AuditPageSize = 100;

    public static void MaterializeRetainedAuditReceipts(
        IGovernancePruneStateStore store,
        IAuditStore audit,
        IOperatorMetadataStore metadata)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(metadata);

        var markers = new List<GovernancePruneMarker>();
        for (var offset = 0; offset < AuditScanLimit; offset += AuditPageSize)
        {
            var page = audit.Read(offset, AuditPageSize);
            foreach (var item in page)
            {
                if (item.Outcome == "applied" && TryKind(item.Action, out var kind))
                {
                    markers.Add(new GovernancePruneMarker(kind, item.Target));
                }
            }

            if (page.Count < AuditPageSize)
            {
                break;
            }
        }

        store.Synchronize(metadata.Snapshot(), markers);
    }

    public static IGovernancePruneStateStore CreateTransient(
        IAuditStore audit,
        IOperatorMetadataStore metadata)
    {
        var store = new InMemoryGovernancePruneStateStore();
        MaterializeRetainedAuditReceipts(store, audit, metadata);
        return store;
    }

    private static bool TryKind(string action, out GovernancePruneKind kind)
    {
        switch (action)
        {
            case "governance.prune.server":
                kind = GovernancePruneKind.Server;
                return true;
            case "governance.prune.incident":
                kind = GovernancePruneKind.Incident;
                return true;
            case "governance.prune.note":
                kind = GovernancePruneKind.Note;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}