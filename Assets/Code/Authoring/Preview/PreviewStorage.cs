using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InnoEngine.Storage;

namespace Inno.VisualNovel.Authoring.Preview;

internal sealed class PreviewStorage : IApplicationStorage, IDisposable
{
    private const int LIMIT = 64 * 1024 * 1024;
    private readonly object m_lock = new();
    private readonly Dictionary<StorageKey, byte[]> m_values = [];
    private int m_bytes;
    private bool m_disposed;
    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(
        StorageKey key,
        CancellationToken cancellationToken = default
    ) { lock (m_lock) { Check(cancellationToken); return ValueTask.FromResult(m_values.ContainsKey(key)); } }
    /// <inheritdoc />
    public ValueTask<byte[]?> ReadAsync(
        StorageKey key,
        CancellationToken cancellationToken = default
    ) { lock (m_lock) { Check(cancellationToken); return ValueTask.FromResult(m_values.TryGetValue(key, out var value) ? value.ToArray() : null); } }
    /// <inheritdoc />
    public ValueTask WriteAsync(
        StorageKey key,
        ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default
    ) {
        lock (m_lock)
        {
            Check(cancellationToken);
            int previous = m_values.GetValueOrDefault(key)?.Length ?? 0;
            if (value.Length > LIMIT - (m_bytes - previous)) throw new IOException("Preview saves exceed their independent 64 MiB memory budget.");
            byte[] copy = value.ToArray(); Check(cancellationToken);
            m_values[key] = copy; m_bytes = m_bytes - previous + copy.Length;
            return ValueTask.CompletedTask;
        }
    }
    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(
        StorageKey key,
        CancellationToken cancellationToken = default
    ) {
        lock (m_lock) { Check(cancellationToken); if (!m_values.Remove(key, out var previous)) return ValueTask.FromResult(false); m_bytes -= previous.Length; return ValueTask.FromResult(true); }
    }
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<StorageKey>> ListAsync(
        StorageKey? prefix = null,
        CancellationToken cancellationToken = default
    ) {
        lock (m_lock)
        {
            Check(cancellationToken);
            return ValueTask.FromResult<IReadOnlyList<StorageKey>>(m_values.Keys.Where(key => prefix is null || key.value == prefix.Value.value
                || key.value.StartsWith(prefix.Value.value + "/", StringComparison.Ordinal)).OrderBy(static key => key.value, StringComparer.Ordinal).ToArray());
        }
    }
    /// <summary>Retires temporary native-format packets and rejects late preview operations.</summary>
    public void Dispose() { lock (m_lock) { m_disposed = true; m_values.Clear(); m_bytes = 0; } }
    private void Check(CancellationToken cancellationToken) { ObjectDisposedException.ThrowIf(m_disposed, this); cancellationToken.ThrowIfCancellationRequested(); }
}
