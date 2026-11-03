using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Graph;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Text;

namespace Inno.VisualNovel.Editor.ScriptEditor;

internal sealed class ScriptEditorView(
    StoryDocumentsModule documents,
    StoryInspectorFields fields,
    EditorInteractions interactions,
    NodeSettingsView properties,
    Func<GraphCanvasState> navigation
)
{
    private enum Mode { Cards, Compact, Proofread }
    private readonly ScriptViewportLayout m_viewport = new();
    private readonly ScriptCardDragView m_drag = new();
    private IReadOnlyList<ScriptRow> m_rows = [];
    private long m_revision = -1;
    private Guid m_document;
    private Mode m_mode, m_measuredMode;
    private float m_width;
    private string m_seenSelection = string.Empty, m_advanced = string.Empty;
    private bool m_locate;
    private StoryMovePlan? m_move;
    private string m_moveOwner = string.Empty, m_destination = string.Empty, m_moveError = string.Empty;
    private long m_moveRevision;
    private bool m_chooseDestination;
    private string m_rangeAnchor = string.Empty;
    private string m_focusDialogue = string.Empty;
    private GraphEdgeId? m_earlier, m_later;
    private string m_adjacentOwner = string.Empty;
    private long m_adjacentRevision = -1;

    internal void Reset()
    {
        m_document = Guid.Empty; m_revision = -1; m_rows = []; m_viewport.SetRows([], Estimate());
        m_seenSelection = m_advanced = string.Empty; m_locate = false;
        m_move = null; m_moveOwner = m_destination = m_moveError = string.Empty; m_chooseDestination = false;
        m_rangeAnchor = string.Empty;
        m_focusDialogue = string.Empty;
        m_adjacentRevision = -1; m_adjacentOwner = string.Empty; m_earlier = m_later = null;
        m_drag.Reset();
    }
    internal void Draw(
        StoryDraftState draft,
        string selected,
        Action<string> select,
        Action<string> selectGrouped,
        Func<bool>? clipboardKeys = null,
        bool hasExternalInspector = false
    ) {
        if (clipboardKeys?.Invoke() == true) return;
        long revision = documents.GetRevision(documents.activeDocumentId);
        if (m_document != documents.activeDocumentId) { Reset(); m_document = documents.activeDocumentId; }
        if (m_revision != revision)
        {
            m_rows = ScriptProjectionBuilder.Build(draft); m_viewport.SetRows(m_rows.Select(static row => row.nodeId), Estimate()); m_revision = revision;
        }
        Mode mode = m_mode;
        if (fields.SelectEnum("script/mode", "Script view · Cards / Compact / Proofread", ref mode)) { fields.Finish(); m_mode = mode; m_locate = true; }
        if (m_seenSelection != selected) { m_seenSelection = selected; m_rangeAnchor = selected; m_locate = true; }
        var input = ImGui.GetInput();
        if (m_mode != Mode.Proofread && selected.Length > 0 && ImGui.IsWindowFocused() && !input.wantsText && !ImGui.IsAnyItemActive()
            && input.alt && !input.control && !input.shift && !input.super
            && (ImGui.IsKeyPressed(ImGuiKey.UpArrow, true) || ImGui.IsKeyPressed(ImGuiKey.DownArrow, true)))
        {
            bool later = ImGui.IsKeyPressed(ImGuiKey.DownArrow, true);
            if (documents.reorder.Adjacent(draft, new(selected), later) is { } adjacent)
            { ProposeMove(draft, selected, adjacent, true); return; }
        }
        if (ImGui.Button("Locate selected node")) m_locate = true;
        ImGui.SameLine(); if (ImGui.Button("Create / edit instruction")) m_advanced = selected.Length > 0 ? selected : "new";
        ImGui.Text(m_rows.Count + " instructions · Segmented by actual connections · Only visible content is drawn; branches, merges and loops retain explicit targets.");
        if (m_advanced == "new" && !hasExternalInspector) properties.Draw(documents, draft, selected, select);
        float width = ImGui.GetContentRegionAvailable().X;
        if (m_measuredMode != m_mode || Math.Abs(m_width - width) > 4)
        { m_measuredMode = m_mode; m_width = width; m_viewport.ResetMeasurements(Estimate()); }
        float start = ImGui.GetCursorPosY();
        if (m_locate && selected.Length > 0 && m_viewport.OffsetOf(selected) is { } offset)
        {
            Space((float)offset); ImGui.Dummy(new(1, 1)); ImGui.SetScrollHereY(.2f); ImGui.SetCursorPosY(start); m_locate = false;
        }
        var range = m_viewport.Visible(ImGui.GetScrollY() - start, Math.Max(1, ImGui.GetWindowSize().Y), 400);
        Space((float)range.before);
        var catalog = new StoryNodeCatalog(documents.codec);
        for (int index = range.first; index < range.end; index++)
        {
            ScriptRow row = m_rows[index]; GraphNodeRecord? node = draft.graph.document.FindNode(new(row.nodeId));
            if (node is null) continue;
            float before = ImGui.GetCursorPosY();
            ImGui.PushId(row.nodeId);
            try
            {
                if (row.startsBlock) ImGui.SeparatorText((draft.fragments.FirstOrDefault(fragment => fragment.id == row.fragmentId)?.name ?? "No fragment") + " · Execution segment");
                string name = catalog.TryResolve(node.definitionId, out var definition) ? definition!.displayName : "Missing · " + node.definitionId;
                if (ImGui.Selectable(name + " · " + row.nodeId + "###vn-script-node/" + row.nodeId, navigation().selectedNodes.Contains(node.id)))
                {
                    fields.Finish(); bool rangeSelection = ImGui.GetInput().shift;
                    string primary = StoryNodeGroupSelection.Choose(navigation(), m_rows.Select(static item => item.nodeId).ToArray(), row.nodeId, m_rangeAnchor,
                        rangeSelection, ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl) || ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper));
                    if (!rangeSelection) m_rangeAnchor = row.nodeId;
                    m_seenSelection = primary; selectGrouped(primary);
                }
                if (m_mode != Mode.Proofread && m_drag.DrawRow(documents.activeDocumentId, revision, draft, m_rows, index, documents.reorder) is { } drop)
                { fields.Finish(); select(drop.node); ProposeMove(draft, drop.node, drop.destination, true); return; }
                if (!hasExternalInspector && m_advanced == row.nodeId && selected == row.nodeId && m_mode != Mode.Proofread)
                {
                    if (ImGui.SmallButton("Collapse full parameters")) { fields.Finish(); m_advanced = string.Empty; }
                    else properties.Draw(documents, draft, row.nodeId, select);
                }
                else if (node.definitionId == StoryNodeIds.dialogue) Dialogue(draft, node, select);
                else if (node.definitionId == StoryNodeIds.choice)
                {
                    ChoiceNode choice = documents.codec.Read<ChoiceNode>(node);
                    EditorUiSession.current.widgets.WrappedText(choice.prompt);
                    foreach (var option in choice.options) EditorUiSession.current.widgets.WrappedText("• " + option.text);
                }
                if (m_mode == Mode.Proofread)
                {
                    if (ImGui.SmallButton("Edit this segment")) { fields.Finish(); select(row.nodeId); m_mode = Mode.Cards; m_advanced = string.Empty; m_locate = true; }
                }
                else if (ImGui.SmallButton("Full parameters and connections")) { fields.Finish(); select(row.nodeId); m_advanced = row.nodeId; }
                if (selected == row.nodeId && m_mode != Mode.Proofread && Move(draft, node)) return;
                if (row.incoming > 1) ImGui.Text("Merge · " + row.incoming + " incoming paths");
                foreach (var edge in row.targets)
                {
                    bool exists = draft.graph.document.FindNode(edge.input.nodeId) is not null;
                    ImGui.BeginDisabled(!exists);
                    try { if (ImGui.Selectable("    " + edge.output.portId.value + " → " + edge.input.nodeId.value)) select(edge.input.nodeId.value); }
                    finally { ImGui.EndDisabled(); }
                    if (exists && m_mode != Mode.Proofread && m_drag.DrawConnection(documents.activeDocumentId, revision, m_rows, edge) is { } connectionDrop)
                    { fields.Finish(); select(connectionDrop.node); ProposeMove(draft, connectionDrop.node, connectionDrop.destination, true); return; }
                }
                ImGui.Separator();
            }
            finally { ImGui.PopId(); }
            m_viewport.Measure(row.nodeId, Math.Max(1, ImGui.GetCursorPosY() - before));
        }
        Space((float)Math.Max(0, m_viewport.height - m_viewport.EndOf(range.end)));
    }

    private void Dialogue(
        StoryDraftState draft,
        GraphNodeRecord node,
        Action<string> select
    ) {
        var line = documents.codec.Read<DialogueNode>(node);
        if (m_mode == Mode.Proofread)
        {
            string speaker = line.speaker.Length > 0 ? line.speaker : line.characterId.Length > 0 ? "Character · " + line.characterId : "Narration";
            ImGui.Text(speaker);
            string plain;
            try { plain = NovelTextMarkup.Parse(line.text).text; }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { plain = line.text + "\nFormatting diagnostics: " + (error).Message; }
            EditorUiSession.current.widgets.WrappedText(plain); return;
        }
        bool changed = false;
        if (m_mode == Mode.Cards)
        {
            string character = line.characterId, speaker = line.speaker, voice = line.voiceCueId;
            changed |= fields.Text(node.id + "/character", "Character", ref character); line.characterId = character;
            if (NovelResourceSelection.Get<CharacterAsset>(interactions) is { } selectedCharacter && ImGui.SmallButton("Link selected character · " + selectedCharacter.displayName))
            { fields.Finish(); line.characterId = selectedCharacter.resourceId; changed = true; }
            changed |= fields.Text(node.id + "/speaker", "Name override / narration", ref speaker); line.speaker = speaker;
            changed |= fields.Text(node.id + "/voice", "Voice cue", ref voice); line.voiceCueId = voice;
        }
        else ImGui.Text(line.speaker.Length > 0 ? line.speaker : line.characterId.Length > 0 ? line.characterId : "Narration");
        if (m_focusDialogue == node.id.value) { fields.FocusText(node.id + "/text/paragraph/0"); m_focusDialogue = string.Empty; }
        string text = line.text; changed |= fields.WritingText(node.id + "/text", "Body", ref text, out bool continueRequested); line.text = text;
        var outputs = draft.graph.document.edges.Where(edge => edge.output.nodeId == node.id).ToArray();
        bool canContinue = outputs.Length == 1 && outputs[0].output.portId.value == "next";
        if (continueRequested && canContinue)
        {
            fields.Finish(); string created = string.Empty;
            documents.Edit(documents.activeDocumentId, "Continue writing dialogue", candidate =>
            { if (changed) documents.edits.SetSettings(candidate, node.id, line); created = documents.edits.ContinueDialogue(candidate, node.id).value; });
            select(created); m_focusDialogue = created; m_locate = true; return;
        }
        if (changed)
        {
            select(node.id.value);
            documents.Edit(documents.activeDocumentId, "Edit script dialogue", candidate => documents.edits.SetSettings(candidate, node.id, line), fields.mergeKey);
        }
        if (canContinue && ImGui.SmallButton("Write the next line (same character / layout, Ctrl+Enter)"))
        {
            fields.Finish(); string created = string.Empty;
            documents.Edit(documents.activeDocumentId, "Continue writing dialogue", candidate => created = documents.edits.ContinueDialogue(candidate, node.id).value);
            select(created); m_focusDialogue = created; m_locate = true;
        }
    }
    private double Estimate() => m_mode switch { Mode.Cards => 320, Mode.Compact => 150, _ => 110 };
    private bool Move(
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        if (m_moveOwner != node.id.value)
        { m_moveOwner = node.id.value; m_move = null; m_chooseDestination = false; m_destination = m_moveError = string.Empty; }
        if (m_move is not null && m_moveRevision != documents.GetRevision(documents.activeDocumentId))
        { m_move = null; m_moveError = "The document changed. Preview the move again."; }
        long revision = m_revision;
        if (m_adjacentOwner != node.id.value || m_adjacentRevision != revision)
        {
            m_earlier = documents.reorder.Adjacent(draft, node.id, false); m_later = documents.reorder.Adjacent(draft, node.id, true);
            m_adjacentOwner = node.id.value; m_adjacentRevision = revision;
        }
        var earlier = m_earlier; var later = m_later;
        ImGui.BeginDisabled(earlier is null);
        try { if (ImGui.SmallButton("Move line up")) { ProposeMove(draft, node.id.value, earlier!.Value, true); return true; } }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); ImGui.BeginDisabled(later is null);
        try { if (ImGui.SmallButton("Move line down")) { ProposeMove(draft, node.id.value, later!.Value, true); return true; } }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); if (ImGui.SmallButton("Move to connection…")) { fields.Finish(); m_chooseDestination = !m_chooseDestination; }
        if (m_chooseDestination)
        {
            fields.Select(node.id + "/move-target", "Insert at connection", ref m_destination,
                new[] { (string.Empty, "Select target connection") }.Concat(draft.graph.document.edges.Where(edge => edge.output.nodeId != node.id && edge.input.nodeId != node.id)
                    .Select(static edge => (edge.id.value, edge.output.nodeId.value + " / " + edge.output.portId.value + " → " + edge.input.nodeId.value))));
            if (m_destination.Length > 0 && ImGui.SmallButton("Preview connection changes")) ProposeMove(draft, node.id.value, new(m_destination), false);
        }
        if (m_moveError.Length > 0) EditorUiSession.current.widgets.Hint(m_moveError);
        if (m_move is not { } plan) return false;
        EditorUiSession.current.widgets.Hint(plan.crossesControlBoundary ? "This operation crosses a control-flow or fragment boundary. Review the following three connection changes before applying them as one History operation." : "Reorder within one linear segment, preserving node identity, text, resources and graph position. Review before applying." );
        foreach (var change in plan.changes) EditorUiSession.current.widgets.WrappedText(
            change.beforeOutput.nodeId.value + " / " + change.beforeOutput.portId.value + " → " + change.beforeInput.nodeId.value + "   ⇒   "
            + change.afterOutput.nodeId.value + " / " + change.afterOutput.portId.value + " → " + change.afterInput.nodeId.value);
        if (ImGui.SmallButton(plan.crossesControlBoundary ? "Accept connections and move" : "Apply reorder"))
        {
            fields.Finish();
            try { documents.Edit(documents.activeDocumentId, "Move story instruction", candidate => documents.reorder.Apply(candidate, plan, true)); m_move = null; m_chooseDestination = false; m_locate = true; }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { m_moveError = (error).Message; m_move = null; }
            return true;
        }
        ImGui.SameLine(); if (ImGui.SmallButton("Cancel move")) { m_move = null; m_moveError = string.Empty; }
        return false;
    }
    private void ProposeMove(
        StoryDraftState draft,
        string source,
        GraphEdgeId destination,
        bool applyLinear
    ) {
        fields.Finish(); m_moveOwner = source; m_move = null; m_moveError = string.Empty;
        try
        {
            var plan = documents.reorder.Preview(draft, new(source), destination);
            if (applyLinear && !plan.crossesControlBoundary)
            {
                documents.Edit(documents.activeDocumentId, "Move story instruction", candidate => documents.reorder.Apply(candidate, plan));
                m_chooseDestination = false; m_locate = true;
            }
            else { m_move = plan; m_moveRevision = documents.GetRevision(documents.activeDocumentId); m_locate = true; }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { m_moveError = (error).Message; }
    }
    private static void Space(float height)
    {
        if (height <= 0) return;
        float start = ImGui.GetCursorPosY(); ImGui.Dummy(new Vector2(1, height)); ImGui.SetCursorPosY(start + height);
    }
}
