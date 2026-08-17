using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800HaRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task HaEvidence_QueryIsSafeOnSql2022AcceptanceTarget()
    {
        var required = string.Equals(Environment.GetEnvironmentVariable("MONITOR_REQUIRE_REAL_SQL"), "1", StringComparison.Ordinal);
        if (!required) return;

        var host = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_HOST");
        var portText = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PORT");
        var username = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_USERNAME");
        var password = Environment.GetEnvironmentVariable("MONITOR_REAL_SQL_PASSWORD");
        Assert.False(string.IsNullOrWhiteSpace(host));
        Assert.True(int.TryParse(portText, out var port));
        Assert.False(string.IsNullOrWhiteSpace(username));
        Assert.False(string.IsNullOrWhiteSpace(password));

        var registration = new ServerRegistration(
            Guid.Parse("67676767-6767-6767-6767-676767676767"),
            "B800 HA SQL 2022",
            new SqlServerEndpoint(host!, port, encrypt: true, trustServerCertificate: true),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("b800-ha-real-sql"),
            true,
            DateTimeOffset.UtcNow);

        var row = await new HaSnapshotQuery().ExecuteAsync(
            registration,
            new SqlLoginSecret(username!, password!),
            CancellationToken.None);
        var evidence = HaEvidenceMapper.Map(row);

        Assert.True(evidence.TotalReplicas >= 0);
        Assert.True(evidence.TotalDatabaseReplicas >= 0);
        Assert.Equal(Math.Min(evidence.TotalReplicas, HaSnapshotQuery.MaxReplicas), evidence.Replicas?.Count ?? 0);
        Assert.Equal(Math.Min(evidence.TotalDatabaseReplicas, HaSnapshotQuery.MaxDatabaseReplicas), evidence.DatabaseReplicas?.Count ?? 0);
        Assert.Equal(evidence.TotalReplicas > HaSnapshotQuery.MaxReplicas, evidence.ReplicasTruncated);
        Assert.Equal(evidence.TotalDatabaseReplicas > HaSnapshotQuery.MaxDatabaseReplicas, evidence.DatabaseReplicasTruncated);

        if (!evidence.IsHadrEnabled)
        {
            Assert.Empty(evidence.Replicas ?? []);
            Assert.Empty(evidence.DatabaseReplicas ?? []);
            return;
        }

        Assert.All(evidence.Replicas ?? [], replica =>
        {
            Assert.False(string.IsNullOrWhiteSpace(replica.GroupKey));
            Assert.False(string.IsNullOrWhiteSpace(replica.ReplicaKey));
            Assert.False(string.IsNullOrWhiteSpace(replica.AvailabilityMode));
            Assert.False(string.IsNullOrWhiteSpace(replica.FailoverMode));
        });
        Assert.All(evidence.DatabaseReplicas ?? [], database =>
        {
            Assert.False(string.IsNullOrWhiteSpace(database.GroupKey));
            Assert.False(string.IsNullOrWhiteSpace(database.ReplicaKey));
            Assert.False(string.IsNullOrWhiteSpace(database.DatabaseKey));
            if (database.LogSendQueueKb.HasValue) Assert.True(database.LogSendQueueKb.Value >= 0);
            if (database.RedoQueueKb.HasValue) Assert.True(database.RedoQueueKb.Value >= 0);
            if (database.SecondaryLagSeconds.HasValue) Assert.True(database.SecondaryLagSeconds.Value >= 0);
        });
    }
}
