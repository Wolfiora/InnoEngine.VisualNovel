using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class AudioTimelineContracts
{
    internal static void Run(
        AudioCueAsset imported,
        SerializationRegistry native
    ) {
        var serializer = new EditorStorySerialization(native); var codec = new StoryNodeDataCodec(serializer); var compiler = new StoryCompiler(serializer); var edits = new StoryEditService(codec);
        var draft = FirstStoryTemplate.Create(codec);
        var performance = edits.Add(draft, "opening", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.Play, clipId = "mixed", instanceId = "mix",
            bindings = [new() { slot = "visual", target = "stage/panel" }, new() { slot = "sound", target = "audio/bgm" }] }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId.value == "entry").id, performance);
        var play = edits.Add(draft, "opening", StoryNodeIds.audio, new AudioNode { operation = AudioOperation.Play, cueId = "tone", slot = "bgm" }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId == performance).id, play);
        var chapter = draft.ToAsset(); var result = compiler.Compile(chapter); Check(result.success, string.Join(";", result.diagnostics.Select(static item => item.message))); chapter.program = result.program;
        var clip = new PerformanceClipAsset { resourceId = "mixed", duration = 1, essential = false, markers = [new() { id = "half", name = "Half", time = .5 }], tracks = [
            new() { target = "visual", property = PerformanceProperty.X, keys = [new(), new() { time = 1, value = 100 }] },
            new() { target = "sound", property = PerformanceProperty.Volume, keys = [new(), new() { time = 1, value = 1 }] }] };
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, initialStage = "room",
            stages = [new() { resourceId = "room", layers = [new() { id = "panel", width = 10, height = 10 }] }], performances = [clip],
            audio = [new() { resourceId = "tone", clip = imported.clip, volume = .8, loop = true, channel = NovelAudioChannel.Bgm }] };
        var output = new Output(); using var session = new NovelSession(game, output); session.preferences.masterVolume = 1; session.StartNew();
        Check(session.story!.status == StoryStatus.Dialogue && output.gains.Count == 1 && output.gains.Values.Single() == 0,
            "Volume performance did not precede the same-pump audio start: " + session.story.status + " / " + session.story.error + " / " + string.Join(",", output.gains.Values));
        session.Tick(.25); Check(Near(output.gains.Values.Single(), .2) && session.stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 25,
            "Audio and visual tracks did not share one logical clock: gain=" + string.Join(",", output.gains.Values) + "; x=" + session.stage.BuildFrame(1920, 1080).items.Single().topLeft.X + "; status=" + session.story.status + "; " + session.story.error);
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        session.Tick(.5); var prior = output.gains.ToDictionary();
        output.rejectPlay = true; bool rejected = false;
        try { session.Restore(saved); } catch (InvalidOperationException) { rejected = true; }
        output.rejectPlay = false;
        Check(rejected && output.gains.OrderBy(static pair => pair.Key).SequenceEqual(prior.OrderBy(static pair => pair.Key))
            && session.stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 75, "Failed candidate audio changed live gain or timeline state.");
        session.Restore(saved); Check(Near(output.gains.Values.Single(), .2), "Restored voices started with the old timeline multiplier.");
        session.Advance(); session.Advance(); Check(session.story.status == StoryStatus.Choice, "Audio timeline fixture did not reach its choice boundary.");
        session.Tick(.5); session.Choose("sea"); Check(session.Rollback() && Near(output.gains.Values.Single(), .2), "Rollback did not reconstruct the saved timeline mix.");
        session.preferences.reducedMotion = true; session.Tick(0);
        Check(session.stage.BuildFrame(1920, 1080).items.Single().topLeft.X == 100 && Near(output.gains.Values.Single(), .2)
            && !session.stage.HasReachedPerformanceMarker("mix", "half"), "Reduced motion skipped audible timing or retained optional visual movement.");
        session.Tick(.25); Check(Near(output.gains.Values.Single(), .4) && session.stage.HasReachedPerformanceMarker("mix", "half"), "Audible marker wait did not retain its logical time.");
        session.preferences.masterVolume = .5; session.Tick(0); Check(Near(output.gains.Values.Single(), .2), "Timeline volume replaced current player preferences.");
        session.stage.ApplyPerformance(new() { operation = PerformanceOperation.Cancel, instanceId = "mix" }); session.Tick(0);
        Check(Near(output.gains.Values.Single(), .4), "Cancelled automation retained a stale audio multiplier.");
        using var noAudio = new NovelSession(game); noAudio.StartNew();
        Check(noAudio.story!.status == StoryStatus.Faulted && noAudio.story.error.Contains(performance.value, StringComparison.Ordinal)
            && noAudio.stage.performances.Capture().Length == 0, "Missing audio adapter silently accepted volume tracks or lost source diagnostics.");
        var damaged = session.stage.Capture(); damaged.performances = saved.stage.performances;
        damaged.performances[0].bindings.Single(item => item.slot == "sound").target = "camera";
        rejected = false; try { session.stage.Restore(damaged); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && session.stage.performances.Capture().Length == 0, "Audio channel bound to an unsupported visual target.");
    }
    private sealed class Output : INovelAudioOutput
    {
        internal readonly Dictionary<long, double> gains = [];
        internal bool rejectPlay; private long m_next;
        public long Play(string cueId, double gain, bool loop, double position)
        { if (rejectPlay) throw new InvalidOperationException("Injected candidate output failure."); long id = ++m_next; gains.Add(id, gain); return id; }
        public void Stop(long voice) => gains.Remove(voice);
        public void SetGain(long voice, double gain) => gains[voice] = gain;
        public void SetPaused(long voice, bool paused) { }
        public bool IsActive(long voice) => gains.ContainsKey(voice);
    }
    private static bool Near(double left, double right) => Math.Abs(left - right) < 1e-9;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
