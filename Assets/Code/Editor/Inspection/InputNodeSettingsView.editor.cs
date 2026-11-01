using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class InputNodeSettingsView(StoryInspectorFields fields)
{
    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        InputNode input = documents.codec.Read<InputNode>(node);
        string prompt = input.prompt, variable = input.variableId, initial = input.defaultText;
        bool changed = fields.Text(node.id + "/input/prompt", fields.Localize("玩家提示"), ref prompt); input.prompt = prompt;
        changed |= fields.Text(node.id + "/input/default", fields.Localize("默认文字（支持变量）"), ref initial); input.defaultText = initial;
        changed |= fields.Select(node.id + "/input/variable", fields.Localize("写入 String 变量"), ref variable, draft.GetVariables().Where(static item => item.initialValue.kind == NovelValueKind.String).Select(static item => (item.id, item.name))); input.variableId = variable;
        double minimum = input.minimumLength, maximum = input.maximumLength;
        changed |= fields.Number(node.id + "/input/min", fields.Localize("最少字素"), ref minimum, 0, 1024); input.minimumLength = (int)minimum;
        changed |= fields.Number(node.id + "/input/max", fields.Localize("最多字素"), ref maximum, 1, 1024); input.maximumLength = (int)maximum;
        bool cancel = input.allowCancel;
        changed |= fields.Toggle(node.id + "/input/cancel", "允许取消（需要 cancel 连接）", ref cancel); input.allowCancel = cancel;
        if (changed) documents.Edit(documents.activeDocumentId, fields.Localize("编辑玩家文字输入"), candidate => documents.edits.SetSettings(candidate, node.id, input), fields.mergeKey);
    }
}
