using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ProtectedFileConnectionSecretStoreTests
{
    [Fact]
    public async Task Credential_RoundTripsAcrossStoreRestart_WithoutPlaintextOnDisk()
    {
        using var directory = new TemporaryDirectory();
        var keyRing = Path.Combine(directory.Path, "keys");
        var secretFile = Path.Combine(directory.Path, "secrets.json");
        var provider = DataProtectionProvider.Create(new DirectoryInfo(keyRing), configuration =>
            configuration.SetApplicationName("Monitor.SqlSecrets.v1"));
        var username = "canary-user-9482";
        var password = "canary-password-2849";
        var first = Store(secretFile, provider);

        var reference = await first.StoreAsync(username, password);
        var second = Store(secretFile, DataProtectionProvider.Create(new DirectoryInfo(keyRing), configuration =>
            configuration.SetApplicationName("Monitor.SqlSecrets.v1")));
        var resolved = await second.ResolveAsync(reference);

        Assert.StartsWith("local:v1:", reference.Value, StringComparison.Ordinal);
        Assert.NotNull(resolved);
        Assert.Equal(username, resolved.Username);
        Assert.Equal(password, resolved.Password);
        var persisted = await File.ReadAllTextAsync(secretFile);
        Assert.DoesNotContain(username, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(password, persisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DifferentKeyRing_FailsClosed()
    {
        using var directory = new TemporaryDirectory();
        var secretFile = Path.Combine(directory.Path, "secrets.json");
        var first = Store(secretFile, DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directory.Path, "keys-a"))));
        var reference = await first.StoreAsync("reader", "secret");
        var second = Store(secretFile, DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directory.Path, "keys-b"))));

        Assert.Null(await second.ResolveAsync(reference));
    }

    [Fact]
    public async Task Delete_RemovesOnlyOwnedLocalSecret()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "secrets.json");
        var store = Store(file, new EphemeralDataProtectionProvider());
        var reference = await store.StoreAsync("reader", "secret");

        await store.DeleteAsync(reference);

        Assert.Null(await store.ResolveAsync(reference));
    }

    [Fact]
    public async Task MissingCredentialPolicy_FailsClosedWithoutWriting()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "secrets.json");
        var store = new ProtectedFileConnectionSecretStore(
            file,
            new EphemeralDataProtectionProvider(),
            new ConfigurationBuilder().Build(),
            []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.StoreAsync("reader", "secret"));

        Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(file));
        Assert.False(new CredentialPolicyOptions().AllowLocalOwnedCredentials);
    }

    private static ProtectedFileConnectionSecretStore Store(string file, IDataProtectionProvider provider) => new(
        file,
        provider,
        new ConfigurationBuilder().Build(),
        [],
        new CredentialPolicyOptions { AllowLocalOwnedCredentials = true });

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-secret-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }
        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
