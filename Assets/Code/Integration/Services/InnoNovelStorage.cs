using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InnoEngine.Storage;
using System.Collections.Concurrent;

namespace Inno.VisualNovel.Integration.Services;

/// <summary>Admits asynchronous persistence requests only from the native owner's live update scope.</summary>
public sealed class InnoNovelStorage : IApplicationStorage, IDisposable
{
    private readonly ConcurrentQueue<Action> m_pending = new();
    private readonly CancellationTokenSource m_lifetime = new();
    private readonly object m_gate = new();
    private bool m_disposed;

    /// <summary>Gets whether this owner has retired and revoked all pending requests.</summary>
    public bool isDisposed { get { lock (m_gate) return m_disposed; } }

    /// <summary>Starts queued native operations from the component's live application-storage execution scope.</summary>
    public void Pump()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        while (m_pending.TryDequeue(out Action? request)) request();
    }

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(
        StorageKey key,
        CancellationToken cancellationToken = default
    ) => Enqueue(token => Storage.ExistsAsync(key, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<byte[]?> ReadAsync(
        StorageKey key,
        CancellationToken cancellationToken = default
    ) => Enqueue(token => Storage.ReadAsync(key, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(
        StorageKey key,
        ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default
    ) => new(Enqueue(async token => { await Storage.WriteAsync(key, value, token).ConfigureAwait(false); return true; }, cancellationToken).AsTask());

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(
        StorageKey key,
        CancellationToken cancellationToken = default
    ) => Enqueue(token => Storage.DeleteAsync(key, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<StorageKey>> ListAsync(
        StorageKey? prefix = null,
        CancellationToken cancellationToken = default
    ) => Enqueue(token => Storage.ListAsync(prefix, token), cancellationToken);

    private ValueTask<T> Enqueue<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken
    ) {
        lock (m_gate)
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, m_lifetime.Token);
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var registration = linked.Token.Register(() => completion.TrySetCanceled(linked.Token));
            m_pending.Enqueue(() => _ = Complete(operation, completion, linked, registration));
            return new(completion.Task);
        }
    }

    private static async Task Complete<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        TaskCompletionSource<T> completion,
        CancellationTokenSource linked,
        CancellationTokenRegistration registration
    ) {
        try
        {
            if (!completion.Task.IsCompleted)
                completion.TrySetResult(await operation(linked.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) { completion.TrySetCanceled(linked.Token); }
        catch (Exception failure) { completion.TrySetException(failure); }
        finally { registration.Dispose(); linked.Dispose(); }
    }

    /// <summary>Revokes queued and active consumers without retaining retired native services or codecs.</summary>
    public void Dispose()
    {
        lock (m_gate)
        {
            if (m_disposed) return;
            m_disposed = true; m_lifetime.Cancel();
            while (m_pending.TryDequeue(out Action? request)) request();
            m_lifetime.Dispose();
        }
    }
}
