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
        if (!ImGui.CollapsingHeader("Breakpoints")) return false;
        bool changed = false;
        if (selected.Length > 0 && !test.breakpoints.Any(item => item.chapter == chapter && item.node == selected)
            && ImGui.Button("Add breakpoint at selected node"))
        { fields.Finish(); test.breakpoints = [.. test.breakpoints, new() { chapter = chapter, node = selected }]; changed = true; }
        int remove = -1;
        for (int index = 0; index < test.breakpoints.Length; index++)
        {
            PreviewBreakpoint item = test.breakpoints[index]; string key = "debug/breakpoint/" + index;
            bool enabled = item.enabled, conditional = item.conditional;
            changed |= fields.Toggle(key + "/enabled", item.chapter + "/" + item.node, ref enabled); item.enabled = enabled;
            string sourceChapter = item.chapter, sourceNode = item.node;
            changed |= fields.Text(key + "/chapter", "Chapter ID", ref sourceChapter); item.chapter = sourceChapter;
            changed |= fields.Text(key + "/node", "Node ID", ref sourceNode); item.node = sourceNode;
            changed |= fields.Toggle(key + "/conditional", "Use condition", ref conditional); item.conditional = conditional;
            if (conditional) changed |= m_expressions.Draw(key + "/condition", item.condition, variables);
            if (ImGui.SmallButton("Remove breakpoint##" + key)) remove = index;
        }
        if (remove >= 0) { fields.Finish(); test.breakpoints = test.breakpoints.Where((_, index) => index != remove).ToArray(); changed = true; }
        EditorUiSession.current.widgets.Hint("Conditions must be Boolean and cannot use randomness. A condition error pauses at the original location for correction without putting the story into Faulted.");
        return changed;
    }
    internal void Draw(
        StoryDebugger debugger,
        NovelPreviewSession preview,
        NovelPreviewCaseAsset test,
        Action<StoryDebugLocation> locate
    ) {
        if (ImGui.Button("Apply current breakpoints to the running session")) debugger.SetBreakpoints(test.breakpoints.Where(static item => item.enabled).Select(static item => item.Build()));
        ImGui.SameLine();
        if (debugger.paused)
        {
            if (ImGui.Button("Continue execution")) { debugger.Continue(); preview.session.Tick(0); }
            ImGui.SameLine();
            if (ImGui.Button("Step instruction")) { debugger.Continue(true); preview.session.Tick(0); }
            EditorUiSession.current.widgets.Hint(debugger.reason + " · " + debugger.location?.task + " · " + debugger.location?.chapter + "/" + debugger.location?.node);
        }
        else if (preview.session.story is { status: not StoryStatus.Stopped } story && ImGui.Button("Pause execution")) debugger.Pause(story);
        if (debugger.location is { } location && ImGui.Button("Locate breakpoint / step position")) locate(location);
        ImGui.Text("Covered nodes: " + debugger.coveredNodes + " · Covered outputs: " + debugger.coveredOutputs);
        if (debugger.coverageTruncated) EditorUiSession.current.widgets.Hint("Coverage reached the 100,000-node/output limit; these counts do not represent complete coverage. Debug individual chapters or routes separately.");
        if (ImGui.CollapsingHeader("Call stack and tasks"))
        {
            foreach (StoryMachine task in preview.session.story!.InspectTasks())
            {
                var source = task.debugLocation;
                ImGui.Text(source.task + " · " + task.status + " · " + source.chapter + "/" + source.node);
                if (ImGui.SmallButton("Locate##task/" + source.task)) locate(source);
                foreach (StoryCallFrame call in task.InspectCalls())
                {
                    ImGui.Text("    Return to " + call.returnNode);
                    foreach (var value in call.locals) ImGui.Text("        " + value.id + " = " + value.value);
                }
            }
        }
        if (ImGui.CollapsingHeader("Execution trace (latest 200 entries)"))
        {
            if (ImGui.SmallButton("Clear trace and coverage")) debugger.ClearTrace();
            foreach (StoryTraceEntry entry in debugger.trace.TakeLast(200))
                if (ImGui.Selectable(entry.sequence + " · " + entry.location.task + " · " + entry.location.chapter + "/" + entry.location.node
                    + (entry.output.Length > 0 ? " → " + entry.output + " → " + entry.target : " · " + entry.location.definition)))
                { locate(entry.location); }
        }
    }
}
