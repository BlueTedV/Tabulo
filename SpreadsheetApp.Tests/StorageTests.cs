using SpreadsheetApp.Core.Models;
using SpreadsheetApp.Core.Storage;
using Xunit;

namespace SpreadsheetApp.Tests;

public class StorageTests
{
    [Fact]
    public void DocumentSerialization_RoundTripPreservesTablesFormulasAndCoordinates()
    {
        var doc = CanvasDocument.CreateDefault();
        var json = DocumentStorageManager.SerializeToJson(doc);

        Assert.NotNull(json);
        Assert.Contains("Financial Model", json);
        Assert.Contains("Sales", json);
        Assert.Contains("Summary", json);
        Assert.Contains("=SUM(B2:B4)", json);

        var restored = DocumentStorageManager.DeserializeFromJson(json);

        Assert.Equal(doc.Name, restored.Name);
        Assert.Equal(2, restored.Tables.Count);

        var sales = restored.GetTableByName("Sales")!;
        Assert.NotNull(sales);
        Assert.Equal(40.0, sales.X);
        Assert.Equal("10000", sales.GetCell(1, 1)!.RawInput);

        var summary = restored.GetTableByName("Summary")!;
        Assert.NotNull(summary);
        Assert.Equal("=Sales!B5", summary.GetCell(1, 1)!.RawInput);
    }

    [Fact]
    public void DocumentSerialization_PreservesTableCustomHeaderColor()
    {
        var doc = CanvasDocument.CreateDefault();
        var sales = doc.GetTableByName("Sales")!;
        sales.HeaderColor = "#2563EB";

        var json = DocumentStorageManager.SerializeToJson(doc);
        Assert.Contains("#2563EB", json);

        var restored = DocumentStorageManager.DeserializeFromJson(json);
        var restoredSales = restored.GetTableByName("Sales")!;
        Assert.Equal("#2563EB", restoredSales.HeaderColor);
    }
}

