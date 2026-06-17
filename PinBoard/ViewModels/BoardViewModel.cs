using System.Collections.ObjectModel;
using System.IO;
using PinBoard.Models;
using PinBoard.Services;

namespace PinBoard.ViewModels;

/// <summary>
/// One open board: its notes, ropes, clips, rope presets, theme and metadata.
/// Maps to/from <see cref="BoardData"/> for persistence.
/// </summary>
public sealed class BoardViewModel : ObservableObject
{
    private string _name = "Untitled";
    private BoardTheme _theme = BoardTheme.CorkB;
    private string _customBackgroundHex = "#c4a47a";

    public ObservableCollection<NoteViewModel> Notes { get; } = new();

    /// <summary>Master list of ropes (for logic and persistence).</summary>
    public ObservableCollection<RopeViewModel> Ropes { get; } = new();

    /// <summary>Render layer for ropes drawn behind notes/clips.</summary>
    public ObservableCollection<RopeViewModel> RopesBackLayer { get; } = new();

    /// <summary>Render layer for ropes drawn in front of notes/clips.</summary>
    public ObservableCollection<RopeViewModel> RopesFrontLayer { get; } = new();

    public ObservableCollection<ClipViewModel> Clips { get; } = new();

    /// <summary>Default front/back placement applied to newly created ropes.</summary>
    public bool DefaultRopesInFront { get; set; }

    /// <summary>User-defined rope presets, surfaced as pills in the bottom bar.</summary>
    public ObservableCollection<RopePresetViewModel> RopePresets { get; } = new();

    /// <summary>Absolute path of the .board file, or null for an unsaved board.</summary>
    public string? FilePath { get; set; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public BoardTheme Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
                ThemeManager.Apply(value, _customBackgroundHex);
        }
    }

    /// <summary>Flat color used when <see cref="Theme"/> is <see cref="BoardTheme.Custom"/>.</summary>
    public string CustomBackgroundHex
    {
        get => _customBackgroundHex;
        set
        {
            if (SetProperty(ref _customBackgroundHex, value) && _theme == BoardTheme.Custom)
                ThemeManager.Apply(BoardTheme.Custom, value);
        }
    }

    /// <summary>Sets a custom flat background color and switches the board to it.</summary>
    public void SetCustomBackground(string hex)
    {
        CustomBackgroundHex = hex;
        if (_theme == BoardTheme.Custom)
            ThemeManager.Apply(BoardTheme.Custom, hex);
        else
            Theme = BoardTheme.Custom;
    }

    /// <summary>Default color applied to newly created notes (settings later).</summary>
    public NoteColor DefaultNoteColor { get; set; } = NoteColor.Amber;

    // ============================================================
    //  Notes
    // ============================================================

    private int NextZ() => (Notes.Count == 0 ? 0 : Notes.Max(n => n.ZIndex)) + 1;

    public NoteViewModel AddNote(double x, double y)
    {
        var note = new NoteViewModel
        {
            X = x,
            Y = y,
            Color = DefaultNoteColor,
            PinColor = NoteColor.Coral,
            ZIndex = NextZ()
        };
        Notes.Add(note);
        return note;
    }

    public void RemoveNote(NoteViewModel note)
    {
        RemoveRopesFor(note.Id);
        Notes.Remove(note);
    }

    private void RemoveRopesFor(string itemId)
    {
        for (int i = Ropes.Count - 1; i >= 0; i--)
        {
            if (Ropes[i].FromId == itemId || Ropes[i].ToId == itemId)
                RemoveRope(Ropes[i]);
        }
    }

    public NoteViewModel DuplicateNote(NoteViewModel note)
    {
        var data = note.ToData();
        data.Id = Guid.NewGuid().ToString("N");
        data.X += 24;
        data.Y += 24;
        data.ZIndex = NextZ();
        var copy = NoteViewModel.FromData(data);
        Notes.Add(copy);
        return copy;
    }

    public void BringToFront(NoteViewModel note) => note.ZIndex = NextZ();

    public void SendToBack(NoteViewModel note)
    {
        int min = Notes.Count == 0 ? 0 : Notes.Min(n => n.ZIndex);
        note.ZIndex = min - 1;
    }

    // ============================================================
    //  Clips (images / documents)
    // ============================================================

    public ClipViewModel AddClip(ClipType type, string absolutePath, double x, double y, double w, double h)
    {
        var clip = new ClipViewModel
        {
            Type = type,
            AbsolutePath = absolutePath,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            ZIndex = Clips.Count == 0 ? 0 : Clips.Max(c => c.ZIndex) + 1
        };
        Clips.Add(clip);
        return clip;
    }

    public void RemoveClip(ClipViewModel clip)
    {
        RemoveRopesFor(clip.Id);
        Clips.Remove(clip);
    }

    public void SelectClip(ClipViewModel clip, bool additive)
    {
        if (!additive)
        {
            ClearSelection();
            clip.IsSelected = true;
        }
        else
        {
            clip.IsSelected = !clip.IsSelected;
        }
    }

    // ============================================================
    //  Selection
    // ============================================================

    public void ClearSelection()
    {
        foreach (var n in Notes) n.IsSelected = false;
        foreach (var r in Ropes) r.IsSelected = false;
        foreach (var c in Clips) c.IsSelected = false;
    }

    public void Select(NoteViewModel note, bool additive)
    {
        if (!additive)
        {
            ClearSelection();
            note.IsSelected = true;
        }
        else
        {
            note.IsSelected = !note.IsSelected;
        }
    }

    public void DeleteSelected()
    {
        foreach (var r in Ropes.Where(r => r.IsSelected).ToList())
            RemoveRope(r);
        foreach (var n in Notes.Where(n => n.IsSelected).ToList())
            RemoveNote(n);
        foreach (var c in Clips.Where(c => c.IsSelected).ToList())
            RemoveClip(c);
    }

    // ============================================================
    //  Ropes
    // ============================================================

    /// <summary>The preset new ropes adopt (the active bottom-bar pill).</summary>
    public RopePresetViewModel? ActivePreset => RopePresets.FirstOrDefault(p => p.IsActive);

    public RopeViewModel CreateRope(IRopeAnchor from, IRopeAnchor to)
    {
        var preset = ActivePreset;
        var rope = new RopeViewModel
        {
            FromId = from.Id,
            ToId = to.Id,
            ColorHex = preset?.ColorHex ?? "#E0564F",
            Dashed = preset?.Dashed ?? false,
            Label = preset?.Name ?? string.Empty,
            InFront = DefaultRopesInFront
        };
        rope.Attach(from, to);
        AddRopeInternal(rope);
        return rope;
    }

    /// <summary>Adds a rope to the master list and the correct render layer.</summary>
    private void AddRopeInternal(RopeViewModel rope)
    {
        Ropes.Add(rope);
        (rope.InFront ? RopesFrontLayer : RopesBackLayer).Add(rope);
        rope.PropertyChanged += OnRopePropertyChanged;
    }

    private void OnRopePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RopeViewModel.InFront) || sender is not RopeViewModel rope)
            return;
        // Move between render layers to reflect the new front/back placement.
        RopesBackLayer.Remove(rope);
        RopesFrontLayer.Remove(rope);
        (rope.InFront ? RopesFrontLayer : RopesBackLayer).Add(rope);
    }

    public void RemoveRope(RopeViewModel rope)
    {
        rope.PropertyChanged -= OnRopePropertyChanged;
        rope.Detach();
        Ropes.Remove(rope);
        RopesBackLayer.Remove(rope);
        RopesFrontLayer.Remove(rope);
    }

    public void SendRopeToFront(RopeViewModel rope) => rope.InFront = true;
    public void SendRopeToBack(RopeViewModel rope) => rope.InFront = false;

    // ============================================================
    //  Rope presets
    // ============================================================

    public RopePresetViewModel AddPreset(string name, string colorHex, bool dashed)
    {
        var preset = new RopePresetViewModel(name, colorHex, dashed);
        RopePresets.Add(preset);
        SelectPreset(preset);
        return preset;
    }

    public void RemovePreset(RopePresetViewModel preset)
    {
        bool wasActive = preset.IsActive;
        RopePresets.Remove(preset);
        if (wasActive && RopePresets.Count > 0)
            SelectPreset(RopePresets[0]);
    }

    public void SelectPreset(RopePresetViewModel preset)
    {
        foreach (var p in RopePresets)
            p.IsActive = ReferenceEquals(p, preset);
    }

    // ============================================================
    //  Persistence mapping
    // ============================================================

    private string? BoardDir => string.IsNullOrEmpty(FilePath) ? null : Path.GetDirectoryName(FilePath);

    /// <summary>Converts an absolute clip path to one relative to the board file (or keeps it absolute if unsaved).</summary>
    private string ToRelative(string absolute)
    {
        var dir = BoardDir;
        if (dir is null || string.IsNullOrEmpty(absolute)) return absolute;
        try { return Path.GetRelativePath(dir, absolute); } catch { return absolute; }
    }

    /// <summary>Resolves a stored (relative or absolute) clip path against the board file location.</summary>
    private string ToAbsolute(string stored)
    {
        if (string.IsNullOrEmpty(stored) || Path.IsPathRooted(stored)) return stored;
        var dir = BoardDir;
        if (dir is null) return stored;
        try { return Path.GetFullPath(stored, dir); } catch { return stored; }
    }

    public static BoardViewModel FromData(BoardData d, string? filePath)
    {
        var vm = new BoardViewModel
        {
            Name = d.Name,
            FilePath = filePath,
            _theme = d.Theme,
            _customBackgroundHex = string.IsNullOrWhiteSpace(d.CustomBackgroundHex) ? "#c4a47a" : d.CustomBackgroundHex
        };

        foreach (var p in d.RopePresets)
            vm.RopePresets.Add(RopePresetViewModel.FromData(p));
        if (vm.RopePresets.Count > 0)
            vm.RopePresets[0].IsActive = true;

        foreach (var n in d.Notes) vm.Notes.Add(NoteViewModel.FromData(n));
        foreach (var c in d.Clips)
            vm.Clips.Add(ClipViewModel.FromData(c, vm.ToAbsolute(c.RelativePath)));

        // Resolve rope endpoints against both notes and clips.
        var anchors = vm.Notes.Cast<IRopeAnchor>().Concat(vm.Clips)
            .ToDictionary(a => a.Id);
        foreach (var rd in d.Ropes)
        {
            var rope = RopeViewModel.FromData(rd);
            if (anchors.TryGetValue(rope.FromId, out var from) &&
                anchors.TryGetValue(rope.ToId, out var to))
            {
                rope.Attach(from, to);
                vm.AddRopeInternal(rope);
            }
        }

        return vm;
    }

    public BoardData ToData() => new()
    {
        Name = Name,
        Theme = Theme,
        CustomBackgroundHex = CustomBackgroundHex,
        RopePresets = RopePresets.Select(p => p.ToData()).ToList(),
        Notes = Notes.Select(n => n.ToData()).ToList(),
        Ropes = Ropes.Select(r => r.ToData()).ToList(),
        Clips = Clips.Select(c => c.ToData(ToRelative(c.AbsolutePath))).ToList()
    };
}
