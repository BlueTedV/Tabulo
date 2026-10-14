using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Tabulo.Views;

public partial class ColorPickerDialog : Window
{
    private double _currentHue = 0.0;        // 0 to 360 degrees
    private double _currentSaturation = 1.0; // 0 to 1
    private double _currentValue = 1.0;      // 0 to 1

    private int _updatingDepth;
    private bool IsUpdatingInternally => _updatingDepth > 0;
    private bool _isDraggingSquare;

    public string SelectedHexColor { get; private set; } = "#000000";

    private static readonly string[] PresetColors =
    [
        "#000000", "#334155", "#475569", "#64748B", "#94A3B8", "#CBD5E1", "#F1F5F9", "#FFFFFF",
        "#DC2626", "#EF4444", "#F87171", "#FEE2E2", "#D97706", "#F59E0B", "#FBBF24", "#FEF3C7",
        "#16A34A", "#22C55E", "#4ADE80", "#DCFCE7", "#0D9488", "#14B8A6", "#2DD4BF", "#CCFBF1",
        "#2563EB", "#3B82F6", "#60A5FA", "#DBEAFE", "#7C3AED", "#8B5CF6", "#A78BFA", "#EDE9FE"
    ];

    public ColorPickerDialog(string? initialHex = null)
    {
        _updatingDepth++;
        try
        {
            InitializeComponent();
            PopulatePresets();

            var startColor = ParseHex(initialHex) ?? Colors.Black;
            if (CurrentColorBorder != null)
            {
                CurrentColorBorder.Background = new SolidColorBrush(startColor);
            }

            Loaded += (_, _) =>
            {
                SetColor(startColor, preserveHueIfAchromatic: false);
                UpdateReticlePosition();
            };

            SetColor(startColor, preserveHueIfAchromatic: false);
        }
        finally
        {
            _updatingDepth--;
        }
    }

    private void PopulatePresets()
    {
        if (PresetPaletteWrapPanel == null) return;

        foreach (var hex in PresetColors)
        {
            var btn = new Button
            {
                Width = 19,
                Height = 19,
                Margin = new Thickness(1),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Background = (SolidColorBrush?)new BrushConverter().ConvertFromString(hex) ?? Brushes.Black,
                BorderBrush = (SolidColorBrush?)new BrushConverter().ConvertFromString("#CBD5E1") ?? Brushes.Gray,
                BorderThickness = new Thickness(1),
                Tag = hex,
                ToolTip = hex
            };
            btn.Click += (_, _) =>
            {
                if (btn.Tag is string colorHex)
                {
                    var c = ParseHex(colorHex);
                    if (c.HasValue) SetColor(c.Value, preserveHueIfAchromatic: true);
                }
            };
            PresetPaletteWrapPanel.Children.Add(btn);
        }
    }

    public void SetColor(Color color, bool preserveHueIfAchromatic = true)
    {
        _updatingDepth++;
        try
        {
            RgbToHsv(color, out var h, out var s, out var v);

            // If color has little to no saturation or brightness, preserve existing hue so slider doesn't jump
            if (!preserveHueIfAchromatic || (s > 0.001 && v > 0.001))
            {
                _currentHue = h;
            }

            _currentSaturation = s;
            _currentValue = v;

            UpdateFromHsv(updateReticle: true, updateHueSlider: true, updateHexAndRgb: true);
        }
        finally
        {
            _updatingDepth--;
        }
    }

    private void UpdateFromHsv(bool updateReticle = true, bool updateHueSlider = true, bool updateHexAndRgb = true)
    {
        if (HueRectangle == null || NewColorBorder == null || HueSlider == null ||
            HexText == null || RedText == null || GreenText == null || BlueText == null ||
            ColorSquareGrid == null || ReticleThumb == null)
        {
            return;
        }

        var color = HsvToRgb(_currentHue, _currentSaturation, _currentValue);
        var pureHue = HsvToRgb(_currentHue, 1.0, 1.0);

        HueRectangle.Fill = new SolidColorBrush(pureHue);
        NewColorBorder.Background = new SolidColorBrush(color);

        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        SelectedHexColor = hex;

        if (updateReticle)
        {
            UpdateReticlePosition();
        }

        if (updateHueSlider && Math.Abs(HueSlider.Value - _currentHue) > 0.05)
        {
            HueSlider.Value = _currentHue;
        }

        if (updateHexAndRgb)
        {
            HexText.Text = hex;
            RedText.Text = color.R.ToString();
            GreenText.Text = color.G.ToString();
            BlueText.Text = color.B.ToString();
        }
    }

    private void UpdateReticlePosition()
    {
        if (ColorSquareGrid == null || ReticleThumb == null) return;
        var w = ColorSquareGrid.ActualWidth;
        var h = ColorSquareGrid.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var x = _currentSaturation * w;
        var y = (1.0 - _currentValue) * h;

        Canvas.SetLeft(ReticleThumb, x);
        Canvas.SetTop(ReticleThumb, y);
    }

    private void ColorSquare_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ColorSquareGrid == null) return;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDraggingSquare = true;
            ColorSquareGrid.CaptureMouse();
            UpdateSquareFromMouse(e.GetPosition(ColorSquareGrid));
        }
    }

    private void ColorSquare_MouseMove(object sender, MouseEventArgs e)
    {
        if (ColorSquareGrid == null) return;

        if (_isDraggingSquare && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSquareFromMouse(e.GetPosition(ColorSquareGrid));
        }
    }

    private void ColorSquare_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingSquare && ColorSquareGrid != null)
        {
            _isDraggingSquare = false;
            ColorSquareGrid.ReleaseMouseCapture();
        }
    }

    private void UpdateSquareFromMouse(Point pos)
    {
        if (ColorSquareGrid == null) return;
        var w = ColorSquareGrid.ActualWidth;
        var h = ColorSquareGrid.ActualHeight;
        if (w <= 0 || h <= 0) return;

        _currentSaturation = Math.Clamp(pos.X / w, 0.0, 1.0);
        _currentValue = Math.Clamp(1.0 - (pos.Y / h), 0.0, 1.0);

        _updatingDepth++;
        try
        {
            UpdateFromHsv(updateReticle: true, updateHueSlider: false, updateHexAndRgb: true);
        }
        finally
        {
            _updatingDepth--;
        }
    }

    private void ColorSquare_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateReticlePosition();
    }

    private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsUpdatingInternally || HueSlider == null) return;

        _currentHue = Math.Clamp(HueSlider.Value, 0.0, 360.0);

        _updatingDepth++;
        try
        {
            UpdateFromHsv(updateReticle: false, updateHueSlider: false, updateHexAndRgb: true);
        }
        finally
        {
            _updatingDepth--;
        }
    }

    private void HexText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsUpdatingInternally || HexText == null) return;

        var text = HexText.Text?.Trim();
        var c = ParseHex(text);
        if (c.HasValue)
        {
            SetColor(c.Value, preserveHueIfAchromatic: true);
        }
    }

    private void RgbText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsUpdatingInternally || RedText == null || GreenText == null || BlueText == null) return;

        if (byte.TryParse(RedText.Text, out var r) &&
            byte.TryParse(GreenText.Text, out var g) &&
            byte.TryParse(BlueText.Text, out var b))
        {
            SetColor(Color.FromRgb(r, g, b), preserveHueIfAchromatic: true);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static Color HsvToRgb(double h, double s, double v)
    {
        h = (h % 360.0 + 360.0) % 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        v = Math.Clamp(v, 0.0, 1.0);

        var c = v * s;
        var x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
        var m = v - c;

        double rPrime = 0, gPrime = 0, bPrime = 0;
        if (h < 60) { rPrime = c; gPrime = x; bPrime = 0; }
        else if (h < 120) { rPrime = x; gPrime = c; bPrime = 0; }
        else if (h < 180) { rPrime = 0; gPrime = c; bPrime = x; }
        else if (h < 240) { rPrime = 0; gPrime = x; bPrime = c; }
        else if (h < 300) { rPrime = x; gPrime = 0; bPrime = c; }
        else { rPrime = c; gPrime = 0; bPrime = x; }

        var r = (byte)Math.Clamp(Math.Round((rPrime + m) * 255.0), 0, 255);
        var g = (byte)Math.Clamp(Math.Round((gPrime + m) * 255.0), 0, 255);
        var b = (byte)Math.Clamp(Math.Round((bPrime + m) * 255.0), 0, 255);

        return Color.FromRgb(r, g, b);
    }

    public static void RgbToHsv(Color color, out double h, out double s, out double v)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        v = max;
        s = max == 0.0 ? 0.0 : delta / max;

        if (delta == 0.0)
        {
            h = 0.0;
        }
        else if (Math.Abs(max - r) < 0.0001)
        {
            h = 60.0 * (((g - b) / delta) % 6.0);
            if (h < 0.0) h += 360.0;
        }
        else if (Math.Abs(max - g) < 0.0001)
        {
            h = 60.0 * (((b - r) / delta) + 2.0);
        }
        else
        {
            h = 60.0 * (((r - g) / delta) + 4.0);
        }
    }

    public static Color? ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var clean = hex.Trim().TrimStart('#');

        if (clean.Length == 6)
        {
            if (byte.TryParse(clean[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(clean[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(clean[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return Color.FromRgb(r, g, b);
            }
        }
        else if (clean.Length == 3)
        {
            if (byte.TryParse($"{clean[0]}{clean[0]}", NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse($"{clean[1]}{clean[1]}", NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse($"{clean[2]}{clean[2]}", NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return Color.FromRgb(r, g, b);
            }
        }

        return null;
    }

    public static string? PickColor(Window owner, string? initialHex = null)
    {
        var dlg = new ColorPickerDialog(initialHex)
        {
            Owner = owner
        };
        return dlg.ShowDialog() == true ? dlg.SelectedHexColor : null;
    }
}
