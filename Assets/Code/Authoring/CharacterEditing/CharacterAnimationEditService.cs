using System;
using System.Linq;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Authoring.CharacterEditing;

/// <summary>Owns bounded frame edits shared by the native character designer and authoring tools.</summary>
public static class CharacterAnimationEditService
{
    /// <summary>Appends one imported character image to an owned part's sequence.</summary>
    /// <param name="character">The complete detached character.</param>
    /// <param name="part">The selected part owned by that character.</param>
    /// <param name="texture">An existing character-local image index.</param>
    public static void AddFrame(
        CharacterAsset character,
        CharacterPart part,
        int texture
    ) {
        if (!character.poses.Any(pose => pose.parts.Contains(part)) || texture < 0 || texture >= character.textures.Length || part.animation.frames.Length >= 256)
            throw new InvalidOperationException("Choose an owned part and available image within the 256-frame limit.");
        part.animation.frames = [.. part.animation.frames, new() { texture = texture }];
        if (part.animation.mode == CharacterAnimationMode.None) part.animation.mode = CharacterAnimationMode.Sequence;
    }
    /// <summary>Moves a frame without changing its image or authored duration.</summary>
    /// <param name="animation">The detached sequence.</param>
    /// <param name="source">The original frame index.</param>
    /// <param name="destination">The final frame index.</param>
    public static void MoveFrame(
        CharacterPartAnimation animation,
        int source,
        int destination
    ) {
        if (source < 0 || source >= animation.frames.Length || destination < 0 || destination >= animation.frames.Length) throw new ArgumentOutOfRangeException(nameof(source));
        var frames = animation.frames.ToList(); var frame = frames[source]; frames.RemoveAt(source); frames.Insert(destination, frame); animation.frames = frames.ToArray();
    }
    /// <summary>Removes a frame and disables animation when the final frame is removed.</summary>
    /// <param name="animation">The detached sequence.</param>
    /// <param name="index">The explicit frame to remove.</param>
    public static void RemoveFrame(
        CharacterPartAnimation animation,
        int index
    ) {
        if (index < 0 || index >= animation.frames.Length) throw new ArgumentOutOfRangeException(nameof(index));
        animation.frames = animation.frames.Where((_, position) => position != index).ToArray();
        if (animation.frames.Length == 0) animation.mode = CharacterAnimationMode.None;
    }
}
