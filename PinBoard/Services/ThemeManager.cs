using System.Windows;
using System.Windows.Media;
using PinBoard.Models;

namespace PinBoard.Services;

/// <summary>
/// Swaps the board background <see cref="ResourceDictionary"/> at runtime.
/// All board themes expose the same key (<c>BoardBackgroundBrush</c>), so any
/// element bound with <c>{DynamicResource BoardBackgroundBrush}</c> updates live
/// when the theme changes — no restart required.
/// </summary>
public static class ThemeManager
{
    private const string BoardThemeTag = "PinBoard.BoardTheme";

    private static readonly IReadOnlyDictionary<BoardTheme, string> ThemeUris =
        new Dictionary<BoardTheme, string>
        {
            [BoardTheme.CorkA] = "Themes/BoardThemes/CorkA.xaml",
            [BoardTheme.CorkB] = "Themes/BoardThemes/CorkB.xaml",
            [BoardTheme.CorkC] = "Themes/BoardThemes/CorkC.xaml",
            [BoardTheme.CorkD] = "Themes/BoardThemes/CorkD.xaml",
            [BoardTheme.Dark]  = "Themes/BoardThemes/Dark.xaml",
            [BoardTheme.Plain] = "Themes/BoardThemes/Plain.xaml",
        };

    /// <summary>The theme currently applied to the application.</summary>
    public static BoardTheme Current { get; private set; } = BoardTheme.CorkB;

    /// <summary>Raised after the active theme changes, so theme-aware visuals refresh.</summary>
    public static event Action? ThemeChanged;

    /// <summary>Applies a board theme by replacing the merged board-theme dictionary.</summary>
    public static void Apply(BoardTheme theme, string? customHex = null)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var merged = app.Resources.MergedDictionaries;

        // Remove any board theme we previously installed (tagged via a marker key).
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Contains(BoardThemeTag))
                merged.RemoveAt(i);
        }

        var dict = theme == BoardTheme.Custom
            ? BuildCustomDictionary(customHex)
            : new ResourceDictionary { Source = new Uri(ThemeUris[theme], UriKind.Relative) };
        dict[BoardThemeTag] = true; // marker so we can find/remove it next time
        merged.Add(dict);

        Current = theme;
        ThemeChanged?.Invoke();
    }

    private static ResourceDictionary BuildCustomDictionary(string? hex)
    {
        Color color;
        try { color = (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(hex) ? "#c4a47a" : hex)!; }
        catch { color = (Color)ColorConverter.ConvertFromString("#c4a47a")!; }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return new ResourceDictionary
        {
            ["BoardBackgroundBrush"] = brush,
            ["BoardBaseColorBrush"] = brush,
        };
    }
}
