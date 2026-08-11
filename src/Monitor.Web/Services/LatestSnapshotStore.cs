using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface ILatestSnapshotStore
{
    IReadOnlyList<ServerHealthSnapshot> LoadAll();
    void Upsert(ServerHealthSnapshot snapshot);
    void Remove(Guid registrationId);
}

public sealed class NullLatestSnapshotStore : ILatestSnapshotStore
{
    public static NullLatestSnapshotStore Instance { get; } = new();
    private NullLatestSnapshotStore() { }
    public IReadOnlyList<ServerHealthSnapshot> LoadAll() => [];
    public void Upsert(ServerHealthSnapshot snapshot) { }
    public void Remove(Guid registrationId) { }
}

public sealed class FileLatestSnapshotStore : ILatestSnapshotStore
{
    private const int CurrentFormatVersion = 1;
    private readonly object _gate = new();
    private readonly string _path;
    private Dictionary<Guid, ServerHealthSnapshot> _snapshots;

    public FileLatestSnapshotStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Latest snapshot store path is required.", nameof(path));
        _path = Path.GetFullPath(path);
        _snapshots = Load(_path);
    }

    public IReadOnlyList<ServerHealthSnapshot> LoadAll()
    {
        lock (_gate)
            return Ordered(_snapshots.Values).ToArray();
    }

    public void Upsert(ServerHealthSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);
        lock (_gate)
        {
            if (_snapshots.TryGetValue(snapshot.RegistrationId, out var current) &&
                current.CollectedAtUtc >= snapshot.CollectedAtUtc)
                return;

            var candidate = new Dictionary<Guid, ServerHealthSnapshot>(_snapshots)
            {
                [snapshot.RegistrationId] = snapshot
            };
            Persist(candidate);
            _snapshots = candidate;
        }
    }

    public void Remove(Guid registrationId)
    {
        lock (_gate)
        {
            if (!_snapshots.ContainsKey(registrationId)) return;
            var candidate = new Dictionary<Guid, ServerHealthSnapshot>(_snapshots);
            candidate.Remove(registrationId);
            Persist(candidate);
            _snapshots = candidate;
        }
    }

    private void Persist(Dictionary<Guid, ServerHealthSnapshot> state) =>
        AtomicJsonFile.Save(
            _path,
            new LatestSnapshotEnvelope(CurrentFormatVersion, Ordered(state.Values).ToArray()));

    private static Dictionary<Guid, ServerHealthSnapshot> Load(string path)
    {
        var envelope = AtomicJsonFile.Load<LatestSnapshotEnvelope>(path);
        if (envelope is null) return [];
        if (envelope.Version != CurrentFormatVersion)
            throw new InvalidDataException("Latest snapshot store format version is not supported.");

        var result = new Dictionary<Guid, ServerHealthSnapshot>();
        foreach (var snapshot in envelope.Snapshots ?? [])
        {
            Validate(snapshot);
            if (!result.TryAdd(snapshot.RegistrationId, snapshot))
                throw new InvalidDataException("Latest snapshot store contains duplicate registration IDs.");
        }
        return result;
    }

    private static IOrderedEnumerable<ServerHealthSnapshot> Ordered(IEnumerable<ServerHealthSnapshot> snapshots) =>
        snapshots.OrderBy(item => item.RegistrationId);

    internal static void Validate(ServerHealthSnapshot snapshot)
    {
        if (snapshot.RegistrationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(snapshot.ServerName) ||
            string.IsNullOrWhiteSpace(snapshot.ProductVersion) ||
            string.IsNullOrWhiteSpace(snapshot.Edition) ||
            snapshot.CollectedAtUtc == default ||
            snapshot.UptimeSeconds < 0 ||
            snapshot.DatabaseTotal < 0 ||
            snapshot.DatabaseOnline < 0 ||
            snapshot.DatabaseOnline > snapshot.DatabaseTotal)
            throw new InvalidDataException("Latest snapshot store contains invalid snapshot metadata.");
    }

    private sealed record LatestSnapshotEnvelope(int Version, ServerHealthSnapshot[]? Snapshots);
}

public sealed class SharedLatestSnapshotStore(ISharedStateDocumentStore store) : ILatestSnapshotStore
{
    private const string DocumentKey = "monitor:latest-snapshots:v1";
    private const int CurrentFormatVersion = 1;

    public IReadOnlyList<ServerHealthSnapshot> LoadAll() =>
        ReadState().Values.OrderBy(item => item.RegistrationId).ToArray();

    public void Upsert(ServerHealthSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        FileLatestSnapshotStore.Validate(snapshot);
        SharedStateDocumentMutation.Mutate(
            store,
            DocumentKey,
            Deserialize,
            state =>
            {
                if (state.TryGetValue(snapshot.RegistrationId, out var current) &&
                    current.CollectedAtUtc >= snapshot.CollectedAtUtc)
                    return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerHealthSnapshot>, bool>.Unchanged(state, false);
                state[snapshot.RegistrationId] = snapshot;
                return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerHealthSnapshot>, bool>.Applied(state, true);
            },
            Serialize);
    }

    public void Remove(Guid registrationId) =>
        SharedStateDocumentMutation.Mutate(
            store,
            DocumentKey,
            Deserialize,
            state =>
            {
                if (!state.Remove(registrationId))
                    return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerHealthSnapshot>, bool>.Unchanged(state, false);
                return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerHealthSnapshot>, bool>.Applied(state, true);
            },
            Serialize);

    private Dictionary<Guid, ServerHealthSnapshot> ReadState() =>
        SharedStateDocumentMutation.ReadState(store, DocumentKey, Deserialize);

    private static Dictionary<Guid, ServerHealthSnapshot> Deserialize(string? payload)
    {
        if (payload is null) return [];
        try
        {
            var envelope = JsonSerializer.Deserialize<SharedEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared latest snapshot state is empty or invalid.");
            if (envelope.Version != CurrentFormatVersion)
                throw new InvalidDataException("Shared latest snapshot format version is not supported.");
            var result = new Dictionary<Guid, ServerHealthSnapshot>();
            foreach (var snapshot in envelope.Snapshots ?? [])
            {
                FileLatestSnapshotStore.Validate(snapshot);
                if (!result.TryAdd(snapshot.RegistrationId, snapshot))
                    throw new InvalidDataException("Shared latest snapshot state contains duplicate registration IDs.");
            }
            return result;
        }
        catch (InvalidDataException) { throw; }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared latest snapshot state is invalid.", exception);
        }
    }

    private static string Serialize(Dictionary<Guid, ServerHealthSnapshot> state) =>
        JsonSerializer.Serialize(
            new SharedEnvelope(CurrentFormatVersion, state.Values.OrderBy(item => item.RegistrationId).ToArray()),
            SharedStateDocumentMutation.JsonOptions);

    private sealed record SharedEnvelope(int Version, ServerHealthSnapshot[]? Snapshots);
}