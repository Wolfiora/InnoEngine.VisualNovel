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
        if (entry.kind != CollectionKind.Cg || !ImGui.CollapsingHeader("CG image variants##" + entry.id)) return false;
        EditorUiSession.current.widgets.Hint("The cover appears in the gallery list. When variants are present, details show only unlocked variants. Additional unlock requirements can refer to endings or other entries in the same library. Every image supports zooming and panning.");
        var image = NovelResourceSelection.Get<TextureAsset>(interactions);
        var ids = library.entries.Select(static item => item.id).ToHashSet(StringComparer.Ordinal);
        bool changed = false;
        ImGui.BeginDisabled(image is not { isMissing: false });
        try
        {
            if (ImGui.Button("Add selected image as a variant"))
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
            bool edited = fields.Text(original.id + "/title", "Variant name", ref title); candidate.title = title;
            edited |= fields.Select(original.id + "/unlock", "Additional unlock requirement", ref gate, new[] { ("", "Unlock with this entry") }.Concat(library.entries.Select(static item => (item.id, item.title + " · " + item.id)))); candidate.unlockEntryId = gate;
            ImGui.Text("Stable identity · " + candidate.id + " · Image · " + candidate.image?.name);
            if (image is { isMissing: false } && ImGui.Button("Replace with selected image##" + original.id)) { fields.Finish(); candidate.image = image; edited = true; }
            if (edited) { CollectionVariantEditService.Set(entry, candidate, ids); changed = true; }
            if (ImGui.Button("Move earlier##" + original.id)) { fields.Finish(); CollectionVariantEditService.Move(entry, original.id, -1); changed = true; }
            ImGui.SameLine();
            if (ImGui.Button("Move later##" + original.id)) { fields.Finish(); CollectionVariantEditService.Move(entry, original.id, 1); changed = true; }
            ImGui.SameLine(); if (ImGui.Button("Remove variant##" + original.id)) { fields.Finish(); remove = original.id; }
        }
        if (remove.Length > 0) { CollectionVariantEditService.Remove(entry, remove); changed = true; }
        return changed;
    }
}
