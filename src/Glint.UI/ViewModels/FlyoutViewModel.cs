using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using Glint.Core.Interfaces;
using Glint.Core.Logging;
using Glint.Core.Models;

namespace Glint.UI.ViewModels;

/// <summary>
/// Top-level viewmodel for the flyout window: per-monitor brightness sliders, master + per-app
/// volume, brightness sync, and presets. Provider event callbacks can arrive on background
/// threads (WMI watcher, NAudio notifications) — all UI-bound collection updates are marshaled
/// onto the UI thread via <see cref="Dispatcher"/>.
/// </summary>
public sealed class FlyoutViewModel : ObservableObject, IDisposable
{
    private readonly IBrightnessProvider _brightnessProvider;
    private readonly IAudioProvider _audioProvider;
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;

    private readonly Dictionary<string, int> _lastKnownBrightness = new();

    private int _masterVolume;
    private bool _isMasterMuted;
    private bool _syncEnabled;
    private bool _anyMonitors;

    public ObservableCollection<MonitorSliderViewModel> Monitors { get; } = new();
    public ObservableCollection<AudioSessionViewModel> AudioSessions { get; } = new();
    public ObservableCollection<PresetViewModel> Presets { get; } = new();

    public ICommand AddPresetCommand { get; }
    public ICommand RefreshCommand { get; }

    public int MasterVolume
    {
        get => _masterVolume;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (SetField(ref _masterVolume, clamped))
            {
                try
                {
                    _audioProvider.MasterVolume = clamped;
                }
                catch (Exception ex)
                {
                    _logger.Error("UI", "Failed to set master volume", ex);
                }
            }
        }
    }

    public bool IsMasterMuted
    {
        get => _isMasterMuted;
        set
        {
            if (SetField(ref _isMasterMuted, value))
            {
                OnPropertyChanged(nameof(IsMasterNotMuted));
                try
                {
                    _audioProvider.IsMasterMuted = value;
                }
                catch (Exception ex)
                {
                    _logger.Error("UI", "Failed to set master mute", ex);
                }
            }
        }
    }

    /// <summary>Inverse of <see cref="IsMasterMuted"/>, used to enable/disable the master volume slider.</summary>
    public bool IsMasterNotMuted => !IsMasterMuted;

    /// <summary>
    /// When enabled, moving any monitor's slider moves all others by the same delta
    /// (lockstep), each independently clamped to 0-100.
    /// </summary>
    public bool SyncEnabled
    {
        get => _syncEnabled;
        set
        {
            if (SetField(ref _syncEnabled, value))
            {
                _settings.Current.SyncEnabled = value;
                _settings.Save();
            }
        }
    }

    /// <summary>True if at least one monitor was detected — used by the view to show an empty-state message.</summary>
    public bool AnyMonitors
    {
        get => _anyMonitors;
        private set
        {
            if (SetField(ref _anyMonitors, value))
                OnPropertyChanged(nameof(NoMonitors));
        }
    }

    /// <summary>Inverse of <see cref="AnyMonitors"/>, for the empty-state message binding.</summary>
    public bool NoMonitors => !AnyMonitors;

    public FlyoutViewModel(IBrightnessProvider brightnessProvider, IAudioProvider audioProvider, ISettingsService settings, IAppLogger logger)
    {
        _brightnessProvider = brightnessProvider;
        _audioProvider = audioProvider;
        _settings = settings;
        _logger = logger;

        AddPresetCommand = new RelayCommand(AddPreset);
        RefreshCommand = new RelayCommand(RefreshAll);

        _syncEnabled = _settings.Current.SyncEnabled;

        LoadMonitors();
        LoadAudioSessions();
        LoadPresets();

        try
        {
            _masterVolume = _audioProvider.MasterVolume;
            _isMasterMuted = _audioProvider.IsMasterMuted;
        }
        catch (Exception ex)
        {
            _logger.Error("UI", "Failed to read initial master volume state", ex);
        }

        _brightnessProvider.MonitorsChanged += OnMonitorsChanged;
        _audioProvider.SessionsChanged += OnSessionsChanged;
        _audioProvider.MasterVolumeChanged += OnMasterVolumeChanged;
    }

    // --- Monitors -----------------------------------------------------------------

    private void LoadMonitors()
    {
        foreach (var existing in Monitors)
        {
            existing.BrightnessChangedByUser -= OnMonitorBrightnessChangedByUser;
            existing.Dispose();
        }
        Monitors.Clear();
        _lastKnownBrightness.Clear();

        IReadOnlyList<MonitorInfo> infos;
        try
        {
            infos = _brightnessProvider.GetMonitors();
        }
        catch (Exception ex)
        {
            _logger.Error("UI", "Failed to load monitors", ex);
            infos = Array.Empty<MonitorInfo>();
        }

        foreach (var info in infos)
        {
            var vm = new MonitorSliderViewModel(info, _brightnessProvider, _logger);
            vm.BrightnessChangedByUser += OnMonitorBrightnessChangedByUser;
            Monitors.Add(vm);
            _lastKnownBrightness[info.Id] = info.Brightness;
        }

        AnyMonitors = Monitors.Count > 0;
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(LoadMonitors);
    }

    /// <summary>
    /// Lockstep sync: when one monitor changes by delta D, every other monitor's brightness
    /// moves by D too, each clamped independently to 0-100. Monitors that hit the clamp
    /// simply stop moving until the anchor reverses direction — this naturally preserves
    /// relative offsets without persisting them.
    /// </summary>
    private void OnMonitorBrightnessChangedByUser(object? sender, int newValue)
    {
        if (sender is not MonitorSliderViewModel changed)
            return;

        var previous = _lastKnownBrightness.TryGetValue(changed.Id, out var p) ? p : newValue;
        var delta = newValue - previous;
        _lastKnownBrightness[changed.Id] = newValue;

        if (!SyncEnabled || delta == 0)
            return;

        foreach (var other in Monitors)
        {
            if (other.Id == changed.Id || !other.IsSupported)
                continue;

            var prevOther = _lastKnownBrightness.TryGetValue(other.Id, out var po) ? po : other.Brightness;
            var target = Math.Clamp(prevOther + delta, 0, 100);

            if (target != other.Brightness)
                other.SetBrightnessSilent(target);

            _lastKnownBrightness[other.Id] = target;
        }
    }

    // --- Audio sessions ------------------------------------------------------------

    private void LoadAudioSessions()
    {
        AudioSessions.Clear();

        IReadOnlyList<AudioSessionInfo> sessions;
        try
        {
            sessions = _audioProvider.GetAudioSessions();
        }
        catch (Exception ex)
        {
            _logger.Error("UI", "Failed to load audio sessions", ex);
            sessions = Array.Empty<AudioSessionInfo>();
        }

        foreach (var info in sessions)
            AudioSessions.Add(new AudioSessionViewModel(info, _audioProvider, _logger));
    }

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                _audioProvider.RefreshSessions();
            }
            catch (Exception ex)
            {
                _logger.Error("UI", "Failed to refresh audio sessions", ex);
            }

            LoadAudioSessions();
        });
    }

    private void OnMasterVolumeChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                SetField(ref _masterVolume, _audioProvider.MasterVolume, nameof(MasterVolume));
                if (SetField(ref _isMasterMuted, _audioProvider.IsMasterMuted, nameof(IsMasterMuted)))
                    OnPropertyChanged(nameof(IsMasterNotMuted));
            }
            catch (Exception ex)
            {
                _logger.Error("UI", "Failed to refresh master volume state", ex);
            }
        });
    }

    // --- Presets ---------------------------------------------------------------

    private void LoadPresets()
    {
        Presets.Clear();

        foreach (var preset in _settings.Current.Presets)
        {
            Presets.Add(CreatePresetViewModel(preset));
        }
    }

    private PresetViewModel CreatePresetViewModel(BrightnessPreset preset)
    {
        var vm = new PresetViewModel(preset, () => ApplyPreset(preset), () => DeletePreset(preset));
        vm.OnRenamed = _settings.Save;
        return vm;
    }

    private void AddPreset()
    {
        var preset = new BrightnessPreset
        {
            Name = $"Preset {_settings.Current.Presets.Count + 1}",
            MonitorBrightness = Monitors
                .Where(m => m.IsSupported)
                .ToDictionary(m => m.Id, m => m.Brightness)
        };

        _settings.Current.Presets.Add(preset);
        _settings.Save();
        Presets.Add(CreatePresetViewModel(preset));
    }

    private void ApplyPreset(BrightnessPreset preset)
    {
        foreach (var monitor in Monitors)
        {
            if (!monitor.IsSupported)
                continue;

            if (preset.MonitorBrightness.TryGetValue(monitor.Id, out var value))
            {
                monitor.SetBrightnessSilent(value);
                _lastKnownBrightness[monitor.Id] = value;
            }
        }
    }

    private void DeletePreset(BrightnessPreset preset)
    {
        _settings.Current.Presets.Remove(preset);
        _settings.Save();

        var vm = Presets.FirstOrDefault(p => p.Model == preset);
        if (vm is not null)
            Presets.Remove(vm);
    }

    // --- Manual refresh ----------------------------------------------------------

    private void RefreshAll()
    {
        try
        {
            _brightnessProvider.RefreshMonitors();
        }
        catch (Exception ex)
        {
            _logger.Error("UI", "Manual monitor refresh failed", ex);
        }

        try
        {
            _audioProvider.RefreshSessions();
        }
        catch (Exception ex)
        {
            _logger.Error("UI", "Manual audio refresh failed", ex);
        }

        LoadAudioSessions();
    }

    public void Dispose()
    {
        _brightnessProvider.MonitorsChanged -= OnMonitorsChanged;
        _audioProvider.SessionsChanged -= OnSessionsChanged;
        _audioProvider.MasterVolumeChanged -= OnMasterVolumeChanged;

        foreach (var monitor in Monitors)
        {
            monitor.BrightnessChangedByUser -= OnMonitorBrightnessChangedByUser;
            monitor.Dispose();
        }
    }
}
