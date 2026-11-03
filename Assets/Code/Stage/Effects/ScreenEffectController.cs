using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Stage.Effects;

/// <summary>Owns bounded screen-effect instances with deterministic replacement, clocks and candidate restoration.</summary>
public sealed class ScreenEffectController
{
    private readonly IReadOnlyDictionary<string, ScreenEffectDefinition> m_presets;
    private readonly Dictionary<string, ScreenEffectState> m_instances = new(StringComparer.Ordinal);
    /// <summary>Freezes the explicit native screen-effect library for one session generation.</summary>
    /// <param name="presets">Declared screen presets; other payload kinds are rejected.</param>
    public ScreenEffectController(IEnumerable<VisualEffectAsset> presets)
    {
        m_presets = presets.ToDictionary(static item => item.resourceId, item =>
        {
            if (item.kind != VisualEffectKind.Screen || string.IsNullOrWhiteSpace(item.resourceId)) throw new InvalidOperationException("Screen library requires unique screen-effect preset identities.");
            VisualEffectRules.Validate(item.screen); return item.screen.Copy();
        }, StringComparer.Ordinal);
    }
    /// <summary>Freezes one native component's resolved parameters without manufacturing an asset or a second scene object.</summary>
    /// <param name="resourceId">The assigned preset's stable resource identity.</param>
    /// <param name="parameters">Validated component-owned parameters.</param>
    public ScreenEffectController(
        string resourceId,
        ScreenEffectDefinition parameters
    ) {
        if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("A screen effect requires a resource identity.", nameof(resourceId));
        VisualEffectRules.Validate(parameters);
        m_presets = new Dictionary<string, ScreenEffectDefinition>(StringComparer.Ordinal) { [resourceId] = parameters.Copy() };
    }
    /// <summary>Applies one explicit instance lifecycle action without reading or changing narrative state.</summary>
    /// <param name="request">The typed authored operation.</param>
    public void Apply(ScreenEffectNode request)
    {
        if (string.IsNullOrWhiteSpace(request.instanceId) || !Enum.IsDefined(request.operation) || !Unit(request.strength) || !Time(request.duration) || request.duration > 3600)
            throw new InvalidOperationException("Screen operation requires a stable instance and bounded finite strength/time.");
        m_instances.TryGetValue(request.instanceId, out var existing);
        if (request.operation is ScreenEffectOperation.Set or ScreenEffectOperation.Fade)
        {
            if (!m_presets.ContainsKey(request.resourceId)) throw new InvalidOperationException("Screen effect preset is unavailable: " + request.resourceId);
            if (existing is null && m_instances.Count >= 32) throw new InvalidOperationException("The stage screen-effect budget of 32 instances was exceeded.");
            double from = request.operation == ScreenEffectOperation.Set ? request.strength : existing?.resourceId == request.resourceId ? Strength(existing) : 0;
            m_instances[request.instanceId] = new() { instanceId = request.instanceId, resourceId = request.resourceId, from = from, to = request.strength,
                duration = request.operation == ScreenEffectOperation.Set ? 0 : request.duration, includeUi = request.includeUi, order = request.order };
            return;
        }
        if (request.operation == ScreenEffectOperation.Remove) { m_instances.Remove(request.instanceId); return; }
        if (request.operation == ScreenEffectOperation.Wait) return;
        if (existing is null) throw new InvalidOperationException("Screen effect instance is unavailable: " + request.instanceId);
        if (request.operation == ScreenEffectOperation.Pause) existing.paused = true;
        else if (request.operation == ScreenEffectOperation.Resume) existing.paused = false;
        else if (request.operation == ScreenEffectOperation.Complete) { existing.elapsed = existing.duration; existing.paused = false; }
    }
    /// <summary>Reports a running or paused incomplete fade, excluding persistent completed image effects.</summary>
    /// <param name="id">The logical effect instance.</param>
    /// <returns>Whether a completion wait must remain blocked.</returns>
    public bool IsActive(string id) => m_instances.TryGetValue(id, out var value) && value.elapsed < value.duration;
    /// <summary>Advances explicit logical clocks; reduced motion completes fades immediately.</summary>
    /// <param name="seconds">Finite nonnegative logical time.</param>
    /// <param name="reducedMotion">Whether temporal image changes should complete immediately.</param>
    public void Tick(
        double seconds,
        bool reducedMotion
    ) {
        if (!Time(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        foreach (var state in m_instances.Values)
            if (reducedMotion) state.elapsed = state.duration;
            else if (!state.paused) state.elapsed = Math.Min(state.duration, state.elapsed + seconds);
    }
    /// <summary>Builds ordered immutable image operations after applying player flash policy.</summary>
    /// <param name="allowFlashing">Whether the player permits full-frame flashes.</param>
    /// <param name="samples">Optional samples from the existing shared performance clock, keyed by effect instance.</param>
    /// <returns>Nonzero operations in explicit order and stable identity order.</returns>
    public IReadOnlyList<ScreenEffectFrame> BuildFrame(
        bool allowFlashing,
        IReadOnlyDictionary<(string target, PerformanceProperty property), double>? samples = null
    )
    {
        var frames = new List<ScreenEffectFrame>();
        foreach (var state in m_instances.Values.OrderBy(static item => item.order).ThenBy(static item => item.instanceId, StringComparer.Ordinal))
        {
            ScreenEffectDefinition preset = Parameters("effect/" + state.instanceId, m_presets[state.resourceId], samples, out double multiplier);
            double strength = Strength(state) * multiplier;
            if (strength <= 0 || !allowFlashing && preset.kind == ScreenEffectKind.Flash) continue;
            frames.Add(new(state.instanceId, preset.kind, strength, state.includeUi, state.order, preset.blurRadius, preset.vignetteRadius,
                preset.vignetteSoftness, preset.saturation, preset.brightness, preset.contrast, preset.pixelSize, preset.color, preset.letterboxRatio, preset.aberrationPixels));
        }
        return frames.AsReadOnly();
    }

    /// <summary>Resolves supported timeline channels without mutating the authored preset.</summary>
    /// <param name="target">The exact track target identity.</param>
    /// <param name="preset">The source parameter declaration.</param>
    /// <param name="samples">Optional sampled channels from the shared timeline evaluator.</param>
    /// <param name="multiplier">Receives the separate normalized blend-strength channel.</param>
    /// <returns>Resolved parameters; callers must not mutate the returned source when there are no matching channels.</returns>
    public static ScreenEffectDefinition Parameters(
        string target,
        ScreenEffectDefinition preset,
        IReadOnlyDictionary<(string target, PerformanceProperty property), double>? samples,
        out double multiplier
    ) {
        multiplier = 1;
        if (samples is null) return preset;
        var values = samples.Where(pair => pair.Key.target == target).ToArray();
        if (values.Length == 0) return preset;
        var allowed = ScreenEffectChannels.For(preset.kind); var result = preset.Copy();
        foreach (var pair in values)
        {
            if (!allowed.Contains(pair.Key.property) || !PerformancePropertyRules.IsValid(pair.Key.property, pair.Value))
                throw new InvalidOperationException("The active screen effect cannot accept this performance parameter: " + target + "/" + pair.Key.property);
            switch (pair.Key.property)
            {
                case PerformanceProperty.EffectStrength: multiplier = pair.Value; break;
                case PerformanceProperty.BlurRadius: result.blurRadius = pair.Value; break;
                case PerformanceProperty.VignetteRadius: result.vignetteRadius = pair.Value; break;
                case PerformanceProperty.VignetteSoftness: result.vignetteSoftness = pair.Value; break;
                case PerformanceProperty.Saturation: result.saturation = pair.Value; break;
                case PerformanceProperty.Brightness: result.brightness = pair.Value; break;
                case PerformanceProperty.Contrast: result.contrast = pair.Value; break;
                case PerformanceProperty.PixelSize: result.pixelSize = pair.Value; break;
                case PerformanceProperty.LetterboxRatio: result.letterboxRatio = pair.Value; break;
                case PerformanceProperty.AberrationPixels: result.aberrationPixels = pair.Value; break;
            }
        }
        VisualEffectRules.Validate(result); return result;
    }
    /// <summary>Captures all clocks and persistent effect instances without source aliases.</summary>
    /// <returns>Detached neutral state.</returns>
    public ScreenEffectState[] Capture() => m_instances.Values.OrderBy(static item => item.instanceId, StringComparer.Ordinal).Select(Copy).ToArray();
    /// <summary>Validates complete candidate state before replacing active image effects.</summary>
    /// <param name="states">The exact-resource candidate.</param>
    public void Restore(ScreenEffectState[] states)
    {
        if (states.Length > 32 || states.Select(static item => item.instanceId).Distinct(StringComparer.Ordinal).Count() != states.Length
            || states.Any(state => string.IsNullOrWhiteSpace(state.instanceId) || !m_presets.ContainsKey(state.resourceId)
                || !Unit(state.from) || !Unit(state.to) || !Time(state.elapsed) || !Time(state.duration) || state.elapsed > state.duration || state.duration > 3600))
            throw new InvalidOperationException("Screen effect snapshot contains invalid resources, identities or clocks.");
        var candidate = states.Select(Copy).ToArray(); m_instances.Clear();
        foreach (var state in candidate) m_instances.Add(state.instanceId, state);
    }
    private static ScreenEffectState Copy(ScreenEffectState value) => new()
    { instanceId = value.instanceId, resourceId = value.resourceId, from = value.from, to = value.to, elapsed = value.elapsed, duration = value.duration, paused = value.paused, includeUi = value.includeUi, order = value.order };
    private static double Strength(ScreenEffectState value) => value.duration == 0 ? value.to : value.from + (value.to - value.from) * (value.elapsed / value.duration);
    private static bool Unit(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
    private static bool Time(double value) => double.IsFinite(value) && value >= 0;
}
