using Monitor.Web.Models;

namespace Monitor.Web.Services;

public sealed record DatabaseStateItemViewModel(
    string Name,
    string State,
    DatabaseStateClass Classification,
    bool Actionable);

public sealed record DatabaseStateProjectionViewModel(
    IReadOnlyList<DatabaseStateItemViewModel> Items,
    DatabaseStateClass WorstObserved,
    int ActionableCount,
    int UnknownCount)
{
    public bool HasEvidence => Items.Count > 0;
}

public static class DatabaseStateProjection
{
    public static DatabaseStateProjectionViewModel Build(DatabaseHealthDetailSnapshot? detail)
    {
        var items = detail?.Items ?? [];
        if (items.Count == 0)
        {
            return new([], DatabaseStateClass.Unknown, 0, 0);
        }

        var projected = items
            .Select(item =>
            {
                var classification = Batch300DatabaseState.Classify(item.State);
                return new DatabaseStateItemViewModel(
                    item.Name,
                    Batch300DatabaseState.NormalizeState(item.State),
                    classification,
                    Batch300DatabaseState.IsActionable(classification));
            })
            .ToArray();

        return new(
            projected,
            Batch300DatabaseState.Worst(projected.Select(item => item.State)),
            projected.Count(item => item.Actionable),
            projected.Count(item => item.Classification == DatabaseStateClass.Unknown));
    }
}
