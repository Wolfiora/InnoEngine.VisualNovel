using System;
using System.Linq;

namespace Inno.VisualNovel.Model.Values;

/// <summary>Defines declaration and enum membership invariants shared by compilation, editing and restoration.</summary>
public static class VariableValueRules
{
    /// <summary>Copies a declaration and all mutable defaults without changing its semantic identity.</summary>
    /// <param name="definition">The source declaration.</param>
    /// <returns>An independently editable typed declaration.</returns>
    public static VariableDefinition Copy(VariableDefinition definition) => new()
    {
        id = definition.id, name = definition.name, group = definition.group, description = definition.description, scope = definition.scope,
        initialValue = definition.initialValue.Copy(), members = definition.members.ToArray()
    };
    /// <summary>Rejects a value incompatible with its declaration, including every member of an enum list.</summary>
    /// <param name="definition">The stable typed declaration.</param>
    /// <param name="value">The candidate default, assignment or restored value.</param>
    public static void Validate(
        VariableDefinition definition,
        NovelValue value
    ) {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(definition.id) || !Enum.IsDefined(definition.scope) || definition.initialValue is null)
            throw new InvalidOperationException("Variable declarations need a stable identity, valid scope and typed initial value.");
        if (definition.group is null || definition.description is null || definition.group.Length > 128 || definition.description.Length > 4096)
            throw new InvalidOperationException("Variable groups allow up to 128 characters and author descriptions up to 4096 characters.");
        NovelValue initial = definition.initialValue;
        if (value.kind != initial.kind || value.kind == NovelValueKind.List && value.elementKind != initial.elementKind)
            throw new InvalidOperationException($"Variable '{definition.id}' requires {initial.kind} with its declared element type.");
        bool enumeration = initial.kind == NovelValueKind.Enum || initial.kind == NovelValueKind.List && initial.elementKind == NovelValueKind.Enum;
        if (!enumeration) return;
        if (definition.members.Length == 0 || definition.members.Any(string.IsNullOrWhiteSpace)
            || definition.members.Distinct(StringComparer.Ordinal).Count() != definition.members.Length)
            throw new InvalidOperationException($"Enum variable '{definition.id}' needs unique nonempty declared members.");
        NovelValue[] values = value.kind == NovelValueKind.Enum ? [value] : value.items;
        if (values.Any(item => !definition.members.Contains(item.text, StringComparer.Ordinal)))
            throw new InvalidOperationException($"A value is not a declared enum member of '{definition.id}'.");
    }
}
