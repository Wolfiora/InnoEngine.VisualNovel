# InnoPlugin.VisualNovel

主插件。把子插件组合成完整的视觉小说玩法与制作体验，是普通用户唯一需要安装的包。

| 项 | 值 |
| --- | --- |
| pluginId | `innoplugin.visualnovel` |
| displayName | `InnoPlugin.VisualNovel` |
| 依赖 | `innoplugin.visualnovel.narrative`、`innoplugin.visualnovel.stage`、`innoplugin.visualnovel.ui` |

## 负责

- `NovelDirectorSystem : GameSystem`：挂在普通 Scene 上，驱动剧情执行、变量、存读档、回想与自动/快进等 VN 规则。
- 把叙事指令翻译成对原生 GameObject/组件的操作：角色立绘、背景、镜头（Stage）、对话框与选项（UI）。
- VN 专用的组件预设与场景模板（标准 VN 场景 = 普通 Scene + Director 系统 + Stage/UI 对象）。
- 编辑器：剧本编辑、叙事地图、时间线、运行调试器等 EditorPanel；Play Mode 下从任意节点预览；Game View 中的 VN 调试叠加。

## 不负责

- 通用叙事图与执行器（Narrative）、2D 舞台渲染（Stage）、UI 控件与布局（UI）本身。
- Scene View / Game View、Inspector、Hierarchy、Undo、资产数据库、序列化：全部使用宿主。
- 任何外部功能插件（Rendering2D、Canvas 等）。

## 目录

```text
Assets/
├── Runtime/   Director 系统、VN 组件、存档与指令绑定
└── Editor/    剧本 / 叙事地图 / 时间线 / 调试器面板与视口 contributor
```
