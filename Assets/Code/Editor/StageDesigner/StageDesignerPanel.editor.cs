using System;
using System.Linq;
using System.Numerics;
using InnoEngine.Rendering;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StageEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Editor.StageDesigner;

/// <summary>Edits native scenery layers and previews the production stage composition.</summary>
[EditorPanel("inno.vn.stage-designer", "VN 舞台设计 · Stage Designer", order: 452, menuPath: "Authoring")]
public sealed class StageDesignerPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly StoryInspectorFields m_fields;
    private readonly NovelPreviewCanvas m_preview;
    private readonly StageCanvasGestures m_canvas;
    private readonly StageLayerBatchView m_batch;
    private bool m_canvasDrawn;
    private string m_selected = string.Empty;
    private string m_message = string.Empty;
    /// <summary>Binds native resource documents and the shared offscreen preview service.</summary>
    /// <param name="documents">The resource draft owner.</param>
    /// <param name="interactions">The engine selection and History coordinator.</param>
    /// <param name="previews">The host-owned native preview bridge.</param>
    public StageDesignerPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions,
        IEditorPreviewService previews
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions); m_preview = new(previews);
        m_canvas = new(documents, interactions, m_fields);
        m_batch = new(m_fields);
        m_toolbar = new(documents, interactions, Finish);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1120, 760);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame(); m_canvasDrawn = false;
        try { Draw(); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
        finally { if (!m_canvasDrawn) m_canvas.Finish(); m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { Finish(); m_preview.Dispose(); }
    private void Finish() { m_canvas.Finish(); m_fields.Finish(); }
    private void Draw()
    {
        if (!m_toolbar.Draw("舞台", ".ivnstage", static () => new StageAsset { resourceId = "stage-" + Guid.NewGuid().ToString("N") }, out StageAsset? stage) || stage is null) return;
        bool changed = false;
        string title = stage.title, resource = stage.resourceId;
        changed |= m_fields.Text("stage-title", m_fields.Localize("舞台名称"), ref title); stage.title = title;
        changed |= m_fields.Text("stage-resource", m_fields.Localize("剧情资源 ID"), ref resource); stage.resourceId = resource;
        double designWidth = stage.width, designHeight = stage.height;
        changed |= m_fields.Number("stage/width", m_fields.Localize("设计宽度"), ref designWidth, 1, 100000); stage.width = designWidth;
        changed |= m_fields.Number("stage/height", m_fields.Localize("设计高度"), ref designHeight, 1, 100000); stage.height = designHeight;
        if (ImGui.Button(m_fields.Label("添加纯色图层")))
        {
            Finish(); m_documents.Edit<StageAsset>(m_toolbar.documentId, m_fields.Localize("添加舞台图层"), candidate => m_selected = StageEditService.AddLayer(candidate)); return;
        }
        foreach (StageLayer layer in stage.layers)
            if (ImGui.Selectable(layer.name + " · " + layer.group + "##" + layer.id, m_selected == layer.id)) { Finish(); m_selected = layer.id; }
        StageLayer? selected = stage.layers.SingleOrDefault(item => item.id == m_selected);
        if (selected is not null)
        {
            string name = selected.name;
            changed |= m_fields.Text(selected.id + "/name", m_fields.Localize("图层名称"), ref name); selected.name = name;
            StageLayerGroup group = selected.group;
            changed |= m_fields.SelectEnum(selected.id + "/band", "合成分组", ref group); selected.group = group;
            double order = selected.order, width = selected.width, height = selected.height, parallax = selected.parallax;
            changed |= m_fields.Number(selected.id + "/order", m_fields.Localize("组内顺序"), ref order, -1000000, 1000000); selected.order = (int)order;
            changed |= m_fields.Number(selected.id + "/width", m_fields.Localize("图层宽度"), ref width, 0, 100000); selected.width = width;
            changed |= m_fields.Number(selected.id + "/height", m_fields.Localize("图层高度"), ref height, 0, 100000); selected.height = height;
            changed |= m_fields.Number(selected.id + "/parallax", m_fields.Localize("视差系数"), ref parallax, -1000, 1000); selected.parallax = parallax;
            changed |= StageTransformFields.Draw(m_fields, selected.id, selected.transform);
            int mask = selected.mask;
            changed |= StageSurfaceFields.Draw(m_fields, selected.id, selected.surface, stage.textures, ref mask); selected.mask = mask;
            if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } maskImage && ImGui.Button(m_fields.Label("使用所选图像作为图层遮罩")))
            {
                m_fields.Finish(); StageEditService.SetMask(stage, m_selected, maskImage); changed = true;
            }
            uint color = selected.color;
            changed |= m_fields.Color(selected.id + "/color", m_fields.Localize("图层颜色"), ref color); selected.color = color;
            bool visible = selected.visible;
            changed |= m_fields.Toggle(selected.id + "/visible", "可见", ref visible); selected.visible = visible;
            if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } image && ImGui.Button(m_fields.Label("使用所选图像")))
            { Finish(); m_documents.Edit<StageAsset>(m_toolbar.documentId, m_fields.Localize("设置舞台图像"), candidate => StageEditService.SetImage(candidate, m_selected, image)); return; }
            ImGui.SameLine();
            if (ImGui.Button(m_fields.Label("移除此图层")))
            { Finish(); m_documents.Edit<StageAsset>(m_toolbar.documentId, m_fields.Localize("移除舞台图层"), candidate => StageEditService.RemoveLayer(candidate, m_selected)); m_selected = string.Empty; return; }
        }
        if (changed) m_documents.Edit<StageAsset>(m_toolbar.documentId, m_fields.Localize("编辑舞台"), candidate =>
        { candidate.title = stage.title; candidate.resourceId = stage.resourceId; candidate.width = stage.width; candidate.height = stage.height; candidate.layers = stage.layers; candidate.textures = stage.textures; }, m_fields.mergeKey);
        if (m_batch.Draw(m_documents, m_toolbar.documentId, stage, Finish)) return;
        var composition = new StageController(stage.textures); composition.SetStage(stage);
        m_canvas.Toolbar(stage);
        float widthAvailable = Math.Max(100, ImGui.GetContentRegionAvailable().X);
        float heightAvailable = Math.Max(120, ImGui.GetContentRegionAvailable().Y);
        float scale = Math.Max(.001f, Math.Min(widthAvailable / (float)stage.width, heightAvailable / (float)stage.height));
        Vector2 size = new((float)stage.width * scale, (float)stage.height * scale), origin = ImGui.GetCursorScreenPosition();
        var frame = composition.BuildFrame((float)stage.width, (float)stage.height);
        m_preview.Draw(new UiFrame((float)stage.width, (float)stage.height, []), size, frame);
        m_canvasDrawn = true; m_canvas.Draw(m_toolbar.documentId, stage, frame, origin, size, scale, m_selected, selectedId => m_selected = selectedId);
    }
}
