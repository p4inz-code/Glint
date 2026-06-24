namespace Glint.Core.Models;

/// <summary>
/// A saved snapshot of brightness levels across all monitors, switchable from the flyout
/// with a single click (e.g. "Day", "Night", "Movie").
/// </summary>
public sealed class BrightnessPreset
{
    /// <summary>User-assigned name, shown on the preset button.</summary>
    public required string Name { get; set; }

    /// <summary>Brightness value (0-100) per monitor, keyed by <see cref="MonitorInfo.Id"/>.</summary>
    public Dictionary<string, int> MonitorBrightness { get; set; } = new();
}
