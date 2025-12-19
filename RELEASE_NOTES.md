## v0.1.0 – Mouse-Follow Focus / 鼠标跟随专注 (2025-12-17)

## ✨ 鼠标跟随 + 托盘热键

**光标离开副屏即按延迟黑屏，托盘/热键一键暂停，点击穿透不挡操作。**

| 类别 | 详细内容 |
| :--- | :--- |
| **自动遮罩** | 50ms 轮询光标，副屏延迟黑屏（默认 180s），进入即刻亮屏。 |
| **遮罩特性** | 纯黑顶置、点击穿透、不出现在 Alt-Tab/任务栏。 |
| **快捷控制** | 托盘菜单 + 全局热键（默认 Ctrl+F12）快速暂停/恢复。 |
| **设置界面** | 可配置延迟、透明度、受控屏、热键、开机自启；DDC/CI 标记为 coming soon。 |

## ✨ Mouse-follow dimming with tray control

**Auto-dims secondary monitors after a delay, wakes instantly on cursor entry, with click-through overlays and a tray-first UI.**

| Category | Details |
| :--- | :--- |
| **Auto blackout** | 50ms cursor polling; masks controlled monitors after configurable delay (default 180s); instant unmask on entry. |
| **Overlay safety** | Pure black, topmost, click-through, hidden from Alt-Tab/taskbar, non-activating. |
| **Quick controls** | Tray menu plus global hotkey (Ctrl+F12 default) to pause/resume anytime. |
| **Settings panel** | Configure delay, opacity, monitor selection, hotkey, autostart; DDC/CI noted as coming soon. |

## v1.0.0 – Hardware Dimming & Localization / 硬件调光与本地化 (2025-12-19)

## ✨ 硬件调光 + 本地化

**支持显示器的背光可直接调低，界面支持中英切换并更易用。**

| 类别 | 详细内容 |
| :--- | :--- |
| **硬件调光** | DDC/CI 调光（失败回退 WMI），诊断视图与单屏禁用，遮罩兜底。 |
| **界面升级** | 设置窗口更大且记忆尺寸；实时中英切换；控件加宽、按钮不再被截断。 |
| **日志增强** | 记录调光成功/失败/超时、能力状态、设置应用，方便排查。 |

## ✨ Hardware dimming & localized UI

**Backlight dimming on supported monitors, localized UI, and richer diagnostics.**

| Category | Details |
| :--- | :--- |
| **Hardware dimming** | DDC/CI with WMI fallback, per-monitor diagnostics/disable, overlay fallback. |
| **UI upgrades** | Larger settings window with size memory; live English/Chinese toggle; wider controls. |
| **Logs** | More events for dim attempts/failures/timeouts and settings apply for easier troubleshooting. |
