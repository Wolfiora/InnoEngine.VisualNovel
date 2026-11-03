using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEngine.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class NovelUiChildrenView(NovelUiEditService edits)
{
    private string m_error = string.Empty;
    internal void Draw(
        InspectionDrawContext context,
        GameObject parent
    ) {
        var widgets = EditorUiSession.current.widgets;
        widgets.Hint("Add a child, edit it in Inspector, and save reusable groups as native Prefabs. Layout containers arrange children in Hierarchy order.");
        try
        {
            Add("Add button", () => edits.CreateButton(parent));
            Add("Add page navigation button", () => edits.CreatePageLink(parent));
            Add("Add list navigation button", () => edits.CreateListNavigation(parent));
            Add("Add text", () => edits.CreateText(parent));
            Add("Add toggle", () => edits.CreateToggle(parent));
            Add("Add vertical layout", () => edits.CreateContainer(parent, UiContainerLayout.Vertical));
            Add("Add horizontal layout", () => edits.CreateContainer(parent, UiContainerLayout.Horizontal));
            Add("Add grid", () => edits.CreateContainer(parent, UiContainerLayout.Grid));
            Add("Add collection list", () => edits.CreateList(parent));
        }
        catch (Exception error) when (error is not OutOfMemoryException) { m_error = error.Message; }
        if (m_error.Length > 0) widgets.Hint(m_error);
        void Add(string label, Func<GameComponent> create)
        {
            if (!ImGui.Button(label)) return;
            context.interactions.SetSelection(create().gameObject); m_error = string.Empty;
        }
    }
}
