using System;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class UiDataFieldsView(StoryInspectorFields fields)
{
    private readonly NovelValueView m_values = new(fields);
    internal bool Draw(
        string owner,
        ref UiDataFieldDefinition[] definitions
    ) {
        bool changed = false; string remove = string.Empty;
        if (ImGui.Button(fields.Caption("添加字段##" + owner, fields.Localize("添加字段##") + owner)))
        { fields.Finish(); definitions = [.. definitions, new() { schema = new() { id = "f" + Guid.NewGuid().ToString("N"), label = "字段", defaultValue = NovelValue.String("") } }]; changed = true; }
        foreach (var field in definitions)
        {
            if (!ImGui.CollapsingHeader(field.schema.label + " · " + field.schema.id + "##" + owner)) continue;
            string prefix = owner + "/" + field.schema.id, id = field.schema.id, label = field.schema.label, help = field.schema.help;
            changed |= fields.Text(prefix + "/id", fields.Localize("字段 ID（单段）"), ref id); field.schema.id = id;
            changed |= fields.Text(prefix + "/label", fields.Localize("显示名"), ref label); field.schema.label = label;
            changed |= fields.Text(prefix + "/help", fields.Localize("说明"), ref help); field.schema.help = help;
            bool writable = field.writable; changed |= fields.Toggle(prefix + "/write", "允许控件写入", ref writable); field.writable = writable;
            NovelValue value = field.schema.defaultValue;
            changed |= m_values.Draw(prefix + "/type", ref value, enumMembers: field.schema.members); field.schema.defaultValue = value;
            if (value.kind == NovelValueKind.Enum || value.kind == NovelValueKind.List && value.elementKind == NovelValueKind.Enum)
            {
                if (field.schema.members.Length == 0) field.schema.members = [value.kind == NovelValueKind.Enum ? value.text : "Default"];
                string members = string.Join(", ", field.schema.members);
                if (fields.Text(prefix + "/members", fields.Localize("枚举成员（逗号分隔）"), ref members))
                { field.schema.members = members.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); changed = true; }
            }
            if (ImGui.Button(fields.Caption("删除字段##" + prefix, fields.Localize("删除字段##") + prefix))) { fields.Finish(); remove = id; changed = true; }
        }
        if (remove.Length > 0) definitions = definitions.Where(item => item.schema.id != remove).ToArray();
        return changed;
    }
}
