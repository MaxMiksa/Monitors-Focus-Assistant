# 界面设计入口

用户已选择 A「随手调节」：逐屏滑条和色温优先，场景为快捷入口。原始候选图保存在 [candidates/controls-first.png](candidates/controls-first.png)；B 为未选候选。

- [使用体验](experience.md)：任务路径和状态。
- [设计基础](foundations.md)：统一参数、字体、连续曲率。
- [共享组件](components.md)：控件及验收规则。
- [A 方案还原核对](pages/controls.md)：逐区域验收与尚未解决的差异。
- [应用图标](brand/README.md)：Logo、exe、托盘与 favicon 文件。

界面实现位于 `src/MonitorsFocus.Web`，桌面宿主为 `src/MonitorsFocus.WebHost`。采用真实截图核对，生成图只定义视觉方向，不作为曲线计算、设备支持或成功状态的证据。图中内屏色温调整已按用户决定取消；只使用硬件调色。

参考：Twinkle Tray 的直接调节路径与 Windows 原生控制器的键盘操作。候选图由内置生图工具生成；提示词核心为四屏成套设计：双屏快捷面板、时间计划、场景编辑、单屏不可用状态，强调滑条优先、无营销图、无多层卡片、清晰中文。

使用用户指定的 Frontend Guidelines；项目的三档字号、全无衬线和连续圆角要求优先于通用默认值。
