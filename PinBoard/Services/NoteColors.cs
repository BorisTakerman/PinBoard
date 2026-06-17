using System.Windows.Media;
using PinBoard.Models;

namespace PinBoard.Services;

/// <summary>
/// Resolves a <see cref="NoteColor"/> to concrete brushes, choosing the bright
/// (cork) variant or the muted dark variant based on the active board theme.
/// Pins stay vivid on every theme (they read as plastic push-pins).
/// </summary>
public static class NoteColors
{
    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }

    // Bright note fills — used on all Cork themes and Plain.
    private static readonly Dictionary<NoteColor, SolidColorBrush> Bright = new()
    {
        [NoteColor.Amber]  = Frozen("#F4C969"),
        [NoteColor.Blue]   = Frozen("#A7C9E8"),
        [NoteColor.Purple] = Frozen("#C9B6E6"),
        [NoteColor.Sage]   = Frozen("#B7CCA1"),
        [NoteColor.Coral]  = Frozen("#F2A98F"),
        [NoteColor.White]  = Frozen("#FBF8F1"),
    };

    // Muted/tinted dark fills — used on the Dark theme.
    private static readonly Dictionary<NoteColor, SolidColorBrush> Muted = new()
    {
        [NoteColor.Amber]  = Frozen("#4A3D21"),
        [NoteColor.Blue]   = Frozen("#27384A"),
        [NoteColor.Purple] = Frozen("#352A47"),
        [NoteColor.Sage]   = Frozen("#2E3A28"),
        [NoteColor.Coral]  = Frozen("#4A2F28"),
        [NoteColor.White]  = Frozen("#2C2D32"),
    };

    // Vivid pin colors — identical across themes.
    private static readonly Dictionary<NoteColor, SolidColorBrush> Pins = new()
    {
        [NoteColor.Amber]  = Frozen("#E8A41E"),
        [NoteColor.Blue]   = Frozen("#2F7FD0"),
        [NoteColor.Purple] = Frozen("#7E4FD0"),
        [NoteColor.Sage]   = Frozen("#5BA34E"),
        [NoteColor.Coral]  = Frozen("#E5604A"),
        [NoteColor.White]  = Frozen("#D8D8D8"),
    };

    private static readonly SolidColorBrush DarkInk = Frozen("#2B2722");
    private static readonly SolidColorBrush LightInk = Frozen("#D9DADE");
    private static readonly SolidColorBrush DarkMuteInk = Frozen("#6B6256");
    private static readonly SolidColorBrush LightMuteInk = Frozen("#8A8C93");

    public static bool IsDarkTheme(BoardTheme theme) => theme == BoardTheme.Dark;

    /// <summary>Note background fill for the given color and theme.</summary>
    public static Brush Body(NoteColor color, BoardTheme theme)
        => (IsDarkTheme(theme) ? Muted : Bright)[color];

    /// <summary>Primary text color that reads on the note body.</summary>
    public static Brush Ink(BoardTheme theme)
        => IsDarkTheme(theme) ? LightInk : DarkInk;

    /// <summary>Muted text color for the category label and rule.</summary>
    public static Brush MutedInk(BoardTheme theme)
        => IsDarkTheme(theme) ? LightMuteInk : DarkMuteInk;

    /// <summary>Vivid pin color (theme-independent).</summary>
    public static Brush Pin(NoteColor color) => Pins[color];

    /// <summary>The six selectable note colors, for palette menus.</summary>
    public static IReadOnlyList<NoteColor> Palette { get; } = new[]
    {
        NoteColor.Amber, NoteColor.Blue, NoteColor.Purple,
        NoteColor.Sage, NoteColor.Coral, NoteColor.White
    };
}
