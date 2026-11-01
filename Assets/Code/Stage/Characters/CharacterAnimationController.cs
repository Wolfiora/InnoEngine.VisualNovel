using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Stage.Characters;

internal sealed class CharacterAnimationController
{
    private readonly Dictionary<string, CharacterAnimationState> m_owners;
    private readonly Dictionary<string, (CharacterAnimationState owner, CharacterPartAnimationState part, double duration)> m_parts = new(StringComparer.Ordinal);
    internal CharacterAnimationController(
        CharacterAnimationState[] states,
        StageLayerState[] layers,
        IReadOnlySet<Guid> textures,
        double time
    ) {
        if (states.Length > 1000 || states.Select(static state => state.owner).Distinct(StringComparer.Ordinal).Count() != states.Length)
            throw new InvalidOperationException("Character animation owners must be unique and bounded.");
        var existing = layers.ToDictionary(static layer => layer.id, StringComparer.Ordinal);
        m_owners = new(StringComparer.Ordinal);
        foreach (var input in states)
        {
            if (!input.owner.StartsWith("character/", StringComparison.Ordinal) || input.owner.Length <= 10 || !layers.Any(layer => layer.owner == input.owner)
                || !double.IsFinite(input.started) || input.started < 0 || input.started > time || !double.IsFinite(input.speechStarted) || input.speechStarted < 0 || input.speechStarted > time
                || input.parts.Length > 10000 || input.basis.Length != 6 || input.basis.Any(static value => !double.IsFinite(value) || Math.Abs(value) > 1e12)
                || !double.IsFinite(input.pivotX) || !double.IsFinite(input.pivotY) || Math.Abs(input.pivotX) > 1e12 || Math.Abs(input.pivotY) > 1e12
                || !double.IsFinite(input.breathingPhase) || input.breathingPhase < 0 || input.breathingPhase >= input.breathing.period
                || !double.IsFinite(input.breathing.amplitude) || input.breathing.amplitude is < 0 or > 0.1 || !double.IsFinite(input.breathing.period) || input.breathing.period is < 0.1 or > 3600
                || !double.IsFinite(input.breathing.anchorX) || input.breathing.anchorX is < 0 or > 1 || !double.IsFinite(input.breathing.anchorY) || input.breathing.anchorY is < 0 or > 1)
                throw new InvalidOperationException("Character animation has invalid ownership, timing or breathing geometry: " + input.owner);
            CharacterComposer.RequireInstance(input.owner["character/".Length..]);
            CharacterAnimationState state = input.Copy(); m_owners.Add(state.owner, state);
            foreach (var part in state.parts)
            {
                if (!existing.TryGetValue(part.layer, out var layer) || layer.owner != state.owner || !Enum.IsDefined(part.mode) || part.mode == CharacterAnimationMode.None
                    || part.frames.Length is < 1 or > 256 || part.frames.Any(frame => !textures.Contains(frame.texture) || !double.IsFinite(frame.seconds) || frame.seconds is < 0.001 or > 3600)
                    || !double.IsFinite(part.blinkInterval) || part.blinkInterval is < 0 or > 3600 || !double.IsFinite(part.phase) || part.phase < 0)
                    throw new InvalidOperationException("Character part animation has unavailable frames or invalid timing: " + part.layer);
                double duration = part.frames.Sum(static frame => frame.seconds);
                if (duration > 86400 || part.phase >= duration + (part.mode == CharacterAnimationMode.Blink ? part.blinkInterval : 0)
                    || !m_parts.TryAdd(part.layer, (state, part, duration))) throw new InvalidOperationException("Character animation track has duplicate ownership or invalid phase: " + part.layer);
            }
        }
        if (layers.Any(layer => layer.owner.StartsWith("character/", StringComparison.Ordinal) && !m_owners.ContainsKey(layer.owner)))
            throw new InvalidOperationException("A character layer lacks its complete neutral animation owner.");
    }
    internal CharacterAnimationState[] Capture() => m_owners.Values.OrderBy(static state => state.owner, StringComparer.Ordinal).Select(static state => state.Copy()).ToArray();
    internal void SetSpeaking(
        string id,
        bool speaking,
        double time
    ) {
        CharacterComposer.RequireInstance(id);
        if (!m_owners.TryGetValue("character/" + id, out var state)) throw new InvalidOperationException("Speaking target is not currently shown: " + id);
        if (state.speaking == speaking) return;
        state.speaking = speaking; state.speechStarted = time;
    }
    internal (Guid texture, Matrix3x2 motion) Evaluate(
        StageLayerState layer,
        double time,
        bool reducedMotion
    ) {
        Guid image = layer.textureId; Matrix3x2 breathing = Matrix3x2.Identity;
        if (!m_owners.TryGetValue(layer.owner, out var owner)) return (image, breathing);
        double elapsed = Math.Max(0, Math.Round(time - owner.started, 9));
        if (owner.breathing.enabled && !reducedMotion)
        {
            Matrix3x2 basis = StageAffineTransform.From(owner.basis);
            if (Math.Abs(basis.GetDeterminant()) >= 1e-12 && Matrix3x2.Invert(basis, out var inverse))
            {
                double scale = 1 + owner.breathing.amplitude * Math.Sin((elapsed + owner.breathingPhase) * Math.Tau / owner.breathing.period);
                breathing = inverse * Matrix3x2.CreateScale(1, (float)scale, new((float)owner.pivotX, (float)owner.pivotY)) * basis;
            }
        }
        if (!m_parts.TryGetValue(layer.id, out var track) || reducedMotion && !track.part.essential) return (image, breathing);
        var part = track.part;
        if (part.mode == CharacterAnimationMode.Talking)
        {
            if (!owner.speaking) return (image, breathing);
            elapsed = Math.Max(0, Math.Round(time - owner.speechStarted, 9));
        }
        else elapsed += part.phase;
        bool loop = part.mode != CharacterAnimationMode.Sequence || part.loop;
        double cycle = track.duration + (part.mode == CharacterAnimationMode.Blink ? part.blinkInterval : 0);
        if (!loop && elapsed >= cycle) return (part.frames[^1].texture, breathing);
        double position = loop ? elapsed % cycle : elapsed;
        if (cycle - position <= 1e-9) position = 0;
        if (part.mode == CharacterAnimationMode.Blink)
        {
            if (position + 1e-9 < part.blinkInterval) return (image, breathing);
            position -= part.blinkInterval;
        }
        double end = 0;
        foreach (var frame in part.frames) { end += frame.seconds; if (position + 1e-9 < end) return (frame.texture, breathing); }
        return (part.frames[^1].texture, breathing);
    }
}
