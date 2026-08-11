using System.Security.Cryptography;

namespace Monitor.Web.Services;

internal static class ProductionAdminCredentialGuard
{
    private const int MinimumIterations = 120_000;
    private const int MinimumSaltBytes = 16;
    private const int MinimumHashBytes = 32;

    private static readonly string[] RequiredEnvironmentVariables =
    [
        "DevelopmentAdmin__Username",
        "DevelopmentAdmin__Iterations",
        "DevelopmentAdmin__SaltBase64",
        "DevelopmentAdmin__HashBase64"
    ];

    public static void Validate(IHostEnvironment environment, Func<string, string?>? readEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.IsDevelopment()) return;

        readEnvironment ??= Environment.GetEnvironmentVariable;
        var values = RequiredEnvironmentVariables.ToDictionary(
            name => name,
            name => readEnvironment(name),
            StringComparer.Ordinal);

        var missing = RequiredEnvironmentVariables
            .Where(name => string.IsNullOrWhiteSpace(values[name]))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Production administrator credentials must be supplied through dedicated environment variables; source-controlled development credentials are never a production fallback.");
        }

        var username = values["DevelopmentAdmin__Username"]!.Trim();
        if (username.Length is < 3 or > 128)
        {
            throw new InvalidOperationException("Production administrator username is outside the supported bounds.");
        }

        if (!int.TryParse(values["DevelopmentAdmin__Iterations"], out var iterations) || iterations < MinimumIterations)
        {
            throw new InvalidOperationException($"Production administrator PBKDF2 iterations must be at least {MinimumIterations}.");
        }

        var salt = Decode(values["DevelopmentAdmin__SaltBase64"]!, "salt");
        var hash = Decode(values["DevelopmentAdmin__HashBase64"]!, "hash");
        if (salt.Length < MinimumSaltBytes)
        {
            throw new InvalidOperationException($"Production administrator salt must be at least {MinimumSaltBytes} bytes.");
        }
        if (hash.Length < MinimumHashBytes)
        {
            throw new InvalidOperationException($"Production administrator hash must be at least {MinimumHashBytes} bytes.");
        }

        // Reject the exact checked-in development baseline even if somebody copies it
        // into environment variables. Production must deliberately provision a distinct
        // credential derivation.
        const string developmentSalt = "dujy3bSi967TdZuFWOIi6w==";
        const string developmentHash = "CNLVuLKpYXvy38O5HUxbdFm+DeuTtfbAVYd6kSJnDws=";
        if (CryptographicOperations.FixedTimeEquals(salt, Convert.FromBase64String(developmentSalt)) &&
            CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(developmentHash)))
        {
            throw new InvalidOperationException("The checked-in development administrator credential is forbidden in production.");
        }
    }

    private static byte[] Decode(string value, string field)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"Production administrator {field} must be valid Base64.", exception);
        }
    }
}
