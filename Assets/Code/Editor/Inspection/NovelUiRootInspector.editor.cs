using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Offers native child controls without requiring authors to write UI markup.</summary>
[InspectionDrawer(typeof(NovelUiRootComponent))]
public sealed class NovelUiRootInspector : InspectionDrawer<NovelUiRootComponent>
{
    private readonly NovelUiEditService m_edits;
    private readonly NovelUiChildrenView m_children;
    private string m_error = string.Empty;
    /// <summary>Connects this Inspector to native reversible scene edits.</summary>
    /// <param name="scenes">The current scene edit service.</param>
    public NovelUiRootInspector(
        SceneEdits scenes
    ) { m_edits = new(scenes, new NativeScenePropertyEdits(scenes)); m_children = new(m_edits); }
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelUiRootComponent target
    ) => ("VN UI Canvas", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelUiRootComponent target
    ) {
        context.DrawProperties();
        EditorUiSession.current.widgets.Hint("Create real child objects, then edit their text, layout and actions in Inspector. Save reusable pages as native Prefabs.");
        try
        {
            if (ImGui.Button("Add page")) { context.interactions.SetSelection(m_edits.CreatePage(target).gameObject); m_error = string.Empty; }
        }
        catch (Exception error) when (error is not OutOfMemoryException) { m_error = error.Message; }
        if (m_error.Length > 0) EditorUiSession.current.widgets.Hint(m_error);
        m_children.Draw(context, target.gameObject);
    }
}
