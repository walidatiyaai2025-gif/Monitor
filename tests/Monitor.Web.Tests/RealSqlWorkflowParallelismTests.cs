using Xunit;

namespace Monitor.Web.Tests;

public sealed class RealSqlWorkflowParallelismTests
{
    private const string SerialCollectionsSwitch = "-- xUnit.ParallelizeTestCollections=false";

    [Fact]
    public void RealSqlAcceptance_SerializesCollectionsWithoutChangingNormalCi()
    {
        var root = FindRepoRoot();
        var realSql = File.ReadAllText(Path.Combine(root, ".github", "workflows", "real-sql-acceptance.yml"));
        var normalCi = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        Assert.Contains("--filter \"Category=RealSql\"", realSql, StringComparison.Ordinal);
        Assert.Contains(SerialCollectionsSwitch, realSql, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(realSql, SerialCollectionsSwitch));
        Assert.DoesNotContain(SerialCollectionsSwitch, normalCi, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(expected, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += expected.Length;
        }

        return count;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Monitor.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, ".github", "workflows")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located from the test base directory.");
    }
}
