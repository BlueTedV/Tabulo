using System.Text.Json;
using System.Text.Json.Serialization;
using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Core.Storage;

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
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public string? ForegroundColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? NumberFormat { get; set; }
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
                            IsBold = kvp.Value.Style.IsBold,
                            IsItalic = kvp.Value.Style.IsItalic,
                            ForegroundColor = kvp.Value.Style.ForegroundColor,
                            BackgroundColor = kvp.Value.Style.BackgroundColor,
                            NumberFormat = kvp.Value.Style.NumberFormat
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
                    cell.Style = new CellStyle
                    {
                        IsBold = cellDto.Style.IsBold,
                        IsItalic = cellDto.Style.IsItalic,
                        ForegroundColor = cellDto.Style.ForegroundColor,
                        BackgroundColor = cellDto.Style.BackgroundColor,
                        NumberFormat = cellDto.Style.NumberFormat
                    };
                }
            }

            doc.AddTable(table);
        }

        return doc;
    }
}
