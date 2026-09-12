using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Monitor.Web.Services;

public sealed record UpgradePackageInfo(
    string Version,
    string Sha256,
    string FileName,
    long SizeBytes,
    DateTimeOffset StagedAtUtc,
    string PackagePath);

public sealed record UpgradeApplyRequest(
    UpgradePackageInfo Package,
    string RequestedBy,
    DateTimeOffset RequestedAtUtc);

public sealed record UpgradeLastResult(
    string Version,
    string Status,
    string Message,
    DateTimeOffset CompletedAtUtc);

public sealed record UpgradeDashboardViewModel(
    UpgradePackageInfo? Staged,
    UpgradeApplyRequest? Pending,
    UpgradeLastResult? LastResult,
    string StagingRoot,
    long MaxUploadBytes);

internal sealed record MonitorUpgradeManifest(string Version, string? Notes = null);
public sealed record UpgradeStageResult(bool Success, string Message, UpgradePackageInfo? Package = null);

internal sealed class UpgradePackageStaging
{
    internal const long MaxUploadBytes = 250L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _root;
    private readonly TimeProvider _timeProvider;

    public UpgradePackageStaging(IWebHostEnvironment environment, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _root = ResolveRoot(environment);
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "incoming"));
        Directory.CreateDirectory(Path.Combine(_root, "staged"));
    }

    public string Root => _root;

    public UpgradeDashboardViewModel GetDashboard() => new(
        ReadJson<UpgradePackageInfo>(Path.Combine(_root, "staged-upgrade.json")),
        ReadJson<UpgradeApplyRequest>(Path.Combine(_root, "pending-upgrade.json")),
        ReadJson<UpgradeLastResult>(Path.Combine(_root, "last-upgrade-result.json")),
        _root,
        MaxUploadBytes);

    public async Task<UpgradeStageResult> StageAsync(Stream source, string originalFileName, long length, string expectedSha256, CancellationToken cancellationToken)
    {
        if (length <= 0 || length > MaxUploadBytes)
            return new(false, $"Package must be between 1 byte and {MaxUploadBytes / 1024 / 1024} MB.");
        if (!string.Equals(Path.GetExtension(originalFileName), ".zip", StringComparison.OrdinalIgnoreCase))
            return new(false, "Only .zip Monitor upgrade packages are accepted.");
        if (!TryNormalizeSha256(expectedSha256, out var expected))
            return new(false, "Expected SHA-256 must contain exactly 64 hexadecimal characters.");

        var temporary = Path.Combine(_root, "incoming", $"{Guid.NewGuid():N}.zip");
        try
        {
            await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[128 * 1024];
                long copied = 0;
                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read == 0) break;
                    copied += read;
                    if (copied > MaxUploadBytes) return new(false, "Package exceeded the upload limit.");
                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await target.FlushAsync(cancellationToken);
                var actual = Convert.ToHexString(hash.GetHashAndReset());
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    return new(false, $"SHA-256 mismatch. Actual package hash is {actual}.");
            }

            var manifest = ValidateArchiveAndReadManifest(temporary);
            if (!Version.TryParse(manifest.Version, out var parsedVersion))
                return new(false, "monitor-upgrade-manifest.json contains an invalid Version.");

            var canonicalVersion = parsedVersion.ToString();
            var targetDirectory = Path.Combine(_root, "staged", canonicalVersion);
            Directory.CreateDirectory(targetDirectory);
            var targetPath = Path.Combine(targetDirectory, expected + ".zip");
            File.Move(temporary, targetPath, overwrite: true);
            var info = new UpgradePackageInfo(
                canonicalVersion,
                expected,
                Path.GetFileName(originalFileName),
                new FileInfo(targetPath).Length,
                _timeProvider.GetUtcNow(),
                targetPath);
            WriteJsonAtomic(Path.Combine(_root, "staged-upgrade.json"), info);
            return new(true, $"Upgrade {canonicalVersion} staged and checksum-verified.", info);
        }
        catch (InvalidDataException exception)
        {
            return new(false, exception.Message);
        }
        catch (IOException)
        {
            return new(false, "Upgrade staging failed because the package store is unavailable.");
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    public UpgradeStageResult RequestApply(string actor)
    {
        var package = ReadJson<UpgradePackageInfo>(Path.Combine(_root, "staged-upgrade.json"));
        if (package is null || !File.Exists(package.PackagePath))
            return new(false, "No valid staged package is available.");
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package.PackagePath)));
        if (!string.Equals(actual, package.Sha256, StringComparison.OrdinalIgnoreCase))
            return new(false, "The staged package checksum changed. Restage the package before applying.");

        var request = new UpgradeApplyRequest(package, actor, _timeProvider.GetUtcNow());
        WriteJsonAtomic(Path.Combine(_root, "pending-upgrade.json"), request);
        return new(true, $"Upgrade {package.Version} is queued for the host updater. The running web process will not overwrite itself.", package);
    }

    internal static bool TryNormalizeSha256(string? value, out string normalized)
    {
        normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit);
    }

    private static MonitorUpgradeManifest ValidateArchiveAndReadManifest(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0 || archive.Entries.Count > 5000)
            throw new InvalidDataException("Upgrade package has an invalid entry count.");

        ZipArchiveEntry? manifestEntry = null;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains("../", StringComparison.Ordinal) || Path.IsPathRooted(name))
                throw new InvalidDataException("Upgrade package contains an unsafe archive path.");
            if (string.Equals(name, "monitor-upgrade-manifest.json", StringComparison.OrdinalIgnoreCase))
                manifestEntry = entry;
        }

        if (manifestEntry is null)
            throw new InvalidDataException("Upgrade package must contain monitor-upgrade-manifest.json at its root.");
        if (manifestEntry.Length > 64 * 1024)
            throw new InvalidDataException("Upgrade manifest is too large.");

        using var stream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize<MonitorUpgradeManifest>(stream, JsonOptions);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("Upgrade manifest is invalid.");
        return manifest;
    }

    private static string ResolveRoot(IWebHostEnvironment environment)
    {
        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(programData)) return Path.Combine(programData, "Monitor", "upgrades");
        }
        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "upgrades"));
    }

    private static T? ReadJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions); }
        catch (JsonException) { return default; }
        catch (IOException) { return default; }
    }

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }
}
