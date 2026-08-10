using Microsoft.AspNetCore.DataProtection;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

public interface IConnectionSecretProtector
{
    string Protect(string secret);
}

public sealed class DataProtectionConnectionSecretProtector : IConnectionSecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionConnectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Monitor.SqlServerCredentials.v1");
    }

    public string Protect(string secret) => _protector.Protect(secret);
}

public interface IServerRegistrationService
{
    IReadOnlyList<RegisteredServerSummary> GetAll();
    ServerRegistrationResult Register(RegisterServerInput input);
}

public sealed class ServerRegistrationService : IServerRegistrationService
{
    private readonly IConnectionSecretProtector _secretProtector;
    private readonly object _sync = new();
    private readonly List<StoredServerRegistration> _registrations = [];

    public ServerRegistrationService(IConnectionSecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public IReadOnlyList<RegisteredServerSummary> GetAll()
    {
        lock (_sync)
        {
            return _registrations
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(ToSummary)
                .ToArray();
        }
    }

    public ServerRegistrationResult Register(RegisterServerInput input)
    {
        var host = input.Host.Trim();
        var instanceName = NullIfWhiteSpace(input.InstanceName);
        var displayName = NullIfWhiteSpace(input.DisplayName)
            ?? (instanceName is null ? host : $"{host}\\{instanceName}");
        var environmentName = input.EnvironmentName.Trim();
        var username = input.AuthenticationMode == SqlAuthenticationMode.SqlLogin
            ? NullIfWhiteSpace(input.Username)
            : null;

        lock (_sync)
        {
            var duplicate = _registrations.Any(existing =>
                existing.Host.Equals(host, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.InstanceName, instanceName, StringComparison.OrdinalIgnoreCase) &&
                existing.Port == input.Port);

            if (duplicate)
            {
                return new ServerRegistrationResult(false, null, "This SQL target is already registered in the current application session.");
            }

            string? protectedPassword = null;
            if (input.AuthenticationMode == SqlAuthenticationMode.SqlLogin)
            {
                protectedPassword = _secretProtector.Protect(input.Password!);
            }

            var stored = new StoredServerRegistration(
                Guid.NewGuid(),
                displayName,
                host,
                instanceName,
                input.Port,
                environmentName,
                input.AuthenticationMode,
                username,
                protectedPassword,
                DateTimeOffset.UtcNow);

            _registrations.Add(stored);
            return new ServerRegistrationResult(true, ToSummary(stored), null);
        }
    }

    private static RegisteredServerSummary ToSummary(StoredServerRegistration item) => new(
        item.Id,
        item.DisplayName,
        item.Host,
        item.InstanceName,
        item.Port,
        item.EnvironmentName,
        item.AuthenticationMode,
        item.Username,
        !string.IsNullOrEmpty(item.ProtectedPassword),
        item.RegisteredAt);

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record StoredServerRegistration(
        Guid Id,
        string DisplayName,
        string Host,
        string? InstanceName,
        int? Port,
        string EnvironmentName,
        SqlAuthenticationMode AuthenticationMode,
        string? Username,
        string? ProtectedPassword,
        DateTimeOffset RegisteredAt);
}
