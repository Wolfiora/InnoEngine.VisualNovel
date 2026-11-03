# 扩展协议

Narrative 的 NovelCommand、NovelCommandContext、NovelCommandState 和会话所有的 NovelCommandRegistry 承担类型化剧情扩展。声明目录保存稳定命令 ID、参数、结果与生命周期能力；运行实例只由当前会话拥有，不在全局缓存跨代工厂或委托。

参数在进入命令时解析并冻结，存档保存中立继续状态。实现需要明确恢复、取消与回滚政策；外部副作用必须声明，预览与并行遵循更严格的许可。普通玩法应使用正常 C# 类型与正式编译诊断，不提供任意 C# 文本执行节点。

UI 动作目录声明类型化参数，UiActionRegistry 绑定当前 owner 的行为。UiDataSource 提供显式字段、集合和图片，声明目录让编辑器按读写方向和类型提供绑定选择。重复行保持稳定键和自身数据上下文，不能用数组下标代替条目身份。

桥接插件通过命令、UI 数据源、动作和受控会话 API 接入。基础 VN 包不引用桥接依赖；小游戏需说明暂停、取消、保存和回滚，返回封闭类型结果后继续 VN 分支。角色表现、视频和其他 backend 需要另行约定，当前不宣称支持。

显式命令注册可用，自动扩展发现仍缺正式 Runtime/Editor TypeCache/TypeRegistry；不能遍历 DLL 或反射调用宿主内部类型代替。Compiler/Extensions 提供显式 StoryCompilerRegistry、StoryNodeCompiler 和 StoryNodeCompilationContext，已通过正式编译与最新完整回归。扩展只接收脱离源图的原生值，将一个源节点降为具有完全相同端口契约的内置指令，再走同一编译校验、并行分析、运行和恢复。禁止改节点 ID、递归自定义 opcode、隐式改连线及绕过内置验证。源图与 Missing 数据不会被降级结果覆盖。

该显式编译 API 不等于 Editor/Importer 的自动接入。正式导入上下文尚无导出的类型目录或 owner 服务注册协议，默认 StoryImporter 不会读取某个编辑器单例注册表。自定义节点的自动菜单、通用参数表单及与正式导入绑定，仍是没有交付的宿主接入部分；不能仅凭纯编译 API 测试标记完整工作流。

确定性演出参数通过 `NovelPerformanceTargets` 显式声明 `extension/` 目标和允许的现有标量通道。输入字典/数组在构造时冻结，声明参与精确存档指纹；通常时间线的目标槽、播放、取消、保存、回滚和回想保持同一执行链。Session 的读取 API 返回现有 PerformanceSample 值，不回调场景脚本或持有外部设备。Native NovelSceneSystem 提供会话创建前配置入口，桥接仍通过正常 GameObject/GameBehavior 生命周期读取。桥接代码及依赖由既有 hostCodeFingerprint 明确贡献。

这个通道不捕获任意外部场景对象、线程或第三方状态。桥接将参数投影到自己的表现，并在写入者消失时恢复作者默认值；外部副作用继续遵循 NovelCommand 的取消、保存与回滚屏障。骨骼、视频、自动类型发现和专用第三方编辑器没有因此实现。参见 [参数通道指南](../../Assets/Documentation/Scripting/PerformanceTargets.md)。`ExtensionPerformance-FinalOwnedFormalCompile.txt` 正式编译/十程序集和三项定向已通过，含直接目标绑定和原生组件生命周期；`Contracts-242-ExtensionPerformance.txt` 完整回归为 242 项通过、0 失败。

自动发现的具体边界见 [决策 0002](../Decisions/0002-FormalHostCapabilityGaps.md)。引擎内部实现、生产二进制引用和静态注册单例均不是本插件的接入方案。
