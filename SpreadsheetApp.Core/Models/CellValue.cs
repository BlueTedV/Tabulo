using System.Globalization;

namespace SpreadsheetApp.Core.Models;

public enum CellValueType
{
    Empty,
    Number,
    Text,
    Boolean,
    DateTime,
    Error,
    Object
}

/// <summary>
/// Immutable representation of an evaluated cell result.
/// </summary>
public readonly record struct CellValue
{
    public CellValueType Type { get; }
    public double NumberValue { get; }
    public string? TextValue { get; }
    public bool BoolValue { get; }
    public DateTime? DateValue { get; }
    public object? ObjectValue { get; }

    public static readonly CellValue Empty = new(CellValueType.Empty);

    public CellValue(double number)
    {
        Type = CellValueType.Number;
        NumberValue = number;
        TextValue = null;
        BoolValue = false;
        DateValue = null;
        ObjectValue = null;
    }

    public CellValue(string text)
    {
        Type = CellValueType.Text;
        NumberValue = 0;
        TextValue = text;
        BoolValue = false;
        DateValue = null;
        ObjectValue = null;
    }

    public CellValue(bool boolean)
    {
        Type = CellValueType.Boolean;
        NumberValue = boolean ? 1.0 : 0.0;
        TextValue = null;
        BoolValue = boolean;
        DateValue = null;
        ObjectValue = null;
    }

    public CellValue(DateTime date)
    {
        Type = CellValueType.DateTime;
        NumberValue = date.ToOADate();
        TextValue = null;
        BoolValue = false;
        DateValue = date;
        ObjectValue = null;
    }

    private CellValue(CellValueType type, string? errorText = null)
    {
        Type = type;
        NumberValue = 0;
        TextValue = errorText;
        BoolValue = false;
        DateValue = null;
        ObjectValue = null;
    }

    public CellValue(object? obj)
    {
        if (obj == null)
        {
            Type = CellValueType.Empty;
            return;
        }

        switch (obj)
        {
            case CellValue cv:
                this = cv;
                return;
            case double d:
                Type = CellValueType.Number;
                NumberValue = d;
                break;
            case float f:
                Type = CellValueType.Number;
                NumberValue = f;
                break;
            case int i:
                Type = CellValueType.Number;
                NumberValue = i;
                break;
            case long l:
                Type = CellValueType.Number;
                NumberValue = l;
                break;
            case decimal m:
                Type = CellValueType.Number;
                NumberValue = (double)m;
                break;
            case bool b:
                Type = CellValueType.Boolean;
                BoolValue = b;
                NumberValue = b ? 1.0 : 0.0;
                break;
            case DateTime dt:
                Type = CellValueType.DateTime;
                DateValue = dt;
                NumberValue = dt.ToOADate();
                break;
            case string s:
                Type = CellValueType.Text;
                TextValue = s;
                break;
            default:
                Type = CellValueType.Object;
                ObjectValue = obj;
                break;
        }
    }

    public static CellValue Error(string error) => new(CellValueType.Error, error);

    public static CellValue DivByZero => Error("#DIV/0!");
    public static CellValue ValueErr => Error("#VALUE!");
    public static CellValue RefErr => Error("#REF!");
    public static CellValue NameErr => Error("#NAME?");
    public static CellValue CircularErr => Error("#CIRCULAR!");
    public static CellValue SyntaxErr => Error("#SYNTAX!");
    public static CellValue NotAvailable => Error("#N/A");

    public bool IsError => Type == CellValueType.Error;
    public bool IsNumber => Type == CellValueType.Number;
    public bool IsEmpty => Type == CellValueType.Empty;

    public double AsNumber()
    {
        return Type switch
        {
            CellValueType.Number => NumberValue,
            CellValueType.Boolean => BoolValue ? 1.0 : 0.0,
            CellValueType.DateTime => NumberValue,
            CellValueType.Text => double.TryParse(TextValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0.0,
            _ => 0.0
        };
    }

    public bool TryGetNumber(out double number)
    {
        if (Type == CellValueType.Number)
        {
            number = NumberValue;
            return true;
        }
        if (Type == CellValueType.Boolean)
        {
            number = BoolValue ? 1.0 : 0.0;
            return true;
        }
        if (Type == CellValueType.Text && double.TryParse(TextValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            number = parsed;
            return true;
        }
        number = 0;
        return false;
    }

    public override string ToString()
    {
        return Type switch
        {
            CellValueType.Empty => string.Empty,
            CellValueType.Number => NumberValue.ToString("G15", CultureInfo.InvariantCulture),
            CellValueType.Text => TextValue ?? string.Empty,
            CellValueType.Boolean => BoolValue ? "TRUE" : "FALSE",
            CellValueType.DateTime => DateValue?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            CellValueType.Error => TextValue ?? "#ERROR!",
            CellValueType.Object => ObjectValue?.ToString() ?? string.Empty,
            _ => string.Empty
        };
    }

    public string Format(string? format, IFormatProvider? provider = null)
    {
        if (string.IsNullOrWhiteSpace(format) || string.Equals(format, "General", StringComparison.OrdinalIgnoreCase))
        {
            return ToString();
        }

        provider ??= CultureInfo.InvariantCulture;

        if (Type == CellValueType.Number)
        {
            var upper = format.Trim().ToUpperInvariant();
            try
            {
                if (upper is "C" or "C0" or "C2" or "CURRENCY")
                {
                    var decimals = upper == "C0" ? 0 : 2;
                    // Standard currency formatting with $ sign
                    return NumberValue.ToString($"$#,##0.{new string('0', decimals)}", provider).TrimEnd('.');
                }
                if (upper is "P" or "P0" or "P1" or "P2" or "PERCENT")
                {
                    var decimals = upper switch { "P0" => 0, "P1" => 1, _ => 2 };
                    return (NumberValue * 100.0).ToString($"#,##0.{new string('0', decimals)}", provider).TrimEnd('.') + "%";
                }
                if (upper is "N" or "N0" or "N2")
                {
                    var decimals = upper == "N0" ? 0 : 2;
                    return NumberValue.ToString($"#,##0.{new string('0', decimals)}", provider).TrimEnd('.');
                }

                return NumberValue.ToString(format, provider);
            }
            catch
            {
                return ToString();
            }
        }

        if (Type == CellValueType.DateTime && DateValue.HasValue)
        {
            try
            {
                return DateValue.Value.ToString(format, provider);
            }
            catch
            {
                return ToString();
            }
        }

        return ToString();
    }
}
