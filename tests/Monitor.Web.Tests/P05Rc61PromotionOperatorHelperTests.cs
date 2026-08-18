using Xunit;

namespace Monitor.Web.Tests;

public sealed class P05Rc61PromotionOperatorHelperTests
{
    private static readonly string Root = FindRoot();
    private const string HelperPath = "scripts/Invoke-Rc61DurablePromotion.ps1";
    private const string SafetyPath = "scripts/Test-Rc61DurablePromotionOperatorSafety.ps1";
    private const string HandoffPath = "deploy/RC61_PROMOTION_OPERATOR.md";

    [Fact]
    public void Helper_RunsLockedPreflightBeforeAnyDispatchAndPinsSelectedRc61()
    {
        var helper = Read(HelperPath);

        Assert.Contains("Test-Rc61DurablePromotionPreflight.ps1", helper, StringComparison.Ordinal);
        Assert.Contains("Assert-LockedPreflight", helper, StringComparison.Ordinal);
        Assert.True(helper.IndexOf("$preflight = & $preflightScript", StringComparison.Ordinal) <
                    helper.IndexOf("$dispatchArguments = @(", StringComparison.Ordinal));

        foreach (var value in new[]
        {
            "walidatiyaai2025-gif/Monitor",
            "1329517438",
            "0.1.0-rc.61",
            "31667721306",
            "9168574442",
            "sha256:1c499b9eb0bfc4245716c14718381b71352df8392aafe430cc415b375b93f382",
            "d0a71f8a5611621ee388a1109dedc76e1a6e70357404cb62c9c7aa188f49c3d5",
            "e28158da67b36dfc5dbf8f4c38b5c43d99c7c728",
            "158148d8bfd05f724014541bc7a0b1eab5dae1b5",
            "v0.1.0-rc.61"
        })
        {
            Assert.Contains(value, helper, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Helper_RequiresExplicitAcknowledgementAndDispatchesOnlyPromotionWorkflow()
    {
        var helper = Read(HelperPath);

        Assert.Contains("[switch]$AcknowledgePromotion", helper, StringComparison.Ordinal);
        Assert.Contains("READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT", helper, StringComparison.Ordinal);
        Assert.Contains("WorkflowDispatchPerformed = $false", helper, StringComparison.Ordinal);
        Assert.Contains("'workflow', 'run', $promotionWorkflow", helper, StringComparison.Ordinal);
        Assert.Contains("'-f', 'acknowledge_promotion=true'", helper, StringComparison.Ordinal);
        Assert.Contains("$promotionWorkflow = 'promote-existing-candidate.yml'", helper, StringComparison.Ordinal);

        Assert.DoesNotContain("'workflow', 'run', 'verify-durable-release.yml'", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release create", helper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--method POST", helper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git tag", helper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git push", helper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Deploy-ProductionSingleNode.ps1", helper, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Helper_CapturesExactRunUrlOrFailsClosedOnDiscoveryAmbiguity()
    {
        var helper = Read(HelperPath);

        Assert.Contains("actions/runs/(?<id>[1-9][0-9]*)", helper, StringComparison.Ordinal);
        Assert.Contains("Get-PromotionRunSnapshot", helper, StringComparison.Ordinal);
        Assert.Contains("Resolve-NewPromotionRunId", helper, StringComparison.Ordinal);
        Assert.Contains("Do not redispatch; inspect these exact runs", helper, StringComparison.Ordinal);
        Assert.Contains("Do not redispatch; inspect recent promote-existing-candidate", helper, StringComparison.Ordinal);
        Assert.Contains("Assert-PromotionRunIdentity", helper, StringComparison.Ordinal);
        Assert.Contains("$promotionRun.actor.login", helper, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch", helper, StringComparison.Ordinal);
        Assert.Contains("head_branch -cne 'main'", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Helper_WaitsOnExactRunAndNeverTreatsFailureAsPermissionToRedispatch()
    {
        var helper = Read(HelperPath);

        Assert.Contains("RunCompletionAttempts", helper, StringComparison.Ordinal);
        Assert.Contains("PROMOTION_DISPATCHED_CHECK_EXACT_RUN", helper, StringComparison.Ordinal);
        Assert.Contains("RedispatchAllowed = $false", helper, StringComparison.Ordinal);
        Assert.Contains("completed with conclusion", helper, StringComparison.Ordinal);
        Assert.Contains("Do not redispatch; inspect", helper, StringComparison.Ordinal);
        Assert.Contains("PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED", helper, StringComparison.Ordinal);
        Assert.Contains("IndependentVerificationDispatched = $false", helper, StringComparison.Ordinal);
        Assert.Contains("PostVerificationReadinessCommand", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSafety_CoversPreviewSuccessFallbackAmbiguityFailureAndExistingState()
    {
        var safety = Read(SafetyPath);

        foreach (var marker in new[]
        {
            "READY_FOR_EXPLICIT_PROMOTION_ACKNOWLEDGEMENT",
            "PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED",
            "fallback",
            "ambiguous",
            "promotion-failure",
            "existing-state",
            "Assert-NoVerificationDispatch",
            "DispatchCount -ne 1",
            "IndependentVerifierAutoDispatches = 0"
        })
        {
            Assert.Contains(marker, safety, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Handoff_PreservesSeparateManualIndependentVerificationBoundary()
    {
        var handoff = Read(HandoffPath);

        Assert.Contains("Invoke-Rc61DurablePromotion.ps1", handoff, StringComparison.Ordinal);
        Assert.Contains("-AcknowledgePromotion", handoff, StringComparison.Ordinal);
        Assert.Contains("PROMOTION_SUCCEEDED_INDEPENDENT_VERIFICATION_REQUIRED", handoff, StringComparison.Ordinal);
        Assert.Contains("IndependentVerificationCommand", handoff, StringComparison.Ordinal);
        Assert.Contains("Test-Rc61CutoverReadiness.ps1", handoff, StringComparison.Ordinal);
        Assert.Contains("does not dispatch the independent verifier", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not redispatch", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not authorize IIS or SQL mutation", handoff, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln"))) return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not locate Monitor.sln for RC.61 promotion operator helper tests.");
    }
}
