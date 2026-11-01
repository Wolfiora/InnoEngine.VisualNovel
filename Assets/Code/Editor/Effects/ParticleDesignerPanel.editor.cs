using System;
using System.Numerics;
using InnoEngine.Rendering;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Editor.Effects;

/// <summary>Authors native particle parameters and previews the same pure emitter and stage frame used by gameplay.</summary>
[EditorPanel("inno.vn.particles", "VN 粒子设计 · Particle Designer", order: 457, menuPath: "Authoring")]
public sealed class ParticleDesignerPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly StoryInspectorFields m_fields;
    private readonly NovelPreviewCanvas m_preview;
    private readonly ParticleCurveView m_curves;
    private double m_time = 1;
    private double m_seed = 1;
    private string m_message = string.Empty;
    /// <summary>Binds resource drafts, native History and production offscreen rendering.</summary>
    /// <param name="documents">The shared resource document owner.</param>
    /// <param name="interactions">Native selection and History.</param>
    /// <param name="previews">The engine preview host.</param>
    public ParticleDesignerPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions,
        IEditorPreviewService previews
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions); m_preview = new(previews);
        m_toolbar = new(documents, interactions, m_fields.Finish);
        m_curves = new(m_fields, documents);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1020, 820);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_fields.Finish(); m_preview.Dispose(); }

    private void Draw()
    {
        if (!m_toolbar.Draw("粒子预设", ".ivneffect", static () => new VisualEffectAsset { resourceId = "particles-" + Guid.NewGuid().ToString("N"), displayName = "新粒子" }, out VisualEffectAsset? asset) || asset is null) return;
        if (asset.kind != VisualEffectKind.Particles) { EditorUiSession.current.widgets.Hint(m_fields.Localize("此资源是转场或屏幕效果，请使用 VN 转场与屏幕效果。")); return; }
        ParticlePreset preset = asset.particles;
        string title = asset.displayName, id = asset.resourceId; StageLayerGroup group = preset.group;
        bool essential = preset.essential;
        bool changed = m_fields.Text("particle/title", m_fields.Localize("名称"), ref title); asset.displayName = title;
        changed |= m_fields.Text("particle/id", m_fields.Localize("剧情资源 ID"), ref id); asset.resourceId = id;
        changed |= m_fields.SelectEnum("particle/group", "合成分组", ref group); preset.group = group;
        changed |= m_fields.Toggle("particle/essential", "减少动态效果时仍保留", ref essential); preset.essential = essential;
        changed |= Number("width", "发射区域宽", () => preset.width, value => preset.width = value, 0, 1000000);
        changed |= Number("height", "发射区域高", () => preset.height, value => preset.height = value, 0, 1000000);
        changed |= Number("rate", "每秒发射量", () => preset.rate, value => preset.rate = value, 0.01, 10000);
        changed |= Number("duration", "发射秒数，0 为持续", () => preset.duration, value => preset.duration = value, 0, 1000000);
        changed |= Number("life-min", "最短生命秒数", () => preset.minimumLifetime, value => preset.minimumLifetime = value, 0.01, 3600);
        changed |= Number("life-max", "最长生命秒数", () => preset.maximumLifetime, value => preset.maximumLifetime = value, 0.01, 3600);
        changed |= Number("vx-min", "最小横向速度", () => preset.minimumVelocityX, value => preset.minimumVelocityX = value);
        changed |= Number("vx-max", "最大横向速度", () => preset.maximumVelocityX, value => preset.maximumVelocityX = value);
        changed |= Number("vy-min", "最小纵向速度", () => preset.minimumVelocityY, value => preset.minimumVelocityY = value);
        changed |= Number("vy-max", "最大纵向速度", () => preset.maximumVelocityY, value => preset.maximumVelocityY = value);
        changed |= Number("ax", "横向加速度", () => preset.accelerationX, value => preset.accelerationX = value);
        changed |= Number("ay", "纵向加速度", () => preset.accelerationY, value => preset.accelerationY = value);
        changed |= Number("size-start", "初始高度", () => preset.startSize, value => preset.startSize = value, 0, 1000000);
        changed |= Number("size-end", "最终高度", () => preset.endSize, value => preset.endSize = value, 0, 1000000);
        changed |= Number("aspect", "宽高比", () => preset.aspect, value => preset.aspect = value, 0.001, 100);
        changed |= Number("angular", "最大旋转速度", () => preset.angularSpeed, value => preset.angularSpeed = value, 0, 1000000);
        changed |= Number("rotation-min", "最小初始角度", () => preset.minimumRotation, value => preset.minimumRotation = value);
        changed |= Number("rotation-max", "最大初始角度", () => preset.maximumRotation, value => preset.maximumRotation = value);
        changed |= Number("parallax", "镜头视差", () => preset.parallax, value => preset.parallax = value);
        double order = preset.order; changed |= m_fields.Number("particle/order", m_fields.Localize("组内顺序"), ref order, -1000000, 1000000); preset.order = (int)order;
        uint start = preset.startColor, end = preset.endColor;
        changed |= m_fields.Color("particle/color-start", m_fields.Localize("初始颜色"), ref start); preset.startColor = start;
        changed |= m_fields.Color("particle/color-end", m_fields.Localize("最终颜色"), ref end); preset.endColor = end;
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } image && ImGui.Button(m_fields.Label("使用所选粒子图像"))) { m_fields.Finish(); preset.texture = image; changed = true; }
        if (preset.texture is not null && ImGui.Button(m_fields.Label("移除粒子图像，使用纯色"))) { m_fields.Finish(); preset.texture = null; changed = true; }
        if (changed) m_documents.Edit<VisualEffectAsset>(m_toolbar.documentId, m_fields.Localize("编辑粒子参数"), candidate =>
        { candidate.resourceId = asset.resourceId; candidate.displayName = asset.displayName; CopyTo(preset, candidate.particles); }, m_fields.mergeKey);
        m_curves.Draw(m_toolbar.documentId, preset);
        ImGui.SeparatorText(m_fields.Localize("纯时间预览 / Deterministic preview"));
        m_fields.Number("particle/preview-time", m_fields.Localize("时间"), ref m_time, 0, 1000000);
        m_fields.Number("particle/preview-seed", m_fields.Localize("固定种子"), ref m_seed, 1, uint.MaxValue);
        var stage = new StageController([], particlePresets: [asset]); stage.particles.Start("preview", asset.resourceId, (uint)m_seed); stage.Tick(m_time);
        float width = Math.Max(100, ImGui.GetContentRegionAvailable().X);
        m_preview.Draw(new UiFrame(1920, 1080, []), new(width, width * 1080 / 1920), stage.BuildFrame(1920, 1080));
    }
    private bool Number(
        string key,
        string label,
        Func<double> read,
        Action<double> write,
        double minimum = -1000000,
        double maximum = 1000000
    ) {
        double value = read(); bool changed = m_fields.Number("particle/" + key, label, ref value, minimum, maximum);
        if (changed) write(value); return changed;
    }
    private static void CopyTo(
        ParticlePreset source,
        ParticlePreset target
    ) {
        target.texture = source.texture; target.group = source.group; target.order = source.order;
        target.parallax = source.parallax; target.width = source.width; target.height = source.height; target.rate = source.rate; target.duration = source.duration;
        target.minimumLifetime = source.minimumLifetime; target.maximumLifetime = source.maximumLifetime; target.minimumVelocityX = source.minimumVelocityX;
        target.maximumVelocityX = source.maximumVelocityX; target.minimumVelocityY = source.minimumVelocityY; target.maximumVelocityY = source.maximumVelocityY;
        target.accelerationX = source.accelerationX; target.accelerationY = source.accelerationY; target.startSize = source.startSize; target.endSize = source.endSize;
        target.aspect = source.aspect; target.angularSpeed = source.angularSpeed; target.startColor = source.startColor; target.endColor = source.endColor; target.essential = source.essential;
        target.minimumRotation = source.minimumRotation; target.maximumRotation = source.maximumRotation;
    }
}
