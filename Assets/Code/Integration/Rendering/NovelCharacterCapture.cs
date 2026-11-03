using System;
using System.Linq;
using InnoEngine.Mathematics;
using InnoEngine.Rendering;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Stage.Characters;

namespace Inno.VisualNovel.Integration.Rendering;

internal static class NovelCharacterCapture
{
    internal static (TextureAsset? texture, Matrix world) Resolve(NovelImageComponent image)
    {
        Matrix world = image.transform.localToWorldMatrix;
        image.gameObject.TryGetComponent<NovelCharacterPartComponent>(out var part);
        if (part?.character is not { } owner) return (image.texture, world);
        if (owner.isDestroyed || !ReferenceEquals(owner.gameObject.scene, image.gameObject.scene))
            throw new InvalidOperationException("The character part owner is missing or belongs to another scene.");
        TransformAncestor();
        if (!double.IsFinite(owner.elapsed) || owner.elapsed < 0 || !double.IsFinite(owner.speechElapsed) || owner.speechElapsed < 0)
            throw new InvalidOperationException("Character clocks must be finite and nonnegative.");
        var breathing = owner.breathing;
        CharacterAnimationRules.ValidateBreathing(breathing);
        if (breathing.enabled)
        {
            float scale = (float)(1 + breathing.amplitude * Math.Sin(owner.elapsed * Math.Tau / breathing.period));
            var anchor = owner.breathingAnchor;
            if (!float.IsFinite(anchor.x) || !float.IsFinite(anchor.y)) throw new InvalidOperationException("Character breathing anchor must be finite.");
            Matrix localMotion = Matrix.CreateTranslation(anchor.x, anchor.y, 0) * Matrix.CreateScale(1, scale, 1)
                * Matrix.CreateTranslation(-anchor.x, -anchor.y, 0);
            Matrix relative = Matrix.identity;
            for (var current = image.transform; !ReferenceEquals(current, owner.transform); current = current.parent!)
                relative = Matrix.CreateTranslation(current.localPosition) * Matrix.CreateFromQuaternion(current.localRotation)
                    * Matrix.CreateScale(current.localScale) * relative;
            world = owner.transform.localToWorldMatrix * localMotion * relative;
        }
        if (part.animation.mode == CharacterAnimationMode.None) return (image.texture, world);
        CharacterAsset resource = owner.character ?? throw new InvalidOperationException("Animated character parts require their character texture library.");
        var declaration = part.animation;
        if (declaration.frames.Any(frame => frame.texture < 0 || frame.texture >= resource.textures.Length))
            throw new InvalidOperationException("A native character part references a missing animation frame.");
        var track = new CharacterPartAnimationState
        {
            layer = image.identity.persistentId.ToString(), mode = declaration.mode, loop = declaration.loop,
            blinkInterval = declaration.blinkInterval, essential = declaration.essential,
            frames = declaration.frames.Select(frame => new CharacterFrameState { texture = resource.textures[frame.texture].identity.persistentId, seconds = frame.seconds }).ToArray()
        };
        double duration = track.frames.Sum(static frame => frame.seconds) + (track.mode == CharacterAnimationMode.Blink ? track.blinkInterval : 0);
        if (declaration.stagger && (track.mode == CharacterAnimationMode.Blink || track.mode == CharacterAnimationMode.Sequence && track.loop))
            track.phase = CharacterFrameSampler.Phase(part.identity.persistentId.ToString()) * duration;
        Guid selected = CharacterFrameSampler.Sample(track, owner.elapsed, owner.speechElapsed, owner.speaking,
            image.texture?.identity.persistentId ?? Guid.Empty, false);
        return (selected == (image.texture?.identity.persistentId ?? Guid.Empty) ? image.texture
            : resource.textures.First(texture => texture.identity.persistentId == selected), world);

        void TransformAncestor()
        {
            var ancestor = image.transform.parent;
            while (ancestor is not null && !ReferenceEquals(ancestor, owner.transform)) ancestor = ancestor.parent;
            if (ancestor is null) throw new InvalidOperationException("A character part must remain in its owning character's native subtree.");
        }
    }
}
