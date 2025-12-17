# Monitors Focus | [English](README.md)

![License](https://img.shields.io/badge/license-TBD-lightgrey) ![Tech](https://img.shields.io/badge/.NET-8%20WinForms-512BD4) ![Status](https://img.shields.io/badge/status-v0.1.0-blue)

✅ **托盘常驻 | 全局暂停/恢复热键 | 纯黑点击穿透遮罩**  
✅ **光标进入副屏即刻亮屏 | 返回主屏延迟熄屏（默认 180 秒，可自定义）**  
✅ **Windows 10/11 | 双/三屏布局 | 不改变窗口位置**  

Monitors Focus 是一款轻量级 Windows 托盘应用，通过纯黑、点击穿透的遮罩自动熄灭选定副屏，光标移入立即点亮，返回主屏后按设定延迟再次变黑。

<p align="center">
  <img src="Presentation/demo.svg" width="720" alt="Monitors Focus 演示"/>
</p>

## 功能

| 功能 | 描述 |
| :--- | :--- |
| ✨ 鼠标跟随黑屏 | 实时跟踪光标，按可配置延迟（默认 180 秒）自动遮罩选定副屏。 |
| 🛡️ 点击穿透 | 纯黑顶置窗口，不抢焦点，不出现在 Alt-Tab/任务栏，鼠标可穿透。 |
| ⚙️ 快速设置 | 托盘设置：延迟、透明度、监控屏选择、全局热键（默认 Ctrl+F12）、开机自启。 |
| 🎛️ 随时暂停 | 热键或托盘菜单一键暂停/恢复自动化。 |

## 使用方法
1. 构建或下载 `MonitorsFocus.exe`（构建方法见下方 Developer Guide）。
2. 运行后在托盘打开 **Settings**，选择需要遮罩的副屏、延迟、透明度、热键。
3. 在主屏工作，返回主屏后延时黑屏，鼠标移入副屏立即亮屏。

<details>
  <summary>运行环境与限制</summary>

  - Windows 10/11 (x64)，若非自包含需 .NET 8 桌面运行时。
  - 为双/多屏设计，支持 PerMonitorV2 DPI 缩放。
  - v0.1.0 暂未包含 DDC/CI 亮度控制（仅遮罩）。
</details>

<details>
  <summary>Developer Guide</summary>

  ```bash
  dotnet restore
  dotnet build
  dotnet run --project src/MonitorsFocus
  # 单文件发布
  dotnet publish src/MonitorsFocus -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
  ```
</details>

<details>
  <summary>开发栈</summary>

  1. Packages & Frameworks: .NET 8, WinForms  
  2. Interfaces & Services: Windows user32 APIs（`RegisterHotKey`、分层窗体）  
  3. Languages: C#
</details>

<details>
  <summary>License</summary>

  License: TBD。
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

Zheyuan (Max) Kong: kongzheyuan@outlook.com | zheyuank@andrew.cmu.edu
