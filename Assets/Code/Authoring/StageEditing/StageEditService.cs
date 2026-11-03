using System;
using System.Linq;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Authoring.StageEditing;

/// <summary>Applies scenery layer edits to detached resource document candidates.</summary>
public static class StageEditService
{
    /// <summary>Adds a solid layer using the stage design size.</summary>
    /// <param name="stage">The candidate scenery.</param>
    /// <returns>A fresh stage-local identity.</returns>
    public static string AddLayer(StageAsset stage)
    {
        if (stage.layers.Length >= 10000) throw new InvalidOperationException("The stage layer budget was exceeded.");
        string id = Guid.NewGuid().ToString("N");
        stage.layers = [.. stage.layers, new() { id = id, name = "New layer", width = stage.width, height = stage.height, color = 0x24344BFF }];
        return id;
    }
    /// <summary>Assigns an imported image without creating a parallel resource database.</summary>
    /// <param name="stage">The candidate scenery.</param>
    /// <param name="id">The target layer identity.</param>
    /// <param name="texture">The canonical imported image.</param>
    public static void SetImage(
        StageAsset stage,
        string id,
        TextureAsset texture
    ) {
        if (texture.isMissing || texture.identity.persistentId == Guid.Empty) throw new InvalidOperationException("Select a persistent imported image.");
        StageLayer layer = stage.layers.Single(item => item.id == id);
        int index = ImageIndex(stage, texture);
        layer.texture = index; layer.width = texture.width; layer.height = texture.height; layer.color = 0xFFFFFFFF;
    }
    /// <summary>Assigns a mask without changing the source image or its geometric dimensions.</summary>
    /// <param name="stage">The candidate scenery.</param>
    /// <param name="id">The exact layer identity.</param>
    /// <param name="texture">The imported mask image.</param>
    public static void SetMask(
        StageAsset stage,
        string id,
        TextureAsset texture
    ) {
        StageLayer layer = stage.layers.Single(item => item.id == id);
        layer.mask = ImageIndex(stage, texture);
    }
    private static int ImageIndex(
        StageAsset stage,
        TextureAsset texture
    ) {
        if (texture.isMissing || texture.identity.persistentId == Guid.Empty) throw new InvalidOperationException("Select a persistent imported image.");
        int index = Array.FindIndex(stage.textures, item => item.identity.persistentId == texture.identity.persistentId);
        if (index < 0) { index = stage.textures.Length; stage.textures = [.. stage.textures, texture]; }
        return index;
    }
    /// <summary>Removes one exact layer while retaining reusable image references.</summary>
    /// <param name="stage">The candidate scenery.</param>
    /// <param name="id">The exact layer identity.</param>
    public static void RemoveLayer(
        StageAsset stage,
        string id
    ) {
        if (!stage.layers.Any(item => item.id == id)) throw new InvalidOperationException("The stage layer is missing.");
        stage.layers = stage.layers.Where(item => item.id != id).ToArray();
    }
}
