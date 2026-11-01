using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inno.Core.Serialization;
using Inno.Storage;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StorageCancellationContracts
{
    internal static void RetiredCodec() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        foreach (int operation in new[] { 0, 1, 2, 3 })
        {
            var disk = new DelayedRead(); var codec = new RetiredCodecGuard();
            using var cancellation = new CancellationTokenSource();
            var saves = new SaveService(disk, codec, "game", "build");
            var progress = new GlobalProgressStore("game", disk, codec);
            Task pending = operation switch
            {
                0 => saves.ReadAsync(new(SaveSlotKind.Manual, 0), cancellation.Token).AsTask(),
                1 => saves.ListAsync(cancellation.Token).AsTask(),
                2 => progress.ReadProgressAsync(cancellation.Token).AsTask(),
                _ => progress.ReadPreferencesAsync(cancellation.Token).AsTask()
            };
            cancellation.Cancel(); disk.completion.SetResult([1, 2, 3]);
            bool cancelled = false;
            try { await pending; } catch (OperationCanceledException) { cancelled = true; }
            if (!cancelled || codec.decodes != 0) throw new InvalidOperationException("Cancelled late storage IO called a retired generation's codec.");
        }
    }

    private sealed class RetiredCodecGuard : INovelStateCodec
    {
        internal int decodes;
        public byte[] Encode<T>(T value) where T : class, ISerializable => throw new InvalidOperationException("Unexpected encoding in a read-cancellation test.");
        public T Decode<T>(ReadOnlySpan<byte> bytes) where T : class, ISerializable
        { decodes++; throw new InvalidOperationException("A retired generation's codec must not be called."); }
    }

    private sealed class DelayedRead : IApplicationStorage
    {
        internal readonly TaskCompletionSource<byte[]?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<byte[]?> ReadAsync(StorageKey key, CancellationToken cancellationToken = default) => new(completion.Task);
        public ValueTask<bool> ExistsAsync(StorageKey key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected existence query.");
        public ValueTask WriteAsync(StorageKey key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected write.");
        public ValueTask<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected delete.");
        public ValueTask<IReadOnlyList<StorageKey>> ListAsync(StorageKey? prefix = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected list.");
    }
}
