# Glint

**Light, tuned.**

Glint is a free, lightweight Windows tray utility for controlling display brightness
and audio volume — all monitors, all apps, one flyout. Built by **Northbyte Studios**.

---

## Features (v1.0.0-alpha)

- **Per-monitor brightness sliders** in a tray flyout, including external monitors
  (via DDC/CI) and the laptop's internal panel (via WMI).
- **Multi-monitor support with hot-plug detection** — plug/unplug a monitor and the
  flyout updates automatically.
- **Brightness sync** — optionally move all displays together, each clamped
  independently so relative brightness differences are preserved.
- **Brightness presets** — save the current levels across all displays and recall
  them with one click; presets are renameable inline.
- **Master volume + per-app volume mixer** built into the same flyout.
- **Global hotkeys** for brightness and volume up/down (remappable in
  `settings.json` for this release).
- **Dark, minimal UI** with a cyan accent — no light theme, no clutter.
- **Zero telemetry.** Glint makes no network requests and collects no data, ever.

---

## Requirements

- Windows 10 or Windows 11, 64-bit.
- A display connected via a cable that supports DDC/CI for external-monitor
  brightness control (most modern monitors do; some budget/older monitors or
  certain docking-station/adapter setups don't expose it). If a monitor doesn't
  support DDC/CI, Glint shows it in the flyout with a "not supported" note instead
  of a non-functional slider — it won't crash or hang.

---

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
BUILD.bat
```

This runs:

```
dotnet publish src\Glint.App\Glint.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

and assembles a clean output folder at `dist\Glint\` containing `Glint.exe`
(self-contained — no .NET install required on the target machine) plus the
license and notice files.

### Running

Just run `Glint.exe`. It starts minimized to the system tray — left-click the
tray icon to open the flyout, right-click for the menu (Open / Exit).

Logs are written to `%AppData%\Glint\logs\`. Settings are stored in
`%AppData%\Glint\settings.json`.

---

## Privacy

Glint collects no data, makes no network requests, and includes no telemetry,
ads, or analytics of any kind. Everything — settings, presets, logs — stays on
your PC in `%AppData%\Glint\`.

---

## Roadmap

This is a **v1.0.0-alpha** build (Session 1). Planned next:

- HDR / SDR brightness handling for HDR displays
- Time-based brightness scheduling
- Per-application brightness/volume profiles
- Start with Windows toggle
- Scroll-to-adjust on the tray icon (pending verification of platform support)

---

## License

MIT — see [LICENSE.txt](LICENSE.txt). Third-party components are listed in
[THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).

---

## Credits

Built by **Northbyte Studios**.

---

<!-- SUPPORT-BLOCK:START -->
<div align="center">

<a href="https://p4inz-code.github.io/donate/"><img src="https://raw.githubusercontent.com/p4inz-code/donate/main/banner.svg" alt="Support this project: donate by UPI" width="100%"></a>

<a href="https://p4inz-code.github.io/donate/"><img src="https://raw.githubusercontent.com/p4inz-code/donate/main/qr-card.svg" alt="UPI QR code, scan with any UPI app" width="240"></a>

Or send any amount to:

`9321614988@jio`

<sub>Payee name: Atharva Patil · <a href="https://p4inz-code.github.io/donate/">Donate from your phone</a></sub>

</div>
<!-- SUPPORT-BLOCK:END -->
