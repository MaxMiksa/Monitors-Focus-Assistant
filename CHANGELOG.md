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
