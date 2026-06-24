using Glint.Core.Interfaces;
using Glint.Core.Logging;
using Glint.Core.Models;

namespace Glint.UI.ViewModels;

/// <summary>
/// Wraps a single <see cref="MonitorInfo"/> for display in the flyout. Brightness writes to
/// hardware are throttled to ~12/sec (ThrottleMs=80) per Section 7 so dragging the slider
/// feels immediate without hammering DDC/CI. The actual hardware call runs on a background
/// thread so it never blocks the UI.
/// </summary>
public sealed class MonitorSliderViewModel : ObservableObject, IDisposable
{
    private const int ThrottleMs = 80; // ~12 writes/sec, within the 10-15/sec target

    private readonly IBrightnessProvider _provider;
    private readonly IAppLogger _logger;
    private readonly object _debounceLock = new();

    private int _brightness;
    private DateTime _lastHardwareWrite = DateTime.MinValue;
    private Timer? _debounceTimer;
    private int _pendingValue;
    private bool _hasPending;

    public string Id { get; }
    public string DisplayName { get; }
    public bool IsInternal { get; }
    public bool IsSupported { get; }

    /// <summary>Inverse of <see cref="IsSupported"/>, for the "not supported" message binding.</summary>
    public bool IsUnsupported => !IsSupported;

    /// <summary>
    /// Raised whenever the user moves this slider (not raised for <see cref="SetBrightnessSilent"/>
    /// updates). The parent <see cref="FlyoutViewModel"/> listens to this for sync propagation.
    /// </summary>
    public event EventHandler<int>? BrightnessChangedByUser;

    public int Brightness
    {
        get => _brightness;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (SetField(ref _brightness, clamped))
            {
                ScheduleHardwareWrite(clamped);
                BrightnessChangedByUser?.Invoke(this, clamped);
            }
        }
    }

    public MonitorSliderViewModel(MonitorInfo info, IBrightnessProvider provider, IAppLogger logger)
    {
        Id = info.Id;
        DisplayName = info.DisplayName;
        IsInternal = info.IsInternal;
        IsSupported = info.IsSupported;
        _brightness = info.Brightness;
        _provider = provider;
        _logger = logger;
    }

    /// <summary>
    /// Updates the displayed brightness from an external source (sync propagation from
    /// another monitor, or a refresh after hot-plug) and still writes to hardware, but
    /// without raising <see cref="BrightnessChangedByUser"/> — avoids feedback loops when
    /// sync is propagating a change across monitors.
    /// </summary>
    public void SetBrightnessSilent(int value)
    {
        var clamped = Math.Clamp(value, 0, 100);
        if (SetField(ref _brightness, clamped, nameof(Brightness)))
        {
            ScheduleHardwareWrite(clamped);
        }
    }

    private void ScheduleHardwareWrite(int value)
    {
        lock (_debounceLock)
        {
            _pendingValue = value;
            _hasPending = true;

            var elapsedMs = (DateTime.UtcNow - _lastHardwareWrite).TotalMilliseconds;
            if (elapsedMs >= ThrottleMs)
            {
                FlushPending();
            }
            else if (_debounceTimer is null)
            {
                var delay = Math.Max(1, ThrottleMs - elapsedMs);
                _debounceTimer = new Timer(_ => FlushPending(), null, (int)delay, Timeout.Infinite);
            }
            // If a timer is already pending, _pendingValue has been updated above and
            // will be picked up when that timer fires — no need to schedule another.
        }
    }

    private void FlushPending()
    {
        int value;
        lock (_debounceLock)
        {
            if (!_hasPending) return;

            value = _pendingValue;
            _hasPending = false;
            _lastHardwareWrite = DateTime.UtcNow;

            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        Task.Run(() =>
        {
            try
            {
                _provider.SetBrightness(Id, value);
            }
            catch (Exception ex)
            {
                _logger.Error("UI", $"Failed to apply brightness for '{Id}'", ex);
            }
        });
    }

    public void Dispose()
    {
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }
}
