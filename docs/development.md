# 开发与构建

已发布 v2.0.0。构建需要Windows、global.json指定的.NET 10 SDK、Node.js 24 LTS、npm及Microsoft Edge WebView2 Runtime。运行成品不需要Node或Vite；自包含打包后不要求另装.NET运行时。

## 工程结构

| 目录 | 职责 |
| --- | --- |
| src/MonitorsFocus | C#托盘核心、DDC/WMI、遮罩、配置、自启；保留旧版入口便于迁移 |
| src/MonitorsFocus.Core | 状态协议、比例计算、每日计划纯计算 |
| src/MonitorsFocus.Web | React/TS界面、Tailwind参数、shadcn源码组件、Motion和Lucide |
| src/MonitorsFocus.WebHost | 薄WinForms窗口承载WebView2；本地资源映射、消息桥和关闭释放 |
| tests/MonitorsFocus.Tests | 无第三方框架的假设备生命周期检查 |
| scripts/build.ps1 | Web优先构建和可选Windows自包含包 |

## 完整构建与启动

```powershell
./scripts/build.ps1
./src/MonitorsFocus.WebHost/bin/Release/net10.0-windows/MonitorsFocus.WebHost.exe
```

本机.NET未加入全局PATH时：

```powershell
./scripts/build.ps1 -Dotnet D:\Apps\dotnet\dotnet.exe
$env:DOTNET_ROOT = 'D:\Apps\dotnet'
$env:DOTNET_ROOT_X64 = 'D:\Apps\dotnet'
./src/MonitorsFocus.WebHost/bin/Release/net10.0-windows/MonitorsFocus.WebHost.exe
```

桌面宿主启动同目录发布包或工作区Release核心，发送 `--center --background`。若旧版已在托盘运行，应先退出旧版，避免旧版单实例锁阻止新核心启动。关闭面板仅退出宿主，核心继续运行；退出核心从托盘菜单操作，并等待硬件恢复。

## 操作入口

应用不再注册全局快捷键，也不提供快捷键设置；场景快捷键不在后续需求中。专注暂停、恢复通过控制面板或托盘操作。标准控件的键盘操作保持可用。

## 登录自启

登录自启只有一条注册表条目：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `MonitorsFocus`，值为 `"<可执行文件>" --center --background`。旧托盘和历史版本写过的 `MonitorsFocus.ControlCenter` 由 `StartupManager` 一并清理，任务管理器“启动应用”里只显示一项，不会出现两个进程抢单实例。核心每次启动按配置校正该条目：安装位置改变或条目被改动后自动重写，已正确时不重复写注册表。

自启必须指向自包含包，不能用 `bin/Release` 下的框架依赖输出：登录会话没有 `DOTNET_ROOT`，若本机 .NET 10 位于 `D:\Apps\dotnet` 且未注册安装位置，启动会因找不到运行时静默失败。当前稳定位置为 `publish/app/core/MonitorsFocus.exe`，面板在同级 `publish/app/desktop/MonitorsFocus.WebHost.exe`；重新发布覆盖这两个目录即可保持自启有效。

## 界面开发

```powershell
npm --prefix src/MonitorsFocus.Web ci
npm --prefix src/MonitorsFocus.Web run dev
```

浏览器打开 `http://127.0.0.1:5173/?preview=1` 是明确标识的示例数据预览，不控制硬件、不把失败回退为演示成功。真实热更新调试运行WebHost并传 `--dev`，只允许固定本地开发源；DevTools仅在此模式开启。普通启动使用打包到wwwroot的本地静态文件，不依赖开发服务器。

```powershell
./src/MonitorsFocus.WebHost/bin/Release/net10.0-windows/MonitorsFocus.WebHost.exe --dev
```

前端通过WebView2消息向宿主提交带请求ID的命令，宿主验证来源和动作白名单，再通过当前用户命名管道请求核心。界面退出取消等待并释放WebView2，不终止核心调度。前端显示的亮度为正常亮度目标，专注可能临时降低硬件亮度；硬件错误另行显示。完整的目标/已应用读回区分仍属硬件验收待办。

## 检查与打包

```powershell
npm --prefix src/MonitorsFocus.Web run lint
dotnet run --project tests/MonitorsFocus.Tests -c Release
./scripts/build.ps1 -Package
```

`pack.bat`代理到同一PowerShell入口。输出到新的 `publish/control-center-时间戳/`，不删除已有发布目录。入口为desktop/MonitorsFocus.WebHost.exe，core目录必须一起分发，不能再按旧版单文件复制。构建不签名、不上传、不发布。

现有自动检查覆盖原生结构、错屏匹配和异步恢复；不替代真实设备、混合DPI、休眠重连和长期低占用验证。依赖通过package-lock.json固定，新增组件后重新检查字体档位、连续角和视觉还原。

Windows下运行完整构建前先停止Vite开发服务器，避免npm ci重装被占用的原生依赖；日常热更新无需重新运行完整构建。
