using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using InnoEngine.Rendering;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Rendering.Geometry;

namespace Inno.VisualNovel.Rendering.Passes;

internal sealed class NovelImageRenderGraph
{
    private readonly RenderPipelineContext m_context;
    private readonly NovelRenderFrame m_frame;
    private readonly NovelRasterPainter m_painter;
    private readonly RenderMaterialPass m_process;
    private readonly RenderMaterialPass m_present;
    private readonly RenderBufferSlice m_quad;
    private readonly Dictionary<NovelImagePlan, RenderTextureHandle> m_images = new(ReferenceEqualityComparer.Instance);
    private int m_index;
    internal NovelImageRenderGraph(
        RenderPipelineContext context,
        NovelRenderFrame frame,
        NovelRasterPainter painter
    ) {
        m_context = context; m_frame = frame; m_painter = painter;
        if (frame.effectsMaterial is null
            || !context.resourceService.TryResolveGraphicsMaterial(frame.effectsMaterial, new("inno.vn.post"), new("inno.vn.process"), QuadBatchBuilder.layout, null, out var process) || process is null
            || !context.resourceService.TryResolveGraphicsMaterial(frame.effectsMaterial, new("inno.vn.post"), new("inno.vn.present"), QuadBatchBuilder.layout, null, out var present) || present is null)
            throw new InvalidOperationException("VN image-processing material is missing or not ready. Inspect its target shader diagnostics.");
        m_process = process; m_present = present;
        var full = new NovelDrawQuad(new(0, 0, frame.width, frame.height), new(0, 0, frame.width, frame.height), 0xFFFFFFFF, new("white"));
        m_quad = context.uploads.UploadBuffer(new(32, RenderBufferUsage.Vertex, QuadBatchBuilder.layout), QuadBatchBuilder.Pack(full, frame.width, frame.height), "VN image quad");
    }
    internal RasterPassBuilder Present(
        RenderViewport viewport,
        RenderTextureHandle? output = null,
        Action? drawn = null
    )
    {
        var image = Build(m_frame.image ?? throw new InvalidOperationException("VN image plan is absent."));
        return Process(image, image, output ?? m_context.outputTexture, viewport, m_present, new(-1, 1, 0, 0), default, 0xFFFFFFFF, 0,
            output is not null || !m_context.preservePresentationTarget, new(0, 0, 0, 1), drawn);
    }
    private RenderTextureHandle Build(NovelImagePlan plan)
    {
        if (m_images.TryGetValue(plan, out var existing)) return existing;
        var target = m_context.graph.CreateTexture("VN Image " + ++m_index,
            new(Math.Max(1, (int)Math.Ceiling(m_frame.width)), Math.Max(1, (int)Math.Ceiling(m_frame.height)), RenderTextureFormat.RGBA8, RenderTextureUsage.Sampled | RenderTextureUsage.ColorAttachment));
        var viewport = new RenderViewport(0, 0, (int)Math.Ceiling(m_frame.width), (int)Math.Ceiling(m_frame.height));
        switch (plan)
        {
            case NovelPaintImage paint: m_painter.Add(paint.quads, target, viewport, true); break;
            case NovelMeshImage mesh: m_painter.AddMeshes(mesh.meshes, target, viewport, true); break;
            case NovelStackImage stack:
                if (stack.layers.Count == 0) m_painter.Add([], target, viewport, true);
                for (int index = 0; index < stack.layers.Count; index++)
                {
                    var source = Build(stack.layers[index]);
                    Process(source, source, target, viewport, m_present, new(-1, 1, 0, 0), default, 0xFFFFFFFF, 0, index == 0);
                }
                break;
            case NovelProcessImage operation:
                Process(Build(operation.source), Build(operation.previous), target, viewport, m_process, operation.config, operation.parameters, operation.color, operation.seed, true); break;
            default: throw new InvalidOperationException("Unsupported VN image operation.");
        }
        m_images.Add(plan, target); return target;
    }
    private RasterPassBuilder Process(
        RenderTextureHandle source,
        RenderTextureHandle previous,
        RenderTextureHandle target,
        RenderViewport viewport,
        RenderMaterialPass material,
        Vector4 config,
        Vector4 parameters,
        uint color,
        uint seed,
        bool clear,
        RenderClearColor clearColor = default,
        Action? drawn = null
    ) {
        float alpha = (color & 255) / 255f;
        var data = new ImageData(material, source, previous, viewport, m_quad, Bytes(config), Bytes(parameters),
            Bytes(new(Linear(color >> 24) * alpha, Linear(color >> 16 & 255) * alpha, Linear(color >> 8 & 255) * alpha, alpha)),
            Bytes(new(m_frame.width, m_frame.height, m_context.capabilities.originBottomLeft ? 1 : 0, seed / (float)uint.MaxValue)), drawn);
        var pass = m_context.graph.AddRasterPass("VN Image Operation " + ++m_index, new("inno.vn.images"), data, static (value, owner) =>
        {
            var commands = owner.commands; value.material.Bind(commands);
            commands.SetViewport(value.viewport.x, value.viewport.y, value.viewport.width, value.viewport.height);
            commands.SetScissor(value.viewport.x, value.viewport.y, value.viewport.width, value.viewport.height);
            commands.BindTexture(new("s_current"), value.source, RenderSamplerState.linearClamp);
            commands.BindTexture(new("s_previous"), value.previous, RenderSamplerState.linearClamp);
            commands.SetUniform(new("u_config"), value.config); commands.SetUniform(new("u_parameters"), value.parameters);
            commands.SetUniform(new("u_color"), value.color); commands.SetUniform(new("u_size"), value.size);
            commands.BindVertexBuffer(value.quad); commands.Draw(6); value.drawn?.Invoke();
        });
        pass.ReadTexture(source); if (previous != source) pass.ReadTexture(previous);
        NovelRasterPainter.Attach(pass, target, clear, clearColor); return pass;
    }
    private static byte[] Bytes(Vector4 value)
    {
        var result = new byte[16]; BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(0), value.X); BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(8), value.Z); BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(12), value.W); return result;
    }
    private static float Linear(uint channel)
    {
        float value = channel / 255f; return value <= .04045f ? value / 12.92f : MathF.Pow((value + .055f) / 1.055f, 2.4f);
    }
    private sealed record ImageData(RenderMaterialPass material, RenderTextureHandle source, RenderTextureHandle previous, RenderViewport viewport, RenderBufferSlice quad,
        byte[] config, byte[] parameters, byte[] color, byte[] size, Action? drawn);
}
