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
        bool changed = fields.Text(node.id + "/input/prompt", "Player prompt", ref prompt); input.prompt = prompt;
        changed |= fields.Text(node.id + "/input/default", "Default text (supports variables)", ref initial); input.defaultText = initial;
        changed |= fields.Select(node.id + "/input/variable", "Write to String variable", ref variable, draft.GetVariables().Where(static item => item.initialValue.kind == NovelValueKind.String).Select(static item => (item.id, item.name))); input.variableId = variable;
        double minimum = input.minimumLength, maximum = input.maximumLength;
        changed |= fields.Number(node.id + "/input/min", "Minimum graphemes", ref minimum, 0, 1024); input.minimumLength = (int)minimum;
        changed |= fields.Number(node.id + "/input/max", "Maximum graphemes", ref maximum, 1, 1024); input.maximumLength = (int)maximum;
        bool cancel = input.allowCancel;
        changed |= fields.Toggle(node.id + "/input/cancel", "Allow cancellation (requires a cancel connection)", ref cancel); input.allowCancel = cancel;
        if (changed) documents.Edit(documents.activeDocumentId, "Edit player text input", candidate => documents.edits.SetSettings(candidate, node.id, input), fields.mergeKey);
    }
}
