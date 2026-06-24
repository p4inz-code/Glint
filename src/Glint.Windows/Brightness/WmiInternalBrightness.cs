using System.Management;

namespace Glint.Windows.Brightness;

/// <summary>
/// Wraps the root\WMI WmiMonitorBrightness / WmiMonitorBrightnessMethods classes used to
/// read and set the internal laptop panel's brightness. External monitors are NOT exposed
/// here — those go through <see cref="Dxva2Interop"/> (DDC/CI).
/// </summary>
internal static class WmiInternalBrightness
{
    private const string Namespace = "root\\WMI";

    /// <summary>True if this machine reports an internal display via WMI (i.e. a laptop panel).</summary>
    public static bool HasInternalDisplay()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Namespace, "SELECT * FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (ManagementBaseObject _ in results)
            {
                return true;
            }
        }
        catch
        {
            // WMI namespace/class not present (desktop with no internal panel, or older driver) — treated as "no internal display".
        }

        return false;
    }

    /// <summary>Reads current internal-display brightness (0-100), or null if unavailable.</summary>
    public static int? GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Namespace, "SELECT * FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (ManagementBaseObject result in results)
            {
                return Convert.ToInt32(result["CurrentBrightness"]);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Sets internal-display brightness (0-100). Timeout parameter is the transition time
    /// in seconds for the brightness change — 0 applies it immediately.
    /// </summary>
    public static bool SetBrightness(int value)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Namespace, "SELECT * FROM WmiMonitorBrightnessMethods");
            using var results = searcher.Get();
            foreach (ManagementObject result in results)
            {
                result.InvokeMethod("WmiSetBrightness", new object[] { (uint)0, (byte)Math.Clamp(value, 0, 100) });
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
