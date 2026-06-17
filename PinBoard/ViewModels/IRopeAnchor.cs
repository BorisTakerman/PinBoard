using System.ComponentModel;

namespace PinBoard.ViewModels;

/// <summary>
/// Anything a rope can attach to (notes and clips). Exposes the geometry the
/// rope needs and notifies when it moves so the rope can re-track live.
/// </summary>
public interface IRopeAnchor : INotifyPropertyChanged
{
    string Id { get; }
    double X { get; }
    double Y { get; }
    double Width { get; }
    double Height { get; }
}
