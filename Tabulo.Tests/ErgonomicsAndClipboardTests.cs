using Tabulo.Core.Commands;
using Tabulo.Core.Engine;
using Tabulo.Core.Models;
using System.Globalization;
using Xunit;

namespace Tabulo.Tests;

public class ErgonomicsAndClipboardTests
{
    [Fact]
    public void CellValueFormatting_SupportsCurrencyPercentageCommaAndDates()
    {
        var num = new CellValue(1234.567);
        Assert.Equal("$1,234.57", num.Format(CellNumberFormats.Currency, CultureInfo.InvariantCulture));
        Assert.Equal("1,234.57", num.Format(CellNumberFormats.Comma, CultureInfo.InvariantCulture));
        Assert.Equal("1,235", num.Format(CellNumberFormats.Integer, CultureInfo.InvariantCulture));

        var percent = new CellValue(0.125);
        Assert.Equal("12.5%", percent.Format(CellNumberFormats.Percent, CultureInfo.InvariantCulture));

        var rawStr = num.Format(CellNumberFormats.General);
        Assert.Equal(num.ToString(), rawStr);

        var err = CellValue.DivByZero;
        Assert.Equal("#DIV/0!", err.Format(CellNumberFormats.Currency));

        var dt = new CellValue(new DateTime(2026, 10, 12));
        Assert.Equal("2026-10-12", dt.Format(CellNumberFormats.ShortDate, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CellRange_CalculatesBoundsAndCoordinatesCorrectly()
    {
        var range = new CellRange(new CellCoordinate(1, 1), new CellCoordinate(3, 2));
        Assert.Equal(1, range.MinRow);
        Assert.Equal(3, range.MaxRow);
        Assert.Equal(1, range.MinCol);
        Assert.Equal(2, range.MaxCol);
        Assert.Equal(3, range.RowCount);
        Assert.Equal(2, range.ColumnCount);
        Assert.Equal(6, range.TotalCells);

        Assert.True(range.Contains(new CellCoordinate(2, 2)));
        Assert.False(range.Contains(new CellCoordinate(4, 2)));
        Assert.Equal("B2:C4", range.ToString());
    }

    [Fact]
    public void BatchCellEditCommand_AppliesAndRevertsMultipleEditsAtomically()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("Data", x: 0, y: 0);
        doc.AddTable(table);

        var calcEngine = new CalculationEngine();
        var undoManager = new UndoRedoManager();

        var edits = new List<CellInputEdit>
        {
            new(new CellCoordinate(0, 0), "", "10"),
            new(new CellCoordinate(0, 1), "", "20"),
            new(new CellCoordinate(0, 2), "", "=A1+B1")
        };

        var cmd = new BatchCellEditCommand(doc, calcEngine, "Data", edits, "Batch paste");
        undoManager.Execute(cmd);

        Assert.Equal("10", table.GetCell(0, 0)!.RawInput);
        Assert.Equal(10.0, table.GetCell(0, 0)!.Value.NumberValue);
        Assert.Equal("20", table.GetCell(0, 1)!.RawInput);
        Assert.Equal(20.0, table.GetCell(0, 1)!.Value.NumberValue);
        Assert.Equal("=A1+B1", table.GetCell(0, 2)!.RawInput);
        Assert.Equal(30.0, table.GetCell(0, 2)!.Value.NumberValue);

        // Undo batch operation
        undoManager.Undo();
        Assert.Equal("", table.GetCell(0, 0)!.RawInput);
        Assert.Equal("", table.GetCell(0, 1)!.RawInput);
        Assert.Equal("", table.GetCell(0, 2)!.RawInput);

        // Redo batch operation
        undoManager.Redo();
        Assert.Equal(30.0, table.GetCell(0, 2)!.Value.NumberValue);
    }

    [Fact]
    public void BatchFormatCommand_AppliesAndRevertsStylesAcrossRange()
    {
        var table = new TableBlock("Styles", x: 0, y: 0);
        var undoManager = new UndoRedoManager();

        var coord1 = new CellCoordinate(0, 0);
        var coord2 = new CellCoordinate(0, 1);
        var cell1 = table.GetOrCreateCell(coord1);
        var cell2 = table.GetOrCreateCell(coord2);

        var edits = new List<CellStyleEdit>
        {
            new(coord1, cell1.Style, cell1.Style with { IsBold = true, Alignment = CellHorizontalAlignment.Center, NumberFormat = CellNumberFormats.Currency }),
            new(coord2, cell2.Style, cell2.Style with { IsBold = true, Alignment = CellHorizontalAlignment.Center, NumberFormat = CellNumberFormats.Currency })
        };

        var cmd = new BatchFormatCommand(table, edits, "Format range");
        undoManager.Execute(cmd);

        Assert.True(cell1.Style.IsBold);
        Assert.Equal(CellHorizontalAlignment.Center, cell1.Style.Alignment);
        Assert.Equal(CellNumberFormats.Currency, cell1.Style.NumberFormat);
        Assert.True(cell2.Style.IsBold);

        // Undo
        undoManager.Undo();
        Assert.False(cell1.Style.IsBold);
        Assert.Equal(CellHorizontalAlignment.Left, cell1.Style.Alignment);
        Assert.Null(cell1.Style.NumberFormat);
    }

    [Fact]
    public void BatchFormatCommand_SupportsTypographyAndClearFormatting()
    {
        var table = new TableBlock("Typography", x: 0, y: 0);
        var undoManager = new UndoRedoManager();

        var coord = new CellCoordinate(0, 0);
        var cell = table.GetOrCreateCell(coord);

        var styled = cell.Style with
        {
            FontFamily = "Arial",
            FontSize = 14.0,
            IsUnderline = true,
            IsStrikethrough = true,
            VerticalAlignment = CellVerticalAlignment.Bottom,
            WrapText = true,
            ForegroundColor = "#2563EB",
            BackgroundColor = "#FEF3C7"
        };

        var cmd = new BatchFormatCommand(table, new List<CellStyleEdit> { new(coord, cell.Style, styled) }, "Apply typography");
        undoManager.Execute(cmd);

        Assert.Equal("Arial", cell.Style.FontFamily);
        Assert.Equal(14.0, cell.Style.FontSize);
        Assert.True(cell.Style.IsUnderline);
        Assert.True(cell.Style.IsStrikethrough);
        Assert.Equal(CellVerticalAlignment.Bottom, cell.Style.VerticalAlignment);
        Assert.True(cell.Style.WrapText);
        Assert.Equal("#2563EB", cell.Style.ForegroundColor);
        Assert.Equal("#FEF3C7", cell.Style.BackgroundColor);

        // Test clear formatting command
        var clearCmd = new BatchFormatCommand(table, new List<CellStyleEdit> { new(coord, cell.Style, CellStyle.Default) }, "Clear formatting");
        undoManager.Execute(clearCmd);

        Assert.Equal("Segoe UI", cell.Style.FontFamily);
        Assert.Equal(11.5, cell.Style.FontSize);
        Assert.False(cell.Style.IsUnderline);
        Assert.False(cell.Style.IsStrikethrough);
        Assert.Equal(CellVerticalAlignment.Center, cell.Style.VerticalAlignment);
        Assert.False(cell.Style.WrapText);
        Assert.Null(cell.Style.BackgroundColor);

        // Undo clear formatting
        undoManager.Undo();
        Assert.Equal("Arial", cell.Style.FontFamily);
        Assert.True(cell.Style.IsUnderline);

        // Undo apply typography
        undoManager.Undo();
        Assert.Equal("Segoe UI", cell.Style.FontFamily);
        Assert.False(cell.Style.IsUnderline);
    }

    [Fact]
    public void CalculationEngine_SetCellInputs_RecalculatesDAGDependenciesAtomically()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("Calc", x: 0, y: 0);
        doc.AddTable(table);

        var engine = new CalculationEngine();

        // Feed multiple inputs with interdependent formulas at once
        var edits = new List<(CellCoordinate coord, string rawInput)>
        {
            (new CellCoordinate(0, 0), "100"),
            (new CellCoordinate(1, 0), "200"),
            (new CellCoordinate(2, 0), "=SUM(A1:A2)"),
            (new CellCoordinate(3, 0), "=A3*1.1")
        };

        engine.SetCellInputs(doc, "Calc", edits);

        Assert.Equal(300.0, table.GetCell(2, 0)!.Value.NumberValue);
        Assert.Equal(330.0, table.GetCell(3, 0)!.Value.NumberValue, precision: 4);

        // Update upstream cell
        engine.SetCellInput(doc, "Calc", new CellCoordinate(0, 0), "200");
        Assert.Equal(400.0, table.GetCell(2, 0)!.Value.NumberValue);
        Assert.Equal(440.0, table.GetCell(3, 0)!.Value.NumberValue, precision: 4);
    }

    [Fact]
    public void BatchCellEditCommand_SupportsCaseTransformationsAndUndoRedo()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("TextData", x: 0, y: 0);
        doc.AddTable(table);
        var engine = new CalculationEngine();
        var undoManager = new UndoRedoManager();

        var c0 = new CellCoordinate(0, 0);
        var c1 = new CellCoordinate(1, 0);
        engine.SetCellInput(doc, "TextData", c0, "hello world");
        engine.SetCellInput(doc, "TextData", c1, "tabulo app");

        // 1. Transform to UPPERCASE
        var upperEdits = new List<CellInputEdit>
        {
            new(c0, table.GetCell(c0)!.RawInput, table.GetCell(c0)!.RawInput.ToUpperInvariant()),
            new(c1, table.GetCell(c1)!.RawInput, table.GetCell(c1)!.RawInput.ToUpperInvariant())
        };
        var upperCmd = new BatchCellEditCommand(doc, engine, "TextData", upperEdits, "Upper case");
        undoManager.Execute(upperCmd);

        Assert.Equal("HELLO WORLD", table.GetCell(c0)!.RawInput);
        Assert.Equal("TABULO APP", table.GetCell(c1)!.RawInput);

        // 2. Transform to Title Case (Capitalize)
        var titleEdits = new List<CellInputEdit>
        {
            new(c0, table.GetCell(c0)!.RawInput, System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(table.GetCell(c0)!.RawInput.ToLowerInvariant())),
            new(c1, table.GetCell(c1)!.RawInput, System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(table.GetCell(c1)!.RawInput.ToLowerInvariant()))
        };
        var titleCmd = new BatchCellEditCommand(doc, engine, "TextData", titleEdits, "Title case");
        undoManager.Execute(titleCmd);

        Assert.Equal("Hello World", table.GetCell(c0)!.RawInput);
        Assert.Equal("Tabulo App", table.GetCell(c1)!.RawInput);

        // 3. Undo Title Case -> Back to UPPERCASE
        undoManager.Undo();
        Assert.Equal("HELLO WORLD", table.GetCell(c0)!.RawInput);
        Assert.Equal("TABULO APP", table.GetCell(c1)!.RawInput);

        // 4. Undo UPPERCASE -> Back to original
        undoManager.Undo();
        Assert.Equal("hello world", table.GetCell(c0)!.RawInput);
        Assert.Equal("tabulo app", table.GetCell(c1)!.RawInput);

        // 5. Redo -> UPPERCASE
        undoManager.Redo();
        Assert.Equal("HELLO WORLD", table.GetCell(c0)!.RawInput);
        Assert.Equal("TABULO APP", table.GetCell(c1)!.RawInput);
    }
}
