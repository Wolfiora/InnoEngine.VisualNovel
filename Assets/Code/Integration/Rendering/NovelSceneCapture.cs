using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Scene;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Stage.Composition;
using NumericsVector2 = System.Numerics.Vector2;

namespace Inno.VisualNovel.Integration.Rendering;

/// <summary>Extracts actual scene components without creating a session or retaining a parallel object tree.</summary>
public static class NovelSceneCapture
{
    /// <summary>Captures active scene images in deterministic native hierarchy and paint order.</summary>
    /// <param name="scene">The explicit content owner.</param>
    /// <param name="view">The shared drawing and selection projection.</param>
    /// <returns>Detached drawing data; deleting a native object removes it from the next capture.</returns>
    public static StageFrame Capture(
        GameScene scene,
        NovelSceneProjection view
    ) {
        ArgumentNullException.ThrowIfNull(scene); ArgumentNullException.ThrowIfNull(view);
        var items = Drawings(scene, view).Select(static entry => entry.draw)
            .OrderBy(static item => item.group).ThenBy(static item => item.order).ToArray();
        var effects = Objects(scene).SelectMany(static item => item.GetComponents<NovelScreenEffectComponent>())
            .SelectMany(static component => component.Capture(true)).OrderBy(static effect => effect.order).ToArray();
        if (effects.Length > 32) throw new InvalidOperationException("A scene supports at most 32 active screen effects.");
        return new(Array.AsReadOnly(items), Array.AsReadOnly(effects));
    }
    /// <summary>Finds the foremost visible image using its transformed quad, including reflection and shear.</summary>
    /// <param name="scene">The explicit scene to inspect.</param>
    /// <param name="view">The projection used to draw this viewport.</param>
    /// <param name="point">Viewport-local pixel coordinates.</param>
    /// <returns>The native object, or null when no quad contains the pointer.</returns>
    public static GameObject? HitTest(
        GameScene scene,
        NovelSceneProjection view,
        NumericsVector2 point
    ) {
        foreach (var entry in Drawings(scene, view).OrderBy(static item => item.draw.group).ThenBy(static item => item.draw.order).Reverse())
        {
            StageDrawItem item = entry.draw;
            if ((item.color & 255) > 0 && (Triangle(point, item.topLeft, item.topRight, item.bottomRight)
                || Triangle(point, item.topLeft, item.bottomRight, item.bottomLeft))) return entry.owner;
        }
        return null;
    }
    /// <summary>Validates stable story bindings including inactive objects, without matching display names.</summary>
    /// <param name="scene">The owning scene.</param>
    /// <returns>A fresh binding map keyed by the author's explicit target IDs.</returns>
    public static IReadOnlyDictionary<string, GameObject> Targets(GameScene scene)
    {
        var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var item in scene.GetObjects())
            foreach (var target in item.GetComponents<NovelTargetComponent>())
                if (!string.IsNullOrWhiteSpace(target.targetId) && !result.TryAdd(target.targetId, item))
                    throw new InvalidOperationException("Duplicate Visual Novel target: " + target.targetId);
        return result;
    }
    private static IEnumerable<(GameObject owner, StageDrawItem draw)> Drawings(
        GameScene scene,
        NovelSceneProjection view
    ) {
        foreach (var item in Objects(scene))
        {
            foreach (var image in item.GetComponents<NovelImageComponent>())
                if (image.visible && Item(image, view) is { } draw) yield return (item, draw);
            foreach (var emitter in item.GetComponents<NovelParticleComponent>())
                foreach (var draw in NovelParticleCapture.Capture(emitter, view)) yield return (item, draw);
        }
    }
    internal static IEnumerable<GameObject> Objects(GameScene scene)
    {
        foreach (var root in scene.GetObjects().Where(static item => item.transform.parent is null).OrderBy(static item => item.transform.siblingIndex))
            foreach (var item in Subtree(root)) yield return item;
    }
    private static IEnumerable<GameObject> Subtree(GameObject item)
    {
        if (!item.activeInHierarchy) yield break;
        yield return item;
        foreach (var child in item.transform.children)
            foreach (var nested in Subtree(child.gameObject)) yield return nested;
    }
    private static StageDrawItem? Item(
        NovelImageComponent image,
        NovelSceneProjection view
    ) {
        new Model.Stages.StageSurface
        {
            sourceX = image.uv.x, sourceY = image.uv.y, sourceWidth = image.uv.width, sourceHeight = image.uv.height,
            cropX = image.crop.x, cropY = image.crop.y, cropWidth = image.crop.width, cropHeight = image.crop.height,
            blend = image.blend, maskMode = image.maskMode
        }.Validate();
        if ((image.color & 255) == 0 || image.crop.width == 0 || image.crop.height == 0 || image.uv.width == 0 || image.uv.height == 0) return null;
        if (!float.IsFinite(image.width) || image.width <= 0 || !float.IsFinite(image.height) || image.height <= 0
            || !float.IsFinite(image.pivot.x) || !float.IsFinite(image.pivot.y))
            throw new InvalidOperationException("Invalid image geometry on " + image.gameObject.name + ".");
        var appearance = NovelCharacterCapture.Resolve(image);
        Matrix matrix = appearance.world;
        float left = image.width * (image.crop.x - image.pivot.x), top = image.height * (image.pivot.y - image.crop.y);
        float width = image.width * image.crop.width, height = image.height * image.crop.height;
        var uv = new Rect(image.uv.x + image.crop.x * image.uv.width, image.uv.y + image.crop.y * image.uv.height, image.uv.width * image.crop.width, image.uv.height * image.crop.height);
        NumericsVector2 Point(float x, float y) => view.Project(Vector3.Transform(new(x, y, 0), matrix), image.parallax);
        string id = image.gameObject.identity.persistentId.ToString();
        return new(id, Point(left, top), Point(left + width, top), Point(left + width, top - height),
            Point(left, top - height), appearance.texture, image.color, image.group, id, image.order, uv,
            image.blend, image.mask, image.maskMode, image.crop);
    }
    internal static bool Triangle(
        NumericsVector2 point,
        NumericsVector2 a,
        NumericsVector2 b,
        NumericsVector2 c
    ) {
        static float Cross(NumericsVector2 u, NumericsVector2 v) => u.X * v.Y - u.Y * v.X;
        float area = Cross(b - a, c - a);
        if (MathF.Abs(area) < .00001f) return false;
        float u = Cross(b - a, point - a) / area, v = Cross(point - a, c - a) / area;
        return u >= 0 && v >= 0 && u + v <= 1;
    }
}
