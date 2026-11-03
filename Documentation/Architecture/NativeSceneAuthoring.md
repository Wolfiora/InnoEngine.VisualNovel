# 原生场景制作重构

本次重构采用用户确认的新制作模型：Scene 上添加 VN System，背景、人物和 UI 使用真实 GameObject/Component，Scene View 编辑画面，Game View 显示结果，剧情图保留独立面板。此文描述正在实施的结构，不表示 R0～R7 已全部完成。

## 已建立的所有权

- `NovelSceneSystem : GameSystem` 是原生场景所属的会话入口。旧 `NovelPlayerComponent` 类型已移除，没有兼容别名。
- `NovelImageComponent` 的资源、尺寸、枢轴、采样区、颜色、混合与遮罩是图片的声明；位置、旋转、缩放及父子关系直接来自原生 Transform。
- `NovelCameraComponent` 的原生 Transform 决定游戏镜头；Scene View 导航不修改该镜头。
- `NovelTargetComponent` 只声明剧情绑定键，不取代引擎对象身份。重复键报错，不按对象名称补找目标。
- `NovelSceneCapture` 每次读取当前原生层级，输出临时 StageFrame；不存在需要同步删除的图片镜像树。父对象停用、删除组件、重排层级、修改 Transform 均由下一次采集反映。
- `NovelSceneProjection` 统一画面和命中坐标：原生 XY 平面、Y 向上，输出左上角原点、Y 向下。默认一个原生单位对应一个设计像素，设计画布中心位于世界原点。
- Game 输出遵循宿主 content scope 的活动 Scene；没有活动 Scene 时仅允许唯一启用的 VN 输出。不会查找全局 Loaded Scene 或在争用时任选一个。

## 引擎 UI 接入更新

2026-11-03 已按用户要求将新原生 UI 改为复用引擎 UI 服务。真实组件生成派生文档，制作态 Scene/Game 消费同一引擎网格，选择由真实 UI 事件定位；删除了临时添加的另一套原生 UI 输入与布局路径。默认模板字体通过官方导入器管理。完整 Play 页面尚待迁移，详见 [引擎 UI 接入](EngineUiIntegration.md)。

## 制作态与生命周期

制作态的 `CaptureScene` 只采集对象，不启动会话、不推进时间、不创建对象、不播放音频。原生 System 进入运行生命周期后才能取得会话；仅把 Scene 加载到编辑世界不应创建会话。

Scene contributor 使用公开的导航、选择和 manipulation space，让宿主执行原生 Transform 手势与 History。编辑镜头与游戏镜头使用相同的图元提取规则。旋转、负缩放及父级组合保留完整矩阵；选择根据投影四边形，不以轴对齐包围盒代替实际几何。

原生角色部位、粒子、屏幕效果、音频和基础时间线已接入此模型。音频组件复用现有音频策略及引擎 Audio，拥有本对象的淡变、暂停、位置与恢复候选；屏幕效果实例参数与源预设隔离。完整 UI 页面、剧情音频目标、转场和剧情运行表现仍需迁移；运行时的 StageController / NovelGameView 路径尚未全部移除。这是实际实施剩余，不能归为引擎 API 阻塞。

## 英文编辑器

插件编辑器直接使用英文文案，已移除编辑器语言设置、模块、翻译表和查询包装。游戏本地化保留；中文教学剧情、游戏 UI 文本和用户资产名称不自动改成英文。编译器继续返回原生 GraphDiagnostic，不再维护供编辑器翻译使用的平行格式模板表。

## 已验证的对象行为

- 原生图片的创建、父子 Transform、裁切、镜像、退化几何、启停、删除和精确四边形命中。
- 原生 SceneEdits 的逐步 Undo/Redo、实际 Prefab 文件保存与实例化，以及 Scene 保存重开后的属性和组件引用。
- 角色外观操作先验证全部部位，然后修改真实图片与 Transform；序列帧、眨眼、说话与呼吸复用 Stage 求值算法。捕获不修改对象，零缩放父级不依赖可逆矩阵。
- 粒子发射器和屏幕效果拥有组件声明及逻辑时钟；停用父级或删除组件后，输出随下一次采集消失。
- 基础时间线以显式 GameObject 引用绑定槽，支持本地 XY、缩放、平面旋转、图片透明度及效果强度。Seek 先验证全部轨道再提交；Play 替换冲突写入者。直接配置出多个写入者会在更新前拒绝。停止后不覆盖其他脚本的修改。
- 时间线 Inspector 使用 Authoring 服务与原生属性事务应用时间点；普通 Edit 画面不推进剧情、时间、音频或外部动作。

时间线坐标是原生局部坐标；旧舞台预设的坐标含义不能直接视为已经迁移。震动叠加、音量及其他效果参数轨道的原生适配还未完成，当前明确报告不支持，未添加假行为。

## 选定 SDK 的具体限制

选定构建为 `artifacts/EngineBuildSnapshot-20261031-0611`。正式表面证据见 `artifacts/Reports/SceneRefactor-FormalApi.txt` 和 `SceneRefactor-RuntimeApi.txt`；不能用当前旁侧源码中的新类型冒充已可使用的接口。

1. **隔离预演入口未导出。** Editor 表面包含 `IEditorSceneWorkspace`、`SceneEdits`、`EditorViewportContributor` 和变换 manipulation space；Runtime 表面包含 `GameSystem`、`GameScene.InstantiatePrefab` 与 `PrefabAsset`。`IEditorScenePlayMode`、`RuntimeSession`、`SceneWorld` 不在两份脚本表面中。不能用直接引用宿主 DLL 或在 Edit 场景里推进来替代隔离预演。
2. **结构创建事务的 Redo 预检查失败。** 原生事务先同时查询全部子操作能否 Redo。包含创建对象、添加组件、再修改属性的事务，会在对象重建前被属性/组件检查拒绝。实际失败保留在 `SceneRefactor-NativeHistoryBlocker.txt`。目前使用独立原生操作，逐步 Undo/Redo 已通过；已有对象的外观与时间点属性操作仍可组成事务。没有插件自建撤销栈。
3. **Scene 资产重新打开会分配实例身份。** 所选 SDK 的原生资产实例化为 Scene/Object/Component 分配新实例 ID，并正确重映射内部组件引用。测试使用实际文件保存和重开验证了这一点。故事槽使用稳定逻辑标识或原生组件引用；不能将跨重开相同实例 GUID 作为现有引擎保证。

自定义 UI 矩形手柄、设备输入和视口实际拖动仍待专项核对，不将这些尚未验证项武断列为 API 缺失。

## 验证范围

`SceneRefactor-PerformanceFormal.txt`：十程序集正式脚本编译、原生候选激活及重编译通过。

`SceneRefactor-FinalContracts.txt`：251 项行为检查通过、0 失败，包含 8 项原生场景/角色/效果/时间线/History 路径检查，也包含仍需迁移的旧运行逻辑。这个总数不代表 R0～R7 或完整 v1 完成。

`SceneRefactor-FormalApi.txt`：10 个程序集依赖检查、0 错误，同时记录选定 SDK 的公开边界。没有生产实现程序集引用，也没有添加程序集。

新增默认材质由 Bootstrap 使用官方材质保存机制生成，System 的 Reset 绑定资源，使序列化记录真实构建依赖。`SceneRefactor-DefaultsFormal.txt` 再次正式编译及候选激活通过；`SceneRefactor-DefaultsContracts.txt` 的 9 项原生场景检查通过，包含全新无剧情 Scene 添加 System/图片后生成首帧、连续捕获不改变场景，以及材质进入原生序列化依赖。`SceneRefactor-PerformanceHistory.txt` 进一步验证真实时间线属性事务 Undo/Redo、文件保存重开及对象绑定重映射。这些后续证据不伪装成已重跑完整 252 项套件。

## 仍需完成

2026-11-03 增量：System 的 `targets` 保存真实 Target 组件引用，`ResolveTarget<T>` 检查场景、类型、重复逻辑 ID 和已删除引用。绑定动作复用原生 History；保存重开正确重映射组件引用。没有按名称修补引用，也没有隐式扫描别的 Scene。此入口用于受控桥接；下方第一项的完整剧情表现迁移仍未完成。

原生 UI 已有运行输入闭环：无剧情场景在 System 更新时准备画面；RenderModel 只排队输入并返回准备好的帧。引擎 UI 负责命中、事件及文档焦点，VN 只选择本场景中的输入 Canvas。Page Navigation 直接修改显式目标页面的原生激活状态，所有对象继续由 Hierarchy 管理。完整故事壳目前仍有旧页面路径，原生按钮可调用同一个已实现的壳动作服务，不新增另一套剧情执行器。

1. 将剧情表现指令接入原生目标；整合剧情音频槽、镜头策略、原生转场和属性控制权。原生音频组件与效果参数已通过定向检查，不等于会话层已全部迁移。
2. 完整 UI 控件组件化与原生 Prefab 页面；删除旧 `.ivnui` 实例树和重复设计器。
3. 快照、回滚、动态实例与扩展对象的原生恢复；失败候选不得影响当前对象。
4. 原生隔离预演需要上述 SDK 入口；其他可独立实现的功能仍继续推进。
5. 三主题、全部页面、三模板和五组 demo 重建，更新教程并重新分发。当前旧 demo 不是新 System 架构的可交付包。
6. Scene View 辅助线、UI 矩形工具和实际交互验收。

不修改引擎、不访问内部类型、不依赖 Rendering2D/Canvas，也不增加替代 SceneWorld、序列化、资产数据库或 History。未进行本次重构的 GUI、GPU 或实际输入验收；需要用户手动测试时再提供具体步骤，不使用 Computer Use。
