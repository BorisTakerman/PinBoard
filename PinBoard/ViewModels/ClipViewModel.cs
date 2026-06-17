using System.IO;
using PinBoard.Models;

namespace PinBoard.ViewModels;

/// <summary>
/// An image or document clip pinned to the board. At runtime it holds an
/// absolute file path; persistence stores a path relative to the .board file
/// (handled by <see cref="BoardViewModel"/>).
/// </summary>
public sealed class ClipViewModel : ObservableObject, IRopeAnchor
{
    private double _x, _y, _width = 180, _height = 180;
    private int _zIndex;
    private bool _isSelected;
    private string _absolutePath = string.Empty;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ClipType Type { get; init; } = ClipType.Image;

    /// <summary>Absolute path to the source file (runtime only).</summary>
    public string AbsolutePath
    {
        get => _absolutePath;
        set
        {
            if (SetProperty(ref _absolutePath, value))
                OnPropertyChanged(nameof(FileName));
        }
    }

    public string FileName => string.IsNullOrEmpty(AbsolutePath) ? string.Empty : Path.GetFileName(AbsolutePath);
    public string Extension => string.IsNullOrEmpty(AbsolutePath) ? string.Empty : Path.GetExtension(AbsolutePath).ToLowerInvariant();

    public double X { get => _x; set => SetProperty(ref _x, value); }
    public double Y { get => _y; set => SetProperty(ref _y, value); }
    public double Width { get => _width; set => SetProperty(ref _width, value); }
    public double Height { get => _height; set => SetProperty(ref _height, value); }
    public int ZIndex { get => _zIndex; set => SetProperty(ref _zIndex, value); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public ClipData ToData(string relativePath) => new()
    {
        Id = Id,
        Type = Type,
        RelativePath = relativePath,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        ZIndex = ZIndex
    };

    public static ClipViewModel FromData(ClipData d, string absolutePath) => new()
    {
        Id = d.Id,
        Type = d.Type,
        AbsolutePath = absolutePath,
        X = d.X,
        Y = d.Y,
        Width = d.Width,
        Height = d.Height,
        ZIndex = d.ZIndex
    };
}
