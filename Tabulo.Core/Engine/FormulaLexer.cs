using System.Globalization;
using System.Text;

namespace Tabulo.Core.Engine;

public enum TokenType
{
    Number,
    String,
    Identifier,
    Operator,
    OpenParen,
    CloseParen,
    Comma,
    Colon,
    Exclamation,
    CSharpScript,
    EndOfFile
}

public record Token(TokenType Type, string Value, int Position);

public class FormulaLexer
{
    private readonly string _input;
    private int _pos;

    public FormulaLexer(string input)
    {
        _input = input?.TrimStart('=') ?? string.Empty;
        _pos = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        // Check if formula is a C# script: starts with CS(
        if (_input.TrimStart().StartsWith("CS(", StringComparison.OrdinalIgnoreCase))
        {
            var raw = _input.Trim();
            var openParenIdx = raw.IndexOf('(');
            var lastParenIdx = raw.LastIndexOf(')');
            if (openParenIdx >= 0 && lastParenIdx > openParenIdx)
            {
                var scriptBody = raw.Substring(openParenIdx + 1, lastParenIdx - openParenIdx - 1);
                tokens.Add(new Token(TokenType.CSharpScript, scriptBody, 0));
                tokens.Add(new Token(TokenType.EndOfFile, string.Empty, _input.Length));
                return tokens;
            }
        }

        while (_pos < _input.Length)
        {
            var ch = _input[_pos];

            if (char.IsWhiteSpace(ch))
            {
                _pos++;
                continue;
            }

            if (char.IsDigit(ch) || (ch == '.' && _pos + 1 < _input.Length && char.IsDigit(_input[_pos + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            if (ch == '"')
            {
                tokens.Add(ReadString());
                continue;
            }

            if (char.IsLetter(ch) || ch == '_' || ch == '$')
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            // Punctuation & operators
            var startPos = _pos;
            switch (ch)
            {
                case '(':
                    _pos++;
                    tokens.Add(new Token(TokenType.OpenParen, "(", startPos));
                    break;
                case ')':
                    _pos++;
                    tokens.Add(new Token(TokenType.CloseParen, ")", startPos));
                    break;
                case ',':
                    _pos++;
                    tokens.Add(new Token(TokenType.Comma, ",", startPos));
                    break;
                case ':':
                    _pos++;
                    tokens.Add(new Token(TokenType.Colon, ":", startPos));
                    break;
                case '!':
                    _pos++;
                    tokens.Add(new Token(TokenType.Exclamation, "!", startPos));
                    break;
                case '+':
                case '-':
                case '*':
                case '/':
                case '^':
                case '&':
                    _pos++;
                    tokens.Add(new Token(TokenType.Operator, ch.ToString(), startPos));
                    break;
                case '=':
                    _pos++;
                    tokens.Add(new Token(TokenType.Operator, "=", startPos));
                    break;
                case '<':
                    _pos++;
                    if (_pos < _input.Length && _input[_pos] == '=')
                    {
                        _pos++;
                        tokens.Add(new Token(TokenType.Operator, "<=", startPos));
                    }
                    else if (_pos < _input.Length && _input[_pos] == '>')
                    {
                        _pos++;
                        tokens.Add(new Token(TokenType.Operator, "<>", startPos));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Operator, "<", startPos));
                    }
                    break;
                case '>':
                    _pos++;
                    if (_pos < _input.Length && _input[_pos] == '=')
                    {
                        _pos++;
                        tokens.Add(new Token(TokenType.Operator, ">=", startPos));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Operator, ">", startPos));
                    }
                    break;
                default:
                    _pos++;
                    break;
            }
        }

        tokens.Add(new Token(TokenType.EndOfFile, string.Empty, _pos));
        return tokens;
    }

    private Token ReadNumber()
    {
        var start = _pos;
        var hasDecimal = false;

        while (_pos < _input.Length)
        {
            var c = _input[_pos];
            if (char.IsDigit(c))
            {
                _pos++;
            }
            else if (c == '.' && !hasDecimal)
            {
                hasDecimal = true;
                _pos++;
            }
            else
            {
                break;
            }
        }

        return new Token(TokenType.Number, _input.Substring(start, _pos - start), start);
    }

    private Token ReadString()
    {
        var start = _pos;
        _pos++; // Skip opening quote
        var sb = new StringBuilder();

        while (_pos < _input.Length)
        {
            var c = _input[_pos];
            if (c == '"')
            {
                if (_pos + 1 < _input.Length && _input[_pos + 1] == '"')
                {
                    sb.Append('"');
                    _pos += 2;
                }
                else
                {
                    _pos++; // Skip closing quote
                    break;
                }
            }
            else
            {
                sb.Append(c);
                _pos++;
            }
        }

        return new Token(TokenType.String, sb.ToString(), start);
    }

    private Token ReadIdentifier()
    {
        var start = _pos;
        while (_pos < _input.Length)
        {
            var c = _input[_pos];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '$')
            {
                _pos++;
            }
            else
            {
                break;
            }
        }

        return new Token(TokenType.Identifier, _input.Substring(start, _pos - start), start);
    }
}
