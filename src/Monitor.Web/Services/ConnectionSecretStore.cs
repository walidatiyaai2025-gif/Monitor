using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed record SqlLoginSecret(string Username, string Password);

internal interface IConnectionSecretStore
{
    ValueTask<SqlLoginSecret?> ResolveAsync(
        ConnectionSecretReference reference,
        CancellationToken cancellationToken = default);
}

internal sealed class ConfigurationConnectionSecretStore(IConfiguration configuration) : IConnectionSecretStore
{
    public ValueTask<SqlLoginSecret?> ResolveAsync(
        ConnectionSecretReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var section = configuration.GetSection($"ConnectionSecrets:{reference.Value}");
        var username = section["Username"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return ValueTask.FromResult<SqlLoginSecret?>(null);
        }

        return ValueTask.FromResult<SqlLoginSecret?>(new SqlLoginSecret(username.Trim(), password));
    }
}
