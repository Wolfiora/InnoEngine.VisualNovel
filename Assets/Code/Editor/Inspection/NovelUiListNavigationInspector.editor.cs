using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Explains no-code collection paging with native destination selection and boundary feedback.</summary>
[InspectionDrawer(typeof(NovelUiListNavigationComponent))]
public sealed class NovelUiListNavigationInspector : InspectionDrawer<NovelUiListNavigationComponent>
{
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelUiListNavigationComponent target
    ) => ("VN List Navigation", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelUiListNavigationComponent target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        widgets.Hint("Pick a Collection List in this scene. Capacity controls page size. First, Previous, Next and Last update its actual First offset; the list creates only the current window of native Prefab rows.");
        widgets.Hint("The button is disabled at its boundary or when the destination is inactive. Put navigation outside the generated rows so it remains available on every page. Lists have no observed rows before their first runtime refresh.");
        if (target.target is null) widgets.Hint("Assign a destination Collection List before running this button.");
        if (target.gameObject.TryGetComponent<NovelUiPageLinkComponent>(out _)
            || target.gameObject.TryGetComponent<NovelUiActionComponent>(out var action) && action is { action.Length: > 0 })
            widgets.Hint("Remove the Page Navigation or clear Story Action; a button has one action owner.");
    }
}
