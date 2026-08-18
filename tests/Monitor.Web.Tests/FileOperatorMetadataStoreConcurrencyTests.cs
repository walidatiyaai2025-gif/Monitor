using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class FileOperatorMetadataStoreConcurrencyTests
{
    [Fact]
    public void IndependentInstances_ReadFreshPeerServerAndIncidentState()
    {
        WithStorePath((path, time) =>
        {
            var first = new FileOperatorMetadataStore(path, time);
            var second = new FileOperatorMetadataStore(path, time);
            var registrationId = Guid.NewGuid();
            const string incidentId = "incident:operator-peer-read";

            first.UpsertServer(new ServerOperatorMetadata(
                registrationId,
                ServerEnvironmentClass.Production,
                "Core DBA",
                ["tier-1"],
                null,
                null,
                time.GetUtcNow()));
            first.AssignIncident(incidentId, "DBA-OnCall");

            Assert.Equal("Core DBA", second.GetServer(registrationId).Group);
            Assert.Equal("DBA-OnCall", second.GetIncident(incidentId).Assignee);

            var snapshot = second.Snapshot();
            Assert.Contains(snapshot.Servers, item => item.RegistrationId == registrationId);
            Assert.Contains(snapshot.Incidents, item => item.IncidentId == incidentId);
        });
    }

    [Fact]
    public void IndependentInstances_PreserveServerAndIncidentMutationsInSharedEnvelope()
    {
        WithStorePath((path, time) =>
        {
            var first = new FileOperatorMetadataStore(path, time);
            var second = new FileOperatorMetadataStore(path, time);
            var registrationId = Guid.NewGuid();
            const string incidentId = "incident:operator-envelope";

            first.UpsertServer(new ServerOperatorMetadata(
                registrationId,
                ServerEnvironmentClass.Staging,
                "Payments",
                ["tier-2"],
                null,
                null,
                time.GetUtcNow()));
            second.AssignIncident(incidentId, "Payments-OnCall");

            var restarted = new FileOperatorMetadataStore(path, time);
            Assert.Equal("Payments", restarted.GetServer(registrationId).Group);
            Assert.Equal("Payments-OnCall", restarted.GetIncident(incidentId).Assignee);
        });
    }

    [Fact]
    public void IndependentInstances_PreservePeerIncidentNotes()
    {
        WithStorePath((path, time) =>
        {
            var first = new FileOperatorMetadataStore(path, time);
            var second = new FileOperatorMetadataStore(path, time);
            const string incidentId = "incident:operator-notes";

            first.AddIncidentNote(incidentId, "operator-a", "first peer note");
            second.AddIncidentNote(incidentId, "operator-b", "second peer note");

            var restarted = new FileOperatorMetadataStore(path, time);
            var notes = restarted.GetIncident(incidentId).Notes;
            Assert.Equal(2, notes.Length);
            Assert.Contains(notes, item => item.Text == "first peer note" && item.Actor == "operator-a");
            Assert.Contains(notes, item => item.Text == "second peer note" && item.Actor == "operator-b");
        });
    }

    private static void WithStorePath(Action<string, TimeProvider> action)
    {
        var root = Path.Combine(Path.GetTempPath(), $"monitor-operator-cross-process-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            action(Path.Combine(root, "operator-metadata.json"), TimeProvider.System);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
