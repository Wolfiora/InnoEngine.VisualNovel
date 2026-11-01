using System;
using System.Linq;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Shares bounded animation validation between resource import, composition and editing.</summary>
public static class CharacterAnimationRules
{
    /// <summary>Rejects unavailable frame images and invalid deterministic timing declarations.</summary>
    /// <param name="character">The complete character and explicit texture closure.</param>
    public static void Validate(CharacterAsset character)
    {
        CharacterBreathing breathing = character.breathing;
        if (!double.IsFinite(breathing.amplitude) || breathing.amplitude is < 0 or > 0.1 || !double.IsFinite(breathing.period) || breathing.period is < 0.1 or > 3600
            || !double.IsFinite(breathing.anchorX) || breathing.anchorX is < 0 or > 1 || !double.IsFinite(breathing.anchorY) || breathing.anchorY is < 0 or > 1)
            throw new InvalidOperationException("Character breathing requires bounded amplitude and a finite positive period.");
        foreach (var part in character.poses.SelectMany(static item => item.parts))
        {
            if (part.name.Length > 256) throw new InvalidOperationException("Character part labels must not exceed 256 characters: " + part.id);
            CharacterPartAnimation animation = part.animation;
            if (!Enum.IsDefined(animation.mode) || animation.frames.Length > 256 || animation.mode != CharacterAnimationMode.None && animation.frames.Length == 0
                || !double.IsFinite(animation.blinkInterval) || animation.blinkInterval is < 0 or > 3600
                || animation.frames.Any(frame => frame.texture < 0 || frame.texture >= character.textures.Length
                    || !double.IsFinite(frame.seconds) || frame.seconds is < 0.001 or > 3600)
                || animation.frames.Sum(static frame => frame.seconds) > 86400)
                throw new InvalidOperationException("Character animation has unavailable frames or invalid timing: " + part.id);
        }
    }
}
