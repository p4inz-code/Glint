namespace Glint.Core.Models;

/// <summary>
/// Root settings object, serialized to %AppData%\Glint\settings.json.
/// <see cref="SchemaVersion"/> exists so future versions can migrate old files instead of
/// silently discarding user settings on upgrade.
/// </summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// When true, dragging any monitor's slider moves all other monitors by the same delta
    /// (lockstep), each clamped independently to 0-100. This preserves relative brightness
    /// differences between monitors without needing separately persisted offsets.
    /// </summary>
    public bool SyncEnabled { get; set; } = false;

    /// <summary>Saved brightness presets, shown as quick-switch buttons in the flyout.</summary>
    public List<BrightnessPreset> Presets { get; set; } = new();

    /// <summary>Global hotkey bindings.</summary>
    public HotkeySettings Hotkeys { get; set; } = new();

    /// <summary>Enables DEBUG-level entries in the log file. Off by default to keep logs small.</summary>
    public bool DebugLoggingEnabled { get; set; } = false;

    /// <summary>Start Glint with Windows (P1 — wired in Session 2, field reserved now so the schema is stable).</summary>
    public bool AutoStartEnabled { get; set; } = false;

    /// <summary>Last known position of the flyout window (screen coordinates), so it reopens where the user left it.</summary>
    public double? FlyoutX { get; set; }
    public double? FlyoutY { get; set; }
}
