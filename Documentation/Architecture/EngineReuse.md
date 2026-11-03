# 引擎基础能力复用与原生 Scene 归属

生产依据是选定构建生成的正式 Runtime/Editor Scripting API，而不是 Core 源码中所有 public 类型。引擎源码仅供只读核对；没有通过生产 DLL 引用或反射扩大可用面。

## 当前复用边界

| 能力 | 当前做法 |
|---|---|
| 通用矩形 | UI 布局、命中、画布适配、行内图片、渲染四边形和九宫格直接使用 `InnoEngine.Mathematics.Rect`；祖先裁剪使用其 `TryIntersect`，不保留 `UiRect` 包装或兼容别名 |
| 向量和矩阵 | 使用引擎公开渲染、ImGui 等接口所接受的 `System.Numerics` 类型；Core 的数学类型同样已导出，但不为了替换名字增加边界转换 |
| 身份与资源 | 原生 `AssetObject`、稳定类型 ID、资产身份和引用解析；不建立另一套资产数据库或运行时全局单例 |
| 剧情图 | 原生 `GraphDocument`、节点/端口/边身份、通用图校验；VN 只增加叙事语义和领域参数 |
| 序列化 | 原生 `ISerializable`、属性/转换器、Importer 和 Artifact；运行时根 codec 尚缺正式入口，不建立替代格式 |
| 编辑事务 | 原生文档服务、History、SceneEdits 和编辑手势；不建立另一套撤销栈 |
| 更新与所有权 | 原生 `GameScene`、`GameObject`、`GameBehavior` 与 `Time.deltaTime`；VN 会话由组件拥有 |
| 事件 | 使用引擎公开事件/编辑器交互契约；没有 VN 全局事件总线 |
| 文字、音频和存储 | 通过已导出的 Text、Audio、Storage 接入；端口用于隔离实际宿主差异，不复制底层服务 |

Core 还暴露了 `Color`、向量、矩阵等基础类型。当前 UI/Shader 的颜色契约是 RRGGBBAA，Core `Color` 的打包约定不同；不能直接替换整数格式。VN 特有的 `StageTransform` 包含透明度与可恢复的二维演出属性，`NovelValue` 包含固定类型、枚举和列表限制，`StoryRandomState` 必须完整快照随机流；它们不是另一套通用 Transform、任意 CLR 值或不可恢复的 `System.Random` 包装。

`Tools/ArchitectureCheck` 的第三参数可指定正式 Runtime API DLL，以 PE 元数据输出实际 Core/Scene 类型、Rect 操作及既有能力缺口。工具读取宿主元数据不会成为生产二进制依赖。

## 原生 Scene → Object → Component

创建游戏时，向导由 Authoring 的原生场景服务调用 SceneEdits 创建 Scene、添加 NovelSceneSystem，并创建真实镜头与初始背景对象；属性通过原生 History 记录。

`NovelSceneSystem : GameSystem` 属于原生 Scene，统一拥有会话生命周期。新增原生图片、角色部位、粒子和屏幕效果组件的采集直接读取 GameObject/Transform；完整运行期剧情表现和 UI 对象化仍在迁移，见 NativeSceneAuthoring.md。

- 会话只能由已加载 Scene 内的活动组件创建；游离、未加载、禁用或销毁的组件不能复活游戏。
- 禁用组件、停用 GameObject、移除组件、销毁 GameObject 和卸载 Scene 均由引擎生命周期回调停止组件所拥有的会话。
- 重新启用会创建新的会话；希望继续既有进度时应使用明确快照/存档，而不是保留退休实例。
- 改绑游戏定义先停止旧会话；重复赋予同一资产对象不重启。
- 另一组件的会话不受影响。预览/回想也是明确的独立 owner，不使用静态 CurrentGame。

`Tests/Contracts/SceneOwnershipContracts.cs` 使用选定现有构建的公开 `SceneWorld`/`SceneManager` 驱动原生激活与销毁，测试不反射调用 protected 生命周期，也不播放音频。它覆盖归属与停止语义；图形输出、实际设备和完整热重载仍需另外验收。

本轮正式编译与十程序集检查通过；原生 Scene 归属及相关布局/裁剪/焦点/九宫格/截图/长文测试 **14 项通过、0 失败**。证据为 `artifacts/Reports/CoreReuse-FormalCompile.txt`、`CoreReuse-FormalApiSurface.txt` 和 `CoreReuse-NativeScene-AndLayout-14.txt`。完整行为基线仍是改动前的 137 项，不把这 14 项重复累计成另一份完整回归。
