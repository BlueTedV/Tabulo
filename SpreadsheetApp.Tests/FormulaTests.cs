using SpreadsheetApp.Core.Engine;
using SpreadsheetApp.Core.Models;
using Xunit;

namespace SpreadsheetApp.Tests;

public class FormulaTests
{
    private static CellValue Eval(string formula)
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("Sheet1");
        doc.AddTable(table);

        var ast = FormulaParser.Parse(formula);
        var ctx = new EvaluationContext(doc, "Sheet1");
        return ast.Evaluate(ctx);
    }

    [Fact]
    public void ArithmeticPrecedence_EvaluatesCorrectly()
    {
        Assert.Equal(7.0, Eval("=1 + 2 * 3").NumberValue);
        Assert.Equal(9.0, Eval("=(1 + 2) * 3").NumberValue);
        Assert.Equal(8.0, Eval("=2 ^ 3").NumberValue);
        Assert.Equal(2.5, Eval("=10 / 4").NumberValue);
        Assert.Equal(CellValue.DivByZero, Eval("=10 / 0"));
    }

    [Fact]
    public void StringConcatenation_EvaluatesCorrectly()
    {
        var result = Eval("=\"Hello \" & \"World\"");
        Assert.Equal("Hello World", result.TextValue);
    }

    [Fact]
    public void ComparisonOperators_EvaluateCorrectly()
    {
        Assert.True(Eval("=5 > 3").BoolValue);
        Assert.True(Eval("=10 <= 10").BoolValue);
        Assert.False(Eval("=\"apple\" = \"banana\"").BoolValue);
        Assert.True(Eval("=\"test\" = \"TEST\"").BoolValue);
    }

    [Fact]
    public void BuiltInFunctions_SumAndAverage()
    {
        var doc = new CanvasDocument();
        var table = new TableBlock("Sheet1");
        table.GetOrCreateCell(0, 0).Value = new CellValue(10);
        table.GetOrCreateCell(1, 0).Value = new CellValue(20);
        table.GetOrCreateCell(2, 0).Value = new CellValue(30);
        doc.AddTable(table);

        var sumAst = FormulaParser.Parse("=SUM(A1:A3)");
        var avgAst = FormulaParser.Parse("=AVERAGE(A1:A3)");

        var ctx = new EvaluationContext(doc, "Sheet1");
        Assert.Equal(60.0, sumAst.Evaluate(ctx).NumberValue);
        Assert.Equal(20.0, avgAst.Evaluate(ctx).NumberValue);
    }

    [Fact]
    public void BuiltInFunctions_IfCondition()
    {
        Assert.Equal(100.0, Eval("=IF(5 > 2, 100, 200)").NumberValue);
        Assert.Equal(200.0, Eval("=IF(1 = 2, 100, 200)").NumberValue);
    }

    [Fact]
    public void BuiltInFunctions_MinMaxRound()
    {
        Assert.Equal(5.0, Eval("=MIN(10, 20, 5, 40)").NumberValue);
        Assert.Equal(40.0, Eval("=MAX(10, 20, 5, 40)").NumberValue);
        Assert.Equal(3.14, Eval("=ROUND(3.14159, 2)").NumberValue);
        Assert.Equal(12.0, Eval("=SQRT(144)").NumberValue);
    }
}
