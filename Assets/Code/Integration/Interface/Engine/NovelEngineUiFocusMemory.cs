using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Scene;
using InnoEngine.UI;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Integration.Interface.Engine;

/// <summary>Remembers engine-observed focus for live native pages; navigation and focus assignment remain with the engine document lifecycle.</summary>
internal sealed class NovelEngineUiFocusMemory
{
    private readonly Dictionary<NovelPageComponent, Entry> m_pages = [];

    internal GameObject? Read(NovelPageComponent page)
    {
        foreach (var stale in m_pages.Keys.Where(static item => item.isDestroyed || !item.gameObject.isRuntimeValid).ToArray()) m_pages.Remove(stale);
        if (!m_pages.TryGetValue(page, out var entry)) return null;
        if (!ReferenceEquals(entry.initial, page.initialFocus) || entry.focused.isDestroyed || !entry.focused.isRuntimeValid
            || !ReferenceEquals(Page(entry.focused), page))
        { m_pages.Remove(page); return null; }
        return entry.focused;
    }

    internal void Observe(
        UiEvent message,
        GameObject owner
    ) {
        if (message.type != UiEventType.Focus || owner.isDestroyed || !owner.isRuntimeValid) return;
        if (!owner.TryGetComponent<NovelUiButtonComponent>(out _) && !owner.TryGetComponent<NovelUiToggleComponent>(out _)) return;
        if (Page(owner) is { } page) m_pages[page] = new(page.initialFocus, owner);
    }

    internal void Clear() => m_pages.Clear();
    private static NovelPageComponent? Page(GameObject owner)
    {
        for (var parent = owner.transform.parent; parent is not null; parent = parent.parent)
        {
            if (parent.gameObject.TryGetComponent<NovelPageComponent>(out var page)) return page;
            if (parent.gameObject.TryGetComponent<NovelUiRootComponent>(out _)) return null;
        }
        return null;
    }
    private sealed record Entry(GameObject? initial, GameObject focused);
}
