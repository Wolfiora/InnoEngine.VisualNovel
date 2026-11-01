using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Player.Collections;
using Inno.VisualNovel.Player.Performance;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ExtensionPerformanceContracts
{
    internal static void SharedClockNativeRestoreRollbackAndReplay(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Create(codec, compiler);
        var channels = new[] { PerformanceProperty.X, PerformanceProperty.Opacity };
        var declarations = new Dictionary<string, PerformanceProperty[]> { ["extension/rig"] = channels };
        var targets = new NovelPerformanceTargets(declarations); channels[0] = PerformanceProperty.Rotation; declarations.Clear();
        using var session = new NovelSession(game, options: new() { performanceTargets = targets });
        Check(!session.TryGetExtensionPerformance("extension/rig", PerformanceProperty.X, out _) && session.CaptureExtensionPerformance().Count == 0,
            "A declared channel invented a writer before story playback.");
        session.StartNew(); session.Tick(.5); Near(Value(session, PerformanceProperty.X), 25); Near(Value(session, PerformanceProperty.Opacity), .25);
        var samples = session.CaptureExtensionPerformance();
        Check(samples.Count == 2 && samples.All(static sample => sample.target == "extension/rig") && targets.targets["extension/rig"].Contains(PerformanceProperty.X),
            "Extension samples included built-in targets or retained caller-owned capability arrays.");
        var state = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        using (var replay = new ReplaySession(game, session, "replay"))
        {
            replay.session.Tick(.25); Near(Value(replay.session, PerformanceProperty.X), 12.5); Near(Value(session, PerformanceProperty.X), 25);
            replay.Return();
        }
        session.Tick(.5); session.Restore(state); Near(Value(session, PerformanceProperty.X), 25);
        session.Advance(); session.Advance(); session.Tick(.25);
        Check(session.canRollback && session.Rollback(), "Extension timelines lost ordinary interactive rollback boundaries.");
        Near(Value(session, PerformanceProperty.X), 25);
        Check(session.story?.sourceNodeId == "hello", "Extension rollback did not restore the loaded dialogue checkpoint.");
        session.stage.ApplyPerformance(new() { operation = PerformanceOperation.Seek, instanceId = "bridge", time = 2 });
        Near(Value(session, PerformanceProperty.X), 100);
        session.stage.ApplyPerformance(new() { operation = PerformanceOperation.Cancel, instanceId = "bridge" });
        Check(session.CaptureExtensionPerformance().Count == 0 && !session.TryGetExtensionPerformance("extension/rig", PerformanceProperty.X, out _)
            && !session.TryGetExtensionPerformance("camera", PerformanceProperty.X, out _), "Cancelled or undeclared channels retained a stale external projection.");
    }
    internal static void FrozenCapabilitiesAndExactSaveIdentity(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Create(codec, compiler);
        var a = new NovelPerformanceTargets(new Dictionary<string, PerformanceProperty[]> { ["extension/rig"] = [PerformanceProperty.X, PerformanceProperty.Opacity] });
        var b = new NovelPerformanceTargets(new Dictionary<string, PerformanceProperty[]> { ["extension/rig"] = [PerformanceProperty.Opacity, PerformanceProperty.X] });
        Check(a.fingerprint == b.fingerprint, "Capability ordering changed an equivalent extension contract.");
        using var first = new NovelSession(game, options: new() { performanceTargets = a }); first.StartNew(); first.Tick(.5);
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(first.Capture()));
        using var equivalent = new NovelSession(game, options: new() { performanceTargets = b }); equivalent.StartNew(); equivalent.Restore(saved);
        Near(Value(equivalent, PerformanceProperty.X), 25);
        var changed = new NovelPerformanceTargets(new Dictionary<string, PerformanceProperty[]> { ["extension/rig"] = [PerformanceProperty.X, PerformanceProperty.Opacity, PerformanceProperty.ScaleX] });
        using var other = new NovelSession(game, options: new() { performanceTargets = changed }); other.StartNew();
        Check(other.buildFingerprint != first.buildFingerprint, "A changed host channel contract kept the same exact-build save identity.");
        Reject(() => other.Restore(saved)); Near(Value(other, PerformanceProperty.X), 0);
        Reject(() => new NovelSession(game));
        Reject(() => new NovelSession(game, options: new() { performanceTargets = new(new Dictionary<string, PerformanceProperty[]> { ["extension/rig"] = [PerformanceProperty.X] }) }));
        Reject(() => new NovelPerformanceTargets(new Dictionary<string, PerformanceProperty[]> { ["audio/music"] = [PerformanceProperty.X] }));
        Reject(() => new NovelPerformanceTargets(new Dictionary<string, PerformanceProperty[]> { ["extension/"] = [PerformanceProperty.X] }));
        Reject(() => new NovelPerformanceTargets(new Dictionary<string, PerformanceProperty[]> { ["extension/rig"] = [PerformanceProperty.X, PerformanceProperty.X] }));
        Reject(() => new NovelPerformanceTargets(new Dictionary<string, PerformanceProperty[]> { ["extension/rig"] = [(PerformanceProperty)999] }));
        var directGame = Create(codec, compiler, true);
        using var direct = new NovelSession(directGame, options: new() { performanceTargets = a }); direct.StartNew(); direct.Tick(.5);
        Near(Value(direct, PerformanceProperty.X), 25);
        Reject(() => new NovelSession(directGame));
    }
    private static NovelProjectAsset Create(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        bool directTarget = false
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var play = edits.Add(draft, "opening", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.Play, clipId = "bridge", instanceId = "bridge",
            bindings = directTarget ? [] : [new() { slot = "signal", target = "extension/rig" }] }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").id, play);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program ?? throw new InvalidOperationException("Extension performance fixture did not compile.");
        return new() { chapters = [chapter], startChapter = chapter.chapterId, performances = [new() { resourceId = "bridge", duration = 2, tracks = [
            new() { target = directTarget ? "extension/rig" : "signal", property = PerformanceProperty.X, keys = [new(), new() { time = 2, value = 100 }] },
            new() { target = directTarget ? "extension/rig" : "signal", property = PerformanceProperty.Opacity, keys = [new(), new() { time = 2, value = 1 }] }] }],
            collections = new() { entries = [new() { id = "replay", kind = CollectionKind.Replay, chapterId = chapter.chapterId, initiallyUnlocked = true }] } };
    }
    private static double Value(
        NovelSession session,
        PerformanceProperty property
    ) => session.TryGetExtensionPerformance("extension/rig", property, out double value) ? value : throw new InvalidOperationException("Missing extension writer: " + property);
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("An invalid extension timeline contract or incompatible snapshot was accepted.");
    }
    private static void Near(
        double actual,
        double expected
    ) { if (Math.Abs(actual - expected) > .00001) throw new InvalidOperationException("Extension timeline value differs: " + actual + " vs " + expected); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
