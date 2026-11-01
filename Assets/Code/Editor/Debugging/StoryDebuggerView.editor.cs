using System;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Debugging;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Editor.Debugging;

internal sealed class StoryDebuggerView(StoryInspectorFields fields)
{
    private readonly ExpressionEditorView m_expressions = new(fields);
    internal bool Settings(
        NovelPreviewCaseAsset test,
        string chapter,
        string selected,
        VariableDefinition[] variables
    ) {
        if (!ImGui.CollapsingHeader(fields.Label("断点 / Breakpoints"))) return false;
        bool changed = false;
        if (selected.Length > 0 && !test.breakpoints.Any(item => item.chapter == chapter && item.node == selected)
            && ImGui.Button(fields.Label("为所选节点添加断点")))
        { fields.Finish(); test.breakpoints = [.. test.breakpoints, new() { chapter = chapter, node = selected }]; changed = true; }
        int remove = -1;
        for (int index = 0; index < test.breakpoints.Length; index++)
        {
            PreviewBreakpoint item = test.breakpoints[index]; string key = "debug/breakpoint/" + index;
            bool enabled = item.enabled, conditional = item.conditional;
            changed |= fields.Toggle(key + "/enabled", item.chapter + "/" + item.node, ref enabled); item.enabled = enabled;
            string sourceChapter = item.chapter, sourceNode = item.node;
            changed |= fields.Text(key + "/chapter", fields.Localize("章节 ID"), ref sourceChapter); item.chapter = sourceChapter;
            changed |= fields.Text(key + "/node", fields.Localize("节点 ID"), ref sourceNode); item.node = sourceNode;
            changed |= fields.Toggle(key + "/conditional", "使用条件", ref conditional); item.conditional = conditional;
            if (conditional) changed |= m_expressions.Draw(key + "/condition", item.condition, variables);
            if (ImGui.SmallButton(fields.Caption("删除断点##" + key, fields.Localize("删除断点##") + key))) remove = index;
        }
        if (remove >= 0) { fields.Finish(); test.breakpoints = test.breakpoints.Where((_, index) => index != remove).ToArray(); changed = true; }
        EditorUiSession.current.widgets.Hint(fields.Localize("条件必须是布尔表达式，不得使用随机运算。条件错误会停在原位置供修正，不会让剧情进入 Faulted。"));
        return changed;
    }
    internal void Draw(
        StoryDebugger debugger,
        NovelPreviewSession preview,
        NovelPreviewCaseAsset test,
        Action<StoryDebugLocation> locate
    ) {
        if (ImGui.Button(fields.Label("应用当前断点到运行会话"))) debugger.SetBreakpoints(test.breakpoints.Where(static item => item.enabled).Select(static item => item.Build()));
        ImGui.SameLine();
        if (debugger.paused)
        {
            if (ImGui.Button(fields.Label("继续执行"))) { debugger.Continue(); preview.session.Tick(0); }
            ImGui.SameLine();
            if (ImGui.Button(fields.Label("单步指令"))) { debugger.Continue(true); preview.session.Tick(0); }
            EditorUiSession.current.widgets.Hint(debugger.reason + " · " + debugger.location?.task + " · " + debugger.location?.chapter + "/" + debugger.location?.node);
        }
        else if (preview.session.story is { status: not StoryStatus.Stopped } story && ImGui.Button(fields.Label("暂停执行"))) debugger.Pause(story);
        if (debugger.location is { } location && ImGui.Button(fields.Label("定位断点/单步位置"))) locate(location);
        ImGui.Text(fields.Localize("覆盖节点 ") + debugger.coveredNodes + fields.Localize(" · 覆盖输出 ") + debugger.coveredOutputs);
        if (debugger.coverageTruncated) EditorUiSession.current.widgets.Hint(fields.Localize("覆盖统计达到 100000 节点/输出上限，当前计数不代表完整覆盖。可按章节或路线分别调试。"));
        if (ImGui.CollapsingHeader(fields.Label("调用栈与任务")))
        {
            foreach (StoryMachine task in preview.session.story!.InspectTasks())
            {
                var source = task.debugLocation;
                ImGui.Text(source.task + " · " + task.status + " · " + source.chapter + "/" + source.node);
                if (ImGui.SmallButton(fields.Caption("定位##task/" + source.task, fields.Localize("定位##task/") + source.task))) locate(source);
                foreach (StoryCallFrame call in task.InspectCalls())
                {
                    ImGui.Text(fields.Localize("    返回到 ") + call.returnNode);
                    foreach (var value in call.locals) ImGui.Text("        " + value.id + " = " + value.value);
                }
            }
        }
        if (ImGui.CollapsingHeader(fields.Label("执行轨迹（最近 200 项）")))
        {
            if (ImGui.SmallButton(fields.Label("清空轨迹和覆盖"))) debugger.ClearTrace();
            foreach (StoryTraceEntry entry in debugger.trace.TakeLast(200))
                if (ImGui.Selectable(entry.sequence + " · " + entry.location.task + " · " + entry.location.chapter + "/" + entry.location.node
                    + (entry.output.Length > 0 ? " → " + entry.output + " → " + entry.target : " · " + entry.location.definition)))
                { locate(entry.location); }
        }
    }
}
