using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300EstateIdentityTests
{
    [Fact] public void B300_001_NormalizeName_CollapsesWhitespaceAndBounds() => Assert.Equal("SQL PROD 01", Batch300EstateIdentity.NormalizeName("  SQL   PROD  01  "));

    [Fact] public void B300_002_NormalizeTag_ProducesSafeLowercaseToken() => Assert.Equal("prod_west-1", Batch300EstateIdentity.NormalizeTag(" Prod_West-1! "));

    [Fact] public void B300_003_ParseVersion_AcceptsFourPartVersion()
    {
        var version = Batch300EstateIdentity.ParseVersion("16.0.4125.3");
        Assert.NotNull(version);
        Assert.Equal(new SqlVersionInfo(16, 0, 4125, 3), version);
    }

    [Fact] public void B300_004_MajorVersion_ReturnsNullForInvalidInput() => Assert.Null(Batch300EstateIdentity.MajorVersion("not-a-version"));

    [Fact] public void B300_005_VersionFamily_BucketsDeterministically()
    {
        Assert.Equal("17+", Batch300EstateIdentity.VersionFamily(17));
        Assert.Equal("legacy", Batch300EstateIdentity.VersionFamily(12));
    }

    [Fact] public void B300_006_ClassifyEdition_RecognizesEnterprise() => Assert.Equal(SqlEditionClass.Enterprise, Batch300EstateIdentity.ClassifyEdition("Enterprise Edition"));

    [Fact] public void B300_007_ClassifyUptime_RecognizesLongRunning() => Assert.Equal(UptimeBand.LongRunning, Batch300EstateIdentity.ClassifyUptime(31L * 24 * 3600));

    [Fact] public void B300_008_StableId_IsDeterministicAndOpaque()
    {
        var first = Batch300EstateIdentity.StableId("SQL01", "MSSQLSERVER");
        var second = Batch300EstateIdentity.StableId(" sql01 ", "mssqlserver");
        Assert.Equal(first, second);
        Assert.Equal(24, first.Length);
    }

    [Fact] public void B300_009_SafeDisplayLabel_OmitsEmptyInstance() => Assert.Equal("SQL01", Batch300EstateIdentity.SafeDisplayLabel("SQL01", " "));

    [Fact] public void B300_010_IsSupportedMajor_FailsClosedForLegacy()
    {
        Assert.True(Batch300EstateIdentity.IsSupportedMajor(16));
        Assert.False(Batch300EstateIdentity.IsSupportedMajor(12));
    }
}
