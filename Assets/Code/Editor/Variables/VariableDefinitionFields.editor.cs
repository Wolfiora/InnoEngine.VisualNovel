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
        bool changed = fields.Text(variable.id + "/name", fields.Localize("显示名"), ref name); variable.name = name;
        string group = variable.group, description = variable.description;
        changed |= fields.Text(variable.id + "/group", fields.Localize("分组（不改变变量身份）"), ref group); variable.group = group;
        changed |= fields.MultilineText(variable.id + "/description", fields.Localize("用途说明"), ref description); variable.description = description;
        if (shared)
        {
            string scope = variable.scope.ToString();
            changed |= fields.Select(variable.id + "/scope", fields.Localize("作用域"), ref scope, new[] { ("Game", fields.Localize("当前游戏")), ("Global", fields.Localize("跨存档全局")) });
            variable.scope = Enum.Parse<VariableScope>(scope);
        }
        else
        {
            VariableScope scope = variable.scope; changed |= fields.SelectEnum(variable.id + "/scope", "作用域", ref scope); variable.scope = scope;
        }
        NovelValue value = variable.initialValue;
        changed |= m_values.Draw(variable.id + "/initial", ref value, true, variable.members);
        if (value.kind == NovelValueKind.Enum || value.kind == NovelValueKind.List && value.elementKind == NovelValueKind.Enum)
        {
            if (variable.members.Length == 0) variable.members = [value.kind == NovelValueKind.Enum ? value.text : "Default"];
            else if (value.kind == NovelValueKind.Enum && !variable.members.Contains(value.text, StringComparer.Ordinal)) value = NovelValue.Enum(variable.members[0]);
            string members = string.Join(", ", variable.members);
            if (fields.Text(variable.id + "/members", fields.Localize("枚举成员，以逗号分隔"), ref members))
            {
                string[] candidate = members.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
                if (candidate.Length > 0)
                {
                    variable.members = candidate;
                    if (value.kind == NovelValueKind.Enum && !candidate.Contains(value.text, StringComparer.Ordinal)) value = NovelValue.Enum(candidate[0]);
                    changed = true;
                }
            }
            EditorUiSession.current.widgets.Hint(value.kind == NovelValueKind.Enum ? fields.Localize("删除默认成员时，默认值会设置为第一个成员。") : fields.Localize("枚举列表的每一项必须属于声明成员；删除仍被使用的成员会产生诊断。"));
        }
        variable.initialValue = value;
        if (variable.scope == VariableScope.Global) EditorUiSession.current.widgets.Hint(fields.Localize("全局变量不会随普通读档或回滚撤销，请仅用于跨存档进度。"));
        return changed;
    }
}
