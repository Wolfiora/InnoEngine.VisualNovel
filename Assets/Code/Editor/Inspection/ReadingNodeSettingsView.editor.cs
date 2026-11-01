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
        bool changed = fields.Text(node.id + "/character", fields.Localize("角色资源 ID（留空为旁白或独立姓名）"), ref character);
        if (NovelResourceSelection.Get<CharacterAsset>(interactions) is { } selectedCharacter && ImGui.Button(fields.Caption("关联角色：" + selectedCharacter.displayName + (selectedCharacter.aliases.Length == 0 ? "" : " · " + string.Join(" / ", selectedCharacter.aliases)), fields.Localize("关联角色：") + selectedCharacter.displayName + (selectedCharacter.aliases.Length == 0 ? "" : " · " + string.Join(" / ", selectedCharacter.aliases)))))
        { fields.Finish(); character = selectedCharacter.resourceId; changed = true; }
        changed |= fields.Text(node.id + "/speaker", fields.Localize("姓名覆盖（留空使用角色本地化姓名）"), ref speaker);
        changed |= fields.MultilineText(node.id + "/text", fields.Localize("正文 / Text"), ref text);
        changed |= m_formatting.Draw(node.id + "/text", ref text);
        changed |= fields.Text(node.id + "/voice", fields.Localize("语音提示资源 ID（可留空）"), ref voice);
        if (NovelResourceSelection.Get<AudioCueAsset>(interactions) is { channel: NovelAudioChannel.Voice, loop: false } selected
            && ImGui.Button(fields.Caption("绑定所选语音：" + selected.title, fields.Localize("绑定所选语音：") + selected.title)))
        { fields.Finish(); voice = selected.resourceId; changed = true; }
        changed |= fields.Number(node.id + "/speed", fields.Localize("每秒字素数"), ref speed, 0.01, 100000);
        changed |= fields.Number(node.id + "/punctuation", fields.Localize("句末标点停顿秒数"), ref punctuation, 0, 10);
        changed |= fields.SelectEnum(node.id + "/presentation", "阅读布局", ref presentation);
        if (presentation == DialoguePresentation.Nvl) changed |= fields.Toggle(node.id + "/clear-page", "从新的一页开始", ref clear);
        if (presentation is DialoguePresentation.Bubble or DialoguePresentation.Overlay)
        {
            changed |= fields.Number(node.id + "/anchor-x", fields.Localize("横向位置比例"), ref anchorX, 0, 1);
            changed |= fields.Number(node.id + "/anchor-y", fields.Localize("纵向位置比例"), ref anchorY, 0, 1);
            changed |= fields.Number(node.id + "/relative-width", fields.Localize("宽度比例"), ref width, 0.1, 1);
        }
        ImGui.Text(fields.Localize("文本 ID · ") + line.textId);
        if (!changed) return;
        line.characterId = character; line.speaker = speaker; line.text = text; line.voiceCueId = voice; line.charactersPerSecond = speed; line.punctuationPause = punctuation;
        line.presentation = presentation; line.clearPage = clear; line.anchorX = anchorX; line.anchorY = anchorY; line.relativeWidth = width;
        documents.Edit(documents.activeDocumentId, fields.Localize("编辑对白"), state => documents.edits.SetSettings(state, node.id, line), fields.mergeKey);
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
        bool changed = fields.Text(prefix + "/prompt", fields.Localize("提示 / Prompt"), ref prompt);
        changed |= fields.Number(prefix + "/timeout", fields.Localize("超时秒数，0 为不限"), ref timeout, 0);
        changed |= fields.Select(prefix + "/default", fields.Localize("超时默认选项"), ref defaultOption,
            new[] { (string.Empty, fields.Localize("未分配")) }.Concat(choice.options.Select(static item => (item.id, item.text))));
        choice.prompt = prompt; choice.timeout = timeout; choice.defaultOption = defaultOption;
        string remove = string.Empty;
        foreach (ChoiceOption option in choice.options)
        {
            string id = prefix + "/" + option.id;
            if (!ImGui.CollapsingHeader(option.text + "##" + id, ImGuiTreeNodeFlags.DefaultOpen)) continue;
            string text = option.text;
            string reason = option.disabledReason;
            changed |= fields.Text(id + "/text", fields.Localize("选项文字"), ref text);
            option.text = text;
            changed |= fields.Text(id + "/reason", fields.Localize("禁用说明"), ref reason);
            option.disabledReason = reason;
            if (ImGui.CollapsingHeader(fields.Caption("显示条件##" + id, fields.Localize("显示条件##") + id))) changed |= m_expressions.Draw(id + "/visible", option.visible, draft.GetVariables());
            if (ImGui.CollapsingHeader(fields.Caption("启用条件##" + id, fields.Localize("启用条件##") + id))) changed |= m_expressions.Draw(id + "/enabled", option.enabled, draft.GetVariables());
            if (ImGui.Button(fields.Caption("删除此选项…##" + id, fields.Localize("删除此选项…##") + id))) { fields.Finish(); m_remove = id; }
            if (m_remove != id) continue;
            string target = draft.graph.document.edges.FirstOrDefault(edge => edge.output.nodeId == node.id && edge.output.portId.value == option.id)?.input.nodeId.value ?? "无连接";
            EditorUiSession.current.widgets.Hint(fields.Localize("将删除此选项和输出连接 → ") + target + fields.Localize("。其他路径保持原样。"));
            if (ImGui.Button(fields.Caption("确认删除选项及其连接##" + id, fields.Localize("确认删除选项及其连接##") + id))) { fields.Finish(); remove = option.id; m_remove = string.Empty; }
            ImGui.SameLine();
            if (ImGui.Button(fields.Caption("取消##" + id, fields.Localize("取消##") + id))) m_remove = string.Empty;
        }
        if (remove.Length > 0)
        {
            choice.options = choice.options.Where(item => item.id != remove).ToArray();
            if (choice.defaultOption == remove) choice.defaultOption = string.Empty;
            changed = true;
        }
        if (ImGui.Button(fields.Caption("添加选项##" + prefix, fields.Localize("添加选项##") + prefix)))
        {
            fields.Finish();
            choice.options = [.. choice.options, new() { id = Guid.NewGuid().ToString("N"), text = "新的选项" }];
            changed = true;
        }
        if (changed)
            documents.Edit(documents.activeDocumentId, fields.Localize("编辑剧情选项"), state => documents.edits.SetSettings(state, node.id, choice, remove.Length > 0), fields.mergeKey);
    }
}
