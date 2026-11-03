# 扩展演出参数通道

`NovelPerformanceTargets` 让特殊玩法声明可由普通时间线驱动的标量通道。仍使用正常演出片段、目标槽、关键帧、剧情演出节点、舞台时钟与快照。

在场景的 `NovelSceneSystem` 创建会话前，通过 `ConfigurePerformanceTargets` 显式配置。目标以 `extension/` 开头，后面是自己的稳定 ID，例如 `extension/lamp`。每个目标声明允许的 `PerformanceProperty`，如 X、Y、Rotation 或 Opacity。字典和数组会被独立冻结，属性排列顺序不影响同一契约。与普通额外目标共同遵循舞台的 4096 目标预算。

在时间线中使用普通目标槽，例如 `lamp`，并在演出节点的目标 ID 中填写 `extension/lamp`。会话准备会检查目标声明、资源、槽位和支持的轨道。普通播放、暂停、指定时间、完成保持、取消、并行任务清理均使用同一执行链。

桥接的原生 GameBehavior 在所属场景更新中读取：

```csharp
bool hasOpacity = player.session.TryGetExtensionPerformance(
    "extension/lamp",
    PerformanceProperty.Opacity,
    out double opacity);
var frame = player.session.CaptureExtensionPerformance();
```

这是读取片段；`player` 是已配置的原生播放器组件。具体表现由自己的场景组件实现。可以用 `CaptureExtensionPerformance()` 一次取得所有扩展参数的独立 `PerformanceSample` 值，减少重复查询。采样不推进时间，不调用桥接代码。

返回 false 表示当前没有写入者，与有效的零值不同。桥接应在缺失时恢复作者指定的默认表现。完成的片段保留终值，取消后移除；不要把上一帧恰好显示的值当作恢复依据。

参数播放状态随原有 StageState 保存、恢复与回滚。读档后的回滚点保留存档时的时间，不会把片段重置到零。回想使用独立时间线并保留相同声明。契约 ID 或通道变化会改变构建指纹，旧普通存档会被明确拒绝。桥接代码及外部依赖仍须通过 `ConfigureHostCodeFingerprint` 提供明确贡献。

底层 Scene、GameObject 和组件生命周期继续由引擎维护。这个协议传递确定性参数；外部网络、文件、任意场景对象状态和第三方库不能因此被宣称自动可保存或倒放。需要外部副作用的玩法继续使用自定义命令的取消、保存与回滚屏障政策。具体骨骼、视频或其他 backend 不包含在此接口实现中。

`NovelPreviewSession` 也可显式传入相同声明，检查参数值而不调用实际场景表现。宿主尚无正式自动发现协议，默认编辑器不会猜测某个运行场景脚本的注册表。逻辑验证和实机表现分别报告。
