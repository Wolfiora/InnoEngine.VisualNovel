# 架构

本文描述 `InnoPlugin.VisualNovel.*` 插件集合的结构决定。当前只有骨架，下面的“负责”均为规划，尚未实现。

## 1. 基本原则

- **纯插件**：不修改 InnoEngine 源码，只使用引擎正式脚本 API（Runtime / Editor / Authoring scope）与正式扩展点。
- **不依赖外部功能插件**：不使用 Rendering2D、Canvas 等；VN 需要的 2D 舞台与 UI 由本仓库的通用子插件提供。
- **原生对象模型**：一切运行时内容都是普通 Scene 中的 GameObject、组件与 GameSystem，不建立平行的场景、对象、渲染或 UI 框架。
- **宿主拥有工作台**：Scene View、Game View、Hierarchy、Inspector、Play Mode、Editor History、Assets、Serialization 都由引擎拥有；插件只提供 contributor、面板、检查器、导入器等扩展。

## 2. 为什么拆成子插件

| 理由 | 结果 |
| --- | --- |
| 叙事、舞台、UI 是三种互不相关的通用能力，其他游戏也会单独需要 | 各自成为无依赖的子插件，可单独导出 |
| 禁止依赖 Rendering2D / Canvas，但又不能把渲染和 UI 写成 VN 私有框架 | Stage 与 UI 作为“通用 2D 舞台 / 通用 UI”存在，VN 只是它们的一个使用方 |
| VN 规则（Director、存档、对话框预设）只对 VN 有意义 | 集中在主插件，组合三个子插件 |
| 引擎按 manifest 依赖校验跨插件程序集引用 | 依赖方向在包层面被引擎强制，不需要额外校验工具 |

暂不拆分的候选（需要时再按同样规则新增）：音频通道（BGM / SE / Voice 混音）、本地化、存档槽。当前它们属于主插件。

## 3. 插件与边界

| 插件 | 负责 | 不负责 |
| --- | --- | --- |
| `InnoPlugin.VisualNovel` | `NovelDirectorSystem : GameSystem`、VN 规则、存读档、对话框/选项等预设、剧本/叙事地图/时间线/调试器面板 | 通用叙事、渲染、UI 控件本身 |
| `InnoPlugin.VisualNovel.Narrative` | 基于引擎 Graph 的叙事图（唯一控制流权威）、剧本编译、无画面执行器、图编辑器与导入器 | 任何呈现 |
| `InnoPlugin.VisualNovel.Stage` | 舞台组件（图层、精灵、背景、镜头、过渡）、`IRenderModel`、Scene View gizmo 与选取 | 剧情、UI、视口本身 |
| `InnoPlugin.VisualNovel.UI` | UI 组件（面板、文本、按钮、布局）、富文本/逐字显示、焦点导航、中立视图内容发布 | VN 规则、平台窗口/IME 等能力 |

## 4. 依赖方向

```text
                 InnoPlugin.VisualNovel
               /           |            \
              v            v             v
   .Narrative          .Stage           .UI
              \            |            /
               v           v           v
          InnoEngine 正式脚本 API 与扩展点
```

- 子插件之间互不依赖。UI 与 Stage 的协作通过引擎中立契约完成：UI 发布视图内容（`IViewContentSource` 一类契约），Stage 的渲染模型按宿主 `RenderOutputSession` 统一绘制。任何一方换成其他实现都不影响另一方。
- Runtime 程序集不得引用 Editor 程序集（引擎强制）。
- 依赖唯一声明在 `Tools/Plugins.psd1`，构建时作为 `--dependencies` 交给引擎导出器写入 `Plugin.inno`。

## 5. 要求一：不改源码、纯插件、不妥协

- 每个插件都是标准 Inno 源码工程：`Settings.Project.inno`（projectId = pluginId）+ 唯一内容根 `Assets/`。`Plugin.inno` manifest 由引擎导出器生成，只含源码与资源，没有预编译二进制。
- 代码只走正式脚本 API；扩展编辑器只用 `EditorPanel`、`EditorModule`、`EditorViewportContributor`、`InspectionDrawer`、`AssetImporter`、Graph editor 等正式扩展点。
- 引擎缺的通用能力记在第 9 节，作为引擎改进提出，而不是在插件里私建替代品。这是“不妥协”的含义：插件里不出现会和引擎未来能力冲突的旁路。

## 6. 要求二：融合已有 Scene / Game 面板

- **Scene View**：舞台与 UI 对象就是 Scene 里的 GameObject，天然出现在 Hierarchy、Inspector 与 Scene View 中。Stage / UI 只通过 `EditorViewportContributor` 增加 gizmo、选取与布局辅助，不创建第二个视口。
- **Game View / Play Mode**：进入 Play Mode 后，`NovelDirectorSystem` 在宿主 Game View 中驱动真实游戏画面，得到类似 LetsGal Studio 的“所见即所得”演示；调试器面板只读取 Director 状态并提供跳转。
- **VN 专用面板**：剧本、叙事地图、时间线、调试器做成 `EditorPanel`，与 Scene/Game View 并列停靠，编辑通过引擎 History 撤销。

## 7. 要求三：结合 Scene / Object 体系，与其他插件共存

- 一个 VN 场景 = 普通 Scene + 挂载的 `NovelDirectorSystem` + 普通 GameObject（角色、背景、镜头、对话框）。Director 只拥有剧情状态、变量与存档，场景对象仍归 Scene。
- 叙事指令被翻译为对组件的普通操作（显示立绘 = 改 Stage 组件属性；显示台词 = 改 UI 文本组件），因此任何插件提供的组件都能被剧情驱动，剧情也能出现在任意游戏场景中。
- 同一 Scene 可以同时挂载其他插件的 GameSystem（例如战斗、地图探索），彼此通过场景对象与引擎事件协作；VN 不独占场景、相机或输入。
- 子插件不感知 VN：别的游戏可以只装 Stage 或 UI。

## 8. 插件内部结构

```text
Plugins/<Name>/
├── Settings.Project.inno   工程身份
├── README.md               职责与边界
└── Assets/
    ├── Runtime/            Runtime 程序集：组件、GameSystem、数据；Authoring 注解也放这里
    └── Editor/             Editor 程序集：面板、模块、视口 contributor、检查器、导入器
```

- 引擎程序集 scope 只有 Runtime 与 Editor；Authoring 是脚本 API 的编译期注解 scope，写在 Runtime 代码中，播放器编译时自动擦除，因此不设单独目录。
- 程序集名规划为 `<Name>`（Runtime）与 `<Name>.Editor`，命名空间与之一致，在写入第一份代码时由 Editor 创建程序集定义。

## 9. 引擎缺口（不在 VN 内绕路）

| 缺口 | 影响 | VN 侧做法 |
| --- | --- | --- |
| 运行时序列化根入口（存档） | 存读档 | 等待引擎 API；只设计存档数据边界 |
| UI 值回读与布局矩形查询 | UI 控件、对话框排版 | 以引擎文本/渲染契约为准，缺失处记录 |
| 窗口模式（全屏/窗口切换） | 设置界面 | 记录，不调用平台层 |
| IME 输入 | 玩家命名等文本输入 | 记录 |
| 手柄输入 | UI 导航 | 先支持鼠标、键盘 |
| 剪贴板 | 编辑器/游戏内文本复制 | 记录 |
| 视频播放 | OP/ED、过场 | 记录 |

缺口清单随引擎演进更新；引擎提供能力后，对应插件直接改用。
