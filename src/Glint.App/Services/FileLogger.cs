using System.Text;
using Glint.Core.Logging;

namespace Glint.App.Services;

/// <summary>
/// Writes timestamped log entries to %AppData%\Glint\logs\glint-{timestamp}.log.
/// One file per app run; the newest <see cref="MaxSessionFiles"/> files are kept.
/// Each file is capped at <see cref="MaxFileSizeBytes"/> — once reached, further entries
/// for that session are dropped (with a single notice line) rather than growing unbounded.
/// Never throws: if the log directory can't be created, the app runs with logging disabled.
/// </summary>
public sealed class FileLogger : IAppLogger, IDisposable
{
    private const int MaxSessionFiles = 5;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    private readonly object _lock = new();
    private readonly StreamWriter? _writer;
    private bool _sizeLimitReached;

    public LogLevel MinimumLevel { get; set; }

    public string LogDirectory { get; }
    public string? CurrentLogFilePath { get; }

    public FileLogger(bool debugLoggingEnabled)
    {
        MinimumLevel = debugLoggingEnabled ? LogLevel.Debug : LogLevel.Info;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        LogDirectory = Path.Combine(appData, "Glint", "logs");

        try
        {
            Directory.CreateDirectory(LogDirectory);
            RotateOldSessions();

            var fileName = $"glint-{DateTime.Now:yyyyMMdd-HHmmss}.log";
            CurrentLogFilePath = Path.Combine(LogDirectory, fileName);

            _writer = new StreamWriter(CurrentLogFilePath, append: true, Encoding.UTF8) { AutoFlush = true };
            _writer.WriteLine($"=== Glint session started {DateTime.Now:O} ===");
        }
        catch
        {
            // No writable log location — continue running without file logging.
            _writer = null;
        }
    }

    private void RotateOldSessions()
    {
        try
        {
            var files = Directory.GetFiles(LogDirectory, "glint-*.log")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .ToList();

            // This run will create one more file, so keep (MaxSessionFiles - 1) existing ones.
            foreach (var old in files.Skip(MaxSessionFiles - 1))
            {
                try { File.Delete(old); }
                catch { /* best-effort cleanup, non-fatal */ }
            }
        }
        catch
        {
            // Non-fatal — worst case old logs accumulate until the next successful rotation.
        }
    }

    public void Log(LogLevel level, string source, string message, Exception? exception = null)
    {
        if (level < MinimumLevel || _writer is null)
            return;

        lock (_lock)
        {
            if (_sizeLimitReached)
                return;

            try
            {
                _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level.ToString().ToUpperInvariant()}] [{source}] {message}");

                if (exception is not null)
                    _writer.WriteLine(exception.ToString());

                if (_writer.BaseStream.Length > MaxFileSizeBytes)
                {
                    _writer.WriteLine("=== Log file size limit reached; further entries suspended for this session ===");
                    _sizeLimitReached = true;
                }
            }
            catch
            {
                // Logging must never throw and take down the app.
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            try
            {
                _writer?.WriteLine($"=== Glint session ended {DateTime.Now:O} ===");
                _writer?.Dispose();
            }
            catch
            {
                // Ignore — shutting down anyway.
            }
        }
    }
}
