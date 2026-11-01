using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Stage.Transitions;

/// <summary>Owns one deterministic composition boundary and a resource-validated previous-stage evaluator.</summary>
public sealed class StageTransitionController
{
    private readonly IReadOnlyDictionary<string, TransitionDefinition> m_presets;
    private StageTransitionState? m_state;
    private StageController? m_before;
    /// <summary>Freezes the explicit transition resource library for one generation.</summary>
    /// <param name="presets">Native transition resources with unique identities.</param>
    public StageTransitionController(IEnumerable<VisualEffectAsset> presets)
    {
        m_presets = presets.ToDictionary(static item => item.resourceId, item =>
        {
            if (item.kind != VisualEffectKind.Transition || string.IsNullOrWhiteSpace(item.resourceId)) throw new InvalidOperationException("Transition library requires unique transition preset identities.");
            VisualEffectRules.Validate(item.transition); return item.transition.Copy();
        }, StringComparer.Ordinal);
    }
    /// <summary>Gets whether a running or paused transition still owns its previous composition.</summary>
    public bool active => m_state is not null;
    /// <summary>Gets the monotonic owner-local boundary identity.</summary>
    public long sequence { get; private set; }
    /// <summary>Gets the current scope, or Stage when no transition is active.</summary>
    public TransitionScope scope => m_state?.scope ?? TransitionScope.Stage;
    /// <summary>Prepares a complete new boundary before replacing any current transition.</summary>
    /// <param name="request">The typed begin operation.</param>
    /// <param name="before">The previous neutral stage without nested boundaries.</param>
    /// <param name="fork">Creates an independent evaluator over the owning stage resource closure.</param>
    public void Begin(
        TransitionNode request,
        StageState before,
        Func<StageState, StageController> fork
    ) {
        if (!m_presets.ContainsKey(request.resourceId) || !Enum.IsDefined(request.scope) || !Enum.IsDefined(request.group) || request.target.Length > 1024 || before.transitions.Length > 0)
            throw new InvalidOperationException("Transition boundary has an unavailable preset, invalid scope or nested previous state.");
        StageController prepared = fork(before); long next = checked(sequence + 1);
        m_state = new() { sequence = next, resourceId = request.resourceId, scope = request.scope, group = request.group, target = request.target };
        m_before = prepared; sequence = next;
    }
    /// <summary>Advances logical time, completing immediately under reduced-motion policy.</summary>
    /// <param name="seconds">Finite nonnegative logical time.</param>
    /// <param name="reducedMotion">Whether the transition should finish without motion.</param>
    /// <param name="allowFlashing">Whether intermediate fade colors are allowed.</param>
    public void Tick(
        double seconds,
        bool reducedMotion,
        bool allowFlashing = true
    ) {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (m_state is null) return;
        double duration = m_presets[m_state.resourceId].duration;
        if (reducedMotion || !allowFlashing && m_presets[m_state.resourceId].kind == TransitionKind.Fade) { Complete(); return; }
        if (!m_state.paused) m_state.elapsed = Math.Min(duration, m_state.elapsed + seconds);
        if (m_state.elapsed >= duration) Complete();
    }
    /// <summary>Pauses or resumes an existing logical boundary.</summary>
    /// <param name="paused">The desired pause policy.</param>
    public void SetPaused(bool paused)
    {
        if (m_state is null) throw new InvalidOperationException("No transition is active.");
        m_state.paused = paused;
    }
    /// <summary>Completes at the new image and releases the previous evaluator.</summary>
    public void Complete() { m_state = null; m_before = null; }
    /// <summary>Returns detached previous-stage state for an explicit cancellation.</summary>
    /// <returns>The previous snapshot, or null when no boundary exists.</returns>
    public StageState? Previous() => m_before?.Capture();
    /// <summary>Extracts an immutable two-image operation for the current canvas.</summary>
    /// <param name="width">Design canvas width.</param>
    /// <param name="height">Design canvas height.</param>
    /// <returns>Null when no transition owns an image.</returns>
    public StageTransitionFrame? BuildFrame(
        float width,
        float height
    ) {
        if (m_state is null) return null;
        var preset = m_presets[m_state.resourceId];
        return new(sequence, preset.kind, m_state.elapsed / preset.duration, preset.angle, preset.softness, preset.color,
            preset.centerX, preset.centerY, preset.reverse, preset.mask, preset.seed, m_state.scope, m_state.group, m_state.target, m_before!.BuildFrame(width, height));
    }
    /// <summary>Captures zero or one complete neutral transition boundaries.</summary>
    /// <returns>Detached metadata and previous composition.</returns>
    public StageTransitionState[] Capture() => m_state is null ? [] : [new()
    {
        sequence = sequence, resourceId = m_state.resourceId, elapsed = m_state.elapsed, paused = m_state.paused,
        scope = m_state.scope, group = m_state.group, target = m_state.target, before = m_before!.Capture()
    }];
    /// <summary>Validates previous composition and clock before adopting a candidate boundary.</summary>
    /// <param name="states">Zero or one saved boundaries.</param>
    /// <param name="savedSequence">The stage's monotonic boundary identity.</param>
    /// <param name="fork">The resource-validated evaluator factory.</param>
    public void Restore(
        StageTransitionState[] states,
        long savedSequence,
        Func<StageState, StageController> fork
    ) {
        if (states.Length > 1 || savedSequence < 0) throw new InvalidOperationException("Transition snapshot exceeds its ownership bound.");
        if (states.Length == 0) { Complete(); sequence = savedSequence; return; }
        var value = states[0];
        if (!m_presets.TryGetValue(value.resourceId, out var preset) || value.sequence != savedSequence || value.sequence <= 0
            || !double.IsFinite(value.elapsed) || value.elapsed < 0 || value.elapsed >= preset.duration || !Enum.IsDefined(value.scope)
            || !Enum.IsDefined(value.group) || value.target.Length > 1024 || value.before.transitions.Length > 0)
            throw new InvalidOperationException("Transition snapshot contains invalid resources, scope or progress.");
        var prepared = fork(value.before);
        m_state = new() { sequence = savedSequence, resourceId = value.resourceId, elapsed = value.elapsed, paused = value.paused, scope = value.scope, group = value.group, target = value.target };
        m_before = prepared; sequence = savedSequence;
    }
}
