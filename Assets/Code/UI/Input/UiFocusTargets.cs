using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.UI.Input;

/// <summary>Finds authored static focus targets without confusing repeated templates or decorative underlays with live controls.</summary>
public static class UiFocusTargets
{
    /// <summary>Lists eligible declarations in paint order; runtime visibility and enabled conditions are checked when focus is applied.</summary>
    /// <param name="root">A bounded, uniquely owned page tree.</param>
    /// <returns>Static interactive controls outside repeated and behind-stage subtrees.</returns>
    public static IReadOnlyList<UiElementDefinition> Enumerate(UiElementDefinition root)
    {
        var result = new List<UiElementDefinition>(); var seen = new HashSet<UiElementDefinition>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(UiElementDefinition element, bool repeated, bool decorative, int depth)>(); pending.Push((root, false, false, 0));
        while (pending.TryPop(out var current))
        {
            if (current.depth > 64 || !seen.Add(current.element) || seen.Count > 10000) throw new ArgumentException("Focus declarations require a bounded uniquely owned tree.", nameof(root));
            bool decorative = current.decorative || current.element.behindStage;
            if (!current.repeated && !decorative && current.element.kind is UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Slider or UiElementKind.Input or UiElementKind.Selector or UiElementKind.ImageViewport)
                result.Add(current.element);
            for (int index = current.element.children.Length - 1; index >= 0; index--)
                pending.Push((current.element.children[index], current.repeated || current.element.repeatBinding.Length > 0, decorative, current.depth + 1));
        }
        return result.AsReadOnly();
    }
}
