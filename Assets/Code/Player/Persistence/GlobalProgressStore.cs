using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InnoEngine.Storage;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Persists read progress, declared globals and preferences as independent atomic native values.</summary>
public sealed class GlobalProgressStore
{
    private readonly IApplicationStorage m_storage;
    private readonly INovelStateCodec m_codec;
    private readonly string m_game;
    private readonly string m_root;
    private readonly SemaphoreSlim m_gate = new(1, 1);
    /// <summary>Binds one game's progress to native serialization and the host sandbox.</summary>
    /// <param name="gameId">The stable game identity.</param>
    /// <param name="storage">The host-owned atomic sandbox.</param>
    /// <param name="codec">The generation-owned native state codec.</param>
    public GlobalProgressStore(
        string gameId,
        IApplicationStorage storage,
        INovelStateCodec codec
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId); ArgumentNullException.ThrowIfNull(storage); ArgumentNullException.ThrowIfNull(codec);
        m_game = gameId; m_storage = storage; m_codec = codec;
        m_root = "visualnovel/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(gameId))).ToLowerInvariant();
    }
    /// <summary>Freezes declared global progress before awaiting an atomic write.</summary>
    /// <param name="session">The matching game session.</param>
    /// <param name="cancellationToken">Cancellation before durable replacement.</param>
    /// <returns>Completion after the complete value is committed.</returns>
    public ValueTask SaveProgressAsync(
        NovelSession session,
        CancellationToken cancellationToken = default
    ) {
        if (session.gameId != m_game) throw new ArgumentException("Progress belongs to another game.", nameof(session));
        return WriteAsync("progress", m_codec.Encode(session.CaptureProgress()), cancellationToken);
    }
    /// <summary>Reads detached progress for validation and application on the session's owning thread.</summary>
    /// <param name="cancellationToken">Cancellation of the read.</param>
    /// <returns>Null for a first run; corrupt or foreign packets throw without changing a session.</returns>
    public async ValueTask<GlobalProgressState?> ReadProgressAsync(CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await m_storage.ReadAsync(new(m_root + "/progress"), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes is null) return null;
        if (bytes.Length > 64 * 1024 * 1024) throw new InvalidOperationException("Global progress exceeds its storage budget.");
        GlobalProgressState state = m_codec.Decode<GlobalProgressState>(bytes);
        if (state.gameId != m_game) throw new InvalidOperationException("Global progress belongs to another game.");
        return state;
    }
    /// <summary>Commits validated preferences without overwriting global progress or save slots.</summary>
    /// <param name="preferences">The detached settings to freeze.</param>
    /// <param name="cancellationToken">Cancellation before durable replacement.</param>
    /// <returns>Completion after the complete settings packet is committed.</returns>
    public ValueTask SavePreferencesAsync(
        PlayerPreferences preferences,
        CancellationToken cancellationToken = default
    ) => WriteAsync("preferences", m_codec.Encode(new PlayerPreferencesState { gameId = m_game, preferences = preferences.Copy() }), cancellationToken);

    /// <summary>Freezes both independent native packets on the caller before queuing ordered atomic replacements.</summary>
    /// <param name="progress">The exact current global snapshot.</param>
    /// <param name="preferences">Independent validated player settings.</param>
    /// <param name="cancellationToken">Cancellation before storage replacement.</param>
    /// <returns>Completion after both independent values have been durably replaced.</returns>
    public ValueTask SaveProfileSnapshotsAsync(
        GlobalProgressState progress,
        PlayerPreferences preferences,
        CancellationToken cancellationToken = default
    ) {
        if (progress.gameId != m_game) throw new InvalidOperationException("Profile progress belongs to another game.");
        byte[] global = m_codec.Encode(progress), settings = m_codec.Encode(new PlayerPreferencesState { gameId = m_game, preferences = preferences.Copy() });
        if (global.Length > 64 * 1024 * 1024 || settings.Length > 1024 * 1024) throw new InvalidOperationException("Profile exceeds its packet budgets.");
        return WriteProfileAsync(global, settings, cancellationToken);
    }

    internal async ValueTask<(byte[]? progress, byte[]? preferences)> ReadProfileBytesAsync(CancellationToken cancellationToken)
    {
        byte[]? global = await m_storage.ReadAsync(new(m_root + "/progress"), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? settings = await m_storage.ReadAsync(new(m_root + "/preferences"), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return (global, settings);
    }
    internal GlobalProgressState? DecodeProgress(byte[]? bytes)
    {
        if (bytes is null) return null;
        if (bytes.Length > 64 * 1024 * 1024) throw new InvalidOperationException("Global progress exceeds its storage budget.");
        var state = m_codec.Decode<GlobalProgressState>(bytes);
        if (state.gameId != m_game) throw new InvalidOperationException("Global progress belongs to another game.");
        return state;
    }
    internal PlayerPreferences? DecodePreferences(byte[]? bytes)
    {
        if (bytes is null) return null;
        if (bytes.Length > 1024 * 1024) throw new InvalidOperationException("Player preferences exceed their storage budget.");
        var state = m_codec.Decode<PlayerPreferencesState>(bytes);
        if (state.gameId != m_game) throw new InvalidOperationException("Player preferences belong to another game.");
        return state.preferences.Copy();
    }
    private async ValueTask WriteProfileAsync(
        byte[] progress,
        byte[] preferences,
        CancellationToken cancellationToken
    ) {
        await m_gate.WaitAsync(cancellationToken);
        try
        {
            await m_storage.WriteAsync(new(m_root + "/progress"), progress, cancellationToken);
            await m_storage.WriteAsync(new(m_root + "/preferences"), preferences, cancellationToken);
        }
        finally { m_gate.Release(); }
    }
    /// <summary>Reads a detached settings candidate without mutating the active game.</summary>
    /// <param name="cancellationToken">Cancellation of the read.</param>
    /// <returns>Null for a first run; otherwise validated independent settings.</returns>
    public async ValueTask<PlayerPreferences?> ReadPreferencesAsync(CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await m_storage.ReadAsync(new(m_root + "/preferences"), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes is null) return null;
        if (bytes.Length > 1024 * 1024) throw new InvalidOperationException("Player preferences exceed their storage budget.");
        PlayerPreferencesState state = m_codec.Decode<PlayerPreferencesState>(bytes);
        if (state.gameId != m_game) throw new InvalidOperationException("Player preferences belong to another game.");
        return state.preferences.Copy();
    }
    private async ValueTask WriteAsync(
        string role,
        byte[] bytes,
        CancellationToken cancellationToken
    ) {
        if (bytes.Length > 64 * 1024 * 1024) throw new InvalidOperationException("Global data exceeds its storage budget.");
        await m_gate.WaitAsync(cancellationToken);
        try { await m_storage.WriteAsync(new(m_root + "/" + role), bytes, cancellationToken); }
        finally { m_gate.Release(); }
    }
}
