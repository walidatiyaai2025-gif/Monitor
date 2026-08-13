using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class EnterpriseSecurityHardeningTests
{
    [Fact]
    public void B200_071_SecureDownloadHeadersDisableSniffingCachingAndOpenBehavior()
    {
        var context = new DefaultHttpContext();
        EnterpriseSecurityPolicy.ApplySecureDownloadHeaders(context.Response);
        Assert.Equal("no-store, max-age=0", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("noopen", context.Response.Headers["X-Download-Options"]);
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
    }

    [Theory]
    [InlineData(EnterpriseDownloadSubject.Servers, "csv")]
    [InlineData(EnterpriseDownloadSubject.Incidents, "csv")]
    [InlineData(EnterpriseDownloadSubject.Manifest, "json")]
    [InlineData(EnterpriseDownloadSubject.Diagnostics, "zip")]
    public void B200_072_DownloadFileNamesAreAllowlistedAndDoNotAcceptUserText(EnterpriseDownloadSubject subject, string extension)
    {
        var name = EnterpriseSecurityPolicy.SafeDownloadFileName(subject, DateTimeOffset.Parse("2026-08-11T00:00:00Z"), extension);
        Assert.Matches("^monitor-[a-z]+-[0-9]{8}-[0-9]{6}\\.(csv|json|zip)$", name);
        Assert.True(name.Length <= EnterpriseSecurityPolicy.MaxDownloadFileNameLength);
        Assert.Throws<ArgumentException>(() => EnterpriseSecurityPolicy.SafeDownloadFileName(subject, DateTimeOffset.UtcNow, "exe"));
    }

    [Fact]
    public void B200_073_DiagnosticsZipEntriesAreLiteralSafeLeafNames()
    {
        var source = Read("src/Monitor.Web/Services/EnterpriseOperatorServices.cs");
        var names = Regex.Matches(source, "CreateEntry\\(\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.True(EnterpriseSecurityPolicy.IsSafeZipEntryName(name), name));
        Assert.DoesNotContain(source.Split('\n'), line => line.Contains("CreateEntry(", StringComparison.Ordinal) && !line.Contains("CreateEntry(\"", StringComparison.Ordinal));
    }

    [Fact]
    public void B200_074_OperatorTextRendersThroughRazorEncodingNotHtmlRaw()
    {
        var incident = Read("src/Monitor.Web/Views/Operations/IncidentDetails.cshtml");
        var enterprise = Read("src/Monitor.Web/Views/EnterpriseOperations/Overview.cshtml");
        Assert.Contains("@note.Text", incident, StringComparison.Ordinal);
        Assert.Contains("@note.Text", enterprise, StringComparison.Ordinal);
        Assert.DoesNotContain("Html.Raw", incident, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Html.Raw", enterprise, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("=cmd|' /C calc'!A0")]
    [InlineData("+SUM(1,1)")]
    [InlineData("-2+3")]
    [InlineData("@WEBSERVICE(\"https://invalid\")")]
    public void B200_075_MetadataExportCellsNeutralizeFormulaPrefixes(string value)
    {
        var cell = EnterpriseReportContract.EscapeCell(value);
        Assert.StartsWith("\"'", cell, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_076_EnterpriseTextBudgetRejectsOversizedAggregateInput()
    {
        EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(new string('a', 1000), new string('b', 1000));
        Assert.Throws<ArgumentException>(() => EnterpriseSecurityPolicy.ValidateEnterpriseTextBudget(new string('x', EnterpriseSecurityPolicy.MaxEnterpriseTextBudget + 1)));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef:memory.pressure")]
    [InlineData("incident_01-rule.alpha")]
    public void B200_077_IncidentRouteIdAllowsOnlyBoundedAsciiTokens(string value)
    {
        Assert.Equal(value, EnterpriseSecurityPolicy.NormalizeIncidentRouteId(value));
        Assert.Throws<ArgumentException>(() => EnterpriseSecurityPolicy.NormalizeIncidentRouteId("incident/<script>"));
        Assert.Throws<ArgumentException>(() => EnterpriseSecurityPolicy.NormalizeIncidentRouteId(new string('a', 181)));
    }

    [Fact]
    public void B200_078_EnterpriseEndpointAuthorizationMatrixIsExplicit()
    {
        AssertClassPolicy(typeof(EnterpriseOperationsController), MonitorPolicies.Read);
        AssertClassPolicy(typeof(EnterpriseReportsController), MonitorPolicies.Read);
        AssertClassPolicy(typeof(IncidentCollaborationController), MonitorPolicies.Read);
        AssertClassPolicy(typeof(GovernanceController), MonitorPolicies.Manage);
        AssertClassPolicy(typeof(FleetIntelligenceController), MonitorPolicies.Read);
        AssertMethodPolicy(typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.Audit), MonitorPolicies.Manage);
        AssertMethodPolicy(typeof(EnterpriseReportsController), nameof(EnterpriseReportsController.Manifest), MonitorPolicies.Manage);
        AssertMethodPolicy(typeof(IncidentCollaborationController), nameof(IncidentCollaborationController.ResolveWithNote), MonitorPolicies.Operate);
        AssertMethodPolicy(typeof(IncidentCollaborationController), nameof(IncidentCollaborationController.ReopenWithReason), MonitorPolicies.Operate);
    }

    [Fact]
    public void B200_079_AuditAndDiagnosticsSourcesDoNotReadCredentialFields()
    {
        var diagnostics = Read("src/Monitor.Web/Services/EnterpriseOperatorServices.cs");
        var reporting = Read("src/Monitor.Web/Services/EnterpriseReportingServices.cs");
        var canary = "Server=secret;Password=CanarySecret;";
        Assert.DoesNotContain("CredentialSecret", diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", reporting, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(canary, EnterpriseReportContract.EscapeCell("safe metadata"), StringComparison.Ordinal);
        Assert.DoesNotContain("SecretReference", reporting, StringComparison.Ordinal);
    }

    [Fact]
    public void B200_080_AllEnterprisePostActionsUseAntiforgeryAndNamedPolicy()
    {
        var controllerTypes = new[] { typeof(EnterpriseOperationsController), typeof(IncidentCollaborationController), typeof(GovernanceController) };
        foreach (var type in controllerTypes)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.DeclaringType == type && method.GetCustomAttribute<HttpPostAttribute>() is not null))
            {
                Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
                var methodPolicy = method.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                var classPolicy = type.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                Assert.False(string.IsNullOrWhiteSpace(methodPolicy ?? classPolicy), $"{type.Name}.{method.Name} lacks a named policy.");
            }
        }
    }

    private static void AssertClassPolicy(Type type, string expected)
    {
        var authorization = Assert.Single(type.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(expected, authorization.Policy);
    }

    private static void AssertMethodPolicy(Type type, string methodName, string expected)
    {
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(expected, authorization.Policy);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(FindRepoRoot(), path.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root containing Monitor.sln was not found.");
    }
}
