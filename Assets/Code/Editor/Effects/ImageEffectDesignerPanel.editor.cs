using System;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Editor.Effects;

/// <summary>Edits screen and transition presets with native History and pure-time production previews.</summary>
[EditorPanel("inno.vn.image-effects", "VN 转场与屏幕效果 · Transitions and Screen Effects", order: 458, menuPath: "Authoring")]
public sealed class ImageEffectDesignerPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly StoryInspectorFields m_fields;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly NovelPreviewCanvas m_preview;
    private double m_time = .3;
    private double m_strength = 1;
    private string m_message = "";
    /// <summary>Binds native documents, gestures and the production offscreen rendering host.</summary>
    /// <param name="documents">Shared native resource drafts.</param>
    /// <param name="interactions">Engine History and asset selection.</param>
    /// <param name="previews">The engine-owned preview service.</param>
    public ImageEffectDesignerPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions,
        IEditorPreviewService previews
    ) {
        m_documents = documents; m_fields = new(interactions); m_toolbar = new(documents, interactions, m_fields.Finish); m_preview = new(previews);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1020, 850);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); } catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
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
        if (!m_toolbar.Draw("转场 / 屏幕效果", ".ivneffect", static () => new VisualEffectAsset { resourceId = "effect-" + Guid.NewGuid().ToString("N"), displayName = "新效果", kind = VisualEffectKind.Screen }, out VisualEffectAsset? asset) || asset is null) return;
        if (asset.kind == VisualEffectKind.Particles) { EditorUiSession.current.widgets.Hint(m_fields.Localize("此资源是粒子，请使用 VN 粒子设计。内置预设修改前需创建项目副本。")); return; }
        string id = asset.resourceId, title = asset.displayName, description = asset.description, kind = asset.kind.ToString();
        bool changed = m_fields.Text("image/id", m_fields.Localize("剧情资源 ID"), ref id); asset.resourceId = id;
        changed |= m_fields.Text("image/title", m_fields.Localize("名称"), ref title); asset.displayName = title;
        changed |= m_fields.Text("image/description", m_fields.Localize("说明"), ref description); asset.description = description;
        changed |= m_fields.Select("image/kind", m_fields.Localize("资源分类"), ref kind, [("Screen", m_fields.Localize("屏幕效果")), ("Transition", m_fields.Localize("转场"))]); asset.kind = Enum.Parse<VisualEffectKind>(kind);
        changed |= asset.kind == VisualEffectKind.Screen ? Screen(asset.screen) : Transition(asset.transition);
        if (changed) m_documents.Edit<VisualEffectAsset>(m_toolbar.documentId, m_fields.Localize("编辑转场或屏幕效果"), candidate =>
        {
            candidate.resourceId = asset.resourceId; candidate.displayName = asset.displayName; candidate.description = asset.description; candidate.kind = asset.kind;
            candidate.screen = asset.screen.Copy(); candidate.transition = asset.transition.Copy();
        }, m_fields.mergeKey);
        ImGui.SeparatorText(m_fields.Localize("纯时间预览 / Deterministic preview"));
        m_fields.Number("image/time", m_fields.Localize("逻辑秒数"), ref m_time, 0, 3600); m_fields.Number("image/strength", m_fields.Localize("预览强度"), ref m_strength, 0, 1);
        var stage = new StageController([], screenPresets: asset.kind == VisualEffectKind.Screen ? [asset] : [], transitionPresets: asset.kind == VisualEffectKind.Transition ? [asset] : []);
        stage.SetStage(Scenery(0x24385AFF, 0xD99E6CFF));
        if (asset.kind == VisualEffectKind.Screen) stage.ApplyScreenEffect(new() { resourceId = asset.resourceId, strength = m_strength });
        else stage.ApplyTransition(new() { resourceId = asset.resourceId }, Scenery(0x637A51FF, 0xABDCE8FF));
        stage.Tick(m_time);
        float width = Math.Max(100, ImGui.GetContentRegionAvailable().X);
        m_preview.Draw(new UiFrame(1920, 1080, []), new(width, width * 1080 / 1920), stage.BuildFrame(1920, 1080));
    }
    private bool Screen(ScreenEffectDefinition effect)
    {
        var kind = effect.kind; bool changed = m_fields.SelectEnum("image/screen-kind", "处理方式", ref kind); effect.kind = kind;
        switch (kind)
        {
            case ScreenEffectKind.Blur: changed |= Number("blur", "模糊半径", () => effect.blurRadius, value => effect.blurRadius = value, 0, 32); break;
            case ScreenEffectKind.Vignette:
                changed |= Number("vignette-radius", "暗角起点", () => effect.vignetteRadius, value => effect.vignetteRadius = value, 0, 1);
                changed |= Number("vignette-soft", "边缘柔度", () => effect.vignetteSoftness, value => effect.vignetteSoftness = value, .001, 1); break;
            case ScreenEffectKind.ColorGrade:
                changed |= Number("saturation", "饱和度", () => effect.saturation, value => effect.saturation = value, 0, 4);
                changed |= Number("brightness", "亮度", () => effect.brightness, value => effect.brightness = value, -1, 1);
                changed |= Number("contrast", "对比度", () => effect.contrast, value => effect.contrast = value, 0, 4); break;
            case ScreenEffectKind.Pixelate: changed |= Number("pixels", "像素块宽度", () => effect.pixelSize, value => effect.pixelSize = value, 1, 512); break;
            case ScreenEffectKind.Letterbox: changed |= Number("bars", "单侧遮幅比例", () => effect.letterboxRatio, value => effect.letterboxRatio = value, 0, .45); goto case ScreenEffectKind.Flash;
            case ScreenEffectKind.Flash:
                uint color = effect.color; changed |= m_fields.Color("image/color", m_fields.Localize("叠加颜色"), ref color); effect.color = color; break;
            case ScreenEffectKind.ChromaticAberration: changed |= Number("aberration", "色差偏移", () => effect.aberrationPixels, value => effect.aberrationPixels = value, 0, 32); break;
        }
        return changed;
    }
    private bool Transition(TransitionDefinition effect)
    {
        var kind = effect.kind; bool changed = m_fields.SelectEnum("image/transition-kind", "转场机制", ref kind); effect.kind = kind;
        changed |= Number("duration", "持续秒数", () => effect.duration, value => effect.duration = value, .001, 3600);
        if (kind is TransitionKind.Wipe or TransitionKind.Slide) changed |= Number("angle", "方向角度", () => effect.angle, value => effect.angle = value, -3600, 3600);
        if (kind is TransitionKind.Wipe or TransitionKind.Mask or TransitionKind.Iris) changed |= Number("softness", "边缘柔度", () => effect.softness, value => effect.softness = value, 0, .5);
        if (kind == TransitionKind.Fade) { uint color = effect.color; changed |= m_fields.Color("image/fade-color", m_fields.Localize("中间颜色"), ref color); effect.color = color; }
        if (kind == TransitionKind.Mask)
        {
            var mask = effect.mask; changed |= m_fields.SelectEnum("image/mask", "遮罩图案", ref mask); effect.mask = mask;
            double seed = effect.seed; changed |= m_fields.Number("image/seed", m_fields.Localize("固定种子"), ref seed, 1, uint.MaxValue); effect.seed = (uint)seed;
        }
        if (kind == TransitionKind.Iris)
        {
            changed |= Number("center-x", "中心横向比例", () => effect.centerX, value => effect.centerX = value, 0, 1);
            changed |= Number("center-y", "中心纵向比例", () => effect.centerY, value => effect.centerY = value, 0, 1);
            bool reverse = effect.reverse; changed |= m_fields.Toggle("image/reverse", "从外向内", ref reverse); effect.reverse = reverse;
        }
        return changed;
    }
    private bool Number(
        string id,
        string label,
        Func<double> read,
        Action<double> write,
        double minimum,
        double maximum
    ) {
        double value = read(); bool changed = m_fields.Number("image/" + id, label, ref value, minimum, maximum); if (changed) write(value); return changed;
    }
    private static StageAsset Scenery(
        uint background,
        uint accent
    ) => new() { layers = [new() { id = "background", color = background }, new() { id = "accent", width = 700, height = 600, color = accent, order = 1, transform = new() { x = 800, y = 260, rotation = 12 } }] };
}
