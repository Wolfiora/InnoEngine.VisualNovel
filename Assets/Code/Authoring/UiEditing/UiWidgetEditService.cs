using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Extracts reusable native UI subtrees and instantiates independent editable controls.</summary>
public static class UiWidgetEditService
{
    /// <summary>Creates a native screen-shaped component template borrowing only imported fonts and images.</summary>
    /// <param name="source">The current page draft.</param>
    /// <param name="elementId">The subtree to extract.</param>
    /// <param name="title">The component's author-facing title.</param>
    /// <returns>A detached editable template; the source page remains unchanged.</returns>
    public static ScreenAsset Extract(
        ScreenAsset source,
        string elementId,
        string title
    ) {
        var element = ScreenEditService.Find(source, elementId);
        var result = UiDefinitionCopy.Copy(new ScreenAsset { title = title, font = source.font, textFonts = source.textFonts, textIcons = source.textIcons, root = element });
        var used = UiStateEditService.References(result).Select(static item => item.fieldId).ToHashSet(StringComparer.Ordinal);
        result.stateVariables = source.stateVariables.Where(field => used.Contains(field.id)).Select(Model.Values.VariableValueRules.Copy).ToArray();
        result.width = Math.Clamp(element.layout.widthMode == UiSizeMode.Fill ? source.width : element.layout.width, 64, 16384);
        result.height = Math.Clamp(element.layout.heightMode == UiSizeMode.Fill ? source.height : element.layout.height, 64, 16384);
        var layout = result.root.layout; layout.x = layout.y = layout.anchorX = layout.anchorY = layout.pivotX = layout.pivotY = 0;
        layout.width = result.width; layout.height = result.height; layout.widthMode = layout.heightMode = UiSizeMode.Fixed;
        return result;
    }
    /// <summary>Inserts a copied component with fresh control, option and localization identities in one candidate edit.</summary>
    /// <param name="destination">The target transaction candidate.</param>
    /// <param name="parentId">The explicit owning container.</param>
    /// <param name="template">The imported or drafted reusable native template.</param>
    /// <returns>The newly inserted component root identity.</returns>
    public static string Insert(
        ScreenAsset destination,
        string parentId,
        ScreenAsset template
    ) {
        var target = ScreenEditService.Flatten(destination); var owner = target.Single(item => item.element.id == parentId);
        if (owner.element.kind is not (UiElementKind.Panel or UiElementKind.Horizontal or UiElementKind.Vertical or UiElementKind.Grid or UiElementKind.Scroll))
            throw new InvalidOperationException("Insert a component into an explicit layout container.");
        var copy = UiDefinitionCopy.Copy(template); var descendants = ScreenEditService.Flatten(copy);
        if (target.Count + descendants.Count > 10000 || owner.depth + 1 + descendants.Max(static item => item.depth) > 64)
            throw new InvalidOperationException("The inserted component exceeds the page's element or depth budget.");
        foreach (var font in copy.textFonts)
            if (destination.textFonts.Any(item => item.key == font.key && item.font?.identity.persistentId != font.font?.identity.persistentId))
                throw new InvalidOperationException("Component font key conflicts with the destination page: " + font.key);
        foreach (var icon in copy.textIcons)
            if (destination.textIcons.Any(item => item.key == icon.key && item.image?.identity.persistentId != icon.image?.identity.persistentId))
                throw new InvalidOperationException("Component image key conflicts with the destination page: " + icon.key);
        var stateIds = copy.stateVariables.ToDictionary(static field => field.id, static _ => Guid.NewGuid().ToString("N"), StringComparer.Ordinal);
        foreach (var field in copy.stateVariables) field.id = stateIds[field.id];
        var stateVariables = destination.stateVariables.Concat(copy.stateVariables).ToArray();
        UiStateRules.Validate(stateVariables);
        foreach (var row in descendants)
        {
            var element = row.element; element.id = Guid.NewGuid().ToString("N"); element.textId = Guid.NewGuid().ToString("N");
            element.tooltipTextId = Guid.NewGuid().ToString("N");
            UiStateEditService.Rewrite(element, path => path.StartsWith("page.", StringComparison.Ordinal) && stateIds.TryGetValue(path[5..], out var mapped) ? "page." + mapped : path);
            if (element.action is "page.reset" or "page.toggle" && stateIds.TryGetValue(element.argument, out var argument)) element.argument = argument;
            element.font ??= copy.font;
            foreach (var option in element.options) { option.id = Guid.NewGuid().ToString("N"); option.textId = Guid.NewGuid().ToString("N"); }
        }
        copy.initialFocus = string.Empty;
        _ = new UiDocumentInstance(copy, static (_, _) => (0, 0));
        destination.stateVariables = stateVariables;
        destination.textFonts = [.. destination.textFonts, .. copy.textFonts.Where(font => !destination.textFonts.Any(item => item.key == font.key))];
        destination.textIcons = [.. destination.textIcons, .. copy.textIcons.Where(icon => !destination.textIcons.Any(item => item.key == icon.key))];
        copy.root.layout.x = copy.root.layout.y = copy.root.layout.anchorX = copy.root.layout.anchorY = copy.root.layout.pivotX = copy.root.layout.pivotY = 0;
        owner.element.children = [.. owner.element.children, copy.root]; return copy.root.id;
    }
}
