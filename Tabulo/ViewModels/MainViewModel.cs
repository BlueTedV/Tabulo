using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Tabulo.Core.Commands;
using Tabulo.Core.Engine;
using Tabulo.Core.Models;
using Tabulo.Core.Storage;
using Tabulo.Scripting;
using System.IO;
using System.Text;
using System.Windows;

namespace Tabulo.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private CanvasDocument _document;

    [ObservableProperty]
    private TableBlock? _activeTable;

    [ObservableProperty]
    private CellCoordinate _activeCellCoord = new(0, 0);

    [ObservableProperty]
    private CellRange _selectedRange = new(0, 0, 0, 0);

    [ObservableProperty]
    private string _selectionSummaryText = string.Empty;

    [ObservableProperty]
    private string _activeCellAddressText = string.Empty;

    [ObservableProperty]
    private string _formulaBarText = string.Empty;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private double _panX = 0.0;

    [ObservableProperty]
    private double _panY = 0.0;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private bool _snapToGrid = true;

    [ObservableProperty]
    private double _gridSnapSize = 20.0;

    [ObservableProperty]
    private double _viewportWidth = 1200.0;

    [ObservableProperty]
    private double _viewportHeight = 700.0;

    [ObservableProperty]
    private bool _isFormulaBarExpanded = false;

    [ObservableProperty]
    private TableBlock? _referencedTable;

    [ObservableProperty]
    private CellRange? _referencedRange;

    [ObservableProperty]
    private bool _isFormulaEditing;

    [ObservableProperty]
    private int _interactiveRefLength = 0;

    [ObservableProperty]
    private int _interactiveRefCaret = -1;

    public CalculationEngine CalcEngine { get; }
    public UndoRedoManager UndoRedo { get; }

    public event EventHandler? RequestCanvasRepaint;
    private string _activeCellInputBeforeEdit = string.Empty;

    public MainViewModel()
    {
        var scriptHost = new CSharpScriptHost();
        CalcEngine = new CalculationEngine(scriptHost);
        UndoRedo = new UndoRedoManager();

        CalcEngine.Recalculated += (_, _) =>
        {
            UpdateActiveCellDisplay();
            RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        };

        UndoRedo.HistoryChanged += (_, _) =>
        {
            UpdateActiveCellDisplay();
            RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        };

        _document = CanvasDocument.CreateDefault();
        _document.DocumentChanged += OnDocumentChangedHandler;
        CalcEngine.RecalculateAll(_document);

        _activeTable = _document.Tables.FirstOrDefault();
        UpdateActiveCellDisplay();
    }

    partial void OnDocumentChanged(CanvasDocument? oldValue, CanvasDocument newValue)
    {
        if (oldValue != null) oldValue.DocumentChanged -= OnDocumentChangedHandler;
        if (newValue != null) newValue.DocumentChanged += OnDocumentChangedHandler;
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentChangedHandler(object? sender, EventArgs e)
    {
        UpdateActiveCellDisplay();
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    public void SelectCell(TableBlock table, CellCoordinate coord)
    {
        ActiveTable = table;
        ActiveCellCoord = coord;
        SelectedRange = CellRange.FromSingle(coord);
        var cell = table.GetCell(coord);
        _activeCellInputBeforeEdit = cell?.RawInput ?? string.Empty;
        UpdateActiveCellDisplay();
        UpdateSelectionSummary();
    }

    public void SelectRange(TableBlock table, CellRange range, CellCoordinate? activeCell = null)
    {
        ActiveTable = table;
        SelectedRange = range;
        if (activeCell.HasValue)
        {
            ActiveCellCoord = activeCell.Value;
            var cell = table.GetCell(activeCell.Value);
            _activeCellInputBeforeEdit = cell?.RawInput ?? string.Empty;
        }
        UpdateActiveCellDisplay();
        UpdateSelectionSummary();
    }

    public void UpdateActiveCellDisplay()
    {
        if (ActiveTable == null)
        {
            ActiveCellAddressText = "--";
            FormulaBarText = string.Empty;
            return;
        }

        if (SelectedRange.TotalCells > 1)
        {
            ActiveCellAddressText = $"{ActiveTable.Name}!{SelectedRange}";
        }
        else
        {
            ActiveCellAddressText = $"{ActiveTable.Name}!{ActiveCellCoord.ToA1()}";
        }

        var cell = ActiveTable.GetCell(ActiveCellCoord);
        FormulaBarText = cell?.RawInput ?? string.Empty;
    }

    public void UpdateSelectionSummary()
    {
        if (ActiveTable == null || SelectedRange.TotalCells <= 1)
        {
            SelectionSummaryText = string.Empty;
            return;
        }

        var count = 0;
        var numCount = 0;
        var sum = 0.0;
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;

        foreach (var coord in SelectedRange.Coordinates)
        {
            var cell = ActiveTable.GetCell(coord);
            if (cell != null && !cell.Value.IsEmpty)
            {
                count++;
                if (cell.Value.TryGetNumber(out var num))
                {
                    numCount++;
                    sum += num;
                    if (num < min) min = num;
                    if (num > max) max = num;
                }
            }
        }

        if (numCount > 0)
        {
            var avg = sum / numCount;
            SelectionSummaryText = $"Sum: {sum:N2}   Avg: {avg:N2}   Count: {count}   Min: {min:N2}   Max: {max:N2}";
        }
        else if (count > 0)
        {
            SelectionSummaryText = $"Count: {count}";
        }
        else
        {
            SelectionSummaryText = string.Empty;
        }
    }

    public void BeginCellEdit(TableBlock table, CellCoordinate coord)
    {
        var cell = table.GetCell(coord);
        _activeCellInputBeforeEdit = cell?.RawInput ?? string.Empty;
    }

    public void UpdateCellLive(TableBlock table, CellCoordinate coord, string currentInput)
    {
        CalcEngine.SetCellInput(Document, table.Name, coord, currentInput);
        FormulaBarText = currentInput;
    }

    public void RevertCellEdit(TableBlock table, CellCoordinate coord)
    {
        ReferencedTable = null;
        ReferencedRange = null;
        IsFormulaEditing = false;
        ResetInteractiveReference();
        CalcEngine.SetCellInput(Document, table.Name, coord, _activeCellInputBeforeEdit);
        FormulaBarText = _activeCellInputBeforeEdit;
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ToggleFormulaBarExpanded() => IsFormulaBarExpanded = !IsFormulaBarExpanded;

    [RelayCommand]
    public void CommitFormulaBar()
    {
        if (ActiveTable == null) return;
        ReferencedTable = null;
        ReferencedRange = null;
        IsFormulaEditing = false;
        ResetInteractiveReference();

        var cell = ActiveTable.GetOrCreateCell(ActiveCellCoord);
        var oldInput = _activeCellInputBeforeEdit != string.Empty ? _activeCellInputBeforeEdit : cell.RawInput;
        var newInput = FormulaBarText;

        if (oldInput != newInput)
        {
            var cmd = new CellEditCommand(Document, CalcEngine, ActiveTable.Name, ActiveCellCoord, oldInput, newInput);
            UndoRedo.Execute(cmd);
            StatusMessage = $"Updated {ActiveCellAddressText}";
            _activeCellInputBeforeEdit = newInput;
        }
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    public void CommitCellEdit(TableBlock table, CellCoordinate coord, string originalInput, string newInput)
    {
        ReferencedTable = null;
        ReferencedRange = null;
        IsFormulaEditing = false;
        ResetInteractiveReference();
        if (originalInput != newInput)
        {
            var cmd = new CellEditCommand(Document, CalcEngine, table.Name, coord, originalInput, newInput);
            UndoRedo.Execute(cmd);
            UpdateActiveCellDisplay();
            StatusMessage = $"Updated {table.Name}!{coord.ToA1()}";
            _activeCellInputBeforeEdit = newInput;
        }
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    public void CommitCellEdit(TableBlock table, CellCoordinate coord, string newInput)
    {
        var oldInput = _activeCellInputBeforeEdit;
        CommitCellEdit(table, coord, oldInput, newInput);
    }

    public void UpdateInteractiveReference(TableBlock targetTable, CellRange targetRange)
    {
        if (ActiveTable == null) return;

        ReferencedTable = targetTable;
        ReferencedRange = targetRange;

        var refStr = FormulaReferenceHelper.FormatReference(ActiveTable.Name, targetTable.Name, targetRange);
        var currentText = FormulaBarText;
        var caret = InteractiveRefCaret >= 0 ? InteractiveRefCaret : currentText.Length;

        var (newText, newCaret, refLen) = FormulaReferenceHelper.InsertOrUpdateReference(
            currentText, caret, refStr, InteractiveRefLength);

        InteractiveRefLength = refLen;
        InteractiveRefCaret = newCaret;

        UpdateCellLive(ActiveTable, ActiveCellCoord, newText);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    public void ResetInteractiveReference()
    {
        InteractiveRefLength = 0;
        InteractiveRefCaret = -1;
    }

    [RelayCommand]
    public void Undo()
    {
        if (UndoRedo.CanUndo)
        {
            UndoRedo.Undo();
            UpdateActiveCellDisplay();
            StatusMessage = $"Undo: {UndoRedo.RedoDescription}";
            RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void Redo()
    {
        if (UndoRedo.CanRedo)
        {
            UndoRedo.Redo();
            UpdateActiveCellDisplay();
            StatusMessage = $"Redo: {UndoRedo.UndoDescription}";
            RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;

    [RelayCommand]
    public void ToggleSnapToGrid()
    {
        SnapToGrid = !SnapToGrid;
        StatusMessage = SnapToGrid ? $"Snap to grid enabled ({GridSnapSize}px)" : "Snap to grid disabled";
    }

    [RelayCommand]
    public void FocusTable(TableBlock? table)
    {
        var target = table ?? ActiveTable;
        if (target == null) return;
        ActiveTable = target;
        var tableW = target.GetTotalWidth();
        var tableH = target.GetTotalHeight();
        var tableCenterX = target.X + (tableW / 2.0);
        var tableCenterY = target.Y + (tableH / 2.0);

        PanX = (ViewportWidth / 2.0) - (tableCenterX * Zoom);
        PanY = (ViewportHeight / 2.0) - (tableCenterY * Zoom);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Focused on {target.Name}";
    }

    [RelayCommand]
    public void FitAllTables()
    {
        if (Document.Tables.Count == 0) return;
        var minX = Document.Tables.Min(t => t.X);
        var minY = Document.Tables.Min(t => t.Y);
        var maxX = Document.Tables.Max(t => t.X + t.GetTotalWidth());
        var maxY = Document.Tables.Max(t => t.Y + t.GetTotalHeight());

        var totalW = maxX - minX;
        var totalH = maxY - minY;
        const double margin = 60.0;

        var availableW = Math.Max(200, ViewportWidth - (margin * 2));
        var availableH = Math.Max(200, ViewportHeight - (margin * 2));

        var scaleX = availableW / Math.Max(1.0, totalW);
        var scaleY = availableH / Math.Max(1.0, totalH);
        var targetZoom = Math.Clamp(Math.Min(scaleX, scaleY), 0.2, 2.0);

        Zoom = targetZoom;
        var centerX = minX + (totalW / 2.0);
        var centerY = minY + (totalH / 2.0);
        PanX = (ViewportWidth / 2.0) - (centerX * targetZoom);
        PanY = (ViewportHeight / 2.0) - (centerY * targetZoom);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Fit all {Document.Tables.Count} table(s) to view ({Zoom:P0})";
    }

    [RelayCommand]
    public void DuplicateTable(TableBlock? table)
    {
        var target = table ?? ActiveTable;
        if (target == null) return;
        var cmd = new DuplicateTableCommand(Document, target);
        UndoRedo.Execute(cmd);
        ActiveTable = cmd.DuplicatedTable;
        ActiveCellCoord = new CellCoordinate(0, 0);
        UpdateActiveCellDisplay();
        StatusMessage = $"Duplicated table {target.Name} -> {cmd.DuplicatedTable.Name}";
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void RenameActiveTable(string? newName)
    {
        if (ActiveTable == null || string.IsNullOrWhiteSpace(newName)) return;
        var oldName = ActiveTable.Name;
        if (string.Equals(oldName, newName.Trim(), StringComparison.OrdinalIgnoreCase)) return;

        var cmd = new RenameTableCommand(ActiveTable, oldName, newName.Trim());
        UndoRedo.Execute(cmd);
        UpdateActiveCellDisplay();
        StatusMessage = $"Renamed table {oldName} to {ActiveTable.Name}";
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void SetActiveTableHeaderColor(string? colorHex)
    {
        if (ActiveTable == null) return;
        var oldColor = ActiveTable.HeaderColor;
        var newColor = string.IsNullOrWhiteSpace(colorHex) || colorHex == "Default" ? null : colorHex;
        if (oldColor == newColor) return;

        var cmd = new SetTableHeaderColorCommand(ActiveTable, oldColor, newColor);
        UndoRedo.Execute(cmd);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Set header color on {ActiveTable.Name}";
    }

    [RelayCommand]
    public void AddTable()
    {
        var nextIndex = Document.Tables.Count + 1;
        var newTable = new TableBlock($"Table_{nextIndex}", x: 100 + (nextIndex * 40), y: 100 + (nextIndex * 40), cols: 6, rows: 12);
        var cmd = new AddTableCommand(Document, newTable);
        UndoRedo.Execute(cmd);
        ActiveTable = newTable;
        ActiveCellCoord = new CellCoordinate(0, 0);
        UpdateActiveCellDisplay();
        StatusMessage = $"Created table {newTable.Name}";
    }

    [RelayCommand]
    public void RecalculateAll()
    {
        CalcEngine.RecalculateAll(Document);
        UpdateActiveCellDisplay();
        StatusMessage = "Recalculated all tables";
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ZoomIn() => Zoom = Math.Min(3.0, Zoom + 0.1);

    [RelayCommand]
    public void ZoomOut() => Zoom = Math.Max(0.2, Zoom - 0.1);

    [RelayCommand]
    public void ResetZoom()
    {
        Zoom = 1.0;
        PanX = 0;
        PanY = 0;
    }

    [RelayCommand]
    public void SaveDocument()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Tabulo Canvas (*.sheetx;*.json)|*.sheetx;*.json|All Files (*.*)|*.*",
            FileName = $"{Document.Name}.sheetx"
        };

        if (sfd.ShowDialog() == true)
        {
            var json = DocumentStorageManager.SerializeToJson(Document);
            File.WriteAllText(sfd.FileName, json);
            StatusMessage = $"Saved to {Path.GetFileName(sfd.FileName)}";
        }
    }

    [RelayCommand]
    public void OpenDocument()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Tabulo Canvas (*.sheetx;*.json)|*.sheetx;*.json|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            var json = File.ReadAllText(ofd.FileName);
            var doc = DocumentStorageManager.DeserializeFromJson(json);
            Document = doc;
            CalcEngine.RecalculateAll(Document);
            UndoRedo.Clear();
            ActiveTable = Document.Tables.FirstOrDefault();
            ActiveCellCoord = new CellCoordinate(0, 0);
            UpdateActiveCellDisplay();
            StatusMessage = $"Loaded {Path.GetFileName(ofd.FileName)}";
            RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void DeleteTable(TableBlock? table)
    {
        var target = table ?? ActiveTable;
        if (target == null) return;

        var cmd = new RemoveTableCommand(Document, target);
        UndoRedo.Execute(cmd);
        ActiveTable = Document.Tables.FirstOrDefault();
        ActiveCellCoord = new CellCoordinate(0, 0);
        UpdateActiveCellDisplay();
        StatusMessage = $"Deleted table {target.Name}";
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void InsertRowAbove()
    {
        if (ActiveTable == null) return;
        var cmd = new InsertRowCommand(ActiveTable, ActiveCellCoord.Row);
        UndoRedo.Execute(cmd);
        CalcEngine.RecalculateAll(Document);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Inserted row above in {ActiveTable.Name}";
    }

    [RelayCommand]
    public void InsertRowBelow()
    {
        if (ActiveTable == null) return;
        var cmd = new InsertRowCommand(ActiveTable, ActiveCellCoord.Row + 1);
        UndoRedo.Execute(cmd);
        CalcEngine.RecalculateAll(Document);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Inserted row below in {ActiveTable.Name}";
    }

    [RelayCommand]
    public void DeleteRow()
    {
        if (ActiveTable == null || ActiveTable.RowCount <= 1) return;
        var cmd = new DeleteRowCommand(ActiveTable, ActiveCellCoord.Row);
        UndoRedo.Execute(cmd);
        if (ActiveCellCoord.Row >= ActiveTable.RowCount)
        {
            ActiveCellCoord = new CellCoordinate(ActiveTable.RowCount - 1, ActiveCellCoord.Column);
        }
        CalcEngine.RecalculateAll(Document);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Deleted row from {ActiveTable.Name}";
    }

    [RelayCommand]
    public void InsertColumnLeft()
    {
        if (ActiveTable == null) return;
        var cmd = new InsertColumnCommand(ActiveTable, ActiveCellCoord.Column);
        UndoRedo.Execute(cmd);
        CalcEngine.RecalculateAll(Document);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Inserted column left in {ActiveTable.Name}";
    }

    [RelayCommand]
    public void InsertColumnRight()
    {
        if (ActiveTable == null) return;
        var cmd = new InsertColumnCommand(ActiveTable, ActiveCellCoord.Column + 1);
        UndoRedo.Execute(cmd);
        CalcEngine.RecalculateAll(Document);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Inserted column right in {ActiveTable.Name}";
    }

    [RelayCommand]
    public void DeleteColumn()
    {
        if (ActiveTable == null || ActiveTable.ColumnCount <= 1) return;
        var cmd = new DeleteColumnCommand(ActiveTable, ActiveCellCoord.Column);
        UndoRedo.Execute(cmd);
        if (ActiveCellCoord.Column >= ActiveTable.ColumnCount)
        {
            ActiveCellCoord = new CellCoordinate(ActiveCellCoord.Row, ActiveTable.ColumnCount - 1);
        }
        CalcEngine.RecalculateAll(Document);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Deleted column from {ActiveTable.Name}";
    }

    [RelayCommand]
    public void CopySelection()
    {
        if (ActiveTable == null) return;
        var range = SelectedRange;
        var sb = new StringBuilder();
        for (var r = range.MinRow; r <= range.MaxRow; r++)
        {
            var rowVals = new List<string>();
            for (var c = range.MinCol; c <= range.MaxCol; c++)
            {
                var cell = ActiveTable.GetCell(r, c);
                rowVals.Add(cell?.RawInput ?? string.Empty);
            }
            sb.AppendLine(string.Join("\t", rowVals));
        }
        var text = sb.ToString().TrimEnd('\r', '\n');
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            StatusMessage = $"Copied {range} to clipboard";
        }
    }

    [RelayCommand]
    public void CutSelection()
    {
        if (ActiveTable == null) return;
        CopySelection();
        ClearSelectedCells();
        StatusMessage = $"Cut {SelectedRange}";
    }

    [RelayCommand]
    public void PasteClipboard()
    {
        if (ActiveTable == null) return;
        if (!Clipboard.ContainsText()) return;

        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        if (lines.Length == 0) return;

        if (lines.Length > 1 && string.IsNullOrEmpty(lines[^1]))
        {
            lines = lines[..^1];
        }

        var startRow = ActiveCellCoord.Row;
        var startCol = ActiveCellCoord.Column;
        var edits = new List<CellInputEdit>();

        var maxRow = startRow + lines.Length - 1;
        var maxCol = startCol;

        for (var r = 0; r < lines.Length; r++)
        {
            var cells = lines[r].Split('\t');
            if (startCol + cells.Length - 1 > maxCol)
            {
                maxCol = startCol + cells.Length - 1;
            }

            for (var c = 0; c < cells.Length; c++)
            {
                var targetCoord = new CellCoordinate(startRow + r, startCol + c);
                var oldInput = ActiveTable.GetCell(targetCoord)?.RawInput ?? string.Empty;
                var newInput = cells[c];
                edits.Add(new CellInputEdit(targetCoord, oldInput, newInput));
            }
        }

        if (edits.Count > 0)
        {
            var cmd = new BatchCellEditCommand(Document, CalcEngine, ActiveTable.Name, edits, $"Paste into {ActiveTable.Name}");
            UndoRedo.Execute(cmd);
            SelectedRange = new CellRange(startRow, startCol, maxRow, maxCol);
            UpdateActiveCellDisplay();
            UpdateSelectionSummary();
            StatusMessage = $"Pasted {edits.Count} cells into {ActiveTable.Name}";
            RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void ClearSelectedCell() => ClearSelectedCells();

    [RelayCommand]
    public void ClearSelectedCells()
    {
        if (ActiveTable == null) return;
        var range = SelectedRange;
        var edits = new List<CellInputEdit>();

        foreach (var coord in range.Coordinates)
        {
            var cell = ActiveTable.GetCell(coord);
            if (cell != null && !string.IsNullOrEmpty(cell.RawInput))
            {
                edits.Add(new CellInputEdit(coord, cell.RawInput, string.Empty));
            }
        }

        if (edits.Count > 0)
        {
            var cmd = new BatchCellEditCommand(Document, CalcEngine, ActiveTable.Name, edits, $"Clear {range} in {ActiveTable.Name}");
            UndoRedo.Execute(cmd);
            UpdateActiveCellDisplay();
            UpdateSelectionSummary();
            StatusMessage = $"Cleared {edits.Count} cells in {ActiveTable.Name}";
            RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void ToggleBold()
    {
        if (ActiveTable == null) return;
        var range = SelectedRange;
        var anyNotBold = false;
        foreach (var coord in range.Coordinates)
        {
            var cell = ActiveTable.GetCell(coord);
            if (cell == null || !cell.Style.IsBold)
            {
                anyNotBold = true;
                break;
            }
        }
        var targetBold = anyNotBold;

        var edits = new List<CellStyleEdit>();
        foreach (var coord in range.Coordinates)
        {
            var cell = ActiveTable.GetOrCreateCell(coord);
            var oldStyle = cell.Style;
            var newStyle = oldStyle with { IsBold = targetBold };
            edits.Add(new CellStyleEdit(coord, oldStyle, newStyle));
        }

        var cmd = new BatchFormatCommand(ActiveTable, edits, $"Toggle bold in {ActiveTable.Name}");
        UndoRedo.Execute(cmd);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ToggleItalic()
    {
        if (ActiveTable == null) return;
        var range = SelectedRange;
        var anyNotItalic = false;
        foreach (var coord in range.Coordinates)
        {
            var cell = ActiveTable.GetCell(coord);
            if (cell == null || !cell.Style.IsItalic)
            {
                anyNotItalic = true;
                break;
            }
        }
        var targetItalic = anyNotItalic;

        var edits = new List<CellStyleEdit>();
        foreach (var coord in range.Coordinates)
        {
            var cell = ActiveTable.GetOrCreateCell(coord);
            var oldStyle = cell.Style;
            var newStyle = oldStyle with { IsItalic = targetItalic };
            edits.Add(new CellStyleEdit(coord, oldStyle, newStyle));
        }

        var cmd = new BatchFormatCommand(ActiveTable, edits, $"Toggle italic in {ActiveTable.Name}");
        UndoRedo.Execute(cmd);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void SetAlignment(string? align)
    {
        if (ActiveTable == null) return;
        var alignment = align?.ToLowerInvariant() switch
        {
            "center" => CellHorizontalAlignment.Center,
            "right" => CellHorizontalAlignment.Right,
            _ => CellHorizontalAlignment.Left
        };

        var edits = new List<CellStyleEdit>();
        foreach (var coord in SelectedRange.Coordinates)
        {
            var cell = ActiveTable.GetOrCreateCell(coord);
            var oldStyle = cell.Style;
            var newStyle = oldStyle with { Alignment = alignment };
            edits.Add(new CellStyleEdit(coord, oldStyle, newStyle));
        }

        var cmd = new BatchFormatCommand(ActiveTable, edits, $"Set alignment to {alignment} in {ActiveTable.Name}");
        UndoRedo.Execute(cmd);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void SetNumberFormat(string? format)
    {
        if (ActiveTable == null) return;
        var edits = new List<CellStyleEdit>();
        foreach (var coord in SelectedRange.Coordinates)
        {
            var cell = ActiveTable.GetOrCreateCell(coord);
            var oldStyle = cell.Style;
            var newStyle = oldStyle with { NumberFormat = format };
            edits.Add(new CellStyleEdit(coord, oldStyle, newStyle));
        }

        var cmd = new BatchFormatCommand(ActiveTable, edits, $"Set number format to {format ?? "General"} in {ActiveTable.Name}");
        UndoRedo.Execute(cmd);
        RequestCanvasRepaint?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Applied {format ?? "General"} format to {SelectedRange}";
    }
}
