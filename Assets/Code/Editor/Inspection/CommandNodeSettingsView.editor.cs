using System.Linq;
using InnoEngine.Graphs;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class CommandNodeSettingsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    private readonly ExpressionEditorView m_expressions = new(fields);
    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        GraphNodeRecord node
    ) {
        CommandNode command = documents.codec.Read<CommandNode>(node);
        var definitions = NovelResourceSelection.Get<NovelCommandCatalogAsset>(interactions)?.definitions
            ?? NovelResourceSelection.Get<NovelProjectAsset>(interactions)?.commandDefinitions ?? [];
        foreach (var definition in definitions)
            if (ImGui.Button(fields.Caption("应用指令表单：" + definition.title + "##" + definition.id, fields.Localize("应用指令表单：") + definition.title + "##" + definition.id)))
            {
                fields.Finish(); command.definition = CommandContract.Copy(definition);
                command.arguments = command.arguments.Where(argument => definition.parameters.Any(parameter => parameter.id == argument.id)).ToArray();
                documents.Edit(documents.activeDocumentId, fields.Localize("应用自定义指令表单"), state => documents.edits.SetSettings(state, node.id, command)); return;
            }
        if (command.definition.id.Length == 0)
        { EditorUiSession.current.widgets.Hint(fields.Localize("选择已导入的 .ivncommands 目录或游戏，再应用其中的指令。实现由扩展作者注册；节点不执行任意 C# 文本。")); return; }
        ImGui.Text(command.definition.title + " · " + command.definition.id);
        if (command.definition.help.Length > 0) EditorUiSession.current.widgets.Hint(command.definition.help);
        ImGui.Text(fields.Localize("生命周期：") + command.definition.capabilities);
        bool changed = false;
        foreach (var parameter in command.definition.parameters)
        {
            ImGui.Text(parameter.label + " · " + parameter.id);
            var argument = command.arguments.SingleOrDefault(item => item.id == parameter.id);
            if (argument is null)
            {
                ImGui.Text(fields.Localize("默认值：") + parameter.defaultValue);
                if (ImGui.Button(fields.Caption("自定义此参数##" + node.id + "/" + parameter.id, fields.Localize("自定义此参数##") + node.id + "/" + parameter.id)))
                { fields.Finish(); command.arguments = [.. command.arguments, new() { id = parameter.id, value = new() { literal = parameter.defaultValue.Copy() } }]; changed = true; }
            }
            else
            {
                changed |= m_expressions.Draw(node.id + "/parameter/" + parameter.id, argument.value, draft.GetVariables());
                if (ImGui.Button(fields.Caption("恢复参数默认值##" + node.id + "/" + parameter.id, fields.Localize("恢复参数默认值##") + node.id + "/" + parameter.id)))
                { fields.Finish(); command.arguments = command.arguments.Where(item => item.id != parameter.id).ToArray(); changed = true; }
            }
            if (parameter.help.Length > 0) EditorUiSession.current.widgets.Hint(parameter.help);
        }
        if (command.definition.hasResult)
        {
            string destination = command.resultVariable;
            changed |= fields.Select(node.id + "/result", fields.Localize("返回值写入"), ref destination, new[] { (string.Empty, fields.Localize("忽略返回值")) }.Concat(draft.GetVariables()
                .Where(variable => variable.initialValue.kind == command.definition.resultDefault.kind).Select(static variable => (variable.id, variable.name + " · " + variable.id))));
            command.resultVariable = destination;
        }
        if (changed) documents.Edit(documents.activeDocumentId, fields.Localize("编辑自定义指令参数"), state => documents.edits.SetSettings(state, node.id, command), fields.mergeKey);
        EditorUiSession.current.widgets.Hint(fields.Localize("缺失实现时保留全部表单和参数。不可保存的活动指令会禁用存档；不可回滚的指令建立屏障。"));
    }
}
