using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Stage.Characters;

internal static class CharacterComposer
{
    internal static (StageLayerState[] layers, CharacterAnimationState animation) Compose(
        string id,
        CharacterAsset character,
        string poseId,
        string expressionId,
        StageTransform transform,
        int order,
        double time
    ) {
        RequireInstance(id); CharacterAnimationRules.Validate(character);
        CharacterPose pose = character.poses.Single(item => item.id == (poseId.Length == 0 ? character.defaultPose : poseId));
        CharacterExpression? expression = character.expressions.SingleOrDefault(item => item.id == (expressionId.Length == 0 ? character.defaultExpression : expressionId));
        if (expression is null && (character.expressions.Length > 0 || expressionId.Length > 0)) throw new InvalidOperationException("Character expression is missing.");
        if (expression is not null && expression.parts.Any(replace => !pose.parts.Any(part => part.id == replace.partId)))
            throw new InvalidOperationException("The chosen expression refers to a part absent from this pose.");
        string owner = "character/" + id; var layers = new List<StageLayerState>();
        var animation = new CharacterAnimationState { owner = owner, resource = character.resourceId, started = time,
            breathing = character.breathing.Copy(), breathingPhase = Phase(owner) * character.breathing.period };
        var tracks = new List<CharacterPartAnimationState>(); var extent = new List<Vector2>(); Matrix3x2 parent = StageAffineTransform.From(transform);
        animation.basis = StageAffineTransform.Values(parent);
        foreach (CharacterPart part in pose.parts)
        {
            part.surface.Validate();
            if (part.mask < -1 || part.mask >= character.textures.Length) throw new InvalidOperationException("Character mask image is unavailable: " + part.id);
            CharacterPartOverride? replace = expression?.parts.SingleOrDefault(item => item.partId == part.id);
            int image = replace?.texture is >= 0 ? replace.texture : part.texture;
            Matrix3x2 local = StageAffineTransform.From(part.transform) * Matrix3x2.CreateTranslation((float)(replace?.offsetX ?? 0), (float)(replace?.offsetY ?? 0));
            string layer = owner + "/" + part.id;
            layers.Add(new()
            {
                id = layer, textureId = character.textures[image].identity.persistentId, owner = owner, partOrder = part.order,
                maskTextureId = part.mask < 0 ? Guid.Empty : character.textures[part.mask].identity.persistentId, surface = part.surface.Copy(),
                pivotX = transform.x, pivotY = transform.y, color = part.color,
                width = part.width, height = part.height, matrix = StageAffineTransform.Values(local * parent), opacity = transform.opacity * part.transform.opacity,
                group = StageLayerGroup.Characters, order = order, visible = replace?.visibility is >= 0 ? replace.visibility == 1 : part.visible
            });
            if (layers[^1].visible)
                foreach (Vector2 corner in new Vector2[] { Vector2.Zero, new((float)part.width, 0), new((float)part.width, (float)part.height), new(0, (float)part.height) })
                    extent.Add(Vector2.Transform(corner, local));
            if (part.animation.mode != CharacterAnimationMode.None)
            {
                double total = part.animation.frames.Sum(static frame => frame.seconds) + (part.animation.mode == CharacterAnimationMode.Blink ? part.animation.blinkInterval : 0);
                tracks.Add(new() { layer = layer, mode = part.animation.mode, loop = part.animation.loop, blinkInterval = part.animation.blinkInterval,
                    essential = part.animation.essential, phase = part.animation.stagger && (part.animation.mode == CharacterAnimationMode.Blink || part.animation.mode == CharacterAnimationMode.Sequence && part.animation.loop) ? Phase(layer) * total : 0,
                    frames = part.animation.frames.Select(frame => new CharacterFrameState { texture = character.textures[frame.texture].identity.persistentId, seconds = frame.seconds }).ToArray() });
            }
        }
        if (extent.Count > 0)
        {
            float minX = extent.Min(static point => point.X), maxX = extent.Max(static point => point.X), minY = extent.Min(static point => point.Y), maxY = extent.Max(static point => point.Y);
            animation.pivotX = minX + (maxX - minX) * character.breathing.anchorX;
            animation.pivotY = minY + (maxY - minY) * character.breathing.anchorY;
        }
        animation.parts = tracks.ToArray(); return (layers.ToArray(), animation);
    }
    internal static void RequireInstance(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Contains('/') || id.Contains('\\')) throw new ArgumentException("Character instance names cannot contain path separators.", nameof(id));
    }
    private static double Phase(string text)
    {
        uint hash = 2166136261;
        foreach (char value in text) { hash = unchecked((hash ^ (byte)value) * 16777619); hash = unchecked((hash ^ (byte)(value >> 8)) * 16777619); }
        return hash / ((double)uint.MaxValue + 1);
    }
}
