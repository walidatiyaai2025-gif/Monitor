using Microsoft.Extensions.Configuration;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class DemoDataConfigurationTests
{
    [Fact]
    public async Task DemoDisabled_ProducesTruthfulEmptyEstateThroughReadService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoData:Enabled"] = "false"
            })
            .Build();
        var demo = new DemoMonitorService(configuration);
        var cache = new FailIfCalledCache();
        var read = new MonitorReadService(demo, new InMemoryServerRegistrationRepository(), cache);

        Assert.Empty(demo.GetServers());
        Assert.Empty(demo.GetIncidents());
        Assert.Null(demo.GetServer("da-sql01"));

        var servers = await read.GetServersAsync();
        var page = await read.GetServersPageAsync(0, 50);
        var dashboard = await read.GetDashboardAsync();

        Assert.Empty(servers);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(dashboard.Servers);
        Assert.Empty(dashboard.Incidents);
        Assert.Contains(dashboard.Metrics, item => item.Name == "Registered servers" && item.Value == "0");
        Assert.DoesNotContain(dashboard.Activity, item => item.Message.Contains("DA-SQL", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, cache.CallCount);
    }

    [Fact]
    public void DemoEnabled_RemainsAvailableForExplicitDevelopmentUse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoData:Enabled"] = "true"
            })
            .Build();
        var demo = new DemoMonitorService(configuration);

        Assert.Equal(4, demo.GetServers().Count);
        Assert.NotNull(demo.GetServer("da-sql01"));
        Assert.NotEmpty(demo.GetIncidents());
        Assert.Contains(demo.GetDashboard().Servers, item => item.Name == "DA-SQL01");
    }

    private sealed class FailIfCalledCache : IServerHealthSnapshotCache
    {
        public int CallCount { get; private set; }

        public SnapshotCacheResult? Peek(Guid registrationId)
        {
            CallCount++;
            throw new InvalidOperationException("Snapshot cache must not be called for an empty estate.");
        }

        public Task<SnapshotCacheResult> GetAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromException<SnapshotCacheResult>(new InvalidOperationException("Snapshot cache must not be called for an empty estate."));
        }

        public Task<SnapshotCacheResult> RefreshAsync(ServerRegistration registration, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromException<SnapshotCacheResult>(new InvalidOperationException("Snapshot cache must not be called for an empty estate."));
        }
    }
}
