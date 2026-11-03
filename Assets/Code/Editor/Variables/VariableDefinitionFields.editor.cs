using System;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Variables;

internal sealed class VariableDefinitionFields(StoryInspectorFields fields)
{
    private readonly NovelValueView m_values = new(fields);
    internal bool Draw(
        VariableDefinition variable,
        bool shared = false
    ) {
        string name = variable.name;
        bool changed = fields.Text(variable.id + "/name", "Display name", ref name); variable.name = name;
        string group = variable.group, description = variable.description;
        changed |= fields.Text(variable.id + "/group", "Group (does not change variable identity)", ref group); variable.group = group;
        changed |= fields.MultilineText(variable.id + "/description", "Purpose", ref description); variable.description = description;
        if (shared)
        {
            string scope = variable.scope.ToString();
            changed |= fields.Select(variable.id + "/scope", "Scope", ref scope, new[] { ("Game", "Current game"), ("Global", "Global across saves") });
            variable.scope = Enum.Parse<VariableScope>(scope);
        }
        else
        {
            VariableScope scope = variable.scope; changed |= fields.SelectEnum(variable.id + "/scope", "Scope", ref scope); variable.scope = scope;
        }
        NovelValue value = variable.initialValue;
        changed |= m_values.Draw(variable.id + "/initial", ref value, true, variable.members);
        if (value.kind == NovelValueKind.Enum || value.kind == NovelValueKind.List && value.elementKind == NovelValueKind.Enum)
        {
            if (variable.members.Length == 0) variable.members = [value.kind == NovelValueKind.Enum ? value.text : "Default"];
            else if (value.kind == NovelValueKind.Enum && !variable.members.Contains(value.text, StringComparer.Ordinal)) value = NovelValue.Enum(variable.members[0]);
            string members = string.Join(", ", variable.members);
            if (fields.Text(variable.id + "/members", "Enum members, comma separated", ref members))
            {
                string[] candidate = members.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
                if (candidate.Length > 0)
                {
                    variable.members = candidate;
                    if (value.kind == NovelValueKind.Enum && !candidate.Contains(value.text, StringComparer.Ordinal)) value = NovelValue.Enum(candidate[0]);
                    changed = true;
                }
            }
            EditorUiSession.current.widgets.Hint(value.kind == NovelValueKind.Enum ? "Removing the default member sets the default to the first member." : "Every enum-list item must be a declared member; removing a member still in use produces a diagnostic.");
        }
        variable.initialValue = value;
        if (variable.scope == VariableScope.Global) EditorUiSession.current.widgets.Hint("Global variables are not undone by loading ordinary saves or rolling back. Use them only for progress shared across saves.");
        return changed;
    }
}
