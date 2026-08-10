namespace Monitor.Web.Services;

public static class EnterpriseOperatorPolicy
{
    public static bool IsMaintenanceActive(ServerOperatorMetadata metadata, DateTimeOffset now) => IsWindowActive(metadata.MaintenanceWindow, now);

    public static bool IsAlertSuppressed(ServerOperatorMetadata metadata, DateTimeOffset now) => IsWindowActive(metadata.AlertSuppressionWindow, now);

    public static bool IsWindowActive(OperatorWindow? window, DateTimeOffset now) =>
        window is not null && now >= window.StartsAtUtc && now < window.EndsAtUtc;
}
