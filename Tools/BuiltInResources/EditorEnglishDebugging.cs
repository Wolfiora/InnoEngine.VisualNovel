using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishDebugging
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "断点 / Breakpoints", "Breakpoints"),
        new("literal", "为所选节点添加断点", "Add breakpoint at selected node"),
        new("literal", "章节 ID", "Chapter ID"),
        new("literal", "节点 ID", "Node ID"),
        new("literal", "使用条件", "Use condition"),
        new("literal", "删除断点##", "Remove breakpoint##"),
        new("literal", "条件必须是布尔表达式，不得使用随机运算。条件错误会停在原位置供修正，不会让剧情进入 Faulted。", "Conditions must be Boolean and cannot use randomness. A condition error pauses at the original location for correction without putting the story into Faulted."),
        new("literal", "应用当前断点到运行会话", "Apply current breakpoints to the running session"),
        new("literal", "继续执行", "Continue execution"),
        new("literal", "单步指令", "Step instruction"),
        new("literal", "暂停执行", "Pause execution"),
        new("literal", "定位断点/单步位置", "Locate breakpoint / step position"),
        new("literal", "覆盖节点 ", "Covered nodes: "),
        new("literal", " · 覆盖输出 ", " · Covered outputs: "),
        new("literal", "覆盖统计达到 100000 节点/输出上限，当前计数不代表完整覆盖。可按章节或路线分别调试。", "Coverage reached the 100,000-node/output limit; these counts do not represent complete coverage. Debug individual chapters or routes separately."),
        new("literal", "调用栈与任务", "Call stack and tasks"),
        new("literal", "定位##task/", "Locate##task/"),
        new("literal", "    返回到 ", "    Return to "),
        new("literal", "执行轨迹（最近 200 项）", "Execution trace (latest 200 entries)"),
        new("literal", "清空轨迹和覆盖", "Clear trace and coverage")
    };
}
