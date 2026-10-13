using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Core.Engine;

public abstract class FormulaNode
{
    public abstract CellValue Evaluate(EvaluationContext context);
    public abstract IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable);
}

public class NumberLiteralNode : FormulaNode
{
    public double Value { get; }
    public NumberLiteralNode(double value) => Value = value;
    public override CellValue Evaluate(EvaluationContext context) => new(Value);
    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable) => Enumerable.Empty<QualifiedCellAddress>();
}

public class StringLiteralNode : FormulaNode
{
    public string Value { get; }
    public StringLiteralNode(string value) => Value = value;
    public override CellValue Evaluate(EvaluationContext context) => new(Value);
    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable) => Enumerable.Empty<QualifiedCellAddress>();
}

public class BooleanLiteralNode : FormulaNode
{
    public bool Value { get; }
    public BooleanLiteralNode(bool value) => Value = value;
    public override CellValue Evaluate(EvaluationContext context) => new(Value);
    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable) => Enumerable.Empty<QualifiedCellAddress>();
}

public class CellReferenceNode : FormulaNode
{
    public string? TableName { get; }
    public CellCoordinate Coordinate { get; }

    public CellReferenceNode(string? tableName, CellCoordinate coordinate)
    {
        TableName = tableName;
        Coordinate = coordinate;
    }

    public override CellValue Evaluate(EvaluationContext context)
    {
        return context.GetCellValue(TableName, Coordinate);
    }

    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable)
    {
        var targetTable = string.IsNullOrWhiteSpace(TableName) ? defaultTable : TableName;
        yield return new QualifiedCellAddress(targetTable, Coordinate);
    }
}

public class RangeReferenceNode : FormulaNode
{
    public string? TableName { get; }
    public CellRange Range { get; }

    public RangeReferenceNode(string? tableName, CellRange range)
    {
        TableName = tableName;
        Range = range;
    }

    public override CellValue Evaluate(EvaluationContext context)
    {
        // Evaluating a range directly in scalar context returns the top-left cell
        return context.GetCellValue(TableName, new CellCoordinate(Range.MinRow, Range.MinCol));
    }

    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable)
    {
        var targetTable = string.IsNullOrWhiteSpace(TableName) ? defaultTable : TableName;
        foreach (var coord in Range.Coordinates)
        {
            yield return new QualifiedCellAddress(targetTable, coord);
        }
    }
}

public class UnaryOpNode : FormulaNode
{
    public string Operator { get; }
    public FormulaNode Operand { get; }

    public UnaryOpNode(string op, FormulaNode operand)
    {
        Operator = op;
        Operand = operand;
    }

    public override CellValue Evaluate(EvaluationContext context)
    {
        var val = Operand.Evaluate(context);
        if (val.IsError) return val;

        if (Operator == "-")
        {
            return new CellValue(-val.AsNumber());
        }
        if (Operator == "+")
        {
            return new CellValue(val.AsNumber());
        }

        return CellValue.ValueErr;
    }

    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable) =>
        Operand.GetCellDependencies(defaultTable);
}

public class BinaryOpNode : FormulaNode
{
    public string Operator { get; }
    public FormulaNode Left { get; }
    public FormulaNode Right { get; }

    public BinaryOpNode(string op, FormulaNode left, FormulaNode right)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public override CellValue Evaluate(EvaluationContext context)
    {
        var leftVal = Left.Evaluate(context);
        if (leftVal.IsError) return leftVal;

        var rightVal = Right.Evaluate(context);
        if (rightVal.IsError) return rightVal;

        // String concatenation
        if (Operator == "&")
        {
            return new CellValue($"{leftVal}{rightVal}");
        }

        // Arithmetic
        if (Operator is "+" or "-" or "*" or "/" or "^")
        {
            var lNum = leftVal.AsNumber();
            var rNum = rightVal.AsNumber();

            return Operator switch
            {
                "+" => new CellValue(lNum + rNum),
                "-" => new CellValue(lNum - rNum),
                "*" => new CellValue(lNum * rNum),
                "/" => rNum == 0.0 ? CellValue.DivByZero : new CellValue(lNum / rNum),
                "^" => new CellValue(Math.Pow(lNum, rNum)),
                _ => CellValue.ValueErr
            };
        }

        // Comparison operators: =, <>, <, <=, >, >=
        return Operator switch
        {
            "=" => new CellValue(CompareValues(leftVal, rightVal) == 0),
            "<>" => new CellValue(CompareValues(leftVal, rightVal) != 0),
            "<" => new CellValue(CompareValues(leftVal, rightVal) < 0),
            "<=" => new CellValue(CompareValues(leftVal, rightVal) <= 0),
            ">" => new CellValue(CompareValues(leftVal, rightVal) > 0),
            ">=" => new CellValue(CompareValues(leftVal, rightVal) >= 0),
            _ => CellValue.ValueErr
        };
    }

    private static int CompareValues(CellValue left, CellValue right)
    {
        if (left.TryGetNumber(out var lNum) && right.TryGetNumber(out var rNum))
        {
            return lNum.CompareTo(rNum);
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable)
    {
        return Left.GetCellDependencies(defaultTable).Concat(Right.GetCellDependencies(defaultTable));
    }
}

public class FunctionCallNode : FormulaNode
{
    public string FunctionName { get; }
    public List<FormulaNode> Arguments { get; }

    public FunctionCallNode(string functionName, List<FormulaNode> arguments)
    {
        FunctionName = functionName.ToUpperInvariant();
        Arguments = arguments;
    }

    public override CellValue Evaluate(EvaluationContext context)
    {
        return BuiltInFunctions.Evaluate(FunctionName, Arguments, context);
    }

    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable)
    {
        foreach (var arg in Arguments)
        {
            foreach (var dep in arg.GetCellDependencies(defaultTable))
            {
                yield return dep;
            }
        }
    }
}

public class CSharpScriptNode : FormulaNode
{
    public string ScriptCode { get; }

    public CSharpScriptNode(string scriptCode)
    {
        ScriptCode = scriptCode;
    }

    public override CellValue Evaluate(EvaluationContext context)
    {
        if (context.ScriptEngine == null)
        {
            return CellValue.Error("#NOSCRIPT!");
        }

        return context.ScriptEngine.ExecuteScript(ScriptCode, context);
    }

    public override IEnumerable<QualifiedCellAddress> GetCellDependencies(string defaultTable)
    {
        // C# scripts can dynamically reference ranges, but can also be tracked if needed.
        yield break;
    }
}
