using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Monitor.Web.Controllers;
using Xunit;

namespace Monitor.Web.Tests;

public sealed partial class B800RazorPostWiringTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void RazorPostForms_WithTagHelperActions_ResolveToHttpPostControllerActions()
    {
        var viewsRoot = Path.Combine(Root, "src", "Monitor.Web", "Views");
        var assembly = typeof(OperationsController).Assembly;
        var checkedForms = 0;

        foreach (var path in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(viewsRoot, path);
            var viewArea = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (string.Equals(viewArea, "Shared", StringComparison.OrdinalIgnoreCase)) continue;

            var source = File.ReadAllText(path);
            foreach (Match match in PostFormRegex().Matches(source))
            {
                var form = match.Value;
                var actionMatch = AspActionRegex().Match(form);
                if (!actionMatch.Success) continue;

                var actionName = actionMatch.Groups["action"].Value;
                var controllerMatch = AspControllerRegex().Match(form);
                var controllerName = controllerMatch.Success ? controllerMatch.Groups["controller"].Value : viewArea;
                var controllerType = assembly.GetType($"Monitor.Web.Controllers.{controllerName}Controller");

                Assert.True(controllerType is not null, $"{relative}: POST form action {actionName} resolves to unknown controller {controllerName}Controller.");

                var candidates = controllerType!.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(method => string.Equals(method.Name, actionName, StringComparison.Ordinal))
                    .ToArray();
                Assert.NotEmpty(candidates);
                Assert.Contains(candidates, method => method.GetCustomAttributes<HttpPostAttribute>().Any());
                checkedForms++;
            }
        }

        Assert.True(checkedForms >= 10, $"Expected a meaningful visible POST-form surface, but only {checkedForms} tag-helper forms were verified.");
    }

    [GeneratedRegex("<form\\b(?=[^>]*\\bmethod\\s*=\\s*[\"']post[\"'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PostFormRegex();

    [GeneratedRegex("\\basp-action\\s*=\\s*[\"'](?<action>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AspActionRegex();

    [GeneratedRegex("\\basp-controller\\s*=\\s*[\"'](?<controller>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AspControllerRegex();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
