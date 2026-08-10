using System.ComponentModel.DataAnnotations;
using Monitor.Web.Services;

namespace Monitor.Web.Models;

public sealed class ConnectionLabRegistrationInput
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
    [Display(Name = "Named instance")]
    public string? InstanceName { get; set; }

    [Range(1, 65535)]
    [Display(Name = "TCP port")]
    public int? Port { get; set; }

    [Display(Name = "Authentication")]
    public SqlAuthenticationMode AuthenticationMode { get; set; } = SqlAuthenticationMode.IntegratedSecurity;

    [StringLength(128)]
    [Display(Name = "Secret reference")]
    public string? SecretReference { get; set; }

    [StringLength(128)]
    [Display(Name = "SQL username")]
    public string? SqlUsername { get; set; }

    [DataType(DataType.Password)]
    [StringLength(256)]
    [Display(Name = "SQL password")]
    public string? SqlPassword { get; set; }

    public bool Encrypt { get; set; } = true;

    [Display(Name = "Trust server certificate")]
    public bool TrustServerCertificate { get; set; }
}

public sealed class CredentialReferenceReplacementInput
{
    [Required]
    [StringLength(128)]
    public string ExternalSecretReference { get; set; } = string.Empty;
}

public sealed record ConnectionLabRegistrationSummary(
    Guid Id,
    string DisplayName,
    string Target,
    SqlAuthenticationMode AuthenticationMode,
    bool HasSecretReference,
    bool UsesLocalOwnedCredential,
    bool IsEnabled,
    bool Encrypt,
    bool TrustServerCertificate,
    DateTimeOffset CreatedAtUtc);

public sealed class ConnectionLabViewModel
{
    public required ConnectionLabRegistrationInput Input { get; init; }
    public required IReadOnlyList<ConnectionLabRegistrationSummary> Registrations { get; init; }
    public ConnectionTestResult? TestResult { get; init; }
    public Guid? TestedRegistrationId { get; init; }
    public int JourneyStep { get; init; }
    public bool AllowsLocalCredentialEntry { get; init; } = true;
    public CredentialReadinessViewModel? CredentialReadiness { get; init; }
}
