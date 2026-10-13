namespace SpreadsheetApp.Core.Models;

/// <summary>
/// Represents the data and presentation of a single cell.
/// </summary>
public class CellModel
{
    private string _rawInput = string.Empty;

    public string RawInput
    {
        get => _rawInput;
        set => _rawInput = value ?? string.Empty;
    }

    public CellValue Value { get; set; } = CellValue.Empty;

    public CellStyle Style { get; set; } = CellStyle.Default;

    public bool IsFormula => !string.IsNullOrWhiteSpace(_rawInput) && _rawInput.StartsWith('=');

    public CellModel() { }

    public CellModel(string rawInput)
    {
        RawInput = rawInput;
    }

    public CellModel(CellValue value)
    {
        Value = value;
        RawInput = value.ToString();
    }

    public CellModel Clone()
    {
        return new CellModel
        {
            RawInput = RawInput,
            Value = Value,
            Style = Style with { }
        };
    }

    public override string ToString() => Value.ToString();
}
