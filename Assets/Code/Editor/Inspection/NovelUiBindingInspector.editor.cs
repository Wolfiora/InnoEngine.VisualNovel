using System;
using System.Collections.Generic;
using System.Linq;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Offers searchable typed field choices for real scene UI controls.</summary>
[InspectionDrawer(typeof(NovelUiBindingComponent))]
public sealed class NovelUiBindingInspector : InspectionDrawer<NovelUiBindingComponent>
{
    private readonly StoryInspectorFields m_fields;
    private readonly NovelUiBindingEditService m_edits;
    private readonly NativeUiInspectorContext m_context = new();
    /// <summary>Connects no-code selectors to native field gestures and property History.</summary>
    /// <param name="interactions">The active editor interaction owner.</param>
    /// <param name="scenes">The native scene edit service supplied by the Inspector host.</param>
    public NovelUiBindingInspector(
        EditorInteractions interactions,
        SceneEdits scenes
    ) { m_fields = new(interactions); m_edits = new(new NativeScenePropertyEdits(scenes)); }
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelUiBindingComponent target
    ) => ("VN UI Bindings", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelUiBindingComponent target
    ) {
        m_fields.BeginFrame();
        try
        {
            var game = m_context.Refresh(target.gameObject);
            var variables = m_context.variables; var sources = game?.uiDataSources ?? [];
            Select(nameof(target.text), "Displayed text", target.text, BuiltInUiChoices.Values(variables).Concat(UiDataSourceChoices.Values(sources)));
            var conditions = BuiltInUiChoices.Conditions(variables).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean));
            Select(nameof(target.visible), "Show when", target.visible, conditions);
            Select(nameof(target.interactable), "Allow interaction when", target.interactable, conditions);
            if (target.gameObject.TryGetComponent<NovelUiToggleComponent>(out _))
                Select(nameof(target.value), "Toggle value", target.value, BuiltInUiChoices.WritableBooleans(variables)
                    .Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, writable: true)));
            EditorUiSession.current.widgets.Hint("Choices come from the scene's VN project. Search by name or field ID. Unbound controls use their authored values; list children may use item fields.");
            if (game is null) EditorUiSession.current.widgets.Hint("Assign a VN project to this scene's System to include game variables and extension fields.");
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { EditorUiSession.current.widgets.Hint(failure.Message); }
        finally { m_fields.EndFrame(); }
        if (ImGui.CollapsingHeader("Advanced binding paths")) context.DrawProperties();
        void Select(string property, string label, string current, IEnumerable<(string id, string name)> choices)
        {
            if (m_fields.Select(target.identity.persistentId + "/" + property, label, ref current, choices.DistinctBy(static item => item.id)))
                m_edits.SetBinding(target, property, current);
        }
    }
}
