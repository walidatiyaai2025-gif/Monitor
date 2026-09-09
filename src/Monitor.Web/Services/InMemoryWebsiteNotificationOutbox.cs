namespace Monitor.Web.Services;

public sealed class InMemoryWebsiteNotificationOutbox : IWebsiteNotificationOutbox
{
    private readonly object _gate = new();
    private readonly List<WebsiteNotificationOutboxItem> _items = [];

    public bool Enqueue(WebsiteNotificationOutboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            if (_items.Any(existing => string.Equals(existing.DedupKey, item.DedupKey, StringComparison.Ordinal))) return false;
            if (_items.Count >= FileWebsiteNotificationOutbox.MaxEntries)
            {
                var removable = _items.Where(existing => existing.Status != WebsiteNotificationDeliveryStatus.Pending)
                    .OrderBy(existing => existing.CreatedAtUtc).FirstOrDefault();
                if (removable is null) throw new InvalidOperationException("Website notification outbox capacity has been reached.");
                _items.Remove(removable);
            }
            _items.Add(item);
            return true;
        }
    }

    public WebsiteNotificationClaim? TryClaimDue(DateTimeOffset nowUtc, TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        lock (_gate)
        {
            var index = _items.FindIndex(item => item.Status == WebsiteNotificationDeliveryStatus.Pending &&
                item.NextAttemptUtc <= nowUtc && (item.LeaseUntilUtc is null || item.LeaseUntilUtc <= nowUtc));
            if (index < 0) return null;
            var token = Guid.NewGuid().ToString("N");
            var claimed = _items[index] with { LeaseToken = token, LeaseUntilUtc = nowUtc + leaseDuration };
            _items[index] = claimed;
            return new WebsiteNotificationClaim(claimed.Id, token, claimed);
        }
    }

    public bool MarkSent(WebsiteNotificationClaim claim, DateTimeOffset sentAtUtc) => Mutate(claim, item => item with
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
        return Mutate(claim, item =>
        {
            var attempts = Math.Min(10, item.Attempts + 1);
            var dead = attempts >= maxAttempts;
            var delay = TimeSpan.FromSeconds(Math.Min(900, 15 * (1 << Math.Min(5, attempts - 1))));
            return item with
            {
                Attempts = attempts,
                Status = dead ? WebsiteNotificationDeliveryStatus.DeadLetter : WebsiteNotificationDeliveryStatus.Pending,
                NextAttemptUtc = dead ? nowUtc : nowUtc + delay,
                LeaseToken = null,
                LeaseUntilUtc = null,
                LastError = error.Length <= 300 ? error : error[..300]
            };
        });
    }

    public IReadOnlyList<WebsiteNotificationOutboxItem> Snapshot()
    {
        lock (_gate) return _items.OrderByDescending(item => item.CreatedAtUtc).ToArray();
    }

    private bool Mutate(WebsiteNotificationClaim claim, Func<WebsiteNotificationOutboxItem, WebsiteNotificationOutboxItem> mutation)
    {
        ArgumentNullException.ThrowIfNull(claim);
        lock (_gate)
        {
            var index = _items.FindIndex(item => string.Equals(item.Id, claim.ItemId, StringComparison.Ordinal));
            if (index < 0 || !string.Equals(_items[index].LeaseToken, claim.Token, StringComparison.Ordinal)) return false;
            _items[index] = mutation(_items[index]);
            return true;
        }
    }
}
