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
        var unresolved = new List<string>();

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
                checkedForms++;

                var controllerType = assembly.GetType($"Monitor.Web.Controllers.{controllerName}Controller");
                if (controllerType is null)
                {
                    unresolved.Add($"{relative}: {controllerName}.{actionName} -> controller not found");
                    continue;
                }

                var candidates = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(method => string.Equals(method.Name, actionName, StringComparison.Ordinal))
                    .ToArray();
                if (candidates.Length == 0)
                {
                    unresolved.Add($"{relative}: {controllerName}.{actionName} -> action method not found");
                    continue;
                }

                if (!candidates.Any(method => method.GetCustomAttributes<HttpPostAttribute>().Any()))
                    unresolved.Add($"{relative}: {controllerName}.{actionName} -> action exists but is not HttpPost");
            }
        }

        Assert.True(checkedForms >= 10, $"Expected a meaningful visible POST-form surface, but only {checkedForms} tag-helper forms were discovered.");
        Assert.True(unresolved.Count == 0, "Unresolved Razor POST wiring:\n" + string.Join("\n", unresolved));
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
