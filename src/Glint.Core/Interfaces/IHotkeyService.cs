using Glint.Core.Models;

namespace Glint.Core.Interfaces;

/// <summary>Logical actions a global hotkey can trigger.</summary>
public enum HotkeyAction
{
    BrightnessUp,
    BrightnessDown,
    VolumeUp,
    VolumeDown
}

/// <summary>
/// Registers OS-level global hotkeys (active even when Glint's flyout isn't focused)
/// and raises <see cref="HotkeyPressed"/> when one fires. The Windows implementation
/// uses a hidden message-only window + RegisterHotKey/WM_HOTKEY.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Registers (or re-registers) hotkeys from the given settings. Safe to call again
    /// after the user changes a binding — unregisters old bindings first. Skips entries
    /// where <see cref="HotkeyCombo.IsEmpty"/> is true. Logs (does not throw) if a
    /// combination is already claimed by another application.
    /// </summary>
    void RegisterHotkeys(HotkeySettings hotkeys);

    event EventHandler<HotkeyAction>? HotkeyPressed;
}
