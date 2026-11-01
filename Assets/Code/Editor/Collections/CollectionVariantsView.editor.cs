using System;
using System.Linq;
using InnoEngine.Rendering;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.CollectionEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Collections;

namespace Inno.VisualNovel.Editor.Collections;

internal sealed class CollectionVariantsView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    internal bool Draw(
        CollectionAsset library,
        CollectionEntry entry
    ) {
        if (entry.kind != CollectionKind.Cg || !ImGui.CollapsingHeader(fields.Caption("CG 多图变体##" + entry.id, fields.Localize("CG 多图变体##") + entry.id))) return false;
        EditorUiSession.current.widgets.Hint(fields.Localize("封面用于鉴赏列表。添加变体后，详情只浏览已解锁变体；附加解锁可引用同一收藏库的结局或其他条目。每张图都支持缩放与拖动。"));
        var image = NovelResourceSelection.Get<TextureAsset>(interactions);
        var ids = library.entries.Select(static item => item.id).ToHashSet(StringComparer.Ordinal);
        bool changed = false;
        ImGui.BeginDisabled(image is not { isMissing: false });
        try
        {
            if (ImGui.Button(fields.Label("添加所选图片为变体")))
            {
                fields.Finish(); CollectionVariantEditService.Set(entry, new() { id = Guid.NewGuid().ToString("N"), title = image!.name, image = image }, ids); changed = true;
            }
        }
        finally { ImGui.EndDisabled(); }
        string remove = string.Empty;
        foreach (var original in entry.variants.ToArray())
        {
            if (!ImGui.CollapsingHeader(original.title + "##variant/" + original.id)) continue;
            var candidate = original.Copy(); string title = candidate.title, gate = candidate.unlockEntryId;
            bool edited = fields.Text(original.id + "/title", fields.Localize("变体名称"), ref title); candidate.title = title;
            edited |= fields.Select(original.id + "/unlock", fields.Localize("附加解锁"), ref gate, new[] { ("", fields.Localize("随本条目解锁")) }.Concat(library.entries.Select(static item => (item.id, item.title + " · " + item.id)))); candidate.unlockEntryId = gate;
            ImGui.Text(fields.Localize("稳定身份 · ") + candidate.id + fields.Localize(" · 图像 · ") + candidate.image?.name);
            if (image is { isMissing: false } && ImGui.Button(fields.Caption("替换为所选图片##" + original.id, fields.Localize("替换为所选图片##") + original.id))) { fields.Finish(); candidate.image = image; edited = true; }
            if (edited) { CollectionVariantEditService.Set(entry, candidate, ids); changed = true; }
            if (ImGui.Button(fields.Caption("前移##" + original.id, fields.Localize("前移##") + original.id))) { fields.Finish(); CollectionVariantEditService.Move(entry, original.id, -1); changed = true; }
            ImGui.SameLine();
            if (ImGui.Button(fields.Caption("后移##" + original.id, fields.Localize("后移##") + original.id))) { fields.Finish(); CollectionVariantEditService.Move(entry, original.id, 1); changed = true; }
            ImGui.SameLine(); if (ImGui.Button(fields.Caption("移除变体##" + original.id, fields.Localize("移除变体##") + original.id))) { fields.Finish(); remove = original.id; }
        }
        if (remove.Length > 0) { CollectionVariantEditService.Remove(entry, remove); changed = true; }
        return changed;
    }
}
