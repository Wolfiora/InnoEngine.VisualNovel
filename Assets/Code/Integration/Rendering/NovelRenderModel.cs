using System;
using System.Linq;
using InnoEngine.Rendering;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Rendering.Passes;

namespace Inno.VisualNovel.Integration.Rendering;

/// <summary>Discovers the scene-owned VN player in a host-selected content scope.</summary>
[RenderModelExtension("inno.vn.model")]
public sealed class NovelRenderModel : IRenderModel
{
    private readonly RenderPipelineAsset m_pipeline = new() { pipelineTypeId = "inno.vn.pipeline" };
    /// <inheritdoc />
    public bool CanRender(RenderOutputSession session) => Find(session) is not null;
    /// <inheritdoc />
    public RenderModelOutput Build(RenderOutputSession session)
    {
        NovelPlayerComponent player = Find(session) ?? throw new InvalidOperationException("This output has no active Visual Novel player.");
        var data = new RenderFrameData();
        data.Set(NovelRenderPass.frameChannel, player.Prepare(session));
        data.Set(Inno.VisualNovel.Rendering.Capture.NovelScreenshotService.channel, player.DrainCaptures());
        return new("Visual Novel", m_pipeline, data);
    }
    /// <inheritdoc />
    public void Dispose() { }

    private static NovelPlayerComponent? Find(RenderOutputSession session) => session.content.GetValues<GameScene>()
        .SelectMany(static scene => scene.GetObjects()).SelectMany(static item => item.GetComponents<NovelPlayerComponent>())
        .Where(static item => item.isActiveAndEnabled && item.project is not null).SingleOrDefault();
}
