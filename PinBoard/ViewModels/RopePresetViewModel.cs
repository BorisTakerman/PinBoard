using System.Windows.Media;
using PinBoard.Models;
using PinBoard.Services;

namespace PinBoard.ViewModels;

/// <summary>
/// A user-created rope preset shown as a pill in the bottom bar: a name, a
/// color and a solid/dashed style. New ropes adopt the active preset.
/// </summary>
public sealed class RopePresetViewModel : ObservableObject
{
    private string _name;
    private string _colorHex;
    private bool _dashed;
    private bool _isActive;

    public RopePresetViewModel(string name, string colorHex, bool dashed)
    {
        _name = name;
        _colorHex = colorHex;
        _dashed = dashed;
    }

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (SetProperty(ref _colorHex, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Pill label: the user's name, or — when they left it blank — the color's
    /// name (so the pill still reads clearly while ropes stay label-less).
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name))
                return Name;
            foreach (var c in RopePalette.Colors)
                if (string.Equals(c.Hex, ColorHex, StringComparison.OrdinalIgnoreCase))
                    return c.Name;
            return ColorHex;
        }
    }

    public bool Dashed
    {
        get => _dashed;
        set => SetProperty(ref _dashed, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    /// <summary>Display brush for the pill's color dot.</summary>
    public Brush ColorBrush
    {
        get
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex)!);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Gray;
            }
        }
    }

    public static RopePresetViewModel FromData(RopePreset d) => new(d.Name, d.Color, d.Dashed) { Id = d.Id };

    public RopePreset ToData() => new() { Id = Id, Name = Name, Color = ColorHex, Dashed = Dashed };
}
