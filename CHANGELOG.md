## v1.0.0 – Hardware Dimming & Localization (2025-12-17)

### Feature 1: Hardware dimming with diagnostics
- **Summary**: Added real hardware dimming (DDC/CI with WMI fallback) plus per-monitor diagnostics and disable list.
- **Problem Solved**: Overlay-only dimming left backlight running; now supported monitors can lower backlight to reduce glare/heat.
- **Feature Details**: Modes Auto/Overlay-only/Hardware-only; per-monitor disable; live status view; logs include set/restore success/failure/timeout; overlay remains a fallback.
- **Technical Implementation**:
  - `HardwareDimmer` with DDC/CI + WMI, capability tracking, session disable on failure/timeout, detailed logging.
  - Mode handling in `MainAppContext`; settings persisted for disabled monitors and mode.
  - UI diagnostics and per-monitor controls in `SettingsForm`.

### Feature 2: Localized, resizable settings with live language switch
- **Summary**: Settings UI now supports English/Chinese live switching, larger default window, size memory, and clearer controls.
- **Problem Solved**: Previously fixed-size, English-only UI with cramped controls.
- **Feature Details**: Default 1400x1000 (min 900x700) with size memory; language dropdown live refresh; enlarged controls; hardware note; buttons no longer clipped.
- **Technical Implementation**:
  - `SettingsForm` translations, label registry, option wrappers for language/mode, size persistence via `AppSettings`.
  - Layout tweaks (wider sliders/list views, larger buttons).

### Feature 3: Expanded logging and tray interaction
- **Summary**: Added app-wide log sink with richer events and tray left-click to open settings.
- **Problem Solved**: Sparse diagnostics made issue triage difficult; tray left-click previously inert.
- **Feature Details**: Logs capability detection, dim attempts, failures/timeouts, settings apply; real-time log panel in Settings; left-click tray opens Settings.
- **Technical Implementation**:
  - `LogSink` hooks across state machine and dimmer; UI log panel subscriptions.
  - Tray `MouseUp` handler opens Settings on left-click.

## v0.1.0 – Focused Dual Monitor Overlay (2025-12-17)

### Feature 1: Mouse-follow blackout overlay
- **Summary**: Added a click-through black overlay that dims controlled monitors when the cursor is away.
- **Problem Solved**: Prevents secondary-screen distraction without moving or rearranging windows.
- **Feature Details**: Polls cursor every 50ms, unmasks instantly on entry, masks after a configurable delay (default 180s), and stays DPI aware.
- **Technical Implementation**:
  - `MainAppContext` state machine with per-monitor `nextMaskAt` timing.
  - `OverlayForm` layered window using `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`.
  - `MouseTracker` 50ms poll with `Screen.FromPoint` and monitor mapping via `MonitorManager`.

### Feature 2: Tray controls and settings UI
- **Summary**: Tray-first controls with a global hotkey and settings window.
- **Problem Solved**: Enables quick pause/resume and on-the-fly configuration without a main window.
- **Feature Details**: Tray menu for Settings/Pause/Exit; global hotkey (Ctrl+F12 default); settings for delay, opacity, monitor selection, hotkey, autostart; DDC/CI marked “coming soon.”
- **Technical Implementation**:
  - `HotkeyManager` with `RegisterHotKey` via hidden `HotkeyWindow`, and pause toggle wiring.
  - `SettingsForm` with monitor checklist, hotkey pickers, validation, and live apply through `ApplySettings`.
  - `SettingsStore` JSON persistence in `%AppData%\\Monitors-Focus\\settings.json`; `StartupManager` applying Run key; `.gitignore` added for build artifacts and internal docs.
