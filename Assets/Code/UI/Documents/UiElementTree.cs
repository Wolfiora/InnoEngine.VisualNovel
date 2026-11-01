using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.UI.Documents;

/// <summary>Traverses authored UI ownership consistently for runtime contracts and authoring operations.</summary>
public static class UiElementTree
{
    /// <summary>Lists every element, including decorative elements and repeated templates, in paint order.</summary>
    /// <param name="root">The bounded, uniquely owned declaration tree.</param>
    /// <returns>Stable elements, parents and depths; no runtime row instances are invented.</returns>
    public static IReadOnlyList<(UiElementDefinition element, UiElementDefinition? parent, int depth)> Enumerate(UiElementDefinition root)
    {
        var result = new List<(UiElementDefinition element, UiElementDefinition? parent, int depth)>();
        var pending = new Stack<(UiElementDefinition element, UiElementDefinition? parent, int depth)>();
        var seen = new HashSet<UiElementDefinition>(ReferenceEqualityComparer.Instance);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        pending.Push((root, null, 0));
        while (pending.TryPop(out var current))
        {
            if (current.element is null || current.depth > 64 || result.Count >= 10000 || !seen.Add(current.element)
                || string.IsNullOrWhiteSpace(current.element.id) || !ids.Add(current.element.id))
                throw new InvalidOperationException("A UI page must be a bounded tree with unique nonempty element identities.");
            result.Add(current);
            for (int index = current.element.children.Length - 1; index >= 0; index--) pending.Push((current.element.children[index], current.element, current.depth + 1));
        }
        return result.AsReadOnly();
    }
}
