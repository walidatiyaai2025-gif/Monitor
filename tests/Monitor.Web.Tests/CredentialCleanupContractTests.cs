using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class CredentialCleanupContractTests
{
    [Fact]
    public async Task RuntimeCredentialWriter_WithoutDeleteImplementation_FailsClosed()
    {
        IRuntimeCredentialWriter writer = new StoreOnlyCredentialWriter();
        var reference = await writer.StoreAsync("reader", "secret");

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await writer.DeleteAsync(reference));

        Assert.Contains("does not support deletion", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectionSecretStore_WithoutDeleteImplementation_FailsClosed()
    {
        IConnectionSecretStore store = new ResolveOnlySecretStore();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await store.DeleteAsync(new ConnectionSecretReference("env:TEST")));

        Assert.Contains("does not support deletion", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StoreOnlyCredentialWriter : IRuntimeCredentialWriter
    {
        public ValueTask<ConnectionSecretReference> StoreAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ConnectionSecretReference("runtime-test"));
    }

    private sealed class ResolveOnlySecretStore : IConnectionSecretStore
    {
        public ValueTask<SqlLoginSecret?> ResolveAsync(
            ConnectionSecretReference reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SqlLoginSecret?>(null);
    }
}
