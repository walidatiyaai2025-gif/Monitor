using System.Net;
using System.Net.Mail;

namespace Monitor.Web.Services;

public interface IWebsiteSmtpCredentialProvider
{
    string? GetPassword(string environmentVariableName);
}

public sealed class EnvironmentWebsiteSmtpCredentialProvider : IWebsiteSmtpCredentialProvider
{
    public string? GetPassword(string environmentVariableName)
    {
        if (string.IsNullOrWhiteSpace(environmentVariableName)) return null;
        return Environment.GetEnvironmentVariable(environmentVariableName);
    }
}

public interface IWebsiteEmailSender
{
    Task SendAsync(WebsiteNotificationOutboxItem item, CancellationToken cancellationToken);
}

public sealed class SmtpWebsiteEmailSender(
    WebsiteNotificationOptions options,
    IWebsiteSmtpCredentialProvider credentials) : IWebsiteEmailSender
{
    public async Task SendAsync(WebsiteNotificationOutboxItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!options.Enabled) throw new InvalidOperationException("Website email notifications are disabled.");
        options.Validate();

        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress),
            Subject = item.Subject,
            Body = item.Body,
            IsBodyHtml = false
        };
        foreach (var recipient in item.Recipients) message.To.Add(new MailAddress(recipient));

        using var smtp = new SmtpClient(options.SmtpHost, options.SmtpPort)
        {
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(options.Username)
        };

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            var password = credentials.GetPassword(options.PasswordEnvironmentVariable);
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Configured SMTP credential secret is unavailable.");
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(options.Username, password);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}

public sealed class WebsiteNotificationDeliveryWorker(
    WebsiteNotificationOptions options,
    IWebsiteNotificationOutbox outbox,
    IWebsiteEmailSender sender,
    TimeProvider timeProvider,
    ILogger<WebsiteNotificationDeliveryWorker> logger) : BackgroundService
{
    private const int MaxDeliveriesPerCycle = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Website email notification delivery is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.DeliveryTickSeconds), timeProvider);
        do
        {
            await DeliverCycleAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeliverCycleAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < MaxDeliveriesPerCycle && !cancellationToken.IsCancellationRequested; index++)
        {
            var now = timeProvider.GetUtcNow();
            var claim = outbox.TryClaimDue(now, TimeSpan.FromMinutes(2));
            if (claim is null) return;

            try
            {
                await sender.SendAsync(claim.Item, cancellationToken);
                if (!outbox.MarkSent(claim, timeProvider.GetUtcNow()))
                    logger.LogWarning("Website email outbox sent item {ItemId}, but claim completion lost ownership.", claim.ItemId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failureClass = exception.GetType().Name;
                if (!outbox.MarkFailed(claim, timeProvider.GetUtcNow(), options.MaxAttempts, failureClass))
                    logger.LogWarning("Website email outbox failed item {ItemId}, but claim completion lost ownership.", claim.ItemId);
                else
                    logger.LogWarning("Website email delivery failed for item {ItemId} with {FailureClass}; bounded retry policy applies.", claim.ItemId, failureClass);
            }
        }
    }
}
