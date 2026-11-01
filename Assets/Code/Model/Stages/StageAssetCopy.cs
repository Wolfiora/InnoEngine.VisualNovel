using System.Linq;

namespace Inno.VisualNovel.Model.Stages;

/// <summary>Freezes scenery declarations while borrowing canonical imported image references.</summary>
public static class StageAssetCopy
{
    /// <summary>Copies all authored values independently of later source edits.</summary>
    /// <param name="source">The imported scenery declaration.</param>
    /// <returns>A detached runtime declaration with borrowed image identities.</returns>
    public static StageAsset Copy(StageAsset source) => new()
    {
        resourceId = source.resourceId, title = source.title, width = source.width, height = source.height, textures = source.textures.ToArray(),
        layers = source.layers.Select(static layer => new StageLayer { id = layer.id, name = layer.name, texture = layer.texture, width = layer.width, height = layer.height,
            transform = layer.transform.Copy(), surface = layer.surface.Copy(), mask = layer.mask,
            group = layer.group, order = layer.order, color = layer.color, parallax = layer.parallax, visible = layer.visible }).ToArray()
    };
}
