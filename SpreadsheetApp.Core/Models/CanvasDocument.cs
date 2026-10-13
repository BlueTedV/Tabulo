using System.Collections.ObjectModel;

namespace SpreadsheetApp.Core.Models;

/// <summary>
/// Root document model representing the infinite 2D canvas and its child tables.
/// </summary>
public class CanvasDocument
{
    public string Name { get; set; } = "Untitled Workspace";
    public ObservableCollection<TableBlock> Tables { get; init; } = new();

    public event EventHandler? DocumentChanged;

    public CanvasDocument()
    {
        Tables.CollectionChanged += (_, _) => NotifyChanged();
    }

    public void NotifyChanged()
    {
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public TableBlock? GetTableByName(string name)
    {
        return Tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public TableBlock? GetTableById(Guid id)
    {
        return Tables.FirstOrDefault(t => t.Id == id);
    }

    public void AddTable(TableBlock table)
    {
        if (Tables.Any(t => string.Equals(t.Name, table.Name, StringComparison.OrdinalIgnoreCase)))
        {
            // Auto-rename if duplicate
            var counter = 2;
            var baseName = table.Name;
            while (Tables.Any(t => string.Equals(t.Name, $"{baseName}_{counter}", StringComparison.OrdinalIgnoreCase)))
            {
                counter++;
            }
            table.Name = $"{baseName}_{counter}";
        }

        Tables.Add(table);
    }

    public bool RemoveTable(TableBlock table) => Tables.Remove(table);

    public static CanvasDocument CreateDefault()
    {
        var doc = new CanvasDocument { Name = "Financial Model" };

        var salesTable = new TableBlock("Sales", x: 40, y: 40, cols: 5, rows: 10);
        salesTable.GetOrCreateCell(0, 0).RawInput = "Quarter";
        salesTable.GetOrCreateCell(0, 1).RawInput = "Revenue";
        salesTable.GetOrCreateCell(0, 2).RawInput = "COGS";
        salesTable.GetOrCreateCell(0, 3).RawInput = "Profit";

        salesTable.GetOrCreateCell(1, 0).RawInput = "Q1";
        salesTable.GetOrCreateCell(1, 1).RawInput = "10000";
        salesTable.GetOrCreateCell(1, 2).RawInput = "4000";
        salesTable.GetOrCreateCell(1, 3).RawInput = "=B2-C2";

        salesTable.GetOrCreateCell(2, 0).RawInput = "Q2";
        salesTable.GetOrCreateCell(2, 1).RawInput = "15000";
        salesTable.GetOrCreateCell(2, 2).RawInput = "6000";
        salesTable.GetOrCreateCell(2, 3).RawInput = "=B3-C3";

        salesTable.GetOrCreateCell(3, 0).RawInput = "Q3";
        salesTable.GetOrCreateCell(3, 1).RawInput = "20000";
        salesTable.GetOrCreateCell(3, 2).RawInput = "7500";
        salesTable.GetOrCreateCell(3, 3).RawInput = "=B4-C4";

        salesTable.GetOrCreateCell(4, 0).RawInput = "Total";
        salesTable.GetOrCreateCell(4, 1).RawInput = "=SUM(B2:B4)";
        salesTable.GetOrCreateCell(4, 2).RawInput = "=SUM(C2:C4)";
        salesTable.GetOrCreateCell(4, 3).RawInput = "=SUM(D2:D4)";

        var summaryTable = new TableBlock("Summary", x: 550, y: 40, cols: 3, rows: 6);
        summaryTable.GetOrCreateCell(0, 0).RawInput = "Metric";
        summaryTable.GetOrCreateCell(0, 1).RawInput = "Value";

        summaryTable.GetOrCreateCell(1, 0).RawInput = "Total Revenue";
        summaryTable.GetOrCreateCell(1, 1).RawInput = "=Sales!B5";

        summaryTable.GetOrCreateCell(2, 0).RawInput = "Total Profit";
        summaryTable.GetOrCreateCell(2, 1).RawInput = "=Sales!D5";

        summaryTable.GetOrCreateCell(3, 0).RawInput = "Profit Margin";
        summaryTable.GetOrCreateCell(3, 1).RawInput = "=B3/B2";

        summaryTable.GetOrCreateCell(4, 0).RawInput = "C# Projected 20% Growth";
        summaryTable.GetOrCreateCell(4, 1).RawInput = "=CS(1.2 * 45000)";

        doc.AddTable(salesTable);
        doc.AddTable(summaryTable);

        return doc;
    }
}
