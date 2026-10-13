using SpreadsheetApp.Core.Engine;
using SpreadsheetApp.Core.Models;
using SpreadsheetApp.Scripting;
using Xunit;

namespace SpreadsheetApp.Tests;

public class ScriptingTests
{
    [Fact]
    public void RoslynCSharp_EvaluatesMathExpressions()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("Code");
        doc.AddTable(table);

        var scriptEngine = new CSharpScriptHost();
        var calcEngine = new CalculationEngine(scriptEngine);

        calcEngine.SetCellInput(doc, "Code", new CellCoordinate(0, 0), "=CS(10 * 25)");
        Assert.Equal(250.0, table.GetCell(0, 0)!.Value.NumberValue);

        calcEngine.SetCellInput(doc, "Code", new CellCoordinate(1, 0), "=CS(Math.Pow(2, 8))");
        Assert.Equal(256.0, table.GetCell(1, 0)!.Value.NumberValue);
    }

    [Fact]
    public void RoslynCSharp_CanQuerySpreadsheetRanges()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("Sales");
        doc.AddTable(table);

        var scriptEngine = new CSharpScriptHost();
        var calcEngine = new CalculationEngine(scriptEngine);

        calcEngine.SetCellInput(doc, "Sales", new CellCoordinate(0, 0), "10");
        calcEngine.SetCellInput(doc, "Sales", new CellCoordinate(1, 0), "20");
        calcEngine.SetCellInput(doc, "Sales", new CellCoordinate(2, 0), "30");

        // Use C# LINQ inside formula
        calcEngine.SetCellInput(doc, "Sales", new CellCoordinate(3, 0), "=CS(Range(\"A1:A3\").Where(x => x > 15).Sum())");
        Assert.Equal(50.0, table.GetCell(3, 0)!.Value.NumberValue);
    }
}
