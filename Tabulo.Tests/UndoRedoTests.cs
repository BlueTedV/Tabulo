using Tabulo.Core.Commands;
using Tabulo.Core.Engine;
using Tabulo.Core.Models;
using Xunit;

namespace Tabulo.Tests;

public class UndoRedoTests
{
    [Fact]
    public void GlobalLinearUndoRedo_ReplaysInterleavedActionsInExactChronologicalOrder()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("T1", x: 10, y: 10);
        doc.AddTable(table);

        var calcEngine = new CalculationEngine();
        var undoManager = new UndoRedoManager();

        // 1. Cell edit
        undoManager.Execute(new CellEditCommand(doc, calcEngine, "T1", new CellCoordinate(0, 0), "", "First"));
        Assert.Equal("First", table.GetCell(0, 0)!.RawInput);

        // 2. Canvas move
        undoManager.Execute(new MoveTableCommand(table, 10, 10, 100, 200));
        Assert.Equal(100.0, table.X);
        Assert.Equal(200.0, table.Y);

        // 3. Cell edit
        undoManager.Execute(new CellEditCommand(doc, calcEngine, "T1", new CellCoordinate(1, 0), "", "Second"));
        Assert.Equal("Second", table.GetCell(1, 0)!.RawInput);

        // 4. Cell edit
        undoManager.Execute(new CellEditCommand(doc, calcEngine, "T1", new CellCoordinate(2, 0), "", "Third"));
        Assert.Equal("Third", table.GetCell(2, 0)!.RawInput);

        // 5. Canvas move
        undoManager.Execute(new MoveTableCommand(table, 100, 200, 500, 500));
        Assert.Equal(500.0, table.X);
        Assert.Equal(500.0, table.Y);

        // --- STEPWISE UNDO ---

        // Undo 5 (Canvas Move: 500,500 -> 100,200)
        undoManager.Undo();
        Assert.Equal(100.0, table.X);
        Assert.Equal(200.0, table.Y);
        Assert.Equal("Third", table.GetCell(2, 0)!.RawInput);

        // Undo 4 (Cell Edit 3: "Third" -> "")
        undoManager.Undo();
        Assert.Equal("", table.GetCell(2, 0)!.RawInput);
        Assert.Equal("Second", table.GetCell(1, 0)!.RawInput);

        // Undo 3 (Cell Edit 2: "Second" -> "")
        undoManager.Undo();
        Assert.Equal("", table.GetCell(1, 0)!.RawInput);
        Assert.Equal(100.0, table.X);

        // Undo 2 (Canvas Move: 100,200 -> 10,10)
        undoManager.Undo();
        Assert.Equal(10.0, table.X);
        Assert.Equal(10.0, table.Y);
        Assert.Equal("First", table.GetCell(0, 0)!.RawInput);

        // Undo 1 (Cell Edit 1: "First" -> "")
        undoManager.Undo();
        Assert.Equal("", table.GetCell(0, 0)!.RawInput);
        Assert.False(undoManager.CanUndo);

        // --- STEPWISE REDO ---
        undoManager.Redo(); // Redo Cell 1 -> "First"
        Assert.Equal("First", table.GetCell(0, 0)!.RawInput);

        undoManager.Redo(); // Redo Canvas Move -> (100, 200)
        Assert.Equal(100.0, table.X);
        Assert.Equal(200.0, table.Y);

        undoManager.Redo(); // Redo Cell 2 -> "Second"
        Assert.Equal("Second", table.GetCell(1, 0)!.RawInput);

        undoManager.Redo(); // Redo Cell 3 -> "Third"
        Assert.Equal("Third", table.GetCell(2, 0)!.RawInput);

        undoManager.Redo(); // Redo Canvas Move -> (500, 500)
        Assert.Equal(500.0, table.X);
        Assert.Equal(500.0, table.Y);

        Assert.False(undoManager.CanRedo);
    }
}
