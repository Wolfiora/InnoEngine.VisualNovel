using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEngine.Graphs;
using InnoEngine.Serialization;
using InnoEditor.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Editor.StoryGraph;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Variables;
using Inno.VisualNovel.Editor.Onboarding;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Editor.Debugging;
using Inno.VisualNovel.Editor.ScriptEditor;
using InnoEditor.Rendering;
using InnoEditor.Graph;

namespace Inno.VisualNovel.Editor.Workspace;

/// <summary>Composes script, graph and interpreter preview over the same native story document.</summary>
[EditorPanel("inno.vn.workspace", "Visual Novel", order: 450, defaultOpen: true, menuPath: "Authoring")]
public sealed class NovelWorkspacePanel : EditorPanel, IEditorPanelReloadState
{
    private readonly StoryDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly NodeSettingsView m_settings;
    private readonly StoryInspectorFields m_fields;
    private readonly VariableTableView m_variables;
    private readonly ChapterStructureView m_structure;
    private readonly StorySearchView m_search;
    private readonly StoryImportView m_import;
    private readonly NovelGameCreationView m_creation;
    private StoryGraphView m_graph;
    private string m_selected = string.Empty;
    private string m_message = string.Empty;
    private readonly StoryPreviewView m_preview;
    private StoryCompilationResult? m_compilation;
    private Guid m_graphDocument;
    private readonly ScriptEditorView m_script;
    private StoryDraftState? m_frameDraft;
    private long m_frameRevision = -1;
    private readonly NovelWorkspaceLayout m_layout;
    private string m_requestedTab = string.Empty;
    private long m_selectionRevision = -1;
    private string m_selectionPrimary = string.Empty;
    private readonly StoryDeletionView m_deletion;
    private readonly StoryInstructionMenuView m_instructions;
    private bool m_graphDrawn;

    /// <summary>Creates a panel whose business state belongs to the document module.</summary>
    /// <param name="documents">The shared authoring owner.</param>
    /// <param name="interactions">The engine history and selection coordinator.</param>
    /// <param name="sceneEdits">The host-owned scene mutation service.</param>
    /// <param name="scenes">The active scene documents.</param>
    /// <param name="properties">The root property history owner.</param>
    /// <param name="resources">The shared preview-case document owner.</param>
    /// <param name="previews">The native offscreen preview service.</param>
    /// <param name="serialization">The host-owned editor registry used by isolated native-format preview saves.</param>
    public NovelWorkspacePanel(
        StoryDocumentsModule documents,
        EditorInteractions interactions,
        SceneEdits sceneEdits,
        IEditorSceneWorkspace scenes,
        IInspectionPropertyEditService properties,
        ResourceDocumentsModule resources,
        IEditorPreviewService previews,
        SerializationRegistry serialization
    ) {
        m_documents = documents;
        m_interactions = interactions;
        m_fields = new(interactions);
        m_search = new(m_fields.text); m_graph = new(m_fields.text);
        m_layout = new(interactions, m_fields);
        m_deletion = new(m_fields);
        m_import = new(m_fields, interactions);
        m_settings = new(m_fields, interactions, SelectPastedNodes);
        m_script = new(documents, m_fields, interactions, m_settings, () => m_graph.navigation);
        m_variables = new(m_fields, resources, interactions);
        m_structure = new(m_fields);
        m_creation = new(documents, interactions, sceneEdits, scenes, properties);
        m_preview = new(documents, resources, interactions, m_fields, previews, new Authoring.Preview.EditorNovelStateCodec(serialization));
        m_instructions = new(documents, m_fields);
    }

    /// <inheritdoc />
    public override Vector2 initialSize => new(1180, 720);
    /// <inheritdoc />
    public override bool allowScrolling => false;

    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame(); m_graphDrawn = false;
        try { DrawWorkspace(context.projectDirectory); }
        catch (Exception failure) when (failure is not OutOfMemoryException) { m_preview.SetVisible(false); m_message = m_fields.Error(failure); m_frameDraft = null; m_frameRevision = -1; }
        finally { if (!m_graphDrawn) m_graph.Suspend(); m_fields.EndFrame(); }
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState()
    {
        m_search.Stop();
        m_settings.Finish();
        m_graph.Suspend();
        m_preview.Stop();
        return m_documents.CaptureReload();
    }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state)
    {
        m_search.Stop();
        m_documents.RestoreReload(state.Span); m_script.Reset(); m_frameDraft = null; m_frameRevision = -1;
    }

    /// <inheritdoc />
    protected override void OnDetach(EditorContext context)
    {
        m_search.Stop();
        m_preview.Dispose();
        m_settings.Finish();
        m_compilation = null;
        m_graph = new(m_fields.text);
        m_script.Reset(); m_frameDraft = null; m_frameRevision = -1;
    }

    private void DrawWorkspace(string projectDirectory)
    {
        m_layout.Header();
        if (m_fields.text.error.Length > 0) EditorUiSession.current.widgets.Hint(m_fields.text.error);
        if (ImGui.CollapsingHeader(m_fields.Label("创建可运行游戏 / New playable game"))) m_creation.Draw();
        if (ImGui.Button(m_fields.Label("创建分支故事 / Create story")))
        {
            m_settings.Finish();
            m_documents.CreateFirstStory();
        }
        ImGui.SameLine();
        if (ImGui.Button(m_fields.Label("打开所选章节 / Open selected")))
        {
            m_settings.Finish();
            AssetPath? path = m_interactions.selection.selectedTarget switch
            {
                StoryAsset asset => asset.assetPath,
                AssetFileEntry file when file.extension == ".ivnstory" => file.assetPath,
                _ => null
            };
            if (path is { } source) m_documents.Open(source);
            else m_message = m_fields.Localize("先在 File Browser 中选择一个 .ivnstory。");
        }
        EditorDocumentContext? document = m_documents.activeDocument;
        if (document is not null)
        {
            ImGui.SameLine();
            if (ImGui.Button(m_fields.Label("保存 / Save"))) { m_settings.Finish(); m_interactions.documents.Save(document.documentId); }
            ImGui.SameLine();
            if (ImGui.Button(m_fields.Label("撤销 / Undo"))) { m_settings.Finish(); m_interactions.history.Undo(); }
            ImGui.SameLine();
            if (ImGui.Button(m_fields.Label("重做 / Redo"))) { m_settings.Finish(); m_interactions.history.Redo(); }
            ImGui.Text(document.title + (document.isDirty ? m_fields.Localize("  · 未保存") : m_fields.Localize("  · 已保存")));
        }
        if (m_message.Length > 0)
            EditorUiSession.current.widgets.Hint(m_message);
        if (m_layout.separatePanels) { m_preview.SetVisible(false); m_layout.Dashboard(); return; }
        if (document is null)
        {
            m_script.Reset(); m_frameDraft = null; m_frameRevision = -1;
            m_preview.Stop();
            EditorUiSession.current.widgets.Hint(m_fields.Localize("创建第一段故事，或选择已导入的章节。剧本和节点图操作同一份内容；预览使用正式剧情执行器。"));
            return;
        }
        if (m_graphDocument != document.documentId)
        {
            m_graph = new(m_fields.text);
            m_graphDocument = document.documentId;
            m_selected = string.Empty;
            m_selectionRevision = -1; m_selectionPrimary = string.Empty;
            m_script.Reset(); m_frameDraft = null; m_frameRevision = -1;
            m_preview.Stop();
        }
        long revision = m_documents.GetRevision(document.documentId);
        if (m_frameDraft is null || m_frameRevision != revision) { m_frameDraft = m_documents.Read(document.documentId); m_frameRevision = revision; }
        StoryDraftState draft = m_frameDraft;
        if (StoryNodeSelection.TryRead(m_interactions, document.documentId, out string primary)
            && draft.graph.document.FindNode(new(primary)) is not null) m_selected = primary;
        if (m_selectionRevision != revision || m_selectionPrimary != m_selected)
        {
            m_selected = StoryNodeGroupSelection.Reconcile(m_graph.navigation, draft.graph.document, m_selected);
            m_selectionRevision = revision; m_selectionPrimary = m_selected;
        }
        DrawSelection(draft);
        bool reviewingDeletion = m_deletion.active;
        bool deleted = m_deletion.Draw(m_documents, draft);
        if (reviewingDeletion || deleted)
        {
            if (deleted) { SelectNode(string.Empty); m_frameDraft = null; m_frameRevision = -1; }
            m_preview.SetVisible(false); return;
        }
        if (m_layout.mode != NovelWorkspaceLayout.Mode.Tools)
        {
            bool visible = m_layout.DrawSplit(() => DrawStoryNavigation(draft),
                () => { if (m_layout.mode == NovelWorkspaceLayout.Mode.Writing) m_script.Draw(draft, m_selected, SelectNode, SelectGroupedNode, () => ClipboardShortcuts(draft), true); else DrawGraph(draft); },
                () => { if (!m_deletion.active) m_settings.Draw(m_documents, draft, m_selected, SelectNode); }, () =>
                { if (!m_deletion.active) m_preview.DrawCompact(draft, SelectNode, () => { m_layout.ShowTools(); m_requestedTab = "剧情预览 / Preview"; }); });
            m_preview.SetVisible(visible && !m_deletion.active); return;
        }
        if (!ImGui.BeginTabBar("vn-workspace")) { m_preview.SetVisible(false); return; }
        bool previewVisible = false;
        try
        {
            Tab("剧本 / Script", () => m_script.Draw(draft, m_selected, SelectNode, SelectGroupedNode, () => ClipboardShortcuts(draft)));
            Tab("节点图 / Graph", () => DrawGraph(draft));
            Tab("节点属性 / Properties", () => m_settings.Draw(m_documents, draft, m_selected, SelectNode));
            Tab("章节 / Chapter", () => m_structure.Draw(m_documents, draft, m_selected));
            Tab("变量 / Variables", () => m_variables.Draw(m_documents, draft, SelectNode));
            Tab("搜索与书签 / Search", () => m_search.Draw(m_documents, draft, SelectNode, m_interactions));
            Tab("导入与阅读稿 / Import", () => m_import.Draw(m_documents, draft, projectDirectory, SelectNode));
            Tab("剧情预览 / Preview", () => { previewVisible = true; DrawPreview(); });
            Tab("诊断 / Diagnostics", DrawDiagnostics);
        }
        finally { ImGui.EndTabBar(); m_preview.SetVisible(previewVisible); }
    }

    private void Tab(
        string name,
        Action draw
    ) {
        if (!ImGui.BeginTabItem(name, m_requestedTab == name ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None)) return;
        if (m_requestedTab == name) m_requestedTab = string.Empty;
        try
        {
            bool visible = ImGui.BeginChild("content-" + name);
            try { if (visible) draw(); }
            finally { ImGui.EndChild(); }
        }
        finally { ImGui.EndTabItem(); }
    }

    private void DrawPreview() => m_preview.Draw(m_documents.Read(m_documents.activeDocumentId), m_selected, SelectNode);
    private void DrawGraph(StoryDraftState draft)
    {
        bool previewSource = m_preview.ContainsSource(m_documents.activeDocument?.assetPath ?? string.Empty);
        m_graphDrawn = true; m_graph.Draw(m_documents, draft, m_selected, SelectGroupedNode, () => ClipboardShortcuts(draft),
            previewSource ? m_preview.debugger : null, previewSource ? m_preview.debugLocation : null, m_preview.executionPaused);
    }

    private void DrawStoryNavigation(StoryDraftState draft)
    {
        EditorUiSession.current.widgets.WrappedText(draft.title);
        foreach (var entry in draft.entries)
            if (ImGui.Selectable(m_fields.Caption("入口 · " + entry.id + "##entry-" + entry.id, m_fields.Localize("入口 · ") + entry.id + "##entry-" + entry.id), m_selected == entry.nodeId)) SelectNode(entry.nodeId);
        ImGui.SeparatorText(m_fields.Localize("片段"));
        foreach (var fragment in draft.fragments)
        {
            bool selected = fragment.nodes.Contains(m_selected, StringComparer.Ordinal);
            if (ImGui.Selectable(fragment.name + " · " + fragment.nodes.Length + "##fragment-" + fragment.id, selected)
                && fragment.nodes.FirstOrDefault() is { } first) SelectNode(first);
        }
        ImGui.Separator();
        EditorUiSession.current.widgets.Hint(m_fields.Localize("节点图和剧本共享节点选择与连线。章节结构、变量、全文搜索、书签、导入和诊断位于「全部工具」。"));
    }

    private void DrawDiagnostics()
    {
        if (ImGui.Button(m_fields.Label("检查章节 / Validate")))
            m_compilation = m_documents.Compile(m_documents.activeDocumentId);
        if (m_compilation is null) return;
        ImGui.Text(m_compilation.success ? m_fields.Localize("编译成功") : m_fields.Localize("编译失败"));
        foreach (GraphDiagnostic diagnostic in m_compilation.diagnostics)
        {
            if (ImGui.Selectable(diagnostic.severity + " · " + diagnostic.code + " · "
                + m_compilation.FormatDiagnostic(diagnostic, message => m_fields.Format(message.Format, message.GetArguments())) + "###vn-diagnostic/" + diagnostic.code + "/" + diagnostic.nodeId + "/" + diagnostic.edgeId))
            {
                var graph = m_frameDraft?.graph.document;
                string node = diagnostic.nodeId?.value ?? string.Empty;
                if (node.Length == 0 && diagnostic.edgeId is { } edgeId && graph?.edges.FirstOrDefault(edge => edge.id == edgeId) is { } edge)
                    node = graph.FindNode(edge.output.nodeId) is not null ? edge.output.nodeId.value : edge.input.nodeId.value;
                if (node.Length > 0 && graph?.FindNode(new(node)) is not null) SelectNode(node);
            }
        }
    }

    private void SelectNode(string nodeId)
    {
        m_graph.navigation.SelectNodes(nodeId.Length > 0 ? [new GraphNodeId(nodeId)] : []);
        SelectGroupedNode(nodeId);
    }
    private void SelectGroupedNode(string nodeId)
    {
        m_selected = nodeId;
        StoryNodeSelection.Select(m_interactions, m_documents.activeDocumentId, nodeId);
    }
    private void SelectPastedNodes(System.Collections.Generic.IReadOnlyCollection<string> nodes)
    {
        m_graph.navigation.SelectNodes(nodes.Select(static id => new GraphNodeId(id)));
        SelectGroupedNode(nodes.FirstOrDefault() ?? string.Empty);
    }
    private void DrawSelection(StoryDraftState draft)
    {
        int count = m_graph.navigation.selectedNodes.Count;
        ImGui.Text(m_fields.Localize("已选 ") + count + m_fields.Localize(" 项 · 剧本 Ctrl/Super 切换，Shift 连选；与节点图共用选择。"));
        if (ImGui.SmallButton(m_fields.Label("选择全部指令")))
        {
            m_fields.Finish(); m_graph.navigation.SelectNodes(draft.graph.document.nodes.Select(static node => node.id));
            SelectGroupedNode(m_selected.Length > 0 ? m_selected : draft.graph.document.nodes.FirstOrDefault()?.id.value ?? string.Empty);
        }
        if (count > 0)
        {
            ImGui.SameLine(); if (ImGui.SmallButton(m_fields.Caption("复制所选 " + count + " 项", m_fields.Localize("复制所选 ") + count + m_fields.Localize(" 项")))) m_settings.CopySelection(m_documents, draft, m_graph.navigation.selectedNodes.Select(static id => id.value).ToArray());
            ImGui.SameLine(); if (ImGui.SmallButton(m_fields.Label("只保留主选项"))) { m_fields.Finish(); SelectNode(m_selected); }
            ImGui.SameLine(); if (ImGui.SmallButton(m_fields.Label("补齐所选并行边界"))) SelectPastedNodes(m_documents.deletion.ExpandBoundaries(draft,
                m_graph.navigation.selectedNodes.Select(static id => id.value).ToArray()));
            ImGui.SameLine(); if (ImGui.SmallButton(m_fields.Label("删除所选组…"))) RequestDeletion(draft);
        }
    }
    private bool ClipboardShortcuts(StoryDraftState draft)
    {
        if (m_instructions.Draw(draft, m_selected, SelectNode)) { m_frameDraft = null; m_frameRevision = -1; m_compilation = null; return true; }
        StoryClipboardShortcuts.Handle(() =>
        {
            if (m_graph.navigation.selectedNodes.Count > 0) m_settings.CopySelection(m_documents, draft, m_graph.navigation.selectedNodes.Select(static id => id.value).ToArray());
        }, () =>
        {
            m_settings.RequestPaste();
            if (m_layout.mode == NovelWorkspaceLayout.Mode.Tools) m_requestedTab = "节点属性 / Properties";
        }, () => RequestDeletion(draft), () =>
        {
            m_fields.Finish(); SelectPastedNodes(draft.graph.document.nodes.Select(static node => node.id.value).ToArray());
        });
        return false;
    }
    private void RequestDeletion(StoryDraftState draft)
    {
        if (m_graph.navigation.selectedNodes.Count == 0) return;
        m_deletion.Request(m_documents, draft, m_graph.navigation.selectedNodes.Select(static id => id.value).ToArray());
    }
}
