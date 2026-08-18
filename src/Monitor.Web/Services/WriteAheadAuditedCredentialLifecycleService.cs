namespace Monitor.Web.Services;

internal sealed class WriteAheadAuditedCredentialLifecycleService(
    ICredentialLifecycleService inner,
    ServerRegistrationMutationGate mutationGate,
    IAuditStore audit) : ICredentialLifecycleService
{
    internal WriteAheadAuditedCredentialLifecycleService(
        CredentialLifecycleService inner,
        IAuditStore audit)
        : this(inner, new ServerRegistrationMutationGate(), audit)
    {
    }

    public async Task<CredentialReplacementResult> ReplaceWithLocalCredentialAsync(
        Guid registrationId,
        string username,
        string password,
        string actor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        actor = NormalizeActor(actor);
        audit.Append(actor, "credential.reference.replace.request", registrationId.ToString("D"), "local");

        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            return await inner.ReplaceWithLocalCredentialAsync(registrationId, username, password, actor, cancellationToken);
        }
        finally
        {
            mutationGate.Release();
        }
    }

    public async Task<CredentialReplacementResult> ReplaceWithExternalReferenceAsync(
        Guid registrationId,
        string externalReference,
        string actor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        actor = NormalizeActor(actor);
        audit.Append(actor, "credential.reference.replace.request", registrationId.ToString("D"), "external");

        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            return await inner.ReplaceWithExternalReferenceAsync(registrationId, externalReference, actor, cancellationToken);
        }
        finally
        {
            mutationGate.Release();
        }
    }

    public async Task<int> CleanupOrphanedOwnedSecretsAsync(
        string actor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        actor = NormalizeActor(actor);
        audit.Append(actor, "credential.cleanup.request", "owned-secrets", "requested");

        await mutationGate.WaitAsync(cancellationToken);
        try
        {
            return await inner.CleanupOrphanedOwnedSecretsAsync(actor, cancellationToken);
        }
        finally
        {
            mutationGate.Release();
        }
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
