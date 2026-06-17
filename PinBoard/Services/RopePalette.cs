namespace PinBoard.Services;

/// <summary>Shared color choices for rope presets and rope recoloring.</summary>
public static class RopePalette
{
    public readonly record struct Swatch(string Name, string Hex);

    public static IReadOnlyList<Swatch> Colors { get; } = new[]
    {
        new Swatch("Red",    "#E0564F"),
        new Swatch("Blue",   "#4F8CF0"),
        new Swatch("Green",  "#54B06A"),
        new Swatch("Purple", "#9B6FE0"),
        new Swatch("Orange", "#E8923F"),
        new Swatch("Teal",   "#3FB6B0"),
        new Swatch("Pink",   "#E06FA8"),
        new Swatch("Yellow", "#E8C24F"),
    };
}
