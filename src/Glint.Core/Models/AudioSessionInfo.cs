namespace Glint.Core.Models;

/// <summary>
/// Represents a single per-application audio session (e.g. Spotify, a browser tab group, a game).
/// One entry per process exposing an audio session via the Windows audio engine.
/// </summary>
public sealed class AudioSessionInfo
{
    /// <summary>Audio session identifier (process-instance scoped). Not stable across app restarts.</summary>
    public required string SessionId { get; init; }

    /// <summary>Underlying process ID — used to refresh/match sessions on re-enumeration.</summary>
    public int ProcessId { get; init; }

    /// <summary>Executable name without extension (e.g. "spotify"), used for grouping/icon lookup.</summary>
    public required string ProcessName { get; init; }

    /// <summary>Friendly display name shown in the UI (falls back to ProcessName if unavailable).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Current session volume, 0-100.</summary>
    public int Volume { get; set; }

    /// <summary>Whether this session is currently muted.</summary>
    public bool IsMuted { get; set; }
}
