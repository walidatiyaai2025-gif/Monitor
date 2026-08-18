using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class ApplicationReadinessTruthfulnessTests
{
    [Fact]
    public async Task SingleNode_DoesNotExposeMultiNodeCredentialFlagAsFalseReadiness()
    {
        var service = Create(
            DeploymentReadinessViewModel.SafeDefault(),
            multiNodeCredentialReady: false,
            backupReady: false);

        var result = await service.CheckAsync();

        Assert.Equal(ApplicationReadinessStatus.Ready, result.Status);
        Assert.True(result.CredentialReady);
        Assert.False(result.BackupReady);
        Assert.Equal("Application control-plane readiness checks passed.", result.Message);
    }

    [Fact]
    public async Task MultiNode_MissingCredentialReadiness_IsNotReadyEvenWhenDeploymentObjectClaimsReady()
    {
        var deployment = new DeploymentReadinessViewModel(
            DeploymentTopology.MultiNode,
            Ready: true,
            Status: "Multi-node ready",
            Message: "Synthetic unit-test topology readiness.",
            NodeLocalState: []);
        var service = Create(deployment, multiNodeCredentialReady: false, backupReady: true);

        var result = await service.CheckAsync();

        Assert.Equal(ApplicationReadinessStatus.NotReady, result.Status);
        Assert.False(result.CredentialReady);
        Assert.Contains("Credential readiness", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultiNode_WithCredentialReadiness_CanPassControlPlaneGateWithoutMakingBackupAGate()
    {
        var deployment = new DeploymentReadinessViewModel(
            DeploymentTopology.MultiNode,
            Ready: true,
            Status: "Multi-node ready",
            Message: "Synthetic unit-test topology readiness.",
            NodeLocalState: []);
        var service = Create(deployment, multiNodeCredentialReady: true, backupReady: false);

        var result = await service.CheckAsync();

        Assert.Equal(ApplicationReadinessStatus.Ready, result.Status);
        Assert.True(result.CredentialReady);
        Assert.False(result.BackupReady);
    }

    private static ApplicationReadinessService Create(
        DeploymentReadinessViewModel deployment,
        bool multiNodeCredentialReady,
        bool backupReady) => new(
            deployment,
            new FakeSharedReadiness(),
            new FakeCredentialReadiness(multiNodeCredentialReady),
            new FakeBackupService(backupReady),
            new SharedStateOptions { Provider = SharedStateProviderKind.Disabled },
            TimeProvider.System);

    private sealed class FakeSharedReadiness : ISharedStateReadinessService
    {
        public Task<SharedStateReadinessViewModel> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SharedStateReadinessViewModel.Ready(1));
    }

    private sealed class FakeCredentialReadiness(bool ready) : ICredentialReadinessService
    {
        public CredentialReadinessViewModel Get() => new(
            DataProtectionKeyStoreMode.SharedState,
            SharedKeyRingReady: ready,
            SqlLoginRegistrations: 0,
            LocalOwnedRegistrations: 0,
            ExternalRegistrations: 0,
            MultiNodeCredentialReady: ready,
            Status: ready ? "HA credential ready" : "HA credential blocked",
            Message: "Bounded readiness test state.");
    }

    private sealed class FakeBackupService(bool ready) : IOperationalBackupService
    {
        public Task<BackupListItem> CreateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BackupValidationResult> ValidateAsync(string backupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BackupRestoreResult> RestoreAsync(string backupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public BackupReadinessViewModel GetReadiness() => new(
            ready,
            ready ? "Backup ready" : "Backup export only / restore blocked",
            ready ? "Ready." : "Restore is not supported in this test state.",
            0,
            null,
            false,
            []);
    }
}
