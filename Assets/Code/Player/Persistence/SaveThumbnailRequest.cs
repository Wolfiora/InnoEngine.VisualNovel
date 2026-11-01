using System.Threading;
using System.Threading.Tasks;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Player.Persistence;

internal sealed class SaveThumbnailRequest
{
    private readonly CancellationTokenSource m_lifetime;
    private bool m_retired;
    internal SaveThumbnailRequest(
        INovelSaveThumbnail capture,
        NovelSessionState state,
        CancellationToken lifetime
    ) {
        m_lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        try { image = capture.CaptureAsync(state, m_lifetime.Token).AsTask(); }
        catch { m_lifetime.Dispose(); throw; }
    }
    internal Task<byte[]> image { get; }
    internal void Retire()
    {
        if (m_retired) return; m_retired = true;
        m_lifetime.Cancel();
        _ = image.ContinueWith(task => { _ = task.Exception; m_lifetime.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
