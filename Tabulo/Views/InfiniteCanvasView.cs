using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tabulo.Core.Commands;
using Tabulo.Core.Engine;
using Tabulo.Core.Models;
using Tabulo.ViewModels;
using SharpVectors.Converters;

namespace Tabulo.Views;

/// <summary>
/// High-performance custom canvas control rendering multiple independent tables with smooth zoom, pan, in-place editing, corner resizing with ghost outlines, and context menus.
/// </summary>
public class InfiniteCanvasView : FrameworkElement
{
    private MainViewModel? _vm;
    private readonly TextBox _floatingEditor;
    private Rect _editorScreenRect = Rect.Empty;

    // Inline Table Title Editor
    private readonly TextBox _titleEditor;
    private TableBlock? _editingTitleTable;
    private Rect _titleEditorScreenRect = Rect.Empty;

    // Interaction states
    private bool _isPanning;
    private Point _lastPanPoint;

    private TableBlock? _draggingTable;
    private Point _dragTableStartPos;
    private Point _dragMouseStartPos;

    private TableBlock? _resizingColTable;
    private int _resizingColIndex = -1;
    private double _resizingColStartWidth;
    private double _resizeMouseStartX;

    // Row resizing state
    private TableBlock? _resizingRowTable;
    private int _resizingRowIndex = -1;
    private double _resizingRowStartHeight;
    private double _resizeMouseStartY;

    // Range selection drag state
    private bool _isSelectingRange;
    private CellCoordinate _selectionAnchorCoord;

    // Interactive Formula Referencing state
    private bool _isReferencingFormula;
    private TableBlock? _formulaRefTable;
    private CellCoordinate _formulaRefStartCoord;

    // Table boundary resizing with ghost outline
    private bool _isResizingTable;
    private TableBlock? _resizingTable;
    private Rect? _ghostTableRect;
    private Point _resizeTableStartMouse;
    private double _resizeTableStartTotalWidth;
    private double _resizeTableStartTotalHeight;
    private int _resizeTableStartCols;
    private int _resizeTableStartRows;

    // Visual assets
    private static readonly Typeface DefaultTypeface = new("Segoe UI");
    private static readonly Brush CanvasBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
    private static readonly Pen GridDotPen = new(new SolidColorBrush(Color.FromArgb(45, 148, 163, 184)), 1.5);
    private static readonly Brush TableCardBackground = Brushes.White;
    private static readonly Pen TableCardBorder = new(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.0);
    private static readonly Brush TableHeaderBackground = new SolidColorBrush(Color.FromRgb(241, 245, 249));
    private static readonly Brush TableTitleBackground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
    private static readonly Pen CellBorderPen = new(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 0.8);
    private static readonly Pen ActiveCellPen = new(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 2.0);
    private static readonly Brush ActiveCellFill = new SolidColorBrush(Color.FromArgb(20, 37, 99, 235));

    // Selection Box and Fill Handle Assets
    private static readonly Brush SelectionCellFill = new SolidColorBrush(Color.FromArgb(28, 37, 99, 235));
    private static readonly Pen SelectionBorderPen = new(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 2.0);
    private static readonly Pen ActiveCellInnerPen = new(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1.5);
    private static readonly Brush FillHandleFill = new SolidColorBrush(Color.FromRgb(37, 99, 235));
    private static readonly Pen FillHandleBorderPen = new(Brushes.White, 1.0);

    // Formula Reference Highlight Assets
    private static readonly Brush ReferenceCellFill = new SolidColorBrush(Color.FromArgb(28, 22, 163, 74));
    private static readonly Pen ReferenceBorderPen = new(new SolidColorBrush(Color.FromRgb(22, 163, 74)), 2.0)
    {
        DashStyle = new DashStyle(new double[] { 3, 2 }, 0)
    };

    // Ghost Outline Assets
    private static readonly Pen GhostOutlinePen = new(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 2.0)
    {
        DashStyle = DashStyles.Dash
    };
    private static readonly Brush GhostOutlineFill = new SolidColorBrush(Color.FromArgb(25, 37, 99, 235));
    private static readonly Brush ResizeGripBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));

    public InfiniteCanvasView()
    {
        Focusable = true;
        ClipToBounds = true;

        _floatingEditor = new TextBox
        {
            Visibility = Visibility.Collapsed,
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.White
        };

        _floatingEditor.KeyDown += FloatingEditor_KeyDown;
        _floatingEditor.LostFocus += FloatingEditor_LostFocus;
        _floatingEditor.TextChanged += FloatingEditor_TextChanged;

        AddVisualChild(_floatingEditor);
        AddLogicalChild(_floatingEditor);

        _titleEditor = new TextBox
        {
            Visibility = Visibility.Collapsed,
            BorderThickness = new Thickness(1.5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.White
        };

        _titleEditor.KeyDown += TitleEditor_KeyDown;
        _titleEditor.LostFocus += TitleEditor_LostFocus;

        AddVisualChild(_titleEditor);
        AddLogicalChild(_titleEditor);

        SizeChanged += (_, _) =>
        {
            if (_vm != null)
            {
                _vm.ViewportWidth = ActualWidth;
                _vm.ViewportHeight = ActualHeight;
            }
        };

        Loaded += (_, _) =>
        {
            HookViewModel(DataContext as MainViewModel);
            if (_vm != null)
            {
                _vm.ViewportWidth = ActualWidth;
                _vm.ViewportHeight = ActualHeight;
            }
        };

        DataContextChanged += (_, e) =>
        {
            HookViewModel(e.NewValue as MainViewModel);
            if (_vm != null)
            {
                _vm.ViewportWidth = ActualWidth;
                _vm.ViewportHeight = ActualHeight;
            }
        };
    }

    private void HookViewModel(MainViewModel? vm)
    {
        if (_vm != null)
        {
            _vm.RequestCanvasRepaint -= OnRequestCanvasRepaint;
        }

        _vm = vm;

        if (_vm != null)
        {
            _vm.RequestCanvasRepaint += OnRequestCanvasRepaint;
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(MainViewModel.Zoom) or nameof(MainViewModel.PanX) or nameof(MainViewModel.PanY) or nameof(MainViewModel.Document) or nameof(MainViewModel.ActiveCellCoord) or nameof(MainViewModel.ActiveTable))
                {
                    UpdateFloatingEditorPosition();
                    UpdateTitleEditorPosition();
                    InvalidateVisual();
                }
            };
        }

        InvalidateVisual();
    }

    private void OnRequestCanvasRepaint(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateFloatingEditorPosition();
            UpdateTitleEditorPosition();
            InvalidateVisual();
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private bool _isUpdatingTextProgrammatically;

    private void FloatingEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingTextProgrammatically || _floatingEditor.Visibility != Visibility.Visible || _vm?.ActiveTable == null) return;

        var text = _floatingEditor.Text;
        _vm.IsFormulaEditing = text.StartsWith('=');
        _vm.UpdateCellLive(_vm.ActiveTable, _vm.ActiveCellCoord, text);

        var caret = _floatingEditor.CaretIndex;
        if (FormulaReferenceHelper.IsExpectingReference(text, caret))
        {
            _vm.ResetInteractiveReference();
            _vm.InteractiveRefCaret = caret;
        }

        InvalidateVisual();
    }

    protected override int VisualChildrenCount => 2;
    protected override Visual GetVisualChild(int index) => index == 0 ? _floatingEditor : _titleEditor;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_floatingEditor.Visibility == Visibility.Visible && !_editorScreenRect.IsEmpty)
        {
            _floatingEditor.Measure(_editorScreenRect.Size);
        }
        else
        {
            _floatingEditor.Measure(new Size(0, 0));
        }

        if (_titleEditor.Visibility == Visibility.Visible && !_titleEditorScreenRect.IsEmpty)
        {
            _titleEditor.Measure(_titleEditorScreenRect.Size);
        }
        else
        {
            _titleEditor.Measure(new Size(0, 0));
        }

        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_floatingEditor.Visibility == Visibility.Visible && !_editorScreenRect.IsEmpty)
        {
            _floatingEditor.Arrange(_editorScreenRect);
        }
        else
        {
            _floatingEditor.Arrange(new Rect(0, 0, 0, 0));
        }

        if (_titleEditor.Visibility == Visibility.Visible && !_titleEditorScreenRect.IsEmpty)
        {
            _titleEditor.Arrange(_titleEditorScreenRect);
        }
        else
        {
            _titleEditor.Arrange(new Rect(0, 0, 0, 0));
        }

        return finalSize;
    }

    #region Coordinate Transformations

    public Point ScreenToCanvas(Point screenPoint)
    {
        if (_vm == null) return screenPoint;
        var zoom = _vm.Zoom;
        return new Point(
            (screenPoint.X - _vm.PanX) / zoom,
            (screenPoint.Y - _vm.PanY) / zoom);
    }

    public Point CanvasToScreen(Point canvasPoint)
    {
        if (_vm == null) return canvasPoint;
        var zoom = _vm.Zoom;
        return new Point(
            (canvasPoint.X * zoom) + _vm.PanX,
            (canvasPoint.Y * zoom) + _vm.PanY);
    }

    #endregion

    #region Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_vm == null || _vm.Document == null) return;

        var zoom = _vm.Zoom;
        var panX = _vm.PanX;
        var panY = _vm.PanY;

        // 1. Canvas background & dotted grid
        dc.DrawRectangle(CanvasBackground, null, new Rect(0, 0, ActualWidth, ActualHeight));
        DrawBackgroundDots(dc, zoom, panX, panY);

        // 2. Viewport Transform
        var transform = new MatrixTransform(zoom, 0, 0, zoom, panX, panY);
        dc.PushTransform(transform);

        // 3. Render all tables on canvas
        foreach (var table in _vm.Document.Tables)
        {
            RenderTable(dc, table, table == _vm.ActiveTable);
        }

        // 4. Render Ghost Outline when resizing table
        if (_isResizingTable && _ghostTableRect.HasValue)
        {
            dc.DrawRoundedRectangle(GhostOutlineFill, GhostOutlinePen, _ghostTableRect.Value, 4, 4);
        }

        dc.Pop();
    }

    private void DrawBackgroundDots(DrawingContext dc, double zoom, double panX, double panY)
    {
        var step = 40.0 * zoom;
        if (step < 14.0) return;

        var startX = panX % step;
        var startY = panY % step;

        for (var x = startX; x < ActualWidth; x += step)
        {
            for (var y = startY; y < ActualHeight; y += step)
            {
                dc.DrawRectangle(GridDotPen.Brush, null, new Rect(x - 1, y - 1, 2, 2));
            }
        }
    }

    private void RenderTable(DrawingContext dc, TableBlock table, bool isActiveTable)
    {
        const double rowHeaderWidth = 40.0;
        const double titleBarHeight = 28.0;
        var totalWidth = table.GetTotalWidth();
        var totalHeight = table.GetTotalHeight();

        var tableRect = new Rect(table.X, table.Y, totalWidth, totalHeight);

        // Table Card Background & Border
        dc.DrawRoundedRectangle(TableCardBackground, TableCardBorder, tableRect, 4, 4);

        // Title Bar
        var titleRect = new Rect(table.X, table.Y, totalWidth, titleBarHeight);
        Brush titleBarBrush = TableTitleBackground;
        Brush titleTextBrush = Brushes.Black;

        if (!string.IsNullOrWhiteSpace(table.HeaderColor))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(table.HeaderColor);
                titleBarBrush = new SolidColorBrush(color);
                titleTextBrush = Brushes.White;
            }
            catch
            {
                titleBarBrush = TableTitleBackground;
                titleTextBrush = Brushes.Black;
            }
        }
        else if (isActiveTable)
        {
            titleBarBrush = new SolidColorBrush(Color.FromRgb(224, 231, 255)); // Soft indigo/blue for active table
            titleTextBrush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
        }

        dc.DrawRoundedRectangle(titleBarBrush, null, titleRect, 4, 4);
        if (_editingTitleTable != table)
        {
            var titleText = new FormattedText(
                $"\uD83D\uDCCA {table.Name}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                12.5,
                titleTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(titleText, new Point(table.X + 8, table.Y + 6));
        }

        // Column Headers (A, B, C...)
        var colHeaderY = table.Y + titleBarHeight;
        var cornerRect = new Rect(table.X, colHeaderY, rowHeaderWidth, table.HeaderHeight);
        dc.DrawRectangle(TableHeaderBackground, CellBorderPen, cornerRect);

        var currentX = table.X + rowHeaderWidth;
        for (var c = 0; c < table.ColumnCount; c++)
        {
            var colW = table.GetColumnWidth(c);
            var colRect = new Rect(currentX, colHeaderY, colW, table.HeaderHeight);
            dc.DrawRectangle(TableHeaderBackground, CellBorderPen, colRect);

            var headerLetter = CellCoordinate.ColumnToLetters(c);
            var headerText = new FormattedText(
                headerLetter,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                11.0,
                Brushes.DimGray,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var textX = currentX + (colW - headerText.Width) / 2;
            var textY = colHeaderY + (table.HeaderHeight - headerText.Height) / 2;
            dc.DrawText(headerText, new Point(textX, textY));

            currentX += colW;
        }

        // Rows and Cells
        var currentY = table.Y + titleBarHeight + table.HeaderHeight;
        for (var r = 0; r < table.RowCount; r++)
        {
            var rowH = table.GetRowHeight(r);

            // Row Header (1, 2, 3...)
            var rowHeaderRect = new Rect(table.X, currentY, rowHeaderWidth, rowH);
            dc.DrawRectangle(TableHeaderBackground, CellBorderPen, rowHeaderRect);
            var rowNumText = new FormattedText(
                (r + 1).ToString(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                10.5,
                Brushes.DimGray,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(rowNumText, new Point(table.X + (rowHeaderWidth - rowNumText.Width) / 2, currentY + (rowH - rowNumText.Height) / 2));

            // Cells in this row
            var cellX = table.X + rowHeaderWidth;
            for (var c = 0; c < table.ColumnCount; c++)
            {
                var colW = table.GetColumnWidth(c);
                var cellRect = new Rect(cellX, currentY, colW, rowH);
                var cellCoord = new CellCoordinate(r, c);

                var cell = table.GetCell(cellCoord);

                // Background
                Brush bgBrush = Brushes.Transparent;
                if (!string.IsNullOrEmpty(cell?.Style.BackgroundColor))
                {
                    try { bgBrush = new BrushConverter().ConvertFromString(cell.Style.BackgroundColor) as Brush ?? Brushes.Transparent; }
                    catch { }
                }
                dc.DrawRectangle(bgBrush, CellBorderPen, cellRect);

                // Cell Value Text
                if (cell != null && !cell.Value.IsEmpty)
                {
                    var displayStr = cell.Value.Format(cell.Style.NumberFormat);
                    var isErr = cell.Value.IsError;

                    var brush = isErr ? Brushes.Crimson : Brushes.Black;
                    var fontWeight = (cell.Style.IsBold || isErr) ? FontWeights.SemiBold : FontWeights.Normal;
                    var fontStyle = cell.Style.IsItalic ? FontStyles.Italic : FontStyles.Normal;

                    var cellFormatted = new FormattedText(
                        displayStr,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(DefaultTypeface.FontFamily, fontStyle, fontWeight, FontStretches.Normal),
                        11.5,
                        brush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip)
                    {
                        MaxTextWidth = Math.Max(1, colW - 8),
                        MaxTextHeight = Math.Max(1, rowH - 4)
                    };

                    double alignX = cellX + 4;
                    if (cell.Style.Alignment == CellHorizontalAlignment.Right || (cell.Style.Alignment == CellHorizontalAlignment.Left && cell.Value.IsNumber))
                    {
                        alignX = cellX + colW - cellFormatted.Width - 6;
                    }
                    else if (cell.Style.Alignment == CellHorizontalAlignment.Center)
                    {
                        alignX = cellX + (colW - cellFormatted.Width) / 2;
                    }

                    dc.DrawText(cellFormatted, new Point(alignX, currentY + (rowH - cellFormatted.Height) / 2));
                }

                // Selection cell wash
                if (isActiveTable && _vm?.SelectedRange.Contains(cellCoord) == true)
                {
                    dc.DrawRectangle(SelectionCellFill, null, cellRect);
                }

                // Interactive Formula Reference cell wash
                if (_vm?.ReferencedTable == table && _vm?.ReferencedRange?.Contains(cellCoord) == true)
                {
                    dc.DrawRectangle(ReferenceCellFill, null, cellRect);
                }

                cellX += colW;
            }

            currentY += rowH;
        }

        // Draw Formula Reference Box (dashed green outline)
        if (_vm?.ReferencedTable == table && _vm?.ReferencedRange != null)
        {
            var refRange = _vm.ReferencedRange.Value;
            var minCol = Math.Clamp(refRange.MinCol, 0, table.ColumnCount - 1);
            var maxCol = Math.Clamp(refRange.MaxCol, 0, table.ColumnCount - 1);
            var minRow = Math.Clamp(refRange.MinRow, 0, table.RowCount - 1);
            var maxRow = Math.Clamp(refRange.MaxRow, 0, table.RowCount - 1);

            var rX = table.X + table.GetColumnX(minCol);
            var rY = table.Y + table.GetRowY(minRow);
            var rW = 0.0;
            for (var c = minCol; c <= maxCol; c++) rW += table.GetColumnWidth(c);
            var rH = 0.0;
            for (var r = minRow; r <= maxRow; r++) rH += table.GetRowHeight(r);

            var refBox = new Rect(rX, rY, rW, rH);
            dc.DrawRectangle(null, ReferenceBorderPen, refBox);
        }

        // Draw Selected Range Outline & Fill Handle
        if (isActiveTable && _vm != null)
        {
            var range = _vm.SelectedRange;
            var minCol = Math.Clamp(range.MinCol, 0, table.ColumnCount - 1);
            var maxCol = Math.Clamp(range.MaxCol, 0, table.ColumnCount - 1);
            var minRow = Math.Clamp(range.MinRow, 0, table.RowCount - 1);
            var maxRow = Math.Clamp(range.MaxRow, 0, table.RowCount - 1);

            var rX = table.X + table.GetColumnX(minCol);
            var rY = table.Y + table.GetRowY(minRow);
            var rW = 0.0;
            for (var c = minCol; c <= maxCol; c++) rW += table.GetColumnWidth(c);
            var rH = 0.0;
            for (var r = minRow; r <= maxRow; r++) rH += table.GetRowHeight(r);

            var selectionBox = new Rect(rX, rY, rW, rH);
            dc.DrawRectangle(null, SelectionBorderPen, selectionBox);

            // Active cell indicator within multi-cell range
            if (range.TotalCells > 1)
            {
                var act = _vm.ActiveCellCoord;
                if (act.Column >= 0 && act.Column < table.ColumnCount && act.Row >= 0 && act.Row < table.RowCount)
                {
                    var aX = table.X + table.GetColumnX(act.Column);
                    var aY = table.Y + table.GetRowY(act.Row);
                    var aW = table.GetColumnWidth(act.Column);
                    var aH = table.GetRowHeight(act.Row);
                    dc.DrawRectangle(null, ActiveCellInnerPen, new Rect(aX, aY, aW, aH));
                }
            }

            // Small Fill Handle square in bottom-right corner of selection
            var handleRect = new Rect(selectionBox.Right - 3.5, selectionBox.Bottom - 3.5, 6.0, 6.0);
            dc.DrawRectangle(Brushes.White, FillHandleBorderPen, handleRect);
            dc.DrawRectangle(FillHandleFill, null, new Rect(selectionBox.Right - 2.5, selectionBox.Bottom - 2.5, 4.0, 4.0));
        }

        // Draw Corner Resize Grip (bottom-right)
        var gripRect = new Rect(table.X + totalWidth - 12, table.Y + totalHeight - 12, 10, 10);
        dc.DrawRectangle(null, null, gripRect);
        // Draw 3 diagonal grip lines
        var gripPen = new Pen(ResizeGripBrush, 1.2);
        dc.DrawLine(gripPen, new Point(table.X + totalWidth - 3, table.Y + totalHeight - 9), new Point(table.X + totalWidth - 9, table.Y + totalHeight - 3));
        dc.DrawLine(gripPen, new Point(table.X + totalWidth - 3, table.Y + totalHeight - 6), new Point(table.X + totalWidth - 6, table.Y + totalHeight - 3));
        dc.DrawLine(gripPen, new Point(table.X + totalWidth - 3, table.Y + totalHeight - 3), new Point(table.X + totalWidth - 3, table.Y + totalHeight - 3));
    }

    #endregion

    #region Direct Typing & Keyboard Navigation

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        base.OnPreviewTextInput(e);

        if (_vm?.ActiveTable != null && _floatingEditor.Visibility != Visibility.Visible)
        {
            if (!string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
            {
                StartFloatingEditor(e.Text);
                e.Handled = true;
            }
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (_vm?.ActiveTable == null) return;

        // If editor is NOT open
        if (_floatingEditor.Visibility != Visibility.Visible)
        {
            // Shortcuts
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    _vm.UndoCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Y)
                {
                    _vm.RedoCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                if (e.Key is Key.D0 or Key.NumPad0)
                {
                    _vm.FitAllTablesCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.C)
                {
                    _vm.CopySelectionCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.X)
                {
                    _vm.CutSelectionCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.V)
                {
                    _vm.PasteClipboardCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.A)
                {
                    var table = _vm.ActiveTable;
                    _vm.SelectRange(table, new CellRange(0, 0, table.RowCount - 1, table.ColumnCount - 1), new CellCoordinate(0, 0));
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.B)
                {
                    _vm.ToggleBoldCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.I)
                {
                    _vm.ToggleItalicCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (e.Key == Key.Z)
                {
                    _vm.RedoCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                if (e.Key is Key.D0 or Key.NumPad0)
                {
                    _vm.FitAllTablesCommand.Execute(null);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                _vm.ClearSelectedCellsCommand.Execute(null);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F2)
            {
                StartFloatingEditor(null);
                e.Handled = true;
                return;
            }

            // Shift+Arrows: Expand / Contract Range Selection
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && e.Key is Key.Down or Key.Up or Key.Left or Key.Right)
            {
                var endR = _vm.SelectedRange.End.Row;
                var endC = _vm.SelectedRange.End.Column;

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    if (e.Key == Key.Down) endR = _vm.ActiveTable.RowCount - 1;
                    if (e.Key == Key.Up) endR = 0;
                    if (e.Key == Key.Right) endC = _vm.ActiveTable.ColumnCount - 1;
                    if (e.Key == Key.Left) endC = 0;
                }
                else
                {
                    if (e.Key == Key.Down) endR = Math.Min(_vm.ActiveTable.RowCount - 1, endR + 1);
                    if (e.Key == Key.Up) endR = Math.Max(0, endR - 1);
                    if (e.Key == Key.Right) endC = Math.Min(_vm.ActiveTable.ColumnCount - 1, endC + 1);
                    if (e.Key == Key.Left) endC = Math.Max(0, endC - 1);
                }

                var newRange = new CellRange(_vm.ActiveCellCoord, new CellCoordinate(endR, endC));
                _vm.SelectRange(_vm.ActiveTable, newRange, _vm.ActiveCellCoord);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Ctrl+Arrows: Jump to table boundary
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.Down or Key.Up or Key.Left or Key.Right)
            {
                var r = _vm.ActiveCellCoord.Row;
                var c = _vm.ActiveCellCoord.Column;
                if (e.Key == Key.Down) r = _vm.ActiveTable.RowCount - 1;
                if (e.Key == Key.Up) r = 0;
                if (e.Key == Key.Right) c = _vm.ActiveTable.ColumnCount - 1;
                if (e.Key == Key.Left) c = 0;
                _vm.SelectCell(_vm.ActiveTable, new CellCoordinate(r, c));
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Standard Navigation: Arrows, Enter, Tab (with Shift support for Enter/Tab)
            if (e.Key is Key.Down or Key.Up or Key.Left or Key.Right or Key.Tab or Key.Enter)
            {
                var cur = _vm.ActiveCellCoord;
                var r = cur.Row;
                var c = cur.Column;

                var isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

                if (e.Key == Key.Enter)
                {
                    if (isShift && r > 0) r--;
                    else r++;
                }
                else if (e.Key == Key.Tab)
                {
                    if (isShift && c > 0) c--;
                    else c++;
                }
                else if (e.Key == Key.Down) r++;
                else if (e.Key == Key.Up && r > 0) r--;
                else if (e.Key == Key.Right) c++;
                else if (e.Key == Key.Left && c > 0) c--;

                _vm.SelectCell(_vm.ActiveTable, new CellCoordinate(r, c));
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }
    }

    #endregion

    #region Mouse Interaction & Corner Resizing

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (_vm == null || _vm.Document == null) return;

        var screenPoint = e.GetPosition(this);
        var canvasPoint = ScreenToCanvas(screenPoint);

        // Middle button or Space+Left: Pan
        if (e.MiddleButton == MouseButtonState.Pressed || (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space)))
        {
            _isPanning = true;
            _lastPanPoint = screenPoint;
            Cursor = Cursors.SizeAll;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var isFormulaEditing = _vm != null && _vm.IsFormulaEditing && _vm.Document != null;
            if (isFormulaEditing)
            {
                for (var i = _vm!.Document!.Tables.Count - 1; i >= 0; i--)
                {
                    var targetTable = _vm.Document.Tables[i];
                    if (TryHitTestCell(targetTable, canvasPoint, out var refCoord))
                    {
                        _isReferencingFormula = true;
                        _formulaRefTable = targetTable;
                        _formulaRefStartCoord = refCoord;

                        if (_vm.InteractiveRefLength == 0)
                        {
                            var activeEditorCaret = _floatingEditor.Visibility == Visibility.Visible
                                ? _floatingEditor.CaretIndex
                                : -1;
                            if (activeEditorCaret >= 0)
                            {
                                _vm.InteractiveRefCaret = activeEditorCaret;
                            }
                        }

                        _vm.UpdateInteractiveReference(targetTable, CellRange.FromSingle(refCoord));

                        if (_floatingEditor.Visibility == Visibility.Visible)
                        {
                            _isUpdatingTextProgrammatically = true;
                            try
                            {
                                _floatingEditor.Text = _vm.FormulaBarText;
                                _floatingEditor.CaretIndex = _vm.InteractiveRefCaret;
                            }
                            finally
                            {
                                _isUpdatingTextProgrammatically = false;
                            }
                        }

                        CaptureMouse();
                        InvalidateVisual();
                        e.Handled = true;
                        return;
                    }
                }

                CommitFloatingEditor();
                _vm.IsFormulaEditing = false;
                _vm.ReferencedTable = null;
                _vm.ReferencedRange = null;
                _vm.ResetInteractiveReference();
            }
            else
            {
                CommitFloatingEditor();
            }

            if (e.ClickCount < 2)
            {
                CommitTitleEditor();
            }

            // Hit test tables in reverse order (topmost first)
            for (var i = _vm.Document!.Tables.Count - 1; i >= 0; i--)
            {
                var table = _vm.Document.Tables[i];
                var totalW = table.GetTotalWidth();
                var totalH = table.GetTotalHeight();
                var tableRect = new Rect(table.X, table.Y, totalW, totalH);

                // 1. Check Corner Resize Handle (bottom-right 16x16 corner)
                var cornerGripRect = new Rect(table.X + totalW - 16, table.Y + totalH - 16, 16, 16);
                if (cornerGripRect.Contains(canvasPoint))
                {
                    _isResizingTable = true;
                    _resizingTable = table;
                    _resizeTableStartMouse = canvasPoint;
                    _resizeTableStartTotalWidth = totalW;
                    _resizeTableStartTotalHeight = totalH;
                    _resizeTableStartCols = table.ColumnCount;
                    _resizeTableStartRows = table.RowCount;
                    _ghostTableRect = new Rect(table.X, table.Y, totalW, totalH);
                    _vm.ActiveTable = table;
                    CaptureMouse();
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                if (tableRect.Contains(canvasPoint))
                {
                    _vm.ActiveTable = table;
                    const double titleBarHeight = 28.0;
                    const double rowHeaderWidth = 40.0;

                    // 2. Clicked on Title Bar or Table Header Corner -> Drag Table or Edit Title on Double Click
                    var isTitleBarOrCorner = canvasPoint.Y <= table.Y + titleBarHeight ||
                        (canvasPoint.Y <= table.Y + titleBarHeight + table.HeaderHeight && canvasPoint.X <= table.X + rowHeaderWidth);

                    if (isTitleBarOrCorner)
                    {
                        if (e.ClickCount >= 2)
                        {
                            StartTableTitleEditor(table);
                            e.Handled = true;
                            return;
                        }

                        _draggingTable = table;
                        _dragTableStartPos = new Point(table.X, table.Y);
                        _dragMouseStartPos = canvasPoint;
                        CaptureMouse();
                        InvalidateVisual();
                        e.Handled = true;
                        return;
                    }

                    // 3. Clicked on Column Header -> Column Resize or Auto-Fit
                    if (canvasPoint.Y <= table.Y + titleBarHeight + table.HeaderHeight)
                    {
                        var relX = canvasPoint.X - table.X;
                        if (TryGetColumnDivider(table, relX, out var colIdx))
                        {
                            if (e.ClickCount == 2)
                            {
                                AutoFitColumn(table, colIdx);
                                e.Handled = true;
                                return;
                            }

                            _resizingColTable = table;
                            _resizingColIndex = colIdx;
                            _resizingColStartWidth = table.GetColumnWidth(colIdx);
                            _resizeMouseStartX = canvasPoint.X;
                            CaptureMouse();
                            e.Handled = true;
                            return;
                        }
                    }

                    // 4. Clicked on Row Header -> Row Resize
                    if (canvasPoint.X >= table.X && canvasPoint.X <= table.X + rowHeaderWidth && canvasPoint.Y > table.Y + titleBarHeight + table.HeaderHeight)
                    {
                        var relY = canvasPoint.Y - table.Y;
                        if (TryGetRowDivider(table, relY, out var rowIdx))
                        {
                            _resizingRowTable = table;
                            _resizingRowIndex = rowIdx;
                            _resizingRowStartHeight = table.GetRowHeight(rowIdx);
                            _resizeMouseStartY = canvasPoint.Y;
                            CaptureMouse();
                            e.Handled = true;
                            return;
                        }
                    }

                    // 5. Clicked on Cell
                    if (TryHitTestCell(table, canvasPoint, out var coord))
                    {
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _vm.ActiveTable == table)
                        {
                            var range = new CellRange(_vm.ActiveCellCoord, coord);
                            _vm.SelectRange(table, range, _vm.ActiveCellCoord);
                            InvalidateVisual();
                            e.Handled = true;
                            return;
                        }

                        _isSelectingRange = true;
                        _selectionAnchorCoord = coord;
                        _vm.SelectCell(table, coord);

                        if (e.ClickCount == 2)
                        {
                            _isSelectingRange = false;
                            StartFloatingEditor(null);
                        }
                        else
                        {
                            CaptureMouse();
                        }

                        InvalidateVisual();
                        e.Handled = true;
                        return;
                    }
                }
            }

            InvalidateVisual();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var screenPoint = e.GetPosition(this);
        var canvasPoint = ScreenToCanvas(screenPoint);

        if (_isPanning)
        {
            var delta = screenPoint - _lastPanPoint;
            _lastPanPoint = screenPoint;
            if (_vm != null)
            {
                _vm.PanX += delta.X;
                _vm.PanY += delta.Y;
            }
            UpdateFloatingEditorPosition();
            UpdateTitleEditorPosition();
            InvalidateVisual();
            return;
        }

        // Table Corner Resize Dragging
        if (_isResizingTable && _resizingTable != null)
        {
            var deltaX = canvasPoint.X - _resizeTableStartMouse.X;
            var deltaY = canvasPoint.Y - _resizeTableStartMouse.Y;

            var newW = Math.Max(120.0, _resizeTableStartTotalWidth + deltaX);
            var newH = Math.Max(80.0, _resizeTableStartTotalHeight + deltaY);

            _ghostTableRect = new Rect(_resizingTable.X, _resizingTable.Y, newW, newH);
            Cursor = Cursors.SizeNWSE;
            InvalidateVisual();
            return;
        }

        // Table Move Dragging
        if (_draggingTable != null)
        {
            var deltaX = canvasPoint.X - _dragMouseStartPos.X;
            var deltaY = canvasPoint.Y - _dragMouseStartPos.Y;
            var targetX = _dragTableStartPos.X + deltaX;
            var targetY = _dragTableStartPos.Y + deltaY;

            if (_vm?.SnapToGrid == true && _vm.GridSnapSize > 0)
            {
                targetX = Math.Round(targetX / _vm.GridSnapSize) * _vm.GridSnapSize;
                targetY = Math.Round(targetY / _vm.GridSnapSize) * _vm.GridSnapSize;
            }

            _draggingTable.X = targetX;
            _draggingTable.Y = targetY;
            UpdateFloatingEditorPosition();
            UpdateTitleEditorPosition();
            InvalidateVisual();
            return;
        }

        // Column Divider Dragging
        if (_resizingColTable != null && _resizingColIndex >= 0)
        {
            var deltaX = canvasPoint.X - _resizeMouseStartX;
            _resizingColTable.SetColumnWidth(_resizingColIndex, _resizingColStartWidth + deltaX);
            UpdateFloatingEditorPosition();
            InvalidateVisual();
            return;
        }

        // Row Divider Dragging
        if (_resizingRowTable != null && _resizingRowIndex >= 0)
        {
            var deltaY = canvasPoint.Y - _resizeMouseStartY;
            _resizingRowTable.SetRowHeight(_resizingRowIndex, Math.Max(15.0, _resizingRowStartHeight + deltaY));
            UpdateFloatingEditorPosition();
            InvalidateVisual();
            return;
        }

        // Formula Reference Dragging
        if (_isReferencingFormula && _formulaRefTable != null && _vm != null)
        {
            var targetCoord = GetClampedCell(_formulaRefTable, canvasPoint);
            var newRange = new CellRange(_formulaRefStartCoord, targetCoord);
            if (_vm.ReferencedRange != newRange)
            {
                _vm.UpdateInteractiveReference(_formulaRefTable, newRange);
                if (_floatingEditor.Visibility == Visibility.Visible)
                {
                    _isUpdatingTextProgrammatically = true;
                    try
                    {
                        _floatingEditor.Text = _vm.FormulaBarText;
                        _floatingEditor.CaretIndex = _vm.InteractiveRefCaret;
                    }
                    finally
                    {
                        _isUpdatingTextProgrammatically = false;
                    }
                }
                InvalidateVisual();
            }
            return;
        }

        // Range Selection Dragging
        if (_isSelectingRange && _vm?.ActiveTable != null)
        {
            var targetCoord = GetClampedCell(_vm.ActiveTable, canvasPoint);
            if (targetCoord != _vm.SelectedRange.End)
            {
                var newRange = new CellRange(_selectionAnchorCoord, targetCoord);
                _vm.SelectRange(_vm.ActiveTable, newRange, _selectionAnchorCoord);
                InvalidateVisual();
            }
            return;
        }

        // Dynamic Cursors on Hover
        if (_vm?.Document != null)
        {
            foreach (var table in _vm.Document.Tables)
            {
                var totalW = table.GetTotalWidth();
                var totalH = table.GetTotalHeight();
                var gripRect = new Rect(table.X + totalW - 16, table.Y + totalH - 16, 16, 16);
                if (gripRect.Contains(canvasPoint))
                {
                    Cursor = Cursors.SizeNWSE;
                    return;
                }
            }

            if (_vm.ActiveTable != null)
            {
                var table = _vm.ActiveTable;
                const double titleBarHeight = 28.0;
                const double rowHeaderWidth = 40.0;
                var relX = canvasPoint.X - table.X;
                var relY = canvasPoint.Y - table.Y;

                if (relY > titleBarHeight && relY <= titleBarHeight + table.HeaderHeight)
                {
                    if (TryGetColumnDivider(table, relX, out _))
                    {
                        Cursor = Cursors.SizeWE;
                        return;
                    }
                }
                else if (relX >= 0 && relX <= rowHeaderWidth && relY > titleBarHeight + table.HeaderHeight)
                {
                    if (TryGetRowDivider(table, relY, out _))
                    {
                        Cursor = Cursors.SizeNS;
                        return;
                    }
                }
            }
        }

        if (!_isPanning)
        {
            Cursor = Cursors.Arrow;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isReferencingFormula)
        {
            _isReferencingFormula = false;
            ReleaseMouseCapture();
            InvalidateVisual();
            if (_floatingEditor.Visibility == Visibility.Visible)
            {
                _floatingEditor.Focus();
                _floatingEditor.CaretIndex = _vm?.InteractiveRefCaret ?? _floatingEditor.Text.Length;
            }
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        // Commit Table Corner Resize
        if (_isResizingTable && _resizingTable != null && _ghostTableRect.HasValue)
        {
            var finalGhost = _ghostTableRect.Value;
            _isResizingTable = false;
            _ghostTableRect = null;
            ReleaseMouseCapture();

            // Calculate target columns & rows
            const double rowHeaderWidth = 40.0;
            const double titleAndHeaderHeight = 56.0;

            var targetCols = Math.Max(1, (int)Math.Round((finalGhost.Width - rowHeaderWidth) / _resizingTable.DefaultColumnWidth));
            var targetRows = Math.Max(1, (int)Math.Round((finalGhost.Height - titleAndHeaderHeight) / _resizingTable.DefaultRowHeight));

            if (targetCols != _resizeTableStartCols || targetRows != _resizeTableStartRows)
            {
                var cmd = new ResizeTableDimensionsCommand(_resizingTable, _resizeTableStartCols, _resizeTableStartRows, targetCols, targetRows);
                _vm?.UndoRedo.Execute(cmd);
            }

            _resizingTable = null;
            InvalidateVisual();
        }

        if (_draggingTable != null)
        {
            var finalX = _draggingTable.X;
            var finalY = _draggingTable.Y;
            if (_dragTableStartPos.X != finalX || _dragTableStartPos.Y != finalY)
            {
                var cmd = new MoveTableCommand(_draggingTable, _dragTableStartPos.X, _dragTableStartPos.Y, finalX, finalY);
                _vm?.UndoRedo.Execute(cmd);
            }
            _draggingTable = null;
            ReleaseMouseCapture();
            InvalidateVisual();
        }

        if (_resizingColTable != null && _resizingColIndex >= 0)
        {
            var finalWidth = _resizingColTable.GetColumnWidth(_resizingColIndex);
            if (Math.Abs(_resizingColStartWidth - finalWidth) > 0.5)
            {
                var cmd = new ResizeColumnCommand(_resizingColTable, _resizingColIndex, _resizingColStartWidth, finalWidth);
                _vm?.UndoRedo.Execute(cmd);
            }
            _resizingColTable = null;
            _resizingColIndex = -1;
            ReleaseMouseCapture();
            InvalidateVisual();
        }

        if (_resizingRowTable != null && _resizingRowIndex >= 0)
        {
            var finalHeight = _resizingRowTable.GetRowHeight(_resizingRowIndex);
            if (Math.Abs(_resizingRowStartHeight - finalHeight) > 0.5)
            {
                var cmd = new ResizeRowCommand(_resizingRowTable, _resizingRowIndex, _resizingRowStartHeight, finalHeight);
                _vm?.UndoRedo.Execute(cmd);
            }
            _resizingRowTable = null;
            _resizingRowIndex = -1;
            ReleaseMouseCapture();
            InvalidateVisual();
        }

        if (_isSelectingRange)
        {
            _isSelectingRange = false;
            ReleaseMouseCapture();
            InvalidateVisual();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_vm == null) return;

        var mousePos = e.GetPosition(this);
        var oldZoom = _vm.Zoom;
        var zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
        var newZoom = Math.Clamp(oldZoom * zoomFactor, 0.2, 3.0);

        _vm.PanX = mousePos.X - ((mousePos.X - _vm.PanX) * (newZoom / oldZoom));
        _vm.PanY = mousePos.Y - ((mousePos.Y - _vm.PanY) * (newZoom / oldZoom));
        _vm.Zoom = newZoom;

        UpdateFloatingEditorPosition();
        UpdateTitleEditorPosition();
        InvalidateVisual();
        e.Handled = true;
    }

    #endregion

    #region Context Menu

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (_vm?.Document == null) return;

        var screenPoint = e.GetPosition(this);
        var canvasPoint = ScreenToCanvas(screenPoint);

        for (var i = _vm.Document.Tables.Count - 1; i >= 0; i--)
        {
            var table = _vm.Document.Tables[i];
            var tableRect = new Rect(table.X, table.Y, table.GetTotalWidth(), table.GetTotalHeight());

            if (tableRect.Contains(canvasPoint))
            {
                _vm.ActiveTable = table;
                const double titleBarHeight = 28.0;

                var menu = new ContextMenu();

                if (canvasPoint.Y <= table.Y + titleBarHeight)
                {
                    // Table Header Context Menu
                    var renameItem = new MenuItem { Header = "Rename Table (Double-Click)", Icon = CreateSvgIcon("pencil.svg") };
                    renameItem.Click += (_, _) => StartTableTitleEditor(table);

                    var focusItem = new MenuItem { Header = "Focus in Viewport", Icon = CreateSvgIcon("target.svg") };
                    focusItem.Click += (_, _) => _vm.FocusTable(table);

                    var duplicateItem = new MenuItem { Header = "Duplicate Table", Icon = CreateSvgIcon("table-duplicate.svg") };
                    duplicateItem.Click += (_, _) => _vm.DuplicateTable(table);

                    var colorMenu = new MenuItem { Header = "Header Color", Icon = CreateSvgIcon("palette.svg") };

                    static MenuItem CreateColorItem(string name, string? hex, Action onClick)
                    {
                        var item = new MenuItem();
                        var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                        var swatch = new Border
                        {
                            Width = 24,
                            Height = 14,
                            CornerRadius = new CornerRadius(3),
                            Margin = new Thickness(0, 0, 8, 0),
                            Background = hex != null ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) : new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                            BorderThickness = new Thickness(1)
                        };
                        var text = new TextBlock
                        {
                            Text = name,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontWeight = FontWeights.Medium
                        };
                        stack.Children.Add(swatch);
                        stack.Children.Add(text);
                        item.Header = stack;
                        item.Click += (_, _) => onClick();
                        return item;
                    }

                    colorMenu.Items.Add(CreateColorItem("Default Slate", null, () => _vm.SetActiveTableHeaderColor(null)));
                    colorMenu.Items.Add(CreateColorItem("Navy Blue", "#1E3A8A", () => _vm.SetActiveTableHeaderColor("#1E3A8A")));
                    colorMenu.Items.Add(CreateColorItem("Emerald Green", "#065F46", () => _vm.SetActiveTableHeaderColor("#065F46")));
                    colorMenu.Items.Add(CreateColorItem("Crimson Red", "#991B1B", () => _vm.SetActiveTableHeaderColor("#991B1B")));
                    colorMenu.Items.Add(CreateColorItem("Amber Orange", "#92400E", () => _vm.SetActiveTableHeaderColor("#92400E")));
                    colorMenu.Items.Add(CreateColorItem("Royal Purple", "#5B21B6", () => _vm.SetActiveTableHeaderColor("#5B21B6")));
                    colorMenu.Items.Add(CreateColorItem("Ocean Teal", "#0F766E", () => _vm.SetActiveTableHeaderColor("#0F766E")));

                    var addRowItem = new MenuItem { Header = "Add Row at End", Icon = CreateSvgIcon("row-insert-below.svg") };
                    addRowItem.Click += (_, _) => _vm.InsertRowBelowCommand.Execute(null);

                    var addColItem = new MenuItem { Header = "Add Column at End", Icon = CreateSvgIcon("col-insert-right.svg") };
                    addColItem.Click += (_, _) => _vm.InsertColumnRightCommand.Execute(null);

                    var deleteItem = new MenuItem { Header = "Delete Table", Icon = CreateSvgIcon("trash.svg"), Foreground = Brushes.Crimson };
                    deleteItem.Click += (_, _) => _vm.DeleteTableCommand.Execute(table);

                    menu.Items.Add(renameItem);
                    menu.Items.Add(focusItem);
                    menu.Items.Add(duplicateItem);
                    menu.Items.Add(colorMenu);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(addRowItem);
                    menu.Items.Add(addColItem);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(deleteItem);
                }
                else if (TryHitTestCell(table, canvasPoint, out var coord))
                {
                    // Cell Context Menu
                    if (!_vm.SelectedRange.Contains(coord))
                    {
                        _vm.SelectCell(table, coord);
                    }

                    // Clipboard Actions
                    var cutItem = new MenuItem { Header = "Cut (Ctrl+X)", Icon = CreateSvgIcon("cut.svg") };
                    cutItem.Click += (_, _) => _vm.CutSelectionCommand.Execute(null);

                    var copyItem = new MenuItem { Header = "Copy (Ctrl+C)", Icon = CreateSvgIcon("copy.svg") };
                    copyItem.Click += (_, _) => _vm.CopySelectionCommand.Execute(null);

                    var pasteItem = new MenuItem { Header = "Paste (Ctrl+V)", Icon = CreateSvgIcon("paste.svg") };
                    pasteItem.Click += (_, _) => _vm.PasteClipboardCommand.Execute(null);

                    // Formatting Actions
                    var boldItem = new MenuItem { Header = "Bold (Ctrl+B)", Icon = CreateSvgIcon("bold.svg") };
                    boldItem.Click += (_, _) => _vm.ToggleBoldCommand.Execute(null);

                    var italicItem = new MenuItem { Header = "Italic (Ctrl+I)", Icon = CreateSvgIcon("italic.svg") };
                    italicItem.Click += (_, _) => _vm.ToggleItalicCommand.Execute(null);

                    var clearCell = new MenuItem { Header = "Clear Contents (Delete)", Icon = CreateSvgIcon("clear.svg") };
                    clearCell.Click += (_, _) => _vm.ClearSelectedCellsCommand.Execute(null);

                    // Number Format Submenu
                    var formatMenu = new MenuItem { Header = "Number Format", Icon = CreateSvgIcon("format-number.svg") };
                    var fmtGeneral = new MenuItem { Header = "General", Icon = CreateSvgIcon("format-general.svg") };
                    fmtGeneral.Click += (_, _) => _vm.SetNumberFormatCommand.Execute(null);
                    var fmtCurrency = new MenuItem { Header = "Currency ($#,##0.00)", Icon = CreateSvgIcon("format-currency.svg") };
                    fmtCurrency.Click += (_, _) => _vm.SetNumberFormatCommand.Execute("C2");
                    var fmtPercent = new MenuItem { Header = "Percentage (0.0%)", Icon = CreateSvgIcon("format-percent.svg") };
                    fmtPercent.Click += (_, _) => _vm.SetNumberFormatCommand.Execute("P1");
                    var fmtComma = new MenuItem { Header = "Comma Number (#,##0.00)", Icon = CreateSvgIcon("format-comma.svg") };
                    fmtComma.Click += (_, _) => _vm.SetNumberFormatCommand.Execute("N2");

                    formatMenu.Items.Add(fmtGeneral);
                    formatMenu.Items.Add(fmtCurrency);
                    formatMenu.Items.Add(fmtPercent);
                    formatMenu.Items.Add(fmtComma);

                    // Row & Column Operations
                    var insRowAbove = new MenuItem { Header = "Insert Row Above", Icon = CreateSvgIcon("row-insert-above.svg") };
                    insRowAbove.Click += (_, _) => _vm.InsertRowAboveCommand.Execute(null);

                    var insRowBelow = new MenuItem { Header = "Insert Row Below", Icon = CreateSvgIcon("row-insert-below.svg") };
                    insRowBelow.Click += (_, _) => _vm.InsertRowBelowCommand.Execute(null);

                    var insColLeft = new MenuItem { Header = "Insert Column Left", Icon = CreateSvgIcon("col-insert-left.svg") };
                    insColLeft.Click += (_, _) => _vm.InsertColumnLeftCommand.Execute(null);

                    var insColRight = new MenuItem { Header = "Insert Column Right", Icon = CreateSvgIcon("col-insert-right.svg") };
                    insColRight.Click += (_, _) => _vm.InsertColumnRightCommand.Execute(null);

                    var delRow = new MenuItem { Header = "Delete Row", Icon = CreateSvgIcon("row-delete.svg") };
                    delRow.Click += (_, _) => _vm.DeleteRowCommand.Execute(null);

                    var delCol = new MenuItem { Header = "Delete Column", Icon = CreateSvgIcon("col-delete.svg") };
                    delCol.Click += (_, _) => _vm.DeleteColumnCommand.Execute(null);

                    menu.Items.Add(cutItem);
                    menu.Items.Add(copyItem);
                    menu.Items.Add(pasteItem);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(boldItem);
                    menu.Items.Add(italicItem);
                    menu.Items.Add(formatMenu);
                    menu.Items.Add(clearCell);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(insRowAbove);
                    menu.Items.Add(insRowBelow);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(insColLeft);
                    menu.Items.Add(insColRight);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(delRow);
                    menu.Items.Add(delCol);
                }

                menu.IsOpen = true;
                e.Handled = true;
                return;
            }
        }
    }

    private static SvgViewbox CreateSvgIcon(string iconName, double size = 15)
    {
        return new SvgViewbox
        {
            Source = new Uri($"/Resources/Icons/{iconName}", UriKind.Relative),
            Width = size,
            Height = size,
            IsHitTestVisible = false
        };
    }

    #endregion

    #region In-Place Cell Editor

    public void StartFloatingEditor(string? initialText)
    {
        if (_vm?.ActiveTable == null) return;

        var table = _vm.ActiveTable;
        var coord = _vm.ActiveCellCoord;
        var cell = table.GetCell(coord);

        _vm.BeginCellEdit(table, coord);

        _isUpdatingTextProgrammatically = true;
        try
        {
            if (initialText != null)
            {
                _floatingEditor.Text = initialText;
                _floatingEditor.CaretIndex = initialText.Length;
                _vm.UpdateCellLive(table, coord, initialText);
            }
            else
            {
                _floatingEditor.Text = cell?.RawInput ?? string.Empty;
                _floatingEditor.SelectAll();
            }
        }
        finally
        {
            _isUpdatingTextProgrammatically = false;
        }

        UpdateFloatingEditorPosition();
        _floatingEditor.Visibility = Visibility.Visible;
        if (_floatingEditor.Text.StartsWith('='))
        {
            _vm.IsFormulaEditing = true;
        }
        _floatingEditor.Focus();
    }

    private void UpdateFloatingEditorPosition()
    {
        if (_vm?.ActiveTable == null || _floatingEditor.Visibility != Visibility.Visible) return;

        var table = _vm.ActiveTable;
        var coord = _vm.ActiveCellCoord;

        var cellCanvasX = table.X + table.GetColumnX(coord.Column);
        var cellCanvasY = table.Y + table.GetRowY(coord.Row);
        var cellW = table.GetColumnWidth(coord.Column);
        var cellH = table.GetRowHeight(coord.Row);

        var screenTopLeft = CanvasToScreen(new Point(cellCanvasX, cellCanvasY));
        var zoom = _vm.Zoom;

        _editorScreenRect = new Rect(
            screenTopLeft.X,
            screenTopLeft.Y,
            Math.Max(60, cellW * zoom),
            Math.Max(24, cellH * zoom));

        _floatingEditor.FontSize = Math.Max(9, 12.0 * zoom);
        InvalidateArrange();
    }

    public void CommitFloatingEditor()
    {
        if (_floatingEditor.Visibility != Visibility.Visible) return;

        var text = _floatingEditor.Text;

        _floatingEditor.Visibility = Visibility.Collapsed;
        _editorScreenRect = Rect.Empty;
        InvalidateArrange();

        if (_vm?.ActiveTable != null)
        {
            _vm.CommitCellEdit(_vm.ActiveTable, _vm.ActiveCellCoord, text);
        }

        InvalidateVisual();
        Focus();
    }

    private void FloatingEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitFloatingEditor();
            if (_vm?.ActiveTable != null)
            {
                var cur = _vm.ActiveCellCoord;
                var isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                var targetRow = isShift ? Math.Max(0, cur.Row - 1) : cur.Row + 1;
                _vm.SelectCell(_vm.ActiveTable, new CellCoordinate(targetRow, cur.Column));
            }
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            CommitFloatingEditor();
            if (_vm?.ActiveTable != null)
            {
                var cur = _vm.ActiveCellCoord;
                var isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                var targetCol = isShift ? Math.Max(0, cur.Column - 1) : cur.Column + 1;
                _vm.SelectCell(_vm.ActiveTable, new CellCoordinate(cur.Row, targetCol));
            }
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _floatingEditor.Visibility = Visibility.Collapsed;
            _editorScreenRect = Rect.Empty;
            InvalidateArrange();
            if (_vm?.ActiveTable != null)
            {
                _vm.RevertCellEdit(_vm.ActiveTable, _vm.ActiveCellCoord);
            }
            Focus();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void FloatingEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isReferencingFormula) return;
        CommitFloatingEditor();
    }

    #endregion

    #region In-Place Table Title Editor

    public void StartTableTitleEditor(TableBlock table)
    {
        CommitFloatingEditor();
        _editingTitleTable = table;
        _titleEditor.Text = table.Name;
        _titleEditor.Visibility = Visibility.Visible;
        UpdateTitleEditorPosition();
        InvalidateMeasure();
        InvalidateArrange();
        _titleEditor.Focus();
        Keyboard.Focus(_titleEditor);
        _titleEditor.SelectAll();
        InvalidateVisual();
    }

    public void UpdateTitleEditorPosition()
    {
        if (_editingTitleTable == null || _vm == null) return;
        const double titleBarHeight = 28.0;
        var totalW = _editingTitleTable.GetTotalWidth();
        var canvasTopLeft = new Point(_editingTitleTable.X + 4, _editingTitleTable.Y + 2);
        var canvasBottomRight = new Point(_editingTitleTable.X + Math.Min(totalW - 8, 260), _editingTitleTable.Y + titleBarHeight - 2);

        var screenTopLeft = CanvasToScreen(canvasTopLeft);
        var screenBottomRight = CanvasToScreen(canvasBottomRight);

        _titleEditorScreenRect = new Rect(screenTopLeft, screenBottomRight);
        _titleEditor.FontSize = Math.Max(10, 12.5 * _vm.Zoom);
        InvalidateMeasure();
        InvalidateArrange();
    }

    public void CommitTitleEditor()
    {
        if (_titleEditor.Visibility != Visibility.Visible || _editingTitleTable == null || _vm == null) return;
        var newName = _titleEditor.Text.Trim();
        var table = _editingTitleTable;
        _titleEditor.Visibility = Visibility.Collapsed;
        _editingTitleTable = null;
        _titleEditorScreenRect = Rect.Empty;
        InvalidateMeasure();
        InvalidateArrange();

        if (!string.IsNullOrWhiteSpace(newName) && newName != table.Name)
        {
            var cmd = new RenameTableCommand(table, table.Name, newName);
            _vm.UndoRedo.Execute(cmd);
            _vm.StatusMessage = $"Renamed table to {table.Name}";
        }
        InvalidateVisual();
        Focus();
    }

    public void CancelTitleEditor()
    {
        _titleEditor.Visibility = Visibility.Collapsed;
        _editingTitleTable = null;
        _titleEditorScreenRect = Rect.Empty;
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
        Focus();
    }

    private void TitleEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTitleEditor();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelTitleEditor();
            e.Handled = true;
        }
    }

    private void TitleEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitTitleEditor();
    }

    #endregion

    #region Hit Testing Helpers

    private static bool TryHitTestCell(TableBlock table, Point canvasPoint, out CellCoordinate coord)
    {
        coord = default;
        const double rowHeaderWidth = 40.0;
        const double titleBarHeight = 28.0;

        var relX = canvasPoint.X - table.X;
        var relY = canvasPoint.Y - table.Y;

        if (relX < rowHeaderWidth || relY < titleBarHeight + table.HeaderHeight)
        {
            return false;
        }

        var currX = rowHeaderWidth;
        var foundCol = -1;
        for (var c = 0; c < table.ColumnCount; c++)
        {
            var w = table.GetColumnWidth(c);
            if (relX >= currX && relX < currX + w)
            {
                foundCol = c;
                break;
            }
            currX += w;
        }

        var currY = titleBarHeight + table.HeaderHeight;
        var foundRow = -1;
        for (var r = 0; r < table.RowCount; r++)
        {
            var h = table.GetRowHeight(r);
            if (relY >= currY && relY < currY + h)
            {
                foundRow = r;
                break;
            }
            currY += h;
        }

        if (foundRow >= 0 && foundCol >= 0)
        {
            coord = new CellCoordinate(foundRow, foundCol);
            return true;
        }

        return false;
    }

    private static bool TryGetColumnDivider(TableBlock table, double relX, out int colIndex)
    {
        colIndex = -1;
        const double rowHeaderWidth = 40.0;
        var currX = rowHeaderWidth;

        for (var c = 0; c < table.ColumnCount; c++)
        {
            currX += table.GetColumnWidth(c);
            if (Math.Abs(relX - currX) <= 4.0)
            {
                colIndex = c;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRowDivider(TableBlock table, double relY, out int rowIndex)
    {
        rowIndex = -1;
        const double titleBarHeight = 28.0;
        var currY = titleBarHeight + table.HeaderHeight;

        for (var r = 0; r < table.RowCount; r++)
        {
            currY += table.GetRowHeight(r);
            if (Math.Abs(relY - currY) <= 4.0)
            {
                rowIndex = r;
                return true;
            }
        }

        return false;
    }

    public void AutoFitColumn(TableBlock table, int colIndex)
    {
        var headerLetter = CellCoordinate.ColumnToLetters(colIndex);
        var headerFormatted = new FormattedText(
            headerLetter,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            DefaultTypeface,
            11.0,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var maxWidth = headerFormatted.Width;

        for (var r = 0; r < table.RowCount; r++)
        {
            var cell = table.GetCell(r, colIndex);
            if (cell != null && !cell.Value.IsEmpty)
            {
                var text = cell.Value.Format(cell.Style.NumberFormat);
                var formatted = new FormattedText(
                    text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    DefaultTypeface,
                    11.5,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                if (formatted.Width > maxWidth)
                {
                    maxWidth = formatted.Width;
                }
            }
        }

        var targetWidth = Math.Max(40.0, maxWidth + 20.0);
        var oldWidth = table.GetColumnWidth(colIndex);
        if (Math.Abs(oldWidth - targetWidth) > 0.5)
        {
            var cmd = new ResizeColumnCommand(table, colIndex, oldWidth, targetWidth);
            _vm?.UndoRedo.Execute(cmd);
            InvalidateVisual();
        }
    }

    private static CellCoordinate GetClampedCell(TableBlock table, Point canvasPoint)
    {
        const double rowHeaderWidth = 40.0;
        const double titleBarHeight = 28.0;

        var relX = canvasPoint.X - table.X - rowHeaderWidth;
        var relY = canvasPoint.Y - table.Y - titleBarHeight - table.HeaderHeight;

        var col = 0;
        var currX = 0.0;
        for (var c = 0; c < table.ColumnCount; c++)
        {
            var w = table.GetColumnWidth(c);
            if (relX < currX + w) { col = c; break; }
            currX += w;
            if (c == table.ColumnCount - 1) col = c;
        }
        if (relX < 0) col = 0;

        var row = 0;
        var currY = 0.0;
        for (var r = 0; r < table.RowCount; r++)
        {
            var h = table.GetRowHeight(r);
            if (relY < currY + h) { row = r; break; }
            currY += h;
            if (r == table.RowCount - 1) row = r;
        }
        if (relY < 0) row = 0;

        return new CellCoordinate(Math.Clamp(row, 0, table.RowCount - 1), Math.Clamp(col, 0, table.ColumnCount - 1));
    }

    #endregion
}
