using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800WriteSurfaceSecurityAcceptanceTests
{
    private static readonly string Root = FindRoot();

    private static readonly IReadOnlyDictionary<string, string> ExpectedNamedPolicies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Key(typeof(ConnectionLabController), nameof(ConnectionLabController.Register))] = MonitorPolicies.Manage,
            [Key(typeof(ConnectionLabController), nameof(ConnectionLabController.Test))] = MonitorPolicies.Manage,
            [Key(typeof(ConnectionLabController), nameof(ConnectionLabController.ReplaceCredentialReference))] = MonitorPolicies.Manage,
            [Key(typeof(ConnectionLabController), nameof(ConnectionLabController.ReplaceLocalCredential))] = MonitorPolicies.Manage,
            [Key(typeof(ConnectionLabController), nameof(ConnectionLabController.CleanupOwnedCredentials))] = MonitorPolicies.Manage,
            [Key(typeof(ConnectionLabController), nameof(ConnectionLabController.Enable))] = MonitorPolicies.Manage,
            [Key(typeof(ConnectionLabController), nameof(ConnectionLabController.Disable))] = MonitorPolicies.Manage,

            [Key(typeof(EnterpriseOperationsController), nameof(EnterpriseOperationsController.UpdateServerProfile))] = MonitorPolicies.Manage,
            [Key(typeof(EnterpriseOperationsController), nameof(EnterpriseOperationsController.AssignIncident))] = MonitorPolicies.Operate,
            [Key(typeof(EnterpriseOperationsController), nameof(EnterpriseOperationsController.AddIncidentNote))] = MonitorPolicies.Operate,
            [Key(typeof(EnterpriseOperationsController), nameof(EnterpriseOperationsController.AcknowledgeRecommendation))] = MonitorPolicies.Operate,

            [Key(typeof(IncidentCollaborationController), nameof(IncidentCollaborationController.ResolveWithNote))] = MonitorPolicies.Operate,
            [Key(typeof(IncidentCollaborationController), nameof(IncidentCollaborationController.ReopenWithReason))] = MonitorPolicies.Operate,

            [Key(typeof(OperationalBackupController), nameof(OperationalBackupController.CreateBackup))] = MonitorPolicies.Manage,
            [Key(typeof(OperationalBackupController), nameof(OperationalBackupController.ValidateBackup))] = MonitorPolicies.Manage,
            [Key(typeof(OperationalBackupController), nameof(OperationalBackupController.RestoreBackup))] = MonitorPolicies.Manage,

            [Key(typeof(GovernanceController), nameof(GovernanceController.Apply))] = MonitorPolicies.Manage,

            [Key(typeof(ServerConnectionsController), nameof(ServerConnectionsController.TestConnection))] = MonitorPolicies.Manage,
            [Key(typeof(ServerConnectionsController), nameof(ServerConnectionsController.RefreshSnapshot))] = MonitorPolicies.Manage,

            [Key(typeof(OperationsController), nameof(OperationsController.RefreshServer))] = MonitorPolicies.Operate,
            [Key(typeof(OperationsController), nameof(OperationsController.AcknowledgeIncident))] = MonitorPolicies.Operate,
            [Key(typeof(OperationsController), nameof(OperationsController.ResolveIncident))] = MonitorPolicies.Operate,
            [Key(typeof(OperationsController), nameof(OperationsController.ReopenIncident))] = MonitorPolicies.Operate,
            [Key(typeof(OperationsController), nameof(OperationsController.RequestAdvisor))] = MonitorPolicies.Advisor
        };

    [Fact]
    public void EveryPostAction_IsAntiforgeryProtectedAndAuthorizationBounded()
    {
        var posts = DiscoverPostActions();
        Assert.NotEmpty(posts);

        foreach (var item in posts)
        {
            Assert.NotEmpty(item.Method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>(inherit: true));

            var anonymous = HasAllowAnonymous(item.Controller, item.Method);
            if (anonymous)
            {
                Assert.Equal(typeof(AccountController), item.Controller);
                Assert.Equal(nameof(AccountController.Login), item.Method.Name);
                Assert.Equal("/login", item.Template);
                continue;
            }

            var authorizations = Authorizations(item.Controller, item.Method);
            Assert.NotEmpty(authorizations);

            if (item.Controller == typeof(AccountController) && item.Method.Name == nameof(AccountController.Logout))
            {
                Assert.Equal("/logout", item.Template);
                continue;
            }

            var key = Key(item.Controller, item.Method.Name);
            Assert.True(ExpectedNamedPolicies.TryGetValue(key, out var expectedPolicy), $"POST action is not in the bounded B800 security matrix: {key} ({item.Template}).");
            Assert.Contains(authorizations, attribute => string.Equals(attribute.Policy, expectedPolicy, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SecurityMatrix_CoversEveryNonAccountPostAndNoStaleAction()
    {
        var posts = DiscoverPostActions();
        var discovered = posts
            .Where(item => item.Controller != typeof(AccountController))
            .Select(item => Key(item.Controller, item.Method.Name))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var expected = ExpectedNamedPolicies.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, discovered);
    }

    [Fact]
    public void AnonymousPostSurface_IsLoginOnlyAndStillRequiresAntiforgery()
    {
        var anonymousPosts = DiscoverPostActions()
            .Where(item => HasAllowAnonymous(item.Controller, item.Method))
            .ToArray();

        var login = Assert.Single(anonymousPosts);
        Assert.Equal(typeof(AccountController), login.Controller);
        Assert.Equal(nameof(AccountController.Login), login.Method.Name);
        Assert.Equal("/login", login.Template);
        Assert.NotEmpty(login.Method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Fact]
    public void SensitiveAdministrationControllers_RemainManageScopedAtClassLevel()
    {
        foreach (var controller in new[]
        {
            typeof(ConnectionLabController),
            typeof(OperationalBackupController),
            typeof(GovernanceController),
            typeof(ServerConnectionsController)
        })
        {
            var policies = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(attribute => attribute.Policy).ToArray();
            Assert.Contains(MonitorPolicies.Manage, policies);
        }
    }

    [Fact]
    public void ReadScopedControllers_CannotUseReadPolicyAloneForMutationActions()
    {
        foreach (var item in DiscoverPostActions().Where(item => item.Controller != typeof(AccountController)))
        {
            var namedPolicies = Authorizations(item.Controller, item.Method)
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .ToArray();

            Assert.Contains(namedPolicies, policy =>
                string.Equals(policy, MonitorPolicies.Manage, StringComparison.Ordinal) ||
                string.Equals(policy, MonitorPolicies.Operate, StringComparison.Ordinal) ||
                string.Equals(policy, MonitorPolicies.Advisor, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void OperatorMutationControllers_RequireAttributableActorBeforeStateChanges()
    {
        var enterprise = Read("src/Monitor.Web/Controllers/EnterpriseOperationsController.cs");
        var collaboration = Read("src/Monitor.Web/Controllers/IncidentCollaborationController.cs");

        Assert.DoesNotContain("? \"unknown\"", enterprise, StringComparison.Ordinal);
        Assert.DoesNotContain("? \"unknown\"", collaboration, StringComparison.Ordinal);
        Assert.Contains("if (!TryActor(out var actor)) return Forbid();", enterprise, StringComparison.Ordinal);
        Assert.Contains("if (!TryActor(out var actor)) return Forbid();", collaboration, StringComparison.Ordinal);

        var profile = Slice(enterprise, "public IActionResult UpdateServerProfile", "[HttpPost(\"/alerts/{id}/owner\")]");
        var actorIndex = profile.IndexOf("TryActor(out var actor)", StringComparison.Ordinal);
        var mutationIndex = profile.IndexOf("_operatorMetadata.UpsertServer(metadata)", StringComparison.Ordinal);
        Assert.True(actorIndex >= 0 && mutationIndex > actorIndex, "Server operator metadata must not mutate before attributable actor validation.");

        var resolve = Slice(collaboration, "public IActionResult ResolveWithNote", "[HttpPost(\"/alerts/{id}/reopen-with-reason\")]");
        actorIndex = resolve.IndexOf("TryActor(out var actor)", StringComparison.Ordinal);
        mutationIndex = resolve.IndexOf("workflow.Resolve(id)", StringComparison.Ordinal);
        Assert.True(actorIndex >= 0 && mutationIndex > actorIndex, "Incident resolution must not mutate before attributable actor validation.");
    }

    private static IReadOnlyList<PostAction> DiscoverPostActions() =>
        typeof(OperationsController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type) && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetCustomAttributes<HttpPostAttribute>(inherit: true)
                    .Select(attribute => new PostAction(controller, method, attribute.Template ?? string.Empty))))
            .OrderBy(item => item.Controller.FullName, StringComparer.Ordinal)
            .ThenBy(item => item.Method.Name, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<AuthorizeAttribute> Authorizations(Type controller, MethodInfo method) =>
        controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .ToArray();

    private static bool HasAllowAnonymous(Type controller, MethodInfo method) =>
        controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any() ||
        method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    private static string Slice(string value, string startToken, string endToken)
    {
        var start = value.IndexOf(startToken, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start token not found: {startToken}");
        var end = value.IndexOf(endToken, start, StringComparison.Ordinal);
        Assert.True(end > start, $"End token not found after {startToken}: {endToken}");
        return value[start..end];
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static string Key(Type controller, string methodName) => $"{controller.Name}.{methodName}";

    private sealed record PostAction(Type Controller, MethodInfo Method, string Template);
}
