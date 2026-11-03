using System;
using System.Linq;
using Inno.Core.Identity;
using Inno.Core.Mathematics;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativePerformanceContracts
{
    internal static void ExplicitWritersAndAtomicTargets(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Native performance");
        var image = scene.CreateObject("Target").AddComponent<NovelImageComponent>();
        var performance = scene.CreateObject("Performance").AddComponent<NovelPerformanceComponent>();
        performance.clip = Clip(100); performance.bindings = [new() { slot = "actor", target = image.gameObject }];
        performance.Play(); NovelSceneSimulation.Tick(scene, .5);
        Check(image.transform.localPosition.x == 50 && performance.elapsed == .5 && (image.color & 255) == 128,
            "A native clip did not write actual target Transform and image properties.");
        var view = new NovelSceneProjection(800, 600, Matrix.identity);
        _ = NovelSceneCapture.Capture(scene, view); _ = NovelSceneCapture.Capture(scene, view);
        Check(performance.elapsed == .5 && image.transform.localPosition.x == 50, "Repeated capture advanced the performance.");
        performance.gameObject.SetActive(false); NovelSceneSimulation.Tick(scene, .25);
        Check(performance.elapsed == .5, "An inactive native owner advanced its clock.");
        performance.gameObject.SetActive(true); performance.paused = true; NovelSceneSimulation.Tick(scene, .25);
        Check(performance.elapsed == .5, "A paused clip advanced.");
        performance.Seek(.25); Check(image.transform.localPosition.x == 25, "Explicit authoring sampling did not apply a deterministic position.");
        var replacement = scene.CreateObject("Replacement").AddComponent<NovelPerformanceComponent>();
        replacement.clip = Clip(200); replacement.bindings = [new() { slot = "actor", target = image.gameObject }];
        replacement.Play(); Check(!performance.playing && replacement.playing, "Starting a replacement retained two writers for one channel.");
        NovelSceneSimulation.Tick(scene, .5); Check(image.transform.localPosition.x == 100, "The replacement did not own the real target.");
        performance.playing = true;
        Reject(() => NovelSceneSimulation.Tick(scene, .25));
        Check(replacement.elapsed == .5 && image.transform.localPosition.x == 100, "Writer-conflict validation partially advanced the scene.");
        performance.Stop(); replacement.Stop(); image.transform.localPosition = new(123, 0, 0);
        NovelSceneSimulation.Tick(scene, .25); Check(image.transform.localPosition.x == 123, "Stopped performance overwrote an external script's Transform.");
        replacement.clip.tracks = [.. replacement.clip.tracks, new() { target = "missing", property = PerformanceProperty.X, keys = [new() { value = 800 }] }];
        Reject(() => replacement.Seek(1));
        Check(image.transform.localPosition.x == 123 && replacement.elapsed == .5, "A missing target committed earlier valid tracks or changed time.");
        replacement.clip = Clip(200); scene.DestroyObject(image.gameObject);
        Reject(() => replacement.Seek(0));
        Check(NovelSceneCapture.Capture(scene, view).items.Count == 0, "A deleted timeline target survived in an alternate stage tree.");
        var ui = scene.CreateObject("Native UI target").AddComponent<Inno.VisualNovel.Integration.Components.Interface.NovelUiLayoutComponent>();
        replacement.bindings = [new() { slot = "actor", target = ui.gameObject }];
        replacement.Seek(.75);
        Check(ui.opacity == .75f && ui.transform.localPosition.x == 150, "A native performance did not write UI layout opacity and the real Transform.");
        ui.gameObject.AddComponent<NovelImageComponent>();
        Reject(() => replacement.Seek(.25));
        Check(ui.opacity == .75f && ui.transform.localPosition.x == 150, "An ambiguous image/UI target partially applied earlier native tracks.");
        var effect = scene.CreateObject("Grading").AddComponent<NovelScreenEffectComponent>();
        var sibling = scene.CreateObject("Other grading instance").AddComponent<NovelScreenEffectComponent>();
        effect.preset = sibling.preset = new VisualEffectAsset { resourceId = "grade", kind = VisualEffectKind.Screen,
            screen = new() { kind = ScreenEffectKind.ColorGrade, brightness = 0, saturation = 1 } };
        replacement.bindings = [new() { slot = "grade", target = effect.gameObject }];
        replacement.clip = new PerformanceClipAsset { duration = 1, tracks = [
            new() { target = "grade", property = PerformanceProperty.Brightness, keys = [new() { value = .3 }] },
            new() { target = "grade", property = PerformanceProperty.Saturation, keys = [new() { value = .5 }] }] };
        replacement.Seek(.5);
        var frame = effect.Capture(true).Single();
        Check(frame.brightness == .3 && frame.saturation == .5 && sibling.Capture(true).Single().brightness == 0
            && effect.preset.screen.brightness == 0 && effect.preset.screen.saturation == 1,
            "Native effect channels lost a sibling track or modified the shared source preset.");
        replacement.clip.tracks = [.. replacement.clip.tracks,
            new() { target = "grade", property = PerformanceProperty.BlurRadius, keys = [new() { value = 12 }] }];
        effect.parameters = null;
        Reject(() => replacement.Seek(.75));
        Check(effect.parameters is null && replacement.elapsed == .5,
            "A channel unsupported by the effect mechanism partially committed native parameter overrides.");
        effect.parameters = new() { kind = ScreenEffectKind.Blur };
        Reject(() => effect.Capture(true));
    }

    private static PerformanceClipAsset Clip(double end) => new()
    {
        duration = 1, tracks = [
            new() { target = "actor", property = PerformanceProperty.X, keys = [new() { time = 0, value = 0 }, new() { time = 1, value = end }] },
            new() { target = "actor", property = PerformanceProperty.Opacity, keys = [new() { time = 0, value = 0 }, new() { time = 1, value = 1 }] }]
    };
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid native performance bindings were accepted.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
