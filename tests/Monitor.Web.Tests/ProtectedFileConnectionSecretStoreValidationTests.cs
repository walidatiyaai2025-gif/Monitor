using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ProtectedFileConnectionSecretStoreValidationTests
{
    private const int MaxEntries = 1024;
    private const int MaxProtectedPayloadLength = 16 * 1024;
    private const int MaxStoreFileBytes = 20 * 1024 * 1024;

    [Fact]
    public void BoundedLocalState_LoadsWithoutDecryptingPersistedPayloads()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        WriteStore(path, new Dictionary<string, string>
        {
            ["local:v1:11111111111111111111111111111111"] = "bounded-protected-payload"
        });

        var store = CreateStore(path);

        var reference = Assert.Single(store.GetOwnedReferences());
        Assert.Equal("local:v1:11111111111111111111111111111111", reference.Value);
    }

    [Fact]
    public void OversizedRawStore_FailsClosedBeforeJsonParsing()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(MaxStoreFileBytes + 1L);
        }

        var exception = Assert.Throws<InvalidOperationException>(() => CreateStore(path));

        Assert.Equal("The protected SQL secret store is invalid.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("file size", exception.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForeignPersistedReference_FailsClosedAtStartup()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        WriteStore(path, new Dictionary<string, string>
        {
            ["env:SHOULD_NOT_BE_LOCAL"] = "bounded-protected-payload"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => CreateStore(path));

        Assert.Equal("The protected SQL secret store is invalid.", exception.Message);
        Assert.DoesNotContain("SHOULD_NOT_BE_LOCAL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OversizedPersistedPayload_FailsClosedAtStartup()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        WriteStore(path, new Dictionary<string, string>
        {
            ["local:v1:22222222222222222222222222222222"] = new string('x', MaxProtectedPayloadLength + 1)
        });

        var exception = Assert.Throws<InvalidOperationException>(() => CreateStore(path));

        Assert.Equal("The protected SQL secret store is invalid.", exception.Message);
    }

    [Fact]
    public void OverCapacityPersistedState_FailsClosedAtStartup()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        WriteStore(path, Enumerable.Range(0, MaxEntries + 1).ToDictionary(
            index => $"local:v1:{index:D32}",
            _ => "bounded-protected-payload",
            StringComparer.Ordinal));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateStore(path));

        Assert.Equal("The protected SQL secret store is invalid.", exception.Message);
    }

    [Fact]
    public async Task FullStore_RejectsNewCredentialBeforePersistenceMutation()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        WriteStore(path, Enumerable.Range(0, MaxEntries).ToDictionary(
            index => $"local:v1:{index:D32}",
            _ => "bounded-protected-payload",
            StringComparer.Ordinal));
        var before = await File.ReadAllTextAsync(path);
        var store = CreateStore(path, allowLocalOwnedCredentials: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.StoreAsync("bounded-user", "bounded-password"));

        Assert.Contains("bounded entry limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await File.ReadAllTextAsync(path));
        Assert.Equal(MaxEntries, store.GetOwnedReferences().Count);
    }

    [Fact]
    public async Task FullStore_AllowsReplacingExistingOwnedReference()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        var entries = Enumerable.Range(0, MaxEntries).ToDictionary(
            index => $"local:v1:{index:D32}",
            _ => "bounded-protected-payload",
            StringComparer.Ordinal);
        WriteStore(path, entries);
        var store = CreateStore(path, allowLocalOwnedCredentials: true);
        var existing = new ConnectionSecretReference("local:v1:00000000000000000000000000000000");

        await store.StoreAsync(existing, new SqlLoginSecret("replacement-user", "replacement-password"));

        Assert.Equal(MaxEntries, store.GetOwnedReferences().Count);
        var resolved = await store.ResolveAsync(existing);
        Assert.NotNull(resolved);
        Assert.Equal("replacement-user", resolved!.Username);
        Assert.Equal("replacement-password", resolved.Password);
        Assert.InRange(new FileInfo(path).Length, 1, MaxStoreFileBytes);
    }

    [Fact]
    public async Task ExplicitReferenceWrite_EnforcesCredentialBounds()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        var store = CreateStore(path, allowLocalOwnedCredentials: true);
        var reference = new ConnectionSecretReference("local:v1:33333333333333333333333333333333");

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await store.StoreAsync(reference, new SqlLoginSecret(new string('u', 129), "password")));

        Assert.Empty(store.GetOwnedReferences());
        Assert.False(File.Exists(path));
    }

    private static ProtectedFileConnectionSecretStore CreateStore(string path, bool allowLocalOwnedCredentials = false) =>
        new(
            path,
            new EphemeralDataProtectionProvider(),
            new ConfigurationBuilder().Build(),
            [],
            new CredentialPolicyOptions { AllowLocalOwnedCredentials = allowLocalOwnedCredentials });

    private static void WriteStore(string path, Dictionary<string, string> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { version = 1, entries }));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-secret-store-validation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
