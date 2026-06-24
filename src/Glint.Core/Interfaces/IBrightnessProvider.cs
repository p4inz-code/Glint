using Glint.Core.Models;

namespace Glint.Core.Interfaces;

/// <summary>
/// Abstraction over platform brightness control. The Windows implementation covers
/// external monitors via DDC/CI (Dxva2) and the internal panel via WMI. Left abstracted
/// so a Linux (ddcutil) or macOS provider can be added later without touching the UI layer.
/// </summary>
public interface IBrightnessProvider : IDisposable
{
    /// <summary>
    /// Returns the current set of detected monitors and their last-known brightness.
    /// Cheap to call — enumeration results are cached and refreshed by <see cref="RefreshMonitors"/>
    /// or automatically on hot-plug.
    /// </summary>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>
    /// Forces re-enumeration of displays (used on startup and after a hot-plug event fires,
    /// to rebuild the monitor list and re-query brightness for anything new).
    /// </summary>
    void RefreshMonitors();

    /// <summary>
    /// Sets brightness for the given monitor. Implementations should be safe to call at
    /// high frequency — the caller (slider ViewModel) debounces/throttles, but a provider
    /// must not block the UI thread or throw on a transient hardware failure.
    /// Returns false (and logs) if the write failed; monitor is marked unsupported by the caller
    /// only after repeated failures, not on a single transient error.
    /// </summary>
    bool SetBrightness(string monitorId, int value);

    /// <summary>
    /// Re-queries hardware brightness for a single monitor (used after SetBrightness to
    /// confirm the value actually applied, since some monitors clamp or round).
    /// </summary>
    int? GetBrightness(string monitorId);

    /// <summary>
    /// Raised when the set of connected monitors changes (hot-plug connect/disconnect).
    /// Subscribers should call <see cref="GetMonitors"/> again to get the updated list.
    /// </summary>
    event EventHandler? MonitorsChanged;
}
