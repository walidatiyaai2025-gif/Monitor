using System.ComponentModel.DataAnnotations;

namespace Monitor.Web.Models;

public enum SqlAuthenticationMode
{
    WindowsIntegrated = 0,
    SqlLogin = 1
}

public sealed class RegisterServerInput
{
    [StringLength(80)]
    [Display(Name = "Display name")]
    public string? DisplayName { get; set; }

    [Required]
    [StringLength(253)]
    [Display(Name = "Server / host")]
    public string Host { get; set; } = string.Empty;

    [StringLength(128)]
    [Display(Name = "Instance")]
    public string? InstanceName { get; set; }

    [Range(1, 65535)]
    public int? Port { get; set; }

    [Required]
    [StringLength(40)]
    [Display(Name = "Environment")]
    public string EnvironmentName { get; set; } = "Production";

    [Display(Name = "Authentication")]
    public SqlAuthenticationMode AuthenticationMode { get; set; } = SqlAuthenticationMode.WindowsIntegrated;

    [StringLength(128)]
    [Display(Name = "SQL login")]
    public string? Username { get; set; }

    [DataType(DataType.Password)]
    [StringLength(256)]
    public string? Password { get; set; }
}

public sealed record RegisteredServerSummary(
    Guid Id,
    string DisplayName,
    string Host,
    string? InstanceName,
    int? Port,
    string EnvironmentName,
    SqlAuthenticationMode AuthenticationMode,
    string? Username,
    bool HasProtectedCredential,
    DateTimeOffset RegisteredAt)
{
    public string Target => string.IsNullOrWhiteSpace(InstanceName)
        ? Port is null ? Host : $"{Host},{Port}"
        : Port is null ? $"{Host}\\{InstanceName}" : $"{Host}\\{InstanceName},{Port}";
}

public sealed class RegisterServerPageViewModel
{
    public required RegisterServerInput Input { get; init; }
    public required IReadOnlyList<RegisteredServerSummary> Registrations { get; init; }
}

public sealed record ServerRegistrationResult(
    bool Success,
    RegisteredServerSummary? Registration,
    string? ErrorMessage);
