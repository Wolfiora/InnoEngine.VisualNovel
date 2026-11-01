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
        ImGui.SeparatorText(fields.Localize("生命周期曲线"));
        EditorUiSession.current.widgets.Hint(fields.Localize("位置 0–1 表示每个粒子的生命比例。空通道使用上方初始/最终值；单关键帧为恒定值。最多 64 个点，支持保持、缓动和 Bezier 时间曲线。"));
        fields.Number("particle/curve-add-age", fields.Localize("新增点的生命位置"), ref m_progress, 0, 1);
        foreach (var row in preset.sizeKeys.Select((key, index) => (key, index)))
            if (ImGui.Selectable(fields.Caption($"尺寸 · {row.key.time:0.###} · {row.key.value:0.###}##particle-size-{row.index}", fields.Format("尺寸 · {0:0.###} · {1:0.###}##particle-size-{2}", row.key.time, row.key.value, row.index)), m_size == row.index)) { fields.Finish(); m_size = row.index; }
        if (m_size >= 0 && m_size < preset.sizeKeys.Length)
        {
            var selected = preset.sizeKeys[m_size];
            var key = new PerformanceKeyframe { time = selected.time, value = selected.value, interpolation = selected.interpolation, curve = selected.curve.Copy() };
            double time = key.time, value = key.value; var mode = key.interpolation;
            bool changed = fields.Number("particle/size-age", fields.Localize("尺寸生命位置"), ref time, 0, 1);
            changed |= fields.Number("particle/size-value", fields.Localize("高度（设计像素）"), ref value, 0, 1000000);
            changed |= fields.SelectEnum("particle/size-ease", "尺寸插值", ref mode);
            key.time = time; key.value = value; key.interpolation = mode;
            if (mode == PerformanceInterpolation.CubicBezier) changed |= Handles("size", key.curve);
            if (changed) { documents.Edit<VisualEffectAsset>(documentId, fields.Localize("编辑粒子尺寸曲线"), asset => ParticleCurveEditService.SetSize(asset.particles, m_size, key), fields.mergeKey); m_size = preset.sizeKeys.Where((_, index) => index != m_size).Count(item => item.time < time); }
            if (ImGui.Button(fields.Label("删除尺寸点"))) { fields.Finish(); documents.Edit<VisualEffectAsset>(documentId, fields.Localize("删除粒子尺寸点"), asset => ParticleCurveEditService.Remove(asset.particles, false, m_size)); m_size = -1; }
        }
        if (ImGui.Button(fields.Label("新增尺寸点")))
        { fields.Finish(); documents.Edit<VisualEffectAsset>(documentId, fields.Localize("新增粒子尺寸点"), asset => ParticleCurveEditService.SetSize(asset.particles, -1, new() { time = m_progress, value = preset.startSize })); m_size = -1; }
        foreach (var row in preset.colorKeys.Select((key, index) => (key, index)))
            if (ImGui.Selectable(fields.Caption($"颜色 · {row.key.progress:0.###} · #{row.key.color:X8}##particle-color-{row.index}", fields.Format("颜色 · {0:0.###} · #{1:X8}##particle-color-{2}", row.key.progress, row.key.color, row.index)), m_color == row.index)) { fields.Finish(); m_color = row.index; }
        if (m_color >= 0 && m_color < preset.colorKeys.Length)
        {
            var key = preset.colorKeys[m_color].Copy(); double time = key.progress; uint color = key.color; var mode = key.interpolation;
            bool changed = fields.Number("particle/color-age", fields.Localize("颜色生命位置"), ref time, 0, 1);
            changed |= fields.Color("particle/gradient-color", fields.Localize("渐变颜色"), ref color);
            changed |= fields.SelectEnum("particle/color-ease", "颜色插值", ref mode);
            key.progress = time; key.color = color; key.interpolation = mode;
            if (mode == PerformanceInterpolation.CubicBezier) changed |= Handles("color", key.curve);
            if (changed) { documents.Edit<VisualEffectAsset>(documentId, fields.Localize("编辑粒子颜色渐变"), asset => ParticleCurveEditService.SetColor(asset.particles, m_color, key), fields.mergeKey); m_color = preset.colorKeys.Where((_, index) => index != m_color).Count(item => item.progress < time); }
            if (ImGui.Button(fields.Label("删除颜色点"))) { fields.Finish(); documents.Edit<VisualEffectAsset>(documentId, fields.Localize("删除粒子颜色点"), asset => ParticleCurveEditService.Remove(asset.particles, true, m_color)); m_color = -1; }
        }
        if (ImGui.Button(fields.Label("新增颜色点")))
        { fields.Finish(); documents.Edit<VisualEffectAsset>(documentId, fields.Localize("新增粒子颜色点"), asset => ParticleCurveEditService.SetColor(asset.particles, -1, new() { progress = m_progress, color = preset.startColor })); m_color = -1; }
    }
    private bool Handles(
        string channel,
        PerformanceCurve curve
    ) {
        double x1 = curve.x1, y1 = curve.y1, x2 = curve.x2, y2 = curve.y2;
        bool changed = fields.Number("particle/" + channel + "/x1", fields.Localize("第一个控制点时间"), ref x1, 0, 1);
        changed |= fields.Number("particle/" + channel + "/y1", fields.Localize("第一个控制点进度"), ref y1, 0, 1);
        changed |= fields.Number("particle/" + channel + "/x2", fields.Localize("第二个控制点时间"), ref x2, 0, 1);
        changed |= fields.Number("particle/" + channel + "/y2", fields.Localize("第二个控制点进度"), ref y2, 0, 1);
        curve.x1 = x1; curve.y1 = y1; curve.x2 = x2; curve.y2 = y2; return changed;
    }
}
