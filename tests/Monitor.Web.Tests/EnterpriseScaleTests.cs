using System.Runtime.CompilerServices;
using System.Text;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class EnterpriseScaleTests
{
    [Fact]
    public void B200_081_MetadataIndexProvidesStableConstantLookupSurface()
    {
        var clock = Clock();
        var serverId = Guid.NewGuid();
        var snapshot = new EnterpriseOperatorSnapshot(
            [new(serverId, ServerEnvironmentClass.Production, "core", ["tier-1"], null, null, clock.GetUtcNow())],
            [new("incident-1", "DBA", [], [], clock.GetUtcNow())]);
        var index = new OperatorMetadataIndex(snapshot);
        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal("core", index.Server(serverId)!.Group);
            Assert.Equal("DBA", index.Incident("incident-1")!.Assignee);
        }
        Assert.Equal(1, index.ServerCount);
        Assert.Equal(1, index.IncidentCount);
    }

    [Fact]
    public void B200_082_ServerPagingIsBoundedAndDeterministic()
    {
        var clock = Clock();
        var registrations = new RegistrationStore(Enumerable.Range(0, 125).Select(i => Registration($"SQL-{i:000}")).ToArray());
        var paging = new EnterprisePagingService(registrations, new IncidentStore([]), new InMemoryOperatorMetadataStore(clock));
        var page = paging.Servers(50, 25);
        Assert.Equal(25, page.Items.Count);
        Assert.Equal(125, page.Total);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
        Assert.Equal("SQL-050", page.Items[0].Registration.DisplayName);
    }

    [Fact]
    public void B200_083_IncidentPagingIsBoundedAndStable()
    {
        var clock = Clock();
        var incidents = Enumerable.Range(0, 130).Select(i => Incident($"rule-{i:000}", clock.GetUtcNow().AddMinutes(-i))).ToArray();
        var paging = new EnterprisePagingService(new RegistrationStore([]), new IncidentStore(incidents), new InMemoryOperatorMetadataStore(clock));
        var page = paging.Incidents(100, 1000);
        Assert.Equal(30, page.Items.Count);
        Assert.Equal(100, page.Limit);
        Assert.Equal(130, page.Total);
    }

    [Fact]
    public void B200_084_NoteRenderingIsLazyAndCapped()
    {
        var clock = Clock();
        var metadata = new InMemoryOperatorMetadataStore(clock);
        for (var i = 0; i < 20; i++)
        {
            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            metadata.AddIncidentNote("incident-notes", "operator", $"Note {i}");
        }
        var paging = new EnterprisePagingService(new RegistrationStore([]), new IncidentStore([]), metadata, new EnterpriseScaleOptions { MaxRenderedNotes = 5 });
        var notes = paging.Notes("incident-notes", 0, 100);
        Assert.Equal(5, notes.Count);
        Assert.Equal("Note 19", notes[0].Text);
    }

    [Fact]
    public async Task B200_085_StreamingCsvHonorsSchemaRowAndSizeBounds()
    {
        await using var stream = new MemoryStream();
        var writer = new EnterpriseStreamingCsvWriter();
        var written = await writer.WriteAsync(stream, ["Value"], Rows(EnterpriseReportContract.MaxRows + 25));
        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(stream.Length, written);
        Assert.Contains(EnterpriseReportContract.SchemaVersion, text, StringComparison.Ordinal);
        Assert.Contains("'=formula", text, StringComparison.Ordinal);
        Assert.True(stream.Length <= EnterpriseReportContract.MaxBytes);
        Assert.Equal(EnterpriseReportContract.MaxRows + 2, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task B200_086_DiagnosticsRunnerEnforcesCancellationTimeout()
    {
        var diagnostics = new BlockingDiagnostics();
        var runner = new BoundedDiagnosticsRunner(diagnostics, new EnterpriseScaleOptions { DiagnosticsTimeoutSeconds = 1 });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.BuildAsync());
        Assert.True(diagnostics.ObservedCancellation);
    }

    [Fact]
    public async Task B200_087_SharedCasTelemetryCountsAttemptsAppliedAndConflicts()
    {
        var inner = new MemorySharedStore(Clock());
        var telemetry = new TelemetrySharedStateDocumentStore(inner);
        var first = await telemetry.CompareExchangeAsync("k", 0, "{}");
        var conflict = await telemetry.CompareExchangeAsync("k", 0, "{}");
        Assert.Equal(SharedStateWriteStatus.Applied, first.Status);
        Assert.Equal(SharedStateWriteStatus.Conflict, conflict.Status);
        Assert.Equal(2, telemetry.Attempts);
        Assert.Equal(1, telemetry.Applied);
        Assert.Equal(1, telemetry.Conflicts);
    }

    [Fact]
    public async Task B200_088_SharedOperatorMetadataSurvivesWriteContention()
    {
        var clock = Clock();
        var telemetry = new TelemetrySharedStateDocumentStore(new MemorySharedStore(clock));
        var nodeA = new SharedOperatorMetadataStore(telemetry, clock);
        var nodeB = new SharedOperatorMetadataStore(telemetry, clock);
        var server = Guid.NewGuid();
        var writes = Enumerable.Range(0, 40).Select(i => Task.Run(() =>
        {
            var node = i % 2 == 0 ? nodeA : nodeB;
            node.UpsertServer(new(server, ServerEnvironmentClass.Production, $"g-{i:00}", ["scale"], null, null, clock.GetUtcNow()));
        }));
        await Task.WhenAll(writes);
        var final = nodeA.GetServer(server);
        Assert.StartsWith("g-", final.Group, StringComparison.Ordinal);
        Assert.True(telemetry.Attempts >= 40);
        Assert.True(telemetry.Applied >= 40);
    }

    [Fact]
    public void B200_089_FleetSummaryUsesOneMetadataAndPeekLookupPerServer()
    {
        var clock = Clock();
        var registrations = Enumerable.Range(0, 100).Select(i => Registration($"SQL-{i:000}")).ToArray();
        var metadata = new CountingMetadataStore(new InMemoryOperatorMetadataStore(clock));
        foreach (var registration in registrations)
            metadata.Inner.UpsertServer(new(registration.Id, ServerEnvironmentClass.Production, "core", ["tier"], null, null, clock.GetUtcNow()));
        var cache = new CountingPeekCache();
        var fleet = new FleetIntelligenceService(new RegistrationStore(registrations), cache, metadata, new IncidentStore([]), clock);
        var snapshot = fleet.Read();
        Assert.Equal(100, snapshot.Unavailable);
        Assert.Equal(100, metadata.ServerReads);
        Assert.Equal(100, cache.Peeks);
        Assert.Equal(0, cache.Collections);
    }

    [Fact]
    public void B200_090_ScaleOptionsRejectUnboundedConfiguration()
    {
        new EnterpriseScaleOptions().Validate();
        Assert.Throws<InvalidOperationException>(() => new EnterpriseScaleOptions { MaxPageSize = 201 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new EnterpriseScaleOptions { MaxRenderedNotes = 21 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new EnterpriseScaleOptions { DiagnosticsTimeoutSeconds = 31 }.Validate());
    }

    private static async IAsyncEnumerable<IReadOnlyList<string?>> Rows(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return [(i == 0 ? "=formula" : i.ToString())];
            await Task.Yield();
        }
    }

    private static MutableTimeProvider Clock() => new(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
    private static ServerRegistration Registration(string name) => new(Guid.NewGuid(), name, new SqlServerEndpoint("sql.internal", 1433), SqlAuthenticationMode.IntegratedSecurity, null, true, DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
    private static HealthIncident Incident(string rule, DateTimeOffset seen)
    {
        var registrationId = Guid.NewGuid();
        return new($"{registrationId:N}:{rule}", registrationId, rule, FindingSeverity.Warning, "Incident", "Cached evidence", seen, seen, 1, IncidentStatus.Open);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider { public DateTimeOffset UtcNow { get; set; } = utcNow; public override DateTimeOffset GetUtcNow() => UtcNow; }
    private sealed class RegistrationStore(IReadOnlyList<ServerRegistration> values) : IServerRegistrationRepository { public IReadOnlyList<ServerRegistration> GetAll() => values; public ServerRegistration? GetById(Guid id) => values.FirstOrDefault(x => x.Id == id); public void Upsert(ServerRegistration registration) => throw new NotSupportedException(); public bool Remove(Guid id) => false; }
    private sealed class IncidentStore(IReadOnlyList<HealthIncident> values) : IHealthIncidentRepository { public void Apply(IEnumerable<HealthFinding> findings) => throw new NotSupportedException(); public void Reconcile(Guid registrationId, DateTimeOffset observedAtUtc, IEnumerable<HealthFinding> activeFindings, bool canResolve) => throw new NotSupportedException(); public IReadOnlyList<HealthIncident> GetAll() => values; public HealthIncident? GetById(string id) => values.FirstOrDefault(x => x.Id == id); public bool TrySetStatus(string id, IncidentStatus expected, IncidentStatus next) => false; }
    private sealed class BlockingDiagnostics : IRedactedDiagnosticsPackageService { public bool ObservedCancellation { get; private set; } public async Task<byte[]> BuildAsync(CancellationToken cancellationToken = default) { try { await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken); return []; } catch (OperationCanceledException) { ObservedCancellation = true; throw; } } }
    private sealed class MemorySharedStore(TimeProvider clock) : ISharedStateDocumentStore
    {
        private readonly object _gate = new(); private readonly Dictionary<string, SharedStateDocument> _items = new(StringComparer.Ordinal);
        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default) { lock (_gate) { _items.TryGetValue(key, out var value); return Task.FromResult(value); } }
        public Task<SharedStateWriteResult> CompareExchangeAsync(string key, long expectedVersion, string payloadJson, CancellationToken cancellationToken = default) { lock (_gate) { _items.TryGetValue(key, out var current); var version=current?.Version??0; if(version!=expectedVersion)return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict,current)); var next=new SharedStateDocument(key,version+1,payloadJson,clock.GetUtcNow()); _items[key]=next; return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied,next)); } }
    }
    private sealed class CountingPeekCache : IServerHealthSnapshotCache { public int Peeks { get; private set; } public int Collections { get; private set; } public SnapshotCacheResult? Peek(Guid registrationId) { Peeks++; return null; } public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken=default) { Collections++; throw new InvalidOperationException(); } public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration,CancellationToken cancellationToken=default) { Collections++; throw new InvalidOperationException(); } }
    private sealed class CountingMetadataStore(IOperatorMetadataStore inner) : IOperatorMetadataStore
    {
        public IOperatorMetadataStore Inner { get; } = inner; public int ServerReads { get; private set; }
        public ServerOperatorMetadata GetServer(Guid registrationId) { ServerReads++; return Inner.GetServer(registrationId); }
        public void UpsertServer(ServerOperatorMetadata metadata) => Inner.UpsertServer(metadata);
        public IncidentOperatorMetadata GetIncident(string incidentId) => Inner.GetIncident(incidentId);
        public void AssignIncident(string incidentId,string? assignee)=>Inner.AssignIncident(incidentId,assignee);
        public void AddIncidentNote(string incidentId,string actor,string note)=>Inner.AddIncidentNote(incidentId,actor,note);
        public void SetRecommendationAcknowledged(string incidentId,string recommendationKey,bool acknowledged)=>Inner.SetRecommendationAcknowledged(incidentId,recommendationKey,acknowledged);
        public EnterpriseOperatorSnapshot Snapshot()=>Inner.Snapshot();
    }
}
