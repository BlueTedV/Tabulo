using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Core.Engine;

/// <summary>
/// Abstraction for executing C# Roslyn or custom script expressions inside formulas.
/// </summary>
public interface IScriptEngine
{
    CellValue ExecuteScript(string code, EvaluationContext context);
}
