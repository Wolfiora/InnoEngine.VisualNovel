using System;
using System.Linq;
using InnoEditor.ImGui;
using InnoEditor.Core;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class ScreenStateView(StoryInspectorFields fields)
{
    private readonly NovelValueView m_values = new(fields);
    private bool m_clearReferences;

    internal bool Draw(ScreenAsset screen)
    {
        if (!ImGui.CollapsingHeader("Screen-local state")) return false;
        EditorUiSession.current.widgets.Hint("Screen fields belong only to the current screen role. Temporary fields reset when leaving the screen. Game fields restore with saves and rollback and remain separate from story variables and global unlocks.");
        bool changed = false;
        if (ImGui.Button("Add screen field"))
        {
            fields.Finish();
            UiStateEditService.Set(screen, new() { id = Guid.NewGuid().ToString("N"), name = "Screen field", scope = VariableScope.Local, initialValue = NovelValue.Boolean(false) });
            changed = true;
        }
        ImGui.Checkbox("Also clear references when deleting (explicit choice)", ref m_clearReferences);
        string remove = string.Empty;
        foreach (var original in screen.stateVariables.ToArray())
        {
            if (!ImGui.CollapsingHeader(original.name + " · " + original.initialValue.kind + "##page-state/" + original.id)) continue;
            var candidate = VariableValueRules.Copy(original); string prefix = "page-state/" + candidate.id;
            ImGui.Text("Stable identity · " + candidate.id);
            string name = candidate.name, lifetime = candidate.scope.ToString();
            bool edited = fields.Text(prefix + "/name", "Display name", ref name); candidate.name = name;
            string group = candidate.group, description = candidate.description;
            edited |= fields.Text(prefix + "/group", "Group", ref group); candidate.group = group;
            edited |= fields.MultilineText(prefix + "/description", "Purpose", ref description); candidate.description = description;
            edited |= fields.Select(prefix + "/scope", "Lifetime", ref lifetime, new[] { ("Local", "Current screen visit · Reset on leaving"), ("Game", "Game state · Save and rollback") });
            candidate.scope = Enum.Parse<VariableScope>(lifetime);
            NovelValue value = candidate.initialValue;
            edited |= m_values.Draw(prefix + "/initial", ref value, enumMembers: candidate.members); candidate.initialValue = value;
            bool enumeration = value.kind == NovelValueKind.Enum || value.kind == NovelValueKind.List && value.elementKind == NovelValueKind.Enum;
            if (enumeration)
            {
                if (candidate.members.Length == 0) candidate.members = [value.kind == NovelValueKind.Enum ? value.text : "Default"];
                string members = string.Join(", ", candidate.members);
                if (fields.Text(prefix + "/members", "Enum members (comma-separated)", ref members))
                { candidate.members = members.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); edited = true; }
            }
            if (edited) { UiStateEditService.Set(screen, candidate); changed = true; }
            ImGui.Text("Referencing control · " + UiStateEditService.References(screen).Count(item => item.fieldId == original.id));
            if (ImGui.Button("Remove field##" + prefix)) { fields.Finish(); remove = original.id; }
        }
        if (remove.Length > 0) { UiStateEditService.Remove(screen, remove, m_clearReferences); changed = true; }
        return changed;
    }
}
