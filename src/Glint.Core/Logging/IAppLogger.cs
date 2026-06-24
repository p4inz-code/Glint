namespace Glint.Core.Logging;

/// <summary>
/// Minimal logging abstraction. The concrete implementation (Glint.App.Services.FileLogger)
/// writes to a rolling log file under %AppData%\Glint\logs. Every layer depends only on this
/// interface so Core/Windows providers stay testable and decoupled from file I/O.
/// </summary>
public interface IAppLogger
{
    /// <summary>Current minimum level that gets written. DEBUG entries are dropped unless this is Debug.</summary>
    LogLevel MinimumLevel { get; set; }

    void Log(LogLevel level, string source, string message, Exception? exception = null);
}

/// <summary>
/// Convenience shortcuts for <see cref="IAppLogger"/>. Implemented as extension methods
/// (rather than default interface members) so they're callable on concrete logger types
/// like <c>FileLogger</c> too — C# only exposes default interface methods through a
/// reference typed as the interface itself, which would force every call site to declare
/// loggers as <c>IAppLogger</c>.
/// </summary>
public static class AppLoggerExtensions
{
    public static void Debug(this IAppLogger logger, string source, string message)
        => logger.Log(LogLevel.Debug, source, message);

    public static void Info(this IAppLogger logger, string source, string message)
        => logger.Log(LogLevel.Info, source, message);

    public static void Warn(this IAppLogger logger, string source, string message, Exception? exception = null)
        => logger.Log(LogLevel.Warn, source, message, exception);

    public static void Error(this IAppLogger logger, string source, string message, Exception? exception = null)
        => logger.Log(LogLevel.Error, source, message, exception);
}
