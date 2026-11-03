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
[EditorPanel("inno.vn.particles", "VN Particle Designer", order: 457, defaultOpen: false, menuPath: "Authoring")]
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
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
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
        if (!m_toolbar.Draw("Particle preset", ".ivneffect", static () => new VisualEffectAsset { resourceId = "particles-" + Guid.NewGuid().ToString("N"), displayName = "New particle preset" }, out VisualEffectAsset? asset) || asset is null) return;
        if (asset.kind != VisualEffectKind.Particles) { EditorUiSession.current.widgets.Hint("This is a transition or screen effect. Use VN Transitions and Screen Effects."); return; }
        ParticlePreset preset = asset.particles;
        string title = asset.displayName, id = asset.resourceId; StageLayerGroup group = preset.group;
        bool essential = preset.essential;
        bool changed = m_fields.Text("particle/title", "Name", ref title); asset.displayName = title;
        changed |= m_fields.Text("particle/id", "Story resource ID", ref id); asset.resourceId = id;
        changed |= m_fields.SelectEnum("particle/group", "Composition group", ref group); preset.group = group;
        changed |= m_fields.Toggle("particle/essential", "Keep with reduced motion", ref essential); preset.essential = essential;
        changed |= Number("width", "Emission area width", () => preset.width, value => preset.width = value, 0, 1000000);
        changed |= Number("height", "Emission area height", () => preset.height, value => preset.height = value, 0, 1000000);
        changed |= Number("rate", "Particles per second", () => preset.rate, value => preset.rate = value, 0.01, 10000);
        changed |= Number("duration", "Emission duration (seconds; 0 emits continuously)", () => preset.duration, value => preset.duration = value, 0, 1000000);
        changed |= Number("life-min", "Minimum lifetime (seconds)", () => preset.minimumLifetime, value => preset.minimumLifetime = value, 0.01, 3600);
        changed |= Number("life-max", "Maximum lifetime (seconds)", () => preset.maximumLifetime, value => preset.maximumLifetime = value, 0.01, 3600);
        changed |= Number("vx-min", "Minimum horizontal velocity", () => preset.minimumVelocityX, value => preset.minimumVelocityX = value);
        changed |= Number("vx-max", "Maximum horizontal velocity", () => preset.maximumVelocityX, value => preset.maximumVelocityX = value);
        changed |= Number("vy-min", "Minimum vertical velocity", () => preset.minimumVelocityY, value => preset.minimumVelocityY = value);
        changed |= Number("vy-max", "Maximum vertical velocity", () => preset.maximumVelocityY, value => preset.maximumVelocityY = value);
        changed |= Number("ax", "Horizontal acceleration", () => preset.accelerationX, value => preset.accelerationX = value);
        changed |= Number("ay", "Vertical acceleration", () => preset.accelerationY, value => preset.accelerationY = value);
        changed |= Number("size-start", "Initial height", () => preset.startSize, value => preset.startSize = value, 0, 1000000);
        changed |= Number("size-end", "Final height", () => preset.endSize, value => preset.endSize = value, 0, 1000000);
        changed |= Number("aspect", "Aspect ratio", () => preset.aspect, value => preset.aspect = value, 0.001, 100);
        changed |= Number("angular", "Maximum angular velocity", () => preset.angularSpeed, value => preset.angularSpeed = value, 0, 1000000);
        changed |= Number("rotation-min", "Minimum initial angle", () => preset.minimumRotation, value => preset.minimumRotation = value);
        changed |= Number("rotation-max", "Maximum initial angle", () => preset.maximumRotation, value => preset.maximumRotation = value);
        changed |= Number("parallax", "Camera parallax", () => preset.parallax, value => preset.parallax = value);
        double order = preset.order; changed |= m_fields.Number("particle/order", "Order within group", ref order, -1000000, 1000000); preset.order = (int)order;
        uint start = preset.startColor, end = preset.endColor;
        changed |= m_fields.Color("particle/color-start", "Initial color", ref start); preset.startColor = start;
        changed |= m_fields.Color("particle/color-end", "Final color", ref end); preset.endColor = end;
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } image && ImGui.Button("Use selected particle image")) { m_fields.Finish(); preset.texture = image; changed = true; }
        if (preset.texture is not null && ImGui.Button("Remove particle image and use solid color")) { m_fields.Finish(); preset.texture = null; changed = true; }
        if (changed) m_documents.Edit<VisualEffectAsset>(m_toolbar.documentId, "Edit particle parameters", candidate =>
        { candidate.resourceId = asset.resourceId; candidate.displayName = asset.displayName; CopyTo(preset, candidate.particles); }, m_fields.mergeKey);
        m_curves.Draw(m_toolbar.documentId, preset);
        ImGui.SeparatorText("Deterministic time preview");
        m_fields.Number("particle/preview-time", "Time", ref m_time, 0, 1000000);
        m_fields.Number("particle/preview-seed", "Fixed seed", ref m_seed, 1, uint.MaxValue);
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
