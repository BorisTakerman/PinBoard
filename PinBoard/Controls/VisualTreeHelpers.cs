using System.Windows;
using System.Windows.Media;

namespace PinBoard.Controls;

internal static class VisualTreeHelpers
{
    /// <summary>Walks up the visual tree to the first ancestor of type <typeparamref name="T"/>.</summary>
    public static T? FindAncestor<T>(DependencyObject? from) where T : DependencyObject
    {
        while (from is not null)
        {
            if (from is T match)
                return match;
            from = VisualTreeHelper.GetParent(from);
        }
        return null;
    }
}
