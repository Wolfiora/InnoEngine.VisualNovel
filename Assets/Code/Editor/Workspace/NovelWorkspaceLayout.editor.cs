using System;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Inspection;

namespace Inno.VisualNovel.Editor.Workspace;

internal sealed class NovelWorkspaceLayout(
    EditorInteractions interactions,
    StoryInspectorFields fields
)
{
    internal enum Mode { Writing, Flow, Performance, Interface, Tools }
    private string m_task = "inno.vn.character-designer", m_error = string.Empty;
    internal Mode mode { get; private set; }
    internal bool separatePanels => mode is Mode.Performance or Mode.Interface;
    internal void ShowTools() { fields.Finish(); mode = Mode.Tools; }

    internal void Header()
    {
        Button(Mode.Writing, "Writing"); ImGui.SameLine(); Button(Mode.Flow, "Flow"); ImGui.SameLine();
        Button(Mode.Performance, "Presentation"); ImGui.SameLine(); Button(Mode.Interface, "Interface"); ImGui.SameLine(); Button(Mode.Tools, "All tools");
        if (ImGui.CollapsingHeader("Characters, audio, localization, collections and publishing"))
        {
            fields.Select("workspace/task", "Task", ref m_task,
                new[] { ("inno.vn.character-designer", "Character Designer"), ("inno.vn.audio", "Audio cue"), ("inno.vn.voices", "Voice Management"),
                    ("inno.vn.localization", "Localization"), ("inno.vn.collections", "Collections and Gallery"), ("inno.vn.publishing", "Publishing Checks") });
            if (ImGui.Button("Open task panel")) { fields.Finish(); Open(m_task); }
        }
        if (m_error.Length > 0) EditorUiSession.current.widgets.Hint(m_error);
    }
    internal void Dashboard()
    {
        if (mode == Mode.Performance)
        {
            EditorUiSession.current.widgets.WrappedText("Build the shot in Scene and Hierarchy. Add VN image, character or particle components to real objects, then edit their properties in Inspector. Use the timeline panel for reusable clips and bind them with a VN Performance component. Game shows the current composition without starting the story.");
            if (ImGui.Button("Scene")) Open("rendering.scene-view"); ImGui.SameLine();
            if (ImGui.Button("Performance Timeline")) Open("inno.vn.timeline"); ImGui.SameLine();
            if (ImGui.Button("Character Designer")) Open("inno.vn.character-designer");
        }
        else
        {
            EditorUiSession.current.widgets.WrappedText("Select the VN Scene System and create a UI canvas in Inspector. Add pages and controls as real child objects. Engine UI handles layout, text, focus and input; native Transform supplies author offsets. The canvas, page and layout Inspectors offer child creation buttons. Save reusable pages as native Prefabs.");
            if (ImGui.Button("Hierarchy")) Open("scene.hierarchy"); ImGui.SameLine();
            if (ImGui.Button("Inspector")) Open("scene.inspector"); ImGui.SameLine();
            if (ImGui.Button("Theme Designer")) Open("inno.vn.theme-designer");
        }
    }
    internal bool DrawSplit(
        Action navigation,
        Action content,
        Action properties,
        Action preview
    ) {
        Vector2 available = ImGui.GetContentRegionAvailable(); bool previewVisible = false;
        if (available.X < 900)
        {
            if (!ImGui.BeginTabBar("vn-compact-workspace")) return false;
            try
            {
                Tab(mode == Mode.Writing ? "Script" : "Graph", content);
                Tab("Story Navigation", navigation); Tab("Properties", properties);
                Tab("Preview", () => { previewVisible = true; preview(); });
            }
            finally { ImGui.EndTabBar(); }
            return previewVisible;
        }
        if (!ImGui.BeginTable("vn-task-workspace", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp, available)) return false;
        try
        {
            ImGui.TableSetupColumn("Story", ImGuiTableColumnFlags.WidthStretch, .8f);
            ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthStretch, mode == Mode.Flow ? 2.8f : 2.2f);
            ImGui.TableSetupColumn("Preview and properties", ImGuiTableColumnFlags.WidthStretch, 1.4f);
            ImGui.TableNextColumn(); Child("task-navigation", new(0, Math.Max(1, available.Y - 8)), navigation);
            ImGui.TableNextColumn(); Child("task-content", new(0, Math.Max(1, available.Y - 8)), content);
            ImGui.TableNextColumn();
            float height = Math.Max(1, available.Y - 8), previewHeight = Math.Min(280, height * .48f);
            previewVisible = Child("task-preview", new(0, previewHeight), preview);
            Child("task-properties", new(0, Math.Max(1, height - previewHeight - 8)), properties);
        }
        finally { ImGui.EndTable(); }
        return previewVisible;
    }
    private void Button(
        Mode target,
        string label
    ) {
        if (!ImGui.SmallButton((mode == target ? "● " : string.Empty) + label + "##workspace-" + target)) return;
        fields.Finish(); mode = target; m_error = string.Empty;
        if (target is Mode.Performance or Mode.Interface) { Open("rendering.scene-view"); Open("rendering.game-view"); Open("scene.hierarchy"); Open("scene.inspector"); }
        if (target == Mode.Performance) Open("inno.vn.timeline");
    }
    private void Open(string panel)
    {
        fields.Finish();
        if (!interactions.OpenPanel(panel)) m_error = "The editor has no active panel: " + panel + ". Check formal script compilation and plugin loading diagnostics.";
    }
    private void Tab(
        string name,
        Action draw
    ) {
        if (!ImGui.BeginTabItem(name)) return;
        try { Child("compact-" + name, Vector2.Zero, draw); }
        finally { ImGui.EndTabItem(); }
    }
    private static bool Child(
        string id,
        Vector2 size,
        Action draw
    ) {
        bool visible = ImGui.BeginChild(id, size, ImGuiChildFlags.Borders);
        try { if (visible) draw(); }
        finally { ImGui.EndChild(); }
        return visible;
    }
}
