using System;
using System.Collections;
using System.Collections.Generic;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Player.Persistence;

internal sealed class RollbackJournal : IReadOnlyList<RollbackJournalEntry>
{
    private readonly List<RollbackJournalEntry> m_entries = [];
    private readonly INovelStateCodec? m_codec;
    private readonly int m_limit;
    internal RollbackJournal(
        int limit,
        INovelStateCodec? codec,
        long budget
    ) { m_limit = limit; m_codec = codec; SetBudget(budget); }
    /// <summary>Gets the count-bounded boundary metadata, including explicitly omitted snapshots.</summary>
    public int Count => m_entries.Count;
    /// <summary>Gets immutable boundary metadata without decoding a native snapshot.</summary>
    public RollbackJournalEntry this[int index] => m_entries[index];
    internal long budget { get; private set; }
    internal long? storedBytes => m_codec is null ? null : m_storedBytes;
    private long m_storedBytes;
    internal string diagnostic { get; private set; } = string.Empty;
    internal void SetBudget(long bytes)
    {
        if (bytes is < 0 or > 1073741824) throw new ArgumentOutOfRangeException(nameof(bytes), "Rollback payload budget must be between zero and one GiB.");
        budget = bytes;
        diagnostic = string.Empty;
        if (bytes == 0) { foreach (var entry in m_entries) entry.Release(); m_storedBytes = 0; }
        else Trim();
    }
    internal void Add(RollbackCheckpoint checkpoint)
    {
        byte[]? payload = null; RollbackCheckpoint? data = null; diagnostic = string.Empty;
        if (budget > 0)
        {
            if (m_codec is null) data = checkpoint;
            else
            {
                try
                {
                    payload = m_codec.Encode(checkpoint);
                    if (payload is null || payload.Length == 0) throw new InvalidOperationException("Native rollback codec returned an empty packet.");
                    if (payload.LongLength > budget) { payload = null; diagnostic = "当前快照超过回滚存储预算，此位置未保留；仍可返回其他有效检查点。"; }
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                { payload = null; diagnostic = "原生回滚快照编码失败：" + error.Message; }
            }
        }
        var entry = new RollbackJournalEntry(checkpoint, data, payload); m_entries.Add(entry); m_storedBytes += entry.bytes;
        Trim();
    }
    internal RollbackCheckpoint Read(int index)
    {
        var entry = m_entries[index];
        if (!entry.available) throw new InvalidOperationException("此回滚快照已经超过预算或无法编码。");
        var candidate = entry.data ?? m_codec!.Decode<RollbackCheckpoint>(entry.payload!);
        if (candidate is null || candidate.state.rollbackEpoch != entry.epoch || candidate.history != entry.history || candidate.name != entry.name
            || RollbackCheckpoint.InteractiveBoundary(candidate.state)?.status != entry.boundary)
            throw new InvalidOperationException("原生回滚快照与保留的边界身份不匹配。");
        return candidate;
    }
    internal void Clear() { m_entries.Clear(); m_storedBytes = 0; diagnostic = string.Empty; }
    internal int FindLastIndex(Predicate<RollbackJournalEntry> match) => m_entries.FindLastIndex(match);
    internal void RemoveRange(
        int start,
        int count
    ) {
        if (start < 0 || count < 0 || start > m_entries.Count - count) throw new ArgumentOutOfRangeException(nameof(start));
        for (int index = start; index < start + count; index++) m_storedBytes -= m_entries[index].bytes;
        m_entries.RemoveRange(start, count);
    }
    private void Trim()
    {
        while (m_entries.Count > m_limit || m_storedBytes > budget && m_entries.Count > 1) RemoveRange(0, 1);
        if (m_storedBytes > budget) { m_entries[0].Release(); m_storedBytes = 0; }
    }
    /// <summary>Enumerates bounded metadata without retaining decoded state.</summary>
    public IEnumerator<RollbackJournalEntry> GetEnumerator() => m_entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
