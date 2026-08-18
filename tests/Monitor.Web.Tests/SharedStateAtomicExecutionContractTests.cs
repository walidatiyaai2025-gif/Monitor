using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class SharedStateAtomicExecutionContractTests
{
    [Fact]
    public void SqlBackend_DocumentOperations_GuardSchemaInsideSerializableTransaction()
    {
        var source = ReadRepositoryFile("src", "Monitor.Web", "Services", "SharedStateStore.cs");
        var backendStart = RequiredIndex(source, "internal sealed class SqlServerSharedStateSqlBackend");
        var storeStart = RequiredIndex(source, "public sealed class SqlServerSharedStateDocumentStore", backendStart);
        var backend = source[backendStart..storeStart];

        var readStart = RequiredIndex(backend, "public async Task<SharedStateDocument?> ReadAsync(");
        var writeStart = RequiredIndex(backend, "public async Task<SharedStateWriteResult> CompareExchangeAsync(", readStart);
        var lockStart = RequiredIndex(backend, "internal static async Task AcquireExecutionLockAsync(", writeStart);
        var read = backend[readStart..writeStart];
        var write = backend[writeStart..lockStart];

        AssertAtomicOrdering(read, "write: false", "new SqlCommand(ReadSql, connection, transaction)");
        AssertAtomicOrdering(write, "write: true", "new SqlCommand(CompareExchangeSql, connection, transaction)");

        Assert.Contains("FROM dbo.MonitorSharedStateSchema WITH (HOLDLOCK)", backend, StringComparison.Ordinal);
        Assert.Contains("FROM dbo.MonitorSharedStateDocuments WITH (HOLDLOCK)", backend, StringComparison.Ordinal);
        Assert.Contains("FROM dbo.MonitorSharedStateDocuments WITH (UPDLOCK, HOLDLOCK)", backend, StringComparison.Ordinal);
        Assert.Contains("Transaction = transaction", backend, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN TRANSACTION;", backend, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT TRANSACTION;", backend, StringComparison.Ordinal);
    }

    private static void AssertAtomicOrdering(string method, string lockMode, string operationCommand)
    {
        var begin = RequiredIndex(method, "BeginTransactionAsync(");
        var serializable = RequiredIndex(method, "IsolationLevel.Serializable", begin);
        var executionLock = RequiredIndex(method, "AcquireExecutionLockAsync(", serializable);
        var mode = RequiredIndex(method, lockMode, executionLock);
        var schema = RequiredIndex(method, "EnsureSupportedSchemaAsync(", mode);
        var operation = RequiredIndex(method, operationCommand, schema);
        var commit = RequiredIndex(method, "transaction.CommitAsync", operation);

        Assert.True(begin < serializable);
        Assert.True(serializable < executionLock);
        Assert.True(executionLock < mode);
        Assert.True(mode < schema);
        Assert.True(schema < operation);
        Assert.True(operation < commit);
    }

    private static int RequiredIndex(string source, string value, int startIndex = 0)
    {
        var index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Required source contract was not found: {value}");
        return index;
    }

    private static string ReadRepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException($"Repository file was not found: {string.Join('/', pathSegments)}");
    }
}
