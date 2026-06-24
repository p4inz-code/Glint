using System.Windows.Input;
using Glint.Core.Models;

namespace Glint.UI.ViewModels;

/// <summary>
/// Wraps a <see cref="BrightnessPreset"/> for the presets row in the flyout. The name is
/// editable inline (bound to a TextBox); apply/delete are handled by the owning
/// <see cref="FlyoutViewModel"/> via the callbacks passed into the constructor.
/// </summary>
public sealed class PresetViewModel : ObservableObject
{
    private string _name;

    /// <summary>The underlying model — mutated in place so the settings file stays in sync.</summary>
    public BrightnessPreset Model { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
            {
                Model.Name = value;
                OnRenamed?.Invoke();
            }
        }
    }

    public ICommand ApplyCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <summary>Invoked after <see cref="Name"/> changes, so the owner can persist settings.</summary>
    public Action? OnRenamed { get; set; }

    public PresetViewModel(BrightnessPreset model, Action apply, Action delete)
    {
        Model = model;
        _name = model.Name;
        ApplyCommand = new RelayCommand(apply);
        DeleteCommand = new RelayCommand(delete);
    }
}
