using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Player.Effects;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Stage.Effects;
using Inno.VisualNovel.Stage.Timeline;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class EffectTimelineContracts
{
    internal static void MechanismsNativeStateAndAtomicBounds(SerializationRegistry native)
    {
        foreach (ScreenEffectKind kind in Enum.GetValues<ScreenEffectKind>())
        {
            var preset = new VisualEffectAsset { resourceId = "preset", kind = VisualEffectKind.Screen, screen = new() { kind = kind } };
            var supported = ScreenEffectChannels.For(kind);
            var clip = new PerformanceClipAsset { resourceId = "clip", duration = 1, tracks = supported.Select(property => new PerformanceTrack
            { target = "effect/fx", property = property, keys = [new() { value = PerformancePropertyRules.Default(property) }, new() { time = 1, value = Destination(property) }] }).ToArray() };
            var capabilities = new Dictionary<string, PerformanceProperty[]> { ["effect/fx"] = supported };
            var stage = new StageController([], [clip], screenPresets: [preset], additionalTargets: capabilities);
            stage.ApplyScreenEffect(new() { operation = ScreenEffectOperation.Set, resourceId = "preset", instanceId = "fx", strength = .8, includeUi = true });
            stage.ApplyPerformance(new() { operation = PerformanceOperation.Play, clipId = "clip", instanceId = "timeline" }); stage.Tick(.5, false, true);
            ScreenEffectFrame frame = stage.BuildFrame(1920, 1080).effects!.Single();
            foreach (var property in supported)
            {
                double expected = (PerformancePropertyRules.Default(property) + Destination(property)) / 2;
                Near(Value(frame, property), property == PerformanceProperty.EffectStrength ? expected * .8 : expected);
            }
            var frozen = native.Deserialize<StageState>(native.Serialize(stage.Capture()));
            var candidate = new StageController([], [clip], screenPresets: [preset], additionalTargets: capabilities); candidate.Restore(frozen); candidate.Tick(0, false, true);
            Check(candidate.BuildFrame(1920, 1080).effects!.Single() == frame, "Native stage recovery lost a screen-parameter sample: " + kind);
            Check(preset.screen.blurRadius == 3 && preset.screen.pixelSize == 8 && preset.screen.brightness == 0, "Pure effect evaluation mutated an authored preset.");
            stage.Tick(0, true, false);
            if (kind == ScreenEffectKind.Flash) Check(stage.BuildFrame(1920, 1080).effects!.Count == 0, "Timeline values bypassed the player's flash policy.");
            else foreach (var property in supported) Near(Value(stage.BuildFrame(1920, 1080).effects!.Single(), property),
                property == PerformanceProperty.EffectStrength ? Destination(property) * .8 : Destination(property));
            stage.ApplyPerformance(new() { operation = PerformanceOperation.Cancel, instanceId = "timeline" }); stage.Tick(0, false, true);
            Near(stage.BuildFrame(1920, 1080).effects!.Single().strength, .8);
            if (kind == ScreenEffectKind.Blur) Near(stage.BuildFrame(1920, 1080).effects!.Single().blurRadius, 3);
            if (kind == ScreenEffectKind.Pixelate) Near(stage.BuildFrame(1920, 1080).effects!.Single().pixelSize, 8);
        }
        foreach (var property in Enum.GetValues<PerformanceProperty>())
        {
            var clip = new PerformanceClipAsset(); PerformanceEditService.AddTrack(clip, "slot", property); _ = new PerformancePlayer(clip);
            double before = clip.tracks[0].keys[0].value; var range = PerformancePropertyRules.Range(property);
            Reject(() => PerformanceEditService.SetKey(clip, 0, .5, range.maximum + 1, PerformanceInterpolation.Linear));
            Check(clip.tracks[0].keys.Length == 1 && clip.tracks[0].keys[0].value == before, "Invalid parameter editing partially replaced authored keys.");
            clip.tracks[0].keys[0].value = double.NaN; Reject(() => new PerformancePlayer(clip));
        }
        var wrong = new PerformanceClipAsset { resourceId = "wrong", tracks = [new() { target = "slot", property = PerformanceProperty.BlurRadius, keys = [new() { value = 3 }] }] };
        var visual = new StageController([], [wrong]); visual.SetStage(new() { layers = [new() { id = "panel", width = 10, height = 10 }] });
        Reject(() => visual.ApplyPerformance(new() { operation = PerformanceOperation.Play, clipId = "wrong", instanceId = "wrong",
            bindings = [new() { slot = "slot", target = "stage/panel" }] }));
        Check(visual.performances.Capture().Length == 0, "An effect parameter silently bound to a visual transform target.");
    }

    internal static void ProductionSessionAndSourceContracts(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var effect = edits.Add(draft, "opening", StoryNodeIds.screenEffect, new ScreenEffectNode { operation = ScreenEffectOperation.Set, resourceId = "blur", instanceId = "fx", strength = .8 }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId.value == "entry").id, effect);
        var play = edits.Add(draft, "opening", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.Play, clipId = "mixed", instanceId = "mix",
            bindings = [new() { slot = "visual", target = "stage/panel" }, new() { slot = "image", target = "effect/fx" }] }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId == effect).id, play);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var clip = new PerformanceClipAsset { resourceId = "mixed", duration = 2, tracks = [
            new() { target = "visual", property = PerformanceProperty.X, keys = [new(), new() { time = 2, value = 100 }] },
            new() { target = "image", property = PerformanceProperty.BlurRadius, keys = [new() { value = 2 }, new() { time = 2, value = 10 }] },
            new() { target = "image", property = PerformanceProperty.EffectStrength, keys = [new() { value = 1 }, new() { time = 2, value = .2 }] }] };
        var preset = new VisualEffectAsset { resourceId = "blur", kind = VisualEffectKind.Screen, screen = new() { kind = ScreenEffectKind.Blur } };
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, performances = [clip], screenEffects = [preset], initialStage = "room",
            stages = [new() { resourceId = "room", layers = [new() { id = "panel", width = 10, height = 10 }] }] };
        using var session = new NovelSession(game); session.StartNew(); session.Tick(.5);
        var frame = session.stage.BuildFrame(1920, 1080); Near(frame.items.Single().topLeft.X, 25); Near(frame.effects!.Single().blurRadius, 4); Near(frame.effects!.Single().strength, .64);
        var snapshot = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); session.Tick(.5); session.Restore(snapshot);
        Near(session.stage.BuildFrame(1920, 1080).effects!.Single().blurRadius, 4);
        session.Advance(); session.Advance(); session.Tick(.5); session.Choose("sea"); Check(session.Rollback(), "Effect fixture could not return to its decision.");
        Near(session.stage.BuildFrame(1920, 1080).effects!.Single().blurRadius, 4);
        session.stage.ApplyPerformance(new() { operation = PerformanceOperation.Seek, instanceId = "mix", time = 1.5 });
        Near(session.stage.BuildFrame(1920, 1080).effects!.Single().blurRadius, 8);
        session.stage.ApplyPerformance(new() { operation = PerformanceOperation.Cancel, instanceId = "mix" }); Near(session.stage.BuildFrame(1920, 1080).effects!.Single().blurRadius, 3);
        clip.tracks[1].keys[0].value = 33; Reject(() => new NovelSession(game), play.value); clip.tracks[1].keys[0].value = 2;
        var targets = NovelEffectPerformance.Targets(chapter.program!, [preset]);
        Check(targets["effect/fx"].Contains(PerformanceProperty.BlurRadius) && !targets["effect/fx"].Contains(PerformanceProperty.PixelSize), "The effect instance advertised unrelated parameters.");
        var pixel = new VisualEffectAsset { resourceId = "pixel", kind = VisualEffectKind.Screen, screen = new() { kind = ScreenEffectKind.Pixelate } };
        chapter.program!.instructions = [.. chapter.program.instructions, new() { sourceChapterId = "test", sourceNodeId = "replace", screenEffect = new() { operation = ScreenEffectOperation.Set, instanceId = "fx", resourceId = "pixel" } }];
        var common = NovelEffectPerformance.Targets(chapter.program, [preset, pixel]);
        Check(common["effect/fx"].SequenceEqual(new[] { PerformanceProperty.EffectStrength }), "A reused instance advertised parameters incompatible with one of its preset kinds.");
        Reject(() => NovelEffectPerformance.ValidateProgram(chapter.program, [clip], common), play.value);
    }

    private static double Destination(PerformanceProperty property)
    { var range = PerformancePropertyRules.Range(property); return range.minimum + (range.maximum - range.minimum) * .75; }
    private static double Value(ScreenEffectFrame frame, PerformanceProperty property) => property switch
    {
        PerformanceProperty.EffectStrength => frame.strength, PerformanceProperty.BlurRadius => frame.blurRadius, PerformanceProperty.VignetteRadius => frame.vignetteRadius,
        PerformanceProperty.VignetteSoftness => frame.vignetteSoftness, PerformanceProperty.Saturation => frame.saturation, PerformanceProperty.Brightness => frame.brightness,
        PerformanceProperty.Contrast => frame.contrast, PerformanceProperty.PixelSize => frame.pixelSize, PerformanceProperty.LetterboxRatio => frame.letterboxRatio,
        PerformanceProperty.AberrationPixels => frame.aberrationPixels, _ => throw new InvalidOperationException("Unsupported test channel.")
    };
    private static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < .001, $"Effect timeline expected {expected}, got {actual}.");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action, string? source = null)
    {
        try { action(); }
        catch (InvalidOperationException error) when (source is null || error.Message.Contains(source, StringComparison.Ordinal)) { return; }
        catch (ArgumentException) when (source is null) { return; }
        throw new InvalidOperationException("An invalid effect timeline was not rejected at its source: " + source);
    }
}
