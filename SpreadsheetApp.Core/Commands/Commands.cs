using SpreadsheetApp.Core.Engine;
using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Core.Commands;

public class CellEditCommand : IUndoableCommand
{
    private readonly CanvasDocument _document;
    private readonly CalculationEngine _calcEngine;
    private readonly string _tableName;
    private readonly CellCoordinate _coord;
    private readonly string _oldInput;
    private readonly string _newInput;

    public string Description => $"Edit {_tableName}!{_coord.ToA1()}";

    public CellEditCommand(CanvasDocument doc, CalculationEngine calcEngine, string tableName, CellCoordinate coord, string oldInput, string newInput)
    {
        _document = doc;
        _calcEngine = calcEngine;
        _tableName = tableName;
        _coord = coord;
        _oldInput = oldInput;
        _newInput = newInput;
    }

    public void Execute()
    {
        _calcEngine.SetCellInput(_document, _tableName, _coord, _newInput);
    }

    public void Undo()
    {
        _calcEngine.SetCellInput(_document, _tableName, _coord, _oldInput);
    }
}

public class MoveTableCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly double _oldX;
    private readonly double _oldY;
    private readonly double _newX;
    private readonly double _newY;

    public string Description => $"Move {_table.Name}";

    public MoveTableCommand(TableBlock table, double oldX, double oldY, double newX, double newY)
    {
        _table = table;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
    }

    public void Execute()
    {
        _table.X = _newX;
        _table.Y = _newY;
    }

    public void Undo()
    {
        _table.X = _oldX;
        _table.Y = _oldY;
    }
}

public class ResizeColumnCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly int _colIndex;
    private readonly double _oldWidth;
    private readonly double _newWidth;

    public string Description => $"Resize column {CellCoordinate.ColumnToLetters(_colIndex)} in {_table.Name}";

    public ResizeColumnCommand(TableBlock table, int colIndex, double oldWidth, double newWidth)
    {
        _table = table;
        _colIndex = colIndex;
        _oldWidth = oldWidth;
        _newWidth = newWidth;
    }

    public void Execute() => _table.SetColumnWidth(_colIndex, _newWidth);
    public void Undo() => _table.SetColumnWidth(_colIndex, _oldWidth);
}

public class ResizeRowCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly int _rowIndex;
    private readonly double _oldHeight;
    private readonly double _newHeight;

    public string Description => $"Resize row {_rowIndex + 1} in {_table.Name}";

    public ResizeRowCommand(TableBlock table, int rowIndex, double oldHeight, double newHeight)
    {
        _table = table;
        _rowIndex = rowIndex;
        _oldHeight = oldHeight;
        _newHeight = newHeight;
    }

    public void Execute() => _table.SetRowHeight(_rowIndex, _newHeight);
    public void Undo() => _table.SetRowHeight(_rowIndex, _oldHeight);
}

public class AddTableCommand : IUndoableCommand
{
    private readonly CanvasDocument _document;
    private readonly TableBlock _table;

    public string Description => $"Add table {_table.Name}";

    public AddTableCommand(CanvasDocument document, TableBlock table)
    {
        _document = document;
        _table = table;
    }

    public void Execute() => _document.AddTable(_table);
    public void Undo() => _document.RemoveTable(_table);
}

public class RemoveTableCommand : IUndoableCommand
{
    private readonly CanvasDocument _document;
    private readonly TableBlock _table;

    public string Description => $"Delete table {_table.Name}";

    public RemoveTableCommand(CanvasDocument document, TableBlock table)
    {
        _document = document;
        _table = table;
    }

    public void Execute() => _document.RemoveTable(_table);
    public void Undo() => _document.AddTable(_table);
}

public class RenameTableCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly string _oldName;
    private readonly string _newName;

    public string Description => $"Rename table {_oldName} to {_newName}";

    public RenameTableCommand(TableBlock table, string oldName, string newName)
    {
        _table = table;
        _oldName = oldName;
        _newName = newName;
    }

    public void Execute() => _table.Name = _newName;
    public void Undo() => _table.Name = _oldName;
}

public class SetTableHeaderColorCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly string? _oldColor;
    private readonly string? _newColor;

    public string Description => $"Set header color of {_table.Name}";

    public SetTableHeaderColorCommand(TableBlock table, string? oldColor, string? newColor)
    {
        _table = table;
        _oldColor = oldColor;
        _newColor = newColor;
    }

    public void Execute() => _table.HeaderColor = _newColor;
    public void Undo() => _table.HeaderColor = _oldColor;
}

public class DuplicateTableCommand : IUndoableCommand
{
    private readonly CanvasDocument _document;
    private readonly TableBlock _newTable;

    public string Description => $"Duplicate table {_newTable.Name}";

    public DuplicateTableCommand(CanvasDocument document, TableBlock original)
    {
        _document = document;
        _newTable = original.Clone($"{original.Name}_Copy", original.X + 40, original.Y + 40);
    }

    public TableBlock DuplicatedTable => _newTable;
    public void Execute() => _document.AddTable(_newTable);
    public void Undo() => _document.RemoveTable(_newTable);
}

public class ResizeTableDimensionsCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly int _oldCols;
    private readonly int _oldRows;
    private readonly int _newCols;
    private readonly int _newRows;

    public string Description => $"Resize {_table.Name} dimensions to {_newCols}x{_newRows}";

    public ResizeTableDimensionsCommand(TableBlock table, int oldCols, int oldRows, int newCols, int newRows)
    {
        _table = table;
        _oldCols = oldCols;
        _oldRows = oldRows;
        _newCols = newCols;
        _newRows = newRows;
    }

    public void Execute() => _table.ResizeDimensions(_newCols, _newRows);
    public void Undo() => _table.ResizeDimensions(_oldCols, _oldRows);
}

public class InsertRowCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly int _rowIndex;

    public string Description => $"Insert row at {_rowIndex + 1} in {_table.Name}";

    public InsertRowCommand(TableBlock table, int rowIndex)
    {
        _table = table;
        _rowIndex = rowIndex;
    }

    public void Execute() => _table.InsertRow(_rowIndex);
    public void Undo() => _table.DeleteRow(_rowIndex);
}

public class DeleteRowCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly int _rowIndex;
    private Dictionary<long, CellModel>? _savedCells;

    public string Description => $"Delete row {_rowIndex + 1} from {_table.Name}";

    public DeleteRowCommand(TableBlock table, int rowIndex)
    {
        _table = table;
        _rowIndex = rowIndex;
    }

    public void Execute()
    {
        _savedCells = _table.CloneCells();
        _table.DeleteRow(_rowIndex);
    }

    public void Undo()
    {
        if (_savedCells != null)
        {
            _table.Cells.Clear();
            foreach (var kvp in _savedCells) _table.Cells[kvp.Key] = kvp.Value;
            _table.RowCount++;
        }
    }
}

public class InsertColumnCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly int _colIndex;

    public string Description => $"Insert column at {CellCoordinate.ColumnToLetters(_colIndex)} in {_table.Name}";

    public InsertColumnCommand(TableBlock table, int colIndex)
    {
        _table = table;
        _colIndex = colIndex;
    }

    public void Execute() => _table.InsertColumn(_colIndex);
    public void Undo() => _table.DeleteColumn(_colIndex);
}

public class DeleteColumnCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly int _colIndex;
    private Dictionary<long, CellModel>? _savedCells;

    public string Description => $"Delete column {CellCoordinate.ColumnToLetters(_colIndex)} from {_table.Name}";

    public DeleteColumnCommand(TableBlock table, int colIndex)
    {
        _table = table;
        _colIndex = colIndex;
    }

    public void Execute()
    {
        _savedCells = _table.CloneCells();
        _table.DeleteColumn(_colIndex);
    }

    public void Undo()
    {
        if (_savedCells != null)
        {
            _table.Cells.Clear();
            foreach (var kvp in _savedCells) _table.Cells[kvp.Key] = kvp.Value;
            _table.ColumnCount++;
        }
    }
}

public class ClearCellCommand : IUndoableCommand
{
    private readonly CanvasDocument _document;
    private readonly CalculationEngine _calcEngine;
    private readonly string _tableName;
    private readonly CellCoordinate _coord;
    private readonly string _oldInput;

    public string Description => $"Clear cell {_tableName}!{_coord.ToA1()}";

    public ClearCellCommand(CanvasDocument doc, CalculationEngine calcEngine, string tableName, CellCoordinate coord, string oldInput)
    {
        _document = doc;
        _calcEngine = calcEngine;
        _tableName = tableName;
        _coord = coord;
        _oldInput = oldInput;
    }

    public void Execute() => _calcEngine.SetCellInput(_document, _tableName, _coord, string.Empty);
    public void Undo() => _calcEngine.SetCellInput(_document, _tableName, _coord, _oldInput);
}

public record CellInputEdit(CellCoordinate Coordinate, string OldInput, string NewInput);

public class BatchCellEditCommand : IUndoableCommand
{
    private readonly CanvasDocument _document;
    private readonly CalculationEngine _calcEngine;
    private readonly string _tableName;
    private readonly List<CellInputEdit> _edits;
    public string Description { get; }

    public BatchCellEditCommand(CanvasDocument doc, CalculationEngine calcEngine, string tableName, List<CellInputEdit> edits, string description)
    {
        _document = doc;
        _calcEngine = calcEngine;
        _tableName = tableName;
        _edits = edits;
        Description = description;
    }

    public void Execute()
    {
        _calcEngine.SetCellInputs(_document, _tableName, _edits.Select(e => (e.Coordinate, e.NewInput)));
    }

    public void Undo()
    {
        _calcEngine.SetCellInputs(_document, _tableName, _edits.Select(e => (e.Coordinate, e.OldInput)));
    }
}

public record CellStyleEdit(CellCoordinate Coordinate, CellStyle OldStyle, CellStyle NewStyle);

public class BatchFormatCommand : IUndoableCommand
{
    private readonly TableBlock _table;
    private readonly List<CellStyleEdit> _edits;
    public string Description { get; }

    public BatchFormatCommand(TableBlock table, List<CellStyleEdit> edits, string description)
    {
        _table = table;
        _edits = edits;
        Description = description;
    }

    public void Execute()
    {
        foreach (var edit in _edits)
        {
            var cell = _table.GetOrCreateCell(edit.Coordinate);
            cell.Style = edit.NewStyle;
        }
    }

    public void Undo()
    {
        foreach (var edit in _edits)
        {
            var cell = _table.GetOrCreateCell(edit.Coordinate);
            cell.Style = edit.OldStyle;
        }
    }
}


