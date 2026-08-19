using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monitor.Web.Services;

namespace Monitor.Web.Tests;

public sealed class WebsiteMonitoringRegistrationTests
{
    [Fact]
    public void Default_configuration_registers_subsystem_without_enabling_outbound_activity()
    {
        var configuration = Configuration(new Dictionary<string, string?>());
        var services = new ServiceCollection();

        services.AddWebsiteMonitoringSubsystem(
            configuration,
            new DeploymentTopologyOptions { Mode = DeploymentTopology.SingleNode },
            useSharedOperationalState: false,
            operationalRoot: null);

        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<WebsiteMonitoringOptions>().Enabled);
        Assert.False(provider.GetRequiredService<WebsiteNotificationOptions>().Enabled);
        Assert.Empty(provider.GetRequiredService<WebsiteOutboundPolicyOptions>().AllowedPrivateHosts);
        Assert.IsType<InMemoryWebsiteTargetStore>(provider.GetRequiredService<IWebsiteTargetStore>());
        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void MultiNode_activation_fails_closed_until_WM6()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["WebsiteMonitoring:Enabled"] = "true"
        });
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() => services.AddWebsiteMonitoringSubsystem(
            configuration,
            new DeploymentTopologyOptions { Mode = DeploymentTopology.MultiNode },
            useSharedOperationalState: false,
            operationalRoot: null));

        Assert.Contains("WM-6", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_operational_state_activation_fails_closed_until_WM6()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["WebsiteMonitoring:Enabled"] = "true"
        });
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() => services.AddWebsiteMonitoringSubsystem(
            configuration,
            new DeploymentTopologyOptions { Mode = DeploymentTopology.SingleNode },
            useSharedOperationalState: true,
            operationalRoot: null));

        Assert.Contains("WM-6", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Notifications_cannot_be_enabled_while_website_monitoring_is_disabled()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["WebsiteNotifications:Enabled"] = "true",
            ["WebsiteNotifications:SmtpHost"] = "smtp.example.com",
            ["WebsiteNotifications:FromAddress"] = "monitor@example.com"
        });
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() => services.AddWebsiteMonitoringSubsystem(
            configuration,
            new DeploymentTopologyOptions { Mode = DeploymentTopology.SingleNode },
            useSharedOperationalState: false,
            operationalRoot: null));

        Assert.Contains("cannot be enabled", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration Configuration(IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
