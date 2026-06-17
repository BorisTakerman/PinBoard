using System.Text.Json.Serialization;

namespace PinBoard.Models;

/// <summary>
/// Global, app-wide preferences persisted to settings.json in the user's
/// AppData. Distinct from per-board data (which lives in each .board file).
/// </summary>
public sealed class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UiTheme UiTheme { get; set; } = UiTheme.Dark;

    /// <summary>How much ropes droop, as a fraction of endpoint distance (0 = straight).</summary>
    public double RopeSag { get; set; } = 0.18;

    /// <summary>Visible thickness of a solid rope's core.</summary>
    public double RopeThickness { get; set; } = 6;

    /// <summary>Folder new boards save to / open from.</summary>
    public string HomeFolder { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NoteColor DefaultNoteColor { get; set; } = NoteColor.Amber;

    public string DefaultRopeColor { get; set; } = "#E0564F";

    /// <summary>When true, new ropes render in front of notes/clips by default.</summary>
    public bool RopesInFront { get; set; } = false;

    public HotkeySettings Hotkeys { get; set; } = new();
}

/// <summary>Tool keyboard shortcuts (stored as Key enum names, e.g. "Q").</summary>
public sealed class HotkeySettings
{
    public string AddNote { get; set; } = "Q";
    public string Rope { get; set; } = "W";
    public string ImageClip { get; set; } = "E";
    public string DocumentClip { get; set; } = "R";
}
