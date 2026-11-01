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
        Button(Mode.Writing, "写作"); ImGui.SameLine(); Button(Mode.Flow, "流程"); ImGui.SameLine();
        Button(Mode.Performance, "演出"); ImGui.SameLine(); Button(Mode.Interface, "界面"); ImGui.SameLine(); Button(Mode.Tools, "全部工具");
        if (ImGui.CollapsingHeader(fields.Label("角色、声音、多语言、收藏与发布")))
        {
            fields.Select("workspace/task", fields.Localize("任务入口"), ref m_task,
                new[] { ("inno.vn.character-designer", fields.Localize("角色设计")), ("inno.vn.audio", fields.Localize("音频提示")), ("inno.vn.voices", fields.Localize("配音管理")),
                    ("inno.vn.localization", fields.Localize("多语言")), ("inno.vn.collections", fields.Localize("收藏与鉴赏")), ("inno.vn.publishing", fields.Localize("发布检查")) });
            if (ImGui.Button(fields.Label("打开任务面板"))) { fields.Finish(); Open(m_task); }
        }
        if (m_error.Length > 0) EditorUiSession.current.widgets.Hint(m_error);
    }
    internal void Dashboard()
    {
        if (mode == Mode.Performance)
        {
            EditorUiSession.current.widgets.WrappedText(fields.Localize("演出工作区使用原生舞台、角色和时间线面板。选择对应资源后打开设计器，编辑同一份原生文档；片段仍由正式舞台/UI/音频时钟预览。面板可以使用引擎停靠与布局保存。"));
            if (ImGui.Button(fields.Label("舞台设计"))) Open("inno.vn.stage-designer"); ImGui.SameLine();
            if (ImGui.Button(fields.Label("演出时间线"))) Open("inno.vn.timeline"); ImGui.SameLine();
            if (ImGui.Button(fields.Label("角色设计"))) Open("inno.vn.character-designer");
        }
        else
        {
            EditorUiSession.current.widgets.WrappedText(fields.Localize("界面工作区使用原生页面和主题设计面板。编辑页面树、画布、绑定、动作和动画；主题与页面继续共用引擎文档、选择和 History。面板位置由引擎停靠布局管理。"));
            if (ImGui.Button(fields.Label("页面设计"))) Open("inno.vn.screen-designer"); ImGui.SameLine();
            if (ImGui.Button(fields.Label("主题设计"))) Open("inno.vn.theme-designer");
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
                Tab(mode == Mode.Writing ? "剧本" : "节点图", content);
                Tab("故事导航", navigation); Tab("属性", properties);
                Tab("预览", () => { previewVisible = true; preview(); });
            }
            finally { ImGui.EndTabBar(); }
            return previewVisible;
        }
        if (!ImGui.BeginTable("vn-task-workspace", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp, available)) return false;
        try
        {
            ImGui.TableSetupColumn(fields.Localize("故事"), ImGuiTableColumnFlags.WidthStretch, .8f);
            ImGui.TableSetupColumn(fields.Localize("内容"), ImGuiTableColumnFlags.WidthStretch, mode == Mode.Flow ? 2.8f : 2.2f);
            ImGui.TableSetupColumn(fields.Localize("预览与属性"), ImGuiTableColumnFlags.WidthStretch, 1.4f);
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
        if (!ImGui.SmallButton(fields.Caption((mode == target ? "● " : string.Empty) + label + "##workspace-" + target,
            (mode == target ? "● " : string.Empty) + fields.Localize(label) + "##workspace-" + target))) return;
        fields.Finish(); mode = target; m_error = string.Empty;
        if (target == Mode.Performance) { Open("inno.vn.stage-designer"); Open("inno.vn.timeline"); }
        else if (target == Mode.Interface) { Open("inno.vn.screen-designer"); Open("inno.vn.theme-designer"); }
    }
    private void Open(string panel)
    {
        fields.Finish();
        if (!interactions.OpenPanel(panel)) m_error = fields.Localize("当前编辑器没有激活面板：") + panel + fields.Localize("。请检查正式脚本编译与插件加载诊断。");
    }
    private void Tab(
        string name,
        Action draw
    ) {
        if (!ImGui.BeginTabItem(fields.Label(name))) return;
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
