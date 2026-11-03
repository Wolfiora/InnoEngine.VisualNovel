using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Applies element-tree operations independently of editor widgets and rendering.</summary>
public static class ScreenEditService
{
    /// <summary>Finds one element by stable identity in a bounded, uniquely owned tree.</summary>
    /// <param name="screen">The candidate screen.</param>
    /// <param name="id">The page-local identity.</param>
    /// <returns>The candidate element; missing identities throw.</returns>
    public static UiElementDefinition Find(
        ScreenAsset screen,
        string id
    ) => Flatten(screen).SingleOrDefault(item => item.element.id == id).element ?? throw new InvalidOperationException("The UI element is missing: " + id);
    /// <summary>Lists the bounded element tree in stable authored paint order.</summary>
    /// <param name="screen">The candidate screen.</param>
    /// <returns>Element, parent and depth without changing declarations.</returns>
    public static IReadOnlyList<(UiElementDefinition element, UiElementDefinition? parent, int depth)> Flatten(ScreenAsset screen)
        => UiElementTree.Enumerate(screen.root);
    /// <summary>Adds a usable control to an explicitly selected parent.</summary>
    /// <param name="screen">The candidate page.</param>
    /// <param name="parentId">The owning container.</param>
    /// <param name="kind">The control category.</param>
    /// <returns>The new stable element identity.</returns>
    public static string Add(
        ScreenAsset screen,
        string parentId,
        UiElementKind kind
    ) {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        UiElementDefinition parent = Find(screen, parentId);
        if (Flatten(screen).Count >= 9999) throw new InvalidOperationException("The page element budget was exceeded.");
        var element = new UiElementDefinition { id = Guid.NewGuid().ToString("N"), textId = Guid.NewGuid().ToString("N"), tooltipTextId = Guid.NewGuid().ToString("N"), kind = kind,
            text = kind is UiElementKind.Text or UiElementKind.Button ? "New control" : string.Empty,
            layout = new() { width = 280, height = kind is UiElementKind.Panel or UiElementKind.Scroll or UiElementKind.Horizontal or UiElementKind.Vertical or UiElementKind.Grid ? 240 : 56 } };
        parent.children = [.. parent.children, element]; return element.id;
    }
    /// <summary>Removes one subtree while keeping the mandatory page root.</summary>
    /// <param name="screen">The candidate page.</param>
    /// <param name="id">The subtree identity.</param>
    public static void Remove(
        ScreenAsset screen,
        string id
    ) {
        var row = Flatten(screen).Single(item => item.element.id == id);
        if (row.parent is null) throw new InvalidOperationException("The page root cannot be deleted.");
        if (Flatten(new ScreenAsset { root = row.element }).Any(item => item.element.id == screen.initialFocus)) screen.initialFocus = string.Empty;
        row.parent.children = row.parent.children.Where(item => item.id != id).ToArray();
    }
    /// <summary>Duplicates one subtree with fresh internal identities and independent declaration values.</summary>
    /// <param name="screen">The candidate page.</param>
    /// <param name="id">The subtree identity.</param>
    /// <returns>The copied root identity.</returns>
    public static string Duplicate(
        ScreenAsset screen,
        string id
    ) {
        var rows = Flatten(screen);
        var row = rows.Single(item => item.element.id == id);
        if (row.parent is null) throw new InvalidOperationException("Duplicate child elements instead of the page root.");
        var copy = UiDefinitionCopy.Copy(new ScreenAsset { root = row.element }).root;
        var descendants = Flatten(new ScreenAsset { root = copy });
        if (rows.Count + descendants.Count > 10000) throw new InvalidOperationException("The page element budget was exceeded.");
        foreach (var child in descendants)
        {
            child.element.id = Guid.NewGuid().ToString("N"); child.element.textId = Guid.NewGuid().ToString("N");
            child.element.tooltipTextId = Guid.NewGuid().ToString("N");
            foreach (var option in child.element.options) { option.id = Guid.NewGuid().ToString("N"); option.textId = Guid.NewGuid().ToString("N"); }
        }
        copy.layout.x += 24; copy.layout.y += 24;
        int index = Array.IndexOf(row.parent.children, row.element);
        row.parent.children = [.. row.parent.children[..(index + 1)], copy, .. row.parent.children[(index + 1)..]];
        return copy.id;
    }
    /// <summary>Moves an existing subtree under an explicit parent without changing its identity.</summary>
    /// <param name="screen">The candidate page.</param>
    /// <param name="id">The subtree identity.</param>
    /// <param name="parentId">The new parent identity.</param>
    /// <param name="index">The destination index after removing the existing child.</param>
    public static void Reparent(
        ScreenAsset screen,
        string id,
        string parentId,
        int index
    ) {
        var rows = Flatten(screen);
        var row = rows.Single(item => item.element.id == id);
        var target = rows.Single(item => item.element.id == parentId);
        UiElementDefinition destination = target.element;
        var subtree = Flatten(new ScreenAsset { root = row.element });
        if (row.parent is null || subtree.Any(item => ReferenceEquals(item.element, destination))) throw new InvalidOperationException("A UI subtree cannot own itself or replace the root.");
        if (target.depth + 1 + subtree.Max(static item => item.depth) > 64) throw new InvalidOperationException("Moving this UI subtree would exceed the page's 64-level ownership limit.");
        UiElementDefinition[] children = destination.children.Where(item => !ReferenceEquals(item, row.element)).ToArray();
        if (index < 0 || index > children.Length) throw new ArgumentOutOfRangeException(nameof(index));
        row.parent.children = row.parent.children.Where(item => !ReferenceEquals(item, row.element)).ToArray();
        destination.children = [.. children[..index], row.element, .. children[index..]];
    }

    /// <summary>Lists valid parent identities without guessing new layout or repeated-row bindings.</summary>
    /// <param name="screen">The current bounded declaration tree.</param>
    /// <param name="id">The child subtree to move; the page root has no eligible parent.</param>
    /// <returns>Stable authored order excluding descendants and destinations exceeding the depth budget.</returns>
    public static IReadOnlyList<string> ReparentTargets(
        ScreenAsset screen,
        string id
    ) {
        var rows = Flatten(screen); var source = rows.Single(item => item.element.id == id);
        if (source.parent is null) return [];
        var subtree = Flatten(new ScreenAsset { root = source.element });
        var descendants = subtree.Select(static item => item.element.id).ToHashSet(StringComparer.Ordinal);
        int depth = subtree.Max(static item => item.depth);
        return Array.AsReadOnly(rows.Where(item => !descendants.Contains(item.element.id) && item.depth + 1 + depth <= 64).Select(static item => item.element.id).ToArray());
    }
}
