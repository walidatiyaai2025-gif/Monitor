using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Monitor.Web.Services;

public enum IncidentNoteRequestState
{
    Armed,
    Applied
}

public sealed record IncidentNoteRequestReceipt(string Target, IncidentNoteRequestState State);

public interface IIncidentNoteRequestStateStore
{
    IncidentNoteClaimResult TryClaim(string receiptTarget);
    void MarkApplied(string receiptTarget);
    void MaterializeLegacy(IEnumerable<IncidentNoteRequestReceipt> receipts);
}

internal static class IncidentNoteRequestStatePolicy
{
    public const int FormatVersion = 1;
    public const int ShardCount = 64;
    public const int MaxEntriesPerShard = 512;
    public const int MaxTargetLength = 160;

    public static string Normalize(string receiptTarget)
    {
        if (string.IsNullOrWhiteSpace(receiptTarget))
            throw new ArgumentException("Incident-note receipt target is required.", nameof(receiptTarget));
        var normalized = receiptTarget.Trim();
        if (normalized.Length > MaxTargetLength || normalized.Any(char.IsControl))
            throw new ArgumentException("Incident-note receipt target is outside the bounded contract.", nameof(receiptTarget));
        return normalized;
    }

    public static int Shard(string receiptTarget)
    {
        var normalized = Normalize(receiptTarget);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return hash[0] & (ShardCount - 1);
    }

    public static Dictionary<string, IncidentNoteRequestState> Validate(
        IEnumerable<KeyValuePair<string, IncidentNoteRequestState>> entries,
        int shard)
    {
        var validated = new Dictionary<string, IncidentNoteRequestState>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!Enum.IsDefined(entry.Value))
                throw new InvalidDataException("Incident-note request state contains an unsupported status.");
            var target = Normalize(entry.Key);
            if (Shard(target) != shard || !validated.TryAdd(target, entry.Value))
                throw new InvalidDataException("Incident-note request state contains an invalid or duplicate target.");
        }

        if (validated.Count > MaxEntriesPerShard)
            throw new InvalidDataException("Incident-note request state exceeds its bounded shard capacity.");
        return validated;
    }

    public static IncidentNoteClaimResult TryClaim(
        Dictionary<string, IncidentNoteRequestState> state,
        string receiptTarget)
    {
        var target = Normalize(receiptTarget);
        if (state.TryGetValue(target, out var existing))
            return existing == IncidentNoteRequestState.Applied
                ? IncidentNoteClaimResult.AlreadyApplied
                : IncidentNoteClaimResult.Ambiguous;
        if (state.Count >= MaxEntriesPerShard)
            throw new InvalidDataException("Incident-note idempotency state is full; refusing an unprotected note mutation.");
        state.Add(target, IncidentNoteRequestState.Armed);
        return IncidentNoteClaimResult.Claimed;
    }

    public static bool MarkApplied(Dictionary<string, IncidentNoteRequestState> state, string receiptTarget)
    {
        var target = Normalize(receiptTarget);
        if (state.TryGetValue(target, out var existing))
        {
            if (existing == IncidentNoteRequestState.Applied) return false;
            state[target] = IncidentNoteRequestState.Applied;
            return true;
        }

        if (state.Count >= MaxEntriesPerShard)
            throw new InvalidDataException("Incident-note idempotency state is full; refusing to lose applied replay protection.");
        state.Add(target, IncidentNoteRequestState.Applied);
        return true;
    }

    public static bool Materialize(
        Dictionary<string, IncidentNoteRequestState> state,
        IEnumerable<IncidentNoteRequestReceipt> receipts,
        int shard)
    {
        var changed = false;
        foreach (var receipt in receipts)
        {
            if (!Enum.IsDefined(receipt.State)) continue;
            var target = Normalize(receipt.Target);
            if (Shard(target) != shard) continue;
            if (state.TryGetValue(target, out var existing))
            {
                if (existing == IncidentNoteRequestState.Armed && receipt.State == IncidentNoteRequestState.Applied)
                {
                    state[target] = IncidentNoteRequestState.Applied;
                    changed = true;
                }
                continue;
            }

            if (state.Count >= MaxEntriesPerShard)
                throw new InvalidDataException("Incident-note legacy migration exceeds bounded idempotency capacity.");
            state.Add(target, receipt.State);
            changed = true;
        }
        return changed;
    }
}

public sealed class InMemoryIncidentNoteRequestStateStore : IIncidentNoteRequestStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Dictionary<string, IncidentNoteRequestState>> _shards = [];

    public IncidentNoteClaimResult TryClaim(string receiptTarget)
    {
        lock (_gate)
        {
            return IncidentNoteRequestStatePolicy.TryClaim(GetShard(receiptTarget), receiptTarget);
        }
    }

    public void MarkApplied(string receiptTarget)
    {
        lock (_gate)
        {
            _ = IncidentNoteRequestStatePolicy.MarkApplied(GetShard(receiptTarget), receiptTarget);
        }
    }

    public void MaterializeLegacy(IEnumerable<IncidentNoteRequestReceipt> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        var snapshot = receipts.ToArray();
        lock (_gate)
        {
            for (var shard = 0; shard < IncidentNoteRequestStatePolicy.ShardCount; shard++)
                _ = IncidentNoteRequestStatePolicy.Materialize(GetShard(shard), snapshot, shard);
        }
    }

    private Dictionary<string, IncidentNoteRequestState> GetShard(string target) =>
        GetShard(IncidentNoteRequestStatePolicy.Shard(target));

    private Dictionary<string, IncidentNoteRequestState> GetShard(int shard)
    {
        if (!_shards.TryGetValue(shard, out var state))
        {
            state = new Dictionary<string, IncidentNoteRequestState>(StringComparer.Ordinal);
            _shards[shard] = state;
        }
        return state;
    }
}

public sealed class FileIncidentNoteRequestStateStore : IIncidentNoteRequestStateStore
{
    private readonly object _gate = new();
    private readonly string _rootPath;
    private readonly Dictionary<int, Dictionary<string, IncidentNoteRequestState>> _loaded = [];

    public FileIncidentNoteRequestStateStore(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Incident-note state root path is required.", nameof(rootPath));
        _rootPath = Path.GetFullPath(rootPath);
    }

    public IncidentNoteClaimResult TryClaim(string receiptTarget)
    {
        var shard = IncidentNoteRequestStatePolicy.Shard(receiptTarget);
        lock (_gate)
        {
            var state = Load(shard);
            var result = IncidentNoteRequestStatePolicy.TryClaim(state, receiptTarget);
            if (result == IncidentNoteClaimResult.Claimed) Persist(shard, state);
            return result;
        }
    }

    public void MarkApplied(string receiptTarget)
    {
        var shard = IncidentNoteRequestStatePolicy.Shard(receiptTarget);
        lock (_gate)
        {
            var state = Load(shard);
            if (IncidentNoteRequestStatePolicy.MarkApplied(state, receiptTarget)) Persist(shard, state);
        }
    }

    public void MaterializeLegacy(IEnumerable<IncidentNoteRequestReceipt> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        var snapshot = receipts.ToArray();
        lock (_gate)
        {
            for (var shard = 0; shard < IncidentNoteRequestStatePolicy.ShardCount; shard++)
            {
                var state = Load(shard);
                if (IncidentNoteRequestStatePolicy.Materialize(state, snapshot, shard)) Persist(shard, state);
            }
        }
    }

    private Dictionary<string, IncidentNoteRequestState> Load(int shard)
    {
        if (_loaded.TryGetValue(shard, out var state)) return state;
        var envelope = AtomicJsonFile.Load<Envelope>(PathFor(shard));
        if (envelope is null)
        {
            state = new Dictionary<string, IncidentNoteRequestState>(StringComparer.Ordinal);
        }
        else
        {
            if (envelope.Version != IncidentNoteRequestStatePolicy.FormatVersion || envelope.Entries is null)
                throw new InvalidDataException("Incident-note request state file format is not supported.");
            state = IncidentNoteRequestStatePolicy.Validate(
                envelope.Entries.Select(item => new KeyValuePair<string, IncidentNoteRequestState>(item.Target, item.State)),
                shard);
        }
        _loaded[shard] = state;
        return state;
    }

    private void Persist(int shard, Dictionary<string, IncidentNoteRequestState> state) =>
        AtomicJsonFile.Save(
            PathFor(shard),
            new Envelope(
                IncidentNoteRequestStatePolicy.FormatVersion,
                state.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new Entry(item.Key, item.Value))
                    .ToArray()));

    private string PathFor(int shard) => Path.Combine(_rootPath, $"incident-note-requests-{shard:D2}.json");
    private sealed record Envelope(int Version, Entry[]? Entries);
    private sealed record Entry(string Target, IncidentNoteRequestState State);
}

public sealed class SharedIncidentNoteRequestStateStore(ISharedStateDocumentStore store) : IIncidentNoteRequestStateStore
{
    private const string KeyPrefix = "monitor:incident-note-requests:v1";
    private readonly ISharedStateDocumentStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public IncidentNoteClaimResult TryClaim(string receiptTarget)
    {
        var shard = IncidentNoteRequestStatePolicy.Shard(receiptTarget);
        return SharedStateDocumentMutation.Mutate(
            _store,
            Key(shard),
            payload => Deserialize(payload, shard),
            state =>
            {
                var result = IncidentNoteRequestStatePolicy.TryClaim(state, receiptTarget);
                return result == IncidentNoteClaimResult.Claimed
                    ? SharedStateDocumentMutation.MutationResult<Dictionary<string, IncidentNoteRequestState>, IncidentNoteClaimResult>.Applied(state, result)
                    : SharedStateDocumentMutation.MutationResult<Dictionary<string, IncidentNoteRequestState>, IncidentNoteClaimResult>.Unchanged(state, result);
            },
            state => Serialize(state, shard));
    }

    public void MarkApplied(string receiptTarget)
    {
        var shard = IncidentNoteRequestStatePolicy.Shard(receiptTarget);
        SharedStateDocumentMutation.Mutate(
            _store,
            Key(shard),
            payload => Deserialize(payload, shard),
            state => IncidentNoteRequestStatePolicy.MarkApplied(state, receiptTarget)
                ? SharedStateDocumentMutation.MutationResult<Dictionary<string, IncidentNoteRequestState>, bool>.Applied(state, true)
                : SharedStateDocumentMutation.MutationResult<Dictionary<string, IncidentNoteRequestState>, bool>.Unchanged(state, false),
            state => Serialize(state, shard));
    }

    public void MaterializeLegacy(IEnumerable<IncidentNoteRequestReceipt> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        var snapshot = receipts.ToArray();
        for (var shard = 0; shard < IncidentNoteRequestStatePolicy.ShardCount; shard++)
        {
            var currentShard = shard;
            SharedStateDocumentMutation.Mutate(
                _store,
                Key(currentShard),
                payload => Deserialize(payload, currentShard),
                state => IncidentNoteRequestStatePolicy.Materialize(state, snapshot, currentShard)
                    ? SharedStateDocumentMutation.MutationResult<Dictionary<string, IncidentNoteRequestState>, bool>.Applied(state, true)
                    : SharedStateDocumentMutation.MutationResult<Dictionary<string, IncidentNoteRequestState>, bool>.Unchanged(state, false),
                state => Serialize(state, currentShard));
        }
    }

    private static Dictionary<string, IncidentNoteRequestState> Deserialize(string? payload, int shard)
    {
        if (payload is null) return new Dictionary<string, IncidentNoteRequestState>(StringComparer.Ordinal);
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared incident-note request state is invalid.");
            if (envelope.Version != IncidentNoteRequestStatePolicy.FormatVersion || envelope.Entries is null)
                throw new InvalidDataException("Shared incident-note request state format is not supported.");
            return IncidentNoteRequestStatePolicy.Validate(
                envelope.Entries.Select(item => new KeyValuePair<string, IncidentNoteRequestState>(item.Target, item.State)),
                shard);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Shared incident-note request state is corrupt.", exception);
        }
    }

    private static string Serialize(Dictionary<string, IncidentNoteRequestState> state, int shard)
    {
        _ = IncidentNoteRequestStatePolicy.Validate(state, shard);
        return JsonSerializer.Serialize(
            new Envelope(
                IncidentNoteRequestStatePolicy.FormatVersion,
                state.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new Entry(item.Key, item.Value))
                    .ToArray()),
            SharedStateDocumentMutation.JsonOptions);
    }

    private static string Key(int shard) => $"{KeyPrefix}:{shard:D2}";
    private sealed record Envelope(int Version, Entry[]? Entries);
    private sealed record Entry(string Target, IncidentNoteRequestState State);
}

public static class IncidentNoteRequestStateMigration
{
    private const int AuditScanLimit = 1000;
    private const int AuditPageSize = 100;

    public static void MaterializeRetainedAuditReceipts(IIncidentNoteRequestStateStore store, IAuditStore audit)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(audit);
        var byTarget = new Dictionary<string, IncidentNoteRequestState>(StringComparer.Ordinal);
        for (var offset = 0; offset < AuditScanLimit; offset += AuditPageSize)
        {
            var page = audit.Read(offset, AuditPageSize);
            foreach (var item in page)
            {
                if (item.Action == "incident.note.request" && item.Outcome == "applied")
                {
                    byTarget[item.Target] = IncidentNoteRequestState.Applied;
                }
                else if (item.Action == "incident.note.write.commit" && item.Outcome == "armed" && !byTarget.ContainsKey(item.Target))
                {
                    byTarget[item.Target] = IncidentNoteRequestState.Armed;
                }
            }
            if (page.Count < AuditPageSize) break;
        }

        store.MaterializeLegacy(byTarget.Select(item => new IncidentNoteRequestReceipt(item.Key, item.Value)));
    }
}
