using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300OperatorSafetyTests
{
    [Fact] public void B300_071_NormalizeText_RemovesControlsAndCollapsesWhitespace() => Assert.Equal("hello world", Batch300OperatorSafety.NormalizeText(" hello\t\n world "));

    [Fact] public void B300_072_LooksSecretBearing_DetectsConnectionShapedMaterial() => Assert.True(Batch300OperatorSafety.LooksSecretBearing("Password=abc"));

    [Fact] public void B300_073_SafeNote_RejectsSecretMaterial() => Assert.Throws<ArgumentException>(() => Batch300OperatorSafety.SafeNote("token=abc"));

    [Fact] public void B300_074_IsSafeRouteId_RejectsSlashes()
    {
        Assert.True(Batch300OperatorSafety.IsSafeRouteId("rule-1_prod"));
        Assert.False(Batch300OperatorSafety.IsSafeRouteId("../rule"));
    }

    [Fact] public void B300_075_SafeFileName_RemovesUnsafeCharacters() => Assert.Equal("monitor-report.csv", Batch300OperatorSafety.SafeFileName(" monitor-report.csv!? "));

    [Fact] public void B300_076_FormulaSafeCell_NeutralizesFormula() => Assert.Equal("'=SUM(A1:A2)", Batch300OperatorSafety.FormulaSafeCell("=SUM(A1:A2)"));

    [Fact] public void B300_077_CorrelationId_PreservesSafeIncomingValue() => Assert.Equal("req-123", Batch300OperatorSafety.CorrelationId("req-123"));

    [Fact] public void B300_078_Fingerprint_IsStableAndOpaque()
    {
        var first = Batch300OperatorSafety.Fingerprint(" Operator note ");
        var second = Batch300OperatorSafety.Fingerprint("operator note");
        Assert.Equal(first, second);
        Assert.Equal(24, first.Length);
    }

    [Fact] public void B300_079_IsAllowedDiagnosticsEntry_IsAllowlisted()
    {
        Assert.True(Batch300OperatorSafety.IsAllowedDiagnosticsEntry("manifest.json"));
        Assert.False(Batch300OperatorSafety.IsAllowedDiagnosticsEntry("../../secret.txt"));
    }

    [Fact] public void B300_080_RedactValue_RedactsSensitiveKeys() => Assert.Equal("[redacted]", Batch300OperatorSafety.RedactValue("ConnectionString", "Server=x"));
}
