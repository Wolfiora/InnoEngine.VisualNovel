using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishParallelPerformance
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "类型 / Type", "Type"),
        new("literal", "值 / Value", "Value"),
        new("literal", "元素类型", "Element type"),
        new("format", "第 {0} 项##{1}/{2}", "Item {0}##{1}/{2}"),
        new("literal", "删除此项##", "Remove item##"),
        new("literal", "添加列表项##", "Add list item##"),
        new("literal", "所属起点：", "Owning fork: "),
        new("literal", "分支在此结束，所属主任务从此节点的 next 继续。汇合点不作为独立章节入口。", "Branches end here. Their parent task continues through this node's next connection. A join cannot be an independent chapter entry."),
        new("literal", "任一完成后取消其余任务", "Cancel remaining tasks when any task completes"),
        new("literal", "修改并行等待策略", "Change parallel wait policy"),
        new("literal", "汇合点：", "Join: "),
        new("literal", "分支按列表顺序执行，不创建线程。允许一个分支拥有对白/选项/输入焦点，其他分支继续演出；不同分支可写不同变量，同变量或输入焦点竞争会报错。解锁、页面切换和跨章节调用放在汇合后。取消不撤回已经提交的写入。", "Branches run in list order without spawning threads. One branch may own dialogue, choice or input focus while others continue presentation. Branches may write different variables; competing variable writes or input focus cause errors. Place unlocks, screen changes and cross-chapter calls after the join. Cancellation retains writes already committed."),
        new("literal", "添加并行分支##", "Add parallel branch##"),
        new("literal", "添加并行分支", "Add parallel branch"),
        new("literal", "分支：", "Branch: "),
        new("literal", "提前执行##", "Execute earlier##"),
        new("literal", "调整并行执行顺序", "Change parallel execution order"),
        new("literal", "延后执行##", "Execute later##"),
        new("literal", "删除此分支…##", "Remove this branch…##"),
        new("literal", "将删除此分支及 ", "This removes the branch and "),
        new("literal", " 个内部节点。存在外部引用时操作会拒绝，先重连引用。", " internal nodes. External references prevent removal; reconnect them first."),
        new("literal", "确认删除分支与内部节点##", "Remove branch and internal nodes##"),
        new("literal", "删除并行分支", "Remove parallel branch"),
        new("literal", "演出实例 ID", "Performance instance ID"),
        new("literal", "使用所选游戏的 UI / 效果目标", "Use UI / effect targets from selected game"),
        new("literal", "片段资源 ID", "Clip resource ID"),
        new("literal", "使用所选片段：", "Use selected clip: "),
        new("literal", "片段目标槽", "Clip target slot"),
        new("literal", "舞台目标 ID", "Stage target ID"),
        new("literal", "选择 UI 目标", "Choose UI target"),
        new("literal", "选择效果实例", "Choose effect instance"),
        new("literal", "移除此绑定##", "Remove this binding##"),
        new("literal", "添加目标绑定##", "Add target binding##"),
        new("literal", "舞台目标使用 camera、character/实例名 或 stage/图层 ID；Volume 绑定 audio/槽位。UI 目标从所选游戏选择 ui/页面/元素，支持 X、Y、ScaleX（统一缩放）、Opacity。效果参数绑定 effect/实例，按预设机制限定可用通道。UI 元素须存在于该角色的所有主题中。位置为偏移，缩放/透明度/音量/效果强度为乘数；其余效果参数使用轨道数值。", "Stage targets use camera, character/instance-name or stage/layer-ID; Volume binds audio/slot. Choose UI targets from the selected game as ui/screen/element, supporting X, Y, ScaleX (uniform scale) and Opacity. Effect parameters bind effect/instance, with channels limited by the preset mechanism. The UI element must exist in every theme for that screen role. Position is an offset. Scale, opacity, volume and effect intensity are multipliers; other effect parameters use the track value."),
        new("literal", "首次到达的标记 ID", "Marker ID to wait for on first arrival"),
        new("literal", "从所选片段选择标记", "Choose marker from selected clip"),
        new("literal", "等待该实例首次到达标记。已取消的实例立即结束等待；拖动/跳时只求值，不触发脚本或解锁。循环实例不会等待下一轮同名标记。", "Wait for this instance's first arrival at the marker. Cancelled instances end the wait immediately. Scrubbing and seeking only evaluate presentation, without running scripts or unlocking content. A looping instance does not wait for the same marker in a later cycle."),
        new("literal", "编辑演出片段请求", "Edit performance clip request")
    };
}
