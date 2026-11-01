# 运行时内存占用：实测与优化路径

测量时间 2026-10-31，本机双屏，Windows 11，发布包 `publish/app`，采样前通过管道发送 `pause` 暂停专注，避免测量过程改变屏幕。
本文区分两个口径：**工作集**（任务管理器「详细信息」的“内存(工作集)”，含与系统共享的映射页）与**专用工作集**（任务管理器「进程」标签默认列，才是本进程真实占用的物理内存）。

## 结论

1. 常驻核心 `MonitorsFocus.exe --center --background` 实测 62 MB 工作集、**11 MB 专用工作集**，120 秒内无增长。62 MB 里约 42 MB 是映射进来的 .NET 10 + WinForms 运行时镜像，这部分与其他 .NET 进程共享。
2. 面板打开时才出现大头：宿主 + 6 个 WebView2 进程合计 423 MB 工作集 / **107 MB 专用工作集**。这是唯一存在“数量级”下降空间的部分。
3. 面板关闭后 6 个 WebView2 进程全部退出、0 残留，核心回落到 61 MB。不存在“关闭后不释放”的问题。
4. 本项目代码本身没有常驻大对象：遮罩是分层窗口 + `SetLayeredWindowAttributes`，不申请全屏位图；50 ms 定时器只做整数比较，稳态几乎不分配。

结论：**常驻侧已经接近 WinForms 自包含应用的下限，不值得为工作集数字重写；要“大幅降低”只能动面板。**

## 实测数据

### 常驻核心

| 时间 | 工作集 | 专用工作集 | 线程 | 句柄 |
| --- | --- | --- | --- | --- |
| 0 s | 62.4 MB | 11.3 MB | 21 | 410 |
| 30 s | 62.3 MB | 11.1 MB | 16 | 402 |
| 60 s | 62.2 MB | 11.1 MB | 13 | 403 |
| 120 s | 62.2 MB | 11.1 MB | 13 | 403 |

### 面板（WebHost + WebView2）

| 浏览器参数 | 进程数 | 工作集 | 专用工作集 | 线程 |
| --- | --- | --- | --- | --- |
| 基线 | 7 | 422.6 MB | 106.7 MB | 263 |
| `--disable-gpu --disable-gpu-compositing` | 7 | 398.5 MB | 92.7 MB | 184 |
| 上一行 + `--renderer-process-limit=1 --max-old-space-size=96` | 7 | 388.8 MB | 91.6 MB | 183 |
| 上一行 + `--single-process` | 2 | 291.0 MB | 79.8 MB | 109 |

面板打开时全应用合计约 485 MB 工作集 / 118 MB 专用工作集；关闭后回到 61 MB。

### 面板进程构成（基线）

| 进程 | 工作集 |
| --- | --- |
| browser | 146.7 MB |
| gpu-process | 83.2 MB |
| renderer | 71.2 MB |
| utility ×2 | 40.7 + 22.9 MB |
| crashpad-handler | 14.7 MB |
| 宿主 `MonitorsFocus.WebHost.exe` | 73.9 MB |

### 核心已加载的运行时镜像（前 8）

`System.Private.CoreLib` 15.3、`System.Windows.Forms` 13.1、`coreclr` 4.5、`Windows.Forms.Primitives` 3.4、`clrjit` 2.0、`Windows.Core` 1.8、`System.Text.Json` 1.8、`System.Drawing.Common` 0.9（单位 MB，合计约 42 MB）。

## 长时间渐变的内存约束（开发版）

每日计划每秒按当前时间直接求值，读取相邻节点和场景，不保存动画帧或展开多日时间线；结果为值类型，不再每秒创建目标字典与目标对象。72小时、259200次逐秒模拟中，计划求值及单屏目标计算新增托管分配为0字节。显示器仍按实际整数亮度与RGB增益能力分步变化，不使用软件Gamma或全屏位图制造中间亮度。

每屏继续使用一个串行worker和一个最新待处理目标；10万次请求在假设备阻塞期间没有新增排队任务，最终只写最新目标。RGB增益不变时跳过硬件写入；增益变化时才创建3元素命令数组。运行时、窗口检测、状态查询和硬件任务仍会产生短期分配，不应把纯计算0分配误解为整个进程0分配，亦未据此宣称已完成真实72小时驻留验收。

## 已排除的路线

- **PublishTrimmed**：.NET 10 直接报 `NETSDK1175`，WinForms 不支持裁剪（本轮实测触发）。
- **NativeAOT**：WinForms 同样不支持；只有先移除 WinForms 才可能，属于 D 之后的追加步骤。
- **WebView2 单进程模式**：数字最省（80 MB 私有），但 Chromium 与 WebView2 均不官方支持，崩溃、输入法、拖放风险高，不采用。

## 可行动项

| # | 动作 | 收益 | 风险 | 工作量 |
| --- | --- | --- | --- | --- |
| A | 面板空闲降级：加载完成后设 `MemoryUsageTargetLevel.Low`，窗口失焦或 5 分钟无操作时 `TrySuspendAsync()` | 预估 30–50 MB 工作集（未实测） | 低 | 0.5 天 |
| B | 浏览器参数 `--disable-gpu --disable-gpu-compositing`（用 `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments`，不污染其他 WebView2 应用） | 实测 工作集 −24 MB、专用 −14 MB、线程 −79 | 低（本地静态页不依赖 GPU 合成） | 0.1 天 |
| C | 面板改原生 WinForms 控件，移除 WebView2 | 面板 485 → 约 75 MB 工作集、118 → 约 15 MB 专用 | 高：放弃 React/Tailwind/shadcn 设计系统，前端 9 个文件作废，界面层重写约 1000 行 | 3–5 天 |
| D | 核心去 WinForms：Win32 隐藏窗口消息循环 + `Shell_NotifyIcon` + 自绘托盘菜单 + `CreateWindowEx` 遮罩 | 预估 62 → 35–40 MB 工作集、11 → 7–8 MB 专用 | 中：托盘菜单、屏幕变化/电源事件、DPI 需重做 | 2–3 天 |
| E | D 完成后启用 NativeAOT | 预估核心约 8–12 MB 工作集 | 中高：JSON 需 source-gen，反射相关代码需清理 | 1–2 天 |
| F | 不常驻，登录后按需启动 | 常驻 0 MB | 高：专注计时、计划全部失效 | — |

## 建议

- 目标只是“打开面板时不刺眼”：做 **B + A**，面板 423 → 约 350 MB 工作集、107 → 80 MB 专用，不改架构。
- 目标是任务管理器上所有数字都是两位数：只能 **C**，用原生界面换掉 WebView2。这是唯一数量级方案，也意味着放弃刚建立的 Web 设计系统。
- 目标是开机更轻、启动更快：**D/E**，但要清楚它降的是工作集，专用工作集只从 11 MB 到 7 MB。

个人判断：常驻侧不值得为数字重写。11 MB 专用对开机自启的后台工具已经合格，把 62 MB 工作集当成“内存占用”会误导后续所有取舍。真正该决定的是产品层面的问题——面板是“调完就关”还是“长期开着”。前者只需 A/B，C 的重写投入换不回体验；后者才需要认真考虑原生界面。

## 复测方法

1. 启动 `publish/app/core/MonitorsFocus.exe --center --background`，用命名管道发送 `{"Action":"pause"}` 暂停专注。
2. 采样 `WorkingSet64` 与 `Win32_PerfRawData_PerfProc_Process.WorkingSetPrivate`，后者才是任务管理器「进程」页显示的值。
3. 面板对比时先记录现有 `msedgewebview2` PID 集合，再启动宿主，用差集定位本应用拉起的进程。
4. 验收补充：面板反复开合 10 次后专用工作集应回到 15 MB 附近；启用 A 后空闲 5 分钟应出现明显下降；跨机器差异主要来自 WebView2 运行时版本与 GPU 驱动。

以上均为工作集口径的单机实测，不构成对其他机器的承诺。
