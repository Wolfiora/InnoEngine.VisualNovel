using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InnoEngine.Rendering;
using Inno.VisualNovel.Rendering.Frames;

namespace Inno.VisualNovel.Rendering.Capture;

/// <summary>Owns a bounded queue of exact immutable frame captures for one scene player.</summary>
public sealed class NovelScreenshotService : IDisposable
{
    private readonly Queue<NovelCaptureRequest> m_requests = new();
    private readonly CancellationTokenSource m_lifetime = new();
    private bool m_disposed;
    private int m_outstanding;
    /// <summary>Gets the native frame-data channel used to submit explicit screenshot side effects.</summary>
    public static RenderDataChannelId channel => new("inno.vn.capture");
    /// <summary>Queues an already frozen image; subsequent live state changes cannot replace it.</summary>
    /// <param name="frame">Exact composition prepared on the session owner.</param>
    /// <param name="width">Thumbnail width, at most 2048.</param>
    /// <param name="height">Thumbnail height, at most 2048.</param>
    /// <param name="cancellationToken">Cancellation of the save or host lifetime.</param>
    /// <returns>A standard PNG after a later rendering frame completes readback.</returns>
    public Task<byte[]> Request(
        NovelRenderFrame frame,
        int width = 320,
        int height = 180,
        CancellationToken cancellationToken = default
    ) {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);
        if (!float.IsFinite(frame.width) || !float.IsFinite(frame.height) || frame.width is <= 0 or > 16384 || frame.height is <= 0 or > 16384)
            throw new ArgumentException("Capture requires a finite bounded design canvas.", nameof(frame));
        if (width is < 1 or > 2048 || height is < 1 or > 2048 || (long)width * height > 2097152) throw new ArgumentOutOfRangeException(nameof(width));
        frame = NovelRenderFrameCopy.Copy(frame);
        if (Interlocked.Increment(ref m_outstanding) > 4) { Interlocked.Decrement(ref m_outstanding); throw new InvalidOperationException("The screenshot queue is full."); }
        var linked = CancellationTokenSource.CreateLinkedTokenSource(m_lifetime.Token, cancellationToken);
        var request = new NovelCaptureRequest(frame, width, height, linked.Token); m_requests.Enqueue(request);
        _ = request.completion.ContinueWith(task => { _ = task.Exception; linked.Dispose(); request.Dispose(); Interlocked.Decrement(ref m_outstanding); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return request.completion;
    }
    /// <summary>Transfers queued capture ownership to the current render-pipeline generation.</summary>
    /// <returns>Requests whose exact images must be rendered.</returns>
    public NovelCaptureRequest[] Drain()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        var result = new List<NovelCaptureRequest>();
        while (m_requests.TryDequeue(out var request))
            if (request.completion.IsCompleted) request.Dispose(); else result.Add(request);
        return result.ToArray();
    }
    /// <summary>Cancels all queued and transferred requests without retaining their native generation.</summary>
    public void Dispose()
    {
        if (m_disposed) return; m_disposed = true; m_lifetime.Cancel();
        while (m_requests.TryDequeue(out var request)) request.Dispose();
        m_lifetime.Dispose();
    }
}
