using System;
using System.Linq;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Reflection;
using Inno.VisualNovel.Integration.Performance;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Applies pure clip samples to explicitly bound native components during the owning System update.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c08")]
public sealed class NovelPerformanceComponent : GameComponent
{
    /// <summary>Gets or sets the reusable clip. Position tracks use native local XY coordinates; rotation is planar degrees.</summary>
    [SerializableProperty] public PerformanceClipAsset? clip { get; set; }
    /// <summary>Gets or sets explicit object references for the reusable clip's target slots.</summary>
    [SerializableProperty] public NovelPerformanceBinding[] bindings { get; set; } = [];
    /// <summary>Gets or sets whether runtime updates apply this writer; external scripts own its properties while stopped.</summary>
    [SerializableProperty] public bool playing { get; set; }
    /// <summary>Gets or sets whether an active writer retains its current logical position.</summary>
    [SerializableProperty] public bool paused { get; set; }
    /// <summary>Gets or sets the neutral playback clock. Use Seek to explicitly apply a new authoring position.</summary>
    [SerializableProperty] public double elapsed { get; set; }

    /// <summary>Validates the complete candidate, replaces conflicting active writers, and applies the clip's first sample.</summary>
    /// <param name="change">Optional synchronous engine property-history adapter; never retained.</param>
    public void Play(Action<EngineObject, string, Action>? change = null)
    {
        var writes = NovelPerformanceWrites.Prepare(this, 0);
        var channels = writes.Select(static write => (write.target, write.channel)).ToHashSet();
        var replaced = gameObject.scene.GetObjects().SelectMany(static item => item.GetComponents<NovelPerformanceComponent>())
            .Where(other => !ReferenceEquals(other, this) && other.playing && other.gameObject.activeInHierarchy
                && NovelPerformanceWrites.Prepare(other, other.elapsed).Any(write => channels.Contains((write.target, write.channel)))).ToArray();
        foreach (var other in replaced) Edit(other, nameof(playing), () => other.playing = false);
        foreach (var write in writes) Edit(write.owner, write.property, write.apply);
        Edit(this, nameof(elapsed), () => elapsed = 0);
        Edit(this, nameof(paused), () => paused = false);
        Edit(this, nameof(playing), () => playing = true);
        void Edit(EngineObject owner, string property, Action mutation) { if (change is null) mutation(); else change(owner, property, mutation); }
    }

    /// <summary>Applies a deterministic position after validating every target, without executing story or external side effects.</summary>
    /// <param name="seconds">Finite nonnegative clip time.</param>
    /// <param name="change">Optional synchronous engine property-history adapter; never retained.</param>
    public void Seek(
        double seconds,
        Action<EngineObject, string, Action>? change = null
    ) {
        var writes = NovelPerformanceWrites.Prepare(this, seconds);
        double position = clip!.loop ? seconds : Math.Min(seconds, clip.duration);
        foreach (var write in writes) Edit(write.owner, write.property, write.apply);
        Edit(this, nameof(elapsed), () => elapsed = position);
        void Edit(EngineObject owner, string property, Action mutation) { if (change is null) mutation(); else change(owner, property, mutation); }
    }

    /// <summary>Releases property ownership while keeping the current authored component values.</summary>
    public void Stop() => playing = false;

    internal void Tick(double seconds)
    {
        if (!playing || !gameObject.activeInHierarchy) return;
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new InvalidOperationException("Performance time must be finite and nonnegative.");
        Seek(paused ? elapsed : elapsed + seconds);
        if (!clip!.loop && elapsed >= clip.duration) playing = false;
    }

    internal static void ValidateWriters(GameScene scene)
    {
        var owners = new System.Collections.Generic.HashSet<(GameObject, PerformanceProperty)>();
        foreach (var performance in scene.GetObjects().Where(static item => item.activeInHierarchy)
            .SelectMany(static item => item.GetComponents<NovelPerformanceComponent>()).Where(static item => item.playing))
            foreach (var write in NovelPerformanceWrites.Prepare(performance, performance.elapsed))
                if (!owners.Add((write.target, write.channel)))
                    throw new InvalidOperationException("Conflicting native performance writers. Start a replacement with Play before updating the scene.");
    }
}
