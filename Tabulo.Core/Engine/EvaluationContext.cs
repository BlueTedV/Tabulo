using Tabulo.Core.Models;

namespace Tabulo.Core.Engine;

/// <summary>
/// Execution environment passed to AST nodes during formula evaluation.
/// </summary>
public class EvaluationContext
{
    public CanvasDocument Document { get; }
    public string CurrentTableName { get; }
    public IScriptEngine? ScriptEngine { get; }

    public EvaluationContext(CanvasDocument document, string currentTableName, IScriptEngine? scriptEngine = null)
    {
        Document = document;
        CurrentTableName = currentTableName;
        ScriptEngine = scriptEngine;
    }

    public TableBlock? GetTable(string? tableName)
    {
        var targetName = string.IsNullOrWhiteSpace(tableName) ? CurrentTableName : tableName;
        return Document.GetTableByName(targetName);
    }

    public CellValue GetCellValue(string? tableName, CellCoordinate coord)
    {
        var table = GetTable(tableName);
        if (table == null)
        {
            return CellValue.RefErr;
        }

        var cell = table.GetCell(coord);
        return cell?.Value ?? CellValue.Empty;
    }

    public IEnumerable<CellValue> GetRangeValues(string? tableName, CellRange range)
    {
        var table = GetTable(tableName);
        if (table == null)
        {
            yield break;
        }

        foreach (var coord in range.Coordinates)
        {
            var cell = table.GetCell(coord);
            yield return cell?.Value ?? CellValue.Empty;
        }
    }
}
