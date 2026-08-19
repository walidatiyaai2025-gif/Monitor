using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IncidentNoteMultiNodeIdempotencyTests
{
    [Fact]
    public async Task SameRequestKey_AcrossTwoNodes_WritesAtMostOneNote()
    {
        var time = TimeProvider.System;
        var sharedState = new MemoryDocumentStore(time);
        using var preflightBarrier = new Barrier(2);

        var first = BuildNode(sharedState, time, preflightBarrier);
        var second = BuildNode(sharedState, time, preflightBarrier);
        const string incidentId = "11111111111111111111111111111111:RULE-CONCURRENT";
        const string requestKey = "request-concurrent-12345678";

        var attempts = await Task.WhenAll(
            Task.Run(() => Attempt(first, incidentId, requestKey)),
            Task.Run(() => Attempt(second, incidentId, requestKey)));

        Assert.Equal(1, attempts.Count(result => result == AttemptResult.Added));
        Assert.All(attempts, result => Assert.Contains(result, new[] { AttemptResult.Added, AttemptResult.Duplicate, AttemptResult.Ambiguous }));

        var metadata = new SharedOperatorMetadataStore(sharedState, time).GetIncident(incidentId);
        Assert.Single(metadata.Notes);
        Assert.Equal("Concurrent investigation note.", metadata.Notes[0].Text);

        var audit = new SharedAuditStore(sharedState, time).Read(0, 1000);
        Assert.Single(audit, item => item.Action == "incident.note.write.commit" && item.Outcome == "armed");
        Assert.Single(audit, item => item.Action == "incident.note.request" && item.Outcome == "applied");
    }

    private static IncidentCollaborationService BuildNode(
        ISharedStateDocumentStore sharedState,
        TimeProvider time,
        Barrier preflightBarrier)
    {
        IAuditStore audit = new SharedAuditStore(sharedState, time);
        audit = new PreflightBarrierAuditStore(audit, preflightBarrier);
        audit = new PerformanceBoundedAuditStore(audit, new PerformanceScaleOptions());
        audit = new CoordinatedIncidentNoteAuditStore(
            audit,
            sharedState,
            time,
            useSharedOperationalState: true,
            new SharedIncidentNoteRequestStateStore(sharedState));

        return new IncidentCollaborationService(
            new SharedOperatorMetadataStore(sharedState, time),
            audit,
            time);
    }

    private static AttemptResult Attempt(IncidentCollaborationService service, string incidentId, string requestKey)
    {
        try
        {
            return service.TryAddNote(
                incidentId,
                "operator",
                "Concurrent investigation note.",
                requestKey)
                ? AttemptResult.Added
                : AttemptResult.Duplicate;
        }
        catch (IncidentNoteRequestAmbiguousException)
        {
            return AttemptResult.Ambiguous;
        }
    }

    private enum AttemptResult
    {
        Added,
        Duplicate,
        Ambiguous
    }

    private sealed class PreflightBarrierAuditStore(IAuditStore inner, Barrier barrier) : IAuditStore
    {
        private int _reads;

        public void Append(string actor, string action, string target, string outcome) =>
            inner.Append(actor, action, target, outcome);

        public IReadOnlyList<AuditEvent> Read(int offset, int limit)
        {
            var read = Interlocked.Increment(ref _reads);
            if (read <= 2 && !barrier.SignalAndWait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Concurrent incident-note preflight did not rendezvous.");
            }

            return inner.Read(offset, limit);
        }
    }

    private sealed class MemoryDocumentStore(TimeProvider timeProvider) : ISharedStateDocumentStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, SharedStateDocument> _documents = new(StringComparer.Ordinal);

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
                    {
                        return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Conflict, null));
                    }

                    var created = new SharedStateDocument(key, 1, payloadJson, timeProvider.GetUtcNow());
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
                    UpdatedAtUtc = timeProvider.GetUtcNow()
                };
                _documents[key] = updated;
                return Task.FromResult(new SharedStateWriteResult(SharedStateWriteStatus.Applied, updated));
            }
        }
    }
}
