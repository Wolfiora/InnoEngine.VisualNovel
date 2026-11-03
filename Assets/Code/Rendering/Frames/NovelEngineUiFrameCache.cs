using System;
using System.Collections.Generic;
using InnoEngine.Mathematics;
using InnoEngine.UI;
using Inno.VisualNovel.UI.Animation;
using Inno.VisualNovel.UI.Imaging;
using Inno.VisualNovel.Rendering.Geometry;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Consumes engine UI resource deltas and freezes complete draws for scene and game presentation.</summary>
public sealed class NovelEngineUiFrameCache
{
    private readonly Dictionary<UiMeshHandle, UiMeshUpdate> m_meshes = [];
    private readonly Dictionary<UiTextureHandle, NovelTextureSource> m_textures = [];

    /// <summary>Resolves an engine frame into complete immutable draws, preserving native mesh topology and per-vertex color.</summary>
    /// <param name="frame">The next incremental frame from one owned engine UI context.</param>
    /// <param name="transform">Canvas-local to output-design coordinates.</param>
    /// <param name="width">Positive output design width.</param>
    /// <param name="height">Positive output design height.</param>
    /// <returns>Ordered triangle draws independent of future resource retirement.</returns>
    public IReadOnlyList<NovelDrawMesh> Apply(
        UiRenderFrame frame,
        Matrix transform,
        float width,
        float height
    ) {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        foreach (var handle in frame.releasedMeshes) m_meshes.Remove(handle);
        foreach (var handle in frame.releasedTextures) m_textures.Remove(handle);
        foreach (var mesh in frame.meshUpdates) m_meshes[mesh.mesh] = mesh;
        foreach (var texture in frame.textureUpdates)
        {
            byte[] pixels = texture.pixels.ToArray();
            for (int i = 0; i < pixels.Length; i += 4)
                for (int channel = 0; channel < 3; channel++) pixels[i + channel] = (byte)Math.Clamp((int)MathF.Round(EngineUiMeshPacker.Linear(pixels[i + channel], pixels[i + 3]) * 255), 0, 255);
            var bitmap = new UiBitmap(texture.width, texture.height, pixels);
            m_textures[texture.texture] = new("bitmap/engine-ui/" + bitmap.contentKey, bitmap: bitmap);
        }
        var result = new List<NovelDrawMesh>();
        foreach (var command in frame.commands)
        {
            if (!m_meshes.TryGetValue(command.mesh, out var mesh)) throw new InvalidOperationException("Engine UI referenced an unpublished mesh.");
            if (mesh.indices.Count == 0) continue;
            if (mesh.indices.Count % 3 != 0 || mesh.indices.Count > 3000000) throw new InvalidOperationException("Engine UI mesh topology exceeds the surface budget.");
            NovelTextureSource image = new("white");
            if (command.texture.isValid && !m_textures.TryGetValue(command.texture, out image!)) throw new InvalidOperationException("Engine UI referenced an unpublished texture.");
            Rect clip = new(0, 0, width, height);
            Rect? localClip = null;
            if (command.scissorEnabled)
            {
                var origin = Vector2.Transform(Vector2.ZERO, transform);
                var x = Vector2.Transform(new(1, 0), transform) - origin;
                var y = Vector2.Transform(new(0, 1), transform) - origin;
                if (MathF.Abs(x.y) > .00001f || MathF.Abs(y.x) > .00001f)
                    localClip = new(command.scissor.x, command.scissor.y, command.scissor.width, command.scissor.height);
                Rect bounds = UiMotionSampling.Bounds(new(command.scissor.x, command.scissor.y, command.scissor.width, command.scissor.height), transform);
                if (!Rect.TryIntersect(clip, bounds, out clip)) continue;
            }
            var bytes = EngineUiMeshPacker.Pack(mesh, transform, width, height, localClip);
            if (bytes.Length > 0) result.Add(new(bytes, clip, image));
        }
        return result.AsReadOnly();
    }

    /// <summary>Forgets resources after the owning engine UI context is destroyed.</summary>
    public void Clear() { m_meshes.Clear(); m_textures.Clear(); }
}
