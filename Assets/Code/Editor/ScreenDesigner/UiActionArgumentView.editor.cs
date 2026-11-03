using System;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Values;
using InnoEditor.ImGui;
using InnoEditor.Core;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class UiActionArgumentView(StoryInspectorFields fields)
{
    private readonly NovelValueView m_values = new(fields);
    internal bool Draw(
        string ownerId,
        UiActionDefinition definition,
        ref string literal
    ) {
        ImGui.Text(definition.argument.label + " · " + definition.argument.defaultValue.kind);
        if (definition.help.Length > 0) EditorUiSession.current.widgets.Hint(definition.help);
        if (definition.argument.help.Length > 0) EditorUiSession.current.widgets.Hint(definition.argument.help);
        if (definition.argument.defaultValue.kind == NovelValueKind.List)
        { EditorUiSession.current.widgets.Hint("Lists use the action's default value, or a typed list field selected as the argument."); return false; }
        NovelValue value;
        try { value = UiActionContract.ResolveArgument(definition, literal); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            EditorUiSession.current.widgets.Hint("Invalid argument: " + (error).Message);
            if (!ImGui.Button("Restore action's default argument##" + ownerId)) return false;
            fields.Finish(); literal = definition.argument.defaultValue.ToString(); return true;
        }
        if (!m_values.Draw(ownerId + "/typed-argument", ref value, false, definition.argument.members)) return false;
        literal = value.ToString(); return true;
    }
}
