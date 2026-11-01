using System;
using System.Linq;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Stage.Characters;

/// <summary>Measures the authored visible pose through normal stage composition, independently of breathing and playback clocks.</summary>
public static class CharacterCompositionBounds
{
    /// <summary>Measures cropped, mirrored and rotated character parts without mutating the canonical resource or starting a session.</summary>
    /// <param name="character">The native layered character declaration.</param>
    /// <param name="pose">The chosen pose, or empty for its explicit default.</param>
    /// <param name="expression">The chosen expression, or empty for its explicit default.</param>
    /// <param name="transform">The complete authored instance transform.</param>
    /// <returns>The axis-aligned extent in stage design pixels; an entirely hidden or degenerate pose is rejected.</returns>
    public static Rect Measure(
        CharacterAsset character,
        string pose,
        string expression,
        StageTransform transform
    ) {
        var source = CharacterAssetCopy.Copy(character); source.breathing.enabled = false;
        var stage = new StageController(source.textures); stage.ShowCharacter("composition-bounds", source, pose, expression, transform);
        var points = stage.BuildFrame(1, 1).items.SelectMany(static item => new[] { item.topLeft, item.topRight, item.bottomRight, item.bottomLeft }).ToArray();
        if (points.Length == 0) throw new InvalidOperationException("该姿势没有可用于站位对齐的可见图层。");
        var bounds = Rect.FromMinMax(new(points.Min(static point => point.X), points.Min(static point => point.Y)),
            new(points.Max(static point => point.X), points.Max(static point => point.Y)));
        if (!float.IsFinite(bounds.x) || !float.IsFinite(bounds.y) || !float.IsFinite(bounds.width) || !float.IsFinite(bounds.height) || bounds.width <= 0 || bounds.height <= 0)
            throw new InvalidOperationException("角色站位需要有限且非零的姿势范围。");
        return bounds;
    }
}
