# 当前架构

当前十个计划程序集均有实际职责；程序集存在不表示所属功能组已经完整交付。详细证据以实施状态与功能矩阵为准。

| 层 | 当前职责 |
|---|---|
| Model | 原生图、节点参数、表达式、变量、编译指令、舞台、角色、音频、UI、时间线、粒子、本地化与收藏声明 |
| Compiler | 图语义校验、表达式类型检查、资源无关的指令降级、内容指纹 |
| Narrative | 隔离解释器、调用局部变量、ReadingController/页内冻结替换值、命令继续状态、有界并行、选项标记、随机与快照 |
| Stage | 分层角色与舞台、镜头、可定位的关键帧、固定种子粒子、转场/屏幕效果与中立恢复 |
| UI | 自有页面树、富文本塑形与稳定布局、测量/排列、重复列表、数值几何绑定、焦点、单次激活、滚动跟随、图像适配、九宫格和生成位图 |
| Player | 隔离会话、游戏壳、音频政策、存档候选、回滚、语言、收藏及隔离回想、玩家数据启动/自动保存和同一边界缩略图协调 |
| Rendering | 引擎字形栅格化、描边/阴影缓存、舞台/UI 四边形、裁切、预乘图像合成、效果/转场、独立传输回读与原生提交 |
| Integration | Scene 组件、RenderModel/Pipeline、输入/音频/存储适配与会话所有权 |
| Authoring | 原生草稿、拓扑编辑、剧本投影、格式编辑、导入、模板、翻译交换、收藏检查、生产链预览/路线录制及引擎 History |
| Editor | 图/剧本、属性、各资源设计器、诊断及共用生产链的预览 |

生产代码仅通过逻辑 namespace 引用公开 Scripting API。工具和契约测试通过明确选定的引擎公开宿主接口组合实际依赖。

基础能力复用、Core 数学类型的取舍和 Scene → Object → Component 所有权详见 [引擎复用边界](EngineReuse.md)。游戏入口是原生 `NovelSceneSystem : GameSystem`，不是插件自己的场景或对象框架。

Editor 消费 Authoring 预览对象暴露的 Player 会话与 UI 类型，因此 `.iasmdef` 显式列出 Player 等公开类型所属程序集。正式 C# 编译不能依靠传递程序集引用解析公共签名；这没有增加程序集，也没有引入反向依赖或生产二进制旁路。所有定义仍由 Bootstrap 使用引擎原生源服务生成。

## 工程定义与 IDE 生成文件

生产结构的权威是 `Assets/Code` 中的源码与 `.iasmdef`。当前十个实际程序集会由引擎 IDE 生成器生成十个 `Inno.VisualNovel.*.csproj`，与默认的 `Inno.GameScripts.csproj`、`Inno.EditorScripts.csproj` 一起写入 `InnoProject.sln`。这十二个根目录项目是正式分层的 IDE 投影；两个默认程序集目前没有生产源文件。不能为减少项目数量而把十个边界合并进默认项目。

这些根目录工程由引擎 `ScriptProjectGenerator.Generate` 按脚本程序集逐一生成，不是手工维护的生产构建入口。它们及生成的 solution 已被 `.gitignore` 排除；不应手工增改引用或将源码挪进默认程序集来消除文件数量。单独删除会使当前 solution 引用失效，并会在下一次生成 IDE 工程时重新出现。

另外五个手工维护的开发工程分别是：

| 文件 | 用途 |
|---|---|
| `Tools/Bootstrap/Bootstrap.csproj` | 用公开宿主 API 生成原生程序集定义和初始工程设置 |
| `Tools/ArchitectureCheck/ArchitectureCheck.csproj` | 检查正式脚本程序集的 PE 引用和源码边界 |
| `Tools/DistributionCheck/DistributionCheck.csproj` | 用正式插件扫描服务验证导出包清单 |
| `Tools/BuiltInResources/BuiltInResources.csproj` | 生成原创音频、粒子精灵和原生资源声明 |
| `Tests/Contracts/Contracts.csproj` | 对正式脚本 DLL 执行行为和公开宿主集成验证 |

它们位于 `Assets` 之外，不进入插件源码包或 Player 生产闭包，也不加入引擎生成的 `InnoProject.sln`。这些开发工程中的宿主程序集引用不能复制到 `Assets/Code` 或根目录生成工程中。

图是唯一控制流事实。剧本投影不改变原始节点或连线，循环不递归展开。所有编辑先作用于 detached candidate，成功编码后发布为中立草稿并记录到引擎 History。没有第二套 Undo 栈。

StoryImporter 从 `.ivnstory` 读取原生创作数据，编译后发布去掉图布局的运行资产。运行解释器只消费 StoryProgram；编辑器读取创作源并保留未成功编译的草稿。

当前 History 以完整章节草稿 before/after 保存结构编辑，保证原子性和清楚的所有权，但尚未满足大章节的最小差异和性能目标。它是需要进一步细化的已知实现限制，不能据此宣称规模验收完成。

## 生命周期

- 文档上下文由引擎 document service 拥有；本插件只保存以稳定 document ID 为键的中立数据。
- 生成代码、转换器与编辑器实例属于当前脚本代际。
- 重载通过 IEditorPanelReloadState 传递原生字节，不能把 dirty 文档写进 editor.ini。
- 解释器复制运行程序，外部修改源资产不会改变正在运行的会话。
- 普通快照包含局部和游戏变量；全局变量独立于回滚。
- 正式游戏存档当前被 Runtime 根序列化入口缺失阻塞，未使用替代 JSON 或二进制协议绕过。
