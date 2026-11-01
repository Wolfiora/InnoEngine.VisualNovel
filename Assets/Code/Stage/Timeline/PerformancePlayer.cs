using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Stage.Timeline;

/// <summary>Evaluates copied keyframes as a pure function of logical time.</summary>
public sealed class PerformancePlayer
{
    private readonly PerformanceTrack[] m_tracks;
    private readonly double m_duration;
    private readonly bool m_loop;
    private double m_elapsed;

    /// <summary>Validates and freezes one performance, rejecting ambiguous simultaneous writers.</summary>
    /// <param name="clip">The source asset whose values are copied at construction.</param>
    public PerformancePlayer(PerformanceClipAsset clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!double.IsFinite(clip.duration) || clip.duration <= 0)
            throw new ArgumentException("Performance duration must be finite and positive.", nameof(clip));
        if (clip.markers.Length > 1024 || clip.markers.Any(marker => string.IsNullOrWhiteSpace(marker.id) || marker.id.Length > 128
            || string.IsNullOrWhiteSpace(marker.name) || marker.name.Length > 256 || !double.IsFinite(marker.time) || marker.time < 0 || marker.time > clip.duration)
            || clip.markers.Select(static marker => marker.id).Distinct(StringComparer.Ordinal).Count() != clip.markers.Length)
            throw new ArgumentException("Performance markers require unique bounded identities, names and times within the clip.", nameof(clip));
        var owners = new HashSet<(string, PerformanceProperty)>();
        foreach (PerformanceTrack track in clip.tracks)
        {
            if (string.IsNullOrWhiteSpace(track.target) || !Enum.IsDefined(track.property)
                || !owners.Add((track.target, track.property)) || track.keys.Length == 0)
                throw new ArgumentException("Tracks require a unique target/property and at least one keyframe.", nameof(clip));
            double previous = -1;
            foreach (PerformanceKeyframe key in track.keys)
            {
                key.curve.Validate();
                if (!double.IsFinite(key.time) || !PerformancePropertyRules.IsValid(track.property, key.value) || key.time <= previous || key.time < 0
                    || key.time > clip.duration || !Enum.IsDefined(key.interpolation))
                    throw new ArgumentException("Keyframes must be finite, ordered, in range and valid for their channel.", nameof(clip));
                previous = key.time;
            }
        }
        m_duration = clip.duration;
        m_loop = clip.loop;
        m_tracks = clip.tracks.Select(static track => new PerformanceTrack
        {
            target = track.target, property = track.property,
            keys = track.keys.Select(static key => new PerformanceKeyframe { time = key.time, value = key.value, interpolation = key.interpolation, curve = key.curve.Copy() }).ToArray()
        }).ToArray();
    }

    /// <summary>Gets or sets whether ordinary updates advance time.</summary>
    public bool paused { get; set; }
    /// <summary>Gets whether a non-looping performance reached its end.</summary>
    public bool completed => !m_loop && m_elapsed >= m_duration;
    /// <summary>Gets the complete elapsed logical time.</summary>
    public double elapsed => m_elapsed;

    /// <summary>Advances a finite interval and returns the current samples.</summary>
    /// <param name="seconds">Nonnegative logical seconds.</param>
    /// <returns>The deterministic values at the new time.</returns>
    public IReadOnlyList<PerformanceSample> Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(m_elapsed + seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!paused) m_elapsed = m_loop ? m_elapsed + seconds : Math.Min(m_duration, m_elapsed + seconds);
        return Evaluate(m_elapsed);
    }

    /// <summary>Samples any nonnegative time without changing playback or executing side effects.</summary>
    /// <param name="seconds">The absolute logical time to sample.</param>
    /// <returns>One value per authored track.</returns>
    public IReadOnlyList<PerformanceSample> Evaluate(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        double time = m_loop ? seconds % m_duration : Math.Min(seconds, m_duration);
        return m_tracks.Select(track => new PerformanceSample(track.target, track.property, PerformanceKeyframeEvaluator.Sample(track.keys, time))).ToArray();
    }

    /// <summary>Captures the neutral logical position.</summary>
    /// <returns>A detached playback snapshot.</returns>
    public PerformanceState Capture() => new() { elapsed = m_elapsed, paused = paused };
    /// <summary>Restores a validated playback position atomically.</summary>
    /// <param name="state">A logical playback snapshot.</param>
    public void Restore(PerformanceState state)
    {
        if (!double.IsFinite(state.elapsed) || state.elapsed < 0 || !m_loop && state.elapsed > m_duration)
            throw new InvalidOperationException("Performance state is out of range.");
        m_elapsed = state.elapsed;
        paused = state.paused;
    }

}
