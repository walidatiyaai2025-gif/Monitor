using System.ComponentModel.DataAnnotations;

namespace Monitor.Web.Models;

public enum ConnectionTestStatus
{
    Success,
    RegistrationNotFound,
    RegistrationDisabled,
    SecretUnavailable,
    AuthenticationFailed,
    Timeout,
    NetworkFailure,
    CertificateFailure,
    InvalidConfiguration,
    UnexpectedFailure
}

public sealed record ConnectionTestResult(
    ConnectionTestStatus Status,
    string Message,
    long ElapsedMilliseconds,
    string? DataSource = null,
    string? ServerVersion = null)
{
    public bool IsSuccess => Status == ConnectionTestStatus.Success;
}

public sealed class ConnectionRegistrationInput
{
    [Required]
    [StringLength(80)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(253)]
    [Display(Name = "Server / host")]
    public string Host { get; set; } = string.Empty;

    [StringLength(128)]
    [Display(Name = "Instance")]
    public string? InstanceName { get; set; }

    [Range(1, 65535)]
    public int? Port { get; set; }

    [Display(Name = "Authentication")]
    public SqlAuthenticationMode AuthenticationMode { get; set; } = SqlAuthenticationMode.IntegratedSecurity;

    [StringLength(128)]
    [Display(Name = "Secret reference")]
    public string? SecretReference { get; set; }

    public bool Encrypt { get; set; } = true;

    [Display(Name = "Trust server certificate")]
    public bool TrustServerCertificate { get; set; }
}

public sealed record ConnectionRegistrationSummary(
    Guid Id,
    string DisplayName,
    string Target,
    SqlAuthenticationMode AuthenticationMode,
    bool HasSecretReference,
    bool IsEnabled,
    bool Encrypt,
    bool TrustServerCertificate,
    DateTimeOffset CreatedAtUtc);

public sealed class ConnectionLabViewModel
{
    public required ConnectionRegistrationInput Input { get; init; }
    public required IReadOnlyList<ConnectionRegistrationSummary> Registrations { get; init; }
    public ConnectionTestResult? TestResult { get; init; }
    public Guid? TestedRegistrationId { get; init; }
}
