using System;
using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Variables;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class FlowNodeSettingsView(
    StoryInspectorFields fields,
    InnoEditor.Interactions.EditorInteractions interactions
)
{
    private readonly ExpressionEditorView m_expressions = new(fields);
    private readonly StageNodeSettingsView m_stage = new(fields, interactions);
    private readonly AudioNodeSettingsView m_audio = new(fields, interactions);
    private readonly PerformanceNodeSettingsView m_performance = new(fields, interactions);
    private readonly ImageEffectNodeSettingsView m_images = new(fields, interactions);
    private readonly InputNodeSettingsView m_input = new(fields);
    private readonly CommandNodeSettingsView m_commands = new(fields, interactions);
    private readonly ParallelNodeSettingsView m_parallel = new(fields);
    private readonly SystemNodeSettingsView m_system = new(fields, interactions);
    private string m_remove = string.Empty;

    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        string id = node.id.value;
        switch (node.definitionId)
        {
            case StoryNodeIds.log:
                var log = documents.codec.Read<Model.Story.Nodes.Debugging.LogNode>(node);
                string message = log.message; var level = log.level; bool previewOnly = log.previewOnly;
                bool logChanged = fields.MultilineText(id + "/log-message", "Log message (supports {variable-id})", ref message);
                logChanged |= fields.SelectEnum(id + "/log-level", "Log level", ref level);
                logChanged |= fields.Toggle(id + "/log-preview", "Preview output only", ref previewOnly);
                if (logChanged)
                {
                    log.message = message; log.level = level; log.previewOnly = previewOnly;
                    documents.Edit(documents.activeDocumentId, "Edit story log", candidate => documents.edits.SetSettings(candidate, node.id, log), fields.mergeKey);
                }
                EditorUiSession.current.widgets.Hint("Messages use the engine Console and include the original chapter and node IDs. Error severity only logs; use an assertion to stop execution. Replaying this instruction after rollback logs it again.");
                break;
            case StoryNodeIds.system: m_system.Draw(documents, node); break;
            case StoryNodeIds.parallel:
            case StoryNodeIds.join: m_parallel.Draw(documents, draft, node); break;
            case StoryNodeIds.command: m_commands.Draw(documents, draft, node); break;
            case StoryNodeIds.unlock:
                UnlockNode unlock = documents.codec.Read<UnlockNode>(node);
                string entryId = unlock.entryId;
                if (fields.Text(id + "/unlock", "Collection entry ID", ref entryId))
                {
                    unlock.entryId = entryId;
                    documents.Edit(documents.activeDocumentId, "Edit collection unlock", state => documents.edits.SetSettings(state, node.id, unlock), fields.mergeKey);
                }
                EditorUiSession.current.widgets.Hint("Unlocks belong to global progress and are retained after loading or rollback. Find entry IDs in VN Collections.");
                break;
            case StoryNodeIds.stage: m_stage.Draw(documents, draft, node); break;
            case StoryNodeIds.audio: m_audio.Draw(documents, node); break;
            case StoryNodeIds.performance: m_performance.Draw(documents, node); break;
            case StoryNodeIds.screenEffect: m_images.Screen(documents, node); break;
            case StoryNodeIds.transition: m_images.Transition(documents, node); break;
            case StoryNodeIds.input: m_input.Draw(documents, draft, node); break;
            case StoryNodeIds.wait:
                WaitNode wait = documents.codec.Read<WaitNode>(node);
                double seconds = wait.seconds; bool input = wait.waitForInput;
                bool waitChanged = fields.Toggle(id + "/input", "Wait for player advance", ref input);
                if (!input) waitChanged |= fields.Number(id + "/seconds", "Wait duration (seconds)", ref seconds, 0);
                if (waitChanged)
                {
                    wait.seconds = seconds; wait.waitForInput = input;
                    documents.Edit(documents.activeDocumentId, "Edit wait", state => documents.edits.SetSettings(state, node.id, wait), fields.mergeKey);
                }
                break;
            case StoryNodeIds.call:
                CallNode call = documents.codec.Read<CallNode>(node);
                string entry = call.entryId;
                string chapter = call.chapterId;
                bool changedCall = fields.Text(id + "/chapter", "Target chapter (empty for this chapter)", ref chapter);
                changedCall |= chapter.Length == 0 ? fields.Select(id + "/entry", "Called entry", ref entry, draft.entries.Select(static item => (item.id, item.id)))
                    : fields.Text(id + "/entry", "Public entry ID", ref entry);
                if (changedCall)
                {
                    call.entryId = entry; call.chapterId = chapter;
                    documents.Edit(documents.activeDocumentId, "Edit fragment call", state => documents.edits.SetSettings(state, node.id, call));
                }
                if (call.jump) EditorUiSession.current.widgets.Hint("This instruction jumps directly without adding a return address.");
                else if (ImGui.Button("Change to jump (remove return connection)##" + id))
                {
                    fields.Finish(); call.jump = true;
                    documents.Edit(documents.activeDocumentId, "Change to jump and remove return connection", state => documents.edits.SetSettings(state, node.id, call, true));
                }
                if (call.jump && ImGui.Button("Change to call (requires a next connection)##" + id))
                {
                    fields.Finish(); call.jump = false;
                    documents.Edit(documents.activeDocumentId, "Change to call", state => documents.edits.SetSettings(state, node.id, call));
                }
                break;
            case StoryNodeIds.condition:
            case StoryNodeIds.assertion:
                ConditionNode condition = documents.codec.Read<ConditionNode>(node);
                if (m_expressions.Draw(id + "/condition", condition.condition, draft.GetVariables()))
                    documents.Edit(documents.activeDocumentId, "Edit condition", state => documents.edits.SetSettings(state, node.id, condition), fields.mergeKey);
                break;
            case StoryNodeIds.assign:
                AssignmentNode assignment = documents.codec.Read<AssignmentNode>(node);
                string variable = assignment.variableId;
                bool changed = fields.Select(id + "/variable", "Write to variable", ref variable, draft.GetVariables().Select(static item => (item.id, item.name + " · " + item.id)));
                assignment.variableId = variable;
                changed |= m_expressions.Draw(id + "/value", assignment.value, draft.GetVariables());
                if (changed)
                    documents.Edit(documents.activeDocumentId, "Edit variable assignment", state => documents.edits.SetSettings(state, node.id, assignment), fields.mergeKey);
                break;
            case StoryNodeIds.random:
                Random(documents, draft, node);
                break;
            case StoryNodeIds.entry:
                EditorUiSession.current.widgets.Hint("The entry follows its next connection into the story. Register a public entry for this node in chapter structure.");
                break;
            case StoryNodeIds.end:
                EditorUiSession.current.widgets.Hint("End the current story session.");
                break;
            case StoryNodeIds.returnFromCall:
                EditorUiSession.current.widgets.Hint("Return through the caller's next connection and restore the caller's local variables.");
                break;
        }
    }

    private void Random(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        RandomNode random = documents.codec.Read<RandomNode>(node);
        bool changed = false;
        string remove = string.Empty;
        foreach (WeightedBranch branch in random.branches)
        {
            string id = node.id + "/" + branch.id;
            double weight = branch.weight;
            changed |= fields.Number(id + "/weight", "Weight " + branch.id, ref weight, double.Epsilon);
            branch.weight = weight;
            if (ImGui.Button("Remove branch…##" + id)) { fields.Finish(); m_remove = id; }
            if (m_remove != id) continue;
            string target = draft.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId.value == branch.id)?.input.nodeId.value ?? "No connection";
            EditorUiSession.current.widgets.Hint("Branch output to remove → " + target);
            if (ImGui.Button("Remove branch and its connection##" + id)) { fields.Finish(); remove = branch.id; m_remove = string.Empty; }
            ImGui.SameLine();
            if (ImGui.Button("Cancel##" + id)) m_remove = string.Empty;
        }
        if (remove.Length > 0) { random.branches = random.branches.Where(item => item.id != remove).ToArray(); changed = true; }
        if (ImGui.Button("Add random branch##" + node.id))
        {
            fields.Finish();
            random.branches = [.. random.branches, new() { id = Guid.NewGuid().ToString("N") }]; changed = true;
        }
        if (changed)
            documents.Edit(documents.activeDocumentId, "Edit random branch", state => documents.edits.SetSettings(state, node.id, random, remove.Length > 0), fields.mergeKey);
    }
}
