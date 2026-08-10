using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ServerRegistrationTests
{
    [Fact]
    public void SqlLoginRegistration_RequiresSecretReference()
    {
        var endpoint = new SqlServerEndpoint("sql01.internal", port: 1433);

        Assert.Throws<ArgumentException>(() => new ServerRegistration(
            Guid.NewGuid(), "SQL 01", endpoint, SqlAuthenticationMode.SqlLogin,
            secretReference: null, isEnabled: true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IntegratedSecurity_RejectsSecretReference()
    {
        var endpoint = new SqlServerEndpoint("sql01.internal");

        Assert.Throws<ArgumentException>(() => new ServerRegistration(
            Guid.NewGuid(), "SQL 01", endpoint, SqlAuthenticationMode.IntegratedSecurity,
            new ConnectionSecretReference("sql01-login"), true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RegistrationJson_DoesNotExposeSecretReferenceOrCredentials()
    {
        var registration = new ServerRegistration(
            Guid.NewGuid(), "SQL 01", new SqlServerEndpoint("sql01.internal", port: 1433),
            SqlAuthenticationMode.SqlLogin, new ConnectionSecretReference("sql01-login"),
            true, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(registration);

        Assert.DoesNotContain("sql01-login", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretReference", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecretStore_ResolvesExternalConfiguration_AndFailsClosedWhenMissing()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionSecrets:sql01-login:Username"] = "monitor_reader",
            ["ConnectionSecrets:sql01-login:Password"] = "not-committed"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var store = new ConfigurationConnectionSecretStore(configuration);

        var secret = await store.ResolveAsync(new ConnectionSecretReference("sql01-login"));
        var missing = await store.ResolveAsync(new ConnectionSecretReference("missing"));

        Assert.NotNull(secret);
        Assert.Equal("monitor_reader", secret.Username);
        Assert.Equal("not-committed", secret.Password);
        Assert.Null(missing);
    }

    [Fact]
    public void Repository_StoresOnlySanitizedRegistration()
    {
        IServerRegistrationRepository repository = new InMemoryServerRegistrationRepository();
        var registration = new ServerRegistration(
            Guid.NewGuid(), "SQL 01", new SqlServerEndpoint("sql01.internal"),
            SqlAuthenticationMode.IntegratedSecurity, null, true, DateTimeOffset.UtcNow);

        repository.Upsert(registration);

        Assert.Equal(registration, repository.GetById(registration.Id));
        Assert.Single(repository.GetAll());
    }
}
