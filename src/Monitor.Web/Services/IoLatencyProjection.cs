using Monitor.Web.Models;

namespace Monitor.Web.Services;

public static class IoLatencyProjection
{
    private const double BytesPerMb = 1024d * 1024d;

    public static IReadOnlyList<IoFileIntelligence> Build(
        StorageHealthSnapshot? storage,
        long? uptimeSeconds,
        int limit = 8)
    {
        if (storage?.IoFiles is not { Count: > 0 } files || uptimeSeconds is null or <= 0)
        {
            return [];
        }

        var seconds = uptimeSeconds.Value;
        var samples = files.Select(file => new IoFileSample(
            file.FileKey,
            file.Reads > 0 ? file.ReadStallMs / (double)file.Reads : 0,
            file.Writes > 0 ? file.WriteStallMs / (double)file.Writes : 0,
            file.BytesRead / BytesPerMb / seconds,
            file.BytesWritten / BytesPerMb / seconds,
            file.Reads,
            file.Writes));

        return Batch400IoLatency.TopFiles(samples, Math.Clamp(limit, 1, 20));
    }
}
