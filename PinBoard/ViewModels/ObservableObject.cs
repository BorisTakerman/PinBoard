using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PinBoard.ViewModels;

/// <summary>
/// Minimal INotifyPropertyChanged base for all ViewModels.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Sets a backing field and raises <see cref="PropertyChanged"/> when the value changes.
    /// Returns true if the value actually changed.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
