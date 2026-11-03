using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Coordinates save-page operations, applying load candidates only on the owning session thread.</summary>
public sealed class SaveSlotController : IDisposable
{
    private sealed record Result(IReadOnlyList<SaveSlotMetadata> slots, NovelSessionState? loaded = null, long expectedRevision = -1);
    private readonly SaveService m_service;
    private readonly NovelSession m_session;
    private readonly Ports.INovelSaveThumbnail? m_thumbnail;
    private (SaveSlot slot, NovelSessionState state, long revision, SaveThumbnailRequest thumbnail)? m_capturing;
    private readonly CancellationTokenSource m_lifetime = new();
    private Task<Result>? m_pending;
    private IReadOnlyList<SaveSlotMetadata> m_slots = [];
    private bool m_disposed;
    private int m_revision;
    private (NovelSessionState state, long revision, SaveThumbnailRequest? thumbnail)? m_automatic;
    private bool m_metadataReady;

    /// <summary>Binds one session and its exact-build storage service.</summary>
    /// <param name="session">The main-thread game owner.</param>
    /// <param name="service">The matching native save service.</param>
    /// <param name="thumbnail">Optional exact-boundary image capture, omitted by headless hosts.</param>
    public SaveSlotController(
        NovelSession session,
        SaveService service,
        Ports.INovelSaveThumbnail? thumbnail = null
    ) { m_session = session; m_service = service; m_thumbnail = thumbnail; }

    /// <summary>Gets whether an operation is awaiting its owner-thread commit.</summary>
    public bool busy => m_pending is not null || m_capturing is not null;
    /// <summary>Gets whether the latest automatic boundary is waiting for current IO to finish.</summary>
    public bool automaticPending => m_automatic is not null;
    /// <summary>Gets the latest recoverable operation diagnostic.</summary>
    public string error { get; private set; } = string.Empty;
    /// <summary>Gets the list revision, incremented only after successful storage inspection.</summary>
    public int revision => m_revision;
    /// <summary>Gets the bounded manual slot capacity.</summary>
    public int manualSlotCount => m_service.manualSlotCount;
    /// <summary>Gets the bounded automatic slot capacity.</summary>
    public int automaticSlotCount => m_service.automaticSlotCount;
    /// <summary>Gets whether a stable active narrative can currently be saved.</summary>
    public bool canSave => !busy && m_session.canSave;
    /// <summary>Gets whether inspected metadata contains an exact-build readable continuation candidate.</summary>
    public bool canContinue => !m_disposed && !busy && m_metadataReady && m_slots.Any(static slot => slot.compatible && slot.error.Length == 0);
    /// <summary>Starts loading the newest exact-build metadata candidate through the normal atomic restoration path.</summary>
    /// <returns>Whether a candidate was available and its owner-thread load operation began.</returns>
    public bool ContinueLatest()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (!canContinue) return false;
        var latest = m_slots.Where(static slot => slot.compatible && slot.error.Length == 0).OrderByDescending(static slot => slot.utcTicks)
            .ThenBy(static slot => slot.slot.kind).ThenBy(static slot => slot.slot.index).First();
        return Load(latest.slot);
    }
    /// <summary>Gets detached list metadata without exposing mutable thumbnail storage.</summary>
    public IReadOnlyList<SaveSlotMetadata> slots => m_slots.Select(static item => item with { thumbnail = item.thumbnail.ToArray() }).ToArray();
    internal IReadOnlyList<SaveSlotMetadata> ReadMetadata() => m_slots.Select(static item => item with { thumbnail = [] }).ToArray();
    internal byte[] ReadThumbnail(SaveSlot slot) => m_slots.SingleOrDefault(item => item.slot == slot)?.thumbnail.ToArray() ?? [];

    /// <summary>Starts rebuilding slot metadata directly from complete packets.</summary>
    /// <returns>Whether a new operation was accepted.</returns>
    public bool Refresh() => Begin(async () => new(await m_service.ListAsync(m_lifetime.Token).ConfigureAwait(false)));

    /// <summary>Captures a stable game synchronously and starts an atomic save operation.</summary>
    /// <param name="slot">The bounded manual, automatic or quick slot.</param>
    /// <returns>Whether saving started.</returns>
    public bool Save(SaveSlot slot)
    {
        if (!canSave) return false;
        if (m_thumbnail is null) return Begin(async () =>
        {
            await m_service.SaveAsync(slot, m_session, cancellationToken: m_lifetime.Token).ConfigureAwait(false);
            return new(await m_service.ListAsync(m_lifetime.Token).ConfigureAwait(false));
        });
        try
        {
            var state = m_session.Capture();
            m_capturing = (slot, state, m_session.stateRevision, new(m_thumbnail, state, m_lifetime.Token)); error = ""; return true;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; return false; }
    }

    /// <summary>Freezes the current game boundary for automatic rotation, coalescing queued requests to the newest state.</summary>
    /// <returns>Whether a savable boundary was retained; active IO is allowed to finish first.</returns>
    public bool RequestAutomatic()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (!m_session.canSave) return false;
        try
        {
            var state = m_session.Capture(); var thumbnail = m_thumbnail is null ? null : new SaveThumbnailRequest(m_thumbnail, state, m_lifetime.Token);
            m_automatic?.thumbnail?.Retire();
            m_automatic = (state, m_session.stateRevision, thumbnail); StartAutomatic(); return true;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; return false; }
    }

    /// <summary>Reads a complete candidate without replacing the current game on a worker continuation.</summary>
    /// <param name="slot">The requested stored slot.</param>
    /// <returns>Whether reading started.</returns>
    public bool Load(SaveSlot slot)
    {
        long revision = m_session.stateRevision;
        return Begin(async () => new(m_slots, await m_service.ReadAsync(slot, m_lifetime.Token).ConfigureAwait(false), revision));
    }

    /// <summary>Deletes a selected slot and reconstructs metadata after durable completion.</summary>
    /// <param name="slot">The slot explicitly confirmed by the player.</param>
    /// <returns>Whether deletion started.</returns>
    public bool Delete(SaveSlot slot) => Begin(async () =>
    {
        await m_service.DeleteAsync(slot, m_lifetime.Token).ConfigureAwait(false);
        return new(await m_service.ListAsync(m_lifetime.Token).ConfigureAwait(false));
    });

    /// <summary>Commits a finished operation on the session owner thread; failures preserve the running game.</summary>
    /// <returns>Whether any finished operation was consumed.</returns>
    public bool Poll()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (m_capturing is { } capture)
        {
            if (capture.revision != m_session.stateRevision)
            { capture.thumbnail.Retire(); m_capturing = null; error = "The game was replaced during screenshot capture; the delayed save was discarded."; }
            else if (!capture.thumbnail.image.IsCompleted) return false;
            else
            {
                m_capturing = null;
                try
                {
                    byte[] image = capture.thumbnail.image.GetAwaiter().GetResult();
                    _ = Begin(async () =>
                    {
                        await m_service.SaveSnapshotAsync(capture.slot, capture.state, image, m_lifetime.Token).ConfigureAwait(false);
                        return new(await m_service.ListAsync(m_lifetime.Token).ConfigureAwait(false));
                    });
                }
                catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; }
                finally { capture.thumbnail.Retire(); }
            }
        }
        if (m_pending is not { IsCompleted: true } pending) { if (m_pending is null) StartAutomatic(); return false; }
        m_pending = null;
        try
        {
            Result result = pending.GetAwaiter().GetResult();
            if (result.loaded is not null)
            {
                if (m_session.stateRevision != result.expectedRevision) throw new InvalidOperationException("The game was replaced while reading; the delayed load was discarded.");
                m_session.Restore(result.loaded);
            }
            m_slots = result.slots; m_metadataReady = true; m_revision++; error = string.Empty;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; m_automatic?.thumbnail?.Retire(); m_automatic = null; }
        StartAutomatic();
        return true;
    }

    /// <summary>Cancels outstanding IO without allowing a late load to replace a disposed game view.</summary>
    public void Dispose()
    {
        if (m_disposed) return;
        m_disposed = true; m_automatic?.thumbnail?.Retire(); m_automatic = null; m_capturing?.thumbnail.Retire(); m_capturing = null; m_lifetime.Cancel();
        Task<Result>? pending = m_pending; m_pending = null;
        if (pending is null || pending.IsCompleted) { _ = pending?.Exception; m_lifetime.Dispose(); }
        else _ = pending.ContinueWith(task => { _ = task.Exception; m_lifetime.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private bool Begin(Func<Task<Result>> operation)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (busy) return false;
        error = string.Empty;
        try { m_pending = operation(); }
        catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; return false; }
        return true;
    }

    private void StartAutomatic()
    {
        if (m_disposed || busy || m_automatic is not { } request) return;
        if (request.revision != m_session.stateRevision) { request.thumbnail?.Retire(); m_automatic = null; return; }
        if (!m_metadataReady) { _ = Refresh(); return; }
        int free = Enumerable.Range(0, automaticSlotCount).FirstOrDefault(index => !m_slots.Any(existing => existing.slot == new SaveSlot(SaveSlotKind.Automatic, index)), -1);
        SaveSlot slot = free >= 0 ? new(SaveSlotKind.Automatic, free) : m_slots.Where(static item => item.slot.kind == SaveSlotKind.Automatic)
            .OrderBy(static item => item.utcTicks).ThenBy(static item => item.slot.index).First().slot;
        m_automatic = null;
        if (request.thumbnail is not null) { m_capturing = (slot, request.state, request.revision, request.thumbnail); return; }
        _ = Begin(async () =>
        {
            await m_service.SaveSnapshotAsync(slot, request.state, cancellationToken: m_lifetime.Token).ConfigureAwait(false);
            return new(await m_service.ListAsync(m_lifetime.Token).ConfigureAwait(false));
        });
    }
}
