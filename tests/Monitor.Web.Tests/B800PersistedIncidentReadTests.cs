using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800PersistedIncidentReadTests
{
    private static readonly Guid ServerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ServerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProductionIncidentRepositories_OwnTheReadContract()
    {
        Assert.Equal(
            typeof(FileHealthIncidentRepository),
            typeof(FileHealthIncidentRepository).GetMethod(nameof(IHealthIncidentRepository.Read), [typeof(IncidentRepositoryQuery)])!.DeclaringType);
        Assert.Equal(
            typeof(SharedHealthIncidentRepository),
            typeof(SharedHealthIncidentRepository).GetMethod(nameof(IHealthIncidentRepository.Read), [typeof(IncidentRepositoryQuery)])!.DeclaringType);
        Assert.Equal(
            typeof(TelemetryHealthIncidentRepository),
            typeof(TelemetryHealthIncidentRepository).GetMethod(nameof(IHealthIncidentRepository.Read), [typeof(IncidentRepositoryQuery)])!.DeclaringType);
    }

    [Fact]
    public void FileRepository_ReadUsesPersistedStateWithBoundedQuerySemantics()
    {
        var root = Path.Combine(Path.GetTempPath(), "monitor-b800-075", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repository = new FileHealthIncidentRepository(Path.Combine(root, "incidents.json"));
            Seed(repository);
            var acknowledged = repository.GetAll().Single(item => item.RuleId == "rule.a-warning");
            Assert.True(repository.TrySetStatus(acknowledged.Id, IncidentStatus.Open, IncidentStatus.Acknowledged));

            var result = repository.Read(new IncidentRepositoryQuery(
                RegistrationIds: new[] { ServerA },
                ExcludeResolved: true,
                Offset: 0,
                Limit: 1));

            Assert.Equal(3, result.Summary.Open);
            Assert.Equal(1, result.Summary.Acknowledged);
            Assert.Equal(3, result.Summary.Critical);
            Assert.Equal(1, result.Summary.Warning);
            Assert.Equal(3, result.TotalMatched);
            Assert.True(result.HasMore);
            Assert.Equal("rule.a-critical-new", Assert.Single(result.Items).RuleId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SharedRepository_ReadPreservesSingleDocumentStateAndBoundedQuerySemantics()
    {
        var store = new MemoryDocumentStore();
        var repository = new SharedHealthIncidentRepository(store);
        Seed(repository);
        var acknowledged = repository.GetAll().Single(item => item.RuleId == "rule.a-warning");
        Assert.True(repository.TrySetStatus(acknowledged.Id, IncidentStatus.Open, IncidentStatus.Acknowledged));

        var result = repository.Read(new IncidentRepositoryQuery(
            RegistrationIds: new[] { ServerA },
            Severity: FindingSeverity.Critical,
            ExcludeResolved: true,
            Limit: 10));

        Assert.Equal(2, result.TotalMatched);
        Assert.False(result.HasMore);
        Assert.Equal(new[] { "rule.a-critical-new", "rule.a-critical-old" }, result.Items.Select(item => item.RuleId).ToArray());
        Assert.Equal(3, result.Summary.Open);
        Assert.Equal(1, result.Summary.Acknowledged);

        var document = await store.ReadAsync("monitor:incidents:v1");
        Assert.NotNull(document);
        Assert.Equal(1, store.DocumentCount);
        Assert.Contains("rule.a-critical-new", document!.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TelemetryRepository_ForwardsReadAndObservesActiveCountWithoutGetAll()
    {
        var inner = new ReadOnlyRepository();
        var telemetry = new MonitorTelemetry(TimeProvider.System);
        var repository = new TelemetryHealthIncidentRepository(inner, telemetry);

        repository.Apply([
            new HealthFinding(ServerA, "rule.synthetic", FindingSeverity.Warning, "Synthetic", "evidence", Now)
        ]);
        var page = repository.Read(new IncidentRepositoryQuery(ExcludeResolved: true, Limit: 1));

        Assert.Equal(2, inner.ReadCount);
        Assert.Equal(0, inner.GetAllCount);
        Assert.Equal(2, telemetry.Snapshot().ActiveIncidents);
        Assert.Equal(2, page.TotalMatched);
        Assert.Equal("one", Assert.Single(page.Items).Id);
    }

    private static void Seed(IHealthIncidentRepository repository)
    {
        repository.Apply([
            Finding(ServerA, "rule.a-critical-old", FindingSeverity.Critical, Now.AddMinutes(-2)),
            Finding(ServerA, "rule.a-critical-new", FindingSeverity.Critical, Now),
            Finding(ServerA, "rule.a-warning", FindingSeverity.Warning, Now.AddMinutes(-1)),
            Finding(ServerB, "rule.b-critical", FindingSeverity.Critical, Now.AddMinutes(1))
        ]);
    }

    private static HealthFinding Finding(Guid registrationId, string ruleId, FindingSeverity severity, DateTimeOffset observedAtUtc) =>
        new(registrationId, ruleId, severity, ruleId, "evidence", observedAtUtc);

    private sealed class MemoryDocumentStore : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

        public int DocumentCount
        {
            get
            {
                lock (_gate) return _documents.Count;
            }
        }

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_documents.TryGetValue(key, out var document) ? document : null);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (!_documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0)
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));

                    var created = new SharedStateDocument(key, 1, payloadJson, Now);
                    _documents[key] = created;
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }

                if (current.Version != expectedVersion)
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));

                var updated = current with { Version = current.Version + 1, PayloadJson = payloadJson, UpdatedAtUtc = Now };
                _documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }

    private sealed class ReadOnlyRepository : IHealthIncidentRepository
    {
        private readonly HealthIncident[] _items =
        [
            Incident("one", ServerA, IncidentStatus.Open),
            Incident("two", ServerA, IncidentStatus.Acknowledged),
            Incident("resolved", ServerA, IncidentStatus.Resolved)
        ];

        public int ReadCount { get; private set; }
        public int GetAllCount { get; private set; }

        public void Apply(IEnumerable<HealthFinding> findings) { }
        public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) { }
        public IReadOnlyList<HealthIncident> GetAll()
        {
            GetAllCount++;
            throw new InvalidOperationException("Telemetry incident observation must not use GetAll().");
        }

        public IncidentRepositoryReadResult Read(IncidentRepositoryQuery query)
        {
            ReadCount++;
            return IncidentRepositoryRead.Project(_items, query);
        }

        public HealthIncident? GetById(string id) => _items.FirstOrDefault(item => item.Id == id);
        public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false;

        private static HealthIncident Incident(string id, Guid registrationId, IncidentStatus status) =>
            new(id, registrationId, "rule." + id, FindingSeverity.Warning, id, "evidence", Now.AddMinutes(-1), Now, 1, status);
    }
}
