using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Glint.Windows.Brightness;

/// <summary>
/// Resolves a friendly monitor name (e.g. "DELL U2721DE") for a given GDI device name
/// (e.g. "\\.\DISPLAY1"). Windows doesn't expose this directly via Dxva2 — the PHYSICAL_MONITOR
/// description is frequently just "Generic PnP Monitor". Instead, this cross-references the
/// PNP hardware ID embedded in the device interface path (via EnumDisplayDevices) against
/// WMI's WmiMonitorID class, which carries the EDID-derived UserFriendlyName.
///
/// This is best-effort: if no match is found, callers fall back to a generic name. A wrong
/// or missing friendly name is cosmetic only and never blocks brightness control.
/// </summary>
internal static class MonitorNameResolver
{
    private const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        [MarshalAs(UnmanagedType.U4)] public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        [MarshalAs(UnmanagedType.U4)] public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    // Matches the PNP hardware ID segment out of a device interface path, e.g.
    // "MONITOR\DELA1FD\{4d36e96e-...}\0007" -> "DELA1FD"
    private static readonly Regex HardwareIdPattern = new(@"^MONITOR\\([A-Za-z0-9]+)\\", RegexOptions.Compiled);

    /// <summary>
    /// Returns a friendly name for the monitor attached to <paramref name="gdiDeviceName"/>
    /// (e.g. "\\.\DISPLAY1"), or null if no match could be resolved.
    /// </summary>
    public static string? Resolve(string gdiDeviceName)
    {
        try
        {
            var dd = new DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));

            if (!EnumDisplayDevices(gdiDeviceName, 0, ref dd, EDD_GET_DEVICE_INTERFACE_NAME))
                return null;

            var match = HardwareIdPattern.Match(dd.DeviceID);
            if (!match.Success)
                return null;

            var hardwareId = match.Groups[1].Value;
            return LookupFriendlyNameByHardwareId(hardwareId);
        }
        catch
        {
            return null;
        }
    }

    private static string? LookupFriendlyNameByHardwareId(string hardwareId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID");
            using var results = searcher.Get();

            foreach (ManagementObject mo in results)
            {
                var instanceName = mo["InstanceName"] as string ?? string.Empty;
                if (!instanceName.Contains(hardwareId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (mo["UserFriendlyName"] is ushort[] nameBytes)
                {
                    var name = DecodeUshortArray(nameBytes);
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }
        }
        catch
        {
            // WMI lookup failed entirely — caller falls back to a generic name.
        }

        return null;
    }

    private static string DecodeUshortArray(ushort[] values)
    {
        var bytes = new byte[values.Length];
        for (int i = 0; i < values.Length; i++)
            bytes[i] = (byte)values[i];

        var raw = Encoding.ASCII.GetString(bytes);
        var nullIndex = raw.IndexOf('\0');
        return nullIndex >= 0 ? raw[..nullIndex] : raw;
    }
}
