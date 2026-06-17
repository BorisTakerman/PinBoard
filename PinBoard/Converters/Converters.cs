using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PinBoard.Converters;

/// <summary>
/// Returns the "active" chrome brush when the bound enum value matches the
/// <c>ConverterParameter</c> (compared by name); otherwise transparent.
/// Used to highlight the selected sidebar tool.
/// </summary>
public sealed class EnumToActiveBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool active = value is not null && parameter is not null &&
                      string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

        if (!active)
            return Brushes.Transparent;

        return Application.Current?.TryFindResource("ChromeActiveBrush") as Brush
               ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Resolves a string resource key (the bound value) to its <see cref="Brush"/>.
/// </summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Application.Current?.TryFindResource(value?.ToString() ?? string.Empty) as Brush
           ?? Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Maps a boolean to one of two brush resources named in the parameter as
/// "TrueKey|FalseKey". Missing keys resolve to transparent.
/// </summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var keys = (parameter?.ToString() ?? "|").Split('|');
        string key = value is true ? keys[0] : (keys.Length > 1 ? keys[1] : string.Empty);
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
