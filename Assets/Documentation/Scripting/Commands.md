# 自定义 C# 剧情指令

普通创作者使用已安装扩展提供的原生 `.ivncommands` 表单目录，不在节点里写 C#。高级用户实现 `NovelCommand`，使用稳定 ID 声明参数、默认值、枚举成员、结果类型及生命周期能力。

## 注册与创作

1. 在当前代际的 `NovelCommandRegistry` 注册独立 handler 工厂；构造与 definition 读取不能产生副作用。
2. 在首次创建会话前调用 `NovelPlayerComponent.ConfigureCommands`，或将注册表传入 `NovelSession`。
3. 扩展作者在 Editor scope 通过 `NovelCommandPublishing.Publish` 将元数据发布为项目内原生目录；工厂、CLR Type 和委托不进入目录。
4. 创作者把目录关联到游戏，在图或剧本中添加自定义指令并应用表单。参数复用类型化表达式，不提供另一套脚本语言。
5. 有返回值时选择兼容的目标变量；允许取消的指令必须连接 cancel 输出。

目录、节点与注册实现使用精确合同摘要比较，默认值、参数和能力变化都会被发现。缺失实现保留原始表单和参数，并在运行位置报告 Missing；不会猜测相近类型。

## 执行与恢复

`Execute` 在会话 owner 线程执行，首次调用的 seconds 为零。`NovelCommandState.elapsed` 由调度器累计，values 存放受限的 Boolean/Integer/Number/String/Enum/一维列表。参数在进入节点时只求值一次，恢复时继续使用保存的参数，不因全局变量改变而重新求值。

context 可以读取参数和剧情变量，并返回一个声明类型的结果。结果只在命令成功完成后写入节点指定变量。回调不能在 Execute 结束后或其他线程继续使用 context；异步工作应由 handler 自己持有取消机制，把结果排队并在下一次 Execute 消费。

`PrepareRestore` 校验候选中立状态并准备私有资源，不修改 VN 或外部状态；失败保留现有会话。Cancel 与 Dispose 释放扩展自身资源，不回写 VN。正常完成也会 Dispose。

## 能力与边界

- 没有 Save 能力的活动命令禁用存档，不能把 Task 或外部对象塞进快照。
- 没有 Rollback 的命令建立回滚屏障并停止快进。ExternalEffects 不能同时声明 Rollback。
- 预览模式既要求 Preview，也拒绝 ExternalEffects；语言切换不重新创建活动命令。
- 并行演出只接受可保存、可回滚、无外部副作用且不写剧情结果变量的扩展。
- 自动类型发现、专用自定义节点编译器及完整 UI 数据源/动作扩展尚未接通。当前接口用显式注册和结构化返回值接入玩法；由后续剧情节点调度 VN 演出。

完整可导入实现见 `~Samples/ScriptExtension`。本批次未进行实机或 GUI 验收。
