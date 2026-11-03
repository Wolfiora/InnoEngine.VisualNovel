using System;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Integration.Components;
using NumericsVector2 = System.Numerics.Vector2;

namespace Inno.VisualNovel.Integration.Rendering;

/// <summary>Maps native XY world coordinates to top-left-origin output pixels without changing objects.</summary>
public sealed class NovelSceneProjection
{
    private Vector3 m_parallaxOrigin;
    /// <summary>Creates a finite orthographic view of a native scene.</summary>
    /// <param name="width">Output width in logical pixels.</param>
    /// <param name="height">Output height in logical pixels.</param>
    /// <param name="worldToView">World-to-camera matrix, independent of object transforms.</param>
    /// <param name="pixelsPerUnit">Positive output magnification.</param>
    public NovelSceneProjection(
        float width,
        float height,
        Matrix worldToView,
        float pixelsPerUnit = 1
    ) {
        if (!float.IsFinite(width) || width <= 0 || !float.IsFinite(height) || height <= 0
            || !float.IsFinite(pixelsPerUnit) || pixelsPerUnit <= 0) throw new ArgumentOutOfRangeException(nameof(width), "A scene view requires finite positive dimensions and magnification.");
        this.width = width; this.height = height; this.worldToView = worldToView; this.pixelsPerUnit = pixelsPerUnit;
    }
    /// <summary>Gets the output width.</summary>
    public float width { get; }
    /// <summary>Gets the output height.</summary>
    public float height { get; }
    /// <summary>Gets the exact world-to-view transform shared by drawing and native gizmos.</summary>
    public Matrix worldToView { get; }
    /// <summary>Gets the output magnification.</summary>
    public float pixelsPerUnit { get; }
    /// <summary>Gets a matching native orthographic projection matrix for host transform tools.</summary>
    public Matrix projection => Matrix.CreateOrthographic(width / pixelsPerUnit, height / pixelsPerUnit, -10000, 10000);
    /// <summary>Projects a world position into the rendered output.</summary>
    /// <param name="world">A point already transformed by its native object hierarchy.</param>
    /// <returns>Its pixel position, with positive Y pointing down.</returns>
    public NumericsVector2 Project(Vector3 world)
        => Project(world, 1);

    /// <summary>Projects a world point with a declared response to game camera translation.</summary>
    /// <param name="world">The unchanged native world position.</param>
    /// <param name="parallax">Finite translation multiplier with magnitude at most one million; editor projections ignore this multiplier.</param>
    /// <returns>The pixel position after camera translation, rotation, magnification and independent shake.</returns>
    public NumericsVector2 Project(
        Vector3 world,
        double parallax
    )
    {
        if (!double.IsFinite(parallax) || Math.Abs(parallax) > 1e6)
            throw new ArgumentOutOfRangeException(nameof(parallax), "Native parallax must be finite with magnitude at most one million.");
        world += m_parallaxOrigin * (float)(1 - parallax);
        Vector3 local = Vector3.Transform(world, worldToView);
        return new(width * .5f + local.x * pixelsPerUnit, height * .5f - local.y * pixelsPerUnit);
    }
    /// <summary>Creates the declared game camera, or a centered design canvas when no camera is assigned.</summary>
    /// <param name="width">Game design width.</param>
    /// <param name="height">Game design height.</param>
    /// <param name="camera">An explicitly assigned native camera.</param>
    /// <returns>A passive projection that retains no scene objects.</returns>
    public static NovelSceneProjection Game(
        float width,
        float height,
        NovelCameraComponent? camera
    ) {
        if (camera is null) return new(width, height, Matrix.identity);
        if (camera.isDestroyed || !camera.gameObject.isRuntimeValid || !camera.gameObject.activeInHierarchy)
            throw new InvalidOperationException("The assigned VN camera is missing or inactive.");
        var shake = camera.shakeOffset;
        if (!float.IsFinite(shake.x) || !float.IsFinite(shake.y)) throw new InvalidOperationException("Camera shake must be finite.");
        return new(width, height, Matrix.CreateTranslation(-shake.x, -shake.y, 0) * camera.transform.worldToLocalMatrix, camera.zoom)
            { m_parallaxOrigin = camera.transform.worldPosition };
    }
}
