namespace Glint.Core.Models;

/// <summary>
/// Represents a single physical display and its current brightness state.
/// Populated by IBrightnessProvider during enumeration and refreshed on hot-plug events.
/// </summary>
public sealed class MonitorInfo
{
    /// <summary>
    /// Stable identifier for this monitor (device path / instance ID). Used as the key
    /// for settings, presets, and sync offsets — must remain consistent across re-enumeration
    /// for the same physical monitor where possible.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Human-readable name shown in the UI (e.g. "Dell U2721DE" or "Built-in Display").</summary>
    public required string DisplayName { get; init; }

    /// <summary>True if this is the laptop's internal panel (controlled via WMI, not DDC/CI).</summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// False if the monitor did not respond to brightness queries (DDC/CI not supported,
    /// disabled in monitor OSD, or cable doesn't carry DDC). The UI hides the slider
    /// for unsupported monitors rather than showing a non-functional control.
    /// </summary>
    public bool IsSupported { get; set; } = true;

    /// <summary>Current brightness, 0-100. Kept in sync with the hardware via debounced writes.</summary>
    public int Brightness { get; set; }

    /// <summary>Minimum brightness reported by the monitor (almost always 0, but DDC/CI allows otherwise).</summary>
    public int MinBrightness { get; init; } = 0;

    /// <summary>Maximum brightness reported by the monitor (almost always 100).</summary>
    public int MaxBrightness { get; init; } = 100;
}
