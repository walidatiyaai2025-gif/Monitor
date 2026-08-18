namespace Monitor.Web.Services;

internal sealed record BackupDirectoryFile(
    string Path,
    string FileName,
    string BackupId,
    DateTimeOffset LastWriteTimeUtc,
    DateTime CreationTimeUtc,
    long SizeBytes);

internal sealed record BackupDirectoryScan(
    int Count,
    IReadOnlyList<BackupListItem> RecentBackups);

internal static class BoundedBackupDirectory
{
    private const string Pattern = "monitor-backup-*.json";
    private const string Prefix = "monitor-backup-";

    public static BackupDirectoryScan ScanReadiness(string root, int recentLimit)
    {
        if (recentLimit < 1) throw new ArgumentOutOfRangeException(nameof(recentLimit));
        if (!Directory.Exists(root)) return new(0, []);
        return ScanReadiness(Enumerate(root), recentLimit);
    }

    internal static BackupDirectoryScan ScanReadiness(IEnumerable<BackupDirectoryFile> files, int recentLimit)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (recentLimit < 1) throw new ArgumentOutOfRangeException(nameof(recentLimit));

        var count = 0;
        var recent = new List<BackupDirectoryFile>(recentLimit);
        foreach (var file in files)
        {
            if (count < int.MaxValue) count++;
            InsertBounded(recent, file, recentLimit, CompareReadinessNewestFirst);
        }

        return new(
            count,
            recent.Select(file => new BackupListItem(file.BackupId, file.LastWriteTimeUtc, file.SizeBytes)).ToArray());
    }

    public static void Prune(string root, int retentionCount)
    {
        if (retentionCount < 1) throw new ArgumentOutOfRangeException(nameof(retentionCount));
        if (!Directory.Exists(root)) return;

        var retained = new List<BackupDirectoryFile>(retentionCount);
        foreach (var candidate in Enumerate(root))
        {
            var evicted = ConsiderForRetention(retained, candidate, retentionCount);
            if (evicted is not null)
            {
                File.Delete(evicted.Path);
            }
        }
    }

    internal static BackupDirectoryFile? ConsiderForRetention(
        List<BackupDirectoryFile> retained,
        BackupDirectoryFile candidate,
        int retentionCount)
    {
        ArgumentNullException.ThrowIfNull(retained);
        ArgumentNullException.ThrowIfNull(candidate);
        if (retentionCount < 1) throw new ArgumentOutOfRangeException(nameof(retentionCount));
        if (retained.Count > retentionCount)
            throw new InvalidOperationException("Backup retention buffer already exceeds its configured bound.");

        var index = FindInsertIndex(retained, candidate, CompareRetentionNewestFirst);
        if (retained.Count < retentionCount)
        {
            retained.Insert(index, candidate);
            return null;
        }

        if (index >= retentionCount)
        {
            return candidate;
        }

        var evicted = retained[^1];
        retained.RemoveAt(retained.Count - 1);
        retained.Insert(index, candidate);
        return evicted;
    }

    private static IEnumerable<BackupDirectoryFile> Enumerate(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, Pattern, SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(path);
            var fileName = info.Name;
            var backupId = Path.GetFileNameWithoutExtension(fileName)[Prefix.Length..];
            yield return new BackupDirectoryFile(
                path,
                fileName,
                backupId,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                info.CreationTimeUtc,
                info.Length);
        }
    }

    private static void InsertBounded(
        List<BackupDirectoryFile> buffer,
        BackupDirectoryFile candidate,
        int limit,
        Comparison<BackupDirectoryFile> comparison)
    {
        var index = FindInsertIndex(buffer, candidate, comparison);
        if (buffer.Count < limit)
        {
            buffer.Insert(index, candidate);
            return;
        }

        if (index >= limit) return;
        buffer.RemoveAt(buffer.Count - 1);
        buffer.Insert(index, candidate);
    }

    private static int FindInsertIndex(
        List<BackupDirectoryFile> buffer,
        BackupDirectoryFile candidate,
        Comparison<BackupDirectoryFile> comparison)
    {
        for (var index = 0; index < buffer.Count; index++)
        {
            if (comparison(candidate, buffer[index]) < 0) return index;
        }
        return buffer.Count;
    }

    private static int CompareReadinessNewestFirst(BackupDirectoryFile left, BackupDirectoryFile right)
    {
        var timestamp = right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
        return timestamp != 0 ? timestamp : string.Compare(right.FileName, left.FileName, StringComparison.Ordinal);
    }

    private static int CompareRetentionNewestFirst(BackupDirectoryFile left, BackupDirectoryFile right)
    {
        var timestamp = right.CreationTimeUtc.CompareTo(left.CreationTimeUtc);
        return timestamp != 0 ? timestamp : string.Compare(right.FileName, left.FileName, StringComparison.Ordinal);
    }
}
