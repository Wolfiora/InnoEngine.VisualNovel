using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Stage.Effects;
using Inno.VisualNovel.Stage.Transitions;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeUiImagePlanContracts
{
    internal static void EffectsTransitionsAndCaptureOwnership()
    {
        var current = Mesh("current"); var previous = Mesh("previous");
        var meshes = new List<NovelDrawMesh> { current };
        var material = new MaterialAsset(); var composer = new NovelFrameComposer(); var page = new UiFrame(800, 600, []);
        var effect = new ScreenEffectFrame("grade", ScreenEffectKind.ColorGrade, 1, true, 0, 0, .5, .1, .5, 0, 1, 1, 0, 0, 0);
        var flat = composer.Compose(page, material, new([]), nativeUi: meshes);
        Check(flat.image is null && flat.uiMeshes.SequenceEqual(meshes), "Ordinary UI must retain the direct mesh pass.");
        var frame = composer.Compose(page, material, new([], [effect]), effectsMaterial: material, nativeUi: meshes);
        Check(frame.uiMeshes.Count == 0 && frame.image is NovelProcessImage process && process.source.Meshes().Single() == current,
            "Include UI processed scenery alone or drew an unprocessed duplicate overlay.");
        var sceneryOnly = composer.Compose(page, material, new([], [effect with { includeUi = false }]), effectsMaterial: material, nativeUi: meshes);
        Check(sceneryOnly.image!.Meshes().Single() == current && Operations(sceneryOnly.image).All(operation => !operation.source.Meshes().Any()),
            "A scenery-only effect changed the native UI.");
        var transition = new StageTransitionFrame(1, TransitionKind.Dissolve, .5, 0, .1, 0, .5, .5, false, default, 1,
            TransitionScope.Output, StageLayerGroup.Background, "", new([], [effect]));
        var output = composer.Compose(page, material, new([], [effect], transition), effectsMaterial: material,
            nativeUi: meshes, previousNativeUi: [previous]);
        Check(output.image is NovelProcessImage blend && blend.source.Meshes().Single() == current && blend.previous.Meshes().Single() == previous,
            "Output transition lost the frozen native UI or reused current geometry on both sides.");
        Check(output.image!.Meshes().Select(mesh => mesh.texture.key).SequenceEqual(new[] { "current", "previous" }),
            "Resource discovery omitted the previous native UI dependencies.");
        var mutable = new NovelRenderFrame(800, 600, material, []) { uiMeshes = meshes, image = new NovelMeshImage(meshes) };
        var frozen = NovelRenderFrameCopy.Copy(mutable); meshes.Clear();
        Check(frozen.uiMeshes.Single() == current && frozen.image!.Meshes().Single() == current && flat.uiMeshes.Single() == current,
            "A delayed screenshot or composed frame retained a mutable mesh-list alias.");
    }
    private static NovelDrawMesh Mesh(string key) => new(new byte[96], new(0, 0, 800, 600), new(key));
    private static IEnumerable<NovelProcessImage> Operations(NovelImagePlan plan)
    {
        if (plan is NovelProcessImage process)
        {
            yield return process;
            foreach (var child in Operations(process.source)) yield return child;
            if (!ReferenceEquals(process.previous, process.source)) foreach (var child in Operations(process.previous)) yield return child;
        }
        else if (plan is NovelStackImage stack) foreach (var layer in stack.layers) foreach (var child in Operations(layer)) yield return child;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
