using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Offers direct child creation while keeping page content in the native Hierarchy.</summary>
[InspectionDrawer(typeof(NovelPageComponent))]
public sealed class NovelPageInspector : InspectionDrawer<NovelPageComponent>
{
    private readonly NovelUiChildrenView m_children;
    /// <summary>Uses current-generation scene edits and native property History.</summary>
    /// <param name="scenes">The active native scene writer.</param>
    public NovelPageInspector(
        SceneEdits scenes
    ) => m_children = new(new NovelUiEditService(scenes, new NativeScenePropertyEdits(scenes)));
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelPageComponent target
    ) => ("VN Page", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelPageComponent target
    ) {
        context.DrawProperties();
        EditorUiSession.current.widgets.Hint("Initial Focus selects a button or toggle inside this page when its engine document opens. Text and style updates preserve current keyboard focus. Hidden or disabled targets are skipped.");
        m_children.Draw(context, target.gameObject);
    }
}
