using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inno.Storage;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Player.Persistence;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class FrameStorageContracts
{
    internal static void PersistenceWithoutCallerPump(SerializationRegistry serialization)
    {
        IApplicationStorage native = new ScopeStorage();
        using var bridge = new InnoNovelStorage();
        var codec = new EditorNovelStateCodec(serialization);
        var profile = new GlobalProgressStore("frame-profile", bridge, codec);
        var saves = new SaveService(bridge, codec, "frame-profile", "build", 2, 1);
        var context = new UnpumpedContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task operations;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            operations = Task.WhenAll(profile.ReadProgressAsync().AsTask(), profile.ReadPreferencesAsync().AsTask(), saves.ListAsync().AsTask());
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!operations.IsCompleted && DateTime.UtcNow < deadline)
        {
            using (StorageExecutionContext.EnterScope(native)) bridge.Pump();
            Thread.Sleep(1);
        }
        if (!operations.IsCompleted || context.posts != 0)
            throw new InvalidOperationException("Persistence captured a host context that does not pump game continuations.");
        operations.GetAwaiter().GetResult();
    }

    internal static void NativeIoAcrossRetiredFrames()
    {
        IApplicationStorage native = new ScopeStorage();
        using var bridge = new InnoNovelStorage();
        var sequence = Sequence(bridge);
        if (sequence.IsCompleted) throw new InvalidOperationException("Queued storage ran before its native update admission.");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!sequence.IsCompleted && DateTime.UtcNow < deadline)
        {
            using (StorageExecutionContext.EnterScope(native)) bridge.Pump();
            Thread.Sleep(1);
        }
        if (!sequence.IsCompleted || !sequence.GetAwaiter().GetResult())
            throw new InvalidOperationException("Native storage did not preserve chained IO across retired frame scopes.");
        var pending = bridge.ExistsAsync(new("cancelled")).AsTask();
        bridge.Dispose();
        if (!pending.IsCanceled || !bridge.isDisposed) throw new InvalidOperationException("Retired storage kept a queued consumer alive.");
    }

    private static async Task<bool> Sequence(InnoNovelStorage storage)
    {
        var key = new StorageKey("native/value");
        await storage.WriteAsync(key, new byte[] { 3, 1, 4 });
        if (!await storage.ExistsAsync(key)) return false;
        var listed = await storage.ListAsync(new StorageKey("native"));
        byte[]? bytes = await storage.ReadAsync(key);
        if (!listed.Contains(key) || bytes is null || !bytes.SequenceEqual(new byte[] { 3, 1, 4 })) return false;
        return await storage.DeleteAsync(key) && !await storage.ExistsAsync(key);
    }

    private sealed class ScopeStorage : IApplicationStorage
    {
        private readonly Dictionary<StorageKey, byte[]> m_values = [];
        /// <inheritdoc />
        public ValueTask<bool> ExistsAsync(
            StorageKey key,
            CancellationToken cancellationToken = default
        ) => ValueTask.FromResult(m_values.ContainsKey(key));
        /// <inheritdoc />
        public ValueTask<byte[]?> ReadAsync(
            StorageKey key,
            CancellationToken cancellationToken = default
        ) => ValueTask.FromResult(m_values.TryGetValue(key, out var value) ? value.ToArray() : null);
        /// <inheritdoc />
        public ValueTask WriteAsync(
            StorageKey key,
            ReadOnlyMemory<byte> value,
            CancellationToken cancellationToken = default
        ) {
            m_values[key] = value.ToArray();
            return ValueTask.CompletedTask;
        }
        /// <inheritdoc />
        public ValueTask<bool> DeleteAsync(
            StorageKey key,
            CancellationToken cancellationToken = default
        ) => ValueTask.FromResult(m_values.Remove(key));
        /// <inheritdoc />
        public ValueTask<IReadOnlyList<StorageKey>> ListAsync(
            StorageKey? prefix = null,
            CancellationToken cancellationToken = default
        ) => ValueTask.FromResult<IReadOnlyList<StorageKey>>(m_values.Keys.Where(key => prefix is null || key.value.StartsWith(prefix.Value.value, StringComparison.Ordinal)).ToArray());
    }

    private sealed class UnpumpedContext : SynchronizationContext
    {
        internal int posts;
        /// <inheritdoc />
        public override void Post(
            SendOrPostCallback callback,
            object? state
        ) {
            Interlocked.Increment(ref posts);
        }
    }
}
