using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PinBoard.Services;
using PinBoard.ViewModels;

namespace PinBoard.Controls;

public partial class RopeControl : UserControl
{
    private const double TwistAmplitude = 2.6;   // how far strands swing from the centerline
    private const double TwistSpacing = 14;       // world px per full twist cycle

    private RopeViewModel? _vm;
    private MainViewModel? _main;
    private BoardViewModel? _board;
    private bool _editing;
    private string _preEditLabel = string.Empty;

    public RopeControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var host = VisualTreeHelpers.FindAncestor<ItemsControl>(this);
        _main = host?.DataContext as MainViewModel;
        _board = _main?.Board;
        SettingsService.Changed += Redraw;
        BuildColorMenu();
        Redraw();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmChanged;
        SettingsService.Changed -= Redraw;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmChanged;
        _vm = DataContext as RopeViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmChanged;
        Redraw();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e) => Redraw();

    // ============================================================
    //  Rendering
    // ============================================================

    private void Redraw()
    {
        if (_vm is null) return;

        var p0 = new Point(_vm.X1, _vm.Y1);
        var p3 = new Point(_vm.X2, _vm.Y2);
        double dx = p3.X - p0.X, dy = p3.Y - p0.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double sag = Math.Clamp(dist * SettingsService.Current.RopeSag, 0, 400);
        var c1 = new Point(p0.X + dx * 0.33, p0.Y + dy * 0.33 + sag);
        var c2 = new Point(p0.X + dx * 0.66, p0.Y + dy * 0.66 + sag);

        var spine = BuildBezier(p0, c1, c2, p3);
        HitPath.Data = spine;
        SelectionGlow.Data = spine;

        var baseColor = ParseColor(_vm.ColorHex);
        var dark = Scale(baseColor, 0.58);
        var mid = Scale(baseColor, 0.82);

        SelectionGlow.Visibility = _vm.IsSelected ? Visibility.Visible : Visibility.Collapsed;
        SelectionGlow.Stroke = (Brush)(TryFindResource("AccentBrush") ?? Brushes.DodgerBlue);

        if (_vm.Dashed)
        {
            // Dashed presets render as a clean thin dashed line (no twist).
            BaseStroke.Visibility = Visibility.Collapsed;
            StrandB.Visibility = Visibility.Collapsed;
            StrandA.Visibility = Visibility.Visible;
            StrandA.Data = spine;
            StrandA.Stroke = FrozenBrush(baseColor);
            StrandA.StrokeThickness = 2.4;
            StrandA.StrokeDashArray = new DoubleCollection { 4, 3 };
        }
        else
        {
            StrandA.StrokeDashArray = null;
            BaseStroke.Visibility = Visibility.Visible;
            StrandA.Visibility = Visibility.Visible;
            StrandB.Visibility = Visibility.Visible;

            double thickness = SettingsService.Current.RopeThickness;
            BaseStroke.Data = spine;
            BaseStroke.Stroke = FrozenBrush(dark);
            BaseStroke.StrokeThickness = thickness;

            var (strandA, strandB) = BuildStrands(p0, c1, c2, p3, dist);
            StrandA.Data = strandA;
            StrandA.Stroke = FrozenBrush(baseColor);
            StrandA.StrokeThickness = thickness * 0.5;
            StrandB.Data = strandB;
            StrandB.Stroke = FrozenBrush(mid);
            StrandB.StrokeThickness = thickness * 0.5;
        }

        var anchorBrush = FrozenBrush(dark);
        AnchorA.Fill = anchorBrush;
        AnchorB.Fill = anchorBrush;
        Canvas.SetLeft(AnchorA, p0.X - AnchorA.Width / 2);
        Canvas.SetTop(AnchorA, p0.Y - AnchorA.Height / 2);
        Canvas.SetLeft(AnchorB, p3.X - AnchorB.Width / 2);
        Canvas.SetTop(AnchorB, p3.Y - AnchorB.Height / 2);

        UpdateLabel();
    }

    private static PathGeometry BuildBezier(Point p0, Point c1, Point c2, Point p3)
    {
        var figure = new PathFigure { StartPoint = p0 };
        figure.Segments.Add(new BezierSegment(c1, c2, p3, true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    /// <summary>
    /// Samples the spine and offsets two strands sinusoidally to opposite sides,
    /// so they weave across each other for a simple twisted-rope look.
    /// </summary>
    private static (PathGeometry a, PathGeometry b) BuildStrands(
        Point p0, Point c1, Point c2, Point p3, double dist)
    {
        int n = Math.Clamp((int)(dist / 3), 24, 400);
        var a = new PointCollection();
        var b = new PointCollection();

        double cum = 0;
        var prev = Eval(0);
        for (int i = 0; i <= n; i++)
        {
            double t = (double)i / n;
            var pt = Eval(t);
            var d = Deriv(t);
            double len = Math.Max(1e-6, Math.Sqrt(d.X * d.X + d.Y * d.Y));
            var normal = new Vector(-d.Y / len, d.X / len);

            if (i > 0)
                cum += (pt - prev).Length;
            double off = TwistAmplitude * Math.Sin(cum / TwistSpacing * 2 * Math.PI);

            a.Add(pt + normal * off);
            b.Add(pt - normal * off);
            prev = pt;
        }

        return (Polyline(a), Polyline(b));

        Point Eval(double t)
        {
            double u = 1 - t;
            double w0 = u * u * u, w1 = 3 * u * u * t, w2 = 3 * u * t * t, w3 = t * t * t;
            return new Point(
                w0 * p0.X + w1 * c1.X + w2 * c2.X + w3 * p3.X,
                w0 * p0.Y + w1 * c1.Y + w2 * c2.Y + w3 * p3.Y);
        }

        Vector Deriv(double t)
        {
            double u = 1 - t;
            return new Vector(
                3 * u * u * (c1.X - p0.X) + 6 * u * t * (c2.X - c1.X) + 3 * t * t * (p3.X - c2.X),
                3 * u * u * (c1.Y - p0.Y) + 6 * u * t * (c2.Y - c1.Y) + 3 * t * t * (p3.Y - c2.Y));
        }
    }

    private static PathGeometry Polyline(PointCollection pts)
    {
        var figure = new PathFigure { StartPoint = pts[0] };
        figure.Segments.Add(new PolyLineSegment(pts, true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex)!; }
        catch { return Colors.IndianRed; }
    }

    private static Color Scale(Color c, double f)
        => Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));

    private static SolidColorBrush FrozenBrush(Color c)
    {
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }

    private void UpdateLabel()
    {
        if (_vm is null) return;
        bool show = _editing || !string.IsNullOrWhiteSpace(_vm.Label);
        LabelHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show) return;

        LabelHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = LabelHost.DesiredSize;
        Canvas.SetLeft(LabelHost, _vm.MidX - size.Width / 2);
        Canvas.SetTop(LabelHost, _vm.MidY - size.Height / 2);
    }

    // ============================================================
    //  Interaction
    // ============================================================

    private void Rope_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _board is null) return;
        _board.ClearSelection();
        _vm.IsSelected = true;
        e.Handled = true;
    }

    private void ToggleDashed_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) { _main?.History.Push(); _vm.Dashed = !_vm.Dashed; }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) { _main?.History.Push(); _board?.RemoveRope(_vm); }
    }

    private void SendToFront_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) { _main?.History.Push(); _board?.SendRopeToFront(_vm); }
    }

    private void SendToBack_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) { _main?.History.Push(); _board?.SendRopeToBack(_vm); }
    }

    private void EditLabel_Click(object sender, RoutedEventArgs e)
    {
        _editing = true;
        _preEditLabel = _vm?.Label ?? string.Empty;
        _main?.History.Begin();
        UpdateLabel();
        LabelBox.IsReadOnly = false;
        LabelBox.IsHitTestVisible = true;
        LabelBox.Focusable = true;
        LabelBox.Focus();
        LabelBox.SelectAll();
    }

    private void LabelBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ExitEdit();

    private void LabelBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Escape)
        {
            Keyboard.ClearFocus();
            ExitEdit();
            e.Handled = true;
        }
    }

    private void ExitEdit()
    {
        _editing = false;
        LabelBox.IsReadOnly = true;
        LabelBox.IsHitTestVisible = false;
        LabelBox.Focusable = false;
        if ((_vm?.Label ?? string.Empty) != _preEditLabel) _main?.History.Commit();
        else _main?.History.Cancel();
        UpdateLabel();
    }

    // --- Color menu ---

    private void BuildColorMenu()
    {
        ColorMenu.Items.Clear();
        foreach (var swatch in RopePalette.Colors)
        {
            var brush = FrozenBrush(ParseColor(swatch.Hex));
            var item = new MenuItem
            {
                Header = swatch.Name,
                Icon = new Rectangle { Width = 13, Height = 13, RadiusX = 3, RadiusY = 3, Fill = brush }
            };
            var hex = swatch.Hex;
            item.Click += (_, _) => { if (_vm is not null) { _main?.History.Push(); _vm.ColorHex = hex; } };
            ColorMenu.Items.Add(item);
        }

        ColorMenu.Items.Add(new Separator());
        var custom = new MenuItem { Header = "Custom…" };
        custom.Click += (_, _) =>
        {
            if (_vm is null) return;
            var picked = ColorPickerWindow.Pick(Window.GetWindow(this), ParseColor(_vm.ColorHex));
            if (picked is Color col)
            {
                _main?.History.Push();
                _vm.ColorHex = $"#{col.R:X2}{col.G:X2}{col.B:X2}";
            }
        };
        ColorMenu.Items.Add(custom);
    }
}
