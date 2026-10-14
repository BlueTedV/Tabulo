using Tabulo.Core.Models;
using Tabulo.Core.Storage;
using Xunit;

namespace Tabulo.Tests;

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

    [Fact]
    public void DocumentSerialization_RoundTripPreservesAllCellStyleProperties()
    {
        var doc = new CanvasDocument { Name = "StyledDoc" };
        var table = new TableBlock("StyledTable", x: 10, y: 20, cols: 5, rows: 5);
        doc.AddTable(table);

        var coord = new CellCoordinate(1, 1);
        var cell = table.GetOrCreateCell(coord);
        cell.RawInput = "1234.56";
        cell.Style = new CellStyle
        {
            FontFamily = "Cascadia Code",
            FontSize = 16.0,
            IsBold = true,
            IsItalic = true,
            IsUnderline = true,
            IsStrikethrough = true,
            Alignment = CellHorizontalAlignment.Right,
            VerticalAlignment = CellVerticalAlignment.Bottom,
            ForegroundColor = "#DC2626",
            BackgroundColor = "#FEF3C7",
            NumberFormat = CellNumberFormats.Currency,
            WrapText = true
        };

        var json = DocumentStorageManager.SerializeToJson(doc);
        Assert.NotNull(json);

        var restored = DocumentStorageManager.DeserializeFromJson(json);
        var restoredTable = restored.GetTableByName("StyledTable")!;
        Assert.NotNull(restoredTable);

        var restoredCell = restoredTable.GetCell(coord)!;
        Assert.NotNull(restoredCell);
        Assert.Equal("1234.56", restoredCell.RawInput);
        Assert.Equal("Cascadia Code", restoredCell.Style.FontFamily);
        Assert.Equal(16.0, restoredCell.Style.FontSize);
        Assert.True(restoredCell.Style.IsBold);
        Assert.True(restoredCell.Style.IsItalic);
        Assert.True(restoredCell.Style.IsUnderline);
        Assert.True(restoredCell.Style.IsStrikethrough);
        Assert.Equal(CellHorizontalAlignment.Right, restoredCell.Style.Alignment);
        Assert.Equal(CellVerticalAlignment.Bottom, restoredCell.Style.VerticalAlignment);
        Assert.Equal("#DC2626", restoredCell.Style.ForegroundColor);
        Assert.Equal("#FEF3C7", restoredCell.Style.BackgroundColor);
        Assert.Equal(CellNumberFormats.Currency, restoredCell.Style.NumberFormat);
        Assert.True(restoredCell.Style.WrapText);
    }
}

