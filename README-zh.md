<img src="docs/design/brand/logo.svg" width="72" alt="Monitors Focus logo">

# Monitors Focus | [English](README.md)

![License](https://img.shields.io/badge/license-MIT-green) ![Tech](https://img.shields.io/badge/.NET-10%20%2B%20WebView2-512BD4) ![Status](https://img.shields.io/badge/status-v2.0.0-blue)

✅ **双屏亮度统一调节 | 保持比例绑定 | 本地 Windows 应用**<br>
✅ **每日时间计划与渐变 | 场景一键切换 | 专注自动调暗**  
✅ **常驻内核 + 按需打开的控制面板 | Windows 10/11 | 不改变窗口位置**  

Monitors Focus 是一款本地运行的 Windows 显示器控制中心。常驻内核负责 DDC/CI 与 WMI 硬件读写，控制面板按需打开、关闭即释放；两块屏幕的亮度、色温、场景和时间计划都在同一个面板里完成。

v2.0.0 是把 v1.0.0 的托盘遮罩应用重写为「内核 + 界面」两层的版本：界面换成 React/TypeScript，硬件访问全部留在 C# 内核，v1 的鼠标跟随熄屏入口保留为兼容启动方式。详见[文档索引](docs/INDEX.md)与[界面核对清单](docs/design/pages/controls.md)。

<p align="center">
  <a href="https://github.com/MaxMiksa/Monitors-Focus-Assistant/releases/download/v2.0.0/MonitorsFocus-v2.0.0-win-x64.zip">
    <img src="Presentation/demo.svg" width="720" alt="Monitors Focus 演示"/>
  </a>
</p>

## 功能

| 功能 | 描述 |
| :--- | :--- |
| 💡 双屏亮度 | 每块屏幕独立滑条，直接写硬件背光；内屏走 WMI，外屏走 DDC/CI。 |
| ⚖️ 比例绑定 | 设定两屏基准后同步调节，保持原有亮度比例；归零后仍保留权重。 |
| 🌡️ 硬件色温 | 直接写 DDC/CI 的 RGB 增益（VCP 0x16/0x18/0x1A），可一键恢复原色。仅限支持的外接屏。 |
| 🕗 时间计划 | 按分钟设置节点与渐变时长，亮度与色温各自按能力判断，跨午夜连续计算。 |
| 🎬 场景 | 每个场景保存各屏的亮度和色温目标，面板一键切换，时间计划也引用场景。 |
| 🎯 专注 | 一段时间无操作后把屏幕压到设定亮度，随时暂停或恢复计划接管。 |
| 🚀 登录自启 | 只写一条 `MonitorsFocus` 注册表项，指向自包含内核，不重复启动。 |
| 🪟 面板按需 | 关闭面板只退出界面进程，内核继续调度；退出内核会先恢复临时调暗与色温。 |

<details>
  <summary>兼容的 v1.0.0 遮罩模式</summary>

  不带 `--center` 启动时仍进入旧的托盘遮罩应用：纯黑点击穿透遮罩、鼠标跟随熄屏、全局暂停热键（默认 Ctrl+F12）、中英文即时切换。此路径不再新增功能，只保证已有配置可用。
</details>

## 使用方法
1. 下载 `MonitorsFocus-v2.0.0-win-x64.zip` 解压，运行 `desktop/MonitorsFocus.WebHost.exe`；`core/` 目录必须与 `desktop/` 一起保留。
2. 面板首屏直接调两块屏幕的亮度；打开「绑定」后按基准比例同步调节。
3. 设置页配置时间计划、场景、专注与登录自启，保存后由内核接管，关闭面板不影响生效。

<details>
  <summary>运行环境与限制</summary>

  - Windows 10/11 (x64)，自包含包已含 .NET 运行时，另需 Microsoft Edge WebView2 Runtime（Win11 通常已预装）。
  - 为双/多屏设计，支持 PerMonitorV2 DPI 缩放。
  - 亮度：外接屏走 DDC/CI，内屏走 WMI，能力以每次探测结果为准。
  - 色温：仅硬件路径，需外接屏支持且颜色预设切到「用户」；内屏不调色温，界面会标注「不支持」而不是假装可用。
  - 尚未覆盖：长时间渐变、休眠与热插拔重连、混合 DPI 的持续运行验收。
</details>

<details>
  <summary>Developer Guide</summary>

  ```powershell
  ./scripts/build.ps1
  ./src/MonitorsFocus.WebHost/bin/Release/net10.0-windows/MonitorsFocus.WebHost.exe
  ./scripts/build.ps1 -Package
  ```

  [Development / 开发与构建](docs/development.md)
</details>

<details>
  <summary>开发栈</summary>

  1. Packages & Frameworks: .NET 10, WebView2, React, Tailwind CSS, shadcn/ui, Motion, Lucide
  2. Interfaces & Services: Windows user32 APIs（`RegisterHotKey`、分层窗体）  
  3. Languages: C#, TypeScript
</details>

<details>
  <summary>License</summary>

  License: MIT。
</details>

<details>
  <summary>FAQ / 故障排查</summary>

  - 热键无效？可能被其他应用占用，请在 Settings 中更换热键。  
  - 副屏未熄？检查设置中是否勾选对应显示器，延迟是否为 0（需要立即熄屏时设为 0）。  
  - 遮罩抢焦点？遮罩使用不激活窗口样式，如仍有问题可通过热键暂停/恢复重置状态。  
</details>

## 🤝 贡献与联系

欢迎提交 Issue 和 Pull Request！  
如有任何问题或建议，请联系 Zheyuan (Max) Kong (卡内基梅隆大学，宾夕法尼亚州)。

Zheyuan (Max) Kong: kongzheyuan@outlook.com | zheyuank@tepper.cmu.edu
本项目 GitHub 链接：https://github.com/MaxMiksa/Monitors-Focus-Assistant
