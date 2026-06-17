using System.ComponentModel;
using System.Windows.Media;
using PinBoard.Models;

namespace PinBoard.ViewModels;

/// <summary>
/// A rope connection between two notes. Endpoints are anchored at each note's
/// top-center pin and recompute live as the notes move or resize.
/// </summary>
public sealed class RopeViewModel : ObservableObject
{
    private string _colorHex = "#E0564F";
    private bool _dashed;
    private string _label = string.Empty;
    private bool _isSelected;
    private bool _inFront;

    private IRopeAnchor? _from;
    private IRopeAnchor? _to;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Ids of the connected items (a note or a clip).</summary>
    public string FromId { get; init; } = string.Empty;
    public string ToId { get; init; } = string.Empty;

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (SetProperty(ref _colorHex, value))
                OnPropertyChanged(nameof(ColorBrush));
        }
    }

    public bool Dashed { get => _dashed; set => SetProperty(ref _dashed, value); }
    public string Label { get => _label; set => SetProperty(ref _label, value); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    /// <summary>Whether this rope renders in front of notes/clips.</summary>
    public bool InFront { get => _inFront; set => SetProperty(ref _inFront, value); }

    public Brush ColorBrush
    {
        get
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex)!);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Gray;
            }
        }
    }

    /// <summary>Vertical offset so the anchor tucks under the note's pin head.</summary>
    private const double PinOffset = 11;

    // Endpoints anchored at each note's top-center pin.
    public double X1 => _from is null ? 0 : _from.X + _from.Width / 2;
    public double Y1 => _from is null ? 0 : _from.Y + PinOffset;
    public double X2 => _to is null ? 0 : _to.X + _to.Width / 2;
    public double Y2 => _to is null ? 0 : _to.Y + PinOffset;

    public double MidX => (X1 + X2) / 2;
    public double MidY => (Y1 + Y2) / 2;

    /// <summary>Wires the rope to its two endpoints so its geometry tracks them.</summary>
    public void Attach(IRopeAnchor from, IRopeAnchor to)
    {
        Detach();
        _from = from;
        _to = to;
        _from.PropertyChanged += OnNoteChanged;
        _to.PropertyChanged += OnNoteChanged;
        RaiseGeometry();
    }

    public void Detach()
    {
        if (_from is not null) _from.PropertyChanged -= OnNoteChanged;
        if (_to is not null) _to.PropertyChanged -= OnNoteChanged;
        _from = null;
        _to = null;
    }

    private void OnNoteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NoteViewModel.X) or nameof(NoteViewModel.Y)
            or nameof(NoteViewModel.Width) or nameof(NoteViewModel.Height))
        {
            RaiseGeometry();
        }
    }

    private void RaiseGeometry()
    {
        OnPropertyChanged(nameof(X1));
        OnPropertyChanged(nameof(Y1));
        OnPropertyChanged(nameof(X2));
        OnPropertyChanged(nameof(Y2));
        OnPropertyChanged(nameof(MidX));
        OnPropertyChanged(nameof(MidY));
    }

    public static RopeViewModel FromData(RopeData d) => new()
    {
        Id = d.Id,
        FromId = d.FromId,
        ToId = d.ToId,
        ColorHex = d.Color,
        Dashed = d.Dashed,
        Label = d.Label,
        InFront = d.InFront
    };

    public RopeData ToData() => new()
    {
        Id = Id,
        FromId = FromId,
        ToId = ToId,
        Color = ColorHex,
        Dashed = Dashed,
        Label = Label,
        InFront = InFront
    };
}
