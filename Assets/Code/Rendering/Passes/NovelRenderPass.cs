using System;
using System.Linq;
using InnoEngine.Mathematics;
using InnoEngine.Rendering;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Rendering.Geometry;
using Inno.VisualNovel.Rendering.Resources;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.Rendering.Passes;

/// <summary>Submits ordered VN images through the public neutral render graph.</summary>
public sealed class NovelRenderPass : IDisposable
{
    private readonly NovelRenderResourceCache m_resources = new();
    private readonly Capture.NovelCaptureRenderer m_capture = new();
    /// <summary>Gets the stable render-frame channel shared with the host adapter.</summary>
    public static RenderDataChannelId frameChannel => new("inno.vn.frame");
    /// <summary>Gets the shader surface contract.</summary>
    public static ShaderContractId shaderContract => new("inno.vn.surface");
    /// <summary>Gets the alpha pass role.</summary>
    public static ShaderPassRoleId alphaRole => new("inno.vn.alpha");

    /// <summary>Prepares resources and declares intermediate images and output passes.</summary>
    /// <param name="context">Current frame-scoped engine rendering context.</param>
    public void Build(RenderPipelineContext context)
    {
        m_capture.Poll(context);
        if (!context.request.data.TryGet(frameChannel, out NovelRenderFrame? frame) || frame is null)
            throw new InvalidOperationException("The VN pipeline requires a prepared VN render frame.");
        if (!context.resourceService.TryResolveGraphicsMaterial(frame.material, shaderContract, alphaRole, QuadBatchBuilder.layout, null, out RenderMaterialPass? material) || material is null)
            throw new InvalidOperationException("VN surface shader is not ready. Inspect its target compilation diagnostics.");
        var captures = context.request.data.TryGet(Capture.NovelScreenshotService.channel, out Capture.NovelCaptureRequest[]? requests) ? requests ?? [] : [];
        var quads = (frame.image?.Paints() ?? frame.quads).Concat(captures.SelectMany(static request => request.frame.image?.Paints() ?? request.frame.quads));
        var meshes = frame.uiMeshes.Concat(frame.image?.Meshes() ?? [])
            .Concat(captures.SelectMany(static request => request.frame.uiMeshes.Concat(request.frame.image?.Meshes() ?? [])));
        m_resources.BeginFrame(context.resourceService, quads.Where(static quad => quad.texture.image is null).Select(static quad => quad.texture.key)
            .Concat(meshes.Select(static mesh => mesh.texture.key)));
        var painter = new NovelRasterPainter(context, frame.width, frame.height, material, m_resources);
        RenderViewport viewport = context.request.viewport;
        var fit = UiCanvasFit.Create(frame.width, frame.height, new(viewport.x, viewport.y, viewport.width, viewport.height), true).bounds;
        var fitted = new RenderViewport((int)fit.x, (int)fit.y, (int)fit.width, (int)fit.height);
        RasterPassBuilder pass = frame.image is null
            ? painter.Add(frame.quads, context.outputTexture, fitted, !context.preservePresentationTarget, new(0, 0, 0, 1))
            : new NovelImageRenderGraph(context, frame, painter).Present(fitted);
        if (frame.uiMeshes.Count > 0) pass = painter.AddMeshes(frame.uiMeshes, context.outputTexture, fitted);
        if (context.outputTexture.isValid) context.graph.MarkOutput(context.outputTexture);
        else pass.HasSideEffect();
        foreach (var request in captures) m_capture.Build(context, request, m_resources);
    }
    /// <inheritdoc />
    public void Dispose() { m_capture.Dispose(); m_resources.Dispose(); }
}
