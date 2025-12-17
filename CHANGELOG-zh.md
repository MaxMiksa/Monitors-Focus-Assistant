## v0.1.0 – 专注双屏遮罩（2025-12-17）

### 功能 1：鼠标跟随式黑屏遮罩
- **总结**: 增加点击穿透的纯黑遮罩，光标离开受控屏后自动熄屏。
- **解决痛点**: 主屏工作时避免副屏干扰，不挪动窗口、不触发重排。
- **功能细节**: 每 50ms 轮询光标，进入受控屏立刻亮屏，返回主屏按可配置延迟（默认 180s）黑屏，支持 DPI 感知。
- **技术实现**:
  - `MainAppContext` 使用 per-monitor `nextMaskAt` 的状态机。
  - `OverlayForm` 分层窗体，使用 `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`。
  - `MouseTracker` 50ms 轮询，`Screen.FromPoint` 结合 `MonitorManager` 识别屏幕。

### 功能 2：托盘控制与设置界面
- **总结**: 托盘优先的控制方式，提供全局热键和设置窗口。
- **解决痛点**: 无主界面干扰，快速暂停/恢复并随时调整参数。
- **功能细节**: 托盘菜单（Settings/Pause/Exit）；全局热键默认 Ctrl+F12；设置项包含延迟、透明度、屏幕选择、热键、开机自启；DDC/CI 标记“coming soon”。
- **技术实现**:
  - `HotkeyManager` 通过隐藏 `HotkeyWindow` 调用 `RegisterHotKey`，联动暂停开关。
  - `SettingsForm` 提供屏幕勾选、热键选择、校验，并通过 `ApplySettings` 实时应用。
  - `SettingsStore` 将配置保存在 `%AppData%\\Monitors-Focus\\settings.json`；`StartupManager` 写入 Run 注册表；新增 `.gitignore` 过滤构建产物与内部文档。
