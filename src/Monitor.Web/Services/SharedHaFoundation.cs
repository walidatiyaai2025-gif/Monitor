using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed class SharedStateConcurrencyException : Exception
{
    public SharedStateConcurrencyException()
        : base("Shared state changed concurrently. Retry the operation.")
    {
    }
}

internal static class SharedStateDocumentMutation
{
    internal const int DefaultMaxAttempts = 12;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static SharedStateDocument? Read(ISharedStateDocumentStore store, string key) =>
        store.ReadAsync(key).ConfigureAwait(false).GetAwaiter().GetResult();

    public static TState ReadState<TState>(
        ISharedStateDocumentStore store,
        string key,
        Func<string?, TState> deserialize) =>
        deserialize(Read(store, key)?.PayloadJson);

    public static TResult Mutate<TState, TResult>(
        ISharedStateDocumentStore store,
        string key,
        Func<string?, TState> deserialize,
        Func<TState, MutationResult<TState, TResult>> mutate,
        Func<TState, string> serialize,
        int maxAttempts = DefaultMaxAttempts)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var current = Read(store, key);
            var state = deserialize(current?.PayloadJson);
            var mutation = mutate(state);
            if (!mutation.Changed)
            {
                return mutation.Result;
            }

            var write = store.CompareExchangeAsync(
                    key,
                    current?.Version ?? 0,
                    serialize(mutation.State))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (write.Applied)
            {
                return mutation.Result;
            }
        }

        throw new SharedStateConcurrencyException();
    }

    public sealed record MutationResult<TState, TResult>(
        TState State,
        TResult Result,
        bool Changed)
    {
        public static MutationResult<TState, TResult> Applied(TState state, TResult result) =>
            new(state, result, true);

        public static MutationResult<TState, TResult> Unchanged(TState state, TResult result) =>
            new(state, result, false);
    }
}

public sealed class SharedServerRegistrationRepository : IServerRegistrationRepository
{
    private const string DocumentKey = "monitor:registrations:v1";
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;

    public SharedServerRegistrationRepository(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public IReadOnlyList<ServerRegistration> GetAll() =>
        Ordered(ReadState().Values).ToArray();

    public ServerRegistration? GetById(Guid id)
    {
        var state = ReadState();
        return state.TryGetValue(id, out var registration) ? registration : null;
    }

    public void Upsert(ServerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ValidateRegistration(registration);

        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                state[registration.Id] = registration;
                return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, bool>.Applied(state, true);
            },
            Serialize);
    }

    public bool Remove(Guid id) =>
        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                if (!state.Remove(id))
                {
                    return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, bool>.Unchanged(state, false);
                }

                return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, bool>.Applied(state, true);
            },
            Serialize);

    public bool ImportIfEmpty(IEnumerable<ServerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var imported = registrations.ToArray();
        foreach (var registration in imported)
        {
            ValidateRegistration(registration);
        }

        if (imported.GroupBy(item => item.Id).Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Registration import contains duplicate registration IDs.");
        }

        if (imported.Length == 0)
        {
            return false;
        }

        return SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                if (state.Count != 0)
                {
                    return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, bool>.Unchanged(state, false);
                }

                foreach (var registration in imported)
                {
                    state.Add(registration.Id, registration);
                }

                return SharedStateDocumentMutation.MutationResult<Dictionary<Guid, ServerRegistration>, bool>.Applied(state, true);
            },
            Serialize);
    }

    private Dictionary<Guid, ServerRegistration> ReadState() =>
        SharedStateDocumentMutation.ReadState(_store, DocumentKey, Deserialize);

    private static Dictionary<Guid, ServerRegistration> Deserialize(string? payload)
    {
        if (payload is null)
        {
            return [];
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<RegistrationEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared registration state is empty or invalid.");
            if (envelope.Version != FormatVersion || envelope.Registrations is null)
            {
                throw new InvalidDataException("Shared registration state format is not supported.");
            }

            var state = new Dictionary<Guid, ServerRegistration>();
            foreach (var item in envelope.Registrations)
            {
                var registration = item.ToDomain();
                ValidateRegistration(registration);
                if (!state.TryAdd(registration.Id, registration))
                {
                    throw new InvalidDataException("Shared registration state contains duplicate IDs.");
                }
            }

            return state;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("Shared registration state is corrupt.", exception);
        }
    }

    private static string Serialize(Dictionary<Guid, ServerRegistration> state)
    {
        var envelope = new RegistrationEnvelope(
            FormatVersion,
            Ordered(state.Values).Select(PersistedRegistration.FromDomain).ToArray());
        return JsonSerializer.Serialize(envelope, SharedStateDocumentMutation.JsonOptions);
    }

    private static IOrderedEnumerable<ServerRegistration> Ordered(IEnumerable<ServerRegistration> registrations) =>
        registrations.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id);

    private static void ValidateRegistration(ServerRegistration registration)
    {
        if (registration.Id == Guid.Empty || string.IsNullOrWhiteSpace(registration.DisplayName) ||
            registration.CreatedAtUtc == default)
        {
            throw new InvalidDataException("Shared registration is outside the allowed metadata contract.");
        }

        if (registration.AuthenticationMode == SqlAuthenticationMode.SqlLogin &&
            registration.SecretReference is null)
        {
            throw new InvalidDataException("SQL Login registration is missing its opaque secret reference.");
        }
    }

    private sealed record RegistrationEnvelope(int Version, PersistedRegistration[]? Registrations);

    private sealed record PersistedRegistration(
        Guid Id,
        string DisplayName,
        string Host,
        int? Port,
        string? InstanceName,
        bool Encrypt,
        bool TrustServerCertificate,
        SqlAuthenticationMode AuthenticationMode,
        string? SecretReference,
        bool IsEnabled,
        DateTimeOffset CreatedAtUtc)
    {
        public static PersistedRegistration FromDomain(ServerRegistration registration) =>
            new(
                registration.Id,
                registration.DisplayName,
                registration.Endpoint.Host,
                registration.Endpoint.Port,
                registration.Endpoint.InstanceName,
                registration.Endpoint.Encrypt,
                registration.Endpoint.TrustServerCertificate,
                registration.AuthenticationMode,
                registration.SecretReference?.Value,
                registration.IsEnabled,
                registration.CreatedAtUtc);

        public ServerRegistration ToDomain()
        {
            var reference = string.IsNullOrWhiteSpace(SecretReference)
                ? (ConnectionSecretReference?)null
                : new ConnectionSecretReference(SecretReference);

            return new ServerRegistration(
                Id,
                DisplayName,
                new SqlServerEndpoint(Host, Port, InstanceName, Encrypt, TrustServerCertificate),
                AuthenticationMode,
                reference,
                IsEnabled,
                CreatedAtUtc);
        }
    }
}

public sealed class SharedAuditStore : IAuditStore
{
    private const string DocumentKey = "monitor:audit:v1";
    private const int FormatVersion = 1;
    private const int MaxEvents = 1000;
    private readonly ISharedStateDocumentStore _store;
    private readonly TimeProvider _timeProvider;

    public SharedAuditStore(ISharedStateDocumentStore store, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public void Append(string actor, string action, string target, string outcome)
    {
        static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
        var item = new AuditEvent(
            Guid.NewGuid(),
            _timeProvider.GetUtcNow(),
            Bound(actor ?? string.Empty, 100),
            Bound(action ?? string.Empty, 80),
            Bound(target ?? string.Empty, 160),
            Bound(outcome ?? string.Empty, 40));

        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                state.Add(item);
                if (state.Count > MaxEvents)
                {
                    state.RemoveRange(0, state.Count - MaxEvents);
                }

                return SharedStateDocumentMutation.MutationResult<List<AuditEvent>, bool>.Applied(state, true);
            },
            Serialize);
    }

    public IReadOnlyList<AuditEvent> Read(int offset, int limit) =>
        SharedStateDocumentMutation.ReadState(_store, DocumentKey, Deserialize)
            .OrderByDescending(item => item.OccurredAtUtc)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 100))
            .ToArray();

    private static List<AuditEvent> Deserialize(string? payload)
    {
        if (payload is null)
        {
            return [];
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<AuditEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared audit state is invalid.");
            if (envelope.Version != FormatVersion || envelope.Events is null || envelope.Events.Length > MaxEvents)
            {
                throw new InvalidDataException("Shared audit state format or event count is invalid.");
            }

            var ids = new HashSet<Guid>();
            foreach (var item in envelope.Events)
            {
                if (item.Id == Guid.Empty || !ids.Add(item.Id) || item.OccurredAtUtc == default ||
                    item.Actor.Length > 100 || item.Action.Length > 80 || item.Target.Length > 160 ||
                    item.Outcome.Length > 40)
                {
                    throw new InvalidDataException("Shared audit state contains invalid bounded metadata.");
                }
            }

            return envelope.Events.OrderBy(item => item.OccurredAtUtc).ToList();
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared audit state is corrupt.", exception);
        }
    }

    private static string Serialize(List<AuditEvent> state) =>
        JsonSerializer.Serialize(
            new AuditEnvelope(FormatVersion, state.OrderBy(item => item.OccurredAtUtc).ToArray()),
            SharedStateDocumentMutation.JsonOptions);

    private sealed record AuditEnvelope(int Version, AuditEvent[]? Events);
}

public sealed class SharedSnapshotHistoryStore : ISnapshotHistoryStore
{
    private const int FormatVersion = 1;
    private const int MaxPerServer = 288;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private readonly ISharedStateDocumentStore _store;
    private readonly TimeProvider _timeProvider;

    public SharedSnapshotHistoryStore(ISharedStateDocumentStore store, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public void Append(SnapshotCacheResult result)
    {
        var snapshot = result.Snapshot;
        var point = new SnapshotHistoryPoint(
            snapshot.RegistrationId,
            snapshot.CollectedAtUtc,
            snapshot.DatabaseOnline,
            snapshot.DatabaseTotal,
            snapshot.Memory?.SqlProcessMemoryUtilizationPercent,
            snapshot.Blocking?.BlockedRequests,
            snapshot.Performance?.RunnableTasks,
            result.Freshness);
        var key = Key(snapshot.RegistrationId);

        SharedStateDocumentMutation.Mutate(
            _store,
            key,
            payload => Deserialize(snapshot.RegistrationId, payload),
            state =>
            {
                var cutoff = _timeProvider.GetUtcNow() - Retention;
                state = state.Where(item => item.CollectedAtUtc >= cutoff).ToList();
                if (!state.Any(item => item.CollectedAtUtc == point.CollectedAtUtc))
                {
                    state.Add(point);
                }

                state = state.OrderBy(item => item.CollectedAtUtc).TakeLast(MaxPerServer).ToList();
                return SharedStateDocumentMutation.MutationResult<List<SnapshotHistoryPoint>, bool>.Applied(state, true);
            },
            state => Serialize(snapshot.RegistrationId, state));
    }

    public IReadOnlyList<SnapshotHistoryPoint> Read(Guid registrationId, TimeSpan window)
    {
        if (registrationId == Guid.Empty)
        {
            return [];
        }

        var cutoff = _timeProvider.GetUtcNow() - window;
        return SharedStateDocumentMutation.ReadState(
                _store,
                Key(registrationId),
                payload => Deserialize(registrationId, payload))
            .Where(item => item.CollectedAtUtc >= cutoff)
            .OrderBy(item => item.CollectedAtUtc)
            .ToArray();
    }

    private static string Key(Guid registrationId) => $"monitor:history:v1:{registrationId:N}";

    private static List<SnapshotHistoryPoint> Deserialize(Guid registrationId, string? payload)
    {
        if (payload is null)
        {
            return [];
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<HistoryEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared history state is invalid.");
            if (envelope.Version != FormatVersion || envelope.RegistrationId != registrationId ||
                envelope.Points is null || envelope.Points.Length > MaxPerServer)
            {
                throw new InvalidDataException("Shared history state format is invalid.");
            }

            var timestamps = new HashSet<DateTimeOffset>();
            foreach (var point in envelope.Points)
            {
                if (point.RegistrationId != registrationId || point.CollectedAtUtc == default ||
                    point.DatabaseTotal < 0 || point.DatabaseOnline < 0 || point.DatabaseOnline > point.DatabaseTotal ||
                    point.MemoryPercent is < 0 or > 100 || point.BlockedRequests is < 0 || point.RunnableTasks is < 0 ||
                    !Enum.IsDefined(point.Freshness) || !timestamps.Add(point.CollectedAtUtc))
                {
                    throw new InvalidDataException("Shared history state contains invalid aggregate data.");
                }
            }

            return envelope.Points.OrderBy(item => item.CollectedAtUtc).ToList();
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared history state is corrupt.", exception);
        }
    }

    private static string Serialize(Guid registrationId, List<SnapshotHistoryPoint> state) =>
        JsonSerializer.Serialize(
            new HistoryEnvelope(FormatVersion, registrationId, state.OrderBy(item => item.CollectedAtUtc).ToArray()),
            SharedStateDocumentMutation.JsonOptions);

    private sealed record HistoryEnvelope(int Version, Guid RegistrationId, SnapshotHistoryPoint[]? Points);
}

public sealed class SharedHealthIncidentRepository : IHealthIncidentRepository
{
    private const string DocumentKey = "monitor:incidents:v1";
    private const int FormatVersion = 1;
    private const int MaxRuleIdLength = 80;
    private const int MaxTitleLength = 160;
    private const int MaxEvidenceLength = 500;
    private readonly ISharedStateDocumentStore _store;

    public SharedHealthIncidentRepository(ISharedStateDocumentStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public void Apply(IEnumerable<HealthFinding> findings)
    {
        var materialized = findings.ToArray();
        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                ApplyTo(state, materialized);
                return SharedStateDocumentMutation.MutationResult<Dictionary<string, HealthIncident>, bool>.Applied(state, true);
            },
            Serialize);
    }

    public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve)
    {
        var active = activeFindings.ToArray();
        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                ApplyTo(state, active);
                if (canResolve)
                {
                    var activeRules = active.Select(item => item.RuleId).ToHashSet(StringComparer.Ordinal);
                    foreach (var pair in state.Where(pair => pair.Value.RegistrationId == registrationId && pair.Value.Status != IncidentStatus.Resolved).ToArray())
                    {
                        if (!activeRules.Contains(pair.Value.RuleId) && observedAtUtc >= pair.Value.LastSeenUtc)
                        {
                            state[pair.Key] = pair.Value with { Status = IncidentStatus.Resolved, LastSeenUtc = observedAtUtc };
                        }
                    }
                }

                return SharedStateDocumentMutation.MutationResult<Dictionary<string, HealthIncident>, bool>.Applied(state, true);
            },
            Serialize);
    }

    public IReadOnlyList<HealthIncident> GetAll() =>
        ReadState().Values.OrderByDescending(item => item.Severity).ThenByDescending(item => item.LastSeenUtc).ToArray();

    public HealthIncident? GetById(string id)
    {
        var state = ReadState();
        return state.TryGetValue(id, out var value) ? value : null;
    }

    public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) =>
        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            state =>
            {
                if (!state.TryGetValue(id, out var current) || current.Status != expected)
                {
                    return SharedStateDocumentMutation.MutationResult<Dictionary<string, HealthIncident>, bool>.Unchanged(state, false);
                }

                state[id] = current with { Status = next };
                return SharedStateDocumentMutation.MutationResult<Dictionary<string, HealthIncident>, bool>.Applied(state, true);
            },
            Serialize);

    private Dictionary<string, HealthIncident> ReadState() => SharedStateDocumentMutation.ReadState(_store, DocumentKey, Deserialize);

    private static void ApplyTo(Dictionary<string, HealthIncident> state, IEnumerable<HealthFinding> findings)
    {
        foreach (var finding in findings)
        {
            ValidateFinding(finding);
            var id = $"{finding.RegistrationId:N}:{finding.RuleId}";
            if (!state.TryGetValue(id, out var current))
            {
                state[id] = new HealthIncident(id, finding.RegistrationId, finding.RuleId, finding.Severity, finding.Title, finding.Evidence, finding.ObservedAtUtc, finding.ObservedAtUtc, 1, IncidentStatus.Open);
                continue;
            }

            if (finding.ObservedAtUtc <= current.LastSeenUtc)
            {
                continue;
            }

            state[id] = current with
            {
                Severity = finding.Severity,
                Title = finding.Title,
                Evidence = finding.Evidence,
                LastSeenUtc = finding.ObservedAtUtc,
                Occurrences = current.Occurrences + 1,
                Status = current.Status == IncidentStatus.Acknowledged ? IncidentStatus.Acknowledged : IncidentStatus.Open
            };
        }
    }

    private static Dictionary<string, HealthIncident> Deserialize(string? payload)
    {
        if (payload is null)
        {
            return new Dictionary<string, HealthIncident>(StringComparer.Ordinal);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<IncidentEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared incident state is invalid.");
            if (envelope.Version != FormatVersion || envelope.Incidents is null)
            {
                throw new InvalidDataException("Shared incident state format is invalid.");
            }

            var state = new Dictionary<string, HealthIncident>(StringComparer.Ordinal);
            foreach (var incident in envelope.Incidents)
            {
                ValidateIncident(incident);
                if (!state.TryAdd(incident.Id, incident))
                {
                    throw new InvalidDataException("Shared incident state contains duplicate IDs.");
                }
            }

            return state;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared incident state is corrupt.", exception);
        }
    }

    private static string Serialize(Dictionary<string, HealthIncident> state)
    {
        foreach (var incident in state.Values)
        {
            ValidateIncident(incident);
        }

        return JsonSerializer.Serialize(new IncidentEnvelope(FormatVersion, state.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray()), SharedStateDocumentMutation.JsonOptions);
    }

    private static void ValidateFinding(HealthFinding finding)
    {
        if (finding.RegistrationId == Guid.Empty || finding.ObservedAtUtc == default ||
            string.IsNullOrWhiteSpace(finding.RuleId) || finding.RuleId.Length > MaxRuleIdLength ||
            string.IsNullOrWhiteSpace(finding.Title) || finding.Title.Length > MaxTitleLength ||
            finding.Evidence.Length > MaxEvidenceLength || !Enum.IsDefined(finding.Severity))
        {
            throw new InvalidDataException("Finding is outside the shared incident bounds.");
        }
    }

    private static void ValidateIncident(HealthIncident incident)
    {
        var expectedId = $"{incident.RegistrationId:N}:{incident.RuleId}";
        if (incident.RegistrationId == Guid.Empty || incident.FirstSeenUtc == default || incident.LastSeenUtc == default ||
            incident.FirstSeenUtc > incident.LastSeenUtc || incident.Occurrences < 1 || string.IsNullOrWhiteSpace(incident.RuleId) ||
            incident.RuleId.Length > MaxRuleIdLength || string.IsNullOrWhiteSpace(incident.Title) || incident.Title.Length > MaxTitleLength ||
            incident.Evidence.Length > MaxEvidenceLength || !string.Equals(incident.Id, expectedId, StringComparison.Ordinal) ||
            !Enum.IsDefined(incident.Severity) || !Enum.IsDefined(incident.Status))
        {
            throw new InvalidDataException("Shared incident state contains invalid bounded metadata.");
        }
    }

    private sealed record IncidentEnvelope(int Version, HealthIncident[]? Incidents);
}

public sealed class SharedSchedulerStatusStore : ISchedulerStatusStore
{
    private const string DocumentKey = "monitor:scheduler-status:v1";
    private const int FormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;

    public SharedSchedulerStatusStore(ISharedStateDocumentStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    public SchedulerStatus Get()
    {
        var document = SharedStateDocumentMutation.Read(_store, DocumentKey);
        if (document is null)
        {
            return Empty();
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<SchedulerEnvelope>(document.PayloadJson, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared scheduler status is invalid.");
            if (envelope.Version != FormatVersion)
            {
                throw new InvalidDataException("Shared scheduler status version is not supported.");
            }

            Validate(envelope.Status);
            return envelope.Status;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared scheduler status is corrupt.", exception);
        }
    }

    public void Set(SchedulerStatus value)
    {
        Validate(value);
        SharedStateDocumentMutation.Mutate(
            _store,
            DocumentKey,
            Deserialize,
            _ => SharedStateDocumentMutation.MutationResult<SchedulerStatus, bool>.Applied(value, true),
            Serialize);
    }

    private static SchedulerStatus Deserialize(string? payload)
    {
        if (payload is null)
        {
            return Empty();
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<SchedulerEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared scheduler status is invalid.");
            if (envelope.Version != FormatVersion)
            {
                throw new InvalidDataException("Shared scheduler status version is not supported.");
            }

            Validate(envelope.Status);
            return envelope.Status;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared scheduler status is corrupt.", exception);
        }
    }

    private static string Serialize(SchedulerStatus value) => JsonSerializer.Serialize(new SchedulerEnvelope(FormatVersion, value), SharedStateDocumentMutation.JsonOptions);
    private static SchedulerStatus Empty() => new(false, false, null, null, 0, 0, 0, 0);

    private static void Validate(SchedulerStatus value)
    {
        if (value.Attempted < 0 || value.Succeeded < 0 || value.Failed < 0 || value.SkippedBackoff < 0 ||
            value.Succeeded + value.Failed + value.SkippedBackoff > value.Attempted)
        {
            throw new InvalidDataException("Shared scheduler status is outside the allowed bounds.");
        }
    }

    private sealed record SchedulerEnvelope(int Version, SchedulerStatus Status);
}

public sealed class HaStateOptions
{
    public const string SectionName = "HaState";
    public bool UseSharedRegistrations { get; set; }
    public bool ImportLocalRegistrationsWhenSharedEmpty { get; set; }
    public bool UseSharedOperationalState { get; set; }

    public void Validate()
    {
        if (ImportLocalRegistrationsWhenSharedEmpty && !UseSharedRegistrations)
        {
            throw new InvalidOperationException("HaState:ImportLocalRegistrationsWhenSharedEmpty requires UseSharedRegistrations.");
        }
    }
}

public sealed class DistributedCoordinationOptions
{
    public const string SectionName = "Coordination";
    public bool Enabled { get; set; }
    public string NodeIdEnvironmentVariable { get; set; } = "MONITOR_NODE_ID";
    public int SchedulerLeaseSeconds { get; set; } = 90;
    public int RefreshLeaseSeconds { get; set; } = 30;
    public int MaxConflictRetries { get; set; } = 12;

    public void Validate()
    {
        if (SchedulerLeaseSeconds is < 30 or > 600)
        {
            throw new InvalidOperationException("Coordination:SchedulerLeaseSeconds must be between 30 and 600.");
        }

        if (RefreshLeaseSeconds is < 15 or > 120)
        {
            throw new InvalidOperationException("Coordination:RefreshLeaseSeconds must be between 15 and 120.");
        }

        if (MaxConflictRetries is < 1 or > 32)
        {
            throw new InvalidOperationException("Coordination:MaxConflictRetries must be between 1 and 32.");
        }

        if (!IsSafeEnvironmentVariableName(NodeIdEnvironmentVariable))
        {
            throw new InvalidOperationException("Coordination:NodeIdEnvironmentVariable is invalid.");
        }
    }

    private static bool IsSafeEnvironmentVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        if (!char.IsAsciiLetter(value[0]) && value[0] != '_')
        {
            return false;
        }

        return value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    }
}

public sealed record NodeIdentity(string Value)
{
    public static NodeIdentity Resolve(DistributedCoordinationOptions options, Func<string, string?>? environmentReader = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (!options.Enabled)
        {
            return new NodeIdentity("single-node");
        }

        var read = environmentReader ?? Environment.GetEnvironmentVariable;
        var value = read(options.NodeIdEnvironmentVariable)?.Trim();
        if (!IsSafeNodeId(value))
        {
            throw new InvalidOperationException("Coordination node identity is missing or invalid.");
        }

        return new NodeIdentity(value!);
    }

    private static bool IsSafeNodeId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    public override string ToString() => "[node]";
}

public sealed record DistributedLeaseHandle(string Resource, string OwnerId, long Version, TimeSpan Duration, DateTimeOffset ExpiresAtUtc);

public interface IDistributedLeaseManager
{
    Task<DistributedLeaseHandle?> TryAcquireAsync(string resource, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<DistributedLeaseHandle?> RenewAsync(DistributedLeaseHandle lease, CancellationToken cancellationToken = default);
    Task<bool> ReleaseAsync(DistributedLeaseHandle lease, CancellationToken cancellationToken = default);
}

public sealed class SharedStateDistributedLeaseManager : IDistributedLeaseManager
{
    private const int LeaseFormatVersion = 1;
    private readonly ISharedStateDocumentStore _store;
    private readonly NodeIdentity _nodeIdentity;
    private readonly TimeProvider _timeProvider;
    private readonly DistributedCoordinationOptions _options;

    public SharedStateDistributedLeaseManager(ISharedStateDocumentStore store, NodeIdentity nodeIdentity, TimeProvider timeProvider, DistributedCoordinationOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _nodeIdentity = nodeIdentity ?? throw new ArgumentNullException(nameof(nodeIdentity));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task<DistributedLeaseHandle?> TryAcquireAsync(string resource, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var key = LeaseKey(resource);
        ValidateDuration(duration);

        for (var attempt = 0; attempt < _options.MaxConflictRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await _store.ReadAsync(key, cancellationToken);
            if (current is not null)
            {
                var existing = DeserializeLease(current.PayloadJson);
                var expiresAt = current.UpdatedAtUtc.AddSeconds(existing.DurationSeconds);
                if (!existing.Released && !string.Equals(existing.OwnerId, _nodeIdentity.Value, StringComparison.Ordinal) && _timeProvider.GetUtcNow() < expiresAt)
                {
                    return null;
                }
            }

            var payload = SerializeLease(new LeaseEnvelope(LeaseFormatVersion, _nodeIdentity.Value, checked((int)duration.TotalSeconds), Released: false));
            var write = await _store.CompareExchangeAsync(key, current?.Version ?? 0, payload, cancellationToken);
            if (write.Applied && write.Document is not null)
            {
                return new DistributedLeaseHandle(resource, _nodeIdentity.Value, write.Document.Version, duration, write.Document.UpdatedAtUtc.Add(duration));
            }
        }

        return null;
    }

    public async Task<DistributedLeaseHandle?> RenewAsync(DistributedLeaseHandle lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (!string.Equals(lease.OwnerId, _nodeIdentity.Value, StringComparison.Ordinal))
        {
            return null;
        }

        ValidateDuration(lease.Duration);
        var payload = SerializeLease(new LeaseEnvelope(LeaseFormatVersion, _nodeIdentity.Value, checked((int)lease.Duration.TotalSeconds), Released: false));
        var write = await _store.CompareExchangeAsync(LeaseKey(lease.Resource), lease.Version, payload, cancellationToken);
        return write.Applied && write.Document is not null
            ? lease with { Version = write.Document.Version, ExpiresAtUtc = write.Document.UpdatedAtUtc.Add(lease.Duration) }
            : null;
    }

    public async Task<bool> ReleaseAsync(DistributedLeaseHandle lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (!string.Equals(lease.OwnerId, _nodeIdentity.Value, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = SerializeLease(new LeaseEnvelope(LeaseFormatVersion, _nodeIdentity.Value, checked((int)lease.Duration.TotalSeconds), Released: true));
        var write = await _store.CompareExchangeAsync(LeaseKey(lease.Resource), lease.Version, payload, cancellationToken);
        return write.Applied;
    }

    private static string LeaseKey(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource) || resource.Length > 80 ||
            resource.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ':' and not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("Distributed lease resource is invalid.", nameof(resource));
        }

        return $"monitor:lease:v1:{resource}";
    }

    private static void ValidateDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(10) || duration > TimeSpan.FromMinutes(10) || duration.TotalSeconds != Math.Truncate(duration.TotalSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }
    }

    private static LeaseEnvelope DeserializeLease(string payload)
    {
        try
        {
            var lease = JsonSerializer.Deserialize<LeaseEnvelope>(payload, SharedStateDocumentMutation.JsonOptions)
                ?? throw new InvalidDataException("Shared lease is invalid.");
            if (lease.Version != LeaseFormatVersion || !NodeIdIsSafe(lease.OwnerId) || lease.DurationSeconds is < 10 or > 600)
            {
                throw new InvalidDataException("Shared lease is outside the allowed contract.");
            }

            return lease;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Shared lease is corrupt.", exception);
        }
    }

    private static string SerializeLease(LeaseEnvelope lease) => JsonSerializer.Serialize(lease, SharedStateDocumentMutation.JsonOptions);
    private static bool NodeIdIsSafe(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    private sealed record LeaseEnvelope(int Version, string OwnerId, int DurationSeconds, bool Released);
}

public sealed class DisabledDistributedLeaseManager : IDistributedLeaseManager
{
    public Task<DistributedLeaseHandle?> TryAcquireAsync(string resource, TimeSpan duration, CancellationToken cancellationToken = default) => Task.FromResult<DistributedLeaseHandle?>(null);
    public Task<DistributedLeaseHandle?> RenewAsync(DistributedLeaseHandle lease, CancellationToken cancellationToken = default) => Task.FromResult<DistributedLeaseHandle?>(null);
    public Task<bool> ReleaseAsync(DistributedLeaseHandle lease, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

public static class DeploymentReadinessEvaluator
{
    public static DeploymentReadinessViewModel Evaluate(DeploymentTopologyOptions deployment, SharedStateOptions sharedState, HaStateOptions haState, DistributedCoordinationOptions coordination)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(sharedState);
        ArgumentNullException.ThrowIfNull(haState);
        ArgumentNullException.ThrowIfNull(coordination);

        var nodeLocal = new List<string>();
        if (!haState.UseSharedRegistrations)
        {
            nodeLocal.Add("Registration metadata store");
        }
        if (!haState.UseSharedOperationalState)
        {
            nodeLocal.Add("Audit, history and incident operational stores");
        }
        nodeLocal.Add("Protected local SQL credential store and key ring");
        nodeLocal.Add("Login attempt limiter");
        nodeLocal.Add("Snapshot cache values");
        if (!coordination.Enabled)
        {
            nodeLocal.Add("Scheduler ownership and refresh single-flight");
        }

        if (deployment.Mode == DeploymentTopology.SingleNode)
        {
            return new DeploymentReadinessViewModel(deployment.Mode, true, "Single-node ready", "The selected persistence and coordination configuration is safe for one active Monitor application instance.", nodeLocal);
        }

        var blockers = new List<string>();
        if (sharedState.Provider != SharedStateProviderKind.SqlServer) blockers.Add("shared-state provider");
        if (!haState.UseSharedRegistrations) blockers.Add("shared registration");
        if (!haState.UseSharedOperationalState) blockers.Add("shared operational state");
        if (!coordination.Enabled) blockers.Add("distributed coordination");
        blockers.Add("shared credential/key-ring strategy");
        blockers.Add("distributed login security state");
        blockers.Add("shared snapshot delivery/cache strategy");

        return new DeploymentReadinessViewModel(
            deployment.Mode,
            blockers.Count == 0,
            blockers.Count == 0 ? "Multi-node ready" : "Multi-node blocked",
            blockers.Count == 0 ? "All required multi-node state and coordination capabilities are enabled." : $"Multi-node startup is blocked until these capability categories are ready: {string.Join(", ", blockers)}.",
            nodeLocal);
    }
}
