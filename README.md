# Monitors Focus | [中文](README-zh.md)

![License](https://img.shields.io/badge/license-TBD-lightgrey) ![Tech](https://img.shields.io/badge/.NET-8%20WinForms-512BD4) ![Status](https://img.shields.io/badge/status-v0.1.0-blue)

✅ **Tray-first, no main window | Pause/Resume hotkey | Click-through black overlay**  
✅ **Instant wake when cursor enters secondary | Auto-dim after delay on primary**  
✅ **Windows 10/11 | Dual/tri monitor layouts | No window rearrange**  

Monitors Focus is a lightweight Windows tray app that auto-dims selected secondary monitors with a pure black, click-through overlay. It wakes the screen instantly when your cursor enters and darkens it again after a delay when you return to the primary display.

<p align="center">
  <img src="Presentation/demo.svg" width="720" alt="Monitors Focus demo"/>
</p>

## Features

| Feature | Description |
| :--- | :--- |
| ✨ Mouse-follow dimming | Tracks cursor and auto-masks selected non-primary monitors after a configurable delay (default 180s). |
| 🛡️ Click-through overlay | Pure black, topmost, non-activating, hidden from Alt-Tab/taskbar; does not steal clicks. |
| ⚙️ Quick settings | Tray settings for delay, opacity, monitor selection, global hotkey (Ctrl+F12 default), launch on startup. |
| 🎛️ Pause anytime | Global hotkey or tray menu instantly pauses/resumes automation. |

## Usage
1. Build or download `MonitorsFocus.exe` (see Developer Guide below for build).
2. Run the app: find the tray icon, open **Settings** to choose monitors, delay, opacity, and hotkey.
3. Work on your primary screen; the selected secondary screens will black out after the delay and wake instantly when you move the cursor onto them.

<details>
  <summary>Requirements & Limits</summary>

  - Windows 10/11 (x64), .NET 8 desktop runtime if not self-contained.
  - Designed for 2+ monitors; works with PerMonitorV2 DPI scaling.
  - DDC/CI hardware dimming is not included in v0.1.0 (overlay-only).
</details>

<details>
  <summary>Developer Guide</summary>

  ```bash
  dotnet restore
  dotnet build
  dotnet run --project src/MonitorsFocus
  # Single-file publish
  dotnet publish src/MonitorsFocus -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
  ```
</details>

<details>
  <summary>Development Stack</summary>

  1. Packages & Frameworks: .NET 8, WinForms
  2. Interfaces & Services: Windows user32 APIs (`RegisterHotKey`, layered windows)
  3. Languages: C#
</details>

<details>
  <summary>License</summary>

  License: TBD.
</details>

<details>
  <summary>FAQ / Troubleshooting</summary>

  - Hotkey not working? Another app may own it—change the hotkey in Settings.
  - Secondary not dimming? Check the monitor checklist in Settings and ensure delay is not 0 if you expect an immediate dim.
  - Overlay steals focus? Overlays are non-activating (`WS_EX_NOACTIVATE`); if you still see focus issues, try pausing/resuming via hotkey to reset.
</details>

## 🤝 Contribution & Contact

Welcome to submit Issues and Pull Requests!
Any questions or suggestions? Please contact Zheyuan (Max) Kong (Carnegie Mellon University, Pittsburgh, PA).

Zheyuan (Max) Kong: kongzheyuan@outlook.com | zheyuank@andrew.cmu.edu
