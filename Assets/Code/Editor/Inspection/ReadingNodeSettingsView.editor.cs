using System;
using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using InnoEditor.Interactions;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class ReadingNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    private readonly ExpressionEditorView m_expressions = new(fields);
    private readonly TextFormattingView m_formatting = new(fields);
    private string m_remove = string.Empty;

    internal void Dialogue(
        StoryDocumentsModule documents,
        GraphNodeRecord node
    ) {
        DialogueNode line = documents.codec.Read<DialogueNode>(node);
        string speaker = line.speaker, character = line.characterId;
        string text = line.text;
        string voice = line.voiceCueId;
        double speed = line.charactersPerSecond;
        double punctuation = line.punctuationPause;
        DialoguePresentation presentation = line.presentation; bool clear = line.clearPage;
        double anchorX = line.anchorX, anchorY = line.anchorY, width = line.relativeWidth;
        bool changed = fields.Text(node.id + "/character", "Character resource ID (empty for narration or a separate name)", ref character);
        if (NovelResourceSelection.Get<CharacterAsset>(interactions) is { } selectedCharacter && ImGui.Button("Linked character: " + selectedCharacter.displayName + (selectedCharacter.aliases.Length == 0 ? "" : " · " + string.Join(" / ", selectedCharacter.aliases))))
        { fields.Finish(); character = selectedCharacter.resourceId; changed = true; }
        changed |= fields.Text(node.id + "/speaker", "Name override (empty for the character's localized name)", ref speaker);
        changed |= fields.MultilineText(node.id + "/text", "Dialogue text", ref text);
        changed |= m_formatting.Draw(node.id + "/text", ref text);
        changed |= fields.Text(node.id + "/voice", "Voice cue resource ID (optional)", ref voice);
        if (NovelResourceSelection.Get<AudioCueAsset>(interactions) is { channel: NovelAudioChannel.Voice, loop: false } selected
            && ImGui.Button("Link selected voice: " + selected.title))
        { fields.Finish(); voice = selected.resourceId; changed = true; }
        changed |= fields.Number(node.id + "/speed", "Graphemes per second", ref speed, 0.01, 100000);
        changed |= fields.Number(node.id + "/punctuation", "Sentence-ending punctuation pause (seconds)", ref punctuation, 0, 10);
        changed |= fields.SelectEnum(node.id + "/presentation", "Reading layout", ref presentation);
        if (presentation == DialoguePresentation.Nvl) changed |= fields.Toggle(node.id + "/clear-page", "Start a new page", ref clear);
        if (presentation is DialoguePresentation.Bubble or DialoguePresentation.Overlay)
        {
            changed |= fields.Number(node.id + "/anchor-x", "Normalized horizontal position", ref anchorX, 0, 1);
            changed |= fields.Number(node.id + "/anchor-y", "Normalized vertical position", ref anchorY, 0, 1);
            changed |= fields.Number(node.id + "/relative-width", "Normalized width", ref width, 0.1, 1);
        }
        ImGui.Text("Text ID · " + line.textId);
        if (!changed) return;
        line.characterId = character; line.speaker = speaker; line.text = text; line.voiceCueId = voice; line.charactersPerSecond = speed; line.punctuationPause = punctuation;
        line.presentation = presentation; line.clearPage = clear; line.anchorX = anchorX; line.anchorY = anchorY; line.relativeWidth = width;
        documents.Edit(documents.activeDocumentId, "Edit dialogue", state => documents.edits.SetSettings(state, node.id, line), fields.mergeKey);
    }

    internal void Choice(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        ChoiceNode choice = documents.codec.Read<ChoiceNode>(node);
        string prefix = node.id.value;
        string prompt = choice.prompt;
        double timeout = choice.timeout;
        string defaultOption = choice.defaultOption;
        bool changed = fields.Text(prefix + "/prompt", "Prompt", ref prompt);
        changed |= fields.Number(prefix + "/timeout", "Timeout (seconds; 0 for unlimited)", ref timeout, 0);
        changed |= fields.Select(prefix + "/default", "Default choice on timeout", ref defaultOption,
            new[] { (string.Empty, "Unassigned") }.Concat(choice.options.Select(static item => (item.id, item.text))));
        choice.prompt = prompt; choice.timeout = timeout; choice.defaultOption = defaultOption;
        string remove = string.Empty;
        foreach (ChoiceOption option in choice.options)
        {
            string id = prefix + "/" + option.id;
            if (!ImGui.CollapsingHeader(option.text + "##" + id, ImGuiTreeNodeFlags.DefaultOpen)) continue;
            string text = option.text;
            string reason = option.disabledReason;
            changed |= fields.Text(id + "/text", "Choice text", ref text);
            option.text = text;
            changed |= fields.Text(id + "/reason", "Disabled reason", ref reason);
            option.disabledReason = reason;
            if (ImGui.CollapsingHeader("Visibility condition##" + id)) changed |= m_expressions.Draw(id + "/visible", option.visible, draft.GetVariables());
            if (ImGui.CollapsingHeader("Enabled condition##" + id)) changed |= m_expressions.Draw(id + "/enabled", option.enabled, draft.GetVariables());
            if (ImGui.Button("Remove this choice…##" + id)) { fields.Finish(); m_remove = id; }
            if (m_remove != id) continue;
            string target = draft.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId.value == option.id)?.input.nodeId.value ?? "No connection";
            EditorUiSession.current.widgets.Hint("This removes the choice and its output connection → " + target + ". Other paths retain their existing connections.");
            if (ImGui.Button("Remove choice and its connection##" + id)) { fields.Finish(); remove = option.id; m_remove = string.Empty; }
            ImGui.SameLine();
            if (ImGui.Button("Cancel##" + id)) m_remove = string.Empty;
        }
        if (remove.Length > 0)
        {
            choice.options = choice.options.Where(item => item.id != remove).ToArray();
            if (choice.defaultOption == remove) choice.defaultOption = string.Empty;
            changed = true;
        }
        if (ImGui.Button("Add choice##" + prefix))
        {
            fields.Finish();
            choice.options = [.. choice.options, new() { id = Guid.NewGuid().ToString("N"), text = "New choice" }];
            changed = true;
        }
        if (changed)
            documents.Edit(documents.activeDocumentId, "Edit story choices", state => documents.edits.SetSettings(state, node.id, choice, remove.Length > 0), fields.mergeKey);
    }
}
