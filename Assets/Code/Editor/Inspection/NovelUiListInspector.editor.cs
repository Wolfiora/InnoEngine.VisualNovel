using System;
using System.Linq;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Explains the native item Prefab and data contract without creating runtime rows in the editor scene.</summary>
[InspectionDrawer(typeof(NovelUiListComponent))]
public sealed class NovelUiListInspector : InspectionDrawer<NovelUiListComponent>
{
    private readonly StoryInspectorFields m_fields;
    private readonly NovelUiBindingEditService m_edits;
    /// <summary>Connects collection choices to the scene project's existing data schemas and native History.</summary>
    /// <param name="interactions">The editor interaction owner.</param>
    /// <param name="scenes">The native scene edit service supplied by the Inspector host.</param>
    public NovelUiListInspector(
        EditorInteractions interactions,
        SceneEdits scenes
    ) { m_fields = new(interactions); m_edits = new(new NativeScenePropertyEdits(scenes)); }
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelUiListComponent target
    ) => ("VN Collection List", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelUiListComponent target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        m_fields.BeginFrame();
        try
        {
            var game = NovelUiBindingEditService.Project(target.gameObject);
            var choices = BuiltInUiChoices.Rows.Where(static choice => choice.id.Length > 0)
                .Concat(UiDataSourceChoices.Rows(game?.uiDataSources ?? [])).DistinctBy(static choice => choice.id);
            string current = target.collection;
            if (m_fields.Select(target.identity.persistentId + "/collection", "Choose collection", ref current, choices))
                m_edits.SetCollection(target, current);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { widgets.Hint(failure.Message); }
        finally { m_fields.EndFrame(); }
        widgets.Hint("Create a button or row group, save it as a native Prefab, and assign Item Prefab. Bind child text to item.text and actions to item.id. Runtime rows are actual Prefab instances in Hierarchy.");
        widgets.Hint("First and Capacity select a bounded collection window. Stable row keys preserve object identity when the collection is reordered. Edit-time rendering never instantiates rows.");
        widgets.Hint("For no-code paging, add List Navigation buttons to the parent page and point them at this list. They disable themselves at the first or last page.");
        if (target.itemPrefab is null) widgets.Hint("Assign an Item Prefab before running this list.");
        else if (target.itemPrefab.isMissing) widgets.Hint("The assigned Item Prefab is missing. Select an existing native Prefab.");
        if (string.IsNullOrWhiteSpace(target.collection)) widgets.Hint("Choose a collection path, such as choices or history.");
    }
}
