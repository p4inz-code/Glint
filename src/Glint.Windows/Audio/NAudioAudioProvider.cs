using Glint.Core.Interfaces;
using Glint.Core.Logging;
using Glint.Core.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Glint.Windows.Audio;

/// <summary>
/// Windows implementation of <see cref="IAudioProvider"/> using NAudio's CoreAudioApi
/// (WASAPI). Master volume operates on the default render endpoint; per-app sessions come
/// from that endpoint's <see cref="AudioSessionManager"/>. If a session's process exits
/// mid-operation, calls are skipped and logged rather than throwing (Section 6).
/// </summary>
public sealed class NAudioAudioProvider : IAudioProvider, IMMNotificationClient
{
    private readonly IAppLogger _logger;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly object _lock = new();
    private readonly Dictionary<string, AudioSessionControl> _sessionControls = new();
    private readonly HashSet<string> _registeredEventSessions = new();

    private MMDevice? _device;
    private List<AudioSessionInfo> _sessions = new();
    private bool _disposed;

    public event EventHandler? SessionsChanged;
    public event EventHandler? MasterVolumeChanged;

    public NAudioAudioProvider(IAppLogger logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();

        try
        {
            _enumerator.RegisterEndpointNotificationCallback(this);
        }
        catch (Exception ex)
        {
            _logger.Warn("Audio", "Could not register endpoint notification callback (default device changes won't auto-refresh)", ex);
        }

        InitializeDevice();
    }

    private void InitializeDevice()
    {
        lock (_lock)
        {
            try
            {
                _device?.AudioEndpointVolume.OnVolumeNotification -= OnMasterVolumeNotification;
                _device?.Dispose();

                _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                _device.AudioEndpointVolume.OnVolumeNotification += OnMasterVolumeNotification;

                RefreshSessionsLocked();
            }
            catch (Exception ex)
            {
                _logger.Error("Audio", "Failed to initialize default playback device", ex);
                _device = null;
                _sessions = new List<AudioSessionInfo>();
                _sessionControls.Clear();
            }
        }
    }

    public int MasterVolume
    {
        get
        {
            lock (_lock)
            {
                if (_device is null) return 0;

                try
                {
                    return (int)Math.Round(_device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                }
                catch (Exception ex)
                {
                    _logger.Error("Audio", "Failed to read master volume", ex);
                    return 0;
                }
            }
        }
        set
        {
            lock (_lock)
            {
                if (_device is null) return;

                try
                {
                    _device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0, 100) / 100f;
                }
                catch (Exception ex)
                {
                    _logger.Error("Audio", "Failed to set master volume", ex);
                }
            }
        }
    }

    public bool IsMasterMuted
    {
        get
        {
            lock (_lock)
            {
                try
                {
                    return _device?.AudioEndpointVolume.Mute ?? false;
                }
                catch (Exception ex)
                {
                    _logger.Error("Audio", "Failed to read master mute state", ex);
                    return false;
                }
            }
        }
        set
        {
            lock (_lock)
            {
                if (_device is null) return;

                try
                {
                    _device.AudioEndpointVolume.Mute = value;
                }
                catch (Exception ex)
                {
                    _logger.Error("Audio", "Failed to set master mute state", ex);
                }
            }
        }
    }

    public IReadOnlyList<AudioSessionInfo> GetAudioSessions()
    {
        lock (_lock)
        {
            return _sessions.ToList();
        }
    }

    public void RefreshSessions()
    {
        lock (_lock)
        {
            RefreshSessionsLocked();
        }
    }

    /// <summary>Must be called with <see cref="_lock"/> held.</summary>
    private void RefreshSessionsLocked()
    {
        var updated = new List<AudioSessionInfo>();
        var updatedControls = new Dictionary<string, AudioSessionControl>();

        if (_device is null)
        {
            _sessions = updated;
            _sessionControls.Clear();
            return;
        }

        try
        {
            var sessionManager = _device.AudioSessionManager;
            var sessions = sessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];

                    if (session.State == AudioSessionState.AudioSessionStateExpired)
                        continue;

                    int pid = (int)session.GetProcessID;
                    if (pid == 0)
                        continue; // skip the system sounds session

                    string processName = "unknown";
                    try
                    {
                        using var process = System.Diagnostics.Process.GetProcessById(pid);
                        processName = process.ProcessName;
                    }
                    catch
                    {
                        // Process exited between enumeration and lookup — session will be
                        // dropped on the next refresh anyway; keep the generic name for now.
                    }

                    var displayNameRaw = session.DisplayName;
                    var displayName = string.IsNullOrWhiteSpace(displayNameRaw) ? processName : displayNameRaw;
                    var sessionId = $"{pid}:{session.GetSessionIdentifier}";

                    updatedControls[sessionId] = session;
                    updated.Add(new AudioSessionInfo
                    {
                        SessionId = sessionId,
                        ProcessId = pid,
                        ProcessName = processName,
                        DisplayName = displayName,
                        Volume = (int)Math.Round(session.SimpleAudioVolume.Volume * 100),
                        IsMuted = session.SimpleAudioVolume.Mute
                    });

                    if (!_registeredEventSessions.Contains(sessionId))
                    {
                        session.RegisterEventClient(new SessionEventsHandler(this));
                        _registeredEventSessions.Add(sessionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("Audio", "Skipped an audio session during enumeration", ex);
                }
            }

            sessionManager.OnSessionCreated -= OnSessionCreated;
            sessionManager.OnSessionCreated += OnSessionCreated;
        }
        catch (Exception ex)
        {
            _logger.Error("Audio", "Failed to enumerate audio sessions", ex);
        }

        _sessions = updated;
        _sessionControls.Clear();
        foreach (var (key, value) in updatedControls)
            _sessionControls[key] = value;
    }

    public void SetSessionVolume(string sessionId, int value)
    {
        lock (_lock)
        {
            if (!_sessionControls.TryGetValue(sessionId, out var session))
            {
                _logger.Warn("Audio", $"SetSessionVolume: session '{sessionId}' no longer exists");
                return;
            }

            try
            {
                session.SimpleAudioVolume.Volume = Math.Clamp(value, 0, 100) / 100f;
                var cached = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
                if (cached is not null)
                    cached.Volume = value;
            }
            catch (Exception ex)
            {
                _logger.Error("Audio", $"Failed to set volume for session '{sessionId}'", ex);
            }
        }
    }

    public void SetSessionMute(string sessionId, bool mute)
    {
        lock (_lock)
        {
            if (!_sessionControls.TryGetValue(sessionId, out var session))
            {
                _logger.Warn("Audio", $"SetSessionMute: session '{sessionId}' no longer exists");
                return;
            }

            try
            {
                session.SimpleAudioVolume.Mute = mute;
                var cached = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
                if (cached is not null)
                    cached.IsMuted = mute;
            }
            catch (Exception ex)
            {
                _logger.Error("Audio", $"Failed to set mute for session '{sessionId}'", ex);
            }
        }
    }

    private void OnMasterVolumeNotification(AudioVolumeNotificationData data)
    {
        MasterVolumeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionCreated(object? sender, IAudioSessionControl newSession)
    {
        _logger.Debug("Audio", "New audio session created");
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Forwards session state changes (e.g. a process closing) up as <see cref="SessionsChanged"/>.</summary>
    private sealed class SessionEventsHandler : IAudioSessionEventsHandler
    {
        private readonly NAudioAudioProvider _owner;
        public SessionEventsHandler(NAudioAudioProvider owner) => _owner = owner;

        public void OnVolumeChanged(float volume, bool isMuted) { }
        public void OnDisplayNameChanged(string displayName) { }
        public void OnIconPathChanged(string iconPath) { }
        public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) { }
        public void OnGroupingParamChanged(ref Guid groupingId) { }
        public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
            => _owner.SessionsChanged?.Invoke(_owner, EventArgs.Empty);

        public void OnStateChanged(AudioSessionState state)
        {
            if (state == AudioSessionState.AudioSessionStateExpired)
                _owner.SessionsChanged?.Invoke(_owner, EventArgs.Empty);
        }
    }

    // --- IMMNotificationClient: re-initialize when the default playback device changes ---

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
    public void OnDeviceAdded(string pwstrDeviceId) { }
    public void OnDeviceRemoved(string deviceId) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow != DataFlow.Render)
            return;

        _logger.Info("Audio", "Default playback device changed — reinitializing audio provider");
        InitializeDevice();
        MasterVolumeChanged?.Invoke(this, EventArgs.Empty);
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(this);
        }
        catch (Exception ex)
        {
            _logger.Warn("Audio", "Failed to unregister endpoint notification callback during shutdown", ex);
        }

        lock (_lock)
        {
            try
            {
                if (_device is not null)
                    _device.AudioEndpointVolume.OnVolumeNotification -= OnMasterVolumeNotification;
                _device?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Warn("Audio", "Failed to dispose audio device cleanly", ex);
            }

            _sessionControls.Clear();
            _sessions.Clear();
        }

        _enumerator.Dispose();
    }
}
