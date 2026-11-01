# 前端工程

React + TypeScript + Tailwind CSS + shadcn/ui + Motion + Lucide。桌面由相邻的 `MonitorsFocus.WebHost` 承载，硬件控制由C#核心负责。

- `npm ci`：按锁文件安装。
- `npm run dev`：启动本机Vite，`/?preview=1`为有明确标识的视觉预览。
- `npm run build`：类型检查并生成dist；由宿主复制为wwwroot。
- `npm run lint`：Oxlint检查。

统一字体、颜色、空间、字号和连续角在 `src/index.css`。shadcn组件源码在 `src/components/ui`，可按项目规范维护，不盲目覆盖回默认样式。只使用14/16/24三档字号；Motion尊重减少动态效果。

运行与打包见[开发说明](../../docs/development.md)。

## 中英文

`src/lib/i18n.ts` 导入 Core 的 `strings.json`，统一中英文与参数插值；React 通过 `useI18n` 读取语言。页面标识与显示标签分离，语言变化不重建草稿。用户场景名只通过明确的默认名称标记翻译。详见 [开发文档](../../docs/development.md#中英文与语言偏好)。
