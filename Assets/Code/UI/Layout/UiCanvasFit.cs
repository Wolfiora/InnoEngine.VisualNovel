using InnoEngine.Mathematics;
using System;
using NumericsVector2 = System.Numerics.Vector2;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Layout;

/// <summary>Shares aspect-fit geometry between rendering, clipping and inverse input coordinates.</summary>
public readonly record struct UiCanvasFit
{
    private UiCanvasFit(
        Rect bounds,
        float sourceWidth,
        float sourceHeight
    ) { this.bounds = bounds; scaleX = bounds.width / sourceWidth; scaleY = bounds.height / sourceHeight; }
    /// <summary>Gets the fitted destination rectangle, excluding letterbox areas.</summary>
    public Rect bounds { get; }
    /// <summary>Gets the actual horizontal scale, including optional pixel rounding.</summary>
    public float scaleX { get; }
    /// <summary>Gets the actual vertical scale, including optional pixel rounding.</summary>
    public float scaleY { get; }
    /// <summary>Fits one logical canvas into a destination rectangle.</summary>
    /// <param name="sourceWidth">The positive logical width.</param>
    /// <param name="sourceHeight">The positive logical height.</param>
    /// <param name="viewport">The positive available destination.</param>
    /// <param name="snapToPixels">Whether to match integer native viewport extents and centering.</param>
    /// <returns>The shared forward and inverse transform.</returns>
    public static UiCanvasFit Create(
        float sourceWidth,
        float sourceHeight,
        Rect viewport,
        bool snapToPixels = false
    ) {
        if (!float.IsFinite(sourceWidth) || !float.IsFinite(sourceHeight) || sourceWidth <= 0 || sourceHeight <= 0
            || !float.IsFinite(viewport.x) || !float.IsFinite(viewport.y) || !float.IsFinite(viewport.width) || !float.IsFinite(viewport.height) || viewport.width <= 0 || viewport.height <= 0)
            throw new ArgumentException("Canvas and viewport dimensions must be finite and positive.");
        float scale = Math.Min(viewport.width / sourceWidth, viewport.height / sourceHeight);
        float width = sourceWidth * scale, height = sourceHeight * scale;
        if (snapToPixels) { width = Math.Max(1, (float)Math.Round(width)); height = Math.Max(1, (float)Math.Round(height)); }
        float x = (viewport.width - width) / 2, y = (viewport.height - height) / 2;
        if (snapToPixels) { x = MathF.Floor(x); y = MathF.Floor(y); }
        return new(new(viewport.x + x, viewport.y + y, width, height), sourceWidth, sourceHeight);
    }
    /// <summary>Projects a logical point into the fitted destination.</summary>
    /// <param name="point">The logical source point.</param>
    /// <returns>The matching destination point.</returns>
    public NumericsVector2 Project(NumericsVector2 point) => new(bounds.x + point.X * scaleX, bounds.y + point.Y * scaleY);
    /// <summary>Projects a logical rectangle without losing clipping alignment.</summary>
    /// <param name="rect">The logical source rectangle.</param>
    /// <returns>The matching destination rectangle.</returns>
    public Rect Project(Rect rect) => new(bounds.x + rect.x * scaleX, bounds.y + rect.y * scaleY, rect.width * scaleX, rect.height * scaleY);
    /// <summary>Converts a destination point back into logical canvas coordinates.</summary>
    /// <param name="point">A destination point, possibly outside the fitted bounds.</param>
    /// <returns>The corresponding logical point.</returns>
    public NumericsVector2 Unproject(NumericsVector2 point) => new((point.X - bounds.x) / scaleX, (point.Y - bounds.y) / scaleY);
}
