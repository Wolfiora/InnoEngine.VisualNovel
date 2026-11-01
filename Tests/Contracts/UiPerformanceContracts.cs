using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Core.Mathematics;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Interface.Performance;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Animation;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiPerformanceContracts
{
    internal static void SharedSessionClockAndRestoration(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var play = edits.Add(draft, "opening", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.Play, clipId = "mixed-ui", instanceId = "mix",
            bindings = [new() { slot = "visual", target = "stage/panel" }, new() { slot = "interface", target = "ui/reading/dialogue" }] }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId.value == "entry").id, play);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var clip = new PerformanceClipAsset { resourceId = "mixed-ui", duration = 2, tracks = [
            Track("visual", PerformanceProperty.X, 0, 100), Track("interface", PerformanceProperty.X, 0, 80),
            Track("interface", PerformanceProperty.ScaleX, 1, 1.2), Track("interface", PerformanceProperty.Opacity, 1, .5)] };
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, performances = [clip], initialStage = "room",
            stages = [new() { resourceId = "room", layers = [new() { id = "panel", width = 10, height = 10 }] }],
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding
            { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
        using var session = new NovelSession(game); using var view = new NovelGameView(game, session, Measure);
        session.StartNew(); view.BuildFrame(); view.Tick(.5);
        var frame = view.BuildFrame(); var item = frame.items.Single(static item => item.id == "dialogue");
        Near(Visual(item).x, 116); Near(item.opacity, .875); Near(Visual(item).width, item.bounds.width * 1.05);
        Near(session.stage.BuildFrame(1920, 1080).items.Single().topLeft.X, 25);
        Near(view.CaptureReadingFrame().items.Single(static item => item.id == "dialogue").opacity, .875);
        var snapshot = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        view.Tick(.5); session.Restore(snapshot);
        Near(view.BuildFrame().items.Single(static item => item.id == "dialogue").opacity, .875);
        session.stage.ApplyPerformance(new() { operation = PerformanceOperation.Pause, instanceId = "mix" }); view.Tick(.2);
        Near(view.BuildFrame().items.Single(static item => item.id == "dialogue").opacity, .875);
        session.stage.ApplyPerformance(new() { operation = PerformanceOperation.Seek, instanceId = "mix", time = 1.5 });
        Near(view.BuildFrame().items.Single(static item => item.id == "dialogue").opacity, .625);
        session.Restore(snapshot); session.Advance(); session.Advance(); view.Tick(.4); session.Choose("sea");
        Check(session.Rollback(), "UI timeline fixture could not roll back its decision.");
        Near(session.uiPerformance.GetValues("reading", session.stage.performances.Evaluate())["dialogue"].opacity, .875);
        session.Choose("sea");
        Near(view.BuildFrame().items.Single(static item => item.id == "dialogue").opacity, .875);
        session.preferences.reducedMotion = true; view.Tick(0);
        Near(view.BuildFrame().items.Single(static item => item.id == "dialogue").opacity, .5);
        session.stage.ApplyPerformance(new() { operation = PerformanceOperation.Cancel, instanceId = "mix" });
        Near(view.BuildFrame().items.Single(static item => item.id == "dialogue").opacity, 1);
        Check(!view.BuildFrame().items.Any(static item => item.id.StartsWith("outgoing/", StringComparison.Ordinal)), "Timeline restore retained unrelated page exit presentation.");
        var malformed = PerformanceClipCopy.Copy(clip); malformed.tracks[2].keys[0].value = 0; game.performances = [malformed];
        Reject(() => new NovelSession(game), play.value);
    }

    internal static void ContractsThemesAndRepeatedTemplates()
    {
        var button = new UiElementDefinition { id = "button", kind = UiElementKind.Button, action = "test", layout = new() { width = 100, height = 40 } };
        var page = new ScreenAsset { width = 400, height = 240, root = new() { id = "root", kind = UiElementKind.Vertical, blocksPointer = false,
            repeatBinding = "rows", layout = new() { width = 400, height = 240 }, children = [button] } };
        var game = new NovelProjectAsset { screens = [new() { role = "reading", screen = page }] };
        var ui = new NovelUiPerformance(game); var channels = ui.GetChannels();
        Check(channels["ui/reading/button"].Length == 4 && !channels["ui/reading/button"].Contains(PerformanceProperty.Volume), "UI advertised an unsupported channel.");
        channels["ui/reading/button"][0] = PerformanceProperty.Volume;
        Check(ui.GetChannels()["ui/reading/button"][0] == PerformanceProperty.X, "A public channel lookup exposed mutable owner state.");
        var values = ui.GetValues("reading", new Dictionary<(string target, PerformanceProperty property), double>
        { [("ui/reading/button", PerformanceProperty.X)] = 80, [("ui/reading/button", PerformanceProperty.ScaleX)] = 1.2 });
        var document = new UiDocumentInstance(page, Measure); var data = new Data(); document.SetPerformanceValues(values);
        values["button"].x = 999;
        var rows = document.BuildFrame(data, false).items.Where(static item => item.id != "root").ToArray();
        Check(rows.Length == 2 && rows.All(item => Math.Abs(Visual(item).x - 80) < .001 && Math.Abs(Visual(item).width - 120) < .001), "Repeated templates lost shared timeline presentation or retained mutable caller values.");
        document.HandleInput(new() { pointerInside = true, pressed = true, x = 180, y = 20 }, data);
        Check(document.HandleInput(new() { pointerInside = true, released = true, x = 180, y = 20 }, data).actions.Single().action == "test", "A timeline-scaled button did not receive matching pointer input.");
        document.SetPerformanceValues(new Dictionary<string, Model.Interface.Animation.UiMotionPose>());
        Check(document.BuildFrame(data, false).items.Where(static item => item.id != "root").All(item => Visual(item).x == 0), "Removing timeline ownership retained stale UI channels.");
        var variant = UiDefinitionCopy.Copy(page); variant.root.children[0].id = "other";
        game.themes = [new() { resourceId = "alternate", screens = [new() { role = "reading", screen = variant }] }];
        var themed = new NovelUiPerformance(game);
        Check(!themed.targets.Any(static item => item.id == "ui/reading/button"), "A theme-dependent missing target was advertised as stable.");
        var clip = new PerformanceClipAsset { duration = 2, tracks = [Track("ui/reading/button", PerformanceProperty.X, 0, 10)] };
        Reject(() => themed.ValidateBinding(clip, [], "source/node"), "source/node");
        clip.tracks = [Track("ui/reading/root", PerformanceProperty.Volume, 0, 1)]; Reject(() => themed.ValidateBinding(clip, [], "wrong-channel"), "wrong-channel");
        Reject(() => ui.GetValues("reading", new Dictionary<(string, PerformanceProperty), double> { [("ui/reading/missing", PerformanceProperty.X)] = 1 }), "missing");
    }

    private static PerformanceTrack Track(string target, PerformanceProperty property, double from, double to) => new()
    { target = target, property = property, keys = [new() { value = from }, new() { time = 2, value = to }] };
    private static Rect Visual(UiDrawItem item) => UiMotionSampling.Bounds(item.bounds, item.presentation ?? Matrix.identity);
    private static (float width, float height) Measure(UiElementDefinition element, string text) => (text.Length * 8, 20);
    private static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < .001, $"UI timeline expected {expected}, got {actual}.");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action, string source)
    { try { action(); } catch (InvalidOperationException error) when (error.Message.Contains(source, StringComparison.Ordinal)) { return; } throw new InvalidOperationException("Invalid UI timeline was not rejected at its source: " + source); }
    private sealed class Data : UiDataSource
    {
        public override bool TryRead(string path, out NovelValue value) { value = NovelValue.Boolean(false); return false; }
        public override bool TryWrite(string path, NovelValue value) => false;
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => [new("a", new Dictionary<string, NovelValue>()), new("b", new Dictionary<string, NovelValue>())];
    }
}
