using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Player.Persistence;

internal sealed class RollbackJournalEntry
{
    internal RollbackJournalEntry(
        RollbackCheckpoint checkpoint,
        RollbackCheckpoint? data,
        byte[]? payload
    ) {
        epoch = checkpoint.state.rollbackEpoch; history = checkpoint.history; name = checkpoint.name;
        boundary = RollbackCheckpoint.InteractiveBoundary(checkpoint.state)?.status; this.data = data; this.payload = payload;
    }
    internal long epoch { get; }
    internal int history { get; }
    internal string name { get; }
    internal StoryStatus? boundary { get; }
    internal RollbackCheckpoint? data { get; private set; }
    internal byte[]? payload { get; private set; }
    internal long bytes => payload?.LongLength ?? 0;
    internal bool available => data is not null || payload is not null;
    internal void Release() { data = null; payload = null; }
}
