using System;
using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class NovelValueView(StoryInspectorFields fields)
{
    internal bool Draw(
        string id,
        ref NovelValue value,
        bool chooseKind = true,
        string[]? enumMembers = null
    ) {
        bool changed = false;
        NovelValueKind kind = value.kind;
        if (chooseKind && fields.SelectEnum(id + "/kind", "类型 / Type", ref kind))
        {
            value = Default(kind); changed = true;
        }
        switch (value.kind)
        {
            case NovelValueKind.Boolean:
                bool flag = value.boolean;
                if (fields.Toggle(id, "值 / Value", ref flag)) { value = NovelValue.Boolean(flag); changed = true; }
                break;
            case NovelValueKind.Integer:
                long integer = value.integer;
                if (fields.Integer(id, "值 / Value", ref integer)) { value = NovelValue.Integer(integer); changed = true; }
                break;
            case NovelValueKind.Number:
                double number = value.number;
                if (fields.Number(id, fields.Localize("值 / Value"), ref number)) { value = NovelValue.Number(number); changed = true; }
                break;
            case NovelValueKind.String:
            case NovelValueKind.Enum:
                string text = value.text;
                bool edited = value.kind == NovelValueKind.Enum && enumMembers is { Length: > 0 }
                    ? fields.Select(id, fields.Localize("值 / Value"), ref text, enumMembers.Select(static item => (item, item)))
                    : fields.Text(id, fields.Localize("值 / Value"), ref text);
                if (edited && (value.kind != NovelValueKind.Enum || !string.IsNullOrWhiteSpace(text)))
                {
                    value = value.kind == NovelValueKind.Enum ? NovelValue.Enum(text) : NovelValue.String(text); changed = true;
                }
                break;
            case NovelValueKind.List:
                NovelValueKind elementKind = value.elementKind;
                string type = elementKind.ToString();
                if (chooseKind && fields.Select(id + "/element-kind", fields.Localize("元素类型"), ref type,
                    Enum.GetValues<NovelValueKind>().Where(static item => item != NovelValueKind.List).Select(static item => (item.ToString(), item.ToString()))))
                {
                    elementKind = Enum.Parse<NovelValueKind>(type);
                    value = NovelValue.List(elementKind, []); changed = true;
                }
                NovelValue[] items = value.items;
                int remove = -1;
                for (int index = 0; index < items.Length; index++)
                {
                    if (!ImGui.CollapsingHeader(fields.Caption($"第 {index + 1} 项##{id}/{index}", fields.Format("第 {0} 项##{1}/{2}", index + 1, id, index)))) continue;
                    NovelValue item = items[index];
                    if (Draw(id + "/" + index, ref item, false, enumMembers)) { items[index] = item; changed = true; }
                    if (ImGui.Button(fields.Caption("删除此项##" + id + "/" + index, fields.Localize("删除此项##") + id + "/" + index))) { fields.Finish(); remove = index; changed = true; }
                }
                if (remove >= 0) items = items.Where((_, index) => index != remove).ToArray();
                if (ImGui.Button(fields.Caption("添加列表项##" + id, fields.Localize("添加列表项##") + id)))
                {
                    fields.Finish(); items = [.. items, elementKind == NovelValueKind.Enum && enumMembers is { Length: > 0 } ? NovelValue.Enum(enumMembers[0]) : Default(elementKind)]; changed = true;
                }
                if (changed) value = NovelValue.List(elementKind, items);
                break;
        }
        return changed;
    }

    internal static NovelValue Default(NovelValueKind kind) => kind switch
    {
        NovelValueKind.Boolean => NovelValue.Boolean(false),
        NovelValueKind.Integer => NovelValue.Integer(0),
        NovelValueKind.Number => NovelValue.Number(0),
        NovelValueKind.String => NovelValue.String(string.Empty),
        NovelValueKind.Enum => NovelValue.Enum("Default"),
        NovelValueKind.List => NovelValue.List(NovelValueKind.String, []),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
