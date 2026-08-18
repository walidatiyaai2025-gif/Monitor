namespace Monitor.Web.Services;

internal sealed class ServerRegistrationMutationGate
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public void Wait(CancellationToken cancellationToken = default) =>
        gate.Wait(cancellationToken);

    public Task WaitAsync(CancellationToken cancellationToken = default) =>
        gate.WaitAsync(cancellationToken);

    public void Release() => gate.Release();
}
