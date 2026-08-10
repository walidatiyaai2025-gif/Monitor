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

public interface IRuntimeCredentialWriter
{
    ValueTask<ConnectionSecretReference> StoreAsync(string username, string password, CancellationToken cancellationToken = default);
}

internal sealed class ConfigurationConnectionSecretStore(IConfiguration configuration) : IConnectionSecretStore, IRuntimeCredentialWriter
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SqlLoginSecret> _runtime = new(StringComparer.Ordinal);

    public ValueTask<SqlLoginSecret?> ResolveAsync(
        ConnectionSecretReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_runtime.TryGetValue(reference.Value, out var runtimeSecret)) return ValueTask.FromResult<SqlLoginSecret?>(runtimeSecret);
        var section = configuration.GetSection($"ConnectionSecrets:{reference.Value}");
        var username = section["Username"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return ValueTask.FromResult<SqlLoginSecret?>(null);
        }

        return ValueTask.FromResult<SqlLoginSecret?>(new SqlLoginSecret(username.Trim(), password));
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
