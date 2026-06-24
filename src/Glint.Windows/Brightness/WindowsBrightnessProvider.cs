using System.Management;
using System.Runtime.InteropServices;
using Glint.Core.Interfaces;
using Glint.Core.Logging;
using Glint.Core.Models;

namespace Glint.Windows.Brightness;

/// <summary>
/// Windows implementation of <see cref="IBrightnessProvider"/>. External monitors are
/// controlled via DDC/CI (Dxva2); the internal laptop panel (which usually does not expose
/// DDC/CI) falls back to the WMI WmiMonitorBrightness* classes. Monitors that support
/// neither are surfaced with <see cref="MonitorInfo.IsSupported"/> = false so the UI can
/// hide their slider instead of showing a dead control.
/// </summary>
public sealed class WindowsBrightnessProvider : IBrightnessProvider
{
    /// <summary>Consecutive SetBrightness failures before a monitor is marked unsupported at runtime.</summary>
    private const int MaxConsecutiveFailures = 3;

    private readonly IAppLogger _logger;
    private readonly object _lock = new();
    private readonly List<MonitorEntry> _monitors = new();

    private ManagementEventWatcher? _hotPlugWatcher;
    private bool _internalAssigned;

    public event EventHandler? MonitorsChanged;

    private sealed class MonitorEntry
    {
        public required MonitorInfo Info { get; init; }
        public IntPtr PhysicalHandle { get; init; }
        public bool IsInternal { get; init; }
        public int FailureCount { get; set; }
    }

    public WindowsBrightnessProvider(IAppLogger logger)
    {
        _logger = logger;
        RefreshMonitors();
        StartHotPlugWatcher();
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        lock (_lock)
        {
            return _monitors.Select(m => m.Info).ToList();
        }
    }

    public void RefreshMonitors()
    {
        lock (_lock)
        {
            ReleasePhysicalHandlesLocked();
            _monitors.Clear();
            _internalAssigned = false;

            try
            {
                Dxva2Interop.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumMonitorCallback, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _logger.Error("Brightness", "Monitor enumeration failed", ex);
            }
        }

        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool EnumMonitorCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Dxva2Interop.RECT lprcMonitor, IntPtr dwData)
    {
        try
        {
            var mi = new Dxva2Interop.MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(Dxva2Interop.MONITORINFOEX)) };
            if (!Dxva2Interop.GetMonitorInfo(hMonitor, ref mi))
            {
                _logger.Warn("Brightness", $"GetMonitorInfo failed for handle 0x{hMonitor:X}");
                return true; // continue enumerating other monitors
            }

            uint physicalCount = 0;
            if (!Dxva2Interop.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref physicalCount) || physicalCount == 0)
            {
                _logger.Debug("Brightness", $"No physical monitors reported for {mi.szDevice}");
                return true;
            }

            var physicalMonitors = new Dxva2Interop.PHYSICAL_MONITOR[physicalCount];
            if (!Dxva2Interop.GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalCount, physicalMonitors))
            {
                _logger.Warn("Brightness", $"GetPhysicalMonitorsFromHMONITOR failed for {mi.szDevice}");
                return true;
            }

            var friendlyName = MonitorNameResolver.Resolve(mi.szDevice);

            for (int i = 0; i < physicalMonitors.Length; i++)
            {
                var id = $"{mi.szDevice}#{i}";
                var entry = BuildMonitorEntry(id, physicalMonitors[i].hPhysicalMonitor, mi.szDevice, friendlyName, physicalMonitors[i].szPhysicalMonitorDescription);
                _monitors.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Brightness", "Error processing a monitor during enumeration", ex);
        }

        return true; // returning true continues enumeration
    }

    private MonitorEntry BuildMonitorEntry(string id, IntPtr handle, string gdiDevice, string? friendlyName, string physicalDescription)
    {
        var displayName = ResolveDisplayName(friendlyName, physicalDescription, gdiDevice);

        uint caps = 0, colorTemps = 0;
        bool capsOk = Dxva2Interop.GetMonitorCapabilities(handle, ref caps, ref colorTemps);
        bool ddcBrightnessSupported = capsOk && (caps & Dxva2Interop.MC_CAPS_BRIGHTNESS) != 0;

        if (ddcBrightnessSupported)
        {
            uint min = 0, cur = 0, max = 100;
            if (Dxva2Interop.GetMonitorBrightness(handle, ref min, ref cur, ref max))
            {
                return new MonitorEntry
                {
                    Info = new MonitorInfo
                    {
                        Id = id,
                        DisplayName = displayName,
                        IsInternal = false,
                        IsSupported = true,
                        Brightness = (int)cur,
                        MinBrightness = (int)min,
                        MaxBrightness = max == 0 ? 100 : (int)max
                    },
                    PhysicalHandle = handle,
                    IsInternal = false
                };
            }

            _logger.Warn("Brightness", $"'{displayName}' reports brightness capability but GetMonitorBrightness failed");
        }

        // DDC/CI brightness unavailable. If this looks like the laptop's internal panel
        // (first monitor without DDC/CI brightness support) and WMI brightness is exposed,
        // route this monitor through WMI instead.
        if (!_internalAssigned)
        {
            var wmiBrightness = WmiInternalBrightness.GetBrightness();
            if (wmiBrightness.HasValue)
            {
                _internalAssigned = true;
                return new MonitorEntry
                {
                    Info = new MonitorInfo
                    {
                        Id = id,
                        DisplayName = string.IsNullOrWhiteSpace(friendlyName) ? "Built-in Display" : friendlyName,
                        IsInternal = true,
                        IsSupported = true,
                        Brightness = wmiBrightness.Value,
                        MinBrightness = 0,
                        MaxBrightness = 100
                    },
                    PhysicalHandle = handle,
                    IsInternal = true
                };
            }
        }

        // Neither DDC/CI nor WMI worked for this monitor — surface it as unsupported
        // rather than showing a slider that silently does nothing (Section 6).
        _logger.Info("Brightness", $"'{displayName}' does not support brightness control (no DDC/CI, no WMI match)");
        return new MonitorEntry
        {
            Info = new MonitorInfo
            {
                Id = id,
                DisplayName = displayName,
                IsInternal = false,
                IsSupported = false,
                Brightness = 0,
                MinBrightness = 0,
                MaxBrightness = 100
            },
            PhysicalHandle = handle,
            IsInternal = false
        };
    }

    private static string ResolveDisplayName(string? friendlyName, string physicalDescription, string gdiDevice)
    {
        if (!string.IsNullOrWhiteSpace(friendlyName))
            return friendlyName;

        if (!string.IsNullOrWhiteSpace(physicalDescription) && physicalDescription != "Generic PnP Monitor")
            return physicalDescription;

        return $"Display {gdiDevice.Replace(@"\\.\", "")}";
    }

    public bool SetBrightness(string monitorId, int value)
    {
        value = Math.Clamp(value, 0, 100);

        MonitorEntry? entry;
        lock (_lock)
        {
            entry = _monitors.FirstOrDefault(m => m.Info.Id == monitorId);
        }

        if (entry is null)
        {
            _logger.Warn("Brightness", $"SetBrightness called for unknown monitor '{monitorId}'");
            return false;
        }

        if (!entry.Info.IsSupported)
            return false;

        bool ok;
        try
        {
            ok = entry.IsInternal
                ? WmiInternalBrightness.SetBrightness(value)
                : Dxva2Interop.SetMonitorBrightness(entry.PhysicalHandle, (uint)value);
        }
        catch (Exception ex)
        {
            _logger.Error("Brightness", $"SetBrightness threw for '{monitorId}'", ex);
            ok = false;
        }

        bool notifyChanged = false;

        lock (_lock)
        {
            if (ok)
            {
                entry.Info.Brightness = value;
                entry.FailureCount = 0;
            }
            else
            {
                entry.FailureCount++;
                _logger.Warn("Brightness", $"SetBrightness failed for '{monitorId}' ({entry.FailureCount}/{MaxConsecutiveFailures})");

                if (entry.FailureCount >= MaxConsecutiveFailures)
                {
                    entry.Info.IsSupported = false;
                    _logger.Error("Brightness", $"Monitor '{monitorId}' marked unsupported after {MaxConsecutiveFailures} consecutive failures");
                    notifyChanged = true;
                }
            }
        }

        if (notifyChanged)
            MonitorsChanged?.Invoke(this, EventArgs.Empty);

        return ok;
    }

    public int? GetBrightness(string monitorId)
    {
        MonitorEntry? entry;
        lock (_lock)
        {
            entry = _monitors.FirstOrDefault(m => m.Info.Id == monitorId);
        }

        if (entry is null || !entry.Info.IsSupported)
            return null;

        try
        {
            if (entry.IsInternal)
                return WmiInternalBrightness.GetBrightness();

            uint min = 0, cur = 0, max = 100;
            return Dxva2Interop.GetMonitorBrightness(entry.PhysicalHandle, ref min, ref cur, ref max) ? (int)cur : null;
        }
        catch (Exception ex)
        {
            _logger.Error("Brightness", $"GetBrightness threw for '{monitorId}'", ex);
            return null;
        }
    }

    private void StartHotPlugWatcher()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2 OR EventType = 3");
            _hotPlugWatcher = new ManagementEventWatcher(query);
            _hotPlugWatcher.EventArrived += (_, _) =>
            {
                _logger.Info("Brightness", "Display device change detected — re-enumerating monitors");
                RefreshMonitors();
            };
            _hotPlugWatcher.Start();
        }
        catch (Exception ex)
        {
            _logger.Warn("Brightness", "Could not start hot-plug watcher; monitor list will only update on manual refresh", ex);
        }
    }

    private void ReleasePhysicalHandlesLocked()
    {
        foreach (var monitor in _monitors)
        {
            if (monitor.PhysicalHandle == IntPtr.Zero)
                continue;

            try
            {
                var arr = new[]
                {
                    new Dxva2Interop.PHYSICAL_MONITOR
                    {
                        hPhysicalMonitor = monitor.PhysicalHandle,
                        szPhysicalMonitorDescription = string.Empty
                    }
                };
                Dxva2Interop.DestroyPhysicalMonitors(1, arr);
            }
            catch (Exception ex)
            {
                _logger.Warn("Brightness", "Failed to release a physical monitor handle", ex);
            }
        }
    }

    public void Dispose()
    {
        _hotPlugWatcher?.Stop();
        _hotPlugWatcher?.Dispose();

        lock (_lock)
        {
            ReleasePhysicalHandlesLocked();
            _monitors.Clear();
        }
    }
}
