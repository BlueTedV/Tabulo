namespace SpreadsheetApp.Core.Models;

/// <summary>
/// Represents a rectangular region of cells defined by start and end coordinates.
/// </summary>
public readonly record struct CellRange(CellCoordinate Start, CellCoordinate End)
{
    public int MinRow => Math.Min(Start.Row, End.Row);
    public int MaxRow => Math.Max(Start.Row, End.Row);
    public int MinCol => Math.Min(Start.Column, End.Column);
    public int MaxCol => Math.Max(Start.Column, End.Column);

    public int RowCount => MaxRow - MinRow + 1;
    public int ColumnCount => MaxCol - MinCol + 1;
    public int TotalCells => RowCount * ColumnCount;

    public CellRange(int startRow, int startCol, int endRow, int endCol)
        : this(new CellCoordinate(startRow, startCol), new CellCoordinate(endRow, endCol))
    {
    }

    public static CellRange FromSingle(CellCoordinate coord) => new(coord, coord);

    public static CellRange FromA1(string a1Range)
    {
        var trimmed = a1Range.Trim();
        var parts = trimmed.Split(':');
        if (parts.Length == 1)
        {
            var single = CellCoordinate.FromA1(parts[0]);
            return new CellRange(single, single);
        }

        if (parts.Length == 2)
        {
            var start = CellCoordinate.FromA1(parts[0]);
            var end = CellCoordinate.FromA1(parts[1]);
            return new CellRange(start, end);
        }

        throw new FormatException($"Invalid range expression '{a1Range}'.");
    }

    public bool Contains(CellCoordinate coord) =>
        coord.Row >= MinRow && coord.Row <= MaxRow &&
        coord.Column >= MinCol && coord.Column <= MaxCol;

    public IEnumerable<CellCoordinate> Coordinates
    {
        get
        {
            for (var r = MinRow; r <= MaxRow; r++)
            {
                for (var c = MinCol; c <= MaxCol; c++)
                {
                    yield return new CellCoordinate(r, c);
                }
            }
        }
    }

    public override string ToString()
    {
        var startA1 = new CellCoordinate(MinRow, MinCol).ToA1();
        var endA1 = new CellCoordinate(MaxRow, MaxCol).ToA1();
        return startA1 == endA1 ? startA1 : $"{startA1}:{endA1}";
    }
}
