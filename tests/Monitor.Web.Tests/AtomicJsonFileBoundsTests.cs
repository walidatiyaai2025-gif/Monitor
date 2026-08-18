using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class AtomicJsonFileBoundsTests : IDisposable
{
    private const int TestMaxDocumentBytes = 1024;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"monitor-operational-json-bounds-{Guid.NewGuid():N}");

    [Fact]
    public void OversizedRawOperationalFile_FailsClosedBeforeJsonParsing()
    {
        Directory.CreateDirectory(_directory);
        var path = StorePath();
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(TestMaxDocumentBytes + 1L);
        }

        var exception = Assert.Throws<InvalidDataException>(() =>
            AtomicJsonFile.Load<TestEnvelope>(path, TestMaxDocumentBytes));

        Assert.Contains("bounded file size", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OversizedOperationalCandidate_PreservesLastGoodDurableFile()
    {
        Directory.CreateDirectory(_directory);
        var path = StorePath();
        AtomicJsonFile.Save(path, new TestEnvelope("stable"), TestMaxDocumentBytes);
        var lastGood = File.ReadAllBytes(path);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AtomicJsonFile.Save(path, new TestEnvelope(new string('x', TestMaxDocumentBytes * 2)), TestMaxDocumentBytes));

        Assert.Contains("bounded file size", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(lastGood, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp", SearchOption.TopDirectoryOnly));
        Assert.Equal("stable", AtomicJsonFile.Load<TestEnvelope>(path, TestMaxDocumentBytes)!.Payload);
    }

    [Fact]
    public void ValidBoundedOperationalDocument_RoundTrips()
    {
        Directory.CreateDirectory(_directory);
        var path = StorePath();

        AtomicJsonFile.Save(path, new TestEnvelope("bounded"), TestMaxDocumentBytes);
        var loaded = AtomicJsonFile.Load<TestEnvelope>(path, TestMaxDocumentBytes);

        Assert.NotNull(loaded);
        Assert.Equal("bounded", loaded!.Payload);
        Assert.InRange(new FileInfo(path).Length, 1, TestMaxDocumentBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private string StorePath() => Path.Combine(_directory, "operational.json");

    private sealed record TestEnvelope(string Payload);
}
