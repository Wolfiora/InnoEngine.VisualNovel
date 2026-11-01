using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Stage.Timeline;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Player.Interface.Performance;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Editor.ScreenDesigner;

namespace Inno.VisualNovel.Editor.Timeline;

/// <summary>Edits reusable performance tracks and keys using pure, seekable stage preview.</summary>
[EditorPanel("inno.vn.timeline", "VN 演出时间线 · Performance Timeline", order: 455, menuPath: "Authoring")]
public sealed class TimelinePanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly StoryInspectorFields m_fields;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly NovelPreviewCanvas m_preview;
    private readonly PerformanceCurveView m_curve;
    private readonly TimelineCanvasView m_canvas;
    private readonly EditorInteractions m_interactions;
    private readonly Dictionary<string, string> m_targets = new(StringComparer.Ordinal);
    private StageAsset? m_stage;
    private ScreenAsset? m_uiPage;
    private VisualEffectAsset? m_effect;
    private string m_effectInstance = "preview-effect";
    private string m_uiRole = "reading";
    private readonly ScreenPreviewDataSource m_uiData = new();
    private string m_slot = "actor";
    private PerformanceProperty m_property;
    private double m_time;
    private int m_track;
    private double m_keyTime;
    private bool m_canvasDrawn;
    private string m_message = string.Empty;
    private string m_marker = string.Empty;
    private bool m_snap = true, m_replaceRange, m_allTracks;
    private double m_gridStep = .05, m_rangeStart, m_rangeEnd;
    /// <summary>Binds native clip documents, engine gestures and the production preview bridge.</summary>
    /// <param name="documents">The native resource draft owner.</param>
    /// <param name="interactions">The host selection and History coordinator.</param>
    /// <param name="previews">The shared offscreen preview service.</param>
    public TimelinePanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions,
        IEditorPreviewService previews
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions);
        m_curve = new(m_fields, interactions.history);
        m_canvas = new(documents, interactions, m_fields);
        m_toolbar = new(documents, interactions, Finish); m_preview = new(previews);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1100, 800);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame(); m_canvasDrawn = false;
        try { Draw(); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
        finally { if (!m_canvasDrawn) m_canvas.Suspend(); m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { Finish(); m_preview.Dispose(); m_stage = null; }
    private void Draw()
    {
        if (!m_toolbar.Draw("演出片段", ".ivnclip", static () => new PerformanceClipAsset { resourceId = "performance-" + Guid.NewGuid().ToString("N") }, out PerformanceClipAsset? clip) || clip is null) return;
        string name = clip.displayName, resource = clip.resourceId;
        bool changed = m_fields.Text("clip/name", m_fields.Localize("片段名称"), ref name); clip.displayName = name;
        changed |= m_fields.Text("clip/resource", m_fields.Localize("剧情资源 ID"), ref resource); clip.resourceId = resource;
        double duration = clip.duration;
        changed |= m_fields.Number("clip/duration", m_fields.Localize("片段秒数"), ref duration, 0.001, 36000);
        if (duration < clip.tracks.SelectMany(static item => item.keys).Select(static key => key.time).Concat(clip.markers.Select(static marker => marker.time)).DefaultIfEmpty(0).Max()) throw new InvalidOperationException(m_fields.Localize("先移动或删除超出新时长的关键帧和标记。"));
        clip.duration = duration;
        bool loop = clip.loop;
        changed |= m_fields.Toggle("clip/loop", "循环", ref loop); clip.loop = loop;
        bool essential = clip.essential;
        changed |= m_fields.Toggle("clip/essential", "减少动态时保留的重要演出", ref essential); clip.essential = essential;
        if (changed) m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("编辑演出片段"), candidate =>
        { candidate.displayName = clip.displayName; candidate.resourceId = clip.resourceId; candidate.duration = clip.duration; candidate.loop = clip.loop; candidate.essential = clip.essential; }, m_fields.mergeKey);
        m_fields.Text("clip/new-target", m_fields.Localize("新轨道目标槽"), ref m_slot);
        m_fields.SelectEnum("clip/new-property", "新轨道属性", ref m_property);
        if (ImGui.Button(m_fields.Label("添加属性轨道")))
        { Finish(); m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("添加演出轨道"), candidate => PerformanceEditService.AddTrack(candidate, m_slot, m_property)); }
        double time = Math.Min(m_time, clip.duration);
        m_fields.Number("clip/preview-time", m_fields.Localize("预览逻辑秒数"), ref time, 0, clip.duration); m_time = time;
        m_fields.Toggle("clip/snap", "拖动与写入时吸附", ref m_snap);
        m_fields.Number("clip/grid", m_fields.Localize("吸附间隔（秒）"), ref m_gridStep, .000001, 36000);
        m_canvasDrawn = true;
        if (m_canvas.Draw(m_toolbar.documentId, clip, ref m_track, ref m_keyTime, ref m_time, ref m_marker, m_snap, m_gridStep)) return;
        Markers(clip);
        for (int index = 0; index < clip.tracks.Length; index++)
        {
            PerformanceTrack track = clip.tracks[index];
            if (ImGui.Selectable(track.target + " · " + track.property + "##track-" + index, m_track == index))
            { Finish(); m_track = index; m_keyTime = track.keys[0].time; }
        }
        if (m_track >= 0 && m_track < clip.tracks.Length) Keys(clip);
        Range(clip);
        foreach (var sample in new PerformancePlayer(clip).Evaluate(m_time).Where(static sample => sample.property == PerformanceProperty.Volume))
            ImGui.Text(m_fields.Localize("音量预览 · ") + sample.target + " × " + sample.value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        if (clip.tracks.Any(static track => track.property == PerformanceProperty.Volume))
            EditorUiSession.current.widgets.Hint(m_fields.Localize("这里纯求值音量数值；实际声音与混合演出在剧情预览中使用同一片段，不在拖动时启动或重播音频。"));
        if (NovelResourceSelection.Get<StageAsset>(m_interactions) is { } selected && ImGui.Button(m_fields.Label("使用所选舞台作为明确预览状态"))) m_stage = StageAssetCopy.Copy(selected);
        if (NovelResourceSelection.Get<ScreenAsset>(m_interactions) is { } page && ImGui.Button(m_fields.Label("使用所选页面作为 UI 预览"))) m_uiPage = UiDefinitionCopy.Copy(page);
        if (NovelResourceSelection.Get<VisualEffectAsset>(m_interactions) is { kind: VisualEffectKind.Screen } effect && ImGui.Button(m_fields.Label("使用所选屏幕效果预览参数"))) m_effect = VisualEffectCopy.Copy(effect);
        if (m_effect is not null)
        {
            m_fields.Text("preview/effect-instance", m_fields.Localize("效果实例"), ref m_effectInstance);
            if (string.IsNullOrWhiteSpace(m_effectInstance)) { EditorUiSession.current.widgets.Hint(m_fields.Localize("指定一个非空效果实例。")); return; }
            if (ImGui.SmallButton(m_fields.Label("关闭效果预览"))) m_effect = null;
        }
        if (m_uiPage is not null)
        {
            m_fields.Text("preview/ui-role", m_fields.Localize("页面角色"), ref m_uiRole);
            if (!Model.Story.Nodes.Interface.SystemNodeRules.IsPageRole(m_uiRole)) { EditorUiSession.current.widgets.Hint(m_fields.Localize("页面角色必须是有效的游戏壳角色。")); return; }
            if (ImGui.SmallButton(m_fields.Label("关闭页面预览"))) m_uiPage = null;
        }
        if (m_stage is null && m_uiPage is null) { EditorUiSession.current.widgets.Hint(m_fields.Localize("选择舞台或 UI 页面作为明确预览状态；拖动时间线只求值，不执行游戏动作。")); return; }
        var ui = new NovelUiPerformance(new() { screens = m_uiPage is null ? [] : [new() { role = m_uiRole, screen = m_uiPage }] });
        var uiChannels = ui.GetChannels();
        string effectTarget = "effect/" + m_effectInstance;
        var effectChannels = m_effect is null ? [] : ScreenEffectChannels.For(m_effect.screen.kind);
        foreach (string slot in clip.tracks.Select(static item => item.target).Distinct(StringComparer.Ordinal))
        {
            bool volume = clip.tracks.Any(track => track.target == slot && track.property == PerformanceProperty.Volume);
            string target = m_targets.GetValueOrDefault(slot) ?? (volume ? slot.StartsWith("audio/", StringComparison.Ordinal) ? slot : "audio/" + slot
                : slot == "camera" ? "camera" : uiChannels.ContainsKey(slot) ? slot : m_effect is not null && slot == effectTarget ? effectTarget : string.Empty);
            if (volume) m_fields.Text("preview/" + slot, m_fields.Localize("纯预览音量目标 ") + slot, ref target);
            else m_fields.Select("preview/" + slot, m_fields.Localize("预览绑定 ") + slot, ref target, (m_stage?.layers.Where(_ => clip.tracks.Where(track => track.target == slot).All(track => StagePerformanceController.SupportsStageProperty(track.property)))
                    .Select(static layer => ("stage/" + layer.id, layer.name)) ?? [])
                .Concat(m_stage is null || !clip.tracks.Where(track => track.target == slot).All(track => StagePerformanceController.SupportsStageProperty(track.property, true)) ? [] : new[] { ("camera", m_fields.Localize("镜头")) })
                .Concat(ui.targets.Where(item => clip.tracks.Where(track => track.target == slot).All(track => uiChannels[item.id].Contains(track.property))))
                .Concat(m_effect is not null && clip.tracks.Where(track => track.target == slot).All(track => effectChannels.Contains(track.property)) ? new[] { (effectTarget, "屏幕效果 · " + m_effectInstance) } : []));
            m_targets[slot] = target;
        }
        if (clip.tracks.Any(track => string.IsNullOrWhiteSpace(m_targets.GetValueOrDefault(track.target)))) return;
        var channels = clip.tracks.Where(static track => track.property == PerformanceProperty.Volume)
            .Select(track => m_targets[track.target]).Distinct(StringComparer.Ordinal).ToDictionary(static target => target, static _ => new[] { PerformanceProperty.Volume }, StringComparer.Ordinal);
        foreach (var pair in uiChannels) channels.Add(pair.Key, pair.Value);
        if (m_effect is not null) channels.Add(effectTarget, effectChannels);
        var bindings = m_targets.Where(pair => clip.tracks.Any(track => track.target == pair.Key)).Select(static pair => new PerformanceBinding { slot = pair.Key, target = pair.Value }).ToArray();
        ui.ValidateBinding(clip, bindings, "Timeline preview");
        var stage = new StageController(m_stage?.textures ?? [], [clip], screenPresets: m_effect is null ? [] : [m_effect], additionalTargets: channels); if (m_stage is not null) stage.SetStage(m_stage);
        if (m_effect is not null) stage.ApplyScreenEffect(new() { operation = ScreenEffectOperation.Set, resourceId = m_effect.resourceId, instanceId = m_effectInstance, includeUi = true });
        stage.ApplyPerformance(new() { operation = PerformanceOperation.Play, instanceId = "preview", clipId = clip.resourceId,
            bindings = bindings });
        stage.ApplyPerformance(new() { operation = PerformanceOperation.Seek, instanceId = "preview", time = m_time });
        float width = Math.Max(100, ImGui.GetContentRegionAvailable().X);
        float designWidth = m_uiPage?.width ?? (float)m_stage!.width, designHeight = m_uiPage?.height ?? (float)m_stage!.height;
        UiFrame frame = new(designWidth, designHeight, []);
        if (m_uiPage is not null)
        {
            var document = new UiDocumentInstance(m_uiPage, NovelPresentationFrame.Measure, NovelPresentationFrame.MeasureRichText);
            m_uiData.SetPage(m_uiPage.stateVariables); document.SetPerformanceValues(ui.GetValues(m_uiRole, stage.performances.Evaluate())); frame = document.BuildFrame(m_uiData, false);
        }
        m_preview.Draw(frame, new(width, width * designHeight / designWidth), stage.BuildFrame(designWidth, designHeight));
    }
    private void Keys(PerformanceClipAsset clip)
    {
        PerformanceTrack track = clip.tracks[m_track];
        foreach (PerformanceKeyframe key in track.keys)
            if (ImGui.Selectable($"{key.time:0.###}s · {key.value:0.###} · {key.interpolation}##key-{m_track}-{key.time:R}", m_keyTime == key.time))
            { Finish(); m_keyTime = key.time; m_time = key.time; }
        PerformanceKeyframe? selected = track.keys.SingleOrDefault(key => key.time == m_keyTime);
        if (selected is not null)
        {
            double value = selected.value;
            var interpolation = selected.interpolation;
            var bounds = PerformancePropertyRules.Range(track.property);
            bool changed = m_fields.Number("clip/key-value", m_fields.Localize("关键帧数值"), ref value, bounds.minimum, bounds.maximum);
            changed |= m_fields.SelectEnum("clip/key-interpolation", "插值", ref interpolation);
            PerformanceCurve curve = selected.curve.Copy();
            if (interpolation == PerformanceInterpolation.CubicBezier) changed |= m_curve.Draw(curve); else m_curve.Finish();
            if (changed) m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("编辑关键帧"), candidate => PerformanceEditService.SetKey(candidate, m_track, m_keyTime, value, interpolation, curve), m_curve.mergeKey);
        }
        if (ImGui.Button(m_fields.Label("在当前预览时间写入关键帧")))
        {
            Finish(); double target = Snap(m_time, clip.duration), value = new PerformancePlayer(clip).Evaluate(target)[m_track].value;
            m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("写入关键帧"), candidate => PerformanceEditService.SetKey(candidate, m_track, target, value, PerformanceInterpolation.Linear)); m_keyTime = m_time = target;
        }
        if (track.keys.Length > 1 && ImGui.Button(m_fields.Label("删除选中关键帧")))
        { Finish(); m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("删除关键帧"), candidate => candidate.tracks[m_track].keys = candidate.tracks[m_track].keys.Where(key => key.time != m_keyTime).ToArray()); }
    }
    private void Markers(PerformanceClipAsset clip)
    {
        ImGui.SeparatorText(m_fields.Localize("同步标记"));
        foreach (var marker in clip.markers)
            if (ImGui.Selectable($"{marker.name} · {marker.time:0.###}s##marker-{marker.id}", m_marker == marker.id)) { Finish(); m_marker = marker.id; m_time = marker.time; }
        if (clip.markers.FirstOrDefault(marker => marker.id == m_marker) is { } selected)
        {
            string name = selected.name; double time = selected.time;
            bool changed = m_fields.Text("clip/marker-name", m_fields.Localize("标记名称"), ref name);
            changed |= m_fields.Number("clip/marker-time", m_fields.Localize("标记逻辑时间"), ref time, 0, clip.duration);
            EditorUiSession.current.widgets.Hint(m_fields.Localize("剧情等待使用稳定标记 ID：") + selected.id);
            if (changed) m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("编辑演出标记"), candidate => PerformanceEditService.SetMarker(candidate, new() { id = selected.id, name = name, time = time }), m_fields.mergeKey);
            if (ImGui.Button(m_fields.Label("删除所选标记"))) { Finish(); m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("删除演出标记"), candidate => PerformanceEditService.RemoveMarker(candidate, selected.id)); m_marker = string.Empty; }
        }
        if (ImGui.Button(m_fields.Label("在当前时间添加标记")))
        {
            Finish(); string id = Guid.NewGuid().ToString("N"); double time = Snap(m_time, clip.duration);
            m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("添加演出标记"), candidate => PerformanceEditService.SetMarker(candidate, new() { id = id, name = "同步点", time = time })); m_marker = id;
        }
    }
    private void Range(PerformanceClipAsset clip)
    {
        if (clip.tracks.Length == 0) return;
        ImGui.SeparatorText(m_fields.Localize("关键帧范围复制"));
        m_fields.Number("clip/range-start", m_fields.Localize("范围开始"), ref m_rangeStart, 0, clip.duration); m_fields.Number("clip/range-end", m_fields.Localize("范围结束"), ref m_rangeEnd, 0, clip.duration);
        m_fields.Toggle("clip/range-all", "包含全部轨道（否则所选轨道）", ref m_allTracks); m_fields.Toggle("clip/range-replace", "允许覆盖相同目标时间的关键帧", ref m_replaceRange);
        if (ImGui.Button(m_fields.Label("复制范围到当前预览时间")))
        {
            Finish(); int[] tracks = m_allTracks ? Enumerable.Range(0, clip.tracks.Length).ToArray() : [m_track]; double destination = Snap(m_time, clip.duration);
            m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, m_fields.Localize("复制关键帧范围"), candidate => PerformanceEditService.CopyRange(candidate, tracks, m_rangeStart, m_rangeEnd, destination, m_replaceRange));
        }
    }
    private double Snap(
        double time,
        double duration
    ) => m_snap ? PerformanceEditService.Snap(time, m_gridStep, duration) : Math.Clamp(time, 0, duration);
    private void Finish() { m_fields.Finish(); m_curve.Finish(); m_canvas.Finish(); }
}
