using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Stage.Timeline;

/// <summary>Owns deterministic performance instances and unambiguous stage-property writers.</summary>
public sealed class StagePerformanceController
{
    private sealed record Clip(PerformancePlayer player, double duration, bool loop, bool essential, PerformanceTrack[] tracks, IReadOnlyDictionary<string, double> markers);
    private readonly Dictionary<string, Clip> m_clips;
    private readonly Dictionary<string, PerformanceProperty[]> m_channels;
    private bool m_reducedMotion;
    private List<StagePerformanceState> m_active = [];
    /// <summary>Freezes explicit clip declarations for one session generation.</summary>
    /// <param name="clips">The imported performance resource library.</param>
    /// <param name="additionalTargets">Owner-supplied non-stage target IDs and their explicit supported scalar channels.</param>
    public StagePerformanceController(
        IEnumerable<PerformanceClipAsset> clips,
        IReadOnlyDictionary<string, PerformanceProperty[]>? additionalTargets = null
    )
    {
        m_channels = (additionalTargets ?? new Dictionary<string, PerformanceProperty[]>()).ToDictionary(static pair => pair.Key, static pair => pair.Value.ToArray(), StringComparer.Ordinal);
        if (m_channels.Count > 4096 || m_channels.Any(static pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value.Length == 0
            || pair.Value.Any(static property => !Enum.IsDefined(property)) || pair.Value.Distinct().Count() != pair.Value.Length))
            throw new ArgumentException("Additional performance targets require finite explicit channel capabilities.", nameof(additionalTargets));
        m_clips = clips.ToDictionary(static clip => clip.resourceId, static clip => new Clip(new(clip), clip.duration, clip.loop, clip.essential,
            clip.tracks.Select(static track => new PerformanceTrack { target = track.target, property = track.property,
                keys = track.keys.Select(static key => new PerformanceKeyframe { value = key.value }).ToArray() }).ToArray(),
            clip.markers.ToDictionary(static marker => marker.id, static marker => marker.time, StringComparer.Ordinal)), StringComparer.Ordinal);
        if (m_clips.Keys.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Performance resource keys cannot be empty.", nameof(clips));
    }
    /// <summary>Reports whether a named performance still has logical playback remaining.</summary>
    /// <param name="id">The active instance identity.</param>
    /// <returns>True for paused or advancing unfinished playback.</returns>
    public bool IsActive(string id) => m_active.Any(item => item.instanceId == id && (m_clips[item.clipId].loop || item.playback.elapsed < m_clips[item.clipId].duration));
    /// <summary>Checks first marker arrival without emitting callbacks during seek or replay.</summary>
    /// <param name="id">The instance identity; absent or cancelled instances release their wait.</param>
    /// <param name="marker">The declared marker identity; unknown markers on active clips are diagnosed.</param>
    /// <param name="reducedMotion">Whether a suppressed optional clip releases the wait immediately.</param>
    /// <returns>Whether first arrival has occurred or waiting is no longer necessary.</returns>
    public bool HasReachedMarker(
        string id,
        string marker,
        bool reducedMotion = false
    ) {
        if (string.IsNullOrWhiteSpace(marker)) throw new ArgumentException("A performance marker identity is required.", nameof(marker));
        var state = m_active.SingleOrDefault(item => item.instanceId == id); if (state is null) return true;
        var clip = m_clips[state.clipId];
        if (!clip.markers.TryGetValue(marker, out double time)) throw new InvalidOperationException("The active performance has no marker: " + state.clipId + "/" + marker);
        return reducedMotion && !clip.essential && !clip.tracks.Any(static track => track.property == PerformanceProperty.Volume) || state.playback.elapsed >= time;
    }
    /// <summary>Applies a lifecycle action after validating all candidate targets and property ownership.</summary>
    /// <param name="request">The detached lifecycle request.</param>
    /// <param name="targets">The currently available concrete stage owners, including camera.</param>
    public void Apply(
        PerformanceNode request,
        IReadOnlyCollection<string> targets
    ) {
        if (!Enum.IsDefined(request.operation) || string.IsNullOrWhiteSpace(request.instanceId)) throw new ArgumentException("Performance operation or identity is invalid.");
        if (request.operation == PerformanceOperation.Play)
        {
            var candidate = new StagePerformanceState { instanceId = request.instanceId, clipId = request.clipId, bindings = request.bindings.Select(Copy).ToArray() };
            Validate(candidate, targets);
            var writes = Writers(candidate).ToHashSet();
            var retained = m_active.Where(item => item.instanceId != candidate.instanceId && !Writers(item).Any(writes.Contains)).ToList();
            if (retained.Count >= 128) throw new InvalidOperationException("The active performance budget of 128 was exceeded.");
            retained.Add(candidate); m_active = retained; return;
        }
        if (request.operation == PerformanceOperation.Cancel) { m_active.RemoveAll(item => item.instanceId == request.instanceId); return; }
        if (request.operation is PerformanceOperation.Wait or PerformanceOperation.WaitMarker) throw new ArgumentException("Performance waits are narrative boundaries.", nameof(request));
        StagePerformanceState state = m_active.SingleOrDefault(item => item.instanceId == request.instanceId) ?? throw new InvalidOperationException("The requested performance is not present.");
        if (request.operation == PerformanceOperation.Pause) state.playback.paused = true;
        else if (request.operation == PerformanceOperation.Resume) state.playback.paused = false;
        else if (request.operation == PerformanceOperation.Seek)
        {
            if (!double.IsFinite(request.time) || request.time < 0) throw new ArgumentOutOfRangeException(nameof(request));
            state.playback.elapsed = m_clips[state.clipId].loop ? request.time : Math.Min(request.time, m_clips[state.clipId].duration);
        }
    }
    /// <summary>Advances logical time without invoking scripts, storage, unlocks or other side effects.</summary>
    /// <param name="seconds">A finite nonnegative interval.</param>
    /// <param name="reducedMotion">Whether optional motion should complete or remain at its static initial sample.</param>
    public void Tick(
        double seconds,
        bool reducedMotion = false
    ) {
        if (!double.IsFinite(seconds) || seconds < 0 || m_active.Any(item => !double.IsFinite(item.playback.elapsed + seconds))) throw new ArgumentOutOfRangeException(nameof(seconds));
        m_reducedMotion = reducedMotion;
        foreach (StagePerformanceState state in m_active.Where(static item => !item.playback.paused))
        {
            Clip clip = m_clips[state.clipId];
            state.playback.elapsed = reducedMotion && !clip.essential && !clip.tracks.Any(static track => track.property == PerformanceProperty.Volume) ? clip.loop ? 0 : clip.duration
                : clip.loop ? state.playback.elapsed + seconds : Math.Min(state.playback.elapsed + seconds, clip.duration);
        }
    }
    /// <summary>Samples each owned property at the current logical time.</summary>
    /// <returns>Exactly one finite value per resolved target and property.</returns>
    public IReadOnlyDictionary<(string target, PerformanceProperty property), double> Evaluate()
    {
        var result = new Dictionary<(string target, PerformanceProperty property), double>();
        foreach (StagePerformanceState state in m_active)
        {
            Clip clip = m_clips[state.clipId];
            var current = clip.player.Evaluate(state.playback.elapsed);
            var still = m_reducedMotion && !clip.essential ? clip.player.Evaluate(clip.loop ? 0 : clip.duration) : current;
            for (int index = 0; index < current.Count; index++)
            {
                var sample = current[index];
                result.Add((Resolve(state, sample.target), sample.property), sample.property == PerformanceProperty.Volume ? sample.value : still[index].value);
            }
        }
        return result;
    }
    /// <summary>Reports whether an available clip declares a particular scalar channel without starting it.</summary>
    /// <param name="clipId">The explicit resource key.</param>
    /// <param name="property">The channel required by a presentation adapter.</param>
    /// <returns>Whether the declared clip contains that channel.</returns>
    public bool HasChannel(
        string clipId,
        PerformanceProperty property
    ) => m_clips.TryGetValue(clipId, out var clip) && clip.tracks.Any(track => track.property == property);
    /// <summary>Captures active and completed property owners without retaining clip instances.</summary>
    /// <returns>Detached neutral states in stable submission order.</returns>
    public StagePerformanceState[] Capture() => m_active.Select(Copy).ToArray();
    /// <summary>Validates an entire candidate set before replacing property ownership and playback.</summary>
    /// <param name="snapshot">The complete neutral timeline state.</param>
    /// <param name="targets">Available concrete stage owners.</param>
    public void Restore(
        StagePerformanceState[] snapshot,
        IReadOnlyCollection<string> targets
    ) {
        if (snapshot.Length > 128 || snapshot.Select(static item => item.instanceId).Distinct(StringComparer.Ordinal).Count() != snapshot.Length) throw new InvalidOperationException("Performance snapshot identities or budget are invalid.");
        var candidate = snapshot.Select(Copy).ToList();
        var writes = new HashSet<(string target, PerformanceProperty property)>();
        foreach (StagePerformanceState state in candidate)
        {
            Validate(state, targets);
            foreach (var writer in Writers(state)) if (!writes.Add(writer)) throw new InvalidOperationException("Restored performances compete for a target property.");
        }
        m_active = candidate;
    }
    /// <summary>Retains only performances whose entire target set remains available.</summary>
    /// <param name="snapshot">The candidate composition's timeline states.</param>
    /// <param name="targets">Available owners after a scene or character removal.</param>
    /// <returns>Detached states without orphaned target writers.</returns>
    public StagePerformanceState[] RetainTargets(
        StagePerformanceState[] snapshot,
        IReadOnlyCollection<string> targets
    ) => snapshot.Where(item => Writers(item).All(writer => targets.Contains(writer.target))).Select(Copy).ToArray();
    private void Validate(
        StagePerformanceState state,
        IReadOnlyCollection<string> targets
    ) {
        if (string.IsNullOrWhiteSpace(state.instanceId) || !m_clips.TryGetValue(state.clipId, out Clip? clip)
            || !double.IsFinite(state.playback.elapsed) || state.playback.elapsed < 0 || !clip.loop && state.playback.elapsed > clip.duration
            || state.bindings.Select(static item => item.slot).Distinct(StringComparer.Ordinal).Count() != state.bindings.Length
            || state.bindings.Any(item => string.IsNullOrWhiteSpace(item.target) || !clip.tracks.Any(track => track.target == item.slot)))
            throw new InvalidOperationException("Performance resources, bindings or playback positions are invalid.");
        var writers = Writers(state).ToArray();
        if (writers.Distinct().Count() != writers.Length || writers.Any(writer => !targets.Contains(writer.target)
            || !Supports(writer.target, writer.property))
            || clip.tracks.Any(track => Resolve(state, track.target) == "camera" && track.property == PerformanceProperty.ScaleX && track.keys.Any(static key => key.value <= 0)))
            throw new InvalidOperationException("Performance bindings have unavailable targets, unsupported camera channels or competing property writers.");
    }
    private bool Supports(
        string target,
        PerformanceProperty property
    ) => m_channels.TryGetValue(target, out var allowed) ? allowed.Contains(property)
        : SupportsStageProperty(property, target == "camera");

    /// <summary>Reports the implemented transform channels for no-code target filtering and runtime validation.</summary>
    /// <param name="property">The scalar channel selected in a performance track.</param>
    /// <param name="camera">Whether the candidate target is the camera rather than a stage object.</param>
    /// <returns>Whether this native stage target can apply the channel.</returns>
    public static bool SupportsStageProperty(
        PerformanceProperty property,
        bool camera = false
    ) => camera ? property is PerformanceProperty.X or PerformanceProperty.Y or PerformanceProperty.ScaleX or PerformanceProperty.Rotation or PerformanceProperty.ShakeX or PerformanceProperty.ShakeY
        : property is PerformanceProperty.X or PerformanceProperty.Y or PerformanceProperty.ScaleX or PerformanceProperty.ScaleY or PerformanceProperty.Rotation or PerformanceProperty.Opacity or PerformanceProperty.ShakeX or PerformanceProperty.ShakeY;
    private IEnumerable<(string target, PerformanceProperty property)> Writers(StagePerformanceState state) => m_clips[state.clipId].tracks.Select(track => (Resolve(state, track.target), track.property));
    private static string Resolve(StagePerformanceState state, string slot) => state.bindings.SingleOrDefault(item => item.slot == slot)?.target ?? slot;
    private static PerformanceBinding Copy(PerformanceBinding binding) => new() { slot = binding.slot, target = binding.target };
    private static StagePerformanceState Copy(StagePerformanceState state) => new() { instanceId = state.instanceId, clipId = state.clipId, bindings = state.bindings.Select(Copy).ToArray(), playback = new() { elapsed = state.playback.elapsed, paused = state.playback.paused } };
}
