using System.Text.RegularExpressions;

namespace SpreadsheetApp.Core.Engine;

/// <summary>
/// Represents metadata for a built-in spreadsheet function.
/// </summary>
public record FunctionInfo(
    string Name,
    string Syntax,
    string Description,
    string Category,
    IReadOnlyList<string> ParameterNames
);

/// <summary>
/// Provides metadata, search, and intelligent context parsing for spreadsheet formulas and functions.
/// </summary>
public static class FunctionCatalog
{
    public static readonly IReadOnlyList<FunctionInfo> Functions = new List<FunctionInfo>
    {
        new("SUM", "SUM(number1, [number2], ...)", "Adds all numbers in a range of cells.", "Math & Trig", new[] { "number1", "number2", "..." }),
        new("AVERAGE", "AVERAGE(number1, [number2], ...)", "Returns the average (arithmetic mean) of arguments.", "Statistical", new[] { "number1", "number2", "..." }),
        new("COUNT", "COUNT(value1, [value2], ...)", "Counts how many numbers are in the list of arguments.", "Statistical", new[] { "value1", "value2", "..." }),
        new("COUNTA", "COUNTA(value1, [value2], ...)", "Counts how many non-empty values are in the list of arguments.", "Statistical", new[] { "value1", "value2", "..." }),
        new("MIN", "MIN(number1, [number2], ...)", "Returns the smallest number in a set of values.", "Statistical", new[] { "number1", "number2", "..." }),
        new("MAX", "MAX(number1, [number2], ...)", "Returns the largest number in a set of values.", "Statistical", new[] { "number1", "number2", "..." }),
        new("PRODUCT", "PRODUCT(number1, [number2], ...)", "Multiplies all numbers given as arguments.", "Math & Trig", new[] { "number1", "number2", "..." }),
        new("IF", "IF(logical_test, value_if_true, [value_if_false])", "Returns one value if condition is TRUE, and another if FALSE.", "Logical", new[] { "logical_test", "value_if_true", "value_if_false" }),
        new("AND", "AND(logical1, [logical2], ...)", "Returns TRUE if all arguments evaluate to TRUE.", "Logical", new[] { "logical1", "logical2", "..." }),
        new("OR", "OR(logical1, [logical2], ...)", "Returns TRUE if any argument evaluates to TRUE.", "Logical", new[] { "logical1", "logical2", "..." }),
        new("NOT", "NOT(logical)", "Reverses the logic of its argument.", "Logical", new[] { "logical" }),
        new("ABS", "ABS(number)", "Returns the absolute value of a number.", "Math & Trig", new[] { "number" }),
        new("ROUND", "ROUND(number, num_digits)", "Rounds a number to a specified number of digits.", "Math & Trig", new[] { "number", "num_digits" }),
        new("SQRT", "SQRT(number)", "Returns the positive square root of a number.", "Math & Trig", new[] { "number" }),
        new("POWER", "POWER(number, power)", "Returns the result of a number raised to a power.", "Math & Trig", new[] { "number", "power" }),
        new("CONCAT", "CONCAT(text1, [text2], ...)", "Combines text from multiple ranges and/or strings.", "Text", new[] { "text1", "text2", "..." }),
        new("CONCATENATE", "CONCATENATE(text1, [text2], ...)", "Joins several text strings into one string.", "Text", new[] { "text1", "text2", "..." }),
        new("LEFT", "LEFT(text, [num_chars])", "Returns the specified number of characters from start of text.", "Text", new[] { "text", "num_chars" }),
        new("RIGHT", "RIGHT(text, [num_chars])", "Returns the specified number of characters from end of text.", "Text", new[] { "text", "num_chars" }),
        new("MID", "MID(text, start_num, num_chars)", "Returns characters from the middle of a text string.", "Text", new[] { "text", "start_num", "num_chars" }),
        new("LEN", "LEN(text)", "Returns the number of characters in a text string.", "Text", new[] { "text" }),
        new("TRIM", "TRIM(text)", "Removes spaces from text except single spaces between words.", "Text", new[] { "text" }),
        new("UPPER", "UPPER(text)", "Converts text to uppercase.", "Text", new[] { "text" }),
        new("LOWER", "LOWER(text)", "Converts text to lowercase.", "Text", new[] { "text" }),
        new("INDEX", "INDEX(array, row_num, [col_num])", "Returns a value from a specified cell within a table or range.", "Lookup", new[] { "array", "row_num", "col_num" })
    };

    private static readonly Dictionary<string, FunctionInfo> FunctionLookup =
        Functions.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

    public static FunctionInfo? GetFunction(string name)
    {
        return FunctionLookup.TryGetValue(name, out var info) ? info : null;
    }

    /// <summary>
    /// Returns autocomplete suggestions matching the given prefix.
    /// </summary>
    public static IReadOnlyList<FunctionInfo> GetSuggestions(string prefix, int maxResults = 8)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Functions.Take(maxResults).ToList();
        }

        var trimmed = prefix.Trim();
        var prefixMatches = Functions
            .Where(f => f.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var containsMatches = Functions
            .Where(f => !f.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) &&
                        f.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return prefixMatches.Concat(containsMatches).Take(maxResults).ToList();
    }

    /// <summary>
    /// Parses the formula text at the caret position to identify if the user is typing a function name
    /// or is inside function arguments.
    /// </summary>
    public static FormulaContextResult ParseContext(string formulaText, int caretIndex)
    {
        if (string.IsNullOrEmpty(formulaText) || caretIndex < 0 || caretIndex > formulaText.Length)
        {
            return new FormulaContextResult(false, null, 0, null, 0, null, null);
        }

        var textUpToCaret = formulaText[..caretIndex];

        // 1. Check if user is typing a function name prefix (e.g. "=S", "=SUM", "=A+S")
        var prefixMatch = Regex.Match(textUpToCaret, @"(?:=|[+\-*/,(&])([A-Za-z_][A-Za-z0-9_]*)$");
        if (prefixMatch.Success)
        {
            var prefixGroup = prefixMatch.Groups[1];
            var prefix = prefixGroup.Value;
            var startIndex = prefixGroup.Index;

            // Ensure this is not immediately followed by an opening paren before caret
            return new FormulaContextResult(
                IsTypingFunctionName: true,
                FunctionPrefix: prefix,
                PrefixStartIndex: startIndex,
                ActiveFunction: null,
                ActiveArgumentIndex: 0,
                ArgumentHint: null,
                MatchingParenIndex: null
            );
        }

        // 2. Check if user is inside function parentheses (e.g. "=SUM(1, 2|")
        var openParenStack = new Stack<(string funcName, int openIndex, int argCount)>();
        var i = 0;
        while (i < textUpToCaret.Length)
        {
            var c = textUpToCaret[i];
            if (c == '"')
            {
                // Skip string literals
                i++;
                while (i < textUpToCaret.Length && textUpToCaret[i] != '"') i++;
            }
            else if (c == '(')
            {
                // Find preceding identifier
                var beforeParen = textUpToCaret[..i];
                var idMatch = Regex.Match(beforeParen, @"([A-Za-z_][A-Za-z0-9_]*)$");
                var funcName = idMatch.Success ? idMatch.Value.ToUpperInvariant() : string.Empty;
                openParenStack.Push((funcName, i, 0));
            }
            else if (c == ')')
            {
                if (openParenStack.Count > 0) openParenStack.Pop();
            }
            else if (c == ',')
            {
                if (openParenStack.Count > 0)
                {
                    var top = openParenStack.Pop();
                    openParenStack.Push((top.funcName, top.openIndex, top.argCount + 1));
                }
            }
            i++;
        }

        if (openParenStack.Count > 0)
        {
            var currentCall = openParenStack.Peek();
            if (!string.IsNullOrEmpty(currentCall.funcName) && FunctionLookup.TryGetValue(currentCall.funcName, out var funcInfo))
            {
                return new FormulaContextResult(
                    IsTypingFunctionName: false,
                    FunctionPrefix: null,
                    PrefixStartIndex: 0,
                    ActiveFunction: funcInfo,
                    ActiveArgumentIndex: currentCall.argCount,
                    ArgumentHint: FormatArgumentHint(funcInfo, currentCall.argCount),
                    MatchingParenIndex: null
                );
            }
        }

        // 3. Check for parenthesis matching at caret
        int? matchIndex = null;
        if (caretIndex > 0 && caretIndex <= formulaText.Length)
        {
            matchIndex = FindMatchingParenthesis(formulaText, caretIndex);
        }

        return new FormulaContextResult(
            IsTypingFunctionName: false,
            FunctionPrefix: null,
            PrefixStartIndex: 0,
            ActiveFunction: null,
            ActiveArgumentIndex: 0,
            ArgumentHint: null,
            MatchingParenIndex: matchIndex
        );
    }

    /// <summary>
    /// Formats the signature highlighting the current argument index.
    /// </summary>
    public static string FormatArgumentHint(FunctionInfo func, int currentArgIndex)
    {
        if (func.ParameterNames.Count == 0) return func.Syntax;

        var parts = new List<string>();
        for (var i = 0; i < func.ParameterNames.Count; i++)
        {
            var p = func.ParameterNames[i];
            if (i == currentArgIndex || (i == func.ParameterNames.Count - 1 && currentArgIndex >= i))
            {
                parts.Add($"▶ {p} ◀");
            }
            else
            {
                parts.Add(p);
            }
        }

        return $"{func.Name}( {string.Join(", ", parts)} ) — {func.Description}";
    }

    /// <summary>
    /// Finds the index of the matching parenthesis if caret is adjacent to '(' or ')'.
    /// </summary>
    public static int? FindMatchingParenthesis(string text, int caretIndex)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Check if character before caret is ')' or '('
        if (caretIndex > 0 && caretIndex <= text.Length)
        {
            var charBefore = text[caretIndex - 1];
            if (charBefore == ')')
            {
                // Scan backward for matching '('
                var depth = 0;
                for (var j = caretIndex - 1; j >= 0; j--)
                {
                    if (text[j] == ')') depth++;
                    else if (text[j] == '(')
                    {
                        depth--;
                        if (depth == 0) return j;
                    }
                }
            }
            else if (charBefore == '(')
            {
                // Scan forward for matching ')'
                var depth = 0;
                for (var j = caretIndex - 1; j < text.Length; j++)
                {
                    if (text[j] == '(') depth++;
                    else if (text[j] == ')')
                    {
                        depth--;
                        if (depth == 0) return j;
                    }
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Result of evaluating formula editor context at a given caret position.
/// </summary>
public record FormulaContextResult(
    bool IsTypingFunctionName,
    string? FunctionPrefix,
    int PrefixStartIndex,
    FunctionInfo? ActiveFunction,
    int ActiveArgumentIndex,
    string? ArgumentHint,
    int? MatchingParenIndex
);
