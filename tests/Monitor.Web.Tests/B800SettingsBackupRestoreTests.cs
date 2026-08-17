using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800SettingsBackupRestoreTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void SettingsAndOperationalBackup_AreAdministratorOnlyNamedManagePolicy()
    {
        var settings = typeof(OperationsController).GetMethod(nameof(OperationsController.Settings))
            ?? throw new MissingMethodException(nameof(OperationsController), nameof(OperationsController.Settings));
        Assert.Contains(settings.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Policy == MonitorPolicies.Manage);

        var controllerPolicy = typeof(OperationalBackupController).GetCustomAttributes<AuthorizeAttribute>().Single();
        Assert.Equal(MonitorPolicies.Manage, controllerPolicy.Policy);

        foreach (var methodName in new[]
        {
            nameof(OperationalBackupController.CreateBackup),
            nameof(OperationalBackupController.ValidateBackup),
            nameof(OperationalBackupController.RestoreBackup)
        })
        {
            var method = typeof(OperationalBackupController).GetMethod(methodName)
                ?? throw new MissingMethodException(nameof(OperationalBackupController), methodName);
            Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }

    [Fact]
    public void Settings_View_WiresBackupValidationAndGuardedRestoreControls()
    {
        var view = Read("src/Monitor.Web/Views/Operations/Settings.cshtml");

        Assert.Contains("TempData[\"BackupStatus\"]", view, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"OperationalBackup\" asp-action=\"CreateBackup\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"OperationalBackup\" asp-action=\"ValidateBackup\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"OperationalBackup\" asp-action=\"RestoreBackup\"", view, StringComparison.Ordinal);
        Assert.Contains("@if (backup.Ready)", view, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmation\" maxlength=\"7\" placeholder=\"type RESTORE\"", view, StringComparison.Ordinal);
        Assert.Contains("checksum validated and rollback-capable", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_RequiresExactConfirmationAndAuditsOutcome()
    {
        var controller = Read("src/Monitor.Web/Controllers/OperationalBackupController.cs");

        Assert.Contains("string.Equals(confirmation?.Trim(), \"RESTORE\", StringComparison.Ordinal)", controller, StringComparison.Ordinal);
        Assert.Contains("confirmation-rejected", controller, StringComparison.Ordinal);
        Assert.Contains("backups.ValidateAsync", controller, StringComparison.Ordinal);
        Assert.Contains("backups.RestoreAsync", controller, StringComparison.Ordinal);
        Assert.Contains("audit.Append(actor, \"backup.validate\"", controller, StringComparison.Ordinal);
        Assert.Contains("audit.Append(actor, \"backup.restore\"", controller, StringComparison.Ordinal);
        Assert.Contains("TempData[\"BackupStatus\"]", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_BackupSurface_DoesNotRenderSecretMaterial()
    {
        var view = Read("src/Monitor.Web/Views/Operations/Settings.cshtml");

        Assert.Contains("Secret ciphertext and Data Protection keys are excluded", view, StringComparison.Ordinal);
        Assert.Contains("No secret reference, SQL username, provider endpoint, connection string or key-encryption key is rendered", view, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlPassword", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionString", view, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
