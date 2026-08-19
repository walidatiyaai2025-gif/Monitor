namespace Monitor.Web.Services;

public static class WebsiteMonitoringRegistration
{
    public static void AddWebsiteMonitoringSubsystem(
        this IServiceCollection services,
        IConfiguration configuration,
        DeploymentTopologyOptions deploymentTopology,
        DistributedCoordinationOptions coordination,
        bool useSharedOperationalState,
        string? operationalRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(deploymentTopology);
        ArgumentNullException.ThrowIfNull(coordination);

        var monitoring = configuration.GetSection(WebsiteMonitoringOptions.SectionName).Get<WebsiteMonitoringOptions>() ?? new();
        monitoring.Validate();
        var outbound = configuration.GetSection(WebsiteOutboundPolicyOptions.SectionName).Get<WebsiteOutboundPolicyOptions>() ?? new();
        outbound.Validate();
        var notifications = configuration.GetSection(WebsiteNotificationOptions.SectionName).Get<WebsiteNotificationOptions>() ?? new();
        notifications.Validate();

        if (notifications.Enabled && !monitoring.Enabled)
            throw new InvalidOperationException("Website email notifications cannot be enabled while Website Monitoring is disabled.");
        if (monitoring.Enabled && deploymentTopology.Mode == DeploymentTopology.MultiNode && !useSharedOperationalState)
            throw new InvalidOperationException("Website Monitoring MultiNode activation requires shared operational state.");
        if (monitoring.Enabled && useSharedOperationalState && !coordination.Enabled)
            throw new InvalidOperationException("Website Monitoring shared operational-state activation requires distributed coordination for per-target probe ownership.");

        services.AddSingleton(monitoring);
        services.AddSingleton(outbound);
        services.AddSingleton(notifications);

        services.AddSingleton<IWebsiteDnsResolver, SystemWebsiteDnsResolver>();
        services.AddSingleton<IWebsiteDestinationAuthorizer, ConfiguredWebsiteDestinationAuthorizer>();
        services.AddSingleton<IWebsiteHttpHopClient, PinnedWebsiteHttpHopClient>();
        services.AddSingleton<IWebsiteProbeEngine, WebsiteProbeEngine>();

        services.AddSingleton<IWebsiteTargetStore>(provider => useSharedOperationalState
            ? new SharedWebsiteTargetStore(provider.GetRequiredService<ISharedStateDocumentStore>())
            : operationalRoot is null
                ? new InMemoryWebsiteTargetStore()
                : new FileWebsiteTargetStore(Path.Combine(operationalRoot, "website-targets.json")));
        services.AddSingleton<IWebsiteProbeHistoryStore>(provider => useSharedOperationalState
            ? new SharedWebsiteProbeHistoryStore(provider.GetRequiredService<ISharedStateDocumentStore>(), provider.GetRequiredService<TimeProvider>())
            : operationalRoot is null
                ? new InMemoryWebsiteProbeHistoryStore(provider.GetRequiredService<TimeProvider>())
                : new FileWebsiteProbeHistoryStore(Path.Combine(operationalRoot, "website-history.json"), provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IWebsiteScheduleStateStore>(provider => useSharedOperationalState
            ? new SharedWebsiteScheduleStateStore(provider.GetRequiredService<ISharedStateDocumentStore>())
            : operationalRoot is null
                ? new InMemoryWebsiteScheduleStateStore()
                : new FileWebsiteScheduleStateStore(Path.Combine(operationalRoot, "website-schedule.json")));
        services.AddSingleton<IWebsiteCheckStateStore>(provider => useSharedOperationalState
            ? new SharedWebsiteCheckStateStore(provider.GetRequiredService<ISharedStateDocumentStore>())
            : operationalRoot is null
                ? new InMemoryWebsiteCheckStateStore()
                : new FileWebsiteCheckStateStore(Path.Combine(operationalRoot, "website-check-state.json")));
        services.AddSingleton<IWebsiteIncidentCoordinator, WebsiteIncidentCoordinator>();
        services.AddSingleton<IWebsiteDependencyCorrelationService, WebsiteDependencyCorrelationService>();

        services.AddSingleton<IWebsiteNotificationGroupStore>(provider => useSharedOperationalState
            ? new SharedWebsiteNotificationGroupStore(provider.GetRequiredService<ISharedStateDocumentStore>())
            : operationalRoot is null
                ? new InMemoryWebsiteNotificationGroupStore()
                : new FileWebsiteNotificationGroupStore(Path.Combine(operationalRoot, "website-notification-groups.json")));
        services.AddSingleton<IWebsiteNotificationOutbox>(provider => useSharedOperationalState
            ? new SharedWebsiteNotificationOutbox(provider.GetRequiredService<ISharedStateDocumentStore>())
            : operationalRoot is null
                ? new InMemoryWebsiteNotificationOutbox()
                : new FileWebsiteNotificationOutbox(Path.Combine(operationalRoot, "website-notification-outbox.json")));
        services.AddSingleton<IWebsiteNotificationPlanner, WebsiteNotificationPlanner>();
        services.AddSingleton<IWebsiteSmtpCredentialProvider, EnvironmentWebsiteSmtpCredentialProvider>();
        services.AddSingleton<IWebsiteEmailSender, SmtpWebsiteEmailSender>();
        services.AddSingleton<IWebsiteProbeExecutionService, WebsiteProbeExecutionService>();

        services.AddHostedService<WebsiteMonitoringWorker>();
        services.AddHostedService<WebsiteNotificationDeliveryWorker>();
    }
}
