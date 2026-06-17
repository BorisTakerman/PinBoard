using PinBoard.Models;

namespace PinBoard.ViewModels;

/// <summary>
/// A single sticky note on the board. Full interaction (drag/resize/edit) lands
/// with the NoteControl in a later pass — this is the data-bound scaffold.
/// </summary>
public sealed class NoteViewModel : ObservableObject, IRopeAnchor
{
    private double _x, _y, _width = 180, _height = 180;
    private int _zIndex;
    private string _text = string.Empty;
    private string _label = string.Empty;
    private NoteColor _color = NoteColor.Amber;
    private NoteColor _pinColor = NoteColor.Coral;
    private string _customColorHex = string.Empty;
    private string _customPinHex = string.Empty;
    private bool _isSelected;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public double X { get => _x; set => SetProperty(ref _x, value); }
    public double Y { get => _y; set => SetProperty(ref _y, value); }
    public double Width { get => _width; set => SetProperty(ref _width, value); }
    public double Height { get => _height; set => SetProperty(ref _height, value); }
    public int ZIndex { get => _zIndex; set => SetProperty(ref _zIndex, value); }

    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public string Label { get => _label; set => SetProperty(ref _label, value); }
    public NoteColor Color { get => _color; set => SetProperty(ref _color, value); }
    public NoteColor PinColor { get => _pinColor; set => SetProperty(ref _pinColor, value); }

    /// <summary>Custom note fill as hex; when non-empty it overrides <see cref="Color"/>.</summary>
    public string CustomColorHex { get => _customColorHex; set => SetProperty(ref _customColorHex, value); }

    /// <summary>Custom pin color as hex; when non-empty it overrides <see cref="PinColor"/>.</summary>
    public string CustomPinHex { get => _customPinHex; set => SetProperty(ref _customPinHex, value); }

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public static NoteViewModel FromData(NoteData d) => new()
    {
        Id = d.Id,
        X = d.X,
        Y = d.Y,
        Width = d.Width,
        Height = d.Height,
        ZIndex = d.ZIndex,
        Text = d.Text,
        Label = d.Label,
        Color = d.Color,
        PinColor = d.PinColor,
        CustomColorHex = d.CustomColorHex,
        CustomPinHex = d.CustomPinHex
    };

    public NoteData ToData() => new()
    {
        Id = Id,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        ZIndex = ZIndex,
        Text = Text,
        Label = Label,
        Color = Color,
        PinColor = PinColor,
        CustomColorHex = CustomColorHex,
        CustomPinHex = CustomPinHex
    };
}
