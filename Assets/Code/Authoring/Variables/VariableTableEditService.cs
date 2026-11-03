using System;
using System.Linq;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.Variables;

/// <summary>Applies typed shared-library edits to native document candidates without retaining asset or undo ownership.</summary>
public static class VariableTableEditService
{
    /// <summary>Adds or replaces a complete declaration only after validating the whole candidate library.</summary>
    /// <param name="table">The native resource transaction candidate.</param>
    /// <param name="definition">A stable Game or Global declaration.</param>
    public static void Set(
        VariableTableAsset table,
        VariableDefinition definition
    ) {
        var values = table.variables.Select(VariableValueRules.Copy).ToArray();
        int index = Array.FindIndex(values, item => item.id == definition.id);
        if (index < 0) values = [.. values, VariableValueRules.Copy(definition)]; else values[index] = VariableValueRules.Copy(definition);
        Validate(table, values); table.variables = values;
    }
    /// <summary>Removes an explicitly confirmed declaration; affected chapters retain their own source diagnostics until repaired.</summary>
    /// <param name="table">The native resource transaction candidate.</param>
    /// <param name="id">The exact existing declaration identity.</param>
    public static void Remove(
        VariableTableAsset table,
        string id
    ) {
        if (!table.variables.Any(item => item.id == id)) throw new AuthoringValidationException($"The shared variable does not exist: {id}");
        var values = table.variables.Where(item => item.id != id).Select(VariableValueRules.Copy).ToArray();
        Validate(table, values); table.variables = values;
    }
    private static void Validate(
        VariableTableAsset table,
        VariableDefinition[] values
    ) => StoryVariableDeclarations.Validate(new() { title = table.title, variables = values });
}
