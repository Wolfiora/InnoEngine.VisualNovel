using System.Collections.Generic;

namespace Inno.VisualNovel.Rendering.Frames;

/// <summary>Paints engine-owned UI triangles into a transparent intermediate image without changing their geometry.</summary>
/// <param name="meshes">Ordered immutable triangle streams and texture dependencies.</param>
public sealed record NovelMeshImage(IReadOnlyList<NovelDrawMesh> meshes) : NovelImagePlan
{
    /// <inheritdoc />
    public override IEnumerable<NovelDrawQuad> Paints() => [];
    /// <inheritdoc />
    public override IEnumerable<NovelDrawMesh> Meshes() => meshes;
}
