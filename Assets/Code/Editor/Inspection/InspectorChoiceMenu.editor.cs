using System;
using System.Collections.Generic;
using System.Linq;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class InspectorChoiceMenu
{
    private const int m_pageSize = 40;
    internal string search { get; set; } = string.Empty;
    internal int page { get; set; }
    internal int matches { get; private set; }
    internal int pages => Math.Max(1, (matches + m_pageSize - 1) / m_pageSize);
    internal IReadOnlyList<(string id, string name)> Read(IReadOnlyList<(string id, string name)> choices)
    {
        var filtered = search.Length == 0 ? choices : choices.Where(item => item.id.Contains(search, StringComparison.OrdinalIgnoreCase)
            || item.name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        matches = filtered.Count; page = Math.Clamp(page, 0, pages - 1);
        return filtered.Skip(page * m_pageSize).Take(m_pageSize).ToArray();
    }
}
