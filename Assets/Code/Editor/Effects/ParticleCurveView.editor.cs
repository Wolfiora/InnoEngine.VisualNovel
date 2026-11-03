using System;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Editor.Effects;

internal sealed class ParticleCurveView(
    StoryInspectorFields fields,
    ResourceDocumentsModule documents
)
{
    private int m_size = -1;
    private int m_color = -1;
    private double m_progress = .5;
    private Guid m_document;
    internal void Draw(
        Guid documentId,
        ParticlePreset preset
    ) {
        if (m_document != documentId) { fields.Finish(); m_document = documentId; m_size = m_color = -1; }
        ImGui.SeparatorText("Lifetime curves");
        EditorUiSession.current.widgets.Hint("Positions from 0 to 1 represent each particle's normalized lifetime. Empty channels use the initial and final values above; a single keyframe holds a constant value. Each channel supports up to 64 points with hold, easing or Bezier timing.");
        fields.Number("particle/curve-add-age", "Lifetime position for new point", ref m_progress, 0, 1);
        foreach (var row in preset.sizeKeys.Select((key, index) => (key, index)))
            if (ImGui.Selectable(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Size · {0:0.###} · {1:0.###}##particle-size-{2}", row.key.time, row.key.value, row.index), m_size == row.index)) { fields.Finish(); m_size = row.index; }
        if (m_size >= 0 && m_size < preset.sizeKeys.Length)
        {
            var selected = preset.sizeKeys[m_size];
            var key = new PerformanceKeyframe { time = selected.time, value = selected.value, interpolation = selected.interpolation, curve = selected.curve.Copy() };
            double time = key.time, value = key.value; var mode = key.interpolation;
            bool changed = fields.Number("particle/size-age", "Size lifetime position", ref time, 0, 1);
            changed |= fields.Number("particle/size-value", "Height (design pixels)", ref value, 0, 1000000);
            changed |= fields.SelectEnum("particle/size-ease", "Size interpolation", ref mode);
            key.time = time; key.value = value; key.interpolation = mode;
            if (mode == PerformanceInterpolation.CubicBezier) changed |= Handles("size", key.curve);
            if (changed) { documents.Edit<VisualEffectAsset>(documentId, "Edit particle size curve", asset => ParticleCurveEditService.SetSize(asset.particles, m_size, key), fields.mergeKey); m_size = preset.sizeKeys.Where((_, index) => index != m_size).Count(item => item.time < time); }
            if (ImGui.Button("Remove size point")) { fields.Finish(); documents.Edit<VisualEffectAsset>(documentId, "Remove particle size point", asset => ParticleCurveEditService.Remove(asset.particles, false, m_size)); m_size = -1; }
        }
        if (ImGui.Button("Add size point"))
        { fields.Finish(); documents.Edit<VisualEffectAsset>(documentId, "Add particle size point", asset => ParticleCurveEditService.SetSize(asset.particles, -1, new() { time = m_progress, value = preset.startSize })); m_size = -1; }
        foreach (var row in preset.colorKeys.Select((key, index) => (key, index)))
            if (ImGui.Selectable(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Color · {0:0.###} · #{1:X8}##particle-color-{2}", row.key.progress, row.key.color, row.index), m_color == row.index)) { fields.Finish(); m_color = row.index; }
        if (m_color >= 0 && m_color < preset.colorKeys.Length)
        {
            var key = preset.colorKeys[m_color].Copy(); double time = key.progress; uint color = key.color; var mode = key.interpolation;
            bool changed = fields.Number("particle/color-age", "Color lifetime position", ref time, 0, 1);
            changed |= fields.Color("particle/gradient-color", "Gradient color", ref color);
            changed |= fields.SelectEnum("particle/color-ease", "Color interpolation", ref mode);
            key.progress = time; key.color = color; key.interpolation = mode;
            if (mode == PerformanceInterpolation.CubicBezier) changed |= Handles("color", key.curve);
            if (changed) { documents.Edit<VisualEffectAsset>(documentId, "Edit particle color gradient", asset => ParticleCurveEditService.SetColor(asset.particles, m_color, key), fields.mergeKey); m_color = preset.colorKeys.Where((_, index) => index != m_color).Count(item => item.progress < time); }
            if (ImGui.Button("Remove color point")) { fields.Finish(); documents.Edit<VisualEffectAsset>(documentId, "Remove particle color point", asset => ParticleCurveEditService.Remove(asset.particles, true, m_color)); m_color = -1; }
        }
        if (ImGui.Button("Add color point"))
        { fields.Finish(); documents.Edit<VisualEffectAsset>(documentId, "Add particle color point", asset => ParticleCurveEditService.SetColor(asset.particles, -1, new() { progress = m_progress, color = preset.startColor })); m_color = -1; }
    }
    private bool Handles(
        string channel,
        PerformanceCurve curve
    ) {
        double x1 = curve.x1, y1 = curve.y1, x2 = curve.x2, y2 = curve.y2;
        bool changed = fields.Number("particle/" + channel + "/x1", "First control point time", ref x1, 0, 1);
        changed |= fields.Number("particle/" + channel + "/y1", "First control point progress", ref y1, 0, 1);
        changed |= fields.Number("particle/" + channel + "/x2", "Second control point time", ref x2, 0, 1);
        changed |= fields.Number("particle/" + channel + "/y2", "Second control point progress", ref y2, 0, 1);
        curve.x1 = x1; curve.y1 = y1; curve.x2 = x2; curve.y2 = y2; return changed;
    }
}
