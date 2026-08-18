using System.Text.Json;
using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed class BackupAssemblyBudget
{
    private static readonly JsonSerializerOptions CompactJson = new(JsonSerializerDefaults.Web);
    private readonly long _maxBundleBytes;
    private long _serializedItemLowerBoundBytes;

    public BackupAssemblyBudget(int maxBundleBytes)
    {
        if (maxBundleBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBundleBytes));
        _maxBundleBytes = maxBundleBytes;
    }

    public T Admit<T>(T item)
    {
        var itemBytes = JsonSerializer.SerializeToUtf8Bytes(item, CompactJson).LongLength;
        if (itemBytes > _maxBundleBytes - _serializedItemLowerBoundBytes)
        {
            throw new InvalidOperationException("Operational backup exceeds the configured bundle size limit.");
        }

        _serializedItemLowerBoundBytes += itemBytes;
        return item;
    }

    public SnapshotHistoryPoint[] CollectHistory(
        IEnumerable<BackupRegistration> registrations,
        Func<Guid, IReadOnlyList<SnapshotHistoryPoint>> readHistory)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(readHistory);

        var points = new List<SnapshotHistoryPoint>();
        foreach (var registration in registrations)
        {
            foreach (var point in readHistory(registration.Id))
            {
                points.Add(Admit(point));
            }
        }

        return points
            .OrderBy(item => item.RegistrationId)
            .ThenBy(item => item.CollectedAtUtc)
            .ToArray();
    }
}
