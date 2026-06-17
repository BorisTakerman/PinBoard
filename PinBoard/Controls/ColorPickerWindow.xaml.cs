using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PinBoard.Controls;

/// <summary>
/// A small themed HSV color-wheel picker. Call <see cref="Pick"/> for a modal
/// dialog that returns the chosen color, or null if cancelled.
/// </summary>
public partial class ColorPickerWindow : Window
{
    private const int Size = 200;
    private const double Radius = 100;

    private double _hue;        // 0..360
    private double _sat;        // 0..1
    private double _val = 1;    // 0..1
    private bool _suppressHex;

    public Color SelectedColor { get; private set; }

    public ColorPickerWindow(Color initial)
    {
        InitializeComponent();
        (_hue, _sat, _val) = RgbToHsv(initial);
        Loaded += (_, _) =>
        {
            ValueSlider.Value = _val;
            RenderWheel();
            UpdateThumb();
            UpdatePreviewAndHex();
        };
    }

    /// <summary>Shows the picker modally; returns the chosen color or null if cancelled.</summary>
    public static Color? Pick(Window? owner, Color initial)
    {
        var win = new ColorPickerWindow(initial);
        if (owner is not null) win.Owner = owner;
        return win.ShowDialog() == true ? win.SelectedColor : null;
    }

    private void RenderWheel()
    {
        var bmp = new WriteableBitmap(Size, Size, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[Size * Size * 4];
        double cx = Size / 2.0, cy = Size / 2.0;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                double dx = x - cx, dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                int i = (y * Size + x) * 4;

                if (dist > Radius)
                    continue; // leave transparent

                double sat = dist / Radius;
                double hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
                var c = HsvToRgb(hue, sat, _val);

                double edge = Radius - dist;            // soft 1px antialiased rim
                byte a = (byte)(edge >= 1 ? 255 : Math.Clamp(edge, 0, 1) * 255);
                pixels[i + 0] = c.B;
                pixels[i + 1] = c.G;
                pixels[i + 2] = c.R;
                pixels[i + 3] = a;
            }
        }

        bmp.WritePixels(new Int32Rect(0, 0, Size, Size), pixels, Size * 4, 0);
        WheelImage.Source = bmp;
    }

    private void UpdateThumb()
    {
        double angle = _hue * Math.PI / 180;
        double r = _sat * Radius;
        double x = Size / 2.0 + Math.Cos(angle) * r;
        double y = Size / 2.0 + Math.Sin(angle) * r;
        Canvas.SetLeft(Thumb, x - Thumb.Width / 2);
        Canvas.SetTop(Thumb, y - Thumb.Height / 2);
    }

    private void UpdatePreviewAndHex()
    {
        SelectedColor = HsvToRgb(_hue, _sat, _val);
        PreviewSwatch.Background = new SolidColorBrush(SelectedColor);
        if (!_suppressHex)
            HexBox.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
    }

    // --- Wheel interaction ---

    private void Wheel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        WheelCanvas.CaptureMouse();
        PickFromPoint(e.GetPosition(WheelCanvas));
    }

    private void Wheel_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && WheelCanvas.IsMouseCaptured)
            PickFromPoint(e.GetPosition(WheelCanvas));
    }

    private void Wheel_MouseUp(object sender, MouseButtonEventArgs e) => WheelCanvas.ReleaseMouseCapture();

    private void PickFromPoint(Point p)
    {
        double dx = p.X - Size / 2.0, dy = p.Y - Size / 2.0;
        double dist = Math.Min(Radius, Math.Sqrt(dx * dx + dy * dy));
        _sat = dist / Radius;
        _hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
        UpdateThumb();
        UpdatePreviewAndHex();
    }

    private void ValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _val = e.NewValue;
        if (!IsLoaded) return;
        RenderWheel();
        UpdatePreviewAndHex();
    }

    // --- Hex entry ---

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitHex();
    }

    private void HexBox_Commit(object sender, RoutedEventArgs e) => CommitHex();

    private void CommitHex()
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(HexBox.Text.Trim())!;
            (_hue, _sat, _val) = RgbToHsv(c);
            _suppressHex = true;
            ValueSlider.Value = _val; // triggers RenderWheel
            RenderWheel();
            UpdateThumb();
            UpdatePreviewAndHex();
            _suppressHex = false;
        }
        catch { /* ignore malformed hex */ }
    }

    // --- Buttons / drag ---

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    // --- HSV <-> RGB ---

    private static (double h, double s, double v) RgbToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * (((b - r) / d) + 2);
            else h = 60 * (((r - g) / d) + 4);
        }
        if (h < 0) h += 360;
        double s = max <= 0 ? 0 : d / max;
        return (h, s, max);
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;
        double r = 0, g = 0, b = 0;
        switch ((int)(h / 60) % 6)
        {
            case 0: r = c; g = x; break;
            case 1: r = x; g = c; break;
            case 2: g = c; b = x; break;
            case 3: g = x; b = c; break;
            case 4: r = x; b = c; break;
            default: r = c; b = x; break;
        }
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
