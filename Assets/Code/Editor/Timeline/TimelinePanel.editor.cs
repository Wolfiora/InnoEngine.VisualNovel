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
[EditorPanel("inno.vn.timeline", "VN Performance Timeline", order: 455, defaultOpen: false, menuPath: "Authoring")]
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
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = (error).Message; }
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
        if (!m_toolbar.Draw("Performance clip", ".ivnclip", static () => new PerformanceClipAsset { resourceId = "performance-" + Guid.NewGuid().ToString("N") }, out PerformanceClipAsset? clip) || clip is null) return;
        string name = clip.displayName, resource = clip.resourceId;
        bool changed = m_fields.Text("clip/name", "Clip name", ref name); clip.displayName = name;
        changed |= m_fields.Text("clip/resource", "Story resource ID", ref resource); clip.resourceId = resource;
        double duration = clip.duration;
        changed |= m_fields.Number("clip/duration", "Clip duration (seconds)", ref duration, 0.001, 36000);
        if (duration < clip.tracks.SelectMany(static item => item.keys).Select(static key => key.time).Concat(clip.markers.Select(static marker => marker.time)).DefaultIfEmpty(0).Max()) throw new InvalidOperationException("Move or delete keyframes and markers beyond the new duration first.");
        clip.duration = duration;
        bool loop = clip.loop;
        changed |= m_fields.Toggle("clip/loop", "Loop", ref loop); clip.loop = loop;
        bool essential = clip.essential;
        changed |= m_fields.Toggle("clip/essential", "Essential performance retained with reduced motion", ref essential); clip.essential = essential;
        if (changed) m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Edit performance clip", candidate =>
        { candidate.displayName = clip.displayName; candidate.resourceId = clip.resourceId; candidate.duration = clip.duration; candidate.loop = clip.loop; candidate.essential = clip.essential; }, m_fields.mergeKey);
        m_fields.Text("clip/new-target", "New track target slot", ref m_slot);
        m_fields.SelectEnum("clip/new-property", "New track property", ref m_property);
        if (ImGui.Button("Add property track"))
        { Finish(); m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Add performance track", candidate => PerformanceEditService.AddTrack(candidate, m_slot, m_property)); }
        double time = Math.Min(m_time, clip.duration);
        m_fields.Number("clip/preview-time", "Preview time (logical seconds)", ref time, 0, clip.duration); m_time = time;
        m_fields.Toggle("clip/snap", "Snap when dragging and writing", ref m_snap);
        m_fields.Number("clip/grid", "Snap interval (seconds)", ref m_gridStep, .000001, 36000);
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
            ImGui.Text("Volume preview · " + sample.target + " × " + sample.value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        if (clip.tracks.Any(static track => track.property == PerformanceProperty.Volume))
            EditorUiSession.current.widgets.Hint("This only evaluates volume values. Actual sound and mixed performance use the same clip in story preview; scrubbing does not start or replay audio.");
        if (NovelResourceSelection.Get<StageAsset>(m_interactions) is { } selected && ImGui.Button("Use selected stage as explicit preview state")) m_stage = StageAssetCopy.Copy(selected);
        if (NovelResourceSelection.Get<ScreenAsset>(m_interactions) is { } page && ImGui.Button("Use selected screen for UI preview")) m_uiPage = UiDefinitionCopy.Copy(page);
        if (NovelResourceSelection.Get<VisualEffectAsset>(m_interactions) is { kind: VisualEffectKind.Screen } effect && ImGui.Button("Use selected screen effect for parameter preview")) m_effect = VisualEffectCopy.Copy(effect);
        if (m_effect is not null)
        {
            m_fields.Text("preview/effect-instance", "Effect instance", ref m_effectInstance);
            if (string.IsNullOrWhiteSpace(m_effectInstance)) { EditorUiSession.current.widgets.Hint("Specify a nonempty effect instance."); return; }
            if (ImGui.SmallButton("Close effect preview")) m_effect = null;
        }
        if (m_uiPage is not null)
        {
            m_fields.Text("preview/ui-role", "Screen role", ref m_uiRole);
            if (!Model.Story.Nodes.Interface.SystemNodeRules.IsPageRole(m_uiRole)) { EditorUiSession.current.widgets.Hint("The screen role must be a valid game-shell role."); return; }
            if (ImGui.SmallButton("Close screen preview")) m_uiPage = null;
        }
        if (m_stage is null && m_uiPage is null) { EditorUiSession.current.widgets.Hint("Select a stage or UI screen as explicit preview state; scrubbing only evaluates the timeline and does not execute game actions."); return; }
        var ui = new NovelUiPerformance(new() { screens = m_uiPage is null ? [] : [new() { role = m_uiRole, screen = m_uiPage }] });
        var uiChannels = ui.GetChannels();
        string effectTarget = "effect/" + m_effectInstance;
        var effectChannels = m_effect is null ? [] : ScreenEffectChannels.For(m_effect.screen.kind);
        foreach (string slot in clip.tracks.Select(static item => item.target).Distinct(StringComparer.Ordinal))
        {
            bool volume = clip.tracks.Any(track => track.target == slot && track.property == PerformanceProperty.Volume);
            string target = m_targets.GetValueOrDefault(slot) ?? (volume ? slot.StartsWith("audio/", StringComparison.Ordinal) ? slot : "audio/" + slot
                : slot == "camera" ? "camera" : uiChannels.ContainsKey(slot) ? slot : m_effect is not null && slot == effectTarget ? effectTarget : string.Empty);
            if (volume) m_fields.Text("preview/" + slot, "Volume-only preview target " + slot, ref target);
            else m_fields.Select("preview/" + slot, "Preview binding " + slot, ref target, (m_stage?.layers.Where(_ => clip.tracks.Where(track => track.target == slot).All(track => StagePerformanceController.SupportsStageProperty(track.property)))
                    .Select(static layer => ("stage/" + layer.id, layer.name)) ?? [])
                .Concat(m_stage is null || !clip.tracks.Where(track => track.target == slot).All(track => StagePerformanceController.SupportsStageProperty(track.property, true)) ? [] : new[] { ("camera", "Camera") })
                .Concat(ui.targets.Where(item => clip.tracks.Where(track => track.target == slot).All(track => uiChannels[item.id].Contains(track.property))))
                .Concat(m_effect is not null && clip.tracks.Where(track => track.target == slot).All(track => effectChannels.Contains(track.property)) ? new[] { (effectTarget, "Screen effect · " + m_effectInstance) } : []));
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
            bool changed = m_fields.Number("clip/key-value", "Keyframe value", ref value, bounds.minimum, bounds.maximum);
            changed |= m_fields.SelectEnum("clip/key-interpolation", "Interpolation", ref interpolation);
            PerformanceCurve curve = selected.curve.Copy();
            if (interpolation == PerformanceInterpolation.CubicBezier) changed |= m_curve.Draw(curve); else m_curve.Finish();
            if (changed) m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Edit keyframe", candidate => PerformanceEditService.SetKey(candidate, m_track, m_keyTime, value, interpolation, curve), m_curve.mergeKey);
        }
        if (ImGui.Button("Write keyframe at current preview time"))
        {
            Finish(); double target = Snap(m_time, clip.duration), value = new PerformancePlayer(clip).Evaluate(target)[m_track].value;
            m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Write keyframe", candidate => PerformanceEditService.SetKey(candidate, m_track, target, value, PerformanceInterpolation.Linear)); m_keyTime = m_time = target;
        }
        if (track.keys.Length > 1 && ImGui.Button("Delete selected keyframe"))
        { Finish(); m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Delete keyframe", candidate => candidate.tracks[m_track].keys = candidate.tracks[m_track].keys.Where(key => key.time != m_keyTime).ToArray()); }
    }
    private void Markers(PerformanceClipAsset clip)
    {
        ImGui.SeparatorText("Synchronization markers");
        foreach (var marker in clip.markers)
            if (ImGui.Selectable($"{marker.name} · {marker.time:0.###}s##marker-{marker.id}", m_marker == marker.id)) { Finish(); m_marker = marker.id; m_time = marker.time; }
        if (clip.markers.FirstOrDefault(marker => marker.id == m_marker) is { } selected)
        {
            string name = selected.name; double time = selected.time;
            bool changed = m_fields.Text("clip/marker-name", "Marker name", ref name);
            changed |= m_fields.Number("clip/marker-time", "Marker logical time", ref time, 0, clip.duration);
            EditorUiSession.current.widgets.Hint("Story waits use the stable marker ID: " + selected.id);
            if (changed) m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Edit performance marker", candidate => PerformanceEditService.SetMarker(candidate, new() { id = selected.id, name = name, time = time }), m_fields.mergeKey);
            if (ImGui.Button("Delete selected marker")) { Finish(); m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Delete performance marker", candidate => PerformanceEditService.RemoveMarker(candidate, selected.id)); m_marker = string.Empty; }
        }
        if (ImGui.Button("Add marker at current time"))
        {
            Finish(); string id = Guid.NewGuid().ToString("N"); double time = Snap(m_time, clip.duration);
            m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Add performance marker", candidate => PerformanceEditService.SetMarker(candidate, new() { id = id, name = "Sync point", time = time })); m_marker = id;
        }
    }
    private void Range(PerformanceClipAsset clip)
    {
        if (clip.tracks.Length == 0) return;
        ImGui.SeparatorText("Copy keyframe range");
        m_fields.Number("clip/range-start", "Range start", ref m_rangeStart, 0, clip.duration); m_fields.Number("clip/range-end", "Range end", ref m_rangeEnd, 0, clip.duration);
        m_fields.Toggle("clip/range-all", "Include all tracks (otherwise selected track)", ref m_allTracks); m_fields.Toggle("clip/range-replace", "Allow overwriting keyframes at matching target times", ref m_replaceRange);
        if (ImGui.Button("Copy range to current preview time"))
        {
            Finish(); int[] tracks = m_allTracks ? Enumerable.Range(0, clip.tracks.Length).ToArray() : [m_track]; double destination = Snap(m_time, clip.duration);
            m_documents.Edit<PerformanceClipAsset>(m_toolbar.documentId, "Copy keyframe range", candidate => PerformanceEditService.CopyRange(candidate, tracks, m_rangeStart, m_rangeEnd, destination, m_replaceRange));
        }
    }
    private double Snap(
        double time,
        double duration
    ) => m_snap ? PerformanceEditService.Snap(time, m_gridStep, duration) : Math.Clamp(time, 0, duration);
    private void Finish() { m_fields.Finish(); m_curve.Finish(); m_canvas.Finish(); }
}
