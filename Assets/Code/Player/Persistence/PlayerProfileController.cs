using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Loads and autosaves independent player progress on the session owner, coalescing changes without overwriting failed loads.</summary>
public sealed class PlayerProfileController : IDisposable
{
    private readonly NovelSession m_session;
    private readonly GlobalProgressStore m_store;
    private readonly CancellationTokenSource m_lifetime = new();
    private Task<(byte[]? progress, byte[]? preferences)>? m_load;
    private Task? m_write;
    private (long variables, long reading, long collections) m_savedProgress;
    private (long variables, long reading, long collections) m_writingProgress;
    private PlayerPreferences m_savedPreferences;
    private PlayerPreferences? m_writingPreferences;
    private double m_elapsed;
    private bool m_writeFailed;
    private bool m_disposed;
    /// <summary>Begins reading an independent profile before the game accepts input.</summary>
    /// <param name="session">The unstarted main session, never a replay.</param>
    /// <param name="store">Its matching native global/settings store.</param>
    public PlayerProfileController(
        NovelSession session,
        GlobalProgressStore store
    ) {
        if (session.story is not null || session.isReplay) throw new InvalidOperationException("Load player profiles before starting a main game.");
        m_session = session; m_store = store; m_savedPreferences = session.preferences.Copy(); Retry();
    }
    /// <summary>Gets whether validated data or explicitly selected defaults can be used.</summary>
    public bool ready { get; private set; }
    /// <summary>Gets whether the initial profile read is pending.</summary>
    public bool loading => m_load is not null;
    /// <summary>Gets whether an atomic profile write is pending.</summary>
    public bool saving => m_write is not null;
    /// <summary>Gets the actionable load or save failure, retained until successful retry.</summary>
    public string error { get; private set; } = "";
    /// <summary>Retries an initial read or a failed save without discarding stored progress.</summary>
    public void Retry()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (ready) { m_writeFailed = false; m_elapsed = 1; return; }
        if (m_load is not null) return;
        error = ""; m_load = m_store.ReadProfileBytesAsync(m_lifetime.Token).AsTask();
    }
    /// <summary>Explicitly permits defaults after a failed initial read; the old packet remains until the next durable profile write.</summary>
    public void UseDefaults()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (ready || loading || error.Length == 0) throw new InvalidOperationException("Defaults require a completed, failed initial profile read.");
        ready = true; error = ""; m_savedProgress = (-1, -1, -1); m_elapsed = 1;
    }
    /// <summary>Consumes completed IO and prepares changes only on the owner thread.</summary>
    /// <param name="seconds">Finite nonnegative elapsed time used to debounce automatic writes.</param>
    public void Poll(double seconds = 0)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (m_load is { IsCompleted: true } load)
        {
            m_load = null;
            try
            {
                var bytes = load.GetAwaiter().GetResult();
                var progress = m_store.DecodeProgress(bytes.progress); var preferences = m_store.DecodePreferences(bytes.preferences);
                m_session.RestoreProfile(progress, preferences); ready = true; error = "";
                m_savedProgress = m_session.progressRevision; m_savedPreferences = m_session.preferences.Copy();
            }
            catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; }
        }
        if (!ready) return;
        if (m_write is { IsCompleted: true } write)
        {
            m_write = null;
            try { write.GetAwaiter().GetResult(); m_savedProgress = m_writingProgress; m_savedPreferences = m_writingPreferences!; error = ""; }
            catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; m_writeFailed = true; }
            m_writingPreferences = null;
        }
        m_elapsed += seconds;
        if (m_write is not null || m_writeFailed || m_elapsed < 1 || !Dirty()) return;
        m_elapsed = 0;
        try
        {
            m_writingProgress = m_session.progressRevision; m_writingPreferences = m_session.preferences.Copy();
            m_write = m_store.SaveProfileSnapshotsAsync(m_session.CaptureProgress(), m_writingPreferences, m_lifetime.Token).AsTask();
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; m_writeFailed = true; m_writingPreferences = null; }
    }
    /// <summary>Freezes the latest profile immediately and queues it after older writes without using the codec from later continuations.</summary>
    /// <returns>Durable completion of the latest exact profile, or an explicit failure.</returns>
    public Task FlushAsync()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (!ready) throw new InvalidOperationException("Resolve the initial profile load before saving.");
        return m_store.SaveProfileSnapshotsAsync(m_session.CaptureProgress(), m_session.preferences.Copy()).AsTask();
    }
    /// <summary>Retires pending reads; latest changes are frozen synchronously before the native codec can be retired.</summary>
    public void Dispose()
    {
        if (m_disposed) return;
        Task? flush = null;
        if (ready && Dirty())
            try { flush = FlushAsync(); }
            catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; }
        m_disposed = true; m_lifetime.Cancel();
        Task pending = Task.WhenAll((Task?)m_load ?? Task.CompletedTask, m_write ?? Task.CompletedTask, flush ?? Task.CompletedTask);
        _ = pending.ContinueWith(task => { _ = task.Exception; m_lifetime.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        m_load = null; m_write = null;
    }
    private bool Dirty() => m_savedProgress != m_session.progressRevision || !Same(m_savedPreferences, m_session.preferences);
    private static bool Same(
        PlayerPreferences left,
        PlayerPreferences right
    ) => left.language == right.language && left.textSpeed == right.textSpeed && left.autoDelay == right.autoDelay && left.fontScale == right.fontScale
        && left.skipReadOnly == right.skipReadOnly && left.reducedMotion == right.reducedMotion && left.highContrast == right.highContrast
        && left.allowFlashing == right.allowFlashing && left.holdSkipModifiers == right.holdSkipModifiers
        && left.stopVoiceOnAdvance == right.stopVoiceOnAdvance && left.masterVolume == right.masterVolume && left.channelVolumes.SequenceEqual(right.channelVolumes)
        && left.characterVoices.Select(static item => (item.characterId, item.volume, item.enabled))
            .SequenceEqual(right.characterVoices.Select(static item => (item.characterId, item.volume, item.enabled)))
        && left.keyboardShortcuts.Select(static item => (item.action, item.key, item.modifiers))
            .SequenceEqual(right.keyboardShortcuts.Select(static item => (item.action, item.key, item.modifiers)))
        && left.navigationShortcuts.Select(static item => (item.action, item.key, item.modifiers))
            .SequenceEqual(right.navigationShortcuts.Select(static item => (item.action, item.key, item.modifiers)));
}
