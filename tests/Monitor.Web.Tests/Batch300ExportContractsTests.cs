using System.Text;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class Batch300ExportContractsTests
{
    [Fact] public void B300_081_ClampRowCount_BoundsRequests()
    {
        Assert.Equal(1, Batch300ExportContracts.ClampRowCount(0));
        Assert.Equal(Batch300ExportContracts.MaxRows, Batch300ExportContracts.ClampRowCount(int.MaxValue));
    }

    [Fact] public void B300_082_NormalizeLineEndings_UsesLfOnly() => Assert.Equal("a\nb\nc", Batch300ExportContracts.NormalizeLineEndings("a\r\nb\rc"));

    [Fact] public void B300_083_EscapeCsv_QuotesAndNeutralizesFormula() => Assert.Equal("\"'=1+1\"", Batch300ExportContracts.EscapeCsv("=1+1"));

    [Fact] public void B300_084_Csv_EmitsSchemaAndUtf8Bom()
    {
        var bytes = Batch300ExportContracts.Csv(["Name"], [new string?[] { "SQL01" }]);
        var preamble = Encoding.UTF8.GetPreamble();
        Assert.True(bytes.AsSpan().StartsWith(preamble));
        var text = Encoding.UTF8.GetString(bytes[preamble.Length..]);
        Assert.StartsWith("#schema,monitor-b300-v1\n", text, StringComparison.Ordinal);
    }

    [Fact] public void B300_085_Checksum_IsStableSha256()
    {
        var checksum = Batch300ExportContracts.Checksum([1, 2, 3]);
        Assert.Equal(64, checksum.Length);
        Assert.Equal(checksum, Batch300ExportContracts.Checksum([1, 2, 3]));
    }

    [Fact] public void B300_086_Manifest_DescribesBoundedContract()
    {
        var manifest = Batch300ExportContracts.Manifest();
        Assert.Equal(Batch300ExportContracts.SchemaVersion, manifest.Schema);
        Assert.Equal("SHA-256", manifest.ChecksumAlgorithm);
    }

    [Fact] public void B300_087_ManifestJson_IsBoundedAndVersioned()
    {
        var json = Encoding.UTF8.GetString(Batch300ExportContracts.ManifestJson());
        Assert.Contains(Batch300ExportContracts.SchemaVersion, json, StringComparison.Ordinal);
        Assert.True(json.Length < 1024);
    }

    [Fact] public void B300_088_SafeDownloadName_UsesUtcTimestampAndSafeSubject()
    {
        var name = Batch300ExportContracts.SafeDownloadName("fleet report!?", new DateTimeOffset(2026, 8, 11, 1, 2, 3, TimeSpan.Zero));
        Assert.Equal("monitor-fleetreport-20260811-010203.csv", name);
    }

    [Fact] public void B300_089_DeterministicSort_IsCaseStable()
    {
        var values = Batch300ExportContracts.DeterministicSort(["b", "A", "a"]);
        Assert.Equal(["A", "a", "b"], values);
    }

    [Fact] public void B300_090_BoundedJson_RejectsOversizedPayload()
    {
        var payload = new string('x', Batch300ExportContracts.MaxBytes + 100);
        Assert.Throws<InvalidOperationException>(() => Batch300ExportContracts.BoundedJson(payload));
    }
}
