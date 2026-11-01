using System;
using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class StoryInstructionMenuView(
    StoryDocumentsModule documents,
    StoryInspectorFields fields
)
{
    private Guid m_document;
    private long m_revision;
    private GraphEdgeId? m_edge;
    private GraphPosition m_position;
    private string m_fragment = string.Empty, m_search = string.Empty, m_message = string.Empty;
    private bool m_focus, m_insert = true;
    private int m_highlight;
    internal bool Draw(
        StoryDraftState draft,
        string selected,
        Action<string> select
    ) {
        bool keyboard = ImGui.IsWindowFocused() && !ImGui.GetInput().wantsText && !ImGui.IsAnyItemActive()
            && !ImGui.GetInput().shift && !Control() && !Alt() && !ImGui.IsMouseButtonDown(ImGuiMouseButton.Left) && !ImGui.IsMouseButtonDown(ImGuiMouseButton.Middle);
        if (ImGui.SmallButton(fields.Label("添加指令…（Tab）")) || keyboard && ImGui.IsKeyPressed(ImGuiKey.Tab, false))
        {
            fields.Finish(); m_document = documents.activeDocumentId; m_revision = documents.GetRevision(m_document);
            m_edge = documents.quickInsert.NextEdge(draft, selected);
            var parent = draft.graph.document.FindNode(new(selected));
            m_position = parent is null ? new(0, 0) : new(parent.position.x, parent.position.y + 150);
            m_fragment = draft.fragments.FirstOrDefault(fragment => fragment.nodes.Contains(selected, StringComparer.Ordinal))?.id
                ?? draft.fragments.FirstOrDefault()?.id ?? string.Empty;
            m_search = m_message = string.Empty; m_highlight = 0; m_focus = true;
            ImGui.OpenPopup("vn-instruction-menu");
        }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (!EditorUiSession.current.widgets.BeginMenuPopup("vn-instruction-menu")) return false;
        try
        {
            if (m_document != documents.activeDocumentId || documents.GetRevision(m_document) != m_revision)
            { ImGui.CloseCurrentPopup(); m_message = fields.Localize("文档已变化，请重新打开指令菜单。"); return false; }
            if (m_fragment.Length == 0) { EditorUiSession.current.widgets.Hint(fields.Localize("先创建章节片段，再添加指令。")); return false; }
            if (m_focus) { ImGui.SetKeyboardFocusHere(); m_focus = false; }
            if (ImGui.InputText(fields.Label("搜索名称、分类或指令 ID"), ref m_search, 256)) m_highlight = 0;
            if (m_edge is not null) ImGui.Checkbox(fields.Label("插入到当前 next 连线"), ref m_insert);
            EditorUiSession.current.widgets.Hint(m_edge is null || !m_insert ? fields.Localize("创建独立指令，之后通过目标选择器或图连线连接。")
                : fields.Localize("线性指令插入原路径；选项创建两条对白分支并汇合；并行创建完整组。其他结构独立创建，保留当前路径。"));
            var choices = new StoryNodeCatalog(documents.codec).definitions.Where(static item => item.id is not (StoryNodeIds.entry or StoryNodeIds.join))
                .Where(item => fields.Localize(item.displayName).Contains(m_search, StringComparison.OrdinalIgnoreCase) || fields.Localize(item.category).Contains(m_search, StringComparison.OrdinalIgnoreCase)
                    || item.displayName.Contains(m_search, StringComparison.OrdinalIgnoreCase) || item.category.Contains(m_search, StringComparison.OrdinalIgnoreCase)
                    || item.id.Contains(m_search, StringComparison.OrdinalIgnoreCase)).GroupBy(static item => item.category).SelectMany(static group => group).ToArray();
            m_highlight = Math.Clamp(m_highlight, 0, Math.Max(0, choices.Length - 1));
            if (choices.Length > 0)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) m_highlight = (m_highlight + 1) % choices.Length;
                if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) m_highlight = (m_highlight + choices.Length - 1) % choices.Length;
            }
            string chosen = string.Empty, category = string.Empty;
            for (int index = 0; index < choices.Length; index++)
            {
                var item = choices[index];
                if (category != item.category) { category = item.category; ImGui.SeparatorText(fields.Localize(category)); }
                if (ImGui.Selectable(fields.Caption(item.displayName + "##quick/" + item.id, fields.Localize(item.displayName) + "##quick/" + item.id), index == m_highlight)) chosen = item.id;
            }
            if (choices.Length == 0) EditorUiSession.current.widgets.Hint(fields.Localize("没有匹配指令，请尝试名称、阅读/流程/演出等分类或英文指令 ID。"));
            if (chosen.Length == 0 && choices.Length > 0 && ImGui.IsKeyPressed(ImGuiKey.Enter, false)) chosen = choices[m_highlight].id;
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) { ImGui.CloseCurrentPopup(); return false; }
            if (chosen.Length == 0) return false;
            fields.Finish(); StoryInsertionResult? result = null;
            documents.Edit(m_document, fields.Localize("快捷添加剧情指令"), candidate => result = documents.quickInsert.Create(candidate, m_fragment, chosen, m_position, m_insert ? m_edge : null));
            select(result!.nodeId.value);
            m_message = result.connected ? string.Empty : fields.Localize("已创建独立指令；请设置参数与目标连接，原有路径保持不变。");
            ImGui.CloseCurrentPopup(); return true;
        }
        finally { EditorUiSession.current.widgets.EndMenuPopup(); }
    }
    private static bool Control() => ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl) || ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper);
    private static bool Alt() => ImGui.IsKeyDown(ImGuiKey.LeftAlt) || ImGui.IsKeyDown(ImGuiKey.RightAlt);
}
