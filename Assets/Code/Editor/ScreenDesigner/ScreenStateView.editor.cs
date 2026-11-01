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
        if (!ImGui.CollapsingHeader(fields.Label("页面局部状态"))) return false;
        EditorUiSession.current.widgets.Hint(fields.Localize("“页面”字段只属于当前页面角色。临时字段离开页面即重置；游戏字段随存档与回滚恢复，和剧情变量/全局解锁分开。"));
        bool changed = false;
        if (ImGui.Button(fields.Label("添加页面字段")))
        {
            fields.Finish();
            UiStateEditService.Set(screen, new() { id = Guid.NewGuid().ToString("N"), name = "页面字段", scope = VariableScope.Local, initialValue = NovelValue.Boolean(false) });
            changed = true;
        }
        ImGui.Checkbox(fields.Label("删除时同时清除引用（显式选择）"), ref m_clearReferences);
        string remove = string.Empty;
        foreach (var original in screen.stateVariables.ToArray())
        {
            if (!ImGui.CollapsingHeader(original.name + " · " + original.initialValue.kind + "##page-state/" + original.id)) continue;
            var candidate = VariableValueRules.Copy(original); string prefix = "page-state/" + candidate.id;
            ImGui.Text(fields.Localize("稳定身份 · ") + candidate.id);
            string name = candidate.name, lifetime = candidate.scope.ToString();
            bool edited = fields.Text(prefix + "/name", fields.Localize("显示名"), ref name); candidate.name = name;
            string group = candidate.group, description = candidate.description;
            edited |= fields.Text(prefix + "/group", fields.Localize("分组"), ref group); candidate.group = group;
            edited |= fields.MultilineText(prefix + "/description", fields.Localize("用途说明"), ref description); candidate.description = description;
            edited |= fields.Select(prefix + "/scope", fields.Localize("生命周期"), ref lifetime, new[] { ("Local", fields.Localize("本次页面 · 离开后重置")), ("Game", fields.Localize("游戏状态 · 保存与回滚")) });
            candidate.scope = Enum.Parse<VariableScope>(lifetime);
            NovelValue value = candidate.initialValue;
            edited |= m_values.Draw(prefix + "/initial", ref value, enumMembers: candidate.members); candidate.initialValue = value;
            bool enumeration = value.kind == NovelValueKind.Enum || value.kind == NovelValueKind.List && value.elementKind == NovelValueKind.Enum;
            if (enumeration)
            {
                if (candidate.members.Length == 0) candidate.members = [value.kind == NovelValueKind.Enum ? value.text : "Default"];
                string members = string.Join(", ", candidate.members);
                if (fields.Text(prefix + "/members", fields.Localize("枚举成员（逗号分隔）"), ref members))
                { candidate.members = members.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); edited = true; }
            }
            if (edited) { UiStateEditService.Set(screen, candidate); changed = true; }
            ImGui.Text(fields.Localize("引用控件 · ") + UiStateEditService.References(screen).Count(item => item.fieldId == original.id));
            if (ImGui.Button(fields.Caption("删除字段##" + prefix, fields.Localize("删除字段##") + prefix))) { fields.Finish(); remove = original.id; }
        }
        if (remove.Length > 0) { UiStateEditService.Remove(screen, remove, m_clearReferences); changed = true; }
        return changed;
    }
}
