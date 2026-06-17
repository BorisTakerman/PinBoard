using System.IO;
using System.Text.Json;
using PinBoard.Models;

namespace PinBoard.Services;

/// <summary>
/// Loads, holds and persists the global <see cref="AppSettings"/>.
/// Raises <see cref="Changed"/> so live consumers (ropes, defaults) refresh.
/// </summary>
public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PinBoard");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Current { get; private set; } = new();

    /// <summary>Raised after settings change so dependent visuals can refresh.</summary>
    public static event Action? Changed;

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }

        if (string.IsNullOrWhiteSpace(Current.HomeFolder))
        {
            Current.HomeFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PinBoard");
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch { /* settings are best-effort; ignore write failures */ }
    }

    /// <summary>Notifies live consumers of a change (call after mutating Current).</summary>
    public static void NotifyChanged() => Changed?.Invoke();
}
