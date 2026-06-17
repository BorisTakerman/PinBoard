using System.Text.Json.Serialization;

namespace PinBoard.Models;

/// <summary>
/// Plain serialization DTOs for the .board file (JSON). ViewModels map to/from these.
/// Kept free of WPF types so the persistence layer stays UI-agnostic.
/// </summary>
public sealed class BoardData
{
    /// <summary>Schema version, so future format changes can be migrated.</summary>
    public int Version { get; set; } = 1;

    public string Name { get; set; } = "Untitled";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BoardTheme Theme { get; set; } = BoardTheme.CorkB;

    /// <summary>Flat background color used when <see cref="Theme"/> is Custom.</summary>
    public string CustomBackgroundHex { get; set; } = "#c4a47a";

    /// <summary>User-defined rope presets shown as pills in the bottom bar.</summary>
    public List<RopePreset> RopePresets { get; set; } = new();

    public List<NoteData> Notes { get; set; } = new();
    public List<RopeData> Ropes { get; set; } = new();
    public List<ClipData> Clips { get; set; } = new();
}

/// <summary>A user-created rope type: a name, a color, and a stroke style.</summary>
public sealed class RopePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "rope";
    /// <summary>Stroke color as a hex string, e.g. "#E0564F".</summary>
    public string Color { get; set; } = "#E0564F";
    public bool Dashed { get; set; }
}

public sealed class NoteData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 180;
    public double Height { get; set; } = 180;
    public int ZIndex { get; set; }

    public string Text { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NoteColor Color { get; set; } = NoteColor.Amber;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NoteColor PinColor { get; set; } = NoteColor.Coral;

    /// <summary>Custom hex fill; overrides <see cref="Color"/> when non-empty.</summary>
    public string CustomColorHex { get; set; } = string.Empty;
    public string CustomPinHex { get; set; } = string.Empty;
}

public sealed class RopeData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Ids of the connected items (note or clip).</summary>
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;

    /// <summary>Stroke color as a hex string, e.g. "#E0564F".</summary>
    public string Color { get; set; } = "#E0564F";
    public bool Dashed { get; set; }
    public string Label { get; set; } = string.Empty;

    /// <summary>When true, the rope renders in front of notes/clips.</summary>
    public bool InFront { get; set; }
}

public sealed class ClipData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClipType Type { get; set; } = ClipType.Image;

    /// <summary>Path relative to the .board file location.</summary>
    public string RelativePath { get; set; } = string.Empty;

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 160;
    public double Height { get; set; } = 160;
    public int ZIndex { get; set; }
}
