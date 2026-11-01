# 项目协作规则

## 当前状态

- 已发布基线为 v2.0.0 显示器控制中心（.NET 10）；v1.0.0 WinForms 入口保留兼容，当前工作区包含发布后的修复。2026-11-01 已将包含全屏及最大化保持亮屏功能的自包含包替换至本机 `publish/app` 并重启核心；原设置与自启路径保留。
- 前端已确定为 TypeScript + React + Tailwind CSS + shadcn/ui + Motion + Lucide；C# WebView2 宿主按需打开，关闭后释放。已停止 WinUI 3 路线，不引入 Tauri/Rust。
- `src/MonitorsFocus` 是常驻核心和兼容旧版入口，`--center --background` 启动新核心；`src/MonitorsFocus.Core` 为协议与纯计算；`src/MonitorsFocus.Web` 为界面；`src/MonitorsFocus.WebHost` 为桌面宿主。没有 Web 后端或生产 HTTP 服务。
- 功能范围：双屏亮度、保持比例的绑定、每日计划与渐变、场景、专注和登录自启。已按用户要求移除全部应用快捷键，包括新版固定 Ctrl+F12 和旧版自定义热键；暂停、恢复使用面板或托盘，场景快捷键不再列入需求。当前工作区新增专注选项「副屏全屏或最大化时保持亮屏」，默认开启；独立于前台焦点检测，恢复正常亮度并隐藏专注遮罩，窗口还原、最小化、关闭或移走后重新计时。仅硬件色温，内屏暂不调色；不实施软件 Gamma。
- 硬件读取、构建与假设备恢复检查已有验证；AOC U27G4 的 DDC 增益写入、预设切换与原色恢复已实测，内屏只调亮度；硬件写入恢复、休眠重连、长时间资源和完整交互仍需验收，不把编译通过等同于完成。

## 开发约定

- 当前开发版完成简体中文 / English / 跟随系统：通用页即时预览，保存语言偏好后面板与托盘同步，无需重启。统一文案在 `src/MonitorsFocus.Core/strings.json`，C# 内嵌与 Web 类型检查共用；导航使用稳定标识。仅带 `DefaultName` 标记且未改名的新默认场景随语言变化，已有及自定义名称原样保留。旧配置缺少语言时跟随系统，首次迁移继承旧版语言。本轮集成已生成独立自包含包，未替换 `publish/app`。

- 当前开发版场景同时保存逐屏正常亮度/色温、绑定开关及比例权重、专注开关和受控副屏规则。默认全部副屏，也可指定副屏；空列表表示不控制任何副屏，主屏始终排除。手动切换与时间计划节点共用场景行为；规则在节点开始时生效，不随渐变反复重置专注计时。旧场景缺少行为时继承加载时的当前设置，不在读取时改写用户文件。此变更尚未替换本机 `publish/app`。

- 每日计划到达节点后使用 smoothstep 缓入缓出，新增节点默认30分钟，可延长至下一节点前；跨午夜按当前时间直接求值，不保存逐帧序列。计划色温不按100 K截断，只在实际RGB增益变化时写硬件；手动滑条与场景立即应用，手动保持到下一节点。计算与慢设备合并请求已自动验证，真实长时间观感仍待验收。

- 集成验收见 `docs/integration-acceptance.md`：35项自动检查与中英文组合交互通过。离线绑定不限制在线屏幕，未入组新屏可单调；保存语言等无关设置不重播计划规则；实时手动接管在下一节点从当前目标渐变，休眠错过节点仍按时钟求值。场景支持原色目标与安全删除，来源区区分手动场景和时间计划。真实硬件联动与长时运行仍待验收。

- 用户已选 A「随手调节」。沿用 `docs/design/` 规范与还原清单。全部无衬线，只用14/16/24三档字号；颜色、空间、圆角参数集中在 Web 的 `src/index.css`，矩形采用连续超椭圆角。
- shadcn组件在项目内维护，避免引入默认模板视觉。Motion尊重减少动态效果；Lucide统一图标。
- 设备句柄只由C#核心持有；WebView桥只开放明确命令，验证来源，不暴露通用Shell/文件系统执行能力。前端不直接调用硬件。
- 登录自启只保留一条 `MonitorsFocus` 条目，统一指向 `--center --background` 的自包含包 `publish/app/core`；不把 `bin/Release` 的框架依赖输出设为自启。
- 设备命令串行，等待超时不代表底层调用取消；释放句柄前必须等在途操作结束。调色前无法读取颜色预设时报告失败，不继续写增益。UI错误不能假装硬件成功。
- 配置保存检查Revision，冲突保留草稿；不覆盖其他操作的新设置。
- 首次迁移先备份旧配置，继承自启、专注延迟、硬件亮度与语言偏好；读取或迁移失败不改已有自启。重新检测按稳定设备标识保留会话内色温，探测恢复后重新应用；退出按最新正常亮度恢复临时专注限制。
- 自动维护本文件、双语README、docs/INDEX.md和相关设计文档；描述当前真实行为，规划单独标记。
- 构建入口为 `scripts/build.ps1`；生产包先构建Web，再构建C#，不依赖Vite/Node常驻。
- `Files/` 是历史本地资料，不驱动流程。不自动提交、推送、发布。阶段完成后清理本轮临时文件，保留用户数据与构建缓存。

## 验证与运行

```powershell
./scripts/build.ps1
npm --prefix src/MonitorsFocus.Web run lint
dotnet run --project tests/MonitorsFocus.Tests -c Release --no-build
./src/MonitorsFocus.WebHost/bin/Release/net10.0-windows/MonitorsFocus.WebHost.exe
```

使用global.json指定的.NET 10 SDK。本机SDK在 `D:\Apps\dotnet`，可传 `-Dotnet D:\Apps\dotnet\dotnet.exe`；框架依赖开发运行还需将 `DOTNET_ROOT` 和 `DOTNET_ROOT_X64` 指向该目录。开发与打包细节见 `docs/development.md`。
