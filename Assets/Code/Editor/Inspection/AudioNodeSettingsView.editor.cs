using System;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Audio;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class AudioNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    internal void Draw(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        AudioNode request = documents.codec.Read<AudioNode>(node);
        string operation = request.operation.ToString();
        string slot = request.slot;
        string cue = request.cueId;
        bool changed = fields.Select(node.id + "/audio/op", "Operation", ref operation, Enum.GetNames<AudioOperation>().Select(static name => (name, name)));
        changed |= fields.Text(node.id + "/audio/slot", "Logical slot", ref slot);
        if (Enum.Parse<AudioOperation>(operation) == AudioOperation.Play) changed |= fields.Text(node.id + "/audio/cue", "Audio resource ID", ref cue);
        if (Enum.Parse<AudioOperation>(operation) == AudioOperation.Play && NovelResourceSelection.Get<AudioCueAsset>(interactions) is { } selected
            && ImGui.Button("Use selected audio: " + selected.title)) { fields.Finish(); cue = selected.resourceId; changed = true; }
        if (!changed) return;
        request.operation = Enum.Parse<AudioOperation>(operation); request.slot = slot; request.cueId = cue;
        documents.Edit(documents.activeDocumentId, "Edit audio", state => documents.edits.SetSettings(state, node.id, request), fields.mergeKey);
    }
}
