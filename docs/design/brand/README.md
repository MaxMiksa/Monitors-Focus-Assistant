# 应用图标

意象为「双屏 + 亮度」：后屏半透明代表调暗，前屏实白代表调亮。

| 用途 | 文件 | 说明 |
|---|---|---|
| 主图形 | [logo.svg](logo.svg)、[logo-512.png](logo-512.png) | 256 网格，项目蓝 `#125cd0` 连续曲率底板 |
| exe 与窗口 | `src/MonitorsFocus/Assets/app.ico` | 16–256px；48px 起显示太阳光芒，更小只保留圆点 |
| 托盘 | `src/MonitorsFocus/Assets/tray-dark.ico`、`tray-light.ico` | 无底板单色，按任务栏深浅色自动切换 |
| 网页 favicon | `src/MonitorsFocus.Web/public/favicon.svg` | 与主图形相同 |

ico 由同一套几何参数栅格化生成；修改图形时需同步更新以上文件。
