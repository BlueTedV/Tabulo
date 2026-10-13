using System.Text;
using System.Text.RegularExpressions;

namespace SpreadsheetApp.Core.Models;

/// <summary>
/// Represents a zero-based (Row, Column) position within a table.
/// </summary>
public readonly record struct CellCoordinate(int Row, int Column) : IComparable<CellCoordinate>
{
    private static readonly Regex A1Regex = new(@"^\$?([A-Za-z]+)\$?([0-9]+)$", RegexOptions.Compiled);

    public long ToKey() => ((long)Row << 32) | (uint)Column;

    public static CellCoordinate FromKey(long key) =>
        new((int)(key >> 32), (int)(key & 0xFFFFFFFF));

    /// <summary>
    /// Converts (0, 0) to "A1", (0, 25) to "Z1", (0, 26) to "AA1".
    /// </summary>
    public string ToA1()
    {
        var colStr = ColumnToLetters(Column);
        return $"{colStr}{Row + 1}";
    }

    /// <summary>
    /// Parses an A1 reference like "A1", "$B$10", "AA99".
    /// </summary>
    public static CellCoordinate FromA1(string a1)
    {
        var match = A1Regex.Match(a1.Trim());
        if (!match.Success)
        {
            throw new FormatException($"Invalid cell reference '{a1}'.");
        }

        var colLetters = match.Groups[1].Value.ToUpperInvariant();
        var rowNumber = int.Parse(match.Groups[2].Value);

        var col = LettersToColumn(colLetters);
        var row = rowNumber - 1;

        return new CellCoordinate(row, col);
    }

    public static bool TryFromA1(string a1, out CellCoordinate coord)
    {
        try
        {
            coord = FromA1(a1);
            return true;
        }
        catch
        {
            coord = default;
            return false;
        }
    }

    public static string ColumnToLetters(int column)
    {
        if (column < 0) return "?";
        var sb = new StringBuilder();
        var col = column;
        while (col >= 0)
        {
            sb.Insert(0, (char)('A' + (col % 26)));
            col = (col / 26) - 1;
        }
        return sb.ToString();
    }

    public static int LettersToColumn(string letters)
    {
        var col = 0;
        foreach (var c in letters)
        {
            if (c is < 'A' or > 'Z') throw new FormatException($"Invalid column character '{c}'.");
            col = col * 26 + (c - 'A' + 1);
        }
        return col - 1;
    }

    public int CompareTo(CellCoordinate other)
    {
        var rowComparison = Row.CompareTo(other.Row);
        return rowComparison != 0 ? rowComparison : Column.CompareTo(other.Column);
    }

    public override string ToString() => ToA1();
}
