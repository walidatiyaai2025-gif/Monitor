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
    public static SharedStateReadinessViewModel Disabled() => new(SharedStateProviderKind.Disabled, SharedStateReadinessStatus.Disabled, false, null, "Shared-state provider is disabled. Single-node local stores remain active.");
    public static SharedStateReadinessViewModel Ready(int schemaVersion) => new(SharedStateProviderKind.SqlServer, SharedStateReadinessStatus.Ready, true, schemaVersion, "Dedicated Monitor shared-state SQL provider is reachable and schema-compatible. Deployment readiness is evaluated separately against repository, coordination, credential and security-state requirements.");
    public static SharedStateReadinessViewModel SchemaMismatch(int? schemaVersion) => new(SharedStateProviderKind.SqlServer, SharedStateReadinessStatus.SchemaMismatch, false, schemaVersion, "Dedicated Monitor shared-state SQL provider is reachable but the required schema version is not installed.");
    public static SharedStateReadinessViewModel Unavailable(string message) => new(SharedStateProviderKind.SqlServer, SharedStateReadinessStatus.Unavailable, false, null, message);
}

public sealed record SettingsViewModel(
    DeploymentReadinessViewModel Deployment,
    SharedStateReadinessViewModel SharedState,
    CredentialReadinessViewModel? CredentialReadiness = null,
    BackupReadinessViewModel? BackupReadiness = null);
