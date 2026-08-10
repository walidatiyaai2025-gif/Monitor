using Microsoft.Extensions.Configuration;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class EnvironmentConnectionSecretProviderTests
{
    [Fact]
    public async Task EnvironmentProvider_ResolvesStrictAliasFromEnvironmentOnly()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["MONITOR_SQL_SECRET_PROD_SQL_01_USERNAME"] = " monitor_reader ",
            ["MONITOR_SQL_SECRET_PROD_SQL_01_PASSWORD"] = "canary-password"
        };
        var provider = Provider(values);

        var secret = await provider.ResolveAsync(new ConnectionSecretReference("env:prod_sql_01"));

        Assert.NotNull(secret);
        Assert.Equal("monitor_reader", secret.Username);
        Assert.Equal("canary-password", secret.Password);
    }

    [Theory]
    [InlineData("env:")]
    [InlineData("env:bad-alias")]
    [InlineData("env:bad.alias")]
    [InlineData("env:bad/alias")]
    [InlineData("env:bad alias")]
    public async Task EnvironmentProvider_InvalidAlias_FailsClosed(string reference)
    {
        var reads = 0;
        var provider = new EnvironmentConnectionSecretProvider(_ =>
        {
            reads++;
            return "must-not-be-read";
        });

        var secret = await provider.ResolveAsync(new ConnectionSecretReference(reference));

        Assert.Null(secret);
        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task EnvironmentProvider_MissingOrPartialSecret_FailsClosed()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["MONITOR_SQL_SECRET_PROD_USERNAME"] = "monitor_reader"
        };
        var provider = Provider(values);

        var secret = await provider.ResolveAsync(new ConnectionSecretReference("env:prod"));

        Assert.Null(secret);
    }

    [Fact]
    public async Task EnvironmentReference_DoesNotFallBackToConfigurationSecrets()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionSecrets:env:prod:Username"] = "unsafe-config-user",
            ["ConnectionSecrets:env:prod:Password"] = "unsafe-config-password"
        }).Build();
        var environmentProvider = Provider(new Dictionary<string, string?>());
        var store = new ConfigurationConnectionSecretStore(configuration, [environmentProvider]);

        var secret = await store.ResolveAsync(new ConnectionSecretReference("env:prod"));

        Assert.Null(secret);
    }

    [Fact]
    public async Task LegacyConfigurationReference_RemainsBackwardCompatible()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionSecrets:sql01-login:Username"] = "legacy_reader",
            ["ConnectionSecrets:sql01-login:Password"] = "legacy-password"
        }).Build();
        var environmentProvider = Provider(new Dictionary<string, string?>());
        var store = new ConfigurationConnectionSecretStore(configuration, [environmentProvider]);

        var secret = await store.ResolveAsync(new ConnectionSecretReference("sql01-login"));

        Assert.NotNull(secret);
        Assert.Equal("legacy_reader", secret.Username);
        Assert.Equal("legacy-password", secret.Password);
    }

    [Fact]
    public async Task RuntimeCredential_RemainsHighestPriorityAndBackwardCompatible()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environmentProvider = Provider(new Dictionary<string, string?>());
        var store = new ConfigurationConnectionSecretStore(configuration, [environmentProvider]);
        var reference = await ((IRuntimeCredentialWriter)store).StoreAsync("runtime_reader", "runtime-password");

        var secret = await store.ResolveAsync(reference);

        Assert.NotNull(secret);
        Assert.Equal("runtime_reader", secret.Username);
        Assert.Equal("runtime-password", secret.Password);
    }

    private static EnvironmentConnectionSecretProvider Provider(IReadOnlyDictionary<string, string?> values) =>
        new(name => values.TryGetValue(name, out var value) ? value : null);
}
