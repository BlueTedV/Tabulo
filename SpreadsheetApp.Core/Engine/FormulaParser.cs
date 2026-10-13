using System.Globalization;
using SpreadsheetApp.Core.Models;

namespace SpreadsheetApp.Core.Engine;

/// <summary>
/// Recursive descent parser producing an AST from a tokenized formula.
/// </summary>
public class FormulaParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public FormulaParser(List<Token> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    public static FormulaNode Parse(string formula)
    {
        var lexer = new FormulaLexer(formula);
        var tokens = lexer.Tokenize();
        var parser = new FormulaParser(tokens);
        return parser.ParseRoot();
    }

    public FormulaNode ParseRoot()
    {
        if (Current.Type == TokenType.CSharpScript)
        {
            var code = Current.Value;
            _pos++;
            return new CSharpScriptNode(code);
        }

        var expr = ParseComparison();
        return expr;
    }

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

    private Token Advance()
    {
        var tok = Current;
        if (_pos < _tokens.Count) _pos++;
        return tok;
    }

    private bool Match(TokenType type, string? value = null)
    {
        if (Current.Type != type) return false;
        if (value != null && !string.Equals(Current.Value, value, StringComparison.OrdinalIgnoreCase)) return false;
        _pos++;
        return true;
    }

    private Token Expect(TokenType type, string? message = null)
    {
        if (Current.Type != type)
        {
            throw new FormatException(message ?? $"Expected token '{type}', found '{Current.Type}' at position {Current.Position}.");
        }
        return Advance();
    }

    // Comparison: =, <>, <, <=, >, >=
    private FormulaNode ParseComparison()
    {
        var left = ParseConcatenation();

        while (Current.Type == TokenType.Operator &&
               Current.Value is "=" or "<>" or "<" or "<=" or ">" or ">=")
        {
            var op = Advance().Value;
            var right = ParseConcatenation();
            left = new BinaryOpNode(op, left, right);
        }

        return left;
    }

    // String Concatenation: &
    private FormulaNode ParseConcatenation()
    {
        var left = ParseAdditive();

        while (Current.Type == TokenType.Operator && Current.Value == "&")
        {
            var op = Advance().Value;
            var right = ParseAdditive();
            left = new BinaryOpNode(op, left, right);
        }

        return left;
    }

    // Additive: +, -
    private FormulaNode ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (Current.Type == TokenType.Operator && Current.Value is "+" or "-")
        {
            var op = Advance().Value;
            var right = ParseMultiplicative();
            left = new BinaryOpNode(op, left, right);
        }

        return left;
    }

    // Multiplicative: *, /
    private FormulaNode ParseMultiplicative()
    {
        var left = ParseExponent();

        while (Current.Type == TokenType.Operator && Current.Value is "*" or "/")
        {
            var op = Advance().Value;
            var right = ParseExponent();
            left = new BinaryOpNode(op, left, right);
        }

        return left;
    }

    // Exponent: ^
    private FormulaNode ParseExponent()
    {
        var left = ParseUnary();

        while (Current.Type == TokenType.Operator && Current.Value == "^")
        {
            var op = Advance().Value;
            var right = ParseUnary();
            left = new BinaryOpNode(op, left, right);
        }

        return left;
    }

    // Unary: +, -
    private FormulaNode ParseUnary()
    {
        if (Current.Type == TokenType.Operator && Current.Value is "+" or "-")
        {
            var op = Advance().Value;
            var operand = ParseUnary();
            return new UnaryOpNode(op, operand);
        }

        return ParsePrimary();
    }

    // Primary: Literals, Parentheses, Functions, Cell/Range References
    private FormulaNode ParsePrimary()
    {
        if (Current.Type == TokenType.Number)
        {
            var numStr = Advance().Value;
            var num = double.Parse(numStr, CultureInfo.InvariantCulture);
            return new NumberLiteralNode(num);
        }

        if (Current.Type == TokenType.String)
        {
            var str = Advance().Value;
            return new StringLiteralNode(str);
        }

        if (Current.Type == TokenType.OpenParen)
        {
            Advance(); // Skip (
            var inner = ParseComparison();
            Expect(TokenType.CloseParen, "Expected closing parenthesis ')'");
            return inner;
        }

        if (Current.Type == TokenType.Identifier)
        {
            var ident = Advance().Value;

            if (string.Equals(ident, "TRUE", StringComparison.OrdinalIgnoreCase))
            {
                return new BooleanLiteralNode(true);
            }
            if (string.Equals(ident, "FALSE", StringComparison.OrdinalIgnoreCase))
            {
                return new BooleanLiteralNode(false);
            }

            // Function Call: IDENT(arg1, arg2, ...)
            if (Current.Type == TokenType.OpenParen)
            {
                Advance(); // Skip (
                var args = new List<FormulaNode>();

                if (Current.Type != TokenType.CloseParen)
                {
                    while (true)
                    {
                        args.Add(ParseComparison());
                        if (Current.Type == TokenType.Comma)
                        {
                            Advance();
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                Expect(TokenType.CloseParen, $"Expected closing parenthesis after '{ident}' arguments.");
                return new FunctionCallNode(ident, args);
            }

            // Table-scoped reference: TableName!A1 or TableName!A1:B10
            if (Current.Type == TokenType.Exclamation)
            {
                Advance(); // Skip !
                var cellToken = Expect(TokenType.Identifier, "Expected cell coordinate after '!'");
                var startCoord = CellCoordinate.FromA1(cellToken.Value);

                if (Current.Type == TokenType.Colon)
                {
                    Advance(); // Skip :
                    var endToken = Expect(TokenType.Identifier, "Expected end cell coordinate after ':'");
                    var endCoord = CellCoordinate.FromA1(endToken.Value);
                    return new RangeReferenceNode(ident, new CellRange(startCoord, endCoord));
                }

                return new CellReferenceNode(ident, startCoord);
            }

            // Local Cell or Range reference: A1 or A1:B10
            if (CellCoordinate.TryFromA1(ident, out var coord))
            {
                if (Current.Type == TokenType.Colon)
                {
                    Advance(); // Skip :
                    var endToken = Expect(TokenType.Identifier, "Expected end cell coordinate after ':' in range expression.");
                    var endCoord = CellCoordinate.FromA1(endToken.Value);
                    return new RangeReferenceNode(null, new CellRange(coord, endCoord));
                }

                return new CellReferenceNode(null, coord);
            }

            throw new FormatException($"Unknown identifier or reference '{ident}'.");
        }

        throw new FormatException($"Unexpected token '{Current.Type}' ({Current.Value}) at position {Current.Position}.");
    }
}
