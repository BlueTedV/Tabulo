using System.Globalization;
using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Core.Engine;

/// <summary>
/// Orchestrates formula parsing, dependency tracking, and incremental recalculation.
/// </summary>
public class CalculationEngine
{
    private readonly DependencyGraph _graph = new();
    private readonly Dictionary<QualifiedCellAddress, FormulaNode> _astCache = new();
    public IScriptEngine? ScriptEngine { get; set; }

    public event EventHandler? Recalculated;

    public CalculationEngine(IScriptEngine? scriptEngine = null)
    {
        ScriptEngine = scriptEngine;
    }

    public DependencyGraph Graph => _graph;

    public void RecalculateAll(CanvasDocument doc)
    {
        // 1. Clear caches and rebuild dependency graph from scratch
        _astCache.Clear();

        var formulaCells = new List<QualifiedCellAddress>();

        foreach (var table in doc.Tables)
        {
            foreach (var (key, cell) in table.Cells)
            {
                var coord = CellCoordinate.FromKey(key);
                var address = new QualifiedCellAddress(table.Name, coord);

                if (cell.IsFormula)
                {
                    try
                    {
                        var ast = FormulaParser.Parse(cell.RawInput);
                        _astCache[address] = ast;
                        var deps = ast.GetCellDependencies(table.Name);
                        _graph.SetDependencies(address, deps);
                        formulaCells.Add(address);
                    }
                    catch (Exception ex)
                    {
                        cell.Value = CellValue.Error($"#SYNTAX! {ex.Message}");
                    }
                }
                else
                {
                    _graph.SetDependencies(address, Enumerable.Empty<QualifiedCellAddress>());
                    cell.Value = ParseLiteralValue(cell.RawInput);
                }
            }
        }

        // 2. Evaluate all formula cells in topological order
        // Provide all cells as seeds to evaluate the entire DAG
        var order = _graph.GetEvaluationOrder(doc.Tables.SelectMany(t => t.Cells.Keys.Select(k => new QualifiedCellAddress(t.Name, CellCoordinate.FromKey(k)))), out var circularCells);

        foreach (var circular in circularCells)
        {
            var table = doc.GetTableByName(circular.TableName);
            var cell = table?.GetCell(circular.Coordinate);
            if (cell != null)
            {
                cell.Value = CellValue.CircularErr;
            }
        }

        foreach (var addr in order)
        {
            EvaluateSingleCell(doc, addr);
        }

        Recalculated?.Invoke(this, EventArgs.Empty);
    }

    public void SetCellInput(CanvasDocument doc, string tableName, CellCoordinate coord, string rawInput)
    {
        var table = doc.GetTableByName(tableName);
        if (table == null) return;

        var cell = table.GetOrCreateCell(coord);
        cell.RawInput = rawInput;

        var addr = new QualifiedCellAddress(tableName, coord);

        if (cell.IsFormula)
        {
            try
            {
                var ast = FormulaParser.Parse(rawInput);
                _astCache[addr] = ast;
                var deps = ast.GetCellDependencies(tableName);
                _graph.SetDependencies(addr, deps);
            }
            catch (Exception ex)
            {
                cell.Value = CellValue.Error($"#SYNTAX! {ex.Message}");
                _astCache.Remove(addr);
                _graph.SetDependencies(addr, Enumerable.Empty<QualifiedCellAddress>());
            }
        }
        else
        {
            _astCache.Remove(addr);
            _graph.SetDependencies(addr, Enumerable.Empty<QualifiedCellAddress>());
            cell.Value = ParseLiteralValue(rawInput);
        }

        // Incremental recalculation
        RecalculateDependents(doc, addr);
    }

    public void SetCellInputs(CanvasDocument doc, string tableName, IEnumerable<(CellCoordinate coord, string rawInput)> edits)
    {
        var table = doc.GetTableByName(tableName);
        if (table == null) return;

        var changedAddrs = new List<QualifiedCellAddress>();

        foreach (var (coord, rawInput) in edits)
        {
            var cell = table.GetOrCreateCell(coord);
            cell.RawInput = rawInput;

            var addr = new QualifiedCellAddress(tableName, coord);
            changedAddrs.Add(addr);

            if (cell.IsFormula)
            {
                try
                {
                    var ast = FormulaParser.Parse(rawInput);
                    _astCache[addr] = ast;
                    var deps = ast.GetCellDependencies(tableName);
                    _graph.SetDependencies(addr, deps);
                }
                catch (Exception ex)
                {
                    cell.Value = CellValue.Error($"#SYNTAX! {ex.Message}");
                    _astCache.Remove(addr);
                    _graph.SetDependencies(addr, Enumerable.Empty<QualifiedCellAddress>());
                }
            }
            else
            {
                _astCache.Remove(addr);
                _graph.SetDependencies(addr, Enumerable.Empty<QualifiedCellAddress>());
                cell.Value = ParseLiteralValue(rawInput);
            }
        }

        RecalculateDependents(doc, changedAddrs);
    }

    public void RecalculateDependents(CanvasDocument doc, QualifiedCellAddress changedCell)
    {
        RecalculateDependents(doc, new[] { changedCell });
    }

    public void RecalculateDependents(CanvasDocument doc, IEnumerable<QualifiedCellAddress> changedCells)
    {
        var changedList = changedCells.ToList();

        // First evaluate the changed cells themselves if they are formulas
        foreach (var changedCell in changedList)
        {
            if (_astCache.ContainsKey(changedCell))
            {
                EvaluateSingleCell(doc, changedCell);
            }
        }

        var order = _graph.GetEvaluationOrder(changedList, out var circularCells);

        foreach (var circular in circularCells)
        {
            var table = doc.GetTableByName(circular.TableName);
            var cell = table?.GetCell(circular.Coordinate);
            if (cell != null)
            {
                cell.Value = CellValue.CircularErr;
            }
        }

        foreach (var addr in order)
        {
            EvaluateSingleCell(doc, addr);
        }

        Recalculated?.Invoke(this, EventArgs.Empty);
    }

    private void EvaluateSingleCell(CanvasDocument doc, QualifiedCellAddress addr)
    {
        var table = doc.GetTableByName(addr.TableName);
        if (table == null) return;

        var cell = table.GetCell(addr.Coordinate);
        if (cell == null || !cell.IsFormula) return;

        if (_astCache.TryGetValue(addr, out var ast))
        {
            var context = new EvaluationContext(doc, addr.TableName, ScriptEngine);
            try
            {
                cell.Value = ast.Evaluate(context);
            }
            catch (Exception ex)
            {
                cell.Value = CellValue.Error($"#ERR! {ex.Message}");
            }
        }
    }

    private static CellValue ParseLiteralValue(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return CellValue.Empty;
        }

        var trimmed = input.Trim();

        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            return new CellValue(num);
        }

        if (bool.TryParse(trimmed, out var b))
        {
            return new CellValue(b);
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, out var dt))
        {
            return new CellValue(dt);
        }

        return new CellValue(input);
    }
}
