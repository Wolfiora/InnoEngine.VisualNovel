using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Installs restrained, editable interaction motion in built-in pages without special runtime controls.</summary>
public static class UiAnimationTemplates
{
    /// <summary>Assigns ordinary declaration channels, preserving input surfaces and content layout.</summary>
    /// <param name="root">The owned template tree before it is saved as a native page.</param>
    public static void Apply(UiElementDefinition root)
    {
        foreach (var row in UiEditing.ScreenEditService.Flatten(new ScreenAsset { root = root }))
        {
            var item = row.element;
            if (item.kind is UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Selector && item.id != "advance-surface")
            {
                item.animation.hover.enabled = true; item.animation.hover.duration = .12; item.animation.hover.pose.y = -2;
                item.animation.pressed.enabled = true; item.animation.pressed.duration = .06; item.animation.pressed.pose.scale = .985f;
            }
        }
    }
}
