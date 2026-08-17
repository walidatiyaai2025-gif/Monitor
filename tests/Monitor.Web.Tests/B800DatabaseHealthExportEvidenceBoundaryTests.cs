using System.Text;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class B800DatabaseHealthExportEvidenceBoundaryTests
{
    [Fact]
    public void Export_PreservesAggregateDetailWhenRetainedRowsAreUnavailable()
    {
        var detail = new DatabaseHealthDetailSnapshot(
            Restoring: 1,
            Recovering: 2,
            RecoveryPending: 3,
            Suspect: 4,
            Emergency: 5,
            OfflineOrOther: 6,
            Items: []);
        var server = new HealthModuleServerViewModel(
            Id: Guid.NewGuid().ToString("D"),
            Name: "sql-aggregate-only",
            Source: ServerDataSource.LiveFresh,
            AgeSeconds: 15,
            DatabaseOnline: 7,
            DatabaseTotal: 8,
            Databases: detail,
            Backups: null,
            Jobs: null,
            Storage: null,
            Blocking: null,
            Performance: null);

        var csv = Encoding.UTF8.GetString(DatabaseHealthExport.Build([server]));

        Assert.Contains("\"sql-aggregate-only\",\"LiveFresh\",\"15\",\"7\",\"8\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Unavailable\",\"Unavailable\",\"Unavailable\",\"Unavailable\",\"1\",\"2\",\"3\",\"4\",\"5\",\"6\"", csv, StringComparison.Ordinal);
    }
}
