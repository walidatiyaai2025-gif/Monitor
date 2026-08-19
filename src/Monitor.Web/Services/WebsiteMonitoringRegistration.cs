namespace Monitor.Web.Services;

public static class WebsiteMonitoringRegistration
{
    public static void AddWebsiteMonitoringSubsystem(
        this IServiceCollection services,
        IConfiguration configuration,
        DeploymentTopologyOptions deploymentTopology,
        bool useSharedOperationalState,
        string? operationalRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(deploymentTopology);

        var monitoring = configuration.GetSection(WebsiteMonitoringOptions.SectionName).Get<WebsiteMonitoringOptions>() ?? new();
        monitoring.Validate();
        var outbound = configuration.GetSection(WebsiteOutboundPolicyOptions.SectionName).Get<WebsiteOutboundPolicyOptions>() ?? new();
        outbound.Validate();
        var notifications = configuration.GetSection(WebsiteNotificationOptions.SectionName).Get<WebsiteNotificationOptions>() ?? new();
        notifications.Validate();

        if (notifications.Enabled && !monitoring.Enabled)
            throw new InvalidOperationException("Website email notifications cannot be enabled while Website Monitoring is disabled.");
        if (monitoring.Enabled && deploymentTopology.Mode == DeploymentTopology.MultiNode)
            throw new InvalidOperationException("Website Monitoring MultiNode activation is blocked until WM-6 distributed ownership/shared-state acceptance is implemented.");
        if (monitoring.Enabled && useSharedOperationalState)
            throw new InvalidOperationException("Website Monitoring shared operational-state activation is blocked until WM-6 shared-state persistence is implemented.");

        services.AddSingleton(monitoring);
        services.AddSingleton(outbound);
        services.AddSingleton(notifications);

        services.AddSingleton<IWebsiteDnsResolver, SystemWebsiteDnsResolver>();
        services.AddSingleton<IWebsiteDestinationAuthorizer, ConfiguredWebsiteDestinationAuthorizer>();
        services.AddSingleton<IWebsiteHttpHopClient, PinnedWebsiteHttpHopClient>();
        services.AddSingleton<IWebsiteProbeEngine, WebsiteProbeEngine>();

        services.AddSingleton<IWebsiteTargetStore>(_ => operationalRoot is null
            ? new InMemoryWebsiteTargetStore()
            : new FileWebsiteTargetStore(Path.Combine(operationalRoot, "website-targets.json")));
        services.AddSingleton<IWebsiteProbeHistoryStore>(provider => operationalRoot is null
            ? new InMemoryWebsiteProbeHistoryStore(provider.GetRequiredService<TimeProvider>())
            : new FileWebsiteProbeHistoryStore(Path.Combine(operationalRoot, "website-history.json"), provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IWebsiteScheduleStateStore>(_ => operationalRoot is null
            ? new InMemoryWebsiteScheduleStateStore()
            : new FileWebsiteScheduleStateStore(Path.Combine(operationalRoot, "website-schedule.json")));
        services.AddSingleton<IWebsiteCheckStateStore>(_ => operationalRoot is null
            ? new InMemoryWebsiteCheckStateStore()
            : new FileWebsiteCheckStateStore(Path.Combine(operationalRoot, "website-check-state.json")));
        services.AddSingleton<IWebsiteIncidentCoordinator, WebsiteIncidentCoordinator>();
        services.AddSingleton<IWebsiteDependencyCorrelationService, WebsiteDependencyCorrelationService>();

        services.AddSingleton<IWebsiteNotificationGroupStore>(_ => operationalRoot is null
            ? new InMemoryWebsiteNotificationGroupStore()
            : new FileWebsiteNotificationGroupStore(Path.Combine(operationalRoot, "website-notification-groups.json")));
        services.AddSingleton<IWebsiteNotificationOutbox>(_ => operationalRoot is null
            ? new InMemoryWebsiteNotificationOutbox()
            : new FileWebsiteNotificationOutbox(Path.Combine(operationalRoot, "website-notification-outbox.json")));
        services.AddSingleton<IWebsiteNotificationPlanner, WebsiteNotificationPlanner>();
        services.AddSingleton<IWebsiteSmtpCredentialProvider, EnvironmentWebsiteSmtpCredentialProvider>();
        services.AddSingleton<IWebsiteEmailSender, SmtpWebsiteEmailSender>();

        services.AddHostedService<WebsiteMonitoringWorker>();
        services.AddHostedService<WebsiteNotificationDeliveryWorker>();
    }
}
