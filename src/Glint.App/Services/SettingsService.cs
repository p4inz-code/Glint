using System.Text.Json;
using Glint.Core.Interfaces;
using Glint.Core.Logging;
using Glint.Core.Models;

namespace Glint.App.Services;

/// <summary>
/// Persists <see cref="AppSettings"/> to %AppData%\Glint\settings.json. Writes are atomic
/// (write to a temp file, then move-replace) so a crash or kill mid-write can't corrupt the
/// settings file. A missing or corrupt file silently falls back to defaults — never throws.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IAppLogger _logger;
    private readonly string _settingsPath;
    private readonly object _lock = new();

    public AppSettings Current { get; private set; }

    public SettingsService(IAppLogger logger)
    {
        _logger = logger;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Glint");

        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            _logger.Error("Settings", $"Failed to create settings directory '{dir}'", ex);
        }

        _settingsPath = Path.Combine(dir, "settings.json");
        Current = LoadOrDefault();
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(Current, JsonOptions);
                var tempPath = _settingsPath + ".tmp";

                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _settingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.Error("Settings", "Failed to save settings.json", ex);
            }
        }
    }

    public void Reload()
    {
        lock (_lock)
        {
            Current = LoadOrDefault();
        }
    }

    private AppSettings LoadOrDefault()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

            if (settings is null)
            {
                _logger.Warn("Settings", "settings.json deserialized to null — using defaults");
                return new AppSettings();
            }

            return settings;
        }
        catch (Exception ex)
        {
            _logger.Warn("Settings", "settings.json missing or corrupt — using defaults", ex);
            return new AppSettings();
        }
    }
}
