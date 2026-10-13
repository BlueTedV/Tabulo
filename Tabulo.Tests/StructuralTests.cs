using Tabulo.Core.Commands;
using Tabulo.Core.Engine;
using Tabulo.Core.Models;
using Xunit;

namespace Tabulo.Tests;

public class StructuralTests
{
    [Fact]
    public void InsertRow_ShiftsExistingCellsDown()
    {
        var table = new TableBlock("Test", 0, 0, cols: 5, rows: 5);
        table.GetOrCreateCell(0, 0).RawInput = "Row0";
        table.GetOrCreateCell(1, 0).RawInput = "Row1";
        table.GetOrCreateCell(2, 0).RawInput = "Row2";

        // Insert at row 1
        table.InsertRow(1);

        Assert.Equal(6, table.RowCount);
        Assert.Equal("Row0", table.GetCell(0, 0)?.RawInput);
        Assert.Null(table.GetCell(1, 0)); // Blank new row
        Assert.Equal("Row1", table.GetCell(2, 0)?.RawInput);
        Assert.Equal("Row2", table.GetCell(3, 0)?.RawInput);
    }

    [Fact]
    public void DeleteRow_ShiftsLowerCellsUp()
    {
        var table = new TableBlock("Test", 0, 0, cols: 5, rows: 5);
        table.GetOrCreateCell(0, 0).RawInput = "Row0";
        table.GetOrCreateCell(1, 0).RawInput = "Row1";
        table.GetOrCreateCell(2, 0).RawInput = "Row2";

        // Delete row 1 ("Row1")
        table.DeleteRow(1);

        Assert.Equal(4, table.RowCount);
        Assert.Equal("Row0", table.GetCell(0, 0)?.RawInput);
        Assert.Equal("Row2", table.GetCell(1, 0)?.RawInput);
    }

    [Fact]
    public void InsertColumn_ShiftsExistingColumnsRight()
    {
        var table = new TableBlock("Test", 0, 0, cols: 5, rows: 5);
        table.GetOrCreateCell(0, 0).RawInput = "ColA";
        table.GetOrCreateCell(0, 1).RawInput = "ColB";

        // Insert at column 1
        table.InsertColumn(1);

        Assert.Equal(6, table.ColumnCount);
        Assert.Equal("ColA", table.GetCell(0, 0)?.RawInput);
        Assert.Null(table.GetCell(0, 1));
        Assert.Equal("ColB", table.GetCell(0, 2)?.RawInput);
    }

    [Fact]
    public void DeleteColumn_ShiftsRightColumnsLeft()
    {
        var table = new TableBlock("Test", 0, 0, cols: 5, rows: 5);
        table.GetOrCreateCell(0, 0).RawInput = "ColA";
        table.GetOrCreateCell(0, 1).RawInput = "ColB";
        table.GetOrCreateCell(0, 2).RawInput = "ColC";

        // Delete column 1 ("ColB")
        table.DeleteColumn(1);

        Assert.Equal(4, table.ColumnCount);
        Assert.Equal("ColA", table.GetCell(0, 0)?.RawInput);
        Assert.Equal("ColC", table.GetCell(0, 1)?.RawInput);
    }

    [Fact]
    public void ResizeDimensionsCommand_SupportsUndoAndRedo()
    {
        var table = new TableBlock("Test", 0, 0, cols: 5, rows: 10);
        var undo = new UndoRedoManager();

        var cmd = new ResizeTableDimensionsCommand(table, oldCols: 5, oldRows: 10, newCols: 8, newRows: 15);
        undo.Execute(cmd);

        Assert.Equal(8, table.ColumnCount);
        Assert.Equal(15, table.RowCount);

        undo.Undo();
        Assert.Equal(5, table.ColumnCount);
        Assert.Equal(10, table.RowCount);

        undo.Redo();
        Assert.Equal(8, table.ColumnCount);
        Assert.Equal(15, table.RowCount);
    }

    [Fact]
    public void CloneTable_CreatesAccurateDeepCopy()
    {
        var original = new TableBlock("Source", 50, 60, cols: 4, rows: 6);
        original.HeaderColor = "#10B981";
        original.GetOrCreateCell(1, 1).RawInput = "OriginalVal";
        original.SetColumnWidth(1, 140.0);
        original.SetRowHeight(1, 35.0);

        var copy = original.Clone("Source_Copy", 90, 100);

        Assert.Equal("Source_Copy", copy.Name);
        Assert.Equal(90.0, copy.X);
        Assert.Equal(100.0, copy.Y);
        Assert.Equal("#10B981", copy.HeaderColor);
        Assert.Equal(4, copy.ColumnCount);
        Assert.Equal(6, copy.RowCount);
        Assert.Equal(140.0, copy.GetColumnWidth(1));
        Assert.Equal(35.0, copy.GetRowHeight(1));
        Assert.Equal("OriginalVal", copy.GetCell(1, 1)?.RawInput);

        // Mutating copy does not alter original
        copy.GetOrCreateCell(1, 1).RawInput = "MutatedVal";
        Assert.Equal("OriginalVal", original.GetCell(1, 1)?.RawInput);
    }

    [Fact]
    public void DuplicateTableCommand_ExecutesAndUndoes()
    {
        var doc = CanvasDocument.CreateDefault();
        var initialCount = doc.Tables.Count;
        var table = doc.Tables.First();
        var undo = new UndoRedoManager();

        var cmd = new DuplicateTableCommand(doc, table);
        undo.Execute(cmd);

        Assert.Equal(initialCount + 1, doc.Tables.Count);
        Assert.Equal($"{table.Name}_Copy", cmd.DuplicatedTable.Name);
        Assert.Contains(cmd.DuplicatedTable, doc.Tables);

        undo.Undo();
        Assert.Equal(initialCount, doc.Tables.Count);
        Assert.DoesNotContain(cmd.DuplicatedTable, doc.Tables);

        undo.Redo();
        Assert.Equal(initialCount + 1, doc.Tables.Count);
        Assert.Contains(cmd.DuplicatedTable, doc.Tables);
    }

    [Fact]
    public void SetTableHeaderColorCommand_SupportsUndoAndRedo()
    {
        var table = new TableBlock("ColoredTable", 0, 0, 5, 5);
        Assert.Null(table.HeaderColor);

        var undo = new UndoRedoManager();
        var cmd = new SetTableHeaderColorCommand(table, null, "#EF4444");
        undo.Execute(cmd);

        Assert.Equal("#EF4444", table.HeaderColor);

        undo.Undo();
        Assert.Null(table.HeaderColor);

        undo.Redo();
        Assert.Equal("#EF4444", table.HeaderColor);
    }
}

