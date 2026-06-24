# Changelog

All notable changes to Glint are documented in this file.

## [1.0.0-alpha] - Session 1

Initial build. Core engine + functional UI, no HDR yet (planned for Session 2).

### Added
- Project scaffold: `Glint.Core`, `Glint.Windows`, `Glint.UI`, `Glint.App`
  (Avalonia UI + .NET 8, MVVM, self-contained single-file publish).
- Per-monitor brightness control: DDC/CI (Dxva2) for external monitors, WMI
  (`WmiMonitorBrightness`) for the internal laptop panel.
- Friendly monitor name resolution via `WmiMonitorID` cross-referenced against
  `EnumDisplayDevices` hardware IDs, with generic-name fallback.
- Hot-plug detection via `Win32_DeviceChangeEvent` — monitor list refreshes
  automatically on connect/disconnect.
- Monitors that don't support brightness control (no DDC/CI, no WMI match) are
  shown as "not supported" instead of a dead slider; repeated write failures at
  runtime mark a monitor unsupported after 3 consecutive failures.
- Master + per-application volume and mute via NAudio/WASAPI
  (`AudioSessionManager`), with live refresh on session create/expire and on
  default-device change.
- Brightness slider writes are debounced/throttled to ~12/sec to avoid
  hammering DDC/CI during a drag.
- Brightness sync (lockstep with per-monitor clamping) across all displays,
  toggleable from the flyout.
- Brightness presets: save current levels, apply, rename inline, delete.
- Global hotkeys (Ctrl+Alt+Arrow keys by default) for brightness/volume
  up/down, remappable via `settings.json`.
- Dark UI (Section 3 palette: `#121214` / `#1A1A1D` panels, `#4ECBF0` cyan
  accent) with a borderless flyout window anchored near the system tray,
  click-outside-to-dismiss.
- JSON settings persistence (`%AppData%\Glint\settings.json`) with atomic
  writes and safe fallback to defaults on missing/corrupt files.
- Rolling session log files (`%AppData%\Glint\logs\`, 5MB cap, last 5
  sessions kept).
- Global exception handling (`AppDomain.UnhandledException`,
  `TaskScheduler.UnobservedTaskException`).
- Placeholder app/tray icon (Concept A — dark disc with cyan "glint" sliver).
- `BUILD.bat`, solution file, `LICENSE.txt` (MIT), `THIRD_PARTY_NOTICES.txt`,
  `README.md`.

### Known limitations / unverified on first build
- Built and written in an environment without a Windows runtime or NuGet
  access — package versions (Avalonia 11.1.3, NAudio 2.2.1, System.Management
  8.0.0) and a couple of Win32/Fluent resource-key names are unverified. See
  README for details; isolated to single files if a fix is needed.
- Scroll-to-adjust on the tray icon is not yet implemented (Avalonia
  `TrayIcon` scroll-event support needs verification).
- HDR/SDR brightness handling is Session 2 scope.
