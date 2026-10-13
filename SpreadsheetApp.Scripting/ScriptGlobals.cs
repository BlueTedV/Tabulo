using SpreadsheetApp.Core.Engine;
using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Scripting;

/// <summary>
/// Execution globals exposed to C# Roslyn formulas.
/// </summary>
public class ScriptGlobals
{
    public CanvasDocument Document { get; }
    public string CurrentTable { get; }
    public EvaluationContext Context { get; }

    public ScriptGlobals(EvaluationContext context)
    {
        Context = context;
        Document = context.Document;
        CurrentTable = context.CurrentTableName;
    }

    public TableBlock? Table(string name) => Document.GetTableByName(name);

    public CellValue Cell(string a1) => Context.GetCellValue(CurrentTable, CellCoordinate.FromA1(a1));

    public CellValue Cell(string tableName, string a1) => Context.GetCellValue(tableName, CellCoordinate.FromA1(a1));

    public double Val(string a1) => Cell(a1).AsNumber();

    public double Val(string tableName, string a1) => Cell(tableName, a1).AsNumber();

    public IEnumerable<double> Range(string a1Range)
    {
        var range = CellRange.FromA1(a1Range);
        foreach (var cellVal in Context.GetRangeValues(CurrentTable, range))
        {
            if (cellVal.TryGetNumber(out var num))
            {
                yield return num;
            }
        }
    }

    public IEnumerable<double> Range(string tableName, string a1Range)
    {
        var range = CellRange.FromA1(a1Range);
        foreach (var cellVal in Context.GetRangeValues(tableName, range))
        {
            if (cellVal.TryGetNumber(out var num))
            {
                yield return num;
            }
        }
    }
}
