using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Input;

internal static class UiScrollController
{
    internal static void Move(
        UiElementInstance element,
        float delta
    ) {
        float extent = element.followEnd ? element.contentVisibleHeight : element.contentHeight;
        float maximum = Math.Max(0, extent - element.bounds.height);
        element.scroll = Math.Clamp(element.scroll + delta, 0, maximum); element.followTail = element.scroll >= maximum - 1;
    }
    internal static bool Page(
        IReadOnlyList<UiElementInstance> order,
        string focus,
        bool backwards
    ) {
        UiElementInstance? selected = order.FirstOrDefault(item => item.id == focus), scroll = null;
        for (UiElementInstance? parent = selected; parent is not null; parent = parent.parent)
            if (Eligible(parent)) { scroll = parent; break; }
        scroll ??= order.FirstOrDefault(Eligible);
        if (scroll is null) return false;
        Move(scroll, (backwards ? -1 : 1) * Math.Max(1, scroll.bounds.height - scroll.definition.layout.padding * 2) * 0.9f);
        return true;
    }
    private static bool Eligible(UiElementInstance element) => element.visible && element.enabled && !element.behindStage
        && element.definition.kind == UiElementKind.Scroll && element.bounds.height > 0 && element.clip.width > 0 && element.clip.height > 0;
}
