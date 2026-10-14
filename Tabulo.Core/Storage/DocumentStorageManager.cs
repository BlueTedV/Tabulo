using System.Text.Json;
using System.Text.Json.Serialization;
using Tabulo.Core.Models;

namespace Tabulo.Core.Storage;

public class DocumentDto
{
    public string Name { get; set; } = string.Empty;
    public List<TableDto> Tables { get; set; } = new();
}

public class TableDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string? HeaderColor { get; set; }
    public int ColumnCount { get; set; }
    public int RowCount { get; set; }
    public Dictionary<int, double> ColumnWidths { get; set; } = new();
    public Dictionary<int, double> RowHeights { get; set; } = new();
    public Dictionary<string, CellDto> Cells { get; set; } = new();
}

public class CellDto
{
    public string RawInput { get; set; } = string.Empty;
    public CellStyleDto? Style { get; set; }
}

public class CellStyleDto
{
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public bool IsStrikethrough { get; set; }
    public string? Alignment { get; set; }
    public string? VerticalAlignment { get; set; }
    public string? ForegroundColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? NumberFormat { get; set; }
    public bool WrapText { get; set; }
}

public static class DocumentStorageManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public static string SerializeToJson(CanvasDocument doc)
    {
        var dto = new DocumentDto
        {
            Name = doc.Name,
            Tables = doc.Tables.Select(t => new TableDto
            {
                Id = t.Id,
                Name = t.Name,
                X = t.X,
                Y = t.Y,
                HeaderColor = t.HeaderColor,
                ColumnCount = t.ColumnCount,
                RowCount = t.RowCount,
                ColumnWidths = new Dictionary<int, double>(t.ColumnWidths),
                RowHeights = new Dictionary<int, double>(t.RowHeights),
                Cells = t.Cells.ToDictionary(
                    kvp => CellCoordinate.FromKey(kvp.Key).ToA1(),
                    kvp => new CellDto
                    {
                        RawInput = kvp.Value.RawInput,
                        Style = kvp.Value.Style == CellStyle.Default ? null : new CellStyleDto
                        {
                            FontFamily = kvp.Value.Style.FontFamily,
                            FontSize = kvp.Value.Style.FontSize,
                            IsBold = kvp.Value.Style.IsBold,
                            IsItalic = kvp.Value.Style.IsItalic,
                            IsUnderline = kvp.Value.Style.IsUnderline,
                            IsStrikethrough = kvp.Value.Style.IsStrikethrough,
                            Alignment = kvp.Value.Style.Alignment.ToString(),
                            VerticalAlignment = kvp.Value.Style.VerticalAlignment.ToString(),
                            ForegroundColor = kvp.Value.Style.ForegroundColor,
                            BackgroundColor = kvp.Value.Style.BackgroundColor,
                            NumberFormat = kvp.Value.Style.NumberFormat,
                            WrapText = kvp.Value.Style.WrapText
                        }
                    })
            }).ToList()
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static CanvasDocument DeserializeFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<DocumentDto>(json, JsonOptions);
        if (dto == null) throw new InvalidOperationException("Failed to deserialize document JSON.");

        var doc = new CanvasDocument
        {
            Name = dto.Name
        };

        foreach (var tDto in dto.Tables)
        {
            var table = new TableBlock(tDto.Name, tDto.X, tDto.Y, tDto.ColumnCount, tDto.RowCount)
            {
                Id = tDto.Id,
                HeaderColor = tDto.HeaderColor
            };

            foreach (var (c, w) in tDto.ColumnWidths) table.ColumnWidths[c] = w;
            foreach (var (r, h) in tDto.RowHeights) table.RowHeights[r] = h;

            foreach (var (a1, cellDto) in tDto.Cells)
            {
                var coord = CellCoordinate.FromA1(a1);
                var cell = table.GetOrCreateCell(coord);
                cell.RawInput = cellDto.RawInput;

                if (cellDto.Style != null)
                {
                    Enum.TryParse<CellHorizontalAlignment>(cellDto.Style.Alignment, true, out var hAlign);
                    Enum.TryParse<CellVerticalAlignment>(cellDto.Style.VerticalAlignment, true, out var vAlign);

                    cell.Style = new CellStyle
                    {
                        FontFamily = cellDto.Style.FontFamily ?? "Segoe UI",
                        FontSize = cellDto.Style.FontSize ?? 11.5,
                        IsBold = cellDto.Style.IsBold,
                        IsItalic = cellDto.Style.IsItalic,
                        IsUnderline = cellDto.Style.IsUnderline,
                        IsStrikethrough = cellDto.Style.IsStrikethrough,
                        Alignment = hAlign,
                        VerticalAlignment = vAlign,
                        ForegroundColor = cellDto.Style.ForegroundColor,
                        BackgroundColor = cellDto.Style.BackgroundColor,
                        NumberFormat = cellDto.Style.NumberFormat,
                        WrapText = cellDto.Style.WrapText
                    };
                }
            }

            doc.AddTable(table);
        }

        return doc;
    }
}
