using System;
using InnoEngine.Mathematics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InnoEngine.Rendering;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Rendering.Geometry;
using Inno.VisualNovel.Rendering.Passes;
using Inno.VisualNovel.Rendering.Resources;
using Inno.VisualNovel.UI.Imaging;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.Rendering.Capture;

internal sealed class NovelCaptureRenderer : IDisposable
{
    private readonly List<Pending> m_pending = [];
    internal void Poll(RenderPipelineContext context)
    {
        foreach (var pending in m_pending.ToArray())
        {
            try
            {
                if (pending.request.completion.IsCompleted) { Retire(pending); continue; }
                if (!ReferenceEquals(pending.service, context.resourceService)) throw new InvalidOperationException("Screenshot device generation was replaced.");
                if (pending.read is null && pending.request.drawnFrame is { } submitted && context.frameIndex > submitted)
                    pending.read = pending.service.ReadTextureAsync(pending.texture, cancellationToken: pending.request.cancellation).AsTask();
                if (pending.read is not { IsCompleted: true } read) continue;
                pending.request.Complete(UiPngImage.Encode(NovelReadbackImage.Normalize(read.GetAwaiter().GetResult(), pending.originBottomLeft)));
                Retire(pending);
            }
            catch (Exception failure) when (failure is not OutOfMemoryException) { pending.request.Fail(failure); Retire(pending); }
        }
    }
    internal void Build(
        RenderPipelineContext context,
        NovelCaptureRequest request,
        NovelRenderResourceCache resources
    ) {
        if (request.completion.IsCompleted) { request.Dispose(); return; }
        var id = new RenderPersistentResourceId("inno.vn.capture/" + Guid.NewGuid().ToString("N")); bool acquired = false;
        try
        {
            if (!context.capabilities.Supports(GraphicsCapability.TextureReadback | GraphicsCapability.TextureBlit)) throw new NotSupportedException("The active graphics host does not support screenshot transfer and readback.");
            var targets = NovelCaptureTargets.Create(request.width, request.height);
            RenderTextureSubresourceData[] initial = [new(0, 0, new byte[checked(request.width * request.height * 4)])];
            PersistentTextureHandle texture = context.resourceService.AcquireTexture(id, 1, targets.readback, initial, "VN save readback transfer"); acquired = true;
            using var mutation = context.graph.BeginMutationScope(); using var names = context.graph.BeginNameScope(id.value);
            var output = context.graph.CreateTexture("Image", targets.image);
            var destination = context.graph.ImportTexture("Readback", texture, targets.readback);
            NovelRenderFrame frame = request.frame with { image = request.frame.image ?? new NovelPaintImage(request.frame.quads) };
            if (!context.resourceService.TryResolveGraphicsMaterial(frame.material, NovelRenderPass.shaderContract, NovelRenderPass.alphaRole, QuadBatchBuilder.layout, null, out var material) || material is null)
                throw new InvalidOperationException("Screenshot surface shader is not ready.");
            var painter = new NovelRasterPainter(context, frame.width, frame.height, material, resources);
            var fit = UiCanvasFit.Create(frame.width, frame.height, new(0, 0, request.width, request.height), true).bounds;
            new NovelImageRenderGraph(context, frame, painter).Present(new((int)fit.x, (int)fit.y, (int)fit.width, (int)fit.height), output);
            if (frame.uiMeshes.Count > 0) painter.AddMeshes(frame.uiMeshes, output, new((int)fit.x, (int)fit.y, (int)fit.width, (int)fit.height));
            var copy = context.graph.AddCopyPass("Transfer save image", new("inno.vn.capture"), new CopyData(output, destination, request, context.frameIndex), static (value, owner) =>
            { owner.commands.CopyTexture(value.source, value.destination); value.request.MarkDrawn(value.frameIndex); });
            copy.CopyTexture(output, destination); context.graph.MarkOutput(destination); mutation.Commit();
            m_pending.Add(new(request, context.resourceService, id, texture, context.capabilities.originBottomLeft));
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            request.Fail(failure); request.Dispose(); if (acquired) context.resourceService.Release(id);
        }
    }
    public void Dispose()
    {
        foreach (var pending in m_pending.ToArray()) { pending.request.Fail(new OperationCanceledException("Screenshot pipeline was retired.")); Retire(pending); }
    }
    private void Retire(Pending pending)
    {
        if (!pending.retiring)
        {
            pending.retiring = true; pending.request.Dispose();
            if (pending.read is not null) _ = pending.read.ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);
        }
        pending.service.Release(pending.id); m_pending.Remove(pending);
    }
    private sealed class Pending(
        NovelCaptureRequest request,
        IRenderResourceService service,
        RenderPersistentResourceId id,
        PersistentTextureHandle texture,
        bool originBottomLeft
    ) {
        internal NovelCaptureRequest request { get; } = request;
        internal IRenderResourceService service { get; } = service;
        internal RenderPersistentResourceId id { get; } = id;
        internal PersistentTextureHandle texture { get; } = texture;
        internal bool originBottomLeft { get; } = originBottomLeft;
        internal Task<RenderTextureReadbackResult>? read;
        internal bool retiring;
    }
    private sealed record CopyData(RenderTextureHandle source, RenderTextureHandle destination, NovelCaptureRequest request, ulong frameIndex);
}
