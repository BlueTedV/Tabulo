using System.Text.RegularExpressions;
using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Core.Engine;

/// <summary>
/// Helper for smart interactive formula reference insertion and token management.
/// </summary>
public static class FormulaReferenceHelper
{
    /// <summary>
    /// Checks if a formula string is currently open for editing and actively expecting a cell reference.
    /// Returns true if formula starts with '=' and the character before caret is an operator, open paren, or comma.
    /// </summary>
    public static bool IsExpectingReference(string text, int caretIndex)
    {
        if (string.IsNullOrEmpty(text) || !text.StartsWith('=')) return false;
        if (caretIndex <= 1) return true; // Just "="

        var textBeforeCaret = text[..caretIndex].TrimEnd();
        if (textBeforeCaret.Length == 0) return false;

        var lastChar = textBeforeCaret[^1];
        return lastChar is '=' or '(' or ',' or '+' or '-' or '*' or '/' or '^' or '&' or ':' or '<' or '>' or ';';
    }

    /// <summary>
    /// Formats a cell coordinate or range into an A1 formula reference.
    /// If targetTable is different from activeTable, prefixes with "TableName!".
    /// </summary>
    public static string FormatReference(string activeTableName, string targetTableName, CellRange range)
    {
        var address = range.TotalCells == 1 ? range.Start.ToA1() : range.ToString();
        if (string.Equals(activeTableName, targetTableName, StringComparison.OrdinalIgnoreCase))
        {
            return address;
        }

        // Cross-table reference
        var safeTableName = targetTableName.Contains(' ') ? $"'{targetTableName}'" : targetTableName;
        return $"{safeTableName}!{address}";
    }

    /// <summary>
    /// Inserts or replaces an existing cell reference at the caret position.
    /// </summary>
    public static (string NewText, int NewCaretIndex, int RefLength) InsertOrUpdateReference(
        string currentText,
        int caretIndex,
        string newReference,
        int previousRefLength)
    {
        currentText ??= "=";
        if (!currentText.StartsWith('='))
        {
            currentText = "=" + currentText;
            caretIndex++;
        }

        caretIndex = Math.Clamp(caretIndex, 0, currentText.Length);

        // If a previous reference was just inserted in this interactive gesture, replace it
        if (previousRefLength > 0 && caretIndex >= previousRefLength)
        {
            var replaceStart = caretIndex - previousRefLength;
            var before = currentText[..replaceStart];
            var after = currentText[caretIndex..];
            var updated = before + newReference + after;
            var newCaret = replaceStart + newReference.Length;
            return (updated, newCaret, newReference.Length);
        }

        // Otherwise insert at caret
        var beforeText = currentText[..caretIndex];
        var afterText = currentText[caretIndex..];
        var result = beforeText + newReference + afterText;
        var caret = caretIndex + newReference.Length;
        return (result, caret, newReference.Length);
    }
}
