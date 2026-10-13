using Tabulo.Core.Models;

namespace Tabulo.Core.Engine;

/// <summary>
/// Abstraction for executing C# Roslyn or custom script expressions inside formulas.
/// </summary>
public interface IScriptEngine
{
    CellValue ExecuteScript(string code, EvaluationContext context);
}
