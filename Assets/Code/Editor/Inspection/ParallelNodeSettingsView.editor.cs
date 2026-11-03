using System;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class ParallelNodeSettingsView(StoryInspectorFields fields)
{
    private string m_remove = string.Empty;
    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        if (node.definitionId == StoryNodeIds.join)
        {
            ImGui.Text("Owning fork: " + documents.codec.Read<JoinNode>(node).forkId);
            EditorUiSession.current.widgets.Hint("Branches end here. Their parent task continues through this node's next connection. A join cannot be an independent chapter entry."); return;
        }
        var parallel = documents.codec.Read<ParallelNode>(node); bool any = parallel.waitAny;
        if (fields.Toggle(node.id + "/any", "Cancel remaining tasks when any task completes", ref any))
        { parallel.waitAny = any; documents.Edit(documents.activeDocumentId, "Change parallel wait policy", state => documents.edits.SetSettings(state, node.id, parallel)); }
        ImGui.Text("Join: " + parallel.joinId);
        EditorUiSession.current.widgets.Hint("Branches run in list order without spawning threads. One branch may own dialogue, choice or input focus while others continue presentation. Branches may write different variables; competing variable writes or input focus cause errors. Place unlocks, screen changes and cross-chapter calls after the join. Cancellation retains writes already committed.");
        var edits = new ParallelEditService(documents.edits, documents.codec);
        if (parallel.branches.Length < 16 && ImGui.Button("Add parallel branch##" + node.id))
        { fields.Finish(); documents.Edit(documents.activeDocumentId, "Add parallel branch", state => edits.AddBranch(state, node.id)); return; }
        foreach (string branch in parallel.branches)
        {
            ImGui.Text("Branch: " + branch);
            int index = Array.IndexOf(parallel.branches, branch);
            if (index > 0 && ImGui.SmallButton("Execute earlier##" + node.id + "/" + branch))
            { fields.Finish(); documents.Edit(documents.activeDocumentId, "Change parallel execution order", state => edits.MoveBranch(state, node.id, branch, index - 1)); return; }
            if (index + 1 < parallel.branches.Length)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Execute later##" + node.id + "/" + branch))
                { fields.Finish(); documents.Edit(documents.activeDocumentId, "Change parallel execution order", state => edits.MoveBranch(state, node.id, branch, index + 1)); return; }
            }
            if (parallel.branches.Length <= 2) continue;
            if (ImGui.Button("Remove this branch…##" + node.id + "/" + branch)) { fields.Finish(); m_remove = branch; }
            if (m_remove != branch) continue;
            string[] members = edits.Members(draft, node.id, branch);
            EditorUiSession.current.widgets.Hint("This removes the branch and " + members.Length + " internal nodes. External references prevent removal; reconnect them first.");
            if (ImGui.Button("Remove branch and internal nodes##" + branch))
            { documents.Edit(documents.activeDocumentId, "Remove parallel branch", state => edits.RemoveBranch(state, node.id, branch)); m_remove = string.Empty; return; }
            ImGui.SameLine(); if (ImGui.Button("Cancel##" + branch)) m_remove = string.Empty;
        }
    }
}
