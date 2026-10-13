namespace Tabulo.Core.Models;

public enum CellHorizontalAlignment
{
    Left,
    Center,
    Right
}

public record CellStyle
{
    public static readonly CellStyle Default = new();

    public string? FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 12.0;
    public bool IsBold { get; init; }
    public bool IsItalic { get; init; }
    public CellHorizontalAlignment Alignment { get; init; } = CellHorizontalAlignment.Left;
    public string? ForegroundColor { get; init; } = "#1E293B"; // Dark slate
    public string? BackgroundColor { get; init; } = null;       // Transparent
    public string? NumberFormat { get; init; } = null;         // "General", "C2", "P1", "N2"
}

public static class CellNumberFormats
{
    public const string General = "General";
    public const string Currency = "C2";
    public const string Percent = "P1";
    public const string Comma = "N2";
    public const string Integer = "N0";
    public const string ShortDate = "yyyy-MM-dd";
}
