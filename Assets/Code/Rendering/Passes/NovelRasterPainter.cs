using InnoEngine.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Rendering;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Rendering.Geometry;
using Inno.VisualNovel.Rendering.Resources;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Rendering.Passes;

internal sealed class NovelRasterPainter(
    RenderPipelineContext context,
    float width,
    float height,
    RenderMaterialPass material,
    NovelRenderResourceCache resources
)
{
    private readonly RenderBufferUploadDescriptor m_vertices = new(32, RenderBufferUsage.Vertex, QuadBatchBuilder.layout);
    private int m_index;
    internal RasterPassBuilder Add(
        IReadOnlyList<NovelDrawQuad> quads,
        RenderTextureHandle target,
        RenderViewport viewport,
        bool clear,
        RenderClearColor color = default
    ) {
        var draws = quads.Select(quad => new Prepared(context.uploads.UploadBuffer(m_vertices, QuadBatchBuilder.Pack(quad, width, height), "VN quad"),
            resources.Resolve(context.resourceService, quad.texture), quad.clip, 6)).ToArray();
        return AddPrepared(draws, target, viewport, clear, color);
    }
    internal RasterPassBuilder AddMeshes(
        IReadOnlyList<NovelDrawMesh> meshes,
        RenderTextureHandle target,
        RenderViewport viewport,
        bool clear = false
    ) {
        var draws = meshes.Select(mesh => new Prepared(context.uploads.UploadBuffer(m_vertices, mesh.vertices, "Engine UI mesh"),
            resources.Resolve(context.resourceService, mesh.texture), mesh.clip, mesh.vertexCount)).ToArray();
        return AddPrepared(draws, target, viewport, clear, default);
    }
    private RasterPassBuilder AddPrepared(
        Prepared[] draws,
        RenderTextureHandle target,
        RenderViewport viewport,
        bool clear,
        RenderClearColor color
    ) {
        var data = new PaintData(material, draws, viewport, width, height);
        var pass = context.graph.AddRasterPass("VN Paint " + ++m_index, new("inno.vn.composition"), data, static (value, owner) =>
        {
            var commands = owner.commands;
            commands.SetViewport(value.viewport.x, value.viewport.y, value.viewport.width, value.viewport.height);
            foreach (var draw in value.draws)
            {
                int x = value.viewport.x + (int)Math.Floor(draw.clip.x / value.width * value.viewport.width);
                int y = value.viewport.y + (int)Math.Floor(draw.clip.y / value.height * value.viewport.height);
                int right = value.viewport.x + (int)Math.Ceiling((draw.clip.x + draw.clip.width) / value.width * value.viewport.width);
                int bottom = value.viewport.y + (int)Math.Ceiling((draw.clip.y + draw.clip.height) / value.height * value.viewport.height);
                if (right <= x || bottom <= y) continue;
                value.material.Bind(commands); commands.SetScissor(x, y, right - x, bottom - y);
                commands.BindTexture(new("s_image"), draw.texture, RenderSamplerState.linearClamp);
                commands.BindVertexBuffer(draw.vertices); commands.Draw(draw.count);
            }
        });
        Attach(pass, target, clear, color); return pass;
    }
    internal static void Attach(
        RasterPassBuilder pass,
        RenderTextureHandle target,
        bool clear,
        RenderClearColor color = default
    ) {
        if (target.isValid) pass.UseColorAttachment(target, 0, clear ? RenderLoadAction.Clear : RenderLoadAction.Load, RenderStoreAction.Store, color);
        else { if (clear) pass.ClearPresentationTarget(color); pass.HasSideEffect(); }
    }
    private sealed record Prepared(RenderBufferSlice vertices, PersistentTextureHandle texture, Rect clip, int count);
    private sealed record PaintData(RenderMaterialPass material, Prepared[] draws, RenderViewport viewport, float width, float height);
}
