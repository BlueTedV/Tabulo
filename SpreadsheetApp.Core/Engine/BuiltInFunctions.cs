using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Core.Engine;

/// <summary>
/// Implements standard Excel mathematical, statistical, logic, text, and lookup functions.
/// </summary>
public static class BuiltInFunctions
{
    public static CellValue Evaluate(string functionName, List<FormulaNode> args, EvaluationContext context)
    {
        return functionName switch
        {
            "SUM" => Sum(args, context),
            "AVERAGE" => Average(args, context),
            "COUNT" => Count(args, context),
            "COUNTA" => CountA(args, context),
            "MIN" => Min(args, context),
            "MAX" => Max(args, context),
            "PRODUCT" => Product(args, context),
            "IF" => If(args, context),
            "AND" => And(args, context),
            "OR" => Or(args, context),
            "NOT" => Not(args, context),
            "ABS" => Abs(args, context),
            "ROUND" => Round(args, context),
            "SQRT" => Sqrt(args, context),
            "POWER" => Power(args, context),
            "CONCATENATE" or "CONCAT" => Concat(args, context),
            "LEFT" => Left(args, context),
            "RIGHT" => Right(args, context),
            "MID" => Mid(args, context),
            "LEN" => Len(args, context),
            "TRIM" => Trim(args, context),
            "UPPER" => Upper(args, context),
            "LOWER" => Lower(args, context),
            "INDEX" => Index(args, context),
            _ => CellValue.NameErr
        };
    }

    private static IEnumerable<CellValue> FlattenArguments(List<FormulaNode> args, EvaluationContext context)
    {
        foreach (var arg in args)
        {
            if (arg is RangeReferenceNode rangeNode)
            {
                foreach (var val in context.GetRangeValues(rangeNode.TableName, rangeNode.Range))
                {
                    yield return val;
                }
            }
            else
            {
                yield return arg.Evaluate(context);
            }
        }
    }

    private static CellValue Sum(List<FormulaNode> args, EvaluationContext context)
    {
        var total = 0.0;
        foreach (var val in FlattenArguments(args, context))
        {
            if (val.IsError) return val;
            if (val.TryGetNumber(out var num))
            {
                total += num;
            }
        }
        return new CellValue(total);
    }

    private static CellValue Product(List<FormulaNode> args, EvaluationContext context)
    {
        var prod = 1.0;
        var hasValues = false;
        foreach (var val in FlattenArguments(args, context))
        {
            if (val.IsError) return val;
            if (val.TryGetNumber(out var num))
            {
                prod *= num;
                hasValues = true;
            }
        }
        return hasValues ? new CellValue(prod) : new CellValue(0.0);
    }

    private static CellValue Average(List<FormulaNode> args, EvaluationContext context)
    {
        var total = 0.0;
        var count = 0;
        foreach (var val in FlattenArguments(args, context))
        {
            if (val.IsError) return val;
            if (val.TryGetNumber(out var num))
            {
                total += num;
                count++;
            }
        }
        return count == 0 ? CellValue.DivByZero : new CellValue(total / count);
    }

    private static CellValue Count(List<FormulaNode> args, EvaluationContext context)
    {
        var count = 0;
        foreach (var val in FlattenArguments(args, context))
        {
            if (val.Type is CellValueType.Number or CellValueType.DateTime)
            {
                count++;
            }
        }
        return new CellValue(count);
    }

    private static CellValue CountA(List<FormulaNode> args, EvaluationContext context)
    {
        var count = 0;
        foreach (var val in FlattenArguments(args, context))
        {
            if (!val.IsEmpty) count++;
        }
        return new CellValue(count);
    }

    private static CellValue Min(List<FormulaNode> args, EvaluationContext context)
    {
        var min = double.PositiveInfinity;
        var found = false;
        foreach (var val in FlattenArguments(args, context))
        {
            if (val.IsError) return val;
            if (val.TryGetNumber(out var num))
            {
                if (num < min) min = num;
                found = true;
            }
        }
        return found ? new CellValue(min) : new CellValue(0.0);
    }

    private static CellValue Max(List<FormulaNode> args, EvaluationContext context)
    {
        var max = double.NegativeInfinity;
        var found = false;
        foreach (var val in FlattenArguments(args, context))
        {
            if (val.IsError) return val;
            if (val.TryGetNumber(out var num))
            {
                if (num > max) max = num;
                found = true;
            }
        }
        return found ? new CellValue(max) : new CellValue(0.0);
    }

    private static CellValue If(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count < 2) return CellValue.ValueErr;

        var conditionVal = args[0].Evaluate(context);
        if (conditionVal.IsError) return conditionVal;

        var isTrue = conditionVal.Type switch
        {
            CellValueType.Boolean => conditionVal.BoolValue,
            CellValueType.Number => conditionVal.NumberValue != 0.0,
            CellValueType.Text => !string.IsNullOrEmpty(conditionVal.TextValue),
            _ => false
        };

        if (isTrue)
        {
            return args[1].Evaluate(context);
        }

        if (args.Count >= 3)
        {
            return args[2].Evaluate(context);
        }

        return new CellValue(false);
    }

    private static CellValue And(List<FormulaNode> args, EvaluationContext context)
    {
        foreach (var arg in args)
        {
            var val = arg.Evaluate(context);
            if (val.IsError) return val;
            if (val.AsNumber() == 0.0 && !val.BoolValue) return new CellValue(false);
        }
        return new CellValue(true);
    }

    private static CellValue Or(List<FormulaNode> args, EvaluationContext context)
    {
        foreach (var arg in args)
        {
            var val = arg.Evaluate(context);
            if (val.IsError) return val;
            if (val.BoolValue || val.AsNumber() != 0.0) return new CellValue(true);
        }
        return new CellValue(false);
    }

    private static CellValue Not(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var val = args[0].Evaluate(context);
        if (val.IsError) return val;
        var b = val.BoolValue || val.AsNumber() != 0.0;
        return new CellValue(!b);
    }

    private static CellValue Abs(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var val = args[0].Evaluate(context);
        if (val.IsError) return val;
        return new CellValue(Math.Abs(val.AsNumber()));
    }

    private static CellValue Round(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var val = args[0].Evaluate(context);
        if (val.IsError) return val;

        var digits = 0;
        if (args.Count >= 2)
        {
            digits = (int)args[1].Evaluate(context).AsNumber();
        }

        return new CellValue(Math.Round(val.AsNumber(), digits, MidpointRounding.AwayFromZero));
    }

    private static CellValue Sqrt(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var val = args[0].Evaluate(context);
        if (val.IsError) return val;
        var num = val.AsNumber();
        if (num < 0) return CellValue.ValueErr;
        return new CellValue(Math.Sqrt(num));
    }

    private static CellValue Power(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count < 2) return CellValue.ValueErr;
        var b = args[0].Evaluate(context).AsNumber();
        var exp = args[1].Evaluate(context).AsNumber();
        return new CellValue(Math.Pow(b, exp));
    }

    private static CellValue Concat(List<FormulaNode> args, EvaluationContext context)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var val in FlattenArguments(args, context))
        {
            sb.Append(val.ToString());
        }
        return new CellValue(sb.ToString());
    }

    private static CellValue Left(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var text = args[0].Evaluate(context).ToString();
        var count = args.Count >= 2 ? (int)args[1].Evaluate(context).AsNumber() : 1;
        count = Math.Max(0, Math.Min(count, text.Length));
        return new CellValue(text[..count]);
    }

    private static CellValue Right(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var text = args[0].Evaluate(context).ToString();
        var count = args.Count >= 2 ? (int)args[1].Evaluate(context).AsNumber() : 1;
        count = Math.Max(0, Math.Min(count, text.Length));
        return new CellValue(text.Substring(text.Length - count, count));
    }

    private static CellValue Mid(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count < 3) return CellValue.ValueErr;
        var text = args[0].Evaluate(context).ToString();
        var start = (int)args[1].Evaluate(context).AsNumber() - 1; // 1-based in Excel
        var length = (int)args[2].Evaluate(context).AsNumber();

        if (start < 0 || start >= text.Length) return new CellValue(string.Empty);
        length = Math.Max(0, Math.Min(length, text.Length - start));
        return new CellValue(text.Substring(start, length));
    }

    private static CellValue Len(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var text = args[0].Evaluate(context).ToString();
        return new CellValue(text.Length);
    }

    private static CellValue Trim(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var text = args[0].Evaluate(context).ToString();
        return new CellValue(text.Trim());
    }

    private static CellValue Upper(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var text = args[0].Evaluate(context).ToString();
        return new CellValue(text.ToUpperInvariant());
    }

    private static CellValue Lower(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count == 0) return CellValue.ValueErr;
        var text = args[0].Evaluate(context).ToString();
        return new CellValue(text.ToLowerInvariant());
    }

    private static CellValue Index(List<FormulaNode> args, EvaluationContext context)
    {
        if (args.Count < 2 || args[0] is not RangeReferenceNode rangeNode)
        {
            return CellValue.ValueErr;
        }

        var rowIdx = (int)args[1].Evaluate(context).AsNumber() - 1; // 1-based
        var colIdx = args.Count >= 3 ? (int)args[2].Evaluate(context).AsNumber() - 1 : 0;

        var targetRow = rangeNode.Range.MinRow + rowIdx;
        var targetCol = rangeNode.Range.MinCol + colIdx;

        return context.GetCellValue(rangeNode.TableName, new CellCoordinate(targetRow, targetCol));
    }
}
