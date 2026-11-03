using InnoEngine.Graphs;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class ImageEffectNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    internal void Screen(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        var request = documents.codec.Read<ScreenEffectNode>(node);
        var operation = request.operation; string instance = request.instanceId, resource = request.resourceId;
        bool changed = fields.SelectEnum(node.id + "/screen/op", "Operation", ref operation); request.operation = operation;
        changed |= fields.Text(node.id + "/screen/instance", "Effect instance ID", ref instance); request.instanceId = instance;
        if (operation is ScreenEffectOperation.Set or ScreenEffectOperation.Fade)
        {
            changed |= fields.Text(node.id + "/screen/resource", "Screen effect resource ID", ref resource); request.resourceId = resource;
            if (NovelResourceSelection.Get<VisualEffectAsset>(interactions) is { kind: VisualEffectKind.Screen } selected && ImGui.Button("Use selected effect: " + selected.displayName))
            { fields.Finish(); request.resourceId = selected.resourceId; changed = true; }
            double strength = request.strength, duration = request.duration, order = request.order; bool ui = request.includeUi, wait = request.waitForCompletion;
            changed |= fields.Number(node.id + "/screen/strength", "Intensity", ref strength, 0, 1); request.strength = strength;
            changed |= fields.Number(node.id + "/screen/order", "Compositing order", ref order, -1000000, 1000000); request.order = (int)order;
            changed |= fields.Toggle(node.id + "/screen/ui", "Include game UI", ref ui); request.includeUi = ui;
            if (operation == ScreenEffectOperation.Fade)
            {
                changed |= fields.Number(node.id + "/screen/duration", "Fade duration (seconds)", ref duration, 0, 3600); request.duration = duration;
                changed |= fields.Toggle(node.id + "/screen/wait", "Wait for fade completion", ref wait); request.waitForCompletion = wait;
            }
        }
        if (changed) documents.Edit(documents.activeDocumentId, "Edit screen effect", state => documents.edits.SetSettings(state, node.id, request), fields.mergeKey);
        EditorUiSession.current.widgets.Hint("An instance with the same ID replaces the previous effect. Control its lifecycle with wait, pause, resume, complete and remove. Flash-white effects are suppressed when flashing is disabled.");
    }

    internal void Transition(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        var request = documents.codec.Read<TransitionNode>(node); var operation = request.operation;
        bool changed = fields.SelectEnum(node.id + "/transition/op", "Operation", ref operation); request.operation = operation;
        if (operation == TransitionOperation.Begin)
        {
            string resource = request.resourceId, next = request.nextStage, target = request.target;
            var scope = request.scope; var group = request.group; bool wait = request.waitForCompletion;
            changed |= fields.Text(node.id + "/transition/resource", "Transition resource ID", ref resource); request.resourceId = resource;
            if (NovelResourceSelection.Get<VisualEffectAsset>(interactions) is { kind: VisualEffectKind.Transition } selected && ImGui.Button("Use selected transition: " + selected.displayName))
            { fields.Finish(); request.resourceId = selected.resourceId; changed = true; }
            changed |= fields.Text(node.id + "/transition/next", "Switch to stage (optional)", ref next); request.nextStage = next;
            if (NovelResourceSelection.Get<StageAsset>(interactions) is { } stage && ImGui.Button("Use selected target stage: " + stage.title))
            { fields.Finish(); request.nextStage = stage.resourceId; changed = true; }
            changed |= fields.SelectEnum(node.id + "/transition/scope", "Scope", ref scope); request.scope = scope;
            if (scope == TransitionScope.Group)
            {
                changed |= fields.SelectEnum(node.id + "/transition/group", "Stage group", ref group); request.group = group;
                changed |= fields.Text(node.id + "/transition/target", "Exact target (optional)", ref target); request.target = target;
            }
            changed |= fields.Toggle(node.id + "/transition/wait", "Wait for transition completion", ref wait); request.waitForCompletion = wait;
        }
        if (changed) documents.Edit(documents.activeDocumentId, "Edit transition", state => documents.edits.SetSettings(state, node.id, request), fields.mergeKey);
        EditorUiSession.current.widgets.Hint("The transition captures the previous image when it starts; subsequent stage operations form the new image. Stage retains live UI, while Output also captures the previous UI. Cancellation restores only the previous stage, not story or global side effects.");
    }
}
