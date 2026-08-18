namespace Monitor.Web.Services;

public sealed partial class FileHealthIncidentRepository
{
    public IncidentRepositoryReadResult Read(IncidentRepositoryQuery query)
    {
        lock (_gate)
        {
            using var lease = AcquireLease();
            _items = Load(_path);
            return IncidentRepositoryRead.Project(_items.Values, query);
        }
    }
}

public sealed partial class SharedHealthIncidentRepository
{
    public IncidentRepositoryReadResult Read(IncidentRepositoryQuery query) =>
        IncidentRepositoryRead.Project(ReadState().Values, query);
}
