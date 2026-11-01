using InnoEngine.Rendering;
using Inno.VisualNovel.Rendering.Passes;

namespace Inno.VisualNovel.Integration.Rendering;

/// <summary>Adapts VN composition to the engine's discovered pipeline lifecycle.</summary>
[RenderPipelineExtension("inno.vn.pipeline")]
public sealed class NovelRenderPipeline : RenderPipeline
{
    private readonly NovelRenderPass m_pass = new();
    /// <inheritdoc />
    public override void Build(RenderPipelineContext context) => m_pass.Build(context);
    /// <inheritdoc />
    protected override void Dispose(bool disposing) => m_pass.Dispose();
}
