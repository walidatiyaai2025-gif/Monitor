using System.Text.Json;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedOperatorMetadataValidationTests
{
    private const string StateKey = "monitor:operator-metadata:v1";
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SharedLoad_DuplicateServerIds_FailsClosed()
    {
        var registrationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var server = ValidServer(registrationId);
        var documents = new TrackingDocumentStore();
        documents.Seed(StateKey, Serialize(new EnterpriseOperatorSnapshot([server, server], [])));
        var store = new SharedOperatorMetadataStore(documents, new FixedTimeProvider(Now));

        var exception = Assert.Throws<InvalidDataException>(() => store.Snapshot());

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, documents.CompareExchangeCalls);
    }

    [Fact]
    public void SharedLoad_NullIncidentNotes_FailsClosed()
    {
        var documents = new TrackingDocumentStore();
        documents.Seed(
            StateKey,
            """
            {
              "servers": [],
              "incidents": [
                {
                  "incidentId": "incident-null-notes",
                  "assignee": null,
                  "notes": null,
                  "acknowledgedRecommendationKeys": [],
                  "updatedAtUtc": "2026-08-18T12:00:00+00:00"
                }
              ]
            }
            """);
        var store = new SharedOperatorMetadataStore(documents, new FixedTimeProvider(Now));

        var exception = Assert.Throws<InvalidDataException>(() => store.Snapshot());

        Assert.Contains("invalid incident state", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, documents.CompareExchangeCalls);
    }

    [Fact]
    public void SharedLoad_OverCapacityIncidents_FailsClosed()
    {
        var incidents = Enumerable.Range(0, OperatorMetadataSnapshotValidator.MaxIncidents + 1)
            .Select(index => ValidIncident($"incident-{index:D4}"))
            .ToArray();
        var documents = new TrackingDocumentStore();
        documents.Seed(StateKey, Serialize(new EnterpriseOperatorSnapshot([], incidents)));
        var store = new SharedOperatorMetadataStore(documents, new FixedTimeProvider(Now));

        var exception = Assert.Throws<InvalidDataException>(() => store.Snapshot());

        Assert.Contains("bounded capacity", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, documents.CompareExchangeCalls);
    }

    [Fact]
    public void SharedMutation_OverCapacityCandidate_FailsBeforeCompareExchange()
    {
        var incidents = Enumerable.Range(0, OperatorMetadataSnapshotValidator.MaxIncidents)
            .Select(index => ValidIncident($"incident-{index:D4}"))
            .ToArray();
        var documents = new TrackingDocumentStore();
        documents.Seed(StateKey, Serialize(new EnterpriseOperatorSnapshot([], incidents)));
        var store = new SharedOperatorMetadataStore(documents, new FixedTimeProvider(Now));

        var exception = Assert.Throws<InvalidDataException>(() =>
            store.AssignIncident("incident-over-capacity", "dba.operator"));

        Assert.Contains("bounded capacity", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, documents.CompareExchangeCalls);
        Assert.Equal(OperatorMetadataSnapshotValidator.MaxIncidents, store.Snapshot().Incidents.Length);
    }

    [Fact]
    public void ValidSharedState_ReadsAndMutatesThroughValidatedCas()
    {
        var registrationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var server = ValidServer(registrationId);
        var incident = ValidIncident("incident-valid");
        var documents = new TrackingDocumentStore();
        documents.Seed(StateKey, Serialize(new EnterpriseOperatorSnapshot([server], [incident])));
        var store = new SharedOperatorMetadataStore(documents, new FixedTimeProvider(Now));

        Assert.Equal(ServerEnvironmentClass.Production, store.GetServer(registrationId).Environment);

        store.AssignIncident("incident-valid", "dba.operator");

        Assert.Equal(1, documents.CompareExchangeCalls);
        Assert.Equal("dba.operator", store.GetIncident("incident-valid").Assignee);
        Assert.Single(store.Snapshot().Servers);
        Assert.Single(store.Snapshot().Incidents);
    }

    private static ServerOperatorMetadata ValidServer(Guid registrationId) => new(
        registrationId,
        ServerEnvironmentClass.Production,
        "Finance",
        ["critical"],
        null,
        null,
        Now);

    private static IncidentOperatorMetadata ValidIncident(string incidentId) => new(
        incidentId,
        null,
        [],
        [],
        Now);

    private static string Serialize(EnterpriseOperatorSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, AtomicJsonFile.Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TrackingDocumentStore : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

        public int CompareExchangeCalls { get; private set; }

        public void Seed(string key, string payloadJson)
        {
            lock (_gate)
            {
                _documents[key] = new SharedStateDocument(key, 1, payloadJson, Now);
            }
        }

        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                CompareExchangeCalls++;
                if (!_documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0)
                    {
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    }

                    var created = new SharedStateDocument(key, 1, payloadJson, Now);
                    _documents[key] = created;
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, created));
                }

                if (current.Version != expectedVersion)
                {
                    return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, current));
                }

                var updated = current with
                {
                    Version = current.Version + 1,
                    PayloadJson = payloadJson,
                    UpdatedAtUtc = Now
                };
                _documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }
}
