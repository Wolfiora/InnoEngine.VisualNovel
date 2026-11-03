using System;
using InnoEditor.Rendering;
using InnoEngine.Mathematics;
using InnoEngine.Scene;
using InnoEngine.Rendering;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Rendering;

namespace Inno.VisualNovel.Editor.Presentation;

/// <summary>Edits native Visual Novel objects in the engine Scene viewport using its selection and transform tools.</summary>
[EditorViewportContributorExtension("inno.vn.scene-viewport", "inno.editor.viewport.scene", order: 1000, controllerPriority: 100)]
public sealed class NovelSceneViewportContributor : EditorViewportContributor
{
    /// <inheritdoc />
    public override bool CanContribute(EditorViewportContext context) => Find(context) is not null;

    /// <inheritdoc />
    public override EditorViewportNavigationProfile ConfigureNavigation(EditorViewportContext context)
    {
        NovelSceneSystem? system = Find(context);
        if (system is null) return EditorViewportNavigationProfile.disabled;
        if (!context.navigation.isInitialized)
            context.navigation.ConfigureOrthographic(Vector3.ZERO, Quaternion.identity, system.height * .6f);
        var profile = new EditorViewportNavigationProfile(new("inno.vn.scene.xy"),
            EditorViewportNavigationCapabilities.Planar | EditorViewportNavigationCapabilities.FrameSelection, EditorViewportNavigationMode.Planar);
        object? selected = context.interactions.selection.selectedTarget;
        GameObject? item = selected as GameObject ?? (selected as GameComponent)?.gameObject;
        if (item is not null && ReferenceEquals(item.scene, system.owningScene))
        {
            item.TryGetComponent<Inno.VisualNovel.Integration.Components.NovelImageComponent>(out var image);
            float radius = image is null ? 100 : Math.Max(image.width, image.height) * .6f
                * Math.Max(MathF.Abs(item.transform.worldScale.x), MathF.Abs(item.transform.worldScale.y));
            profile.focusBounds = new(item.transform.worldPosition, Math.Max(1, radius));
        }
        return profile;
    }

    /// <inheritdoc />
    public override EditorViewportContribution Build(EditorViewportContext context)
    {
        NovelSceneSystem system = Find(context) ?? throw new InvalidOperationException("The active scene has no enabled VN system.");
        NovelSceneProjection view = Projection(context);
        var frame = NovelRenderModel.BuildScene(system, view);
        return new(frame.data, frame.pipeline, frame.targetFormat,
            new(view.worldToView, view.projection, true, EditorViewportManipulationPlane.XY));
    }

    /// <inheritdoc />
    public override void HandlePointer(EditorViewportPointerContext context)
    {
        NovelSceneSystem? system = Find(context.viewport);
        if (system is null) return;
        var view = Projection(context.viewport);
        GameObject? selected = system.PickSceneUi(view, new(context.x * context.viewport.pixelWidth, context.y * context.viewport.pixelHeight))
            ?? NovelSceneCapture.HitTest(system.owningScene, view,
            new(context.x * context.viewport.pixelWidth, context.y * context.viewport.pixelHeight));
        context.viewport.interactions.SetSelection(selected);
    }

    private static NovelSceneSystem? Find(EditorViewportContext context)
        => NovelRenderModel.SelectSystem(context.content.GetValues<GameScene>(), context.content.activeContent, requireGameOutput: false);

    private static NovelSceneProjection Projection(EditorViewportContext context)
    {
        Vector3 center = context.navigation.position;
        return new(context.pixelWidth, context.pixelHeight, Matrix.CreateTranslation(-center.x, -center.y, 0),
            context.pixelHeight / (2 * context.navigation.orthographicSize));
    }
}
