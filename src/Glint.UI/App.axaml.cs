using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Glint.UI.ViewModels;
using Glint.UI.Views;

namespace Glint.UI;

public partial class App : Application
{
    /// <summary>
    /// Set by the composition root (Glint.App/Program.cs) before the desktop lifetime
    /// starts. Keeps Glint.UI free of direct references to Glint.Windows/Glint.App —
    /// the App only knows it needs *a* <see cref="FlyoutViewModel"/>, not how it's built.
    /// </summary>
    public static Func<FlyoutViewModel>? ViewModelFactory { get; set; }

    /// <summary>
    /// Invoked when the user selects "Exit" from the tray menu, before the lifetime shuts
    /// down — the composition root uses this to dispose providers/services cleanly.
    /// </summary>
    public static Action? OnExitRequested { get; set; }

    private FlyoutWindow? _flyoutWindow;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Glint is a tray-only app — never show a taskbar window or quit when a window closes.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var viewModel = ViewModelFactory?.Invoke()
                ?? throw new InvalidOperationException("App.ViewModelFactory must be assigned before starting the Avalonia lifetime.");

            _flyoutWindow = new FlyoutWindow { DataContext = viewModel };

            SetupTrayIcon(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _trayIcon = new TrayIcon
        {
            ToolTipText = "Glint — Light, tuned.",
            IsVisible = true
        };

        try
        {
            _trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Glint.UI/Assets/tray-icon.ico")));
        }
        catch
        {
            // Icon asset not present — falls back to the platform default tray glyph.
            // Cosmetic only; does not affect functionality.
        }

        // Left-click toggles the flyout (parity with Windows volume/brightness flyouts).
        _trayIcon.Clicked += (_, _) => ToggleFlyout();

        var menu = new NativeMenu();

        var openItem = new NativeMenuItem("Open Glint");
        openItem.Click += (_, _) => ShowFlyout();
        menu.Items.Add(openItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            OnExitRequested?.Invoke();
            desktop.Shutdown();
        };
        menu.Items.Add(exitItem);

        _trayIcon.Menu = menu;
    }

    private void ToggleFlyout()
    {
        if (_flyoutWindow is null)
            return;

        if (_flyoutWindow.IsVisible)
            _flyoutWindow.Hide();
        else
            ShowFlyout();
    }

    private void ShowFlyout() => _flyoutWindow?.ShowNearTray();
}
