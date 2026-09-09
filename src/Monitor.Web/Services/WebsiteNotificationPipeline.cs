using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace Monitor.Web.Services;

public sealed class WebsiteNotificationOptions
{
    public const string SectionName = "WebsiteNotifications";
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 25;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string PasswordEnvironmentVariable { get; set; } = "MONITOR_WEBSITE_SMTP_PASSWORD";
    public int DeliveryTickSeconds { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;

    public void Validate()
    {
        if (SmtpPort is < 1 or > 65535) throw new InvalidOperationException("WebsiteNotifications:SmtpPort must be between 1 and 65535.");
        if (DeliveryTickSeconds is < 1 or > 60) throw new InvalidOperationException("WebsiteNotifications:DeliveryTickSeconds must be between 1 and 60.");
        if (MaxAttempts is < 1 or > 10) throw new InvalidOperationException("WebsiteNotifications:MaxAttempts must be between 1 and 10.");
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(SmtpHost) || SmtpHost.Length > 253) throw new InvalidOperationException("WebsiteNotifications:SmtpHost is required when email notifications are enabled.");
        if (!WebsiteNotificationValidation.IsEmail(FromAddress)) throw new InvalidOperationException("WebsiteNotifications:FromAddress must be a valid email address.");
        if (!string.IsNullOrWhiteSpace(Username) && (string.IsNullOrWhiteSpace(PasswordEnvironmentVariable) || PasswordEnvironmentVariable.Length > 120))
            throw new InvalidOperationException("An environment-variable secret reference is required when SMTP username authentication is configured.");
    }
}

public sealed record WebsiteNotificationGroup(string Id, string Name, IReadOnlyList<string> Recipients, bool IsEnabled = true);

public static class WebsiteNotificationValidation
{
    public const int MaxGroups = 100;
    public const int MaxRecipientsPerGroup = 50;

    public static void ValidateGroup(WebsiteNotificationGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (string.IsNullOrWhiteSpace(group.Id) || group.Id.Length > 80 || group.Id.Any(char.IsWhiteSpace))
            throw new ArgumentException("Notification group id must contain 1-80 non-whitespace characters.", nameof(group));
        if (string.IsNullOrWhiteSpace(group.Name) || group.Name.Length > 120)
            throw new ArgumentException("Notification group name must contain 1-120 characters.", nameof(group));
        if (group.Recipients is null || group.Recipients.Count is < 1 or > MaxRecipientsPerGroup)
            throw new ArgumentException($"Notification group must contain 1-{MaxRecipientsPerGroup} recipients.", nameof(group));
        if (group.Recipients.Any(address => !IsEmail(address)))
            throw new ArgumentException("Notification group contains an invalid email address.", nameof(group));
        if (group.Recipients.Distinct(StringComparer.OrdinalIgnoreCase).Count() != group.Recipients.Count)
            throw new ArgumentException("Notification group recipients must be unique.", nameof(group));
    }

    public static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254) return false;
        try
        {
            var parsed = new MailAddress(value.Trim());
            return string.Equals(parsed.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public interface IWebsiteNotificationGroupStore
{
    IReadOnlyList<WebsiteNotificationGroup> GetAll();
    WebsiteNotificationGroup? Get(string id);
    void Upsert(WebsiteNotificationGroup group);
    bool Remove(string id);
}

public sealed class InMemoryWebsiteNotificationGroupStore : IWebsiteNotificationGroupStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, WebsiteNotificationGroup> _groups = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<WebsiteNotificationGroup> GetAll()
    {
        lock (_gate) return _groups.Values.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public WebsiteNotificationGroup? Get(string id)
    {
        lock (_gate) return _groups.TryGetValue(id, out var group) ? group : null;
    }

    public void Upsert(WebsiteNotificationGroup group)
    {
        WebsiteNotificationValidation.ValidateGroup(group);
        lock (_gate)
        {
            if (!_groups.ContainsKey(group.Id) && _groups.Count >= WebsiteNotificationValidation.MaxGroups)
                throw new InvalidOperationException("Website notification group capacity has been reached.");
            _groups[group.Id] = Normalize(group);
        }
    }

    public bool Remove(string id)
    {
        lock (_gate) return _groups.Remove(id);
    }

    internal static WebsiteNotificationGroup Normalize(WebsiteNotificationGroup group) => group with
    {
        Id = group.Id.Trim(),
        Name = group.Name.Trim(),
        Recipients = group.Recipients.Select(address => address.Trim()).ToArray()
    };
}

public sealed class FileWebsiteNotificationGroupStore : IWebsiteNotificationGroupStore
{
    private const int CurrentFormatVersion = 1;
    private const int MaxDocumentBytes = 2 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _leasePath;

    public FileWebsiteNotificationGroupStore(string path)
    {
        _path = Path.GetFullPath(path);
        _leasePath = $"{_path}.lock";
        using var lease = AcquireLease();
        _ = Load();
    }

    public IReadOnlyList<WebsiteNotificationGroup> GetAll()
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            return Load().Values.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public WebsiteNotificationGroup? Get(string id)
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            return Load().TryGetValue(id, out var group) ? group : null;
        }
    }

    public void Upsert(WebsiteNotificationGroup group)
    {
        WebsiteNotificationValidation.ValidateGroup(group);
        var normalized = InMemoryWebsiteNotificationGroupStore.Normalize(group);
        lock (_gate)
        {
            using var lease = AcquireLease();
            var groups = Load();
            if (!groups.ContainsKey(normalized.Id) && groups.Count >= WebsiteNotificationValidation.MaxGroups)
                throw new InvalidOperationException("Website notification group capacity has been reached.");
            groups[normalized.Id] = normalized;
            Persist(groups.Values);
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            var groups = Load();
            if (!groups.Remove(id)) return false;
            Persist(groups.Values);
            return true;
        }
    }

    private Dictionary<string, WebsiteNotificationGroup> Load()
    {
        var envelope = AtomicJsonFile.Load<GroupEnvelope>(_path, MaxDocumentBytes);
        if (envelope is null) return new(StringComparer.OrdinalIgnoreCase);
        if (envelope.Version != CurrentFormatVersion || envelope.Groups is null || envelope.Groups.Length > WebsiteNotificationValidation.MaxGroups)
            throw new InvalidDataException("Website notification group store format or capacity is invalid.");
        var groups = new Dictionary<string, WebsiteNotificationGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in envelope.Groups)
        {
            try { WebsiteNotificationValidation.ValidateGroup(group); }
            catch (ArgumentException exception) { throw new InvalidDataException("Website notification group store contains invalid group metadata.", exception); }
            var normalized = InMemoryWebsiteNotificationGroupStore.Normalize(group);
            if (!groups.TryAdd(normalized.Id, normalized)) throw new InvalidDataException("Website notification group store contains duplicate ids.");
        }
        return groups;
    }

    private void Persist(IEnumerable<WebsiteNotificationGroup> groups) =>
        AtomicJsonFile.Save(_path, new GroupEnvelope(CurrentFormatVersion, groups.OrderBy(group => group.Id, StringComparer.OrdinalIgnoreCase).ToArray()), MaxDocumentBytes);
    private FileStream AcquireLease() => CrossProcessFileLease.Acquire(_leasePath, "Website notification group store");
    private sealed record GroupEnvelope(int Version, WebsiteNotificationGroup[]? Groups);
}

public enum WebsiteNotificationKind
{
    IncidentOpened,
    IncidentReopened,
    IncidentRecovered
}

public enum WebsiteNotificationDeliveryStatus
{
    Pending,
    Sent,
    DeadLetter
}

public sealed record WebsiteNotificationOutboxItem(
    string Id,
    string DedupKey,
    Guid TargetId,
    string IncidentId,
    WebsiteNotificationKind Kind,
    string[] Recipients,
    string Subject,
    string Body,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset NextAttemptUtc,
    int Attempts,
    WebsiteNotificationDeliveryStatus Status,
    string? LeaseToken,
    DateTimeOffset? LeaseUntilUtc,
    string? LastError);

public sealed record WebsiteNotificationClaim(string ItemId, string Token, WebsiteNotificationOutboxItem Item);

public interface IWebsiteNotificationOutbox
{
    bool Enqueue(WebsiteNotificationOutboxItem item);
    WebsiteNotificationClaim? TryClaimDue(DateTimeOffset nowUtc, TimeSpan leaseDuration);
    bool MarkSent(WebsiteNotificationClaim claim, DateTimeOffset sentAtUtc);
    bool MarkFailed(WebsiteNotificationClaim claim, DateTimeOffset nowUtc, int maxAttempts, string error);
    IReadOnlyList<WebsiteNotificationOutboxItem> Snapshot();
}

public sealed class FileWebsiteNotificationOutbox : IWebsiteNotificationOutbox
{
    private const int CurrentFormatVersion = 1;
    private const int MaxDocumentBytes = 16 * 1024 * 1024;
    public const int MaxEntries = 2000;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _leasePath;

    public FileWebsiteNotificationOutbox(string path)
    {
        _path = Path.GetFullPath(path);
        _leasePath = $"{_path}.lock";
        using var lease = AcquireLease();
        _ = Load();
    }

    public bool Enqueue(WebsiteNotificationOutboxItem item)
    {
        ValidateItem(item);
        lock (_gate)
        {
            using var lease = AcquireLease();
            var items = Load();
            if (items.Any(existing => string.Equals(existing.DedupKey, item.DedupKey, StringComparison.Ordinal))) return false;
            if (items.Count >= MaxEntries)
            {
                var removable = items.Where(existing => existing.Status != WebsiteNotificationDeliveryStatus.Pending)
                    .OrderBy(existing => existing.CreatedAtUtc).FirstOrDefault();
                if (removable is null) throw new InvalidOperationException("Website notification outbox capacity has been reached.");
                items.Remove(removable);
            }
            items.Add(item);
            Persist(items);
            return true;
        }
    }

    public WebsiteNotificationClaim? TryClaimDue(DateTimeOffset nowUtc, TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        lock (_gate)
        {
            using var lease = AcquireLease();
            var items = Load();
            var index = items.FindIndex(item => item.Status == WebsiteNotificationDeliveryStatus.Pending &&
                item.NextAttemptUtc <= nowUtc && (item.LeaseUntilUtc is null || item.LeaseUntilUtc <= nowUtc));
            if (index < 0) return null;
            var token = Guid.NewGuid().ToString("N");
            var claimed = items[index] with { LeaseToken = token, LeaseUntilUtc = nowUtc + leaseDuration };
            items[index] = claimed;
            Persist(items);
            return new WebsiteNotificationClaim(claimed.Id, token, claimed);
        }
    }

    public bool MarkSent(WebsiteNotificationClaim claim, DateTimeOffset sentAtUtc) => MutateClaim(claim, current => current with
    {
        Status = WebsiteNotificationDeliveryStatus.Sent,
        LeaseToken = null,
        LeaseUntilUtc = null,
        LastError = null,
        NextAttemptUtc = sentAtUtc
    });

    public bool MarkFailed(WebsiteNotificationClaim claim, DateTimeOffset nowUtc, int maxAttempts, string error)
    {
        if (maxAttempts is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        var boundedError = Bound(error, 300);
        return MutateClaim(claim, current =>
        {
            var attempts = Math.Min(10, current.Attempts + 1);
            var dead = attempts >= maxAttempts;
            var retryDelay = TimeSpan.FromSeconds(Math.Min(900, 15 * (1 << Math.Min(5, attempts - 1))));
            return current with
            {
                Attempts = attempts,
                Status = dead ? WebsiteNotificationDeliveryStatus.DeadLetter : WebsiteNotificationDeliveryStatus.Pending,
                NextAttemptUtc = dead ? nowUtc : nowUtc + retryDelay,
                LeaseToken = null,
                LeaseUntilUtc = null,
                LastError = boundedError
            };
        });
    }

    public IReadOnlyList<WebsiteNotificationOutboxItem> Snapshot()
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            return Load().OrderByDescending(item => item.CreatedAtUtc).ToArray();
        }
    }

    private bool MutateClaim(WebsiteNotificationClaim claim, Func<WebsiteNotificationOutboxItem, WebsiteNotificationOutboxItem> mutation)
    {
        ArgumentNullException.ThrowIfNull(claim);
        lock (_gate)
        {
            using var lease = AcquireLease();
            var items = Load();
            var index = items.FindIndex(item => string.Equals(item.Id, claim.ItemId, StringComparison.Ordinal));
            if (index < 0 || !string.Equals(items[index].LeaseToken, claim.Token, StringComparison.Ordinal)) return false;
            items[index] = mutation(items[index]);
            Persist(items);
            return true;
        }
    }

    private List<WebsiteNotificationOutboxItem> Load()
    {
        var envelope = AtomicJsonFile.Load<OutboxEnvelope>(_path, MaxDocumentBytes);
        if (envelope is null) return [];
        if (envelope.Version != CurrentFormatVersion || envelope.Items is null || envelope.Items.Length > MaxEntries)
            throw new InvalidDataException("Website notification outbox format or capacity is invalid.");
        foreach (var item in envelope.Items) ValidateItem(item);
        if (envelope.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != envelope.Items.Length ||
            envelope.Items.Select(item => item.DedupKey).Distinct(StringComparer.Ordinal).Count() != envelope.Items.Length)
            throw new InvalidDataException("Website notification outbox contains duplicate identities.");
        return envelope.Items.OrderBy(item => item.CreatedAtUtc).ToList();
    }

    private static void ValidateItem(WebsiteNotificationOutboxItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 80 || string.IsNullOrWhiteSpace(item.DedupKey) || item.DedupKey.Length > 80 ||
            item.TargetId == Guid.Empty || string.IsNullOrWhiteSpace(item.IncidentId) || item.IncidentId.Length > 180 ||
            !Enum.IsDefined(item.Kind) || item.Recipients is null || item.Recipients.Length is < 1 or > 100 ||
            item.Recipients.Any(address => !WebsiteNotificationValidation.IsEmail(address)) || item.Subject.Length is < 1 or > 200 ||
            item.Body.Length is < 1 or > 4000 || item.CreatedAtUtc == default || item.NextAttemptUtc == default ||
            item.Attempts is < 0 or > 10 || !Enum.IsDefined(item.Status) || item.LeaseToken is { Length: > 64 } ||
            (item.LeaseToken is null) != (item.LeaseUntilUtc is null) || item.LastError is { Length: > 300 })
            throw new InvalidDataException("Website notification outbox contains invalid bounded metadata.");
    }

    private void Persist(IEnumerable<WebsiteNotificationOutboxItem> items) =>
        AtomicJsonFile.Save(_path, new OutboxEnvelope(CurrentFormatVersion, items.OrderBy(item => item.CreatedAtUtc).ToArray()), MaxDocumentBytes);
    private FileStream AcquireLease() => CrossProcessFileLease.Acquire(_leasePath, "Website notification outbox");
    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
    private sealed record OutboxEnvelope(int Version, WebsiteNotificationOutboxItem[]? Items);
}

public interface IWebsiteNotificationPlanner
{
    bool Queue(WebsiteTargetDefinition target, WebsiteProbeResult result, WebsiteIncidentObservation observation);
}

public sealed class WebsiteNotificationPlanner(
    WebsiteNotificationOptions options,
    IWebsiteNotificationGroupStore groups,
    IWebsiteNotificationOutbox outbox) : IWebsiteNotificationPlanner
{
    public bool Queue(WebsiteTargetDefinition target, WebsiteProbeResult result, WebsiteIncidentObservation observation)
    {
        if (!options.Enabled || observation.Incident is null) return false;
        var kind = observation.Transition switch
        {
            WebsiteIncidentTransition.Opened => WebsiteNotificationKind.IncidentOpened,
            WebsiteIncidentTransition.Reopened => WebsiteNotificationKind.IncidentReopened,
            WebsiteIncidentTransition.Recovered => WebsiteNotificationKind.IncidentRecovered,
            _ => (WebsiteNotificationKind?)null
        };
        if (kind is null) return false;

        var recipients = (target.NotificationGroupIds ?? Array.Empty<string>())
            .Select(id => groups.Get(id))
            .Where(group => group is { IsEnabled: true })
            .SelectMany(group => group!.Recipients)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();
        if (recipients.Length == 0) return false;

        var incident = observation.Incident;
        var dedup = Hash($"{incident.Id}|{kind}|{incident.LastSeenUtc.UtcTicks}");
        var subjectPrefix = kind == WebsiteNotificationKind.IncidentRecovered ? "RECOVERED" : "ALERT";
        var subject = Bound($"[{subjectPrefix}] {target.Name} - {incident.Title}", 200);
        var body = Bound(BuildBody(target, result, observation), 4000);
        var createdAt = result.CompletedAtUtc;
        var item = new WebsiteNotificationOutboxItem(
            Guid.NewGuid().ToString("N"), dedup, target.Id, incident.Id, kind.Value, recipients, subject, body,
            createdAt, createdAt, 0, WebsiteNotificationDeliveryStatus.Pending, null, null, null);
        return outbox.Enqueue(item);
    }

    private static string BuildBody(WebsiteTargetDefinition target, WebsiteProbeResult result, WebsiteIncidentObservation observation)
    {
        var incident = observation.Incident!;
        var status = result.Evidence.HttpStatusCode is int http ? http.ToString() : "n/a";
        var elapsed = result.Evidence.ElapsedMilliseconds is long ms ? $"{ms} ms" : "n/a";
        return $"Monitor Website Notification\n\nTarget: {target.Name}\nEnvironment: {target.Environment}\nURL host: {result.FinalUri.DnsSafeHost}\nTransition: {observation.Transition}\nIncident: {incident.Id}\nSeverity: {incident.Severity}\nObserved rule: {result.Classification.RuleId}\nProbable layer: {result.Classification.ProbableLayer}\nConfidence: {result.Classification.Confidence}\nHTTP status: {status}\nResponse time: {elapsed}\nObserved at UTC: {result.CompletedAtUtc:O}\n\nEvidence: {result.Classification.EvidenceSummary}\n\nThis diagnosis separates observed evidence from probable root cause. Review correlated infrastructure/application evidence before remediation.";
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..40];
    private static string Bound(string value, int max) => value.Length <= max ? value : value[..max];
}
