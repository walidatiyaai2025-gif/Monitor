namespace Monitor.Web.Models;

public enum ConnectionTestStatus
{
    Succeeded,
    RegistrationNotFound,
    Disabled,
    SecretUnavailable,
    TimedOut,
    AuthenticationFailed,
    NetworkUnavailable,
    CertificateRejected,
    Failed,
    PermissionDenied
}

public sealed record ConnectionTestResult(
    ConnectionTestStatus Status,
    string Message,
    long ElapsedMilliseconds,
    string? ServerVersion = null)
{
    public bool Succeeded => Status == ConnectionTestStatus.Succeeded;
}