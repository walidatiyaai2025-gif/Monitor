using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class WebsiteMonitoringRegistrationHaTests
{
    [Fact]
    public void SingleNode_SharedOperationalState_DoesNotRequireDistributedCoordination()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebsiteMonitoring:Enabled"] = "true",
            ["WebsiteNotifications:Enabled"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<ISharedStateDocumentStore, StubSharedStateStore>();
        services.AddSingleton(TimeProvider.System);

        services.AddWebsiteMonitoringSubsystem(
            configuration,
            new DeploymentTopologyOptions { Mode = DeploymentTopology.SingleNode },
            new DistributedCoordinationOptions { Enabled = false },
            useSharedOperationalState: true,
            operationalRoot: null);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<SharedWebsiteTargetStore>(provider.GetRequiredService<IWebsiteTargetStore>());
        Assert.IsType<SharedWebsiteScheduleStateStore>(provider.GetRequiredService<IWebsiteScheduleStateStore>());
    }

    private sealed class StubSharedStateStore : ISharedStateDocumentStore
    {
        public Task<SharedStateDocument?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<SharedStateDocument?>(null);

        public Task<SharedStateWriteResult> CompareExchangeAsync(
            string key,
            long expectedVersion,
            string payloadJson,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SharedStateWriteResult(
                SharedStateWriteStatus.Applied,
                new SharedStateDocument(key, expectedVersion + 1, payloadJson, DateTimeOffset.UtcNow)));
    }
}
