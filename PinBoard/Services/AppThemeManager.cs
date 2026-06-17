using System.Windows;
using System.Windows.Media;
using PinBoard.Models;

namespace PinBoard.Services;

/// <summary>
/// Applies a global app-chrome theme by recoloring the shared chrome brushes
/// in place. Because the shell references those brushes by <c>StaticResource</c>,
/// mutating each brush's <see cref="SolidColorBrush.Color"/> updates the whole
/// UI live, with no restart and no per-element rewiring.
/// </summary>
public static class AppThemeManager
{
    // Brush resource keys (defined in Themes/Chrome.xaml) -> hex per theme.
    private static readonly Dictionary<UiTheme, Dictionary<string, string>> Palettes = new()
    {
        [UiTheme.Dark] = new()
        {
            ["ChromeBrush"] = "#0d0e10",
            ["ChromeAltBrush"] = "#111214",
            ["ChromeBorderBrush"] = "#1c1e22",
            ["ChromeHoverBrush"] = "#1a1c20",
            ["ChromeActiveBrush"] = "#26282d",
            ["TextPrimaryBrush"] = "#e6e7ea",
            ["TextMutedBrush"] = "#7d818a",
            ["TextFaintBrush"] = "#565a62",
            ["AccentBrush"] = "#4f8cf0",
        },
        [UiTheme.Midnight] = new()
        {
            ["ChromeBrush"] = "#0b0e16",
            ["ChromeAltBrush"] = "#0f1320",
            ["ChromeBorderBrush"] = "#1d2436",
            ["ChromeHoverBrush"] = "#161c2b",
            ["ChromeActiveBrush"] = "#232c42",
            ["TextPrimaryBrush"] = "#e8ecf6",
            ["TextMutedBrush"] = "#828daa",
            ["TextFaintBrush"] = "#5a647d",
            ["AccentBrush"] = "#5b8cff",
        },
        [UiTheme.Light] = new()
        {
            ["ChromeBrush"] = "#f4f5f7",
            ["ChromeAltBrush"] = "#ffffff",
            ["ChromeBorderBrush"] = "#d8dce3",
            ["ChromeHoverBrush"] = "#e9ebf0",
            ["ChromeActiveBrush"] = "#dde2ea",
            ["TextPrimaryBrush"] = "#1b1d22",
            ["TextMutedBrush"] = "#6b7280",
            ["TextFaintBrush"] = "#99a0ab",
            ["AccentBrush"] = "#2f6fed",
        },
        [UiTheme.Sefora] = new()
        {
            ["ChromeBrush"] = "#17141b",
            ["ChromeAltBrush"] = "#1d1922",
            ["ChromeBorderBrush"] = "#2c2533",
            ["ChromeHoverBrush"] = "#251f2e",
            ["ChromeActiveBrush"] = "#322a3d",
            ["TextPrimaryBrush"] = "#f0e9f1",
            ["TextMutedBrush"] = "#a597ac",
            ["TextFaintBrush"] = "#6f6477",
            ["AccentBrush"] = "#d56fa6",
        },
        [UiTheme.Forest] = new()
        {
            ["ChromeBrush"] = "#0c130f",
            ["ChromeAltBrush"] = "#101814",
            ["ChromeBorderBrush"] = "#1d2a22",
            ["ChromeHoverBrush"] = "#16201a",
            ["ChromeActiveBrush"] = "#233530",
            ["TextPrimaryBrush"] = "#e6efe8",
            ["TextMutedBrush"] = "#8aa195",
            ["TextFaintBrush"] = "#5f7468",
            ["AccentBrush"] = "#4fb07a",
        },
        [UiTheme.Slate] = new()
        {
            ["ChromeBrush"] = "#14171c",
            ["ChromeAltBrush"] = "#191d24",
            ["ChromeBorderBrush"] = "#272d37",
            ["ChromeHoverBrush"] = "#1f242c",
            ["ChromeActiveBrush"] = "#2d3540",
            ["TextPrimaryBrush"] = "#e7eaef",
            ["TextMutedBrush"] = "#8b94a3",
            ["TextFaintBrush"] = "#616a78",
            ["AccentBrush"] = "#6d8cb0",
        },
    };

    private const string UiThemeTag = "PinBoard.UiTheme";

    public static UiTheme Current { get; private set; } = UiTheme.Dark;

    public static IReadOnlyList<UiTheme> All { get; } = Enum.GetValues<UiTheme>();

    /// <summary>
    /// Applies a chrome theme by swapping in a tagged dictionary of the themeable
    /// brushes. The shell references these brushes via <c>DynamicResource</c>, so
    /// the whole UI updates live. Brushes in Chrome.xaml act as defaults.
    /// </summary>
    public static void Apply(UiTheme theme)
    {
        var app = Application.Current;
        if (app is null || !Palettes.TryGetValue(theme, out var palette))
            return;

        var merged = app.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Contains(UiThemeTag))
                merged.RemoveAt(i);
        }

        var dict = new ResourceDictionary();
        foreach (var (key, hex) in palette)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
            brush.Freeze();
            dict[key] = brush;
        }
        dict[UiThemeTag] = true;
        merged.Add(dict);

        Current = theme;
    }

    /// <summary>Chrome + accent colors used to render a theme preview swatch.</summary>
    public static (Color chrome, Color accent, Color text) Preview(UiTheme theme)
    {
        var p = Palettes[theme];
        return (
            (Color)ColorConverter.ConvertFromString(p["ChromeBrush"])!,
            (Color)ColorConverter.ConvertFromString(p["AccentBrush"])!,
            (Color)ColorConverter.ConvertFromString(p["TextPrimaryBrush"])!);
    }
}
