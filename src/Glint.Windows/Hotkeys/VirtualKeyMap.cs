namespace Glint.Windows.Hotkeys;

/// <summary>
/// Maps the human-readable key names stored in <see cref="Glint.Core.Models.HotkeyCombo.Key"/>
/// to Win32 virtual-key codes for RegisterHotKey. Covers the keys realistically used for
/// brightness/volume shortcuts; unrecognized names are skipped (logged) rather than crashing.
/// </summary>
internal static class VirtualKeyMap
{
    public static readonly Dictionary<string, uint> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Arrows — defaults for brightness/volume hotkeys
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Left"] = 0x25,
        ["Right"] = 0x27,

        // Navigation cluster
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,

        // Digits
        ["D0"] = 0x30, ["D1"] = 0x31, ["D2"] = 0x32, ["D3"] = 0x33, ["D4"] = 0x34,
        ["D5"] = 0x35, ["D6"] = 0x36, ["D7"] = 0x37, ["D8"] = 0x38, ["D9"] = 0x39,

        // Letters
        ["A"] = 0x41, ["B"] = 0x42, ["C"] = 0x43, ["D"] = 0x44, ["E"] = 0x45, ["F"] = 0x46,
        ["G"] = 0x47, ["H"] = 0x48, ["I"] = 0x49, ["J"] = 0x4A, ["K"] = 0x4B, ["L"] = 0x4C,
        ["M"] = 0x4D, ["N"] = 0x4E, ["O"] = 0x4F, ["P"] = 0x50, ["Q"] = 0x51, ["R"] = 0x52,
        ["S"] = 0x53, ["T"] = 0x54, ["U"] = 0x55, ["V"] = 0x56, ["W"] = 0x57, ["X"] = 0x58,
        ["Y"] = 0x59, ["Z"] = 0x5A,

        // Function keys
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73, ["F5"] = 0x74, ["F6"] = 0x75,
        ["F7"] = 0x76, ["F8"] = 0x77, ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,

        // Numpad
        ["NumPad0"] = 0x60, ["NumPad1"] = 0x61, ["NumPad2"] = 0x62, ["NumPad3"] = 0x63, ["NumPad4"] = 0x64,
        ["NumPad5"] = 0x65, ["NumPad6"] = 0x66, ["NumPad7"] = 0x67, ["NumPad8"] = 0x68, ["NumPad9"] = 0x69,
        ["Add"] = 0x6B, ["Subtract"] = 0x6D, ["Multiply"] = 0x6A, ["Divide"] = 0x6F,

        // Misc
        ["OemPlus"] = 0xBB,
        ["OemMinus"] = 0xBD,
        ["Tab"] = 0x09,
        ["Space"] = 0x20,
    };

    public static bool TryGetValue(string key, out uint vkCode) => Map.TryGetValue(key, out vkCode);
}
