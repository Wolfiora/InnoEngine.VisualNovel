using System;
using System.Collections.Generic;
using System.Linq;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Freezes public frame collections before delayed capture while borrowing only immutable pixels and canonical imported resources.</summary>
public static class NovelRenderFrameCopy
{
    /// <summary>Copies all known image operations with bounded depth and no mutable list aliases.</summary>
    /// <param name="frame">The exact owner-prepared frame.</param>
    /// <returns>An independently owned composition declaration.</returns>
    public static NovelRenderFrame Copy(NovelRenderFrame frame)
    {
        var copies = new Dictionary<NovelImagePlan, NovelImagePlan>(ReferenceEqualityComparer.Instance); int nodes = 0, paints = 0;
        var active = new HashSet<NovelImagePlan>(ReferenceEqualityComparer.Instance);
        return frame with { quads = Quads(frame.quads), image = frame.image is null ? null : Image(frame.image, 0) };
        IReadOnlyList<NovelDrawQuad> Quads(IReadOnlyList<NovelDrawQuad> source)
        {
            paints = checked(paints + source.Count); if (paints > 100000) throw new InvalidOperationException("Capture exceeds its paint budget.");
            return Array.AsReadOnly(source.ToArray());
        }
        NovelImagePlan Image(NovelImagePlan source, int depth)
        {
            if (copies.TryGetValue(source, out var existing)) return existing;
            if (depth > 128 || ++nodes > 4096 || !active.Add(source)) throw new InvalidOperationException("Capture image operations exceed their depth or ownership budget.");
            NovelImagePlan copy = source switch
            {
                NovelPaintImage paint => new NovelPaintImage(Quads(paint.quads)),
                NovelStackImage stack => new NovelStackImage(Array.AsReadOnly(stack.layers.Select(item => Image(item, depth + 1)).ToArray())),
                NovelProcessImage process => process with { source = Image(process.source, depth + 1), previous = Image(process.previous, depth + 1) },
                _ => throw new InvalidOperationException("Capture contains an unsupported image operation.")
            };
            active.Remove(source); copies.Add(source, copy); return copy;
        }
    }
}
