# Core 复用与 Scene 组件边界验证

本轮按用户要求仅进行无窗口验证，没有 Computer Use、GUI、GPU 输出、音频设备或实机验收。此报告不代表完整 v1 或 P0～P8 完成。

## 已落地的变更

- 核对选定现有构建的正式 Runtime API，记录 Core、数学、事件、图和 Scene 的实际导出；没有从 public 引擎实现类型推断脚本可用性。
- 去掉插件通用 `UiRect`，UI 布局、命中、画布投影、行内图片、渲染和九宫格直接使用 `InnoEngine.Mathematics.Rect`，裁剪使用其 `TryIntersect`。引擎边界要求的 BCL 向量继续保留。
- 游戏仍以原生 Scene、GameObject、`NovelPlayerComponent : GameBehavior` 组织。组件随原生 Update 推进，随禁用、停用、移除、销毁和卸载退休其会话；离开有效场景的组件不能创建会话。
- 改绑游戏定义先停止旧会话，重复赋予相同资产对象保持当前会话；每个组件独立持有游戏状态。
- 仓库规则与架构文档已记录基础能力复用原则，没有新增场景系统、对象框架、事件总线、资产数据库或 undo 栈。

## 验证证据

| 检查 | 结果 | 证据 |
|---|---|---|
| 改动前完整行为基线 | 137 通过、0 失败，包含连续写作与时间线标记 | `artifacts/Reports/Validation-137-TimelineAndWriting.txt` |
| 当前正式脚本编译与程序集边界 | 通过；10 个程序集、0 架构错误 | `artifacts/Reports/CoreReuse-FormalCompile.txt` |
| 正式 Core/Scene API 导出 | PE 清单已生成；Rect 相交/命中等 API 可用 | `artifacts/Reports/CoreReuse-FormalApiSurface.txt` |
| 当前受影响行为定向验证 | 14 通过、0 失败 | `artifacts/Reports/CoreReuse-NativeScene-AndLayout-14.txt` |
| 源码包与全新工程编译 | 通过；10 个程序集、无其他插件依赖或覆盖 | `artifacts/Reports/Distribution-137-CoreReuse.txt` |

14 项包括原生 Scene 归属/激活/移除/销毁、UI 布局、重复列表、焦点/输入、画布映射、九宫格、组件模板、UI 转场快照、图像合成计划、滚动及长文排版。原生生命周期测试使用公开 SceneWorld/SceneManager，不反射调用 protected 方法，不播放音频；没有实际 Runtime 帧时钟/GPU/设备验收。

14 项与 137 项基线部分重叠，不能相加宣称 151 项完整回归。尚未对最新源码运行全部 138 项注册测试。

## 当前源码包

- 路径：`artifacts/Distribution/InnoEngine.VisualNovel.iplugin`
- 源码清单哈希：`C016BAE91AEFE4AFEF3F5AEFF782928C4E96DC5660F615F3408CAA3F0B13B6DC`
- 文件 SHA-256：`A60B4A4B7ABB784E08BBE640F0386BE7042CA155345042C891F94B0D22EBAE50`
- 大小：17,988,274 字节。
- 新消费工程：`artifacts/AcceptanceProjects/CleanConsumer-f8f63a5bac21458382afe05f156a3aa2`

该包包含 137 项基线的连续写作/标记及本次 Core/组件变更。选定引擎仍是 `artifacts/EngineBuildSnapshot-20261031-0611`，引擎源码未修改。

## 仍未完成

完整功能缺口继续以 [实施状态](../ImplementationStatus.md) 和 [功能矩阵](../Specifications/FeatureMatrix.md) 为准。页面动画/持久局部状态、完整 UI 扩展、更高级时间线、交互并行、语言图片/字体、资源审计与完整创作手势等仍需实施。

Runtime 原生根 codec、自动类型发现和完整 IME/手柄输入仍有正式 API 边界；冷资源自动就绪仍有明确宿主准备前提。干净工程编译不证明首次 GUI 创作、Windows 游戏成品、性能、长期运行或热重载矩阵通过。视频/流媒体按用户要求继续排除。
