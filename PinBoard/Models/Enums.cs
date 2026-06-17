namespace PinBoard.Models;

/// <summary>The active editing tool selected in the left sidebar.</summary>
public enum ToolMode
{
    Select,
    AddNote,
    Rope,
    ImageClip,
    DocumentClip,
    Pan
}

/// <summary>Board background theme. Stored per-board in the .board JSON.</summary>
public enum BoardTheme
{
    CorkA,
    CorkB,
    CorkC,
    CorkD,
    Dark,
    Plain,
    /// <summary>A flat user-chosen color (see BoardViewModel.CustomBackgroundHex).</summary>
    Custom
}

/// <summary>Global app-chrome theme (titlebar / sidebar / bars). A user preference.</summary>
public enum UiTheme
{
    Midnight,
    Dark,
    Light,
    Sefora,
    Forest,
    Slate
}

/// <summary>The palette a sticky note can use. Rendered bright on cork, muted on dark.</summary>
public enum NoteColor
{
    Amber,
    Blue,
    Purple,
    Sage,
    Coral,
    White
}

/// <summary>Kind of file clip pinned to the board.</summary>
public enum ClipType
{
    Image,
    Document
}
