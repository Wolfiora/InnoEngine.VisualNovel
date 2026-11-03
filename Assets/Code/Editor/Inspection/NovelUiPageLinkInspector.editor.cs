using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Explains native page navigation while leaving destination selection to the engine object picker.</summary>
[InspectionDrawer(typeof(NovelUiPageLinkComponent))]
public sealed class NovelUiPageLinkInspector : InspectionDrawer<NovelUiPageLinkComponent>
{
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelUiPageLinkComponent target
    ) => ("VN Page Navigation", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelUiPageLinkComponent target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        widgets.Hint("Pick a real Page component in this scene. Show keeps the current page visible; use a modal destination to block it. Close hides the destination. Replace closes this button's nearest page first.");
        widgets.Hint("Pages keep their objects and field values while hidden. Native focus memory restores the previous control when a page returns. Editor drawing never activates a page.");
        if (target.target is null) widgets.Hint("Assign a destination Page before running this button.");
        if (target.gameObject.TryGetComponent<NovelUiActionComponent>(out var action) && action is { action.Length: > 0 })
            widgets.Hint("Clear the Story Action on this object; one activation cannot also navigate a native page.");
    }
}
