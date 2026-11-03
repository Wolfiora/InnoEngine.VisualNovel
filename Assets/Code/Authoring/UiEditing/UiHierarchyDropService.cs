using System;
using System.Linq;
using InnoEditor.Interactions;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Resolves before, after and into tree drops through the existing subtree ownership rules.</summary>
public static class UiHierarchyDropService
{
    /// <summary>Preflights a precise tree destination without changing the page, coordinates, bindings or selection.</summary>
    /// <param name="screen">The current detached native declaration.</param>
    /// <param name="sourceId">The exact existing subtree.</param>
    /// <param name="targetId">The hovered tree row.</param>
    /// <param name="placement">The native editor placement gesture.</param>
    /// <returns>A destination for the caller's guarded document transaction, with explicit context-change warnings.</returns>
    public static UiHierarchyDropTarget Describe(
        ScreenAsset screen,
        string sourceId,
        string targetId,
        EditorDropPlacement placement
    ) {
        var rows = ScreenEditService.Flatten(screen);
        var source = rows.SingleOrDefault(row => row.element.id == sourceId);
        var target = rows.SingleOrDefault(row => row.element.id == targetId);
        if (source.element is null || target.element is null || source.parent is null || sourceId == targetId)
            throw new AuthoringValidationException($"Drag an existing non-root element to a different target element.");
        string parent;
        int index;
        if (placement == EditorDropPlacement.Into)
        { parent = target.element.id; index = target.element.children.Count(child => child.id != sourceId); }
        else if (placement is EditorDropPlacement.Before or EditorDropPlacement.After && target.parent is not null)
        {
            parent = target.parent.id;
            var siblings = target.parent.children.Where(child => child.id != sourceId).ToArray();
            index = Array.FindIndex(siblings, child => child.id == targetId) + (placement == EditorDropPlacement.After ? 1 : 0);
        }
        else throw new AuthoringValidationException($"The root only accepts children. Choose before, after or inside for other targets.");
        if (!ScreenEditService.ReparentTargets(screen, sourceId).Contains(parent, StringComparer.Ordinal))
            throw new AuthoringValidationException($"The target cannot be the element itself, its descendant or a position beyond the page-depth limit.");
        if (parent == source.parent.id && index == Array.IndexOf(source.parent.children, source.element))
            throw new AuthoringValidationException($"The element is already at this position.");
        var byId = rows.ToDictionary(static row => row.element.id, StringComparer.Ordinal);
        string RowOwner(string? id)
        {
            while (id is not null)
            {
                var row = byId[id]; if (row.element.repeatBinding.Length > 0) return id;
                id = row.parent?.id;
            }
            return string.Empty;
        }
        return new(sourceId, parent, index, parent != source.parent.id, RowOwner(source.parent.id) != RowOwner(parent));
    }
}
