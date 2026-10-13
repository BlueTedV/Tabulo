namespace SpreadsheetApp.Core.Models;

/// <summary>
/// An independent table situated at (X, Y) on the infinite 2D canvas.
/// Uses sparse storage for memory efficiency.
/// </summary>
public class TableBlock
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Table";

    // Canvas Position (in canvas units)
    public double X { get; set; }
    public double Y { get; set; }

    public int ColumnCount { get; set; } = 10;
    public int RowCount { get; set; } = 25;

    public double DefaultColumnWidth { get; set; } = 85.0;
    public double DefaultRowHeight { get; set; } = 26.0;
    public double HeaderHeight { get; set; } = 28.0;
    public string? HeaderColor { get; set; } = null; // Accent color hex (e.g., "#2563EB", "#059669")

    public Dictionary<int, double> ColumnWidths { get; init; } = new();
    public Dictionary<int, double> RowHeights { get; init; } = new();

    // Sparse storage: key is CellCoordinate.ToKey()
    public Dictionary<long, CellModel> Cells { get; init; } = new();

    public TableBlock() { }

    public TableBlock(string name, double x = 50, double y = 50, int cols = 10, int rows = 25)
    {
        Name = name;
        X = x;
        Y = y;
        ColumnCount = cols;
        RowCount = rows;
    }

    public CellModel? GetCell(CellCoordinate coord)
    {
        return Cells.TryGetValue(coord.ToKey(), out var cell) ? cell : null;
    }

    public CellModel? GetCell(int row, int col) => GetCell(new CellCoordinate(row, col));

    public CellModel GetOrCreateCell(CellCoordinate coord)
    {
        var key = coord.ToKey();
        if (!Cells.TryGetValue(key, out var cell))
        {
            cell = new CellModel();
            Cells[key] = cell;

            // Automatically expand table dimensions if cell coordinate exceeds current bounds
            if (coord.Row >= RowCount) RowCount = coord.Row + 1;
            if (coord.Column >= ColumnCount) ColumnCount = coord.Column + 1;
        }
        return cell;
    }

    public CellModel GetOrCreateCell(int row, int col) => GetOrCreateCell(new CellCoordinate(row, col));

    public void SetCell(CellCoordinate coord, CellModel cell)
    {
        Cells[coord.ToKey()] = cell;
        if (coord.Row >= RowCount) RowCount = coord.Row + 1;
        if (coord.Column >= ColumnCount) ColumnCount = coord.Column + 1;
    }

    public void RemoveCell(CellCoordinate coord)
    {
        Cells.Remove(coord.ToKey());
    }

    public double GetColumnWidth(int col)
    {
        return ColumnWidths.TryGetValue(col, out var w) ? w : DefaultColumnWidth;
    }

    public void SetColumnWidth(int col, double width)
    {
        ColumnWidths[col] = Math.Max(20.0, width);
    }

    public double GetRowHeight(int row)
    {
        return RowHeights.TryGetValue(row, out var h) ? h : DefaultRowHeight;
    }

    public void SetRowHeight(int row, double height)
    {
        RowHeights[row] = Math.Max(15.0, height);
    }

    public double GetTotalWidth()
    {
        // 40 for row header index column + sum of column widths
        const double rowHeaderWidth = 40.0;
        var sum = rowHeaderWidth;
        for (var c = 0; c < ColumnCount; c++)
        {
            sum += GetColumnWidth(c);
        }
        return sum;
    }

    public double GetTotalHeight()
    {
        // Title bar (28) + Col Header (24) + sum of row heights
        const double titleBarHeight = 28.0;
        var sum = titleBarHeight + HeaderHeight;
        for (var r = 0; r < RowCount; r++)
        {
            sum += GetRowHeight(r);
        }
        return sum;
    }

    public double GetColumnX(int col)
    {
        const double rowHeaderWidth = 40.0;
        var sum = rowHeaderWidth;
        for (var c = 0; c < col; c++)
        {
            sum += GetColumnWidth(c);
        }
        return sum;
    }

    public double GetRowY(int row)
    {
        const double titleBarHeight = 28.0;
        var sum = titleBarHeight + HeaderHeight;
        for (var r = 0; r < row; r++)
        {
            sum += GetRowHeight(r);
        }
        return sum;
    }

    public void ResizeDimensions(int newCols, int newRows)
    {
        ColumnCount = Math.Max(1, newCols);
        RowCount = Math.Max(1, newRows);
    }

    public void InsertRow(int atIndex)
    {
        var newCells = new Dictionary<long, CellModel>();
        foreach (var (key, cell) in Cells)
        {
            var coord = CellCoordinate.FromKey(key);
            if (coord.Row >= atIndex)
            {
                var newCoord = new CellCoordinate(coord.Row + 1, coord.Column);
                newCells[newCoord.ToKey()] = cell;
            }
            else
            {
                newCells[key] = cell;
            }
        }
        Cells.Clear();
        foreach (var kvp in newCells) Cells[kvp.Key] = kvp.Value;
        RowCount++;
    }

    public void DeleteRow(int atIndex)
    {
        if (RowCount <= 1) return;
        var newCells = new Dictionary<long, CellModel>();
        foreach (var (key, cell) in Cells)
        {
            var coord = CellCoordinate.FromKey(key);
            if (coord.Row == atIndex)
            {
                continue; // Deleted
            }
            if (coord.Row > atIndex)
            {
                var newCoord = new CellCoordinate(coord.Row - 1, coord.Column);
                newCells[newCoord.ToKey()] = cell;
            }
            else
            {
                newCells[key] = cell;
            }
        }
        Cells.Clear();
        foreach (var kvp in newCells) Cells[kvp.Key] = kvp.Value;
        RowCount--;
    }

    public void InsertColumn(int atIndex)
    {
        var newCells = new Dictionary<long, CellModel>();
        foreach (var (key, cell) in Cells)
        {
            var coord = CellCoordinate.FromKey(key);
            if (coord.Column >= atIndex)
            {
                var newCoord = new CellCoordinate(coord.Row, coord.Column + 1);
                newCells[newCoord.ToKey()] = cell;
            }
            else
            {
                newCells[key] = cell;
            }
        }
        Cells.Clear();
        foreach (var kvp in newCells) Cells[kvp.Key] = kvp.Value;
        ColumnCount++;
    }

    public void DeleteColumn(int atIndex)
    {
        if (ColumnCount <= 1) return;
        var newCells = new Dictionary<long, CellModel>();
        foreach (var (key, cell) in Cells)
        {
            var coord = CellCoordinate.FromKey(key);
            if (coord.Column == atIndex)
            {
                continue; // Deleted
            }
            if (coord.Column > atIndex)
            {
                var newCoord = new CellCoordinate(coord.Row, coord.Column - 1);
                newCells[newCoord.ToKey()] = cell;
            }
            else
            {
                newCells[key] = cell;
            }
        }
        Cells.Clear();
        foreach (var kvp in newCells) Cells[kvp.Key] = kvp.Value;
        ColumnCount--;
    }

    public Dictionary<long, CellModel> CloneCells()
    {
        return Cells.ToDictionary(k => k.Key, v => v.Value.Clone());
    }

    public TableBlock Clone(string newName, double newX, double newY)
    {
        var copy = new TableBlock(newName, newX, newY, ColumnCount, RowCount)
        {
            DefaultColumnWidth = DefaultColumnWidth,
            DefaultRowHeight = DefaultRowHeight,
            HeaderHeight = HeaderHeight,
            HeaderColor = HeaderColor
        };
        foreach (var (col, width) in ColumnWidths) copy.ColumnWidths[col] = width;
        foreach (var (row, height) in RowHeights) copy.RowHeights[row] = height;
        foreach (var (key, cell) in CloneCells()) copy.Cells[key] = cell;
        return copy;
    }
}
