# Scene 对象与引擎 UI 的接入

2026-11-03 增量：原生网格进入统一图像计划，Include UI 效果在网格合成后执行；渲染资源闭包与延迟捕获同时保留当前/转场旧 UI 的纹理。多 Canvas 输入先通过引擎几何提取实现更新后的变换，再使用引擎命中；提取出的增量进入同一缓存，不能丢弃导致后续帧缺少字形。输入在 System 更新中处理，Game 输出渲染只排队。18 项定向证据见 `NativeUi-PageNavigationContracts.txt`，包含真实无窗口 RuntimeSession 中页面按钮的显示、关闭、替换、重复渲染和目标失效。

2026-11-03：根据用户新增要求，UI 重构改为同时复用原生 Scene 与引擎 UI 服务。早期“自有完整 UI 布局与输入实现”的计划不再作为新原生 UI 的目标架构。当前仍在迁移，不代表全部页面或 Play 游戏壳完成。

## 所有权与制作流程

- 作者通过 Scene System Inspector 创建 UI Canvas；Canvas、Page、Layout Container 的 Inspector 可创建按钮、文字、Boolean 开关及横向/纵向/Grid 容器，并选中新对象。它们全部是真实 GameObject/Component，编辑操作使用原生 SceneEdits 和属性 History。页面可保存为原生 Prefab。
- 对象身份、启停、父子结构和 Transform 以 Scene 为准。组件只保存布局、文字、绑定和动作等作者参数。
- `NovelEngineUiProjection` 将真实子对象生成引擎 UI 文档片段。片段不保存为另一份剧情或控件资产，没有自己的对象身份分配、删除规则或撤销栈。
- `NovelEngineUiSurface` 通过公开 `InnoEngine.UI.IUiService` 复用布局、字形、焦点、输入、事件及渲染输出。按钮动作依事件中的对象 ID 定位，拒绝其他文档、失效对象、禁用控件及同一样本重复激活。
- 文字与样式修改按元素更新；结构变化才替换片段。没有每帧重新加载文档或重置焦点。
- 默认模板 `Assets/BuiltIn/SceneUi.rml` 只提供字体、基础样式和场景内容插槽。字体依赖通过引擎 RML/Font 导入器收集；插件安装后仍使用所属资源源中的相对依赖。作者无需编写 RML。
- RML 编码只在 Integration 的适配目录内；不引用 RmlUi 实现程序集，不依赖 Canvas 或 Rendering2D。测试工具可以使用明确选定构建中的公开宿主 API 与原生后端。

## 制作态显示与选择

`NovelSceneSystem.CaptureScene` 将原生舞台输出和引擎 UI 的三角形输出组合。`NovelSceneUiPresentation` 持有场景所属的引擎上下文，删除/停用 Canvas 后回收；Scene/Game 的制作态视角消费相同 UI 输出。Scene 选择通过引擎的指针事件定位真实对象，不再拿另一套布局算法猜测控件位置。

`NovelEngineUiFrameCache` 接收引擎发布的网格与纹理增量，转换为独立帧。保留任意三角形拓扑、每顶点颜色、UV 和裁切，不能把引擎文字网格近似成一个矩形。渲染管线与截图管线均提交这些三角形。

引擎目前通过 Update 实现布局刷新。制作态仅在内容变化后提交无按键、无点击、无文字的中立输入，并仅保留引擎发出的焦点/悬停状态用于视觉反馈；不会调用 VN 会话 Tick、执行动作、修改 Scene 或播放音频。未修改的重复 Capture 不再刷新输入。文档 CSS 动画使用引擎自己的时间，目前没有公开的暂停/指定时间能力，因此不能宣称任意外部模板的动画已具备确定性的编辑暂停；默认模板不包含动画。

## 选定公开契约的具体限制

SDK 为 `artifacts/EngineBuildSnapshot-20261031-0611`。`NativeUi-EngineApi.txt` 从正式 Runtime 脚本表面提取了接口，实际存在的操作包括 CreateContext、LoadDocument、SetContent、SetText、SetAttribute、Update、Render、DrainEvents 与 HasElementAtPoint。

1. `UiEvent` 只公开 type/document/targetId；`Change` 没有修改后的值，IUiService 也没有 GetValue/GetAttribute。原生输入框、选择器、滑块的值回写需要相应契约，不能伪造一个成功的 Change。按钮命令不受此限制。Boolean 切换也可以在 Click 后修改绑定数据，不应扩大为所有开关都无法实现。
2. 没有任意元素 Focus/GetFocus 调用，但这不再阻塞原生页面初始焦点与页面返回恢复。适配器使用正式 RML `autofocus` 和 `IUiService.ShowDocument`，只在页面/结构确实变化时应用；记忆来自实际引擎 Focus 事件，按原生页面归属并排除失效目标。Tab 导航及普通样式更新不重新抢焦点。实际无窗口后端已验证初始目标、页面切换返回、目标禁用/重新启用和错误跨页引用。
3. 没有读取元素最终布局矩形的操作。Scene 可通过真实指针事件选择对象；自动布局子项的精确矩形手柄、Frame Selection 范围和对齐辅助线仍不能由该接口直接得到。
4. UI 输出的裁切为上下文空间的轴对齐矩形。插件现在先在上下文空间裁切三角形，再应用原生 Canvas 变换；旋转、反射后的精确裁切不再使用扩大后的包围框代替。面积、UV 插值与冻结帧资源生命周期已通过逻辑验证。

## 已补充的控件与排版

- Boolean 开关复用引擎 Click 与键盘导航；绑定值交给数据源 TryWrite，失败不改作者默认值；未绑定时值由真实组件持有。没有为开关实现第二套指针或焦点逻辑。
- Grid 使用引擎 Flex 行/单元布局。技术包装节点只负责分配布局空间，不是独立创作对象；真实子项的身份、顺序和启停仍来自 Hierarchy。隐藏首项后下一项进入首格已通过真实无窗口后端验证。
- 富文本继续使用 VN 现有解析结果，通过引擎字体与文档排版表达颜色、字号、强调、描边/阴影和 Ruby。已测试组合字素、零揭示/部分揭示/完整揭示与字形几何。未揭示部分保留布局内容；HTML 特殊字符转义，不把游戏文字当作任意文档元素。
- Scroll 组件将滚动和裁切配置交给引擎；随后已通过真实无窗口后端的滚轮、裁切与点击行为检查。滚动条外观及实际桌面使用体验仍未验收。
- 页面/容器创建、原生组件 Undo/Redo、Canvas Prefab 实例的焦点引用重映射、导入字体依赖和 Scene 保存重开已通过原生编辑器服务验证。

## 明确尚未完成

Play 中的旧 NovelGameView 游戏壳还没有整体接入此对象模型。全套页面、图片注册与行内图片、主题状态、跨 Canvas 模态管理及对象快照仍需迁移。原生列表条目 Prefab 已有实现，但完整游戏壳与快照尚未接入。输入型控件的值回写仍受所选公开契约限制。已提供的适配器对未接入的控件明确拒绝，不降级到另一套输入引擎。

字体选择通过模板导入依赖和 `.vn-font-N` 样式类实现，N 对应 `UiDocumentAsset.fonts` 中首次出现的字体族顺序。引擎拥有实际字体注册与版本映射，插件不推测它的私有名称或版本后缀。Canvas 默认字体与控件覆盖均使用这些样式；没有声明的字体/样式明确报错，保留上一份可用文档。`NativeUi-FontsFormal.txt` 和 `NativeUi-FontsContracts.txt` 的四项定向检查通过，覆盖真实多字体输出、覆盖清除和错误候选。

可视化字体模板自动生成尚未交付：所选宿主中实际导入的 FontAsset 身份有效、isMissing=false，但 assetPath 为空，再次按 ID 加载也相同。无法从这些对象可靠取得 RML 字体依赖的来源；正式 Assets 表面也没有按 ID 查询 AssetInfo 的操作。失败验证记录在 `NativeUi-FontAuthoringContracts.txt`，不可用的生成按钮及实现已经移除。默认模板和预先声明的多字体模板不受此问题影响；不能因此声称任意字体选择都已有无代码闭环。

目前的无窗口检查不等于 GPU 画面、鼠标拖拽、手柄、IME 或完整 UI/UX 验收。没有启动编辑器窗口或操作桌面；需要用户手动检查时另给明确步骤。

## 本轮证据

- `NativeUi-ClippingFormal.txt`：最新十程序集正式编译、候选激活及重编译通过。
- `NativeUi-ClippingContracts.txt`：13 项相关检查通过；覆盖真实无窗口 UI、开关写回、富文本/Grid、旋转裁切、场景权威、原生 History/Prefab/保存重开与场景生命周期。它们仍不能证明完整 Play 迁移。
- `NativeUi-HistoryContracts.txt`：本轮专门补充的 UI 创建与原生持久化检查通过。

- `artifacts/Reports/NativeUi-EngineFinalContracts.txt`：23 项相关检查通过、0 失败。真实 RmlUi 后端生成首帧几何、字体纹理；鼠标样本、Tab/Enter、实际 System 制作态捕获、连续增量帧、原生停用和 Scene 选择通过。其他条目是保留的原生场景与旧游戏壳回归，不代表新 UI 已覆盖所有旧功能。
- `artifacts/Reports/NativeUi-EngineContractsBuild.txt`：测试程序基于选定宿主程序集和正式脚本产物编译通过。
- 托管 SDK 选用 `EngineBuildSnapshot-20261031-0611`；无窗口 UI 检查明确使用本仓库 `artifacts/EngineEditorSnapshot/native/ui/windows-x64/inno-ui-debug.dll`，SHA256 为 `1306FD3332707D1473EB69F2253D14FB0311FA82F26C34AFF273835EBADCA3F8`。检查不启动窗口、不调用 GPU、不发送系统级键鼠输入；输入样本只传给隔离的引擎 UI 上下文。
- 全部生成资产、原生依赖副本、临时消费工程与日志均在本仓库 artifacts；引擎源码未修改。
正式脚本与原生候选激活记录：`artifacts/Reports/NativeUi-EngineFinalFormal.txt`；十程序集依赖检查：`artifacts/Reports/NativeUi-EngineFinalArchitecture.txt`。

## 控件反馈与原生集合进展

NativeUi-FeedbackContracts.txt 和 NativeUi-ListsContracts.txt 的五项定向检查通过，含实际无窗口滚轮/裁切/点击穿透、引擎事件驱动的状态颜色、真实 Prefab 条目、键重排身份保持、重复键失败保留和清理生命周期。时间线 Opacity 可写真实 UI Layout，同时拥有舞台 Image 和 UI Layout 的歧义目标被原子拒绝。新增 List Inspector 与创建入口；这是组件和绑定能力，Play 游戏壳/全部页面及条目完整快照尚未迁移。生产渲染和选择不创建列表对象。

`NativeScene-LifecycleFormal.txt` 正式编译与 `NativeScene-LifecycleContracts.txt` 的 15 项相关检查通过，已包含页面焦点恢复、字体覆盖、镜头图层/粒子视差和显式 StopGame 后不被下一次 Update 自动重启的回归。独立 StartGame 可重新创建故事；被动制作画面仍可捕获。
