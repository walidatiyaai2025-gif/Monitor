using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800HaRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task HaEvidence_CollectsTruthfulSql2022StateUnderLeastPrivilege()
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

        Assert.InRange(evidence.TotalLocalDatabaseReplicas, 0, int.MaxValue);
        Assert.NotNull(evidence.Replicas);
        Assert.Equal(Math.Min(evidence.TotalLocalDatabaseReplicas, HaSnapshotQuery.MaxReplicas), evidence.Replicas.Count);
        Assert.Equal(evidence.TotalLocalDatabaseReplicas > HaSnapshotQuery.MaxReplicas, evidence.IsTruncated);

        if (!evidence.IsHadrEnabled)
        {
            Assert.Equal(0, evidence.TotalLocalDatabaseReplicas);
            Assert.Empty(evidence.Replicas);
            return;
        }

        Assert.All(evidence.Replicas, replica =>
        {
            Assert.False(string.IsNullOrWhiteSpace(replica.GroupKey));
            Assert.False(string.IsNullOrWhiteSpace(replica.DatabaseKey));
            if (replica.SendQueueBytes.HasValue) Assert.True(replica.SendQueueBytes.Value >= 0);
            if (replica.RedoQueueBytes.HasValue) Assert.True(replica.RedoQueueBytes.Value >= 0);
            if (replica.LagSeconds.HasValue) Assert.True(replica.LagSeconds.Value >= 0);
        });
    }
}
