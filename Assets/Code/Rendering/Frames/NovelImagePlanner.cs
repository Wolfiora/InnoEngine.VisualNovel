using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Stage.Effects;
using Inno.VisualNovel.Stage.Transitions;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Rendering.Frames;

internal sealed class NovelImagePlanner(
    Func<UiFrame, float, float, bool, IReadOnlyList<NovelDrawQuad>> paintUi,
    Func<StageFrame?, float, float, IReadOnlyList<NovelDrawQuad>> paintStage
)
{
    internal NovelImagePlan Build(
        StageFrame stage,
        UiFrame ui,
        UiFrame? previousUi,
        float width,
        float height,
        IReadOnlyList<NovelDrawMesh>? nativeUi,
        IReadOnlyList<NovelDrawMesh>? previousNativeUi
    ) {
        NovelImagePlan current = Scene(stage, ui, nativeUi);
        if (stage.transition is { scope: TransitionScope.Output } output)
            current = Transition(current, Scene(output.before, previousUi ?? (previousNativeUi is not null ? new UiFrame(width, height, [])
                : throw new InvalidOperationException("Output transition has no frozen previous UI.")), previousNativeUi), output);
        return current;

        NovelImagePlan Scene(StageFrame frame, UiFrame page, IReadOnlyList<NovelDrawMesh>? meshes)
        {
            NovelImagePlan scenery = Stage(frame, page);
            if (frame.transition is { scope: TransitionScope.Stage } whole)
                scenery = Transition(scenery, Stage(whole.before, page), whole);
            NovelImagePlan image = new NovelStackImage([scenery, new NovelPaintImage(paintUi(page, width, height, false))]);
            if (meshes is { Count: > 0 }) image = new NovelStackImage([image, new NovelMeshImage(Array.AsReadOnly(meshes.ToArray()))]);
            return Effects(image, frame, true);
        }
        NovelImagePlan Stage(StageFrame frame, UiFrame page)
        {
            NovelImagePlan scenery = new NovelPaintImage(paintUi(page, width, height, true));
            if (frame.transition is { scope: TransitionScope.Group } group)
            {
                bool Selected(StageDrawItem item) => item.group == group.group && (group.target.Length == 0 || item.owner == group.target);
                StageDrawItem[] selected = frame.items.Where(Selected).ToArray(), previous = group.before.items.Where(Selected).ToArray();
                int order = selected.Concat(previous).Select(static item => item.order).DefaultIfEmpty(0).Min();
                var pending = new List<StageDrawItem>(); bool inserted = false;
                foreach (var item in frame.items)
                {
                    if (!inserted && (item.group > group.group || item.group == group.group && item.order >= order))
                    { Flush(); scenery = new NovelStackImage([scenery, Transition(Isolated(selected), Isolated(previous), group)]); inserted = true; }
                    if (!Selected(item)) pending.Add(item);
                }
                Flush(); if (!inserted) scenery = new NovelStackImage([scenery, Transition(Isolated(selected), Isolated(previous), group)]);
                void Flush() { if (pending.Count > 0) { scenery = NovelStageImagePlanner.Append(scenery, pending.ToArray(), Paint); pending.Clear(); } }
            }
            else scenery = NovelStageImagePlanner.Append(scenery, frame.items, Paint);
            return Effects(scenery, frame, false);
        }
        NovelImagePlan Paint(IReadOnlyList<StageDrawItem> items) => new NovelPaintImage(paintStage(new(items), width, height));
        NovelImagePlan Isolated(IReadOnlyList<StageDrawItem> items) => NovelStageImagePlanner.Append(new NovelPaintImage([]), items, Paint);
    }

    private static NovelImagePlan Transition(
        NovelImagePlan source,
        NovelImagePlan previous,
        StageTransitionFrame transition
    ) => new NovelProcessImage(source, previous, new(100 + (int)transition.kind, (float)transition.progress, (int)transition.mask, transition.reverse ? 1 : 0),
        new((float)(transition.angle * Math.PI / 180), (float)transition.softness, (float)transition.centerX, (float)transition.centerY), transition.color, transition.seed);

    private static NovelImagePlan Effects(
        NovelImagePlan image,
        StageFrame frame,
        bool includeUi
    ) {
        foreach (ScreenEffectFrame effect in frame.effects?.Where(item => item.includeUi == includeUi) ?? [])
        {
            Vector4 config = new((int)effect.kind, (float)effect.strength, 0, 0);
            Vector4 parameters = effect.kind switch
            {
                ScreenEffectKind.Blur => new((float)effect.blurRadius, 1, 0, 0),
                ScreenEffectKind.Vignette => new((float)effect.vignetteRadius, (float)effect.vignetteSoftness, 0, 0),
                ScreenEffectKind.ColorGrade => new((float)effect.saturation, (float)effect.brightness, (float)effect.contrast, 0),
                ScreenEffectKind.Pixelate => new((float)effect.pixelSize, 0, 0, 0),
                ScreenEffectKind.Letterbox => new((float)effect.letterboxRatio, 0, 0, 0),
                ScreenEffectKind.ChromaticAberration => new((float)effect.aberrationPixels, 0, 0, 0),
                _ => default
            };
            if (effect.kind == ScreenEffectKind.Blur)
            {
                var horizontal = new NovelProcessImage(image, image, config, parameters, effect.color);
                image = new NovelProcessImage(horizontal, image, config, new((float)effect.blurRadius, 0, 1, 1), effect.color);
            }
            else image = new NovelProcessImage(image, image, config, parameters, effect.color);
        }
        return image;
    }
}
