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
            if (ImGui.Button("Apply command form:" + definition.title + "##" + definition.id))
            {
                fields.Finish(); command.definition = CommandContract.Copy(definition);
                command.arguments = command.arguments.Where(argument => definition.parameters.Any(parameter => parameter.id == argument.id)).ToArray();
                documents.Edit(documents.activeDocumentId, "Apply custom command form", state => documents.edits.SetSettings(state, node.id, command)); return;
            }
        if (command.definition.id.Length == 0)
        { EditorUiSession.current.widgets.Hint("Select an imported .ivncommands catalog or a game, then apply a command from it. Extension authors register implementations. Nodes do not execute arbitrary C# text."); return; }
        ImGui.Text(command.definition.title + " · " + command.definition.id);
        if (command.definition.help.Length > 0) EditorUiSession.current.widgets.Hint(command.definition.help);
        ImGui.Text("Lifecycle: " + command.definition.capabilities);
        bool changed = false;
        foreach (var parameter in command.definition.parameters)
        {
            ImGui.Text(parameter.label + " · " + parameter.id);
            var argument = command.arguments.SingleOrDefault(item => item.id == parameter.id);
            if (argument is null)
            {
                ImGui.Text("Default: " + parameter.defaultValue);
                if (ImGui.Button("Override this parameter##" + node.id + "/" + parameter.id))
                { fields.Finish(); command.arguments = [.. command.arguments, new() { id = parameter.id, value = new() { literal = parameter.defaultValue.Copy() } }]; changed = true; }
            }
            else
            {
                changed |= m_expressions.Draw(node.id + "/parameter/" + parameter.id, argument.value, draft.GetVariables());
                if (ImGui.Button("Restore parameter default##" + node.id + "/" + parameter.id))
                { fields.Finish(); command.arguments = command.arguments.Where(item => item.id != parameter.id).ToArray(); changed = true; }
            }
            if (parameter.help.Length > 0) EditorUiSession.current.widgets.Hint(parameter.help);
        }
        if (command.definition.hasResult)
        {
            string destination = command.resultVariable;
            changed |= fields.Select(node.id + "/result", "Write result to", ref destination, new[] { (string.Empty, "Ignore result") }.Concat(draft.GetVariables()
                .Where(variable => variable.initialValue.kind == command.definition.resultDefault.kind).Select(static variable => (variable.id, variable.name + " · " + variable.id))));
            command.resultVariable = destination;
        }
        if (changed) documents.Edit(documents.activeDocumentId, "Edit custom command parameters", state => documents.edits.SetSettings(state, node.id, command), fields.mergeKey);
        EditorUiSession.current.widgets.Hint("Missing implementations retain their forms and parameters. Saving is disabled while a command that cannot be saved is active. Commands that cannot roll back create a rollback barrier.");
    }
}
