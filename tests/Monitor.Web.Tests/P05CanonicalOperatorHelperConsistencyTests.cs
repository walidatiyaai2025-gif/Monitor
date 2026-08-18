using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05CanonicalOperatorHelperConsistencyTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void CanonicalP0Documents_AgreeOnCurrentRc61OperatorHelperBoundary()
    {
        var documents = new Dictionary<string, string>
        {
            ["STATUS"] = Read("docs/STATUS.md"),
            ["IMPLEMENTATION_PLAN"] = Read("docs/IMPLEMENTATION_PLAN.md"),
            ["PRODUCTION_MVP"] = Read("docs/PRODUCTION_MVP.md")
        };

        foreach (var (_, text) in documents)
        {
            Assert.Contains("RC.61", text, StringComparison.Ordinal);
            Assert.Contains("Invoke-Rc61DurablePromotion.ps1", text, StringComparison.Ordinal);
            Assert.Contains("READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT", text, StringComparison.Ordinal);
            Assert.Contains("-AcknowledgePromotion", text, StringComparison.Ordinal);
            Assert.Contains("PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED", text, StringComparison.Ordinal);
            Assert.Contains("IndependentVerificationCommand", text, StringComparison.Ordinal);
            Assert.Contains("do not redispatch", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Test-Rc61CutoverReadiness.ps1", text, StringComparison.Ordinal);
            Assert.Contains("ExternalGatesPassed = 0", text, StringComparison.Ordinal);
            Assert.Contains("#162 -> #116 -> #111", text, StringComparison.Ordinal);
            Assert.Contains("3cd711b608e4ceaf8872eb22a25541bbbfe2729a", text, StringComparison.Ordinal);
            Assert.Contains("0/15", text, StringComparison.Ordinal);
            Assert.Contains("no production mutation", text, StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain("promotion/verifier still have zero runs", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Current operator handoff: PR #271", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CanonicalP0Documents_KeepIndependentVerifierSeparateFromPromotion()
    {
        foreach (var path in new[]
        {
            "docs/STATUS.md",
            "docs/IMPLEMENTATION_PLAN.md",
            "docs/PRODUCTION_MVP.md"
        })
        {
            var text = Read(path);
            var preview = text.IndexOf("Invoke-Rc61DurablePromotion.ps1", StringComparison.Ordinal);
            var acknowledgement = text.IndexOf("-AcknowledgePromotion", StringComparison.Ordinal);
            var verifier = text.IndexOf("IndependentVerificationCommand", StringComparison.Ordinal);
            var readiness = text.IndexOf("Test-Rc61CutoverReadiness.ps1", StringComparison.Ordinal);

            Assert.True(preview >= 0 && preview <= acknowledgement && acknowledgement < verifier && verifier < readiness,
                $"{path} must preserve preview -> explicit acknowledgement -> separate verifier -> explicit run-ID readiness order.");
        }
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Monitor.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
