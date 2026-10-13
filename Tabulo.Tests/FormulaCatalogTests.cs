using Tabulo.Core.Engine;
using Xunit;

namespace Tabulo.Tests;

public class FormulaCatalogTests
{
    [Fact]
    public void GetSuggestions_ReturnsMatchingFunctions()
    {
        var suggestions = FunctionCatalog.GetSuggestions("S");
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Name == "SUM");
        Assert.Contains(suggestions, s => s.Name == "SQRT");

        var avgSuggestions = FunctionCatalog.GetSuggestions("AV");
        Assert.Single(avgSuggestions);
        Assert.Equal("AVERAGE", avgSuggestions[0].Name);
    }

    [Fact]
    public void ParseContext_DetectsFunctionPrefix()
    {
        var context = FunctionCatalog.ParseContext("=SU", 3);
        Assert.True(context.IsTypingFunctionName);
        Assert.Equal("SU", context.FunctionPrefix);
        Assert.Equal(1, context.PrefixStartIndex);
    }

    [Fact]
    public void ParseContext_DetectsActiveArgumentHint()
    {
        var context = FunctionCatalog.ParseContext("=SUM(A1, B2", 11);
        Assert.False(context.IsTypingFunctionName);
        Assert.NotNull(context.ActiveFunction);
        Assert.Equal("SUM", context.ActiveFunction.Name);
        Assert.Equal(1, context.ActiveArgumentIndex);
        Assert.Contains("▶ number2 ◀", context.ArgumentHint);
    }

    [Fact]
    public void FindMatchingParenthesis_LocatesPair()
    {
        var formula = "=SUM(1, 2)";
        var match = FunctionCatalog.FindMatchingParenthesis(formula, formula.Length);
        Assert.NotNull(match);
        Assert.Equal(4, match.Value);
    }

    [Fact]
    public void FormatReference_FormatsSingleAndRangeReferences()
    {
        var single = Tabulo.Core.Models.CellRange.FromSingle(new(0, 0));
        Assert.Equal("A1", FormulaReferenceHelper.FormatReference("Sales", "Sales", single));
        Assert.Equal("Sales!A1", FormulaReferenceHelper.FormatReference("Summary", "Sales", single));

        var range = new Tabulo.Core.Models.CellRange(0, 0, 4, 1);
        Assert.Equal("A1:B5", FormulaReferenceHelper.FormatReference("Sales", "Sales", range));
        Assert.Equal("Sales!A1:B5", FormulaReferenceHelper.FormatReference("Summary", "Sales", range));
    }

    [Fact]
    public void InsertOrUpdateReference_ReplacesInteractiveDrafts()
    {
        // 1. Initial insert after "=SUM("
        var (text1, caret1, len1) = FormulaReferenceHelper.InsertOrUpdateReference("=SUM(", 5, "A1", 0);
        Assert.Equal("=SUM(A1", text1);
        Assert.Equal(7, caret1);
        Assert.Equal(2, len1);

        // 2. Drag expands to A1:B5, replacing A1
        var (text2, caret2, len2) = FormulaReferenceHelper.InsertOrUpdateReference(text1, caret1, "A1:B5", len1);
        Assert.Equal("=SUM(A1:B5", text2);
        Assert.Equal(10, caret2);
        Assert.Equal(5, len2);
    }
}
