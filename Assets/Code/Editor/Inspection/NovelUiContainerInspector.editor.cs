using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Explains native layout ownership and adds editable children in the selected container.</summary>
[InspectionDrawer(typeof(NovelUiContainerComponent))]
public sealed class NovelUiContainerInspector : InspectionDrawer<NovelUiContainerComponent>
{
    private readonly NovelUiChildrenView m_children;
    /// <summary>Uses native scene edits and property History for every created child.</summary>
    /// <param name="scenes">The active native scene writer.</param>
    public NovelUiContainerInspector(
        SceneEdits scenes
    ) => m_children = new(new NovelUiEditService(scenes, new NativeScenePropertyEdits(scenes)));
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelUiContainerComponent target
    ) => ("VN Layout Container", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelUiContainerComponent target
    ) {
        context.DrawProperties();
        EditorUiSession.current.widgets.Hint("Automatic layout is computed by engine UI. Child Transform positions are offsets within their allocated slots; reordering children changes the layout. Add VN UI Scroll to make overflow scrollable.");
        m_children.Draw(context, target.gameObject);
    }
}
