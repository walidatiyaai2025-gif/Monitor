using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class IncidentCollaborationTests
{
    [Fact]
    public void B200_021_QueryFiltersByAssignee()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        var audit = new RecordingAuditStore(clock);
        var first = Incident("memory.pressure", clock.UtcNow);
        var second = Incident("blocking.active", clock.UtcNow, Guid.NewGuid());
        metadata.AssignIncident(first.Id, "DBA-A");
        metadata.AssignIncident(second.Id, "DBA-B");
        var service = new IncidentCollaborationService(metadata, audit, clock);

        var rows = service.QueryByAssignee([first, second], "dba-a");

        var row = Assert.Single(rows);
        Assert.Equal(first.Id, row.Incident.Id);
        Assert.Equal("DBA-A", row.Assignee);
    }

    [Fact]
    public void B200_022_AssignmentCreatesOwnerChangeAuditTimeline()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        var audit = new RecordingAuditStore(clock);
        var service = new IncidentCollaborationService(metadata, audit, clock);
        const string id = "incident-owner-change";

        service.Assign(id, "DBA-A", "operator");
        service.Assign(id, "DBA-B", "operator");

        var events = audit.Read(0, 100).Where(item => item.Action == "incident.owner.change").OrderBy(item => item.OccurredAtUtc).ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal("unassigned->DBA-A", events[0].Outcome);
        Assert.Equal("DBA-A->DBA-B", events[1].Outcome);
    }

    [Fact]
    public void B200_023_NotesArePagedAndBounded()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        for (var index = 0; index < 8; index++)
        {
            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            metadata.AddIncidentNote("incident-notes", "operator", $"Note {index}");
        }
        var service = new IncidentCollaborationService(metadata, new RecordingAuditStore(clock), clock);

        var page = service.ReadNotes("incident-notes", 2, 3);

        Assert.Equal(3, page.Count);
        Assert.Equal("Note 5", page[0].Text);
        Assert.Equal("Note 3", page[2].Text);
        Assert.Equal(8, service.ReadNotes("incident-notes", 0, 1000).Count);
    }

    [Fact]
    public void B200_024_NoteIdentityRemainsStableAcrossLaterWrites()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        metadata.AddIncidentNote("incident-identity", "operator", "First note");
        var first = Assert.Single(metadata.GetIncident("incident-identity").Notes);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        metadata.AddIncidentNote("incident-identity", "operator", "Second note");

        var reread = metadata.GetIncident("incident-identity").Notes.Single(item => item.Text == "First note");
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(first.Id, reread.Id);
        Assert.Equal(first.OccurredAtUtc, reread.OccurredAtUtc);
    }

    [Fact]
    public void B200_025_ReplayedNoteRequestIsNotAppliedTwice()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        var audit = new RecordingAuditStore(clock);
        var service = new IncidentCollaborationService(metadata, audit, clock);

        Assert.True(service.TryAddNote("incident-replay", "operator", "Check cached evidence", "req-00000001"));
        Assert.False(service.TryAddNote("incident-replay", "operator", "Check cached evidence", "req-00000001"));

        Assert.Single(metadata.GetIncident("incident-replay").Notes);
        Assert.Single(audit.Read(0, 100), item => item.Action == "incident.note.request");
    }

    [Fact]
    public void B200_026_SlaBucketsAreDeterministic()
    {
        var now = DateTimeOffset.Parse("2026-08-11T03:00:00Z");
        var clock = new FixedTimeProvider(now);
        var service = new IncidentCollaborationService(new InMemoryOperatorMetadataStore(clock), new RecordingAuditStore(clock), clock);

        Assert.Equal(IncidentSlaBucket.Fresh, service.ClassifySla(Incident("a", now.AddMinutes(-10))));
        Assert.Equal(IncidentSlaBucket.Aging, service.ClassifySla(Incident("b", now.AddMinutes(-45))));
        Assert.Equal(IncidentSlaBucket.Breached, service.ClassifySla(Incident("c", now.AddHours(-3))));
        Assert.Equal(IncidentSlaBucket.Resolved, service.ClassifySla(Incident("d", now.AddHours(-3)) with { Status = IncidentStatus.Resolved }));
    }

    [Fact]
    public void B200_027_SeverityEscalationCreatesAuditMarker()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var audit = new RecordingAuditStore(clock);
        var service = new IncidentCollaborationService(new InMemoryOperatorMetadataStore(clock), audit, clock);
        var before = Incident("blocking.active", clock.UtcNow) with { Severity = FindingSeverity.Warning };
        var after = before with { Severity = FindingSeverity.Critical, LastSeenUtc = clock.UtcNow.AddMinutes(1) };

        Assert.True(service.RecordSeverityEscalation(before, after, "observer"));
        var marker = Assert.Single(audit.Read(0, 100), item => item.Action == "incident.severity.escalation");
        Assert.Equal("Warning->Critical", marker.Outcome);
        Assert.False(service.RecordSeverityEscalation(after, before, "observer"));
    }

    [Fact]
    public void B200_028_ReopenReasonIsBoundedSeparateOperatorNote()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        var service = new IncidentCollaborationService(metadata, new RecordingAuditStore(clock), clock);

        service.AddReopenReason("incident-reopen", "operator", "Fresh evidence returned after resolution.");

        var note = Assert.Single(metadata.GetIncident("incident-reopen").Notes);
        Assert.StartsWith("[REOPEN] ", note.Text, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => service.AddReopenReason("incident-reopen", "operator", "Password=Secret123"));
    }

    [Fact]
    public void B200_029_ResolutionNoteIsBoundedSeparateOperatorNote()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        var metadata = new InMemoryOperatorMetadataStore(clock);
        var service = new IncidentCollaborationService(metadata, new RecordingAuditStore(clock), clock);

        service.AddResolutionNote("incident-resolve", "operator", "Validated recovery from cached health evidence.");

        var note = Assert.Single(metadata.GetIncident("incident-resolve").Notes);
        Assert.StartsWith("[RESOLUTION] ", note.Text, StringComparison.Ordinal);
        Assert.True(note.Text.Length <= EnterpriseOperatorValidation.MaxNoteLength);
    }

    [Fact]
    public void B200_030_CollaborationTransitionsAreProtectedAndViewsExposeWorkflow()
    {
        foreach (var name in new[] { nameof(IncidentCollaborationController.ResolveWithNote), nameof(IncidentCollaborationController.ReopenWithReason) })
        {
            var method = typeof(IncidentCollaborationController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;
            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.Equal(MonitorPolicies.Operate, Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>()).Policy);
        }

        var repo = FindRepoRoot();
        var details = File.ReadAllText(Path.Combine(repo, "src", "Monitor.Web", "Views", "Operations", "IncidentDetails.cshtml"));
        var enterprise = File.ReadAllText(Path.Combine(repo, "src", "Monitor.Web", "Views", "EnterpriseOperations", "Overview.cshtml"));
        Assert.Contains("Resolve with operator note", details, StringComparison.Ordinal);
        Assert.Contains("Reopen with reason", details, StringComparison.Ordinal);
        Assert.Contains("requestKey", enterprise, StringComparison.Ordinal);
        Assert.Contains("SLA bucket", enterprise, StringComparison.Ordinal);
    }

    private static HealthIncident Incident(string rule, DateTimeOffset firstSeen, Guid? registrationId = null)
    {
        var registration = registrationId ?? Guid.NewGuid();
        return new HealthIncident($"{registration:N}:{rule}", registration, rule, FindingSeverity.Warning, "Incident", "Cached evidence.", firstSeen, firstSeen, 1, IncidentStatus.Open);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RecordingAuditStore(TimeProvider timeProvider) : IAuditStore
    {
        private readonly List<AuditEvent> _events = [];
        public void Append(string actor, string action, string target, string outcome) => _events.Add(new AuditEvent(Guid.NewGuid(), timeProvider.GetUtcNow(), actor, action, target, outcome));
        public IReadOnlyList<AuditEvent> Read(int offset, int limit) => _events.OrderByDescending(item => item.OccurredAtUtc).Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 100)).ToArray();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root containing Monitor.sln was not found.");
    }
}
