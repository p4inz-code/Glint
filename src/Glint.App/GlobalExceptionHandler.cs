using Glint.Core.Logging;

namespace Glint.App;

/// <summary>
/// Installs process-wide unhandled exception handlers so unexpected failures are logged
/// with full detail instead of silently crashing or vanishing. Per Section 6, individual
/// provider failures (one monitor, one audio session) are already caught locally — this is
/// the last-resort net for anything that slips through.
/// </summary>
public static class GlobalExceptionHandler
{
    public static void Install(IAppLogger logger)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            logger.Error("Fatal", "Unhandled AppDomain exception", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Error("Fatal", "Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }
}
