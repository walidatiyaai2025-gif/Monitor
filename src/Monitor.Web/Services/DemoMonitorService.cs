using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IDemoMonitorService
{
    DashboardViewModel GetDashboard();
    IReadOnlyList<ServerCard> GetServers();
    ServerDetailsViewModel? GetServer(string id);
    IReadOnlyList<IncidentRow> GetIncidents();
}

public sealed class DemoMonitorService : IDemoMonitorService
{
    private static readonly IReadOnlyList<ServerCard> Servers =
    [
        new("da-sql01", "DA-SQL01", "SQL Server 2022", "Enterprise", HealthState.Healthy, 24, 61, 34, 34, 41, 41, 14),
        new("da-sql02", "DA-SQL02", "SQL Server 2019", "Enterprise", HealthState.Warning, 41, 86, 28, 28, 36, 37, 16),
        new("da-sql03", "DA-SQL03", "SQL Server 2025", "Enterprise", HealthState.Critical, 31, 68, 42, 43, 48, 48, 18),
        new("da-sql04", "DA-SQL04", "SQL Server 2022", "Standard", HealthState.Offline, 0, 0, 0, 19, 0, 12, 74)
    ];

    private static readonly IReadOnlyList<IncidentRow> Incidents =
    [
        new("INC-00034", "Critical", "DA-SQL03", "Backup SLA exceeded for one production database", "7 min", "Open"),
        new("INC-00033", "Warning", "DA-SQL02", "Memory pressure crossed warning threshold", "12 min", "Investigating"),
        new("INC-00032", "Critical", "DA-SQL04", "SQL instance connection unavailable", "1 hr", "Open"),
        new("INC-00031", "Warning", "DA-SQL02", "SQL Agent job failed on last execution", "2 hr", "Acknowledged")
    ];

    public DashboardViewModel GetDashboard() => new()
    {
        Servers = Servers,
        Incidents = Incidents,
        Metrics =
        [
            new("Estate health", "75%", "3 of 4 instances reachable", HealthState.Warning),
            new("Backup SLA", "97%", "1 database outside policy", HealthState.Warning),
            new("Jobs", "125 / 138", "1 failed · 12 unavailable", HealthState.Warning),
            new("Blocking", "1", "Active blocking chain", HealthState.Critical)
        ],
        Activity =
        [
            new("09:55:42", "DA-SQL01 health snapshot collected", HealthState.Healthy),
            new("09:55:42", "34 databases checked from cached snapshot", HealthState.Healthy),
            new("09:55:43", "DA-SQL02 memory warning remains active", HealthState.Warning),
            new("09:55:44", "DA-SQL03 backup incident correlated", HealthState.Critical),
            new("09:55:44", "INC-00034 surfaced to Command Center", HealthState.Critical)
        ]
    };

    public IReadOnlyList<ServerCard> GetServers() => Servers;

    public ServerDetailsViewModel? GetServer(string id)
    {
        var server = Servers.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (server is null)
        {
            return null;
        }

        return new ServerDetailsViewModel
        {
            Server = server,
            Metrics =
            [
                new("CPU", $"{server.CpuPercent}%", "Latest collected snapshot", server.CpuPercent > 80 ? HealthState.Warning : HealthState.Healthy),
                new("Memory", $"{server.MemoryPercent}%", "Latest collected snapshot", server.MemoryPercent > 82 ? HealthState.Warning : HealthState.Healthy),
                new("Databases", $"{server.DatabaseOnline} / {server.DatabaseTotal}", "Online databases", server.DatabaseOnline == server.DatabaseTotal ? HealthState.Healthy : HealthState.Critical),
                new("SQL Agent", $"{server.JobsHealthy} / {server.JobsTotal}", "Healthy jobs", server.JobsHealthy == server.JobsTotal ? HealthState.Healthy : HealthState.Warning)
            ]
        };
    }

    public IReadOnlyList<IncidentRow> GetIncidents() => Incidents;
}
