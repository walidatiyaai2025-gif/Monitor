using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800TempDbRealSqlTests
{
    [Fact]
    [Trait("Category", "RealSql")]
    public async Task TempDbEvidence_CollectsBoundedLogicalPointInTimeData()
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
            Guid.Parse("65656565-6565-6565-6565-656565656565"),
            "B800 TempDB SQL 2022",
            new SqlServerEndpoint(host!, port, encrypt: true, trustServerCertificate: true),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("b800-tempdb-real-sql"),
            true,
            DateTimeOffset.UtcNow);

        var row = await new TempDbSnapshotQuery().ExecuteAsync(
            registration,
            new SqlLoginSecret(username!, password!),
            CancellationToken.None);
        var evidence = TempDbEvidenceMapper.Map(row);

        Assert.True(evidence.LogicalCpuCount > 0);
        Assert.True(evidence.TotalDataFiles > 0);
        Assert.NotNull(evidence.DataFiles);
        Assert.NotEmpty(evidence.DataFiles!);
        Assert.InRange(evidence.DataFiles.Count, 1, TempDbSnapshotQuery.MaxFiles);
        Assert.Equal(evidence.TotalDataFiles > evidence.DataFiles.Count, evidence.IsTruncated);
        Assert.All(evidence.DataFiles, file =>
        {
            Assert.True(file.FileId > 0);
            Assert.False(string.IsNullOrWhiteSpace(file.FileKey));
            Assert.True(file.SizeBytes > 0);
            if (file.UsedBytes.HasValue)
                Assert.InRange(file.UsedBytes.Value, 0, file.SizeBytes);
            Assert.True(file.Reads >= 0);
            Assert.True(file.Writes >= 0);
            Assert.True(file.ReadStallMs >= 0);
            Assert.True(file.WriteStallMs >= 0);
        });
    }
}
