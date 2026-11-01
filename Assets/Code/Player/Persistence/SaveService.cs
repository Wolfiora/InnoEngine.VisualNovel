using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InnoEngine.Storage;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Uses native serialization and atomic application storage for whole-packet saves.</summary>
public sealed class SaveService
{
    private readonly IApplicationStorage m_storage;
    private readonly INovelStateCodec m_codec;
    private readonly string m_gameId;
    private readonly string m_build;
    private readonly string m_prefix;
    private readonly int m_manual;
    private readonly int m_automatic;
    private readonly SemaphoreSlim m_gate = new(1, 1);

    /// <summary>Gets the configured independent manual slot count.</summary>
    public int manualSlotCount => m_manual;
    /// <summary>Gets the configured rotating automatic slot count.</summary>
    public int automaticSlotCount => m_automatic;

    /// <summary>Creates an owner for one game's exact imported build.</summary>
    /// <param name="storage">The host-owned atomic sandbox.</param>
    /// <param name="codec">The generation-owned native serialization adapter.</param>
    /// <param name="gameId">The stable game identity.</param>
    /// <param name="buildFingerprint">The owning session's exact content, implementation and host build fingerprint.</param>
    /// <param name="manualSlots">The manual slot count.</param>
    /// <param name="automaticSlots">The automatic rotation size.</param>
    public SaveService(
        IApplicationStorage storage,
        INovelStateCodec codec,
        string gameId,
        string buildFingerprint,
        int manualSlots = 100,
        int automaticSlots = 3
    ) {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(codec);
        if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(buildFingerprint)) throw new ArgumentException("Game and build identities are required.");
        if (manualSlots is < 1 or > 10000 || automaticSlots is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(manualSlots));
        m_storage = storage; m_codec = codec; m_gameId = gameId; m_build = buildFingerprint;
        m_manual = manualSlots; m_automatic = automaticSlots;
        m_prefix = "visualnovel/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(gameId))).ToLowerInvariant();
    }

    /// <summary>Captures and freezes one packet before awaiting atomic replacement.</summary>
    /// <param name="slot">The destination logical slot.</param>
    /// <param name="session">The active game; it must match this service.</param>
    /// <param name="thumbnail">An image captured at the same logical boundary.</param>
    /// <param name="cancellationToken">Cancellation before storage commit.</param>
    /// <returns>Completion only after durable replacement.</returns>
    public ValueTask SaveAsync(
        SaveSlot slot,
        NovelSession session,
        ReadOnlyMemory<byte> thumbnail = default,
        CancellationToken cancellationToken = default
    ) => SaveSnapshotAsync(slot, session.Capture(), thumbnail, cancellationToken);

    /// <summary>Freezes a previously captured boundary before awaiting atomic storage replacement.</summary>
    /// <param name="slot">The bounded destination slot.</param>
    /// <param name="state">Detached exact-build state captured on the session owner thread.</param>
    /// <param name="thumbnail">An image from the same logical boundary.</param>
    /// <param name="cancellationToken">Cancellation before durable commit.</param>
    /// <returns>Completion only after the complete packet has been committed.</returns>
    public async ValueTask SaveSnapshotAsync(
        SaveSlot slot,
        NovelSessionState state,
        ReadOnlyMemory<byte> thumbnail = default,
        CancellationToken cancellationToken = default
    ) {
        CheckSlot(slot);
        CheckState(state);
        if (thumbnail.Length > 8 * 1024 * 1024) throw new ArgumentException("Save thumbnails must not exceed 8 MiB.", nameof(thumbnail));
        byte[] bytes = m_codec.Encode(new SaveEnvelope { kind = slot.kind, index = slot.index, utcTicks = DateTime.UtcNow.Ticks, state = state, thumbnail = thumbnail.ToArray() });
        if (bytes.Length > 64 * 1024 * 1024) throw new InvalidOperationException("Save packet exceeds the 64 MiB budget.");
        await m_gate.WaitAsync(cancellationToken);
        try { await m_storage.WriteAsync(Key(slot), bytes, cancellationToken); }
        finally { m_gate.Release(); }
    }

    /// <summary>Returns validated detached state without mutating an active game.</summary>
    /// <param name="slot">The source slot.</param>
    /// <param name="cancellationToken">Cancellation of the read.</param>
    /// <returns>A candidate for NovelSession.Restore; malformed and incompatible packets throw.</returns>
    public async ValueTask<NovelSessionState> ReadAsync(
        SaveSlot slot,
        CancellationToken cancellationToken = default
    ) {
        CheckSlot(slot);
        byte[] bytes = await m_storage.ReadAsync(Key(slot), cancellationToken) ?? throw new InvalidOperationException("The selected save slot is empty.");
        cancellationToken.ThrowIfCancellationRequested();
        SaveEnvelope packet = Decode(slot, bytes);
        CheckState(packet.state);
        return packet.state;
    }

    /// <summary>Reconstructs the slot list directly from complete packets, requiring no mutable index.</summary>
    /// <param name="cancellationToken">Cancellation between slot reads.</param>
    /// <returns>Existing slots in category and number order, including corrupt packet diagnostics.</returns>
    public async ValueTask<IReadOnlyList<SaveSlotMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<SaveSlotMetadata>();
        foreach (SaveSlot slot in Slots())
        {
            byte[]? bytes = await m_storage.ReadAsync(Key(slot), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes is null) continue;
            try
            {
                SaveEnvelope packet = Decode(slot, bytes);
                bool compatible = packet.state.gameId == m_gameId && packet.state.buildFingerprint == m_build;
                result.Add(new(slot, packet.utcTicks, packet.state.history.LastOrDefault()?.text ?? packet.state.narrative.nodeId,
                    packet.state.playTime, packet.thumbnail.ToArray(), compatible, compatible ? string.Empty : "Save content does not match this build."));
            }
            catch (Exception failure) when (failure is not OutOfMemoryException and not OperationCanceledException)
            { result.Add(new(slot, 0, string.Empty, 0, [], false, failure.Message)); }
        }
        return result;
    }

    /// <summary>Chooses the first empty automatic slot, otherwise the oldest complete packet.</summary>
    /// <param name="cancellationToken">Cancellation of slot inspection.</param>
    /// <returns>An automatic slot that never overlaps manual or quick slots.</returns>
    public async ValueTask<SaveSlot> NextAutomaticAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SaveSlotMetadata> existing = await ListAsync(cancellationToken);
        for (int index = 0; index < m_automatic; index++)
            if (!existing.Any(item => item.slot == new SaveSlot(SaveSlotKind.Automatic, index))) return new(SaveSlotKind.Automatic, index);
        return existing.Where(static item => item.slot.kind == SaveSlotKind.Automatic).OrderBy(static item => item.utcTicks).ThenBy(static item => item.slot.index).First().slot;
    }

    /// <summary>Deletes exactly one bounded slot through the host sandbox.</summary>
    /// <param name="slot">The source slot.</param>
    /// <param name="cancellationToken">Cancellation of deletion.</param>
    /// <returns>Whether a packet was removed.</returns>
    public async ValueTask<bool> DeleteAsync(
        SaveSlot slot,
        CancellationToken cancellationToken = default
    ) {
        CheckSlot(slot);
        await m_gate.WaitAsync(cancellationToken);
        try { return await m_storage.DeleteAsync(Key(slot), cancellationToken); }
        finally { m_gate.Release(); }
    }

    private SaveEnvelope Decode(
        SaveSlot slot,
        byte[] bytes
    ) {
        if (bytes.Length > 64 * 1024 * 1024) throw new InvalidOperationException("Save packet exceeds the 64 MiB budget.");
        SaveEnvelope result = m_codec.Decode<SaveEnvelope>(bytes);
        if (result.kind != slot.kind || result.index != slot.index || result.utcTicks <= 0 || result.utcTicks > DateTime.MaxValue.Ticks
            || result.thumbnail.Length > 8 * 1024 * 1024) throw new InvalidOperationException("Save metadata does not match its slot.");
        return result;
    }

    private void CheckState(NovelSessionState state)
    {
        if (state.gameId != m_gameId || state.buildFingerprint != m_build) throw new InvalidOperationException("Save content does not match this game build.");
    }

    private void CheckSlot(SaveSlot slot)
    {
        if (!Enum.IsDefined(slot.kind) || slot.index < 0 || slot.index >= (slot.kind == SaveSlotKind.Manual ? m_manual : slot.kind == SaveSlotKind.Automatic ? m_automatic : 1))
            throw new ArgumentOutOfRangeException(nameof(slot));
    }

    private StorageKey Key(SaveSlot slot) => new(m_prefix + "/slots/" + slot.kind.ToString().ToLowerInvariant() + "/" + slot.index.ToString(CultureInfo.InvariantCulture));
    private IEnumerable<SaveSlot> Slots()
    {
        for (int index = 0; index < m_manual; index++) yield return new(SaveSlotKind.Manual, index);
        for (int index = 0; index < m_automatic; index++) yield return new(SaveSlotKind.Automatic, index);
        yield return new(SaveSlotKind.Quick, 0);
    }
}
