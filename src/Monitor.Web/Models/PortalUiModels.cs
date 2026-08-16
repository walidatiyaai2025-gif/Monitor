namespace Monitor.Web.Models;

public sealed record PortalStateViewModel(
    string Title,
    string Message,
    string Tone = "neutral",
    string? ActionText = null,
    string? ActionController = null,
    string? ActionName = null,
    object? ActionRouteValues = null);

public sealed record PortalPageHeaderViewModel(
    string Eyebrow,
    string Title,
    string Description,
    string? BackText = null,
    string? BackController = null,
    string? BackAction = null,
    object? BackRouteValues = null);
