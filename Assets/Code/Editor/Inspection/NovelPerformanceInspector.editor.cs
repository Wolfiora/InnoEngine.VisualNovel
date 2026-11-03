using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Integration.Components;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Exposes native object bindings and explicit reversible performance sampling.</summary>
[InspectionDrawer(typeof(NovelPerformanceComponent))]
public sealed class NovelPerformanceInspector : InspectionDrawer<NovelPerformanceComponent>
{
    private readonly IInspectionPropertyEditService m_properties;
    private string m_error = string.Empty;
    /// <summary>Creates the current Inspector's native property writer.</summary>
    /// <param name="scenes">The native scene edit service supplied by the Inspector host.</param>
    public NovelPerformanceInspector(SceneEdits scenes) => m_properties = new NativeScenePropertyEdits(scenes);
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelPerformanceComponent target
    ) => ("VN Performance", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelPerformanceComponent target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        widgets.Hint("Bind each clip slot to a native object. XY uses local Transform coordinates; rotation uses planar degrees. Edit mode never advances playback.");
        widgets.Hint("Native channels: position, scale, rotation, image or UI opacity, audio volume, camera shake, and screen-effect strength and parameters. Effect channels must match the assigned preset; overrides affect this instance only.");
        if (ImGui.Button("Apply current position"))
        {
            try { new NovelPerformanceEditService(context.interactions.history, m_properties).Seek(target, target.elapsed); m_error = string.Empty; }
            catch (Exception failure) when (failure is not OutOfMemoryException) { m_error = failure.Message; }
        }
        if (m_error.Length > 0) widgets.Hint(m_error);
    }
}
