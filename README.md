# InnoEngine.VisualNovel

InnoEngine 的视觉小说插件集合。仓库本身不是插件，而是一组 `InnoPlugin.VisualNovel.*` 源码插件；普通用户只需安装主插件 `InnoPlugin.VisualNovel`（导出时可内嵌全部依赖），子插件也可以单独导出复用。

> 当前阶段只有仓库与插件骨架，尚未实现任何功能。

## 插件

| 插件 | pluginId | 依赖 | 职责 |
| --- | --- | --- | --- |
| [InnoPlugin.VisualNovel](Plugins/InnoPlugin.VisualNovel/README.md) | `innoplugin.visualnovel` | Narrative、Stage、UI | 主插件：`NovelDirectorSystem` 与 VN 规则、制作面板 |
| [InnoPlugin.VisualNovel.Narrative](Plugins/InnoPlugin.VisualNovel.Narrative/README.md) | `innoplugin.visualnovel.narrative` | 无 | 叙事图、剧本编译与无画面执行器 |
| [InnoPlugin.VisualNovel.Stage](Plugins/InnoPlugin.VisualNovel.Stage/README.md) | `innoplugin.visualnovel.stage` | 无 | 原生 Scene 中的分层 2D 舞台渲染与过渡 |
| [InnoPlugin.VisualNovel.UI](Plugins/InnoPlugin.VisualNovel.UI/README.md) | `innoplugin.visualnovel.ui` | 无 | 游戏内 UI 控件、布局、文本呈现与输入导航 |

## 仓库结构

```text
InnoEngine.VisualNovel/
├── Plugins/                       每个子目录是一个独立的 Inno 源码插件工程
│   └── InnoPlugin.VisualNovel.X/
│       ├── Settings.Project.inno  工程身份（projectId = pluginId），引擎生成
│       ├── README.md              职责与边界
│       └── Assets/                唯一内容根，原样进入 .iplugin
│           ├── Runtime/           游戏 + 编辑器编译（Runtime / Authoring API）
│           └── Editor/            仅编辑器编译（Editor API）
├── Sandbox/                       开发宿主工程，安装全部插件后在 Editor 中联调
├── Documentation/
│   ├── Architecture.md            架构、依赖方向与引擎缺口
│   └── Development.md             开发流
├── Tools/
│   ├── Plugins.psd1               插件清单与依赖（唯一声明处）
│   └── Build-Plugins.ps1          按依赖顺序调用引擎导出器
├── .editorconfig                  沿用 InnoEngine code-format
└── global.json                    与引擎相同的 .NET SDK
```

## 开始

见 [开发流](Documentation/Development.md) 与 [架构](Documentation/Architecture.md)。

## 许可

[Apache-2.0](LICENSE)
