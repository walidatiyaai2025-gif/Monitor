namespace Monitor.Web.Models;

public sealed record ReportCenterViewModel(
    IReadOnlyList<ServerCard> Servers,
    int TotalServers);
