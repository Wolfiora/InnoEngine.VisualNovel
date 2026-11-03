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

/// <summary>Provides a searchable story/extension action menu and typed argument field selection.</summary>
[InspectionDrawer(typeof(NovelUiActionComponent))]
public sealed class NovelUiActionInspector : InspectionDrawer<NovelUiActionComponent>
{
    private readonly StoryInspectorFields m_fields;
    private readonly NovelUiBindingEditService m_edits;
    private readonly NativeUiInspectorContext m_context = new();
    /// <summary>Uses native gestures and History for button action authoring.</summary>
    /// <param name="interactions">The active editor interaction owner.</param>
    /// <param name="scenes">The native scene edit service supplied by the Inspector host.</param>
    public NovelUiActionInspector(
        EditorInteractions interactions,
        SceneEdits scenes
    ) { m_fields = new(interactions); m_edits = new(new NativeScenePropertyEdits(scenes)); }
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelUiActionComponent target
    ) => ("VN Story Action", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelUiActionComponent target
    ) {
        m_fields.BeginFrame();
        try
        {
            var game = m_context.Refresh(target.gameObject); string prefix = target.identity.persistentId.ToString();
            string action = target.action;
            if (m_fields.Select(prefix + "/action", "On activation", ref action, BuiltInUiChoices.Actions
                .Concat((game?.uiActions ?? []).Select(static item => (item.id, "Extension · " + item.title)))))
                m_edits.SetAction(target, nameof(target.action), action);
            string argument = target.argument;
            bool changed = action == "open" ? m_fields.Select(prefix + "/argument", "Story screen", ref argument, BuiltInUiChoices.Pages)
                : m_fields.Text(prefix + "/argument", "Literal argument", ref argument);
            if (changed) m_edits.SetAction(target, nameof(target.argument), argument, m_fields.mergeKey);
            string binding = target.argumentBinding;
            if (m_fields.Select(prefix + "/binding", "Argument from field", ref binding, BuiltInUiChoices.Arguments
                .Concat(BuiltInUiChoices.Values(m_context.variables)).Concat(UiDataSourceChoices.Values(game?.uiDataSources ?? [])).DistinctBy(static item => item.id)))
                m_edits.SetAction(target, nameof(target.argumentBinding), binding);
            EditorUiSession.current.widgets.Hint("Field arguments override literal arguments. Story actions require a running VN project. To open an actual scene page without a story, use a Page Navigation button instead.");
            if (game?.uiActions.FirstOrDefault(item => item.id == action) is { help.Length: > 0 } custom) EditorUiSession.current.widgets.Hint(custom.help);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { EditorUiSession.current.widgets.Hint(failure.Message); }
        finally { m_fields.EndFrame(); }
        if (ImGui.CollapsingHeader("Advanced action paths")) context.DrawProperties();
    }
}
