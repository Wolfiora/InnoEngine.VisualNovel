# InnoPlugin.VisualNovel.UI

通用屏幕 UI 子插件：游戏内界面的控件、布局与文本呈现。替代对 Canvas 的依赖，可以被其他游戏单独复用。

| 项 | 值 |
| --- | --- |
| pluginId | `innoplugin.visualnovel.ui` |
| displayName | `InnoPlugin.VisualNovel.UI` |
| 依赖 | 无 |

## 负责

- 挂在 GameObject 上的 UI 组件：面板、文本、按钮、列表、滚动与基础布局。
- 富文本与逐字显示等文本呈现能力（对话框是由这些通用控件组合出的 VN 预设，属于主插件）。
- 输入焦点与导航（鼠标、键盘；手柄等待引擎能力）。
- 以引擎中立视图内容契约（`IViewContentSource` 等）发布绘制内容，不直接依赖 Stage。
- 编辑器：UI 组件的 InspectionDrawer 与 Scene View 布局辅助。

## 不负责

- 对白推进、选项结果、存读档界面逻辑等 VN 规则（主插件负责）。
- 在插件内自建窗口、IME、剪贴板等平台能力；缺失时记录为引擎缺口。

## 目录

```text
Assets/
├── Runtime/   UI 组件、布局、文本呈现、输入导航
└── Editor/    检查器与布局辅助
```
