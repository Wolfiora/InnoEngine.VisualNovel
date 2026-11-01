using System;

namespace Inno.VisualNovel.Narrative.Execution;

internal sealed class StoryTaskBudget
{
    private int m_count;
    internal void Acquire() { if (m_count >= 64) throw new InvalidOperationException("Parallel task budget of 64 was exceeded."); m_count++; }
    internal void Release() => m_count--;
}
