using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.UI.Input;

internal sealed class UiFocusController
{
    private bool m_reveal;
    internal string id { get; private set; } = string.Empty;
    internal void EnsureValid(IReadOnlyList<UiElementInstance> order)
    {
        if (order.Any(item => item.id == id && Eligible(item))) return;
        id = order.FirstOrDefault(Eligible)?.id ?? string.Empty; m_reveal = true;
    }
    internal bool Focus(
        string target,
        IReadOnlyList<UiElementInstance> order
    ) {
        if (!order.Any(item => item.id == target && Eligible(item))) return false;
        id = target; m_reveal = true; return true;
    }
    internal bool Navigate(
        bool previous,
        IReadOnlyList<UiElementInstance> order
    ) {
        UiElementInstance[] items = order.Where(Eligible).ToArray();
        if (items.Length == 0) return false;
        int current = Array.FindIndex(items, item => item.id == id);
        int index = current < 0 ? previous ? items.Length - 1 : 0 : (current + (previous ? -1 : 1) + items.Length) % items.Length;
        id = items[index].id; m_reveal = true; return true;
    }
    internal void RequestReveal() => m_reveal = true;
    internal void Reveal(
        IReadOnlyList<UiElementInstance> order,
        UiElementInstance root,
        float width,
        float height
    ) {
        if (!m_reveal) return;
        m_reveal = false;
        UiElementInstance? selected = order.FirstOrDefault(item => item.id == id);
        if (selected is null) return;
        for (UiElementInstance? parent = selected.parent; parent is not null; parent = parent.parent)
        {
            if (parent.definition.kind != UiElementKind.Scroll) continue;
            float top = parent.bounds.y + parent.definition.layout.padding;
            float bottom = parent.bounds.y + parent.bounds.height - parent.definition.layout.padding;
            float delta = selected.bounds.y < top ? selected.bounds.y - top : selected.bounds.y + selected.bounds.height > bottom ? selected.bounds.y + selected.bounds.height - bottom : 0;
            float scroll = Math.Clamp(parent.scroll + delta, 0, Math.Max(0, parent.contentHeight - parent.bounds.height));
            if (scroll == parent.scroll) continue;
            parent.scroll = scroll;
            UiLayoutPass.Arrange(root, new(0, 0, width, height), new(0, 0, width, height));
        }
    }
    internal static bool Eligible(UiElementInstance item)
    {
        if (!item.visible || !item.inputVisible || !item.enabled || item.behindStage || item.bounds.width <= 0 || item.bounds.height <= 0
            || item.definition.kind is not (UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Slider or UiElementKind.Input or UiElementKind.Selector or UiElementKind.ImageViewport)) return false;
        if (item.clip.width > 0 && item.clip.height > 0) return true;
        for (UiElementInstance? parent = item.parent; parent is not null; parent = parent.parent)
            if (parent.definition.kind == UiElementKind.Scroll && parent.clip.width > 0 && parent.clip.height > 0) return true;
        return false;
    }
}
