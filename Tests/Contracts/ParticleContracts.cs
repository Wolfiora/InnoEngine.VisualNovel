using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Stage.Effects;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ParticleContracts
{
    internal static void PureSeedAndTime()
    {
        var preset = Preset(); var simulator = new ParticleSimulator(preset);
        var state = new ParticleEmitterState { id = "rain", presetId = preset.resourceId, elapsed = 2, seed = 17 };
        var a = simulator.Evaluate(state).ToArray();
        Check(a.Length > 0 && a.SequenceEqual(simulator.Evaluate(state)), "Particle evaluation consumed random state or depended on prior frames.");
        state.seed = 18; Check(!a.SequenceEqual(simulator.Evaluate(state)), "Different seeds produced identical particles.");
        var whole = new StageController([], particlePresets: [preset]); var divided = new StageController([], particlePresets: [preset]);
        whole.particles.Start("rain", preset.resourceId, 17); divided.particles.Start("rain", preset.resourceId, 17);
        whole.Tick(2); for (int step = 0; step < 16; step++) divided.Tick(0.125);
        Check(whole.BuildFrame(1920, 1080).items.SequenceEqual(divided.BuildFrame(1920, 1080).items), "Equivalent logical time changed particle geometry with frame subdivision.");
        whole.particles.Stop("rain"); whole.Tick(preset.particles.maximumLifetime);
        Check(whole.particles.Capture().Length == 0 && !whole.hasContent, "Stopped particles did not expire and release their owner.");
        preset.particles.rate = 10000; bool rejected = false;
        try { _ = new ParticleSimulator(preset); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "An unbounded live-particle budget reached rendering.");
    }

    internal static void NativeRestoreAndMotion(SerializationRegistry native)
    {
        var preset = Preset(); var stage = new StageController([], particlePresets: [preset]); stage.particles.Start("rain", preset.resourceId, 19); stage.Tick(1);
        var before = stage.BuildFrame(1920, 1080).items.ToArray();
        StageState saved = native.Deserialize<StageState>(native.Serialize(stage.Capture()));
        stage.Tick(1); stage.Restore(saved);
        Check(stage.BuildFrame(1920, 1080).items.SequenceEqual(before), "Native restoration changed seeded particle geometry or time.");
        stage.Tick(1, true); Check(stage.BuildFrame(1920, 1080).items.Count == 0, "Reduced motion did not suppress an optional emitter.");
        stage.Tick(0, false); Check(stage.BuildFrame(1920, 1080).items.SequenceEqual(before), "Reduced motion consumed suspended emitter time.");
        saved.particles[0].seed = 0;
        bool rejected = false; try { stage.Restore(saved); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && stage.BuildFrame(1920, 1080).items.SequenceEqual(before), "Invalid particle restoration replaced the active composition.");
        stage.particles.Remove("rain"); Check(!stage.hasContent, "Immediate removal retained particles.");
    }

    internal static void SessionFlow(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var start = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.StartParticles, resourceId = "rain", target = "weather", seed = 23 }, new(0, 200));
        var wait = edits.Add(draft, "opening", StoryNodeIds.wait, new WaitNode { seconds = 1 }, new(0, 300));
        var stop = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.StopParticles, target = "weather" }, new(0, 400));
        edits.Connect(draft, new(new("entry"), new("next")), start); edits.Connect(draft, new(start, new("next")), wait);
        edits.Connect(draft, new(wait, new("next")), stop); edits.Connect(draft, new(stop, new("next")), new("hello"));
        var chapter = draft.ToAsset(); var compilation = compiler.Compile(chapter);
        Check(compilation.success, string.Join("; ", compilation.diagnostics.Select(static item => item.message))); chapter.program = compilation.program;
        var preset = Preset(); var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, particles = [preset] };
        using var session = new NovelSession(game); session.StartNew(); session.Tick(0.5);
        Check(session.story!.status == StoryStatus.Timer && session.stage.hasContent, "Particle start did not enter the shared presentation pipeline.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        var before = session.stage.BuildFrame(1920, 1080).items.ToArray();
        preset.particles.maximumVelocityX = 10000;
        session.Tick(0.5); Check(session.story.status == StoryStatus.Dialogue && session.stage.particles.Capture()[0].stopped, "Narrative stop did not stop births.");
        session.Restore(saved);
        Check(session.stage.BuildFrame(1920, 1080).items.SequenceEqual(before), "Restored particles retained a mutable source preset alias.");
        using var missing = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId }); missing.StartNew();
        Check(missing.story!.status == StoryStatus.Faulted && missing.story.error.Contains(start.value, StringComparison.Ordinal), "Missing particle presets lost source diagnostics.");
    }

    internal static void CompletionWait(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var start = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.StartParticles, resourceId = "rain", target = "weather" }, new(0, 200));
        var wait = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.WaitParticles, target = "weather" }, new(0, 300));
        edits.Connect(draft, new(new("entry"), new("next")), start); edits.Connect(draft, new(start, new("next")), wait); edits.Connect(draft, new(wait, new("next")), new("hello"));
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var preset = Preset(); preset.particles.duration = 0.25; preset.particles.minimumLifetime = 0.1; preset.particles.maximumLifetime = 0.15;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, particles = [preset] };
        using var session = new NovelSession(game); session.StartNew();
        Check(session.story!.status == StoryStatus.Presentation, "Particle completion wait did not suspend narrative playback.");
        session.Tick(0.5); Check(session.story.status == StoryStatus.Dialogue, "Expired finite particles did not release their wait.");
        using var reduced = new NovelSession(game); reduced.preferences.reducedMotion = true; reduced.StartNew(); reduced.Tick(0);
        Check(reduced.story!.status == StoryStatus.Dialogue, "Suppressed decorative particles left an unfinishable narrative wait.");
    }

    internal static VisualEffectAsset Preset() => new() { resourceId = "rain", particles = new() { rate = 20, minimumLifetime = 2, maximumLifetime = 3, width = 100, height = 100 } };
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
