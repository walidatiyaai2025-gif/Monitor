namespace Monitor.Web.Services;

public enum RuntimePressureClass
{
    Healthy,
    Elevated,
    High,
    Critical
}

public sealed record RuntimePressureInput(int MemoryPercent, int BlockedRequests, long MaxWaitMilliseconds, int RunnableTasks, int PendingIoRequests);
public sealed record RuntimePressureResult(int Score, RuntimePressureClass Classification, string[] Signals);

public static class Batch300RuntimePressure
{
    public static int NormalizePercent(int value) => Math.Clamp(value, 0, 100);

    public static int MemoryPoints(int memoryPercent)
    {
        var value = NormalizePercent(memoryPercent);
        return value switch { >= 95 => 40, >= 90 => 30, >= 80 => 20, >= 70 => 10, _ => 0 };
    }

    public static int BlockingPoints(int blockedRequests, long maxWaitMilliseconds)
    {
        var blocked = Math.Max(0, blockedRequests);
        var wait = Math.Max(0, maxWaitMilliseconds);
        var points = blocked switch { >= 20 => 25, >= 10 => 18, >= 3 => 10, >= 1 => 5, _ => 0 };
        if (wait >= 60_000) points += 10;
        else if (wait >= 10_000) points += 5;
        return Math.Min(points, 30);
    }

    public static int SchedulerPoints(int runnableTasks)
    {
        var value = Math.Max(0, runnableTasks);
        return value switch { >= 32 => 20, >= 16 => 14, >= 8 => 8, >= 4 => 4, _ => 0 };
    }

    public static int IoPoints(int pendingIoRequests)
    {
        var value = Math.Max(0, pendingIoRequests);
        return value switch { >= 32 => 15, >= 16 => 10, >= 8 => 6, >= 4 => 3, _ => 0 };
    }

    public static int Score(RuntimePressureInput input) => Math.Clamp(
        MemoryPoints(input.MemoryPercent) + BlockingPoints(input.BlockedRequests, input.MaxWaitMilliseconds) + SchedulerPoints(input.RunnableTasks) + IoPoints(input.PendingIoRequests),
        0,
        100);

    public static RuntimePressureClass Classify(int score) => Math.Clamp(score, 0, 100) switch
    {
        >= 80 => RuntimePressureClass.Critical,
        >= 55 => RuntimePressureClass.High,
        >= 25 => RuntimePressureClass.Elevated,
        _ => RuntimePressureClass.Healthy
    };

    public static bool IsHotspot(RuntimePressureInput input) => Score(input) >= 55;

    public static string[] Signals(RuntimePressureInput input)
    {
        var values = new List<string>();
        if (MemoryPoints(input.MemoryPercent) > 0) values.Add("memory");
        if (BlockingPoints(input.BlockedRequests, input.MaxWaitMilliseconds) > 0) values.Add("blocking");
        if (SchedulerPoints(input.RunnableTasks) > 0) values.Add("scheduler");
        if (IoPoints(input.PendingIoRequests) > 0) values.Add("io");
        return values.ToArray();
    }

    public static RuntimePressureResult Evaluate(RuntimePressureInput input)
    {
        var score = Score(input);
        return new(score, Classify(score), Signals(input));
    }
}
