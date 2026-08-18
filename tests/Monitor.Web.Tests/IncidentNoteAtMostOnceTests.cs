using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IncidentNoteAtMostOnceTests
{
    [Fact]
    public void FinalAuditFailure_MakesSameRequestKeyAmbiguousAndPreventsSecondNote()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 18, 12, 30, 0, TimeSpan.Zero));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        var audit = new FailFinalNoteReceiptAuditStore(clock);
        var service = new IncidentCollaborationService(metadata, audit, clock);
        const string incidentId = "incident-ambiguous-retry";
        const string requestKey = "request-ambiguous-0001";

        var firstFailure = Assert.Throws<IOException>(() =>
            service.TryAddNote(incidentId, "operator", "Checked the bounded cached evidence.", requestKey));

        Assert.Contains("final note receipt unavailable", firstFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(metadata.GetIncident(incidentId).Notes);
        Assert.Single(audit.Read(0, 100), item =>
            item.Action == "incident.note.write.request" && item.Outcome == "requested");
        Assert.DoesNotContain(audit.Read(0, 100), item =>
            item.Action == "incident.note.request" && item.Outcome == "applied");

        var retryFailure = Assert.Throws<IncidentNoteRequestAmbiguousException>(() =>
            service.TryAddNote(incidentId, "operator", "Checked the bounded cached evidence.", requestKey));

        Assert.Contains("unresolved outcome", retryFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(metadata.GetIncident(incidentId).Notes);
        Assert.Single(audit.Read(0, 100), item =>
            item.Action == "incident.note.write.request" && item.Outcome == "requested");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FailFinalNoteReceiptAuditStore(TimeProvider timeProvider) : IAuditStore
    {
        private readonly List<AuditEvent> _events = [];

        public void Append(string actor, string action, string target, string outcome)
        {
            if (action == "incident.note.request" && outcome == "applied")
            {
                throw new IOException("Final note receipt unavailable.");
            }

            _events.Add(new AuditEvent(Guid.NewGuid(), timeProvider.GetUtcNow(), actor, action, target, outcome));
        }

        public IReadOnlyList<AuditEvent> Read(int offset, int limit) =>
            _events
                .OrderByDescending(item => item.OccurredAtUtc)
                .Skip(Math.Max(0, offset))
                .Take(Math.Clamp(limit, 1, 100))
                .ToArray();
    }
}
