using System;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Characters;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StageEditing;

/// <summary>Places actual pose bounds at explicit relative stage anchors while preserving authored transforms.</summary>
public static class CharacterPlacementEditService
{
    /// <summary>Returns a detached transform aligning the selected pose extent to a declared canvas or safe area.</summary>
    /// <param name="character">The exact character whose visible parts provide the measured extent.</param>
    /// <param name="pose">The selected pose, or empty for its default.</param>
    /// <param name="expression">The selected expression, or empty for its default.</param>
    /// <param name="initial">The instance's current complete transform.</param>
    /// <param name="reference">The explicit design canvas or inset safe area.</param>
    /// <param name="relativeX">The target's horizontal position relative to the reference, allowing offscreen placement.</param>
    /// <param name="relativeY">The target's vertical position relative to the reference.</param>
    /// <param name="anchorX">The normalized horizontal point of the measured character extent to align.</param>
    /// <param name="anchorY">The normalized vertical point of the measured character extent to align.</param>
    /// <returns>A complete independent transform; inputs and active stage state remain unchanged.</returns>
    public static StageTransform Place(
        CharacterAsset character,
        string pose,
        string expression,
        StageTransform initial,
        Rect reference,
        double relativeX,
        double relativeY,
        double anchorX = .5,
        double anchorY = 1
    ) {
        if (!Finite(reference.x) || !Finite(reference.y) || !Finite(reference.width) || !Finite(reference.height) || reference.width <= 0 || reference.height <= 0
            || !double.IsFinite(relativeX) || !double.IsFinite(relativeY) || Math.Abs(relativeX) > 1000 || Math.Abs(relativeY) > 1000
            || !double.IsFinite(anchorX) || !double.IsFinite(anchorY) || anchorX is < 0 or > 1 || anchorY is < 0 or > 1)
            throw new ArgumentException("Character placement requires finite bounds, bounded relative targets and normalized extent anchors.");
        Rect bounds = CharacterCompositionBounds.Measure(character, pose, expression, initial);
        var result = initial.Copy();
        result.x += reference.x + reference.width * relativeX - bounds.x - bounds.width * anchorX;
        result.y += reference.y + reference.height * relativeY - bounds.y - bounds.height * anchorY;
        if (!Finite(result.x) || !Finite(result.y)) throw new AuthoringValidationException($"The placement exceeds the supported design-coordinate range.");
        return result;
    }
    private static bool Finite(double value) => double.IsFinite(value) && Math.Abs(value) <= 1e7;
}
