using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SpreadsheetApp.Core.Engine;
using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private GridLength _lastSidebarWidth = new(260);

    public MainWindow()
    {
        InitializeComponent();

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpreadsheetApp.ViewModels.MainViewModel.IsSidebarOpen))
            {
                UpdateSidebarVisibility(ViewModel.IsSidebarOpen);
            }
        };

        UpdateSidebarVisibility(ViewModel.IsSidebarOpen);
    }

    private void UpdateSidebarVisibility(bool isOpen)
    {
        if (SidebarColumn == null) return;

        if (isOpen)
        {
            SidebarColumn.MinWidth = 180;
            SidebarColumn.MaxWidth = 500;
            SidebarColumn.Width = _lastSidebarWidth.Value >= 180 ? _lastSidebarWidth : new GridLength(260);
            if (SidebarSplitter != null)
            {
                SidebarSplitter.Visibility = Visibility.Visible;
            }
        }
        else
        {
            if (SidebarColumn.Width.Value > 0)
            {
                _lastSidebarWidth = SidebarColumn.Width;
            }
            SidebarColumn.MinWidth = 0;
            SidebarColumn.Width = new GridLength(0);
            if (SidebarSplitter != null)
            {
                SidebarSplitter.Visibility = Visibility.Collapsed;
            }
        }

        CanvasView?.InvalidateVisual();
    }

    private void FormulaBar_KeyDown(object sender, KeyEventArgs e)
    {
        // Handled primarily in FormulaBar_PreviewKeyDown to prevent newline on commit
    }

    private void FormulaBar_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (FormulaAutocompletePopup.IsOpen)
        {
            if (e.Key == Key.Down)
            {
                var next = FormulaAutocompleteList.SelectedIndex + 1;
                if (next < FormulaAutocompleteList.Items.Count)
                {
                    FormulaAutocompleteList.SelectedIndex = next;
                    FormulaAutocompleteList.ScrollIntoView(FormulaAutocompleteList.SelectedItem);
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                var prev = FormulaAutocompleteList.SelectedIndex - 1;
                if (prev >= 0)
                {
                    FormulaAutocompleteList.SelectedIndex = prev;
                    FormulaAutocompleteList.ScrollIntoView(FormulaAutocompleteList.SelectedItem);
                }
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Tab or Key.Enter)
            {
                CommitAutocompleteSelection();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                FormulaAutocompletePopup.IsOpen = false;
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            FormulaAutocompletePopup.IsOpen = false;
            FormulaParameterHintBar.Visibility = Visibility.Collapsed;
            ViewModel.CommitFormulaBarCommand.Execute(null);
            CanvasView.InvalidateVisual();
            CanvasView.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            FormulaAutocompletePopup.IsOpen = false;
            FormulaParameterHintBar.Visibility = Visibility.Collapsed;
            if (ViewModel.ActiveTable != null)
            {
                ViewModel.RevertCellEdit(ViewModel.ActiveTable, ViewModel.ActiveCellCoord);
            }
            CanvasView.InvalidateVisual();
            CanvasView.Focus();
            e.Handled = true;
            return;
        }
    }

    private void FormulaBar_GotFocus(object sender, RoutedEventArgs e)
    {
        if (FormulaBarInput.Text.StartsWith('='))
        {
            ViewModel.IsFormulaEditing = true;
            UpdateFormulaAssist();
        }
    }

    private void FormulaBar_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!FormulaAutocompletePopup.IsKeyboardFocusWithin)
        {
            FormulaAutocompletePopup.IsOpen = false;
        }
    }

    private void FormulaBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (FormulaBarInput.IsFocused && ViewModel.ActiveTable != null)
        {
            var text = FormulaBarInput.Text;
            ViewModel.IsFormulaEditing = text.StartsWith('=');
            ViewModel.UpdateCellLive(ViewModel.ActiveTable, ViewModel.ActiveCellCoord, text);

            var caret = FormulaBarInput.CaretIndex;
            if (FormulaReferenceHelper.IsExpectingReference(text, caret))
            {
                ViewModel.ResetInteractiveReference();
                ViewModel.InteractiveRefCaret = caret;
            }

            UpdateFormulaAssist();
            CanvasView.InvalidateVisual();
        }
    }

    private void FormulaBar_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (FormulaBarInput.IsFocused)
        {
            UpdateFormulaAssist();
        }
    }

    private void FormulaAutocompleteList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Tab or Key.Enter)
        {
            CommitAutocompleteSelection();
            FormulaBarInput.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            FormulaAutocompletePopup.IsOpen = false;
            FormulaBarInput.Focus();
            e.Handled = true;
        }
    }

    private void FormulaAutocompleteList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        CommitAutocompleteSelection();
        FormulaBarInput.Focus();
        e.Handled = true;
    }

    private void CommitAutocompleteSelection()
    {
        if (FormulaAutocompleteList.SelectedItem is FunctionInfo func)
        {
            var text = FormulaBarInput.Text;
            var caret = FormulaBarInput.CaretIndex;
            var context = FunctionCatalog.ParseContext(text, caret);

            var prefix = context.FunctionPrefix ?? string.Empty;
            var replaceStart = Math.Max(0, caret - prefix.Length);
            var before = text[..replaceStart];
            var after = text[caret..];

            var insertText = func.Name + "(";
            var newText = before + insertText + after;
            var newCaret = replaceStart + insertText.Length;

            FormulaBarInput.Text = newText;
            FormulaBarInput.CaretIndex = newCaret;
            FormulaAutocompletePopup.IsOpen = false;
            UpdateFormulaAssist();
        }
    }

    private void UpdateFormulaAssist()
    {
        var text = FormulaBarInput.Text;
        var caret = FormulaBarInput.CaretIndex;

        if (string.IsNullOrEmpty(text) || !text.StartsWith('='))
        {
            FormulaAutocompletePopup.IsOpen = false;
            FormulaParameterHintBar.Visibility = Visibility.Collapsed;
            if (ViewModel.ReferencedRange != null)
            {
                ViewModel.ReferencedTable = null;
                ViewModel.ReferencedRange = null;
                CanvasView.InvalidateVisual();
            }
            return;
        }

        var context = FunctionCatalog.ParseContext(text, caret);

        // 1. Parameter Hint Bar
        if (context.ArgumentHint != null)
        {
            FormulaParameterHintText.Text = context.ArgumentHint;
            FormulaParameterHintBar.Visibility = Visibility.Visible;
        }
        else if (context.MatchingParenIndex.HasValue)
        {
            FormulaParameterHintText.Text = $"Matched parenthesis pair at position {context.MatchingParenIndex.Value + 1}";
            FormulaParameterHintBar.Visibility = Visibility.Visible;
        }
        else
        {
            FormulaParameterHintBar.Visibility = Visibility.Collapsed;
        }

        // 2. Autocomplete Popup
        if (context.IsTypingFunctionName && !string.IsNullOrWhiteSpace(context.FunctionPrefix))
        {
            var suggestions = FunctionCatalog.GetSuggestions(context.FunctionPrefix).ToList();
            if (suggestions.Count > 0)
            {
                FormulaAutocompleteList.ItemsSource = suggestions;
                if (FormulaAutocompleteList.SelectedIndex < 0 || FormulaAutocompleteList.SelectedIndex >= suggestions.Count)
                {
                    FormulaAutocompleteList.SelectedIndex = 0;
                }
                FormulaAutocompletePopup.IsOpen = true;
            }
            else
            {
                FormulaAutocompletePopup.IsOpen = false;
            }
        }
        else
        {
            FormulaAutocompletePopup.IsOpen = false;
        }
    }

    private void DropdownMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.DataContext = btn.DataContext;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void DropdownMenuButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.ContextMenu != null)
        {
            fe.ContextMenu.PlacementTarget = fe;
            fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            fe.ContextMenu.DataContext = fe.DataContext;
        }
    }

    private void NavigatorRenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TableBlock table)
        {
            ViewModel.FocusTable(table);
            CanvasView.StartTableTitleEditor(table);
        }
    }

    private void NavigatorTableTitle_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TableBlock table)
        {
            ViewModel.FocusTable(table);
            CanvasView.StartTableTitleEditor(table);
            e.Handled = true;
        }
    }
}