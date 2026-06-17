using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PinBoard.Models;
using PinBoard.Services;
using PinBoard.ViewModels;

namespace PinBoard.Controls;

public partial class NoteControl : UserControl
{
    private const double MinWidth_ = 130;
    private const double MinHeight_ = 100;

    private NoteViewModel? _vm;
    private MainViewModel? _main;
    private BoardViewModel? _board;
    private Canvas? _canvas;          // the world canvas (item panel) for world coords

    private bool _dragging;
    private bool _resizing;
    private bool _gestureMoved;       // did a drag/resize actually change anything
    private Point _start;             // drag/resize start, in world coords
    private double _originX, _originY, _originW, _originH;
    private bool _editing;
    private string _preEditText = string.Empty;

    public NoteControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _canvas = VisualTreeHelpers.FindAncestor<Canvas>(this);
        var host = VisualTreeHelpers.FindAncestor<ItemsControl>(this);
        _main = host?.DataContext as MainViewModel;
        _board = _main?.Board;

        BuildColorMenus();
        ThemeManager.ThemeChanged += UpdateColors;
        UpdateColors();
        UpdateSelectionVisual();
        ShowLabelIfPresent();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeChanged -= UpdateColors;
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as NoteViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;

        UpdateColors();
        UpdateSelectionVisual();
        ShowLabelIfPresent();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NoteViewModel.Color):
            case nameof(NoteViewModel.PinColor):
            case nameof(NoteViewModel.CustomColorHex):
            case nameof(NoteViewModel.CustomPinHex):
                UpdateColors();
                break;
            case nameof(NoteViewModel.IsSelected):
                UpdateSelectionVisual();
                break;
            case nameof(NoteViewModel.Label):
                ShowLabelIfPresent();
                break;
        }
    }

    // --- Theming ---

    private void UpdateColors()
    {
        if (_vm is null) return;
        var theme = ThemeManager.Current;

        bool customBody = !string.IsNullOrWhiteSpace(_vm.CustomColorHex);
        Body.Background = customBody ? HexBrush(_vm.CustomColorHex) : NoteColors.Body(_vm.Color, theme);
        PinFill.Fill = string.IsNullOrWhiteSpace(_vm.CustomPinHex)
            ? NoteColors.Pin(_vm.PinColor)
            : HexBrush(_vm.CustomPinHex);

        // For a custom body color, pick ink by luminance; otherwise follow the theme.
        var ink = customBody ? InkFor(_vm.CustomColorHex) : NoteColors.Ink(theme);
        var muted = customBody ? ink : NoteColors.MutedInk(theme);
        BodyBox.Foreground = ink;
        BodyBox.CaretBrush = ink;
        LabelBox.Foreground = muted;
        LabelBox.CaretBrush = muted;
        Rule.Background = muted;
        Rule.Opacity = 0.4;
        GripPath.Stroke = muted;
    }

    private void UpdateSelectionVisual()
        => SelectionRing.Visibility = _vm?.IsSelected == true ? Visibility.Visible : Visibility.Collapsed;

    private void ShowLabelIfPresent()
    {
        if (_editing) return;
        bool has = !string.IsNullOrWhiteSpace(_vm?.Label);
        LabelBox.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Dragging the note ---

    private void Body_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _board is null) return;

        // Rope tool: drag from this note to another to connect them.
        if (_main?.ActiveTool == ToolMode.Rope)
        {
            (Window.GetWindow(this) as MainWindow)?.StartRopeDrag(_vm);
            e.Handled = true;
            return;
        }

        // Double-click enters text editing instead of dragging.
        if (e.ClickCount == 2)
        {
            EnterEdit(BodyBox);
            e.Handled = true;
            return;
        }

        bool additive = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        _board.Select(_vm, additive);

        if (_editing) return; // let the textbox handle the click

        _dragging = true;
        _gestureMoved = false;
        _main?.History.Begin();
        _start = e.GetPosition(_canvas);
        _originX = _vm.X;
        _originY = _vm.Y;
        Body.CaptureMouse();
        e.Handled = true;
    }

    private void Body_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _vm is null || _canvas is null) return;
        var p = e.GetPosition(_canvas);
        _vm.X = _originX + (p.X - _start.X);
        _vm.Y = _originY + (p.Y - _start.Y);
        _gestureMoved = true;
    }

    private void Body_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Body.ReleaseMouseCapture();
        if (_gestureMoved) _main?.History.Commit(); else _main?.History.Cancel();
    }

    // --- Resizing ---

    private void Resize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _canvas is null) return;
        _resizing = true;
        _gestureMoved = false;
        _main?.History.Begin();
        _start = e.GetPosition(_canvas);
        _originW = _vm.Width;
        _originH = _vm.Height;
        ResizeHandle.CaptureMouse();
        e.Handled = true;
    }

    private void Resize_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_resizing || _vm is null || _canvas is null) return;
        var p = e.GetPosition(_canvas);
        _vm.Width = Math.Max(MinWidth_, _originW + (p.X - _start.X));
        _vm.Height = Math.Max(MinHeight_, _originH + (p.Y - _start.Y));
        _gestureMoved = true;
    }

    private void Resize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        ResizeHandle.ReleaseMouseCapture();
        if (_gestureMoved) _main?.History.Commit(); else _main?.History.Cancel();
        e.Handled = true;
    }

    // --- Inline editing ---

    private void EnterEdit(TextBox box)
    {
        _editing = true;
        _preEditText = box.Text;
        _main?.History.Begin();
        box.Visibility = Visibility.Visible;
        box.IsReadOnly = false;
        box.IsHitTestVisible = true;
        box.Focusable = true;
        box.Focus();
        box.SelectAll();
    }

    private void ExitEdit(TextBox box)
    {
        _editing = false;
        box.IsReadOnly = true;
        box.IsHitTestVisible = false;
        box.Focusable = false;
        if (box.Text != _preEditText) _main?.History.Commit(); else _main?.History.Cancel();
        ShowLabelIfPresent();
    }

    private void BodyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => ExitEdit(BodyBox);

    private void LabelBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => ExitEdit(LabelBox);

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Escape / Enter commits a single-line label edit.
        if (sender is TextBox box && (e.Key == Key.Escape || e.Key == Key.Enter))
        {
            Keyboard.ClearFocus();
            ExitEdit(box);
            e.Handled = true;
        }
    }

    // --- Context menu ---

    private void BuildColorMenus()
    {
        ColorMenu.Items.Clear();
        PinColorMenu.Items.Clear();
        foreach (var c in NoteColors.Palette)
        {
            ColorMenu.Items.Add(MakeColorItem(c, isPin: false));
            PinColorMenu.Items.Add(MakeColorItem(c, isPin: true));
        }
        ColorMenu.Items.Add(new Separator());
        ColorMenu.Items.Add(MakeCustomColorItem(isPin: false));
        PinColorMenu.Items.Add(new Separator());
        PinColorMenu.Items.Add(MakeCustomColorItem(isPin: true));
    }

    private MenuItem MakeColorItem(NoteColor color, bool isPin)
    {
        var swatch = new Rectangle
        {
            Width = 13,
            Height = 13,
            RadiusX = 3,
            RadiusY = 3,
            Fill = isPin ? NoteColors.Pin(color) : NoteColors.Body(color, ThemeManager.Current)
        };
        var item = new MenuItem { Header = color.ToString(), Icon = swatch };
        item.Click += (_, _) =>
        {
            if (_vm is null) return;
            _main?.History.Push();
            if (isPin) { _vm.PinColor = color; _vm.CustomPinHex = string.Empty; }
            else { _vm.Color = color; _vm.CustomColorHex = string.Empty; }
        };
        return item;
    }

    private MenuItem MakeCustomColorItem(bool isPin)
    {
        var item = new MenuItem { Header = "Custom…" };
        item.Click += (_, _) =>
        {
            if (_vm is null) return;
            var current = isPin ? _vm.CustomPinHex : _vm.CustomColorHex;
            var seed = ParseColor(string.IsNullOrWhiteSpace(current) ? "#F4C969" : current);
            var picked = ColorPickerWindow.Pick(Window.GetWindow(this), seed);
            if (picked is Color col)
            {
                _main?.History.Push();
                var hex = $"#{col.R:X2}{col.G:X2}{col.B:X2}";
                if (isPin) _vm.CustomPinHex = hex;
                else _vm.CustomColorHex = hex;
            }
        };
        return item;
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex)!; }
        catch { return Colors.Goldenrod; }
    }

    private static SolidColorBrush HexBrush(string hex)
    {
        var b = new SolidColorBrush(ParseColor(hex));
        b.Freeze();
        return b;
    }

    private static Brush InkFor(string hex)
    {
        var c = ParseColor(hex);
        double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        var ink = lum > 0.55 ? Color.FromRgb(0x2B, 0x27, 0x22) : Color.FromRgb(0xEC, 0xEC, 0xEC);
        var b = new SolidColorBrush(ink);
        b.Freeze();
        return b;
    }

    private void EditLabel_Click(object sender, RoutedEventArgs e) => EnterEdit(LabelBox);

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null && _board is not null)
        {
            _main?.History.Push();
            _board.Select(_board.DuplicateNote(_vm), additive: false);
        }
    }

    private void BringToFront_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) { _main?.History.Push(); _board?.BringToFront(_vm); }
    }

    private void SendToBack_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) { _main?.History.Push(); _board?.SendToBack(_vm); }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) { _main?.History.Push(); _board?.RemoveNote(_vm); }
    }
}
