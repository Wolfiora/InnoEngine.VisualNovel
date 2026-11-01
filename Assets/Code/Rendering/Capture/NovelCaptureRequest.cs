using System;
using System.Threading;
using System.Threading.Tasks;
using Inno.VisualNovel.Rendering.Frames;

namespace Inno.VisualNovel.Rendering.Capture;

/// <summary>Owns one bounded exact-frame capture until completion, cancellation or timeout.</summary>
public sealed class NovelCaptureRequest : IDisposable
{
    private readonly TaskCompletionSource<byte[]> m_completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource m_lifetime;
    private readonly CancellationTokenRegistration m_cancel;
    private int m_disposed;
    internal NovelCaptureRequest(
        NovelRenderFrame frame,
        int width,
        int height,
        CancellationToken cancellation
    ) {
        this.frame = frame; this.width = width; this.height = height;
        m_lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation); m_lifetime.CancelAfter(TimeSpan.FromSeconds(30));
        m_cancel = m_lifetime.Token.Register(() =>
        {
            if (cancellation.IsCancellationRequested) m_completion.TrySetCanceled(cancellation);
            else m_completion.TrySetException(new TimeoutException("Screenshot did not complete within 30 seconds; the previous save remains valid."));
        });
    }
    /// <summary>Gets the frozen paint image captured at the same boundary as its game snapshot.</summary>
    public NovelRenderFrame frame { get; }
    /// <summary>Gets the bounded destination width.</summary>
    public int width { get; }
    /// <summary>Gets the bounded destination height.</summary>
    public int height { get; }
    /// <summary>Gets completion with a standard PNG image.</summary>
    public Task<byte[]> completion => m_completion.Task;
    internal CancellationToken cancellation => m_lifetime.Token;
    internal ulong? drawnFrame { get; private set; }
    internal void MarkDrawn(ulong frameIndex) => drawnFrame = frameIndex;
    internal void Complete(byte[] png) => m_completion.TrySetResult(png);
    internal void Fail(Exception failure) => m_completion.TrySetException(failure);
    /// <summary>Releases request-local timers and cancellation subscriptions after rendering retires it.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref m_disposed, 1) != 0) return;
        m_completion.TrySetCanceled(); m_cancel.Dispose(); m_lifetime.Cancel(); m_lifetime.Dispose();
    }
}
