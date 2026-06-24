using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Glint.UI.ViewModels;

/// <summary>
/// Minimal INotifyPropertyChanged base class. Avoids pulling in CommunityToolkit.Mvvm or
/// ReactiveUI to keep the dependency list to Avalonia + audio/brightness interop only,
/// per the project's locked dependency rules.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Sets <paramref name="field"/> to <paramref name="value"/> and raises
    /// <see cref="PropertyChanged"/> if the value actually changed. Returns true if changed.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
