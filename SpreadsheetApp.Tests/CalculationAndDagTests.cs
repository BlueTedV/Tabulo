using SpreadsheetApp.Core.Engine;
using SpreadsheetApp.Core.Models;
using Xunit;

namespace SpreadsheetApp.Tests;

public class CalculationAndDagTests
{
    [Fact]
    public void DependencyChain_RecalculatesInOrder()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("Data");
        doc.AddTable(table);

        var engine = new CalculationEngine();
        engine.SetCellInput(doc, "Data", new CellCoordinate(0, 0), "10");         // A1 = 10
        engine.SetCellInput(doc, "Data", new CellCoordinate(1, 0), "=A1 * 2");     // A2 = 20
        engine.SetCellInput(doc, "Data", new CellCoordinate(2, 0), "=A2 + 5");     // A3 = 25

        Assert.Equal(10.0, table.GetCell(0, 0)!.Value.NumberValue);
        Assert.Equal(20.0, table.GetCell(1, 0)!.Value.NumberValue);
        Assert.Equal(25.0, table.GetCell(2, 0)!.Value.NumberValue);

        // Update root cell A1 to 50
        engine.SetCellInput(doc, "Data", new CellCoordinate(0, 0), "50");

        Assert.Equal(50.0, table.GetCell(0, 0)!.Value.NumberValue);
        Assert.Equal(100.0, table.GetCell(1, 0)!.Value.NumberValue);
        Assert.Equal(105.0, table.GetCell(2, 0)!.Value.NumberValue);
    }

    [Fact]
    public void CrossTableReferencing_PropagatesChanges()
    {
        var doc = new CanvasDocument();
        var sales = new TableBlock("Sales");
        var summary = new TableBlock("Summary");
        doc.AddTable(sales);
        doc.AddTable(summary);

        var engine = new CalculationEngine();
        engine.SetCellInput(doc, "Sales", new CellCoordinate(0, 0), "100");
        engine.SetCellInput(doc, "Sales", new CellCoordinate(1, 0), "200");
        engine.SetCellInput(doc, "Summary", new CellCoordinate(0, 0), "=SUM(Sales!A1:A2) * 1.1");

        Assert.Equal(330.0, summary.GetCell(0, 0)!.Value.NumberValue);

        // Update sales A1 to 300
        engine.SetCellInput(doc, "Sales", new CellCoordinate(0, 0), "300");
        Assert.Equal(550.0, summary.GetCell(0, 0)!.Value.NumberValue);
    }

    [Fact]
    public void CircularDependency_IsFlaggedAsCircularError()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("Sheet1");
        doc.AddTable(table);

        var engine = new CalculationEngine();
        engine.SetCellInput(doc, "Sheet1", new CellCoordinate(0, 0), "=B1 + 1"); // A1 = B1 + 1
        engine.SetCellInput(doc, "Sheet1", new CellCoordinate(0, 1), "=A1 * 2"); // B1 = A1 * 2

        var a1 = table.GetCell(0, 0)!;
        var b1 = table.GetCell(0, 1)!;

        Assert.True(a1.Value.IsError || b1.Value.IsError);
        Assert.Contains("#CIRCULAR!", a1.Value.TextValue + b1.Value.TextValue);
    }

    [Fact]
    public void RecalculatedEvent_FiresOnSetCellInputAndRecalculateAll()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("LiveTest");
        doc.AddTable(table);

        var engine = new CalculationEngine();
        var eventCount = 0;
        engine.Recalculated += (_, _) => eventCount++;

        engine.SetCellInput(doc, "LiveTest", new CellCoordinate(0, 0), "123");
        Assert.Equal(1, eventCount);

        engine.SetCellInput(doc, "LiveTest", new CellCoordinate(1, 0), "=A1 * 10");
        Assert.Equal(2, eventCount);

        engine.RecalculateAll(doc);
        Assert.Equal(3, eventCount);
    }
}

