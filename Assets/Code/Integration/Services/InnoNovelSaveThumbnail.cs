using System;
using System.Threading;
using System.Threading.Tasks;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Rendering.Capture;
using Inno.VisualNovel.Rendering.Frames;

namespace Inno.VisualNovel.Integration.Services;

internal sealed class InnoNovelSaveThumbnail(
    NovelScreenshotService screenshots,
    Func<NovelSessionState, NovelRenderFrame> prepare
) : INovelSaveThumbnail
{
    /// <inheritdoc />
    public ValueTask<byte[]> CaptureAsync(
        NovelSessionState state,
        CancellationToken cancellationToken = default
    ) => new(screenshots.Request(prepare(state), cancellationToken: cancellationToken));
}
