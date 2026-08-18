using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class BoundedBackupDirectoryTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReadinessScan_CountsFullSequenceWhileRetainingOnlyNewestFive()
    {
        var files = Enumerable.Range(0, 10_000).Select(CreateSyntheticFile);

        var scan = BoundedBackupDirectory.ScanReadiness(files, recentLimit: 5);

        Assert.Equal(10_000, scan.Count);
        Assert.Equal(5, scan.RecentBackups.Count);
        Assert.Equal(
            ["backup-09999", "backup-09998", "backup-09997", "backup-09996", "backup-09995"],
            scan.RecentBackups.Select(item => item.BackupId).ToArray());
    }

    [Fact]
    public void RetentionSelection_NeverBuffersMoreThanConfiguredLimit()
    {
        const int retention = 10;
        var retained = new List<BackupDirectoryFile>(retention);
        var peakBuffered = 0;
        var evicted = 0;

        foreach (var candidate in Enumerable.Range(0, 10_000).Select(CreateSyntheticFile))
        {
            if (BoundedBackupDirectory.ConsiderForRetention(retained, candidate, retention) is not null)
            {
                evicted++;
            }
            peakBuffered = Math.Max(peakBuffered, retained.Count);
        }

        Assert.Equal(retention, peakBuffered);
        Assert.Equal(9_990, evicted);
        Assert.Equal(retention, retained.Count);
        Assert.Equal(
            Enumerable.Range(9_990, 10).Reverse().Select(index => $"backup-{index:D5}").ToArray(),
            retained.Select(item => item.BackupId).ToArray());
    }

    [Fact]
    public void DirectoryPrune_LeavesExactlyConfiguredFiles_AndReadinessRemainsBounded()
    {
        using var directory = new TempDirectory();
        for (var index = 0; index < 250; index++)
        {
            var path = Path.Combine(directory.Path, $"monitor-backup-backup-{index:D5}.json");
            File.WriteAllText(path, "{}");
            File.SetLastWriteTimeUtc(path, Epoch.UtcDateTime.AddSeconds(index));
        }

        BoundedBackupDirectory.Prune(directory.Path, retentionCount: 7);
        var scan = BoundedBackupDirectory.ScanReadiness(directory.Path, recentLimit: 5);

        Assert.Equal(7, Directory.GetFiles(directory.Path, "monitor-backup-*.json").Length);
        Assert.Equal(7, scan.Count);
        Assert.Equal(5, scan.RecentBackups.Count);
        Assert.Equal("backup-00249", scan.RecentBackups[0].BackupId);
    }

    private static BackupDirectoryFile CreateSyntheticFile(int index)
    {
        var fileName = $"monitor-backup-backup-{index:D5}.json";
        var timestamp = Epoch.AddSeconds(index);
        return new BackupDirectoryFile(
            $"/backups/{fileName}",
            fileName,
            $"backup-{index:D5}",
            timestamp,
            timestamp.UtcDateTime,
            index + 1L);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"monitor-backup-directory-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
