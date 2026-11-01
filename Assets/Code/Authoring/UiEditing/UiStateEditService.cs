using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Edits page state declarations and their control references in one caller-owned native document transaction.</summary>
public static class UiStateEditService
{
    /// <summary>Validates a complete candidate before adding or replacing a stable page field.</summary>
    /// <param name="screen">The document transaction candidate.</param>
    /// <param name="definition">The complete typed declaration; an existing identity is replaced in place.</param>
    public static void Set(
        ScreenAsset screen,
        VariableDefinition definition
    ) {
        var copy = UiDefinitionCopy.Copy(screen);
        int index = Array.FindIndex(copy.stateVariables, field => field.id == definition.id);
        if (index < 0) copy.stateVariables = [.. copy.stateVariables, VariableValueRules.Copy(definition)];
        else copy.stateVariables[index] = VariableValueRules.Copy(definition);
        Validate(copy);
        screen.stateVariables = copy.stateVariables;
    }

    /// <summary>Deletes a field only after rejecting or explicitly clearing all affected page bindings and actions.</summary>
    /// <param name="screen">The document transaction candidate.</param>
    /// <param name="id">The existing stable field identity.</param>
    /// <param name="clearBindings">Whether the author explicitly requested clearing dependent control references.</param>
    public static void Remove(
        ScreenAsset screen,
        string id,
        bool clearBindings = false
    ) {
        if (!screen.stateVariables.Any(field => field.id == id)) throw new InvalidOperationException("The page field does not exist: " + id);
        string[] users = References(screen).Where(item => item.fieldId == id).Select(static item => item.elementId).Distinct(StringComparer.Ordinal).ToArray();
        if (!clearBindings && users.Length > 0) throw new InvalidOperationException("Page field is referenced by controls: " + string.Join(", ", users) + ". Clear references explicitly before deletion.");
        var copy = UiDefinitionCopy.Copy(screen);
        copy.stateVariables = copy.stateVariables.Where(field => field.id != id).ToArray();
        foreach (var row in ScreenEditService.Flatten(copy))
        {
            Rewrite(row.element, value => value == "page." + id ? "" : value);
            if (row.element.action is "page.reset" or "page.toggle" && row.element.argument == id)
            { row.element.action = row.element.argument = row.element.argumentBinding = string.Empty; }
        }
        Validate(copy);
        screen.stateVariables = copy.stateVariables; screen.root = copy.root;
    }

    /// <summary>Locates page field references in control values, geometry, lists, conditions and state actions.</summary>
    /// <param name="screen">The current page or extracted subtree.</param>
    /// <returns>Stable element and field identities without retaining document instances.</returns>
    public static IReadOnlyList<(string elementId, string fieldId)> References(ScreenAsset screen)
    {
        var result = new List<(string, string)>();
        foreach (var row in ScreenEditService.Flatten(screen))
        {
            var e = row.element;
            foreach (string path in new[] { e.binding, e.repeatBinding, e.visibleBinding, e.enabledBinding, e.followEndBinding, e.optionsBinding,
                e.argumentBinding, e.imageBinding, e.tooltipBinding, e.animation.waitingBinding, e.geometryBinding.x, e.geometryBinding.y, e.geometryBinding.width, e.geometryBinding.height })
                if (path.StartsWith("page.", StringComparison.Ordinal)) result.Add((e.id, path[5..]));
            if (e.action is "page.reset" or "page.toggle") result.Add((e.id, e.argument));
        }
        return result;
    }

    internal static void Rewrite(
        UiElementDefinition element,
        Func<string, string> map
    ) {
        element.binding = map(element.binding); element.repeatBinding = map(element.repeatBinding);
        element.visibleBinding = map(element.visibleBinding); element.enabledBinding = map(element.enabledBinding);
        element.followEndBinding = map(element.followEndBinding); element.optionsBinding = map(element.optionsBinding);
        element.argumentBinding = map(element.argumentBinding); element.imageBinding = map(element.imageBinding);
        element.tooltipBinding = map(element.tooltipBinding);
        element.animation.waitingBinding = map(element.animation.waitingBinding);
        var geometry = element.geometryBinding;
        geometry.x = map(geometry.x); geometry.y = map(geometry.y); geometry.width = map(geometry.width); geometry.height = map(geometry.height);
    }

    private static void Validate(ScreenAsset screen) => _ = new UiDocumentInstance(screen, static (_, _) => (0, 0));
}
