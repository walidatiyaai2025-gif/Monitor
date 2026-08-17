namespace Monitor.Web.Services;

public sealed partial class FileHealthIncidentRepository
{
    public IncidentRepositoryReadResult Read(IncidentRepositoryQuery query)
    {
        lock (_gate)
        {
            return IncidentRepositoryRead.Project(_items.Values, query);
        }
    }
}

public sealed partial class SharedHealthIncidentRepository
{
    public IncidentRepositoryReadResult Read(IncidentRepositoryQuery query) =>
        IncidentRepositoryRead.Project(ReadState().Values, query);
}
