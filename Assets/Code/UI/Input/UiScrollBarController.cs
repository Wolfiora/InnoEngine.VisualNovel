using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Input;

internal sealed class UiScrollBarController
{
    private UiElementInstance? m_drag;
    private float m_offset;
    private bool m_thumb;

    internal UiDrawItem[] Paint(
        UiDrawItem[] draws,
        IReadOnlyList<UiElementInstance> order,
        bool highContrast
    ) {
        var after = new Dictionary<string, List<UiDrawItem>>(StringComparer.Ordinal);
        for (int index = 0; index < order.Count; index++)
        {
            UiElementInstance owner = order[index];
            if (!Metrics(owner, out Rect track, out Rect thumb, out _)) continue;
            int end = index;
            while (end + 1 < order.Count && DescendsFrom(order[end + 1], owner)) end++;
            while (end > index && !order[end].visible) end--;
            if (!after.TryGetValue(order[end].id, out var items)) after.Add(order[end].id, items = []);
            var style = owner.definition.style;
            items.Add(Item("track", track, highContrast ? 0x303030FFu : style.foreground & 0xFFFFFF00u | 0x30));
            items.Add(Item("thumb", thumb, highContrast ? 0xFFEB3BFFu : style.focus));
            UiDrawItem Item(
                string part,
                Rect bounds,
                uint color
            ) => new(owner.id + "/scrollbar/" + part, UiElementKind.Panel, bounds, owner.visualClip,
                color, 0, "", null, 1, 0, null, false, 0, opacity: owner.visualOpacity * style.opacity);
        }
        if (after.Count == 0) return draws;
        var result = new List<UiDrawItem>(draws.Length + after.Count * 2);
        foreach (var draw in draws)
        {
            result.Add(draw);
            if (after.TryGetValue(draw.id, out var bars)) result.AddRange(bars);
        }
        return result.ToArray();
    }

    internal bool Handle(
        UiInputSnapshot input,
        IReadOnlyList<UiElementInstance> order
    ) {
        if (m_drag is { } captured)
        {
            if (!order.Contains(captured) || !Metrics(captured, out Rect track, out Rect thumb, out float maximum))
            { Cancel(); return true; }
            if (m_thumb && (input.held || input.released))
            {
                float travel = track.height - thumb.height;
                float desired = travel <= 0 ? 0 : Math.Clamp((input.y - track.y - m_offset) / travel, 0, 1) * maximum;
                UiScrollController.Move(captured, desired - captured.scroll);
            }
            if (input.released || !input.held && !input.pressed) Cancel();
            return true;
        }
        if (!input.pressed || !input.pointerInside || input.composing) return false;
        for (int index = order.Count - 1; index >= 0; index--)
        {
            UiElementInstance owner = order[index];
            if (!Metrics(owner, out Rect track, out Rect thumb, out _) || !track.Contains(input.x, input.y) || !owner.visualClip.Contains(input.x, input.y)) continue;
            if (order.Skip(index + 1).Any(item => !DescendsFrom(item, owner) && item.inputVisible && item.enabled && item.visualOpacity > .001f && !item.behindStage
                && item.definition.blocksPointer && item.visualBounds.Contains(input.x, input.y) && item.visualClip.Contains(input.x, input.y))) return false;
            m_drag = owner;
            m_thumb = thumb.Contains(input.x, input.y);
            if (m_thumb) m_offset = input.y - thumb.y;
            else
            {
                UiScrollController.Move(owner, (input.y < thumb.y ? -1 : 1) * owner.bounds.height * .9f);
                m_offset = 0;
            }
            return true;
        }
        return false;
    }

    internal void Cancel() { m_drag = null; m_thumb = false; }

    private static bool Metrics(
        UiElementInstance owner,
        out Rect track,
        out Rect thumb,
        out float maximum
    ) {
        track = thumb = default;
        float extent = owner.followEnd ? owner.contentVisibleHeight : owner.contentHeight;
        maximum = Math.Max(0, extent - owner.bounds.height);
        if (!owner.visible || !owner.inputVisible || !owner.enabled || owner.behindStage || owner.visualOpacity <= .001f
            || owner.definition.kind != UiElementKind.Scroll || maximum <= 1 || owner.bounds.height <= 0
            || owner.visualBounds.width < 20 || owner.visualBounds.height < 28 || owner.visualClip.width <= 0 || owner.visualClip.height <= 0) return false;
        float scale = owner.visualBounds.height / owner.bounds.height;
        float inset = Math.Min(owner.visualBounds.height / 4, Math.Max(2, owner.definition.layout.padding * scale));
        track = new(owner.visualBounds.x + owner.visualBounds.width - 14, owner.visualBounds.y + inset, 12, owner.visualBounds.height - inset * 2);
        float length = Math.Clamp(track.height * owner.bounds.height / extent, Math.Min(24, track.height), track.height);
        thumb = new(track.x, track.y + Math.Clamp(owner.scroll / maximum, 0, 1) * (track.height - length), track.width, length);
        return true;
    }

    private static bool DescendsFrom(
        UiElementInstance item,
        UiElementInstance owner
    ) {
        for (UiElementInstance? parent = item.parent; parent is not null; parent = parent.parent)
            if (parent == owner) return true;
        return false;
    }
}
