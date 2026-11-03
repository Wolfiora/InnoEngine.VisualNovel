using System;
using System.Collections.Generic;
using System.Linq;

namespace Inno.VisualNovel.Model.Values;

/// <summary>Validates shared declarations and creates one unambiguous owned set for compilation and authoring.</summary>
public static class StoryVariableDeclarations
{
    /// <summary>Checks shared lifetime, unique identities and typed defaults before import or editing.</summary>
    /// <param name="table">The native library or detached edit candidate.</param>
    public static void Validate(VariableTableAsset table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.isMissing) throw new InvalidOperationException("Shared variable library is unavailable: " + table.assetPath);
        if (table.title.Length > 256 || table.variables.Length > 10000) throw new InvalidOperationException("The shared variable library title or declaration count exceeds its limit.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in table.variables)
        {
            if (variable is null || !ids.Add(variable.id) || variable.scope == VariableScope.Local)
                throw new InvalidOperationException("Shared variables require unique IDs and Game or Global scope; call-local variables belong to chapters.");
            VariableValueRules.Validate(variable, variable.initialValue);
        }
    }
    /// <summary>Combines a shared library with chapter declarations without overriding duplicate identities or borrowing mutable defaults.</summary>
    /// <param name="chapter">The chapter's own declarations.</param>
    /// <param name="table">The optional explicitly referenced library.</param>
    /// <returns>Independent declarations in library-then-chapter authoring order.</returns>
    public static VariableDefinition[] Merge(
        IEnumerable<VariableDefinition> chapter,
        VariableTableAsset? table
    ) {
        ArgumentNullException.ThrowIfNull(chapter);
        if (table is not null) Validate(table);
        VariableDefinition[] values = (table?.variables ?? []).Concat(chapter).ToArray();
        if (values.Length > 10000 || values.Any(static item => item is null)
            || values.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException("Library and chapter variables cannot share an ID; choose an explicit owner for each declaration.");
        foreach (var value in values) VariableValueRules.Validate(value, value.initialValue);
        return values.Select(VariableValueRules.Copy).ToArray();
    }
    /// <summary>Compares the semantic contract required before handing an existing chapter declaration to a library.</summary>
    /// <param name="first">The current declaration.</param>
    /// <param name="second">The proposed shared authority.</param>
    /// <returns>Whether identity, lifetime, defaults and enum membership remain identical.</returns>
    public static bool Equivalent(
        VariableDefinition first,
        VariableDefinition second
    ) => first.id == second.id && first.scope == second.scope && first.initialValue.Equals(second.initialValue)
        && first.members.SequenceEqual(second.members, StringComparer.Ordinal);
}
