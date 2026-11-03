using System;
using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Performance;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Player.Interface.Performance;
using Inno.VisualNovel.Player.Effects;
using Inno.VisualNovel.Player.Sessions;
using System.Collections.Generic;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class PerformanceNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    private NovelUiPerformance? m_ui;
    private IReadOnlyDictionary<string, PerformanceProperty[]> m_effects = new Dictionary<string, PerformanceProperty[]>();
    internal void Draw(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        PerformanceNode request = documents.codec.Read<PerformanceNode>(node);
        PerformanceOperation operation = request.operation;
        string id = request.instanceId, clip = request.clipId;
        bool changed = fields.SelectEnum(node.id + "/performance/op", "Operation", ref operation);
        changed |= fields.Text(node.id + "/performance/id", "Performance instance ID", ref id);
        request.operation = operation; request.instanceId = id;
        if (operation == PerformanceOperation.Play)
        {
            if (NovelResourceSelection.Get<NovelProjectAsset>(interactions) is { } game && ImGui.Button("Use UI / effect targets from selected game"))
            { m_ui = new(game); m_effects = NovelEffectPerformance.Targets(new StoryProgramLibrary(game).program, game.screenEffects); }
            changed |= fields.Text(node.id + "/performance/clip", "Clip resource ID", ref clip); request.clipId = clip;
            if (NovelResourceSelection.Get<PerformanceClipAsset>(interactions) is { } selected && ImGui.Button("Use selected clip: " + selected.displayName))
            {
                fields.Finish(); request.clipId = selected.resourceId;
                request.bindings = selected.tracks.Select(static track => track.target).Distinct(StringComparer.Ordinal).Select(slot =>
                    new PerformanceBinding { slot = slot, target = slot == "camera" ? "camera" : string.Empty }).ToArray();
                changed = true;
            }
            int remove = -1;
            for (int index = 0; index < request.bindings.Length; index++)
            {
                PerformanceBinding binding = request.bindings[index];
                string slot = binding.slot, target = binding.target;
                changed |= fields.Text(node.id + "/slot/" + index, "Clip target slot", ref slot); binding.slot = slot;
                changed |= fields.Text(node.id + "/target/" + index, "Stage target ID", ref target); binding.target = target;
                if (m_ui is not null) { changed |= fields.Select(node.id + "/ui-target/" + index, "Choose UI target", ref target, m_ui.targets); binding.target = target; }
                if (m_effects.Count > 0) { changed |= fields.Select(node.id + "/effect-target/" + index, "Choose effect instance", ref target, m_effects.Keys.Select(static item => (item, item))); binding.target = target; }
                if (ImGui.Button("Remove this binding##" + node.id + "/" + index)) { fields.Finish(); remove = index; }
            }
            if (remove >= 0) { request.bindings = request.bindings.Where((_, index) => index != remove).ToArray(); changed = true; }
            if (ImGui.Button("Add target binding##" + node.id))
            { fields.Finish(); request.bindings = [.. request.bindings, new() { slot = "actor", target = string.Empty }]; changed = true; }
            EditorUiSession.current.widgets.Hint("Stage targets use camera, character/instance-name or stage/layer-ID; Volume binds audio/slot. Choose UI targets from the selected game as ui/screen/element, supporting X, Y, ScaleX (uniform scale) and Opacity. Effect parameters bind effect/instance, with channels limited by the preset mechanism. The UI element must exist in every theme for that screen role. Position is an offset. Scale, opacity, volume and effect intensity are multipliers; other effect parameters use the track value.");
        }
        if (operation == PerformanceOperation.Seek)
        {
            double time = request.time;
            changed |= fields.Number(node.id + "/time", "Logical time (seconds)", ref time, 0); request.time = time;
        }
        if (operation == PerformanceOperation.WaitMarker)
        {
            string marker = request.markerId; changed |= fields.Text(node.id + "/marker", "Marker ID to wait for on first arrival", ref marker); request.markerId = marker;
            if (NovelResourceSelection.Get<PerformanceClipAsset>(interactions) is { } selected)
            {
                changed |= fields.Select(node.id + "/marker-choice", "Choose marker from selected clip", ref marker, selected.markers.Select(static item => (item.id, item.name)));
                request.markerId = marker;
            }
            EditorUiSession.current.widgets.Hint("Wait for this instance's first arrival at the marker. Cancelled instances end the wait immediately. Scrubbing and seeking only evaluate presentation, without running scripts or unlocking content. A looping instance does not wait for the same marker in a later cycle.");
        }
        if (changed) documents.Edit(documents.activeDocumentId, "Edit performance clip request", state => documents.edits.SetSettings(state, node.id, request), fields.mergeKey);
    }
}
