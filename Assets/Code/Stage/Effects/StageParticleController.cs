using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Owns emitter lifetimes, fixed seeds and neutral snapshots for one stage.</summary>
public sealed class StageParticleController
{
    private sealed record Preset(ParticlePreset definition, ParticleSimulator simulator);
    private readonly Dictionary<string, Preset> m_presets;
    private List<ParticleEmitterState> m_emitters = [];
    private bool m_reducedMotion;
    /// <summary>Freezes an explicit native preset library.</summary>
    /// <param name="presets">Resources admitted to this session's content closure.</param>
    public StageParticleController(IEnumerable<VisualEffectAsset> presets) => m_presets = presets.ToDictionary(static preset => preset.resourceId,
        static preset => new Preset(VisualEffectCopy.Copy(preset).particles, new(preset)), StringComparer.Ordinal);

    /// <summary>Starts or explicitly replaces one emitter without changing other instances.</summary>
    /// <param name="id">The stable stage-local instance key.</param>
    /// <param name="presetId">The declared resource key.</param>
    /// <param name="seed">The reproducible nonzero seed.</param>
    /// <param name="x">Horizontal emission origin.</param>
    /// <param name="y">Vertical emission origin.</param>
    public void Start(
        string id,
        string presetId,
        uint seed,
        double x = 0,
        double y = 0
    ) {
        var state = new ParticleEmitterState { id = id, presetId = presetId, seed = seed, x = x, y = y };
        Restore([.. m_emitters.Where(item => item.id != id), state]);
    }
    /// <summary>Stops births while allowing existing particles to finish their declared lifetimes.</summary>
    /// <param name="id">The active emitter identity.</param>
    public void Stop(string id)
    {
        var state = m_emitters.SingleOrDefault(item => item.id == id) ?? throw new InvalidOperationException("Particle emitter is unavailable: " + id);
        if (!state.stopped) { state.stopped = true; state.emissionEnd = state.elapsed; }
    }
    /// <summary>Immediately removes an emitter and all its particles; repeated removal is harmless.</summary>
    /// <param name="id">The stage-local identity.</param>
    public void Remove(string id) => m_emitters.RemoveAll(item => item.id == id);
    /// <summary>Reports remaining births or live particles, respecting reduced-motion suppression.</summary>
    /// <param name="id">The stage-local emitter identity.</param>
    /// <returns>Whether a narrative wait should remain suspended.</returns>
    public bool IsActive(string id)
    {
        ParticleEmitterState? state = m_emitters.SingleOrDefault(item => item.id == id);
        if (state is null) return false;
        Preset preset = m_presets[state.presetId];
        if (m_reducedMotion && !preset.definition.essential) return false;
        return !state.stopped && (preset.definition.duration == 0 || state.elapsed < preset.definition.duration)
            || preset.simulator.Evaluate(state).Count > 0;
    }
    /// <summary>Advances bounded logical time and removes fully expired emitters.</summary>
    /// <param name="seconds">A finite nonnegative interval.</param>
    /// <param name="reducedMotion">Whether optional emitters should be suppressed and suspended.</param>
    public void Tick(
        double seconds,
        bool reducedMotion = false
    ) {
        if (!double.IsFinite(seconds) || seconds < 0 || m_emitters.Any(item => item.elapsed + seconds > 1e12)) throw new ArgumentOutOfRangeException(nameof(seconds));
        m_reducedMotion = reducedMotion;
        foreach (ParticleEmitterState state in m_emitters)
            if (!reducedMotion || m_presets[state.presetId].definition.essential) state.elapsed += seconds;
        m_emitters.RemoveAll(state =>
        {
            var preset = m_presets[state.presetId].definition;
            double end = state.stopped ? state.emissionEnd : preset.duration;
            if (preset.duration > 0) end = Math.Min(end, preset.duration);
            return (state.stopped || preset.duration > 0) && state.elapsed >= end + preset.maximumLifetime;
        });
    }
    /// <summary>Extracts immutable particles and canonical resources without retaining mutable preset objects.</summary>
    /// <returns>Emitter batches in stable identity order.</returns>
    public IReadOnlyList<ParticleDrawBatch> BuildBatches() => m_emitters.OrderBy(static state => state.id, StringComparer.Ordinal).Where(state => !m_reducedMotion || m_presets[state.presetId].definition.essential)
        .Select(state =>
        {
            Preset preset = m_presets[state.presetId]; var definition = preset.definition;
            return new ParticleDrawBatch(state.id, definition.group, definition.order, definition.parallax, definition.texture, preset.simulator.Evaluate(state));
        }).ToArray();
    /// <summary>Captures owned emitter state without random generators, callbacks or device handles.</summary>
    /// <returns>Detached neutral states in stable submission order.</returns>
    public ParticleEmitterState[] Capture() => m_emitters.Select(Copy).ToArray();
    /// <summary>Validates all resources, instances and aggregate budgets before committing an emitter set.</summary>
    /// <param name="snapshot">The complete candidate state.</param>
    public void Restore(ParticleEmitterState[] snapshot)
    {
        if (snapshot.Length > 32 || snapshot.Select(static state => state.id).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new InvalidOperationException("Particle emitter identities or the 32-emitter budget are invalid.");
        ParticleEmitterState[] candidate = snapshot.Select(Copy).ToArray();
        double budget = 0;
        foreach (ParticleEmitterState state in candidate)
        {
            ParticleSimulator.ValidateState(state);
            if (!m_presets.TryGetValue(state.presetId, out Preset? preset)) throw new InvalidOperationException("Particle preset is unavailable: " + state.presetId);
            budget += Math.Ceiling(preset.definition.rate * preset.definition.maximumLifetime) + 1;
        }
        if (budget > 16384) throw new InvalidOperationException("The stage exceeds its 16384-live-particle budget.");
        m_emitters = candidate.ToList();
    }
    private static ParticleEmitterState Copy(ParticleEmitterState state) => new()
    { id = state.id, presetId = state.presetId, seed = state.seed, elapsed = state.elapsed, x = state.x, y = state.y, stopped = state.stopped, emissionEnd = state.emissionEnd };
}
