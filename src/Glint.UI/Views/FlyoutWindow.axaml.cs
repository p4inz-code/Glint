using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Glint.UI.Views;

public partial class FlyoutWindow : Window
{
    public FlyoutWindow()
    {
        InitializeComponent();

        // Click-outside-to-dismiss, matching the behavior of the built-in Windows
        // volume/brightness flyouts.
        Deactivated += (_, _) => Hide();
    }

    /// <summary>
    /// Shows the flyout anchored to the bottom-right of the primary screen's working area
    /// (where the Windows system tray sits). Avalonia's cross-platform TrayIcon API doesn't
    /// expose the icon's exact screen position, so this anchors to the screen corner —
    /// position is recalculated each time using the actual measured size after layout.
    /// </summary>
    public void ShowNearTray()
    {
        Show();
        Activate();

        // Position after the first layout pass so Bounds reflects the real rendered size
        // (SizeToContent="Height" means Height isn't known until content is measured).
        Dispatcher.UIThread.Post(Reposition, DispatcherPriority.Loaded);
    }

    private void Reposition()
    {
        var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
            return;

        var workingArea = screen.WorkingArea;
        var scaling = screen.Scaling;
        var width = (Bounds.Width > 0 ? Bounds.Width : Width) * scaling;
        var height = (Bounds.Height > 0 ? Bounds.Height : 400) * scaling;

        const int margin = 12;
        var x = workingArea.Right - (int)width - margin;
        var y = workingArea.Bottom - (int)height - margin;

        Position = new PixelPoint(x, y);
    }
}
