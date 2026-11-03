using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Rendering;
using Inno.VisualNovel.Rendering.Frames;

namespace Inno.VisualNovel.Rendering.Resources;

/// <summary>Owns bounded provider textures and retires them with their pipeline generation.</summary>
public sealed class NovelRenderResourceCache : IDisposable
{
    private readonly string m_owner = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, TextureEntry> m_textures = new(StringComparer.Ordinal);
    private IRenderResourceService? m_service;
    private long m_nextId;
    private long m_frame;

    /// <summary>Retires unused glyph textures before preparing a new frame.</summary>
    /// <param name="service">The current device owner.</param>
    /// <param name="activeKeys">Complete keys needed by this frame.</param>
    public void BeginFrame(
        IRenderResourceService service,
        IEnumerable<string> activeKeys
    ) {
        if (m_service is not null && !ReferenceEquals(service, m_service)) Dispose();
        m_service = service;
        m_frame++;
        var active = activeKeys.ToHashSet(StringComparer.Ordinal);
        if (active.Count > 4096) throw new InvalidOperationException("The VN frame exceeds its distinct glyph texture budget.");
        foreach (string key in m_textures.Keys.Where(key => key.StartsWith("bitmap/", StringComparison.Ordinal) && !active.Contains(key)).ToArray())
        {
            service.Release(m_textures[key].id); m_textures.Remove(key);
        }
        if (m_textures.Count + active.Count <= 4096) return;
        foreach (string key in m_textures.Keys.Where(key => !active.Contains(key)).ToArray())
        {
            service.Release(m_textures[key].id);
            m_textures.Remove(key);
        }
    }

    /// <summary>Resolves a neutral texture source through the active device owner.</summary>
    /// <param name="service">Current generation's rendering service.</param>
    /// <param name="source">Imported image, glyph or white source.</param>
    /// <returns>The active persistent handle.</returns>
    public PersistentTextureHandle Resolve(
        IRenderResourceService service,
        NovelTextureSource source
    ) {
        if (m_service is not null && !ReferenceEquals(service, m_service)) Dispose();
        m_service = service;
        if (source.image is not null)
            return service.TryResolveTexture(source.image, out PersistentTextureHandle imported) ? imported : throw new InvalidOperationException("Image has no ready target artifact: " + source.image.assetPath);
        if (m_textures.TryGetValue(source.key, out TextureEntry? existing))
        {
            if (existing.frame != m_frame)
            {
                existing.handle = service.AcquireTexture(existing.id, 1, existing.descriptor, existing.uploads, "VN " + source.key);
                existing.frame = m_frame;
            }
            return existing.handle;
        }
        if (m_textures.Count >= 4096) throw new InvalidOperationException("Prepare the bounded VN resource set before resolving textures.");
        int width = source.bitmap?.width ?? source.glyph?.width ?? 1, height = source.bitmap?.height ?? source.glyph?.height ?? 1;
        var pixels = source.bitmap?.pixels.ToArray() ?? new byte[checked(width * height * 4)];
        for (int index = 0; source.bitmap is null && index < width * height; index++)
        {
            pixels[index * 4] = pixels[index * 4 + 1] = pixels[index * 4 + 2] = 255;
            pixels[index * 4 + 3] = source.glyph is null ? (byte)255 : source.glyph.pixels.Span[index];
        }
        var id = new RenderPersistentResourceId("inno.vn/" + m_owner + "/" + ++m_nextId);
        var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.RGBA8, RenderTextureUsage.Sampled);
        RenderTextureSubresourceData[] uploads = [new(0, 0, pixels)];
        PersistentTextureHandle handle = service.AcquireTexture(id, 1, descriptor, uploads, "VN " + source.key);
        m_textures.Add(source.key, new(id, descriptor, uploads, handle, m_frame));
        return handle;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (m_service is not null) foreach (var texture in m_textures.Values) m_service.Release(texture.id);
        m_textures.Clear();
        m_service = null;
    }

    private sealed class TextureEntry(
        RenderPersistentResourceId id,
        RenderTextureDescriptor descriptor,
        RenderTextureSubresourceData[] uploads,
        PersistentTextureHandle handle,
        long frame
    ) {
        internal RenderPersistentResourceId id { get; } = id;
        internal RenderTextureDescriptor descriptor { get; } = descriptor;
        internal RenderTextureSubresourceData[] uploads { get; } = uploads;
        internal PersistentTextureHandle handle { get; set; } = handle;
        internal long frame { get; set; } = frame;
    }
}
