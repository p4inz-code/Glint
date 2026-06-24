using System.Runtime.InteropServices;
using Glint.Core.Interfaces;
using Glint.Core.Logging;
using Glint.Core.Models;

namespace Glint.Windows.Hotkeys;

/// <summary>
/// Windows implementation of <see cref="IHotkeyService"/>. Creates a hidden message-only
/// window (HWND_MESSAGE) on a dedicated thread with its own GetMessage loop, then uses
/// RegisterHotKey/WM_HOTKEY for global shortcuts that work even when Glint's flyout isn't
/// focused. If a combination is already claimed by another app, RegisterHotKey fails —
/// this is logged and that single binding is skipped; it never crashes the app.
///
/// IMPORTANT: RegisterHotKey/UnregisterHotKey must be called from the same thread that
/// owns the window (otherwise Win32 returns ERROR_WINDOW_OF_OTHER_THREAD, 1408). Since the
/// message window lives on its own dedicated thread, <see cref="RegisterHotkeys"/> hands
/// off to that thread by posting a custom message (<see cref="WM_REGISTER_HOTKEYS"/>) and
/// the actual (Un)RegisterHotKey calls happen inside <see cref="WndProc"/>.
/// </summary>
public sealed class WindowsHotkeyService : IHotkeyService
{
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_DESTROY = 0x0002;

    /// <summary>Custom message: "re-read _pendingHotkeys and (re)register on this thread".</summary>
    private const uint WM_REGISTER_HOTKEYS = 0x0400 + 1; // WM_USER + 1

    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly IAppLogger _logger;
    private readonly object _lock = new();
    private readonly Dictionary<int, HotkeyAction> _registeredIds = new();
    private readonly ManualResetEventSlim _windowReady = new(false);

    private Thread? _messageThread;
    private IntPtr _hwnd = IntPtr.Zero;
    private int _nextId = 1;
    private HotkeySettings? _pendingHotkeys;

    // Keep the delegate alive for the lifetime of the service — the native window class
    // holds a function pointer to it, and a GC'd delegate here would crash the process.
    private WndProcDelegate? _wndProcDelegate;

    public event EventHandler<HotkeyAction>? HotkeyPressed;

    public WindowsHotkeyService(IAppLogger logger)
    {
        _logger = logger;
    }

    public void RegisterHotkeys(HotkeySettings hotkeys)
    {
        EnsureMessageThreadStarted();

        if (!_windowReady.Wait(TimeSpan.FromSeconds(2)) || _hwnd == IntPtr.Zero)
        {
            _logger.Error("Hotkeys", "Hotkey message window failed to initialize; global hotkeys are disabled");
            return;
        }

        lock (_lock)
        {
            _pendingHotkeys = hotkeys;
        }

        // Hand off to the message-loop thread — see class remarks on ERROR_WINDOW_OF_OTHER_THREAD.
        if (!PostMessage(_hwnd, WM_REGISTER_HOTKEYS, IntPtr.Zero, IntPtr.Zero))
        {
            _logger.Warn("Hotkeys", $"Failed to post hotkey registration request (Win32 error {Marshal.GetLastWin32Error()})");
        }
    }

    /// <summary>
    /// Runs on the message-loop thread (called from <see cref="WndProc"/>). Unregisters any
    /// previously registered hotkeys and registers the latest set from <see cref="_pendingHotkeys"/>.
    /// </summary>
    private void DoRegisterHotkeys(IntPtr hWnd)
    {
        lock (_lock)
        {
            foreach (var id in _registeredIds.Keys)
                UnregisterHotKey(hWnd, id);
            _registeredIds.Clear();
            _nextId = 1;

            var hotkeys = _pendingHotkeys;
            if (hotkeys is null)
                return;

            RegisterOne(hWnd, hotkeys.BrightnessUp, HotkeyAction.BrightnessUp);
            RegisterOne(hWnd, hotkeys.BrightnessDown, HotkeyAction.BrightnessDown);
            RegisterOne(hWnd, hotkeys.VolumeUp, HotkeyAction.VolumeUp);
            RegisterOne(hWnd, hotkeys.VolumeDown, HotkeyAction.VolumeDown);
        }
    }

    /// <summary>Must be called with <see cref="_lock"/> held, on the message-loop thread.</summary>
    private void RegisterOne(IntPtr hWnd, HotkeyCombo combo, HotkeyAction action)
    {
        if (combo.IsEmpty)
            return;

        if (!VirtualKeyMap.TryGetValue(combo.Key, out var vk))
        {
            _logger.Warn("Hotkeys", $"Unrecognized key '{combo.Key}' for {action} — skipping");
            return;
        }

        var id = _nextId++;
        if (RegisterHotKey(hWnd, id, (uint)combo.Modifiers, vk))
        {
            _registeredIds[id] = action;
            _logger.Debug("Hotkeys", $"Registered {combo} -> {action}");
        }
        else
        {
            var err = Marshal.GetLastWin32Error();
            _logger.Warn("Hotkeys", $"Could not register '{combo}' for {action} (Win32 error {err}) — likely already bound by another application");
        }
    }

    private void EnsureMessageThreadStarted()
    {
        if (_messageThread is not null)
            return;

        _messageThread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "GlintHotkeyMessageLoop"
        };
        _messageThread.Start();
    }

    private void MessageLoop()
    {
        try
        {
            _wndProcDelegate = WndProc;

            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProcDelegate,
                hInstance = GetModuleHandle(null),
                lpszClassName = "GlintHotkeyMessageWindow",
                lpszMenuName = string.Empty
            };

            if (RegisterClassEx(ref wc) == 0)
            {
                _logger.Error("Hotkeys", $"RegisterClassEx failed (Win32 error {Marshal.GetLastWin32Error()})");
                _windowReady.Set();
                return;
            }

            _hwnd = CreateWindowEx(0, wc.lpszClassName, null, 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                _logger.Error("Hotkeys", $"CreateWindowEx failed (Win32 error {Marshal.GetLastWin32Error()})");
                _windowReady.Set();
                return;
            }

            _windowReady.Set();

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_HOTKEY)
                    HandleHotkeyMessage((int)msg.wParam);

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Hotkeys", "Hotkey message loop crashed", ex);
            _windowReady.Set();
        }
    }

    private void HandleHotkeyMessage(int id)
    {
        HotkeyAction? action = null;

        lock (_lock)
        {
            if (_registeredIds.TryGetValue(id, out var a))
                action = a;
        }

        if (action.HasValue)
            HotkeyPressed?.Invoke(this, action.Value);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_REGISTER_HOTKEYS:
                DoRegisterHotkeys(hWnd);
                return IntPtr.Zero;

            case WM_CLOSE:
                lock (_lock)
                {
                    foreach (var id in _registeredIds.Keys)
                        UnregisterHotKey(hWnd, id);
                    _registeredIds.Clear();
                }
                DestroyWindow(hWnd);
                return IntPtr.Zero;

            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;

            default:
                return DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    public void Dispose()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        // Triggers WM_CLOSE -> unregister all -> DestroyWindow -> WM_DESTROY -> PostQuitMessage,
        // all on the message-loop thread, which then exits naturally.
        PostMessage(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        _hwnd = IntPtr.Zero;
    }

    // --- P/Invoke -------------------------------------------------------------

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle, int x, int y, int width, int height, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
