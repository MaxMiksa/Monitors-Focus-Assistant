# Logs

## Step 1 - Analyze and understand user needs
- 1.1 Read `AGENT.md` to capture the required execution order and logging requirements.
- 1.2 Listed repository files to locate `PRD.md` and the current `Logs.md`.
- 1.3 Read `PRD.md` fully to understand the current functional and non-functional requirements.
- 1.4 Extracted core intent from the user's language: main screen used for work, secondary should be black by default; moving/dragging to the secondary should wake it; returning to main should darken the secondary after ~3-5 minutes; app must be tiny, fast to start/respond, and have a settings UI.
- 1.5 Mapped PRD functional scope: real-time mouse tracking; main/secondary region detection; black overlay that is always-on-top, click-through, and does not disrupt window positions; optional DDC/CI for hardware brightness.
- 1.6 Mapped PRD non-functional scope: low CPU/memory, fast switching (<50ms), tray-only presence, global hotkey toggle.
- 1.7 Noted potential clarifications for later PRD refinement (e.g., configurable idle delay default vs 3-5 minutes, multi-monitor beyond 2, Windows-only assumptions from Twinkle Tray context).

## Step 2 - Improve and refine PRD.md
- 2.1 Re-read the original PRD to locate inconsistencies (e.g., default delay 0s vs user-stated 3-5 minutes).
- 2.2 Identified missing constraints to add: Windows version scope, DPI scaling, monitor hot-plug handling, Alt-Tab/taskbar visibility, and click-through behavior.
- 2.3 Defined scope boundaries to keep first release focused (exclude RDP/virtual displays and advanced scene detection).
- 2.4 Expanded functional requirements into clear sections: screen detection, mouse tracking, overlay behavior, tray/hotkey, settings UI, and window stability constraints.
- 2.5 Added optional DDC/CI brightness control and clarified it is disabled by default.
- 2.6 Updated non-functional requirements with concrete performance, stability, and startup targets.
- 2.7 Rewrote `PRD.md` with consistent numbering and a simplified logic flow diagram.
- 2.8 Quick consistency review to ensure the improved PRD aligns with the user-stated 3-5 minute delay while keeping it configurable.

## Step 3 - Create DEV.md (development guide)
- 3.1 Chose a lightweight Windows implementation direction: .NET 8 WinForms with minimal native interop.
- 3.2 Defined core modules for the system: settings store, monitor manager, mouse tracker, overlay manager, tray controller, hotkey manager.
- 3.3 Specified a simple state machine to model pause/resume and per-monitor overlay visibility.
- 3.4 Documented overlay window requirements (borderless, topmost, click-through, hidden from Alt-Tab/taskbar).
- 3.5 Added performance targets and mouse polling interval to match PRD timing constraints.
- 3.6 Wrote build/run and packaging commands for local dev and single-file release.
- 3.7 Added a quick manual test checklist for basic functional verification.

## Step 4 - Review and correct DEV.md
- 4.1 Re-read `DEV.md` to validate component definitions against PRD requirements.
- 4.2 Added explicit display-change refresh using `SystemEvents.DisplaySettingsChanged`.
- 4.3 Clarified delay logic using per-monitor `nextMaskAt` timestamps instead of a single timer.
- 4.4 Added `WS_EX_NOACTIVATE` to prevent overlay focus stealing.
- 4.5 Added DPI awareness guidance (`PerMonitorV2`) to keep cursor and screen bounds aligned.
- 4.6 Renumbered sections after adding the DPI awareness section.

## Step 5 - Create detailed checklist.md
- 5.1 Outlined development phases from project scaffolding through QA/packaging.
- 5.2 Added detailed checklist items for settings persistence and monitor enumeration.
- 5.3 Added granular steps for mouse tracking, overlay creation, and state machine timing.
- 5.4 Included tray/hotkey integration and settings UI validation tasks.
- 5.5 Added optional DDC/CI tasks as an explicit phase.
- 5.6 Included QA checks for DPI scaling, multi-monitor behavior, and publish packaging.

## Step 6 - Review and correct checklist.md
- 6.1 Added explicit requirement for hidden main context to avoid visible windows.
- 6.2 Added fallback behavior for missing/corrupt settings.
- 6.3 Added overlay recreation on display changes and cleanup on exit.
- 6.4 Added hotkey unregistration and tray disposal on exit.
- 6.5 Added re-register and overlay rebuild steps when settings change.

## Step 7 - Implement code following checklist phases
### Phase 1 - Project scaffolding
- 7.1.1 Created `src/MonitorsFocus` project directory structure.
- 7.1.2 Added `MonitorsFocus.sln` with SDK-style C# project reference.
- 7.1.3 Added `src/MonitorsFocus/MonitorsFocus.csproj` targeting `net8.0-windows` with WinForms enabled.
- 7.1.4 Implemented `src/MonitorsFocus/Program.cs` with PerMonitorV2 DPI, visual styles, and `Application.Run` entry.
- 7.1.5 Added a minimal `MainAppContext` to ensure no visible main window.
- 7.1.6 Reviewed scaffolding for compile readiness; noted tray icon and build verification pending.

### Phase 2 - Settings model and persistence
- 7.2.1 Created `src/MonitorsFocus/Settings` folder for configuration code.
- 7.2.2 Implemented `AppSettings` and `HotkeySettings` with defaults and normalization.
- 7.2.3 Implemented `SettingsStore` for JSON load/save with enum string conversion.
- 7.2.4 Added validation clamps for delay and opacity values.
- 7.2.5 Ensured settings directory creation and default fallback on missing/corrupt files.

### Phase 3 - Monitor enumeration and tracking
- 7.3.1 Created `src/MonitorsFocus/Monitoring` folder for monitor-related code.
- 7.3.2 Implemented `MonitorInfo` with stable IDs, bounds, and primary flag.
- 7.3.3 Implemented `MonitorManager` to enumerate `Screen.AllScreens` and expose controlled monitor selection logic.
- 7.3.4 Wired `SystemEvents.DisplaySettingsChanged` to refresh monitor data and emit change events.
- 7.3.5 Noted that overlay recreation on display change will be wired in the overlay phase.

### Phase 4 - Mouse tracking and active screen detection
- 7.4.1 Implemented `MouseTracker` with a 50ms WinForms timer.
- 7.4.2 Used `Cursor.Position` and `Screen.FromPoint` to resolve active screen each tick.
- 7.4.3 Exposed a lightweight `Action<Point, Screen>` callback to avoid per-tick allocations.

### Phase 5 - Overlay windows
- 7.5.1 Added `NativeMethods` with window style constants and `SetWindowPos` interop.
- 7.5.2 Implemented `OverlayForm` with click-through, no-activation, topmost behavior.
- 7.5.3 Added opacity and bounds update helpers to `OverlayForm`.
- 7.5.4 Implemented `OverlayManager` to build and dispose per-monitor overlays.
- 7.5.5 Added overlay entry state storage (`NextMaskAt`, `IsMasked`) for later automation.

### Phase 6 - State machine and automation
- 7.6.1 Expanded `MainAppContext` to load settings, initialize monitor/overlay managers, and start mouse tracking.
- 7.6.2 Implemented per-monitor delay logic using `NextMaskAt` timestamps and `DateTime.UtcNow`.
- 7.6.3 Added immediate unmask when the cursor is on a controlled monitor.
- 7.6.4 Added pause/resume state handling and timer resets.
- 7.6.5 Wired display change events to rebuild overlays and reset timers.
- 7.6.6 Implemented cleanup in `ExitThreadCore` to dispose trackers and overlays.
- 7.6.7 Routed display-change handling through the UI `SynchronizationContext` to avoid cross-thread form access.

### Phase 7 - Tray and hotkey
- 7.7.1 Added hotkey interop (`RegisterHotKey`, `UnregisterHotKey`) and constants in `NativeMethods`.
- 7.7.2 Implemented `HotkeyWindow` to receive `WM_HOTKEY` without a visible form.
- 7.7.3 Implemented `HotkeyManager` to register/unregister and raise hotkey events.
- 7.7.4 Added tray icon with context menu (Settings, Pause/Resume, Exit).
- 7.7.5 Wired pause/resume toggle to both tray menu and hotkey.
- 7.7.6 Added disposal for hotkey manager and tray icon on app exit.

### Phase 8 - Settings UI
- 7.8.1 Added `SettingsForm` with delay, opacity, monitor selection, hotkey, autostart, and DDC/CI toggles.
- 7.8.2 Populated monitor list with primary labeling and default selection rules.
- 7.8.3 Implemented hotkey validation and settings normalization on save.
- 7.8.4 Added `StartupManager` to apply autostart preference via registry.
- 7.8.5 Wired settings dialog to `MainAppContext` with live apply, overlay rebuild, and hotkey re-register.
- 7.8.6 Marked DDC/CI UI as disabled with "coming soon" to avoid misleading configuration.
- 7.8.7 Added validation to require at least one controlled monitor before saving.
- 7.8.8 Added save failure handling with a warning message to avoid crashing on IO errors.

### Repo hygiene
- 7.H.1 Added `.gitignore` to exclude build artifacts and internal instruction docs per release rules.
- 7.8.8 Added save failure handling with a warning message to avoid crashing on IO errors.

## Step 8 - Code and requirement checks, then fixes
- 8.1 全局代码检查：确保窗口操作在 UI 线程执行，发现显示器变更回调可能跨线程 -> 使用 `SynchronizationContext` 修复。
- 8.2 功能需求检查：核对 PRD 核心点（鼠标跟随遮罩、延迟黑屏、托盘+热键、设置界面、点击穿透黑屏）；确认 DDC/CI 为可选且暂未实现，界面已标记“coming soon”避免误导。
- 8.3 性能路径检查：鼠标轮询 50ms，循环无额外分配；遮罩操作仅在状态变化时触发。
- 8.4 兼容性检查：DPI 感知在 Program 中启用 PerMonitorV2；遮罩使用透明层和不激活样式以避免抢焦点。
- 8.5 未执行的测试：由于当前环境缺少 `dotnet`/Windows，未能运行 `dotnet build` 或实际多显示器手测，需在目标环境验证。

## Step 9 - Release docs for v0.1.0
- 9.1 准备了双语 README（中文/英文）遵循给定结构，包含徽章、钩子、特性表、折叠章节、联系信息。
- 9.2 创建 `CHANGELOG-zh.md` 与 `CHANGELOG.md`，按模板记录两项核心特性及技术实现细节。
- 9.3 创建 `RELEASE_NOTES.md`（双语表格+亮点标题）并注明 v0.1.0 日期 2025-12-17。
- 9.4 添加 `Presentation/demo.svg` 作为示例视觉展示并在 README 中引用。
- 9.5 记录 `.gitignore` 确保内部文档和构建产物被忽略。
- 9.6 在 `MonitorsFocus.csproj` 中设置版本号为 0.1.0。

## Step 10 - Sync README with updated AI release rules
- 10.1 阅读最新的 `AI_RELEASE_RULES.md`，确认新增的 Brief Introduction 要求。
- 10.2 在 `README.md` 和 `README-zh.md` 的 hook 后添加 1-2 句简介，解释应用作用与核心行为。

## Step 11 - Gitignore alignment and AI rules update
- 11.1 更新 `.gitignore`，加入 `.vs/`、`.vscode/`、`*.user`、`*.suo` 及现有 bin/obj，保持内部文档忽略。
- 11.2 在 `AI_RELEASE_RULES.md` 的 Git Ignore Rules 中新增生成/同步要求和完整条目列表，强调发布时同步 `.gitignore`。

## Step 12 - AGENT Plan analysis and Plan Plus
- 12.1 重新阅读 `AGENT.md`，理解原 Plan 旨在串行推进和日志留存以保证可追溯。
- 12.2 评估原 Plan 的通用性：大部分步骤可作为通用 AI 指南保留，但需按项目复杂度裁剪。
- 12.3 在 `AGENT.md` 末尾新增“关于原 Plan 的理解与留存评估”说明。
- 12.4 新增“Plan Plus”提出更稳健的改进版计划（环境确认、风险基线、三件套迭代、分阶段闸门、线程资源检查、配置容错、测试计划、发布同步、复盘交接）。

## Step 13 - Packaging script and gitignore update
- 13.1 Added `pack.bat` to publish a win-x64 single-file self-contained build into `publish/win-x64`.
- 13.2 Updated `.gitignore` to exclude `publish/` along with IDE/user artifacts.

## Step 14 - Win11 test plan document
- 14.1 Created `TESTING_WIN11.md` with step-by-step manual tests (core behavior, stability, edge cases) for Windows 11 multi-monitor setups.
- 14.2 Added `TESTING_WIN11.md` to `.gitignore` per instruction to keep test notes local.

## Step 15 - CODEX coordination doc
- 15.1 Added `CODEX.md` as a persistent handoff document between WSL Codex (author) and Win11 Codex (tester), including responsibilities and project summary.
- 15.2 Embedded the Win11 testing plan (replacing TESTING_WIN11.md) as the initial handoff record with detailed tasks, formatting rules, and user participation notes.
- 15.3 Added `CODEX.md` to `.gitignore` to keep the coordination log local.

## Step 16 - WSL build feasibility check
- 16.1 Confirmed .NET SDK 8.0.122 is installed on WSL (Ubuntu 24.04), RID `ubuntu.24.04-x64`.
- 16.2 Checked workloads: none installed; Windows Desktop targeting pack not present.

## Step 17 - WSL build attempt
- 17.1 Ran `dotnet build src/MonitorsFocus/MonitorsFocus.csproj -p:EnableWindowsTargeting=true`.
- 17.2 Build failed: missing SDK `Microsoft.NET.Sdk.WindowsDesktop` (path `/usr/lib/dotnet/sdk/8.0.122/Sdks/Microsoft.NET.Sdk.WindowsDesktop/Sdk` not found); workload resolver returned null.
- 17.3 Conclusion: On WSL/Linux, WindowsDesktop targeting pack is absent; WinForms build not available here.

## Step 18 - Next steps for build/test
- 18.1 WSL cannot build WinForms without the WindowsDesktop SDK (not available on Linux). Recommend building on Win11 or installing WindowsDesktop targeting pack on Windows.
- 18.2 For local logic testing on WSL, option is to extract non-UI logic into a cross-platform library (e.g., settings/state machine) and test via a console app; current code references WinForms types (Keys, Forms), so refactor would be needed before WSL compilation.

## Step 19 - Hardware dimming implementation and opacity fix
- 19.1 Added DDC/CI hardware dimmer support (`src/MonitorsFocus/Brightness/HardwareDimmer.cs`) with per-monitor capability detection via `dxva2.dll` APIs; stores original brightness, dims to target level, and restores on unmask/exit.
- 19.2 Extended `MonitorInfo`/`MonitorManager` to capture HMONITOR handles (via `MonitorFromPoint`) for DDC/CI operations.
- 19.3 Integrated hardware dimming into state machine in `MainAppContext` (dim on mask, restore on unmask/pause/exit; refresh on monitor changes and settings apply).
- 19.4 Updated settings model/UI: added hardware dimming toggle and level slider; enabled previously disabled DDC/CI control.
- 19.5 Fixed 100% opacity issue by forcing layered opacity to 0.999 when user selects 100%, keeping the overlay visible and fully black.
- 19.6 Updated `NativeMethods` with brightness and monitor handle interop; added checklist Phase 9 completion.

## Step 20 - Hardware dimming enhancements per feedback
- 20.1 Added WMI fallback (`WmiBrightnessController`) for cases where DDC/CI is unavailable; sessions now mark support/failure and track messages.
- 20.2 Added per-monitor hardware dim disable list in settings and UI; capability statuses now surface in the Settings hardware diagnostics list.
- 20.3 Hardware dim operations now run asynchronously with timeout marking; session disables on failure/timeout to avoid repeated attempts.
- 20.4 Settings form shows diagnostics (Supported/Unsupported/Failed) and a “Rescan (applies on save)” helper; rescan occurs on settings save/monitor change.
- 20.5 `AppSettings` persists hardware dim disabled IDs; `MainAppContext` refreshes dimmer with overrides; README feature tables already note optional hardware dimming.

## Step 21 - Bug fix: SettingsForm null reference
- 21.1 Fixed `ToggleHardwareControls` null reference by deferring the call until hardware controls are created and adding null guards.

## Step 22 - UI/UX and logging improvements
- 22.1 Added app-wide `LogSink` and Settings log panel for live diagnostics.
- 22.2 Settings window now resizable, with increased control widths and minimum button sizes; added language selector (English/Chinese), dimming mode selector, hardware note text, and hardware diagnostics expansion.
- 22.3 Added hardware dimming per-monitor disable list, rescan notice, and ability to choose modes (Auto/Overlay-only/Hardware-only).
- 22.4 Ensured 100% opacity remains mapped to visible overlay; control sizing adjustments to avoid clipped buttons.

## Step 23 - Post-record 6 fixes/updates
- 23.1 Added window size persistence in settings (`LastWindowWidth/Height`) and set initial size larger (default 1200x900); Settings now resizable with remembered size.
- 23.2 Added left-click tray action to open Settings.
- 23.3 Expanded LogSink usage: more log lines for capability detection, hardware dim attempts/success/failure/timeout, settings apply.
- 23.4 Strengthened hardware dim error handling: DDC/CI/WMI set now updates failure message and disables session on failure; capability detection logs status.
- 23.5 Added bilingual UI support in Settings (language selector) and updated README feature lines.
- 23.6 Known pending: language switch currently requires re-open (live refresh not yet implemented); hardware dim efficacy still to validate on target hardware; logs may need further expansion per user feedback.

## Step 24 - Record 6/7 fixes (in progress, not yet Win11-verified)
- 24.1 Increased default Settings size to 1400x1000 and persisted last window size; ensured min size 900x700.
- 24.2 Added live language switch support with translations for labels/buttons/columns/hardware notes; language selection updates immediately without reopening.
- 24.3 Introduced localization helpers and option wrappers for language and dimming mode combos; mode texts localized.
- 24.4 Expanded logging: capability detection, skip reasons (paused/mode/disabled), pending mask timers, dim attempts, timeouts/failures, per-monitor capability summaries.
- 24.5 Added more defensive logging in HardwareDimmer (DDC/CI and WMI detection, set/restore failures) and state machine skip logs.
- 24.6 Retained left-click tray open; needs Win11 confirmation.

## Step 25 - Licensing
- 25.1 Added `LICENSE` with MIT terms.
