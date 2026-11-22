# InnoPlugin.VisualNovel.Stage

通用 2D 舞台子插件：在原生 Scene 中渲染分层 2D 内容。替代对 Rendering2D 的依赖，但只实现 VN 所需的最小通用集合，可以被其他 2D 叙事或演出类游戏复用。

| 项 | 值 |
| --- | --- |
| pluginId | `innoplugin.visualnovel.stage` |
| displayName | `InnoPlugin.VisualNovel.Stage` |
| 依赖 | 无 |

## 负责

- 挂在 GameObject 上的舞台组件：图层、精灵/立绘、背景、舞台镜头，以及淡入淡出、溶解、位移等过渡。
- 通过引擎 `IRenderModel` 把舞台组件绘制进宿主提供的 `RenderOutputSession`；同时可以绘制其他插件发布的中立视图内容。
- 编辑器：Scene View 中的选取、gizmo 与对齐辅助（`EditorViewportContributor`），舞台组件的 InspectionDrawer。

## 不负责

- 剧情、对白、选项与任何 VN 规则。
- 视口、渲染目标与呈现：由宿主 Scene View / Game View 拥有。
- UI 控件与布局（UI 子插件负责）。

## 目录

```text
Assets/
├── Runtime/   舞台组件、渲染模型、过渡
└── Editor/    视口 contributor、gizmo、检查器
```
