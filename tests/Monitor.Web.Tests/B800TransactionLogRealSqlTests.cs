using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800TransactionLogRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task TransactionLogEvidence_CollectsBoundedSql2022Stats()
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
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "B800 Transaction Log SQL 2022",
            new SqlServerEndpoint(host!, port, encrypt: true, trustServerCertificate: true),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("b800-transaction-log-real-sql"),
            true,
            DateTimeOffset.UtcNow);

        var row = await new TransactionLogSnapshotQuery().ExecuteAsync(
            registration,
            new SqlLoginSecret(username!, password!),
            CancellationToken.None);
        var evidence = TransactionLogEvidenceMapper.Map(row);

        Assert.True(evidence.TotalDatabases > 0);
        Assert.NotNull(evidence.Databases);
        Assert.NotEmpty(evidence.Databases!);
        Assert.Equal(Math.Min(evidence.TotalDatabases, TransactionLogSnapshotQuery.MaxDatabases), evidence.Databases.Count);
        Assert.Equal(evidence.TotalDatabases > TransactionLogSnapshotQuery.MaxDatabases, evidence.IsTruncated);
        Assert.Contains(evidence.Databases, database => database.HasDetailedStats);
        Assert.All(evidence.Databases, database =>
        {
            Assert.False(string.IsNullOrWhiteSpace(database.DatabaseKey));
            Assert.False(string.IsNullOrWhiteSpace(database.RecoveryModel));
            if (database.LogBackupAgeSeconds.HasValue)
                Assert.True(database.LogBackupAgeSeconds.Value >= 0);

            if (!database.HasDetailedStats) return;
            Assert.NotNull(database.TotalLogSizeBytes);
            Assert.NotNull(database.ActiveLogSizeBytes);
            Assert.NotNull(database.TotalVlfCount);
            Assert.NotNull(database.ActiveVlfCount);
            Assert.False(string.IsNullOrWhiteSpace(database.ReuseWait));
            Assert.True(database.TotalLogSizeBytes!.Value > 0);
            Assert.InRange(database.ActiveLogSizeBytes!.Value, 0, database.TotalLogSizeBytes.Value);
            Assert.True(database.TotalVlfCount!.Value > 0);
            Assert.InRange(database.ActiveVlfCount!.Value, 0, database.TotalVlfCount.Value);
        });
    }
}
