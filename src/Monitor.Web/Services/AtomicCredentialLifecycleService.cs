using Monitor.Web.Models;

namespace Monitor.Web.Services;

internal sealed class AtomicCredentialLifecycleService(
    IServerRegistrationRepository registrations,
    IConnectionSecretStore secrets,
    IServerConnectionTester tester,
    IAuditStore audit,
    CredentialPolicyOptions credentialPolicy) : ICredentialLifecycleService
{
    private const string LocalPrefix = "local:v1:";

    public async Task<CredentialReplacementResult> ReplaceWithExternalReferenceAsync(
        Guid registrationId,
        string externalReference,
        string actor,
        CancellationToken cancellationToken = default)
    {
        actor = NormalizeActor(actor);
        var registration = registrations.GetById(registrationId);
        if (registration is null)
        {
            Audit(actor, registrationId, "not-found");
            return new(CredentialReplacementStatus.RegistrationNotFound, "Server registration was not found.");
        }

        if (registration.AuthenticationMode != SqlAuthenticationMode.SqlLogin)
        {
            Audit(actor, registrationId, "not-sql-login");
            return new(CredentialReplacementStatus.NotSqlLogin, "This registration does not use SQL Login authentication.");
        }

        ConnectionSecretReference nextReference;
        try
        {
            nextReference = new ConnectionSecretReference(externalReference);
            if (nextReference.Value.StartsWith(LocalPrefix, StringComparison.Ordinal) ||
                nextReference.Value.StartsWith("runtime-", StringComparison.Ordinal))
            {
                throw new ArgumentException("Reference is not external.", nameof(externalReference));
            }
        }
        catch (ArgumentException)
        {
            Audit(actor, registrationId, "invalid-reference");
            return new(CredentialReplacementStatus.InvalidReference, "Provide a valid external secret reference.");
        }

        if (await secrets.ResolveAsync(nextReference, cancellationToken) is null)
        {
            Audit(actor, registrationId, "secret-unavailable");
            return new(CredentialReplacementStatus.SecretUnavailable, "The replacement credential is unavailable.");
        }

        var candidate = WithSecretReference(registration, nextReference);
        var test = await tester.TestAsync(candidate, cancellationToken);
        if (!test.Succeeded)
        {
            Audit(actor, registrationId, $"test-{test.Status}");
            return new(CredentialReplacementStatus.ConnectionRejected, "The replacement credential did not pass Test Connection.", test);
        }

        ServerRegistrationFieldMutationResult commit;
        try
        {
            commit = registrations.TryReplaceSecretReference(
                registration.Id,
                registration.SecretReference,
                nextReference);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or SharedStateConcurrencyException)
        {
            Audit(actor, registrationId, "commit-failed");
            return new(CredentialReplacementStatus.Failed, "The credential reference could not be committed safely.", test);
        }

        if (commit.Status == ServerRegistrationFieldMutationStatus.NotFound)
        {
            Audit(actor, registrationId, "not-found");
            return new(CredentialReplacementStatus.RegistrationNotFound, "The server registration was removed before the credential could be committed.", test);
        }

        if (commit.Status == ServerRegistrationFieldMutationStatus.Conflict)
        {
            Audit(actor, registrationId, "conflict");
            return new(CredentialReplacementStatus.Failed, "The credential reference changed concurrently. Retry the replacement from the latest server state.", test);
        }

        var previousReference = registration.SecretReference;
        if (commit.Applied && previousReference is not null &&
            !string.Equals(previousReference.Value.Value, nextReference.Value, StringComparison.Ordinal) &&
            secrets is IOwnedConnectionSecretStore owned && owned.Owns(previousReference.Value))
        {
            try
            {
                await owned.DeleteOwnedAsync(previousReference.Value, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                // The registration already points at the tested replacement. Cleanup can remove a retained local orphan later.
            }
        }

        Audit(actor, registrationId, "applied");
        return new(CredentialReplacementStatus.Applied, "Credential reference replaced and Test Connection succeeded.", test);
    }

    public async Task<CredentialReplacementResult> ReplaceWithLocalCredentialAsync(
        Guid registrationId,
        string username,
        string password,
        string actor,
        CancellationToken cancellationToken = default)
    {
        actor = NormalizeActor(actor);
        if (!credentialPolicy.AllowLocalOwnedCredentials)
        {
            Audit(actor, registrationId, "local-policy-disabled");
            return new(CredentialReplacementStatus.Failed, "Local protected credential replacement is disabled by deployment policy.");
        }

        var registration = registrations.GetById(registrationId);
        if (registration is null)
        {
            Audit(actor, registrationId, "not-found");
            return new(CredentialReplacementStatus.RegistrationNotFound, "Server registration was not found.");
        }

        if (registration.AuthenticationMode != SqlAuthenticationMode.SqlLogin)
        {
            Audit(actor, registrationId, "not-sql-login");
            return new(CredentialReplacementStatus.NotSqlLogin, "This registration does not use SQL Login authentication.");
        }

        if (secrets is not IRuntimeCredentialWriter writer || secrets is not IOwnedConnectionSecretStore owned)
        {
            Audit(actor, registrationId, "local-unsupported");
            return new(CredentialReplacementStatus.Failed, "Local protected credential replacement is unavailable.");
        }

        ConnectionSecretReference nextReference;
        try
        {
            nextReference = await writer.StoreAsync(username, password, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            Audit(actor, registrationId, "invalid-local-credential");
            return new(CredentialReplacementStatus.Failed, "Provide a valid SQL username and password.");
        }

        var candidate = WithSecretReference(registration, nextReference);
        try
        {
            var test = await tester.TestAsync(candidate, cancellationToken);
            if (!test.Succeeded)
            {
                await DeleteCandidateAsync(owned, nextReference);
                Audit(actor, registrationId, $"test-{test.Status}");
                return new(CredentialReplacementStatus.ConnectionRejected, "The credential was not changed because the candidate did not connect.", test);
            }

            ServerRegistrationFieldMutationResult commit;
            try
            {
                commit = registrations.TryReplaceSecretReference(
                    registration.Id,
                    registration.SecretReference,
                    nextReference);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or SharedStateConcurrencyException)
            {
                await DeleteCandidateAsync(owned, nextReference);
                Audit(actor, registrationId, "commit-failed");
                return new(CredentialReplacementStatus.Failed, "The credential could not be committed safely.", test);
            }

            if (!commit.Applied)
            {
                await DeleteCandidateAsync(owned, nextReference);
                if (commit.Status == ServerRegistrationFieldMutationStatus.NotFound)
                {
                    Audit(actor, registrationId, "not-found");
                    return new(CredentialReplacementStatus.RegistrationNotFound, "The server registration was removed before the credential could be committed.", test);
                }

                Audit(actor, registrationId, commit.Status == ServerRegistrationFieldMutationStatus.Conflict ? "conflict" : "commit-unchanged");
                return new(
                    CredentialReplacementStatus.Failed,
                    commit.Status == ServerRegistrationFieldMutationStatus.Conflict
                        ? "The credential reference changed concurrently. Retry the replacement from the latest server state."
                        : "The credential could not be committed safely.",
                    test);
            }

            var previous = registration.SecretReference;
            if (previous is not null && owned.Owns(previous.Value) &&
                !registrations.GetAll().Any(item => item.Id != registration.Id && item.SecretReference?.Value == previous.Value.Value))
            {
                try
                {
                    await owned.DeleteOwnedAsync(previous.Value, CancellationToken.None);
                }
                catch (Exception exception) when (exception is IOException or InvalidOperationException)
                {
                }
            }

            Audit(actor, registrationId, "applied");
            return new(CredentialReplacementStatus.Applied, "Credential updated and connection verified. Monitoring can resume.", test);
        }
        catch (OperationCanceledException)
        {
            await DeleteCandidateAsync(owned, nextReference);
            throw;
        }
    }

    public async Task<int> CleanupOrphanedOwnedSecretsAsync(
        string actor,
        CancellationToken cancellationToken = default)
    {
        actor = NormalizeActor(actor);
        if (secrets is not IOwnedConnectionSecretStore owned)
        {
            audit.Append(actor, "credential.cleanup", "owned-secrets", "unsupported");
            return 0;
        }

        var referenced = registrations.GetAll()
            .Where(item => item.SecretReference is not null)
            .Select(item => item.SecretReference!.Value.Value)
            .ToHashSet(StringComparer.Ordinal);
        var removed = 0;
        foreach (var reference in owned.GetOwnedReferences())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (referenced.Contains(reference.Value))
            {
                continue;
            }

            await owned.DeleteOwnedAsync(reference, cancellationToken);
            removed++;
        }

        audit.Append(actor, "credential.cleanup", "owned-secrets", removed == 0 ? "none" : "removed");
        return removed;
    }

    private static ServerRegistration WithSecretReference(
        ServerRegistration registration,
        ConnectionSecretReference reference) =>
        new(
            registration.Id,
            registration.DisplayName,
            registration.Endpoint,
            registration.AuthenticationMode,
            reference,
            registration.IsEnabled,
            registration.CreatedAtUtc);

    private static async Task DeleteCandidateAsync(
        IOwnedConnectionSecretStore owned,
        ConnectionSecretReference reference)
    {
        try
        {
            await owned.DeleteOwnedAsync(reference, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
        }
    }

    private void Audit(string actor, Guid registrationId, string outcome) =>
        audit.Append(actor, "credential.reference.replace", registrationId.ToString("D"), outcome);

    private static string NormalizeActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new InvalidOperationException("Authenticated actor identity is required.");
        }

        return actor.Trim();
    }
}
