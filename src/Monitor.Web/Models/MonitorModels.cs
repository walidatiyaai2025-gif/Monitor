namespace Monitor.Web.Models;

public enum HealthState
{
    Healthy,
    Warning,
    Critical,
    Offline,
    Unknown
}

public enum ServerDataSource
{
    Demo,
    LiveFresh,
    LiveStale
}

public sealed record ServerCard(
    string Id,
    string Name,
    string Version,
    string Edition,
    HealthState State,
    int CpuPercent,
    int MemoryPercent,
    int DatabaseOnline,
    int DatabaseTotal,
    int JobsHealthy,
    int JobsTotal,
    int LastScanSecondsAgo,
    ServerDataSource Source = ServerDataSource.Demo);

public sealed record MetricCard(string Name, string Value, string Detail, HealthState State);
public sealed record ActivityItem(string Time, string Message, HealthState State);
public sealed record IncidentRow(string Id, string Severity, string Server, string Title, string Age, string State);

public sealed class DashboardViewModel
{
    public required IReadOnlyList<ServerCard> Servers { get; init; }
    public required IReadOnlyList<MetricCard> Metrics { get; init; }
    public required IReadOnlyList<ActivityItem> Activity { get; init; }
    public required IReadOnlyList<IncidentRow> Incidents { get; init; }
    public int DatabaseCount => Servers.Sum(server => server.DatabaseTotal);
    public int OnlineDatabaseCount => Servers.Sum(server => server.DatabaseOnline);
    public int CriticalCount => Incidents.Count(incident => incident.Severity == "Critical");
    public int WarningCount => Incidents.Count(incident => incident.Severity == "Warning");
}

public sealed class ServerDetailsViewModel
{
    public required ServerCard Server { get; init; }
    public required IReadOnlyList<MetricCard> Metrics { get; init; }
}

public sealed record HealthModuleServerViewModel(
    string Id,
    string Name,
    ServerDataSource Source,
    int AgeSeconds,
    int DatabaseOnline,
    int DatabaseTotal,
    DatabaseHealthDetailSnapshot? Databases,
    BackupHealthSnapshot? Backups,
    SqlAgentHealthSnapshot? Jobs,
    StorageHealthSnapshot? Storage,
    BlockingHealthSnapshot? Blocking,
    PerformanceHealthSnapshot? Performance);

public sealed record HealthModulePageViewModel(string Title, string Description, IReadOnlyList<HealthModuleServerViewModel> Servers);

public enum FindingSeverity { Warning, Critical }
public sealed record HealthFinding(Guid RegistrationId, string RuleId, FindingSeverity Severity, string Title, string Evidence, DateTimeOffset ObservedAtUtc);
public enum IncidentStatus { Open, Acknowledged, Resolved }
public sealed record HealthIncident(string Id, Guid RegistrationId, string RuleId, FindingSeverity Severity, string Title, string Evidence, DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc, int Occurrences, IncidentStatus Status);
