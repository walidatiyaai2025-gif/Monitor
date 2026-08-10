using Monitor.Web.Services;

namespace Monitor.Web.Models;

public enum SharedStateReadinessStatus
{
    Disabled,
    Ready,
    SchemaMismatch,
    Unavailable
}

public sealed record SharedStateReadinessViewModel(
    SharedStateProviderKind Provider,
    SharedStateReadinessStatus Status,
    bool SharedStorageReady,
    int? SchemaVersion,
    string Message)
{
    public static SharedStateReadinessViewModel Disabled() =>
        new(
            SharedStateProviderKind.Disabled,
            SharedStateReadinessStatus.Disabled,
            SharedStorageReady: false,
            SchemaVersion: null,
            Message: "Shared-state provider is disabled. Single-node local stores remain active.");

    public static SharedStateReadinessViewModel Ready(int schemaVersion) =>
        new(
            SharedStateProviderKind.SqlServer,
            SharedStateReadinessStatus.Ready,
            SharedStorageReady: true,
            SchemaVersion: schemaVersion,
            Message: "Dedicated Monitor shared-state SQL provider is reachable and schema-compatible. Multi-node remains blocked until application repositories and distributed coordination are migrated.");

    public static SharedStateReadinessViewModel SchemaMismatch(int? schemaVersion) =>
        new(
            SharedStateProviderKind.SqlServer,
            SharedStateReadinessStatus.SchemaMismatch,
            SharedStorageReady: false,
            SchemaVersion: schemaVersion,
            Message: "Dedicated Monitor shared-state SQL provider is reachable but the required schema version is not installed.");

    public static SharedStateReadinessViewModel Unavailable(string message) =>
        new(
            SharedStateProviderKind.SqlServer,
            SharedStateReadinessStatus.Unavailable,
            SharedStorageReady: false,
            SchemaVersion: null,
            Message: message);
}

public sealed record SettingsViewModel(
    DeploymentReadinessViewModel Deployment,
    SharedStateReadinessViewModel SharedState);
