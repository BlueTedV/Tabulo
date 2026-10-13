using CommunityToolkit.Mvvm.ComponentModel;

namespace SpreadsheetApp.Core.Commands;

public interface IUndoableCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Centralized, linear undo/redo manager tracking canvas moves, cell edits, and structural changes chronologically.
/// </summary>
public partial class UndoRedoManager : ObservableObject
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();

    public event EventHandler? HistoryChanged;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string _undoDescription = string.Empty;

    [ObservableProperty]
    private string _redoDescription = string.Empty;

    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        UpdateState();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        UpdateState();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        UpdateState();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateState();
    }

    private void UpdateState()
    {
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
        UndoDescription = _undoStack.Count > 0 ? _undoStack.Peek().Description : string.Empty;
        RedoDescription = _redoStack.Count > 0 ? _redoStack.Peek().Description : string.Empty;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
