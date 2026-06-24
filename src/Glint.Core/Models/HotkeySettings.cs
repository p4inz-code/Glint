namespace Glint.Core.Models;

/// <summary>
/// Modifier keys for global hotkeys, matching the Win32 MOD_* constants used by
/// RegisterHotKey (MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8) so the Windows
/// hotkey service can cast directly without translation tables.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

/// <summary>
/// A single global hotkey definition. <see cref="Key"/> is stored as the .NET
/// <c>System.Windows.Forms.Keys</c> / virtual-key name (e.g. "Up", "Down", "Add", "Subtract")
/// so it maps 1:1 to a Win32 virtual-key code without a UI-framework dependency in Core.
/// </summary>
public sealed class HotkeyCombo
{
    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.None;

    /// <summary>Virtual-key name, or empty string if this hotkey is disabled.</summary>
    public string Key { get; set; } = string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(Key);

    public override string ToString()
    {
        if (IsEmpty) return "(none)";
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}

/// <summary>
/// The full set of remappable global shortcuts. Step sizes are intentionally small (2%)
/// so repeated key presses feel like fine control rather than jumps.
/// </summary>
public sealed class HotkeySettings
{
    public HotkeyCombo BrightnessUp { get; set; } = new() { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, Key = "Up" };
    public HotkeyCombo BrightnessDown { get; set; } = new() { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, Key = "Down" };
    public HotkeyCombo VolumeUp { get; set; } = new() { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, Key = "Right" };
    public HotkeyCombo VolumeDown { get; set; } = new() { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, Key = "Left" };

    /// <summary>Step size in brightness/volume percent applied per hotkey press.</summary>
    public int StepSize { get; set; } = 2;
}
