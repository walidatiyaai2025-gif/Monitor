using System.Text.Json;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class BackupPolicyWiringTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void DefaultConfiguration_HasNoRpoNumbers()
    {
        foreach (var relative in new[] { "src/Monitor.Web/appsettings.json", "deploy/appsettings.Production.example.json" })
        {
            using var json = JsonDocument.Parse(Read(relative));
            var policy = json.RootElement.GetProperty("BackupPolicy");
            Assert.False(policy.GetProperty("Enabled").GetBoolean());
            Assert.False(policy.TryGetProperty("FullRpoMinutes", out _));
            Assert.False(policy.TryGetProperty("LogRpoMinutes", out _));
        }
    }

    [Fact]
    public void Startup_ValidatesPolicyBeforeRegistration()
    {
        var program = Read("src/Monitor.Web/Program.cs");
        Assert.Contains("GetSection(BackupPolicyOptions.SectionName).Get<BackupPolicyOptions>()", program, StringComparison.Ordinal);
        Assert.Contains("backupPolicyOptions.Validate();", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton(backupPolicyOptions)", program, StringComparison.Ordinal);
    }

    [Fact]
    public void BackupScreen_ReceivesPolicyButDoesNotClaimCompliance()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationsController.cs");
        var view = Read("src/Monitor.Web/Views/Operations/Backups.cshtml");

        Assert.Contains("ViewData[\"BackupPolicy\"] = _backupPolicy", controller, StringComparison.Ordinal);
        Assert.Contains("BACKUP RPO POLICY", view, StringComparison.Ordinal);
        Assert.Contains("RPO POLICY NOT CONFIGURED", view, StringComparison.Ordinal);
        Assert.Contains("POLICY CONFIGURED · EVIDENCE INCOMPLETE", view, StringComparison.Ordinal);
        Assert.Contains("B300 COMPLIANCE", view, StringComparison.Ordinal);
        Assert.Contains("Not evaluated", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Batch300BackupCompliance", view, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
