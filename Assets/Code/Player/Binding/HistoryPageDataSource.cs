using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Player.Binding;

/// <summary>Projects bounded backlog pages with original voice indices and exact retained rollback positions.</summary>
public sealed class HistoryPageDataSource(NovelSession session) : UiDataSource
{
    private const int PAGE_SIZE = 100;
    private int m_page;
    private bool m_latest = true;
    private int Last => Math.Max(0, (session.historyCount - 1) / PAGE_SIZE);
    private int Current => m_page = m_latest ? Last : Math.Clamp(m_page, 0, Last);
    /// <summary>Selects the latest backlog page when opening history.</summary>
    public void Open() { m_latest = true; m_page = Last; }
    /// <summary>Moves one or more bounded pages without changing original history row identities.</summary>
    /// <param name="delta">Signed page distance.</param>
    public void Move(int delta)
    {
        m_page = (int)Math.Clamp((long)Current + delta, 0, Last); m_latest = m_page == Last;
    }
    /// <inheritdoc />
    public override bool TryRead(string path, out NovelValue value)
    {
        int page = Current;
        value = path switch
        {
            "history.previous" => NovelValue.Boolean(page > 0), "history.next" => NovelValue.Boolean(page < Last),
            "history.empty" => NovelValue.Boolean(session.historyCount == 0), "history.count" => NovelValue.Integer(session.historyCount),
            "history.followLatest" => NovelValue.Boolean(m_latest),
            "history.page" => NovelValue.String((page + 1).ToString(CultureInfo.InvariantCulture) + " / " + (Last + 1).ToString(CultureInfo.InvariantCulture)),
            _ => NovelValue.Boolean(false)
        };
        return path is "history.previous" or "history.next" or "history.empty" or "history.count" or "history.page" or "history.followLatest";
    }
    /// <inheritdoc />
    public override bool TryWrite(string path, NovelValue value) => false;
    /// <inheritdoc />
    public override IReadOnlyList<UiDataRow> ReadRows(string path)
    {
        if (path != "history") return [];
        int start = Current * PAGE_SIZE;
        return session.ReadHistoryRange(start, PAGE_SIZE).Select((line, offset) =>
        {
            int index = start + offset; string key = index.ToString(CultureInfo.InvariantCulture);
            return new UiDataRow(key, new Dictionary<string, NovelValue>
            {
                ["id"] = NovelValue.String(line.nodeId), ["text"] = NovelValue.String(line.speaker + "\n" + line.text), ["index"] = NovelValue.String(key),
                ["voiced"] = NovelValue.Boolean(line.voiceCueId.Length > 0), ["canRollback"] = NovelValue.Boolean(session.CanRollbackToHistory(index))
            });
        }).ToArray();
    }
}
