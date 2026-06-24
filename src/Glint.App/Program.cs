using Avalonia;
using Avalonia.Threading;
using Glint.App.Services;
using Glint.Core.Interfaces;
using Glint.Core.Logging;
using Glint.UI.ViewModels;
using Glint.Windows.Audio;
using Glint.Windows.Brightness;
using Glint.Windows.Hotkeys;

namespace Glint.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logger = new FileLogger(debugLoggingEnabled: false);
        GlobalExceptionHandler.Install(logger);

        try
        {
            RunApp(logger, args);
        }
        catch (Exception ex)
        {
            logger.Error("Startup", "Fatal error during application startup", ex);
        }
        finally
        {
            logger.Dispose();
        }
    }

    private static void RunApp(FileLogger logger, string[] args)
    {
        var settingsService = new SettingsService(logger);
        logger.MinimumLevel = settingsService.Current.DebugLoggingEnabled ? LogLevel.Debug : LogLevel.Info;

        var brightnessProvider = new WindowsBrightnessProvider(logger);
        var audioProvider = new NAudioAudioProvider(logger);
        var hotkeyService = new WindowsHotkeyService(logger);

        FlyoutViewModel? flyoutViewModel = null;

        // App.axaml.cs calls this once, on the UI thread, when the desktop lifetime starts.
        // Keeps Glint.UI free of references to Glint.Windows / Glint.App.
        Glint.UI.App.ViewModelFactory = () =>
        {
            flyoutViewModel = new FlyoutViewModel(brightnessProvider, audioProvider, settingsService, logger);
            return flyoutViewModel;
        };

        hotkeyService.HotkeyPressed += (_, action) =>
            Dispatcher.UIThread.Post(() => HandleHotkey(action, flyoutViewModel, settingsService));

        hotkeyService.RegisterHotkeys(settingsService.Current.Hotkeys);

        Glint.UI.App.OnExitRequested = () =>
        {
            logger.Info("Shutdown", "Exit requested from tray menu");

            try { flyoutViewModel?.Dispose(); }
            catch (Exception ex) { logger.Error("Shutdown", "Error disposing flyout viewmodel", ex); }

            try { hotkeyService.Dispose(); }
            catch (Exception ex) { logger.Error("Shutdown", "Error disposing hotkey service", ex); }

            try { audioProvider.Dispose(); }
            catch (Exception ex) { logger.Error("Shutdown", "Error disposing audio provider", ex); }

            try { brightnessProvider.Dispose(); }
            catch (Exception ex) { logger.Error("Shutdown", "Error disposing brightness provider", ex); }

            settingsService.Save();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Applies a brightness/volume hotkey by adjusting the live viewmodel (so sliders and
    /// hardware update together, and brightness sync still applies). Runs on the UI thread.
    /// </summary>
    private static void HandleHotkey(HotkeyAction action, FlyoutViewModel? vm, ISettingsService settings)
    {
        if (vm is null)
            return;

        var step = settings.Current.Hotkeys.StepSize;

        switch (action)
        {
            case HotkeyAction.BrightnessUp:
                foreach (var monitor in vm.Monitors.Where(m => m.IsSupported))
                    monitor.Brightness = Math.Clamp(monitor.Brightness + step, 0, 100);
                break;

            case HotkeyAction.BrightnessDown:
                foreach (var monitor in vm.Monitors.Where(m => m.IsSupported))
                    monitor.Brightness = Math.Clamp(monitor.Brightness - step, 0, 100);
                break;

            case HotkeyAction.VolumeUp:
                vm.MasterVolume = Math.Clamp(vm.MasterVolume + step, 0, 100);
                break;

            case HotkeyAction.VolumeDown:
                vm.MasterVolume = Math.Clamp(vm.MasterVolume - step, 0, 100);
                break;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Glint.UI.App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
