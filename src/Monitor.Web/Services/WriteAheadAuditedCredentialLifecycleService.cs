namespace Monitor.Web.Services;

internal sealed class WriteAheadAuditedCredentialLifecycleService(
    CredentialLifecycleService inner,
    IAuditStore audit) : ICredentialLifecycleService
{
    public Task<CredentialReplacementResult> ReplaceWithLocalCredentialAsync(
        Guid registrationId,
        string username,
        string password,
        string actor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        actor = NormalizeActor(actor);
        audit.Append(actor, "credential.reference.replace.request", registrationId.ToString("D"), "local");
        return inner.ReplaceWithLocalCredentialAsync(registrationId, username, password, actor, cancellationToken);
    }

    public Task<CredentialReplacementResult> ReplaceWithExternalReferenceAsync(
        Guid registrationId,
        string externalReference,
        string actor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        actor = NormalizeActor(actor);
        audit.Append(actor, "credential.reference.replace.request", registrationId.ToString("D"), "external");
        return inner.ReplaceWithExternalReferenceAsync(registrationId, externalReference, actor, cancellationToken);
    }

    public Task<int> CleanupOrphanedOwnedSecretsAsync(
        string actor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        actor = NormalizeActor(actor);
        audit.Append(actor, "credential.cleanup.request", "owned-secrets", "requested");
        return inner.CleanupOrphanedOwnedSecretsAsync(actor, cancellationToken);
    }

    private static string NormalizeActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new InvalidOperationException("Authenticated actor identity is required.");
        }

        return actor.Trim();
    }
}
