using Glint.Core.Interfaces;
using Glint.Core.Logging;
using Glint.Core.Models;

namespace Glint.UI.ViewModels;

/// <summary>
/// Wraps a single <see cref="AudioSessionInfo"/> (one running app's audio session) for the
/// per-app volume list. WASAPI session volume calls are cheap (no hardware bus), so unlike
/// <see cref="MonitorSliderViewModel"/> these are applied directly without throttling.
/// </summary>
public sealed class AudioSessionViewModel : ObservableObject
{
    private readonly IAudioProvider _provider;
    private readonly IAppLogger _logger;

    private int _volume;
    private bool _isMuted;

    public string SessionId { get; }
    public string ProcessName { get; }
    public string DisplayName { get; }

    public int Volume
    {
        get => _volume;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (SetField(ref _volume, clamped))
            {
                try
                {
                    _provider.SetSessionVolume(SessionId, clamped);
                }
                catch (Exception ex)
                {
                    _logger.Error("UI", $"Failed to set volume for session '{SessionId}'", ex);
                }
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetField(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(IsNotMuted));
                try
                {
                    _provider.SetSessionMute(SessionId, value);
                }
                catch (Exception ex)
                {
                    _logger.Error("UI", $"Failed to set mute for session '{SessionId}'", ex);
                }
            }
        }
    }

    /// <summary>Inverse of <see cref="IsMuted"/>, used to enable/disable the volume slider.</summary>
    public bool IsNotMuted => !IsMuted;

    public AudioSessionViewModel(AudioSessionInfo info, IAudioProvider provider, IAppLogger logger)
    {
        SessionId = info.SessionId;
        ProcessName = info.ProcessName;
        DisplayName = info.DisplayName;
        _volume = info.Volume;
        _isMuted = info.IsMuted;
        _provider = provider;
        _logger = logger;
    }
}
