using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed record SqlLoginSecret(string Username, string Password);

internal interface IConnectionSecretStore
{
    ValueTask<SqlLoginSecret?> ResolveAsync(
        ConnectionSecretReference reference,
        CancellationToken cancellationToken = default);
    ValueTask StoreAsync(ConnectionSecretReference reference, SqlLoginSecret secret, CancellationToken cancellationToken = default) =>
        ValueTask.FromException(new NotSupportedException("The secret store is read-only."));
    ValueTask DeleteAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal interface IExternalConnectionSecretProvider
{
    bool Handles(ConnectionSecretReference reference);
    ValueTask<SqlLoginSecret?> ResolveAsync(
        ConnectionSecretReference reference,
        CancellationToken cancellationToken = default);
}

public interface IRuntimeCredentialWriter
{
    ValueTask<ConnectionSecretReference> StoreAsync(string username, string password, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal sealed class EnvironmentConnectionSecretProvider : IExternalConnectionSecretProvider
{
    private const string ReferencePrefix = "env:";
    private const string VariablePrefix = "MONITOR_SQL_SECRET_";
    private const int MaximumAliasLength = 64;
    private readonly Func<string, string?> _readEnvironmentVariable;

    public EnvironmentConnectionSecretProvider()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    internal EnvironmentConnectionSecretProvider(Func<string, string?> readEnvironmentVariable)
    {
        _readEnvironmentVariable = readEnvironmentVariable ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));
    }

    public bool Handles(ConnectionSecretReference reference) =>
        reference.Value.StartsWith(ReferencePrefix, StringComparison.OrdinalIgnoreCase);

    public ValueTask<SqlLoginSecret?> ResolveAsync(
        ConnectionSecretReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryNormalizeAlias(reference.Value, out var alias))
        {
            return ValueTask.FromResult<SqlLoginSecret?>(null);
        }

        var prefix = $"{VariablePrefix}{alias}_";
        var username = _readEnvironmentVariable($"{prefix}USERNAME");
        var password = _readEnvironmentVariable($"{prefix}PASSWORD");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return ValueTask.FromResult<SqlLoginSecret?>(null);
        }

        return ValueTask.FromResult<SqlLoginSecret?>(new SqlLoginSecret(username.Trim(), password));
    }

    private static bool TryNormalizeAlias(string reference, out string alias)
    {
        alias = string.Empty;
        if (!reference.StartsWith(ReferencePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = reference[ReferencePrefix.Length..].Trim();
        if (value.Length is < 1 or > MaximumAliasLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        alias = value.ToUpperInvariant();
        return true;
    }
}

internal sealed class ConfigurationConnectionSecretStore : IConnectionSecretStore, IRuntimeCredentialWriter
{
    private readonly IConfiguration _configuration;
    private readonly IExternalConnectionSecretProvider[] _externalProviders;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SqlLoginSecret> _runtime = new(StringComparer.Ordinal);

    public ConfigurationConnectionSecretStore(IConfiguration configuration)
        : this(configuration, [new EnvironmentConnectionSecretProvider()])
    {
    }

    public ConfigurationConnectionSecretStore(
        IConfiguration configuration,
        IEnumerable<IExternalConnectionSecretProvider> externalProviders)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _externalProviders = (externalProviders ?? throw new ArgumentNullException(nameof(externalProviders))).ToArray();
    }

    public async ValueTask<SqlLoginSecret?> ResolveAsync(
        ConnectionSecretReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_runtime.TryGetValue(reference.Value, out var runtimeSecret))
        {
            return runtimeSecret;
        }

        var provider = _externalProviders.FirstOrDefault(candidate => candidate.Handles(reference));
        if (provider is not null)
        {
            return await provider.ResolveAsync(reference, cancellationToken);
        }

        var section = _configuration.GetSection($"ConnectionSecrets:{reference.Value}");
        var username = section["Username"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        return new SqlLoginSecret(username.Trim(), password);
    }

    public ValueTask StoreAsync(ConnectionSecretReference reference, SqlLoginSecret secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _runtime[reference.Value] = secret;
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(ConnectionSecretReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _runtime.TryRemove(reference.Value, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ConnectionSecretReference> StoreAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reference = new ConnectionSecretReference($"runtime-{Guid.NewGuid():N}");
        _runtime[reference.Value] = new SqlLoginSecret(username.Trim(), password);
        return ValueTask.FromResult(reference);
    }
}
