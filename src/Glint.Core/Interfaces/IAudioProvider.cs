using Glint.Core.Models;

namespace Glint.Core.Interfaces;

/// <summary>
/// Abstraction over platform audio control: system master volume/mute and per-application
/// (audio session) volume/mute. The Windows implementation wraps NAudio's CoreAudioApi.
/// </summary>
public interface IAudioProvider : IDisposable
{
    /// <summary>Current master output volume, 0-100. Setter applies immediately (master volume isn't debounced).</summary>
    int MasterVolume { get; set; }

    /// <summary>Mute state of the default playback device.</summary>
    bool IsMasterMuted { get; set; }

    /// <summary>
    /// Returns the current list of active per-app audio sessions. Cached and refreshed
    /// via <see cref="RefreshSessions"/> or automatically when sessions are created/destroyed.
    /// </summary>
    IReadOnlyList<AudioSessionInfo> GetAudioSessions();

    /// <summary>Forces re-enumeration of audio sessions (e.g. after <see cref="SessionsChanged"/> fires).</summary>
    void RefreshSessions();

    /// <summary>Sets volume (0-100) for a specific session. No-op and logged if the session no longer exists.</summary>
    void SetSessionVolume(string sessionId, int value);

    /// <summary>Sets mute state for a specific session.</summary>
    void SetSessionMute(string sessionId, bool mute);

    /// <summary>
    /// Raised when audio sessions are created or destroyed (app opened/closed) or the
    /// default playback device changes. Subscribers should call <see cref="RefreshSessions"/>.
    /// </summary>
    event EventHandler? SessionsChanged;

    /// <summary>
    /// Raised when the master volume or mute state changes from outside Glint
    /// (e.g. user used the OS volume mixer or hardware keys) so the slider stays in sync.
    /// </summary>
    event EventHandler? MasterVolumeChanged;
}
