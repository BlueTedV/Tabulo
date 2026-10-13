using Tabulo.Core.Models;

namespace Tabulo.Core.Engine;

/// <summary>
/// A fully qualified cell reference incorporating table name and coordinate, e.g. "Sales!B5".
/// </summary>
public readonly record struct QualifiedCellAddress(string TableName, CellCoordinate Coordinate)
{
    public override string ToString() => $"{TableName}!{Coordinate.ToA1()}";

    public static QualifiedCellAddress Parse(string qualifiedA1, string defaultTable)
    {
        var parts = qualifiedA1.Split('!');
        if (parts.Length == 1)
        {
            return new QualifiedCellAddress(defaultTable, CellCoordinate.FromA1(parts[0]));
        }
        if (parts.Length == 2)
        {
            return new QualifiedCellAddress(parts[0].Trim(), CellCoordinate.FromA1(parts[1]));
        }
        throw new FormatException($"Invalid cell reference '{qualifiedA1}'.");
    }
}

/// <summary>
/// A fully qualified range reference incorporating table name and range, e.g. "Sales!B2:B10".
/// </summary>
public readonly record struct QualifiedCellRange(string TableName, CellRange Range)
{
    public override string ToString() => $"{TableName}!{Range}";

    public static QualifiedCellRange Parse(string qualifiedRange, string defaultTable)
    {
        var parts = qualifiedRange.Split('!');
        if (parts.Length == 1)
        {
            return new QualifiedCellRange(defaultTable, CellRange.FromA1(parts[0]));
        }
        if (parts.Length == 2)
        {
            return new QualifiedCellRange(parts[0].Trim(), CellRange.FromA1(parts[1]));
        }
        throw new FormatException($"Invalid range reference '{qualifiedRange}'.");
    }
}
