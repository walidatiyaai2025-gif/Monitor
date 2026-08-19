using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class GovernancePruneStateTests
{
    [Fact]
    public void FileState_MigratesLegacyReceipt_SurvivesAuditEvictionAndRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-governance-prune-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var now = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
            var time = new FixedTimeProvider(now);
            var metadata = new InMemoryOperatorMetadataStore(time);
            const string incidentId = "incident-retained";
            metadata.AddIncidentNote(incidentId, "operator", "legacy note");
            var note = Assert.Single(metadata.GetIncident(incidentId).Notes);
            var audit = new FileAuditStore(Path.Combine(directory, "audit.json"), time);
            audit.Append("operator", "governance.prune.note", note.Id.ToString("D"), "applied");

            var state = new FileGovernancePruneStateStore(directory);
            GovernancePruneStateMigration.MaterializeRetainedAuditReceipts(state, audit, metadata);
            Assert.True(state.Contains(GovernancePruneKind.Note, note.Id.ToString("D")));

            for (var index = 0; index < 1001; index++)
            {
                audit.Append("operator", "test.audit.noise", index.ToString(), "applied");
            }

            Assert.DoesNotContain(ReadAllAudit(audit), item =>
                item.Action == "governance.prune.note" && item.Target == note.Id.ToString("D"));

            var restarted = new FileGovernancePruneStateStore(directory);
            Assert.True(restarted.Contains(GovernancePruneKind.Note, note.Id.ToString("D")));

            var collaboration = new IncidentCollaborationService(
                metadata,
                audit,
                time,
                pruneState: restarted);
            Assert.Empty(collaboration.ReadNotes(incidentId, 0, 20));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileState_ContainsReloadsMarkerWrittenByPeerInstance()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-governance-prune-peer-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var stale = new FileGovernancePruneStateStore(directory);
            var writer = new FileGovernancePruneStateStore(directory);
            var noteId = Guid.NewGuid().ToString("D");

            writer.MarkPruned(GovernancePruneKind.Note, noteId);

            Assert.True(stale.Contains(GovernancePruneKind.Note, noteId));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileState_DistinctPeerMarkersAreNotLost()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-governance-prune-peer-write-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var nodeA = new FileGovernancePruneStateStore(directory);
            var nodeB = new FileGovernancePruneStateStore(directory);
            var noteA = Guid.NewGuid().ToString("D");
            var noteB = Guid.NewGuid().ToString("D");

            nodeA.MarkPruned(GovernancePruneKind.Note, noteA);
            nodeB.MarkPruned(GovernancePruneKind.Note, noteB);

            var restarted = new FileGovernancePruneStateStore(directory);
            Assert.True(restarted.Contains(GovernancePruneKind.Note, noteA));
            Assert.True(restarted.Contains(GovernancePruneKind.Note, noteB));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileState_SynchronizeReloadsPeerStateBeforeLegacyMerge()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"monitor-governance-prune-peer-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var now = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
            var metadata = new InMemoryOperatorMetadataStore(new FixedTimeProvider(now));
            const string incidentId = "incident-peer-sync";
            metadata.AddIncidentNote(incidentId, "operator", "peer marker");
            metadata.AddIncidentNote(incidentId, "operator", "legacy marker");
            var notes = metadata.GetIncident(incidentId).Notes;
            Assert.Equal(2, notes.Count());
            var peerMarker = notes[0].Id.ToString("D");
            var legacyMarker = notes[1].Id.ToString("D");

            var stale = new FileGovernancePruneStateStore(directory);
            var writer = new FileGovernancePruneStateStore(directory);
            writer.MarkPruned(GovernancePruneKind.Note, peerMarker);

            stale.Synchronize(
                metadata.Snapshot(),
                [new GovernancePruneMarker(GovernancePruneKind.Note, legacyMarker)]);

            var restarted = new FileGovernancePruneStateStore(directory);
            Assert.True(restarted.Contains(GovernancePruneKind.Note, peerMarker));
            Assert.True(restarted.Contains(GovernancePruneKind.Note, legacyMarker));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IncidentMarker_RemainsStateAwareAcrossReactivationAndRetentionWindow()
    {
        var now = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
        var time = new FixedTimeProvider(now);
        var metadata = new InMemoryOperatorMetadataStore(time);
        var audit = new InMemoryAuditStore(time);
        var state = new InMemoryGovernancePruneStateStore();
        var registrationId = Guid.NewGuid();
        var incidentId = $"{registrationId:N}:database.suspect";
        state.MarkPruned(GovernancePruneKind.Incident, incidentId);
        var service = new IncidentCollaborationService(metadata, audit, time, pruneState: state);

        var open = Incident(incidentId, registrationId, now.AddDays(-45), now, IncidentStatus.Open);
        Assert.Single(service.QueryByAssignee([open], null));

        var recentlyResolved = open with { Status = IncidentStatus.Resolved, LastSeenUtc = now.AddDays(-5) };
        Assert.Single(service.QueryByAssignee([recentlyResolved], null));

        var expiredResolved = open with { Status = IncidentStatus.Resolved, LastSeenUtc = now.AddDays(-31) };
        Assert.Empty(service.QueryByAssignee([expiredResolved], null));
    }

    [Fact]
    public async Task SharedState_ConcurrentDistinctMarkers_RetryAgainstLatestCasState()
    {
        var inner = new MemoryDocumentStore();
        var blocking = new BlockingCompareExchangeStore(inner);
        var nodeA = new SharedGovernancePruneStateStore(blocking);
        var nodeB = new SharedGovernancePruneStateStore(inner);
        var noteA = Guid.NewGuid().ToString("D");
        var noteB = Guid.NewGuid().ToString("D");

        var nodeATask = Task.Run(() => nodeA.MarkPruned(GovernancePruneKind.Note, noteA));
        await blocking.FirstWriteStarted;

        nodeB.MarkPruned(GovernancePruneKind.Note, noteB);
        blocking.Release();
        await nodeATask;

        Assert.True(nodeB.Contains(GovernancePruneKind.Note, noteA));
        Assert.True(nodeB.Contains(GovernancePruneKind.Note, noteB));
        Assert.True(blocking.ConflictObserved);
    }

    [Fact]
    public void InMemoryState_RejectsGrowthBeyondOperatorMetadataBound()
    {
        var state = new InMemoryGovernancePruneStateStore();
        for (var index = 0; index < 5000; index++)
        {
            state.MarkPruned(GovernancePruneKind.Server, Guid.NewGuid().ToString("D"));
        }

        Assert.Throws<InvalidDataException>(() =>
            state.MarkPruned(GovernancePruneKind.Server, Guid.NewGuid().ToString("D")));
    }

    private static HealthIncident Incident(
        string id,
        Guid registrationId,
        DateTimeOffset firstSeen,
        DateTimeOffset lastSeen,
        IncidentStatus status) =>
        new(
            id,
            registrationId,
            "database.suspect",
            FindingSeverity.Critical,
            "Suspect database",
            "evidence",
            firstSeen,
            lastSeen,
            1,
            status);

    private static IReadOnlyList<AuditEvent> ReadAllAudit(IAuditStore audit)
    {
        var events = new List<AuditEvent>();
        for (var offset = 0; offset < 1000; offset += 100)
        {
            var page = audit.Read(offset, 100);
            events.AddRange(page);
            if (page.Count < 100)
            {
                break;
            }
        }

        return events;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class MemoryDocumentStore : ISharedStateDocumentStore
    {
        private readonly object gate = new();
        private readonly Dictionary<string, SharedStateDocument> documents = new(StringComparer.Ordinal);

        public Task<SharedStateDocument?> ReadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                return Task.FromResult(documents.TryGetValue(key, out var document) ? document : null);
            }
        }

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                if (!documents.TryGetValue(key, out var current))
                {
                    if (expectedVersion != 0)
                    {
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    }

                    var created = new SharedStateDocument(key, 1, payloadJson, DateTimeOffset.UtcNow);
                    documents[key] = created;
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
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }

    private sealed class BlockingCompareExchangeStore(MemoryDocumentStore inner) : ISharedStateDocumentStore
    {
        private readonly TaskCompletionSource<bool> firstWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int remainingBlocks = 1;

        public Task FirstWriteStarted => firstWriteStarted.Task;
        public bool ConflictObserved { get; private set; }

        public void Release() => release.TrySetResult(true);

        public Task<SharedStateDocument?> ReadAsync(
            string key,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(key, cancellationToken);

        public async Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref remainingBlocks, 0) == 1)
            {
                firstWriteStarted.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }

            var result = await inner.CompareExchangeAsync(key, expectedVersion, payloadJson, cancellationToken);
            if (result.Status == SharedStateWriteStatus.Conflict)
            {
                ConflictObserved = true;
            }

            return result;
        }
    }
}