using Microsoft.Extensions.FileProviders;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ProductionAdminCredentialGuardTests
{
    [Fact]
    public void Development_DoesNotRequireProductionEnvironmentCredentials()
    {
        ProductionAdminCredentialGuard.Validate(
            new FakeHostEnvironment("Development"),
            _ => null);
    }

    [Fact]
    public void Production_MissingEnvironmentCredentials_FailsClosed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionAdminCredentialGuard.Validate(
                new FakeHostEnvironment("Production"),
                _ => null));

        Assert.Contains("environment variables", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HashBase64", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SaltBase64", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_DistinctStrongCredential_IsAccepted()
    {
        var values = ValidValues();

        ProductionAdminCredentialGuard.Validate(
            new FakeHostEnvironment("Production"),
            name => values.GetValueOrDefault(name));
    }

    [Fact]
    public void Production_CheckedInDevelopmentCredential_IsRejectedEvenWhenCopiedToEnvironment()
    {
        var values = ValidValues();
        values["DevelopmentAdmin__SaltBase64"] = "dujy3bSi967TdZuFWOIi6w==";
        values["DevelopmentAdmin__HashBase64"] = "CNLVuLKpYXvy38O5HUxbdFm+DeuTtfbAVYd6kSJnDws=";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionAdminCredentialGuard.Validate(
                new FakeHostEnvironment("Production"),
                name => values.GetValueOrDefault(name)));

        Assert.Contains("forbidden", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_WeakIterationCount_FailsClosed()
    {
        var values = ValidValues();
        values["DevelopmentAdmin__Iterations"] = "119999";

        Assert.Throws<InvalidOperationException>(() =>
            ProductionAdminCredentialGuard.Validate(
                new FakeHostEnvironment("Production"),
                name => values.GetValueOrDefault(name)));
    }

    private static Dictionary<string, string?> ValidValues() => new(StringComparer.Ordinal)
    {
        ["DevelopmentAdmin__Username"] = "ProductionAdmin",
        ["DevelopmentAdmin__Iterations"] = "150000",
        ["DevelopmentAdmin__SaltBase64"] = Convert.ToBase64String(Enumerable.Range(1, 16).Select(value => (byte)value).ToArray()),
        ["DevelopmentAdmin__HashBase64"] = Convert.ToBase64String(Enumerable.Range(33, 32).Select(value => (byte)value).ToArray())
    };

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Monitor.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
