using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using SpreadsheetApp.Core.Engine;
using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Scripting;

/// <summary>
/// High-performance Roslyn scripting host with delegate compilation caching and execution timeouts.
/// </summary>
public class CSharpScriptHost : IScriptEngine
{
    private readonly ScriptOptions _options;
    private readonly ConcurrentDictionary<string, ScriptRunner<object>> _delegateCache = new();
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(2);

    public CSharpScriptHost()
    {
        _options = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(System.Linq.Enumerable).Assembly,
                typeof(System.Math).Assembly,
                typeof(CellValue).Assembly,
                typeof(ScriptGlobals).Assembly)
            .WithImports(
                "System",
                "System.Math",
                "System.Linq",
                "System.Collections.Generic",
                "SpreadsheetApp.Core.Models",
                "SpreadsheetApp.Scripting");
    }

    public CellValue ExecuteScript(string code, EvaluationContext context)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return CellValue.Empty;
        }

        try
        {
            var runner = _delegateCache.GetOrAdd(code, c =>
            {
                var script = CSharpScript.Create<object>(c, _options, typeof(ScriptGlobals));
                return script.CreateDelegate();
            });

            using var cts = new CancellationTokenSource(ExecutionTimeout);
            var globals = new ScriptGlobals(context);

            var task = runner(globals, cts.Token);
            var result = task.GetAwaiter().GetResult();

            return result switch
            {
                null => CellValue.Empty,
                CellValue cv => cv,
                double d => new CellValue(d),
                int i => new CellValue(i),
                float f => new CellValue(f),
                decimal m => new CellValue((double)m),
                bool b => new CellValue(b),
                string s => new CellValue(s),
                DateTime dt => new CellValue(dt),
                _ => new CellValue(result?.ToString() ?? string.Empty)
            };
        }
        catch (OperationCanceledException)
        {
            return CellValue.Error("#TIMEOUT!");
        }
        catch (CompilationErrorException cex)
        {
            var msg = string.Join("; ", cex.Diagnostics.Select(d => d.GetMessage()));
            return CellValue.Error($"#CS_COMPILATION! {msg}");
        }
        catch (Exception ex)
        {
            return CellValue.Error($"#CS_ERR! {ex.Message}");
        }
    }
}
