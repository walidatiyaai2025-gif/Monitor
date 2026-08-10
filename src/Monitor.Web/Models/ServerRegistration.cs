using System.Text.Json.Serialization;

namespace Monitor.Web.Models;

public enum SqlAuthenticationMode
{
    IntegratedSecurity,
    SqlLogin
}

public sealed record SqlServerEndpoint
{
    public SqlServerEndpoint(
        string host,
        int? port = null,
        string? instanceName = null,
        bool encrypt = true,
        bool trustServerCertificate = false)
    {
        Host = NormalizeHost(host);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        var normalizedInstance = string.IsNullOrWhiteSpace(instanceName) ? null : NormalizeInstanceName(instanceName);
        if (port.HasValue && normalizedInstance is not null)
        {
            throw new ArgumentException("Specify either a port or an instance name, not both.");
        }

        Port = port;
        InstanceName = normalizedInstance;
        Encrypt = encrypt;
        TrustServerCertificate = trustServerCertificate;
    }

    public string Host { get; }
    public int? Port { get; }
    public string? InstanceName { get; }
    public bool Encrypt { get; }
    public bool TrustServerCertificate { get; }

    private static string NormalizeHost(string value)
    {
        var normalized = RequireText(value, nameof(value), 255);
        if (normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character is ';' or '=' or ',' or '\\' or '/' or '"' or '\''))
            throw new ArgumentException("SQL host contains unsupported characters.", "host");
        return normalized;
    }

    private static string NormalizeInstanceName(string value)
    {
        var normalized = RequireText(value, nameof(value), 128);
        if (normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character is ';' or '=' or ',' or '\\' or '/' or '"' or '\''))
            throw new ArgumentException("SQL instance name contains unsupported characters.", "instanceName");
        return normalized;
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
            throw new ArgumentException("Value is invalid or exceeds the supported length.", parameterName);
        return normalized;
    }
}

public readonly record struct ConnectionSecretReference
{
    public ConnectionSecretReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Secret reference is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 256 || normalized.Any(char.IsControl))
            throw new ArgumentException("Secret reference is invalid or too long.", nameof(value));

        Value = normalized;
    }

    public string Value { get; }
    public override string ToString() => "[secret-reference]";
}

public sealed record ServerRegistration
{
    public ServerRegistration(
        Guid id,
        string displayName,
        SqlServerEndpoint endpoint,
        SqlAuthenticationMode authenticationMode,
        ConnectionSecretReference? secretReference,
        bool isEnabled,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Registration ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        var normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length > 120 || normalizedDisplayName.Any(char.IsControl))
            throw new ArgumentException("Display name is invalid or too long.", nameof(displayName));

        if (authenticationMode == SqlAuthenticationMode.SqlLogin && secretReference is null)
        {
            throw new ArgumentException("SQL login authentication requires a secret reference.", nameof(secretReference));
        }

        if (authenticationMode == SqlAuthenticationMode.IntegratedSecurity && secretReference is not null)
        {
            throw new ArgumentException("Integrated security cannot use a SQL login secret.", nameof(secretReference));
        }

        Id = id;
        DisplayName = normalizedDisplayName;
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        AuthenticationMode = authenticationMode;
        SecretReference = secretReference;
        IsEnabled = isEnabled;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public string DisplayName { get; }
    public SqlServerEndpoint Endpoint { get; }
    public SqlAuthenticationMode AuthenticationMode { get; }

    [JsonIgnore]
    public ConnectionSecretReference? SecretReference { get; }

    public bool IsEnabled { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}
