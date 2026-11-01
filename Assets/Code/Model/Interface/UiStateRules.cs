using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Validates page-owned variable declarations without creating a second story value system.</summary>
public static class UiStateRules
{
    /// <summary>Rejects duplicate, unbounded or globally scoped page fields and invalid typed defaults.</summary>
    /// <param name="variables">The page's ordinary typed variable declarations.</param>
    public static void Validate(VariableDefinition[] variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (variables.Length > 256) throw new InvalidOperationException("A page supports at most 256 local state fields.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in variables)
        {
            if (variable is null || string.IsNullOrWhiteSpace(variable.id) || variable.id.Length > 128
                || variable.id.Trim() != variable.id || variable.name.Length > 256 || !ids.Add(variable.id)
                || variable.members.Length > 256 || variable.members.Any(static member => member.Length > 128)
                || variable.scope is not (VariableScope.Local or VariableScope.Game))
                throw new InvalidOperationException("Page fields require unique bounded identities and Local or Game scope; Global belongs to story progress.");
            VariableValueRules.Validate(variable, variable.initialValue);
            ValidateValue(variable.initialValue);
        }
    }

    /// <summary>Checks a page value against the bounded neutral UI state budget.</summary>
    /// <param name="value">The detached field value.</param>
    public static void ValidateValue(NovelValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.kind is NovelValueKind.String or NovelValueKind.Enum && value.text.Length > 32768)
            throw new InvalidOperationException("Page text state is limited to 32768 UTF-16 characters.");
        if (value.kind != NovelValueKind.List) return;
        var items = value.items;
        if (items.Length > 4096) throw new InvalidOperationException("Page lists are limited to 4096 items.");
        foreach (var item in items) ValidateValue(item);
    }

    /// <summary>Compares the state contract used by theme variants of the same shell role.</summary>
    /// <param name="first">The existing canonical field.</param>
    /// <param name="second">The candidate theme field.</param>
    /// <returns>Whether lifetime, default type/value and enum members match.</returns>
    public static bool Equivalent(
        VariableDefinition first,
        VariableDefinition second
    ) => first.id == second.id && first.scope == second.scope && first.initialValue.Equals(second.initialValue)
        && first.members.SequenceEqual(second.members, StringComparer.Ordinal);

    /// <summary>Checks one control's page bindings against declared field types before layout or input runs.</summary>
    /// <param name="element">The authored control.</param>
    /// <param name="fields">The owning page's validated declaration lookup.</param>
    public static void ValidateElement(
        UiElementDefinition element,
        IReadOnlyDictionary<string, VariableDefinition> fields
    ) {
        Check(element.binding, element.kind switch
        {
            UiElementKind.Toggle => NovelValueKind.Boolean,
            UiElementKind.Input => NovelValueKind.String,
            UiElementKind.Slider or UiElementKind.Progress => NovelValueKind.Number,
            _ => null
        });
        if (element.kind == UiElementKind.Selector && Field(element.binding) is { } selector)
        {
            if (selector.initialValue.kind == NovelValueKind.List) throw new InvalidOperationException("A page selector value must be scalar: " + element.id);
            if (element.optionsBinding.Length == 0) foreach (var option in element.options) VariableValueRules.Validate(selector, option.value);
            else if (Field(element.optionsBinding) is { } list)
            {
                if (list.initialValue.kind != NovelValueKind.List || list.initialValue.elementKind != selector.initialValue.kind
                    || selector.initialValue.kind == NovelValueKind.Enum && list.members.Any(member => !selector.members.Contains(member, StringComparer.Ordinal)))
                    throw new InvalidOperationException("A page selector list must match its destination type and enum members: " + element.id);
            }
        }
        Check(element.repeatBinding, NovelValueKind.List); Check(element.optionsBinding, NovelValueKind.List);
        Check(element.visibleBinding, NovelValueKind.Boolean); Check(element.enabledBinding, NovelValueKind.Boolean);
        Check(element.followEndBinding, NovelValueKind.Boolean); Check(element.argumentBinding, null);
        Check(element.tooltipBinding, NovelValueKind.String);
        Check(element.animation.waitingBinding, NovelValueKind.Boolean);
        Check(element.geometryBinding.x, NovelValueKind.Number); Check(element.geometryBinding.y, NovelValueKind.Number);
        Check(element.geometryBinding.width, NovelValueKind.Number); Check(element.geometryBinding.height, NovelValueKind.Number);
        if (element.imageBinding.StartsWith("page.", StringComparison.Ordinal))
            throw new InvalidOperationException("Page scalar state cannot contain an imported image: " + element.id);
        if (element.action is "page.reset" or "page.toggle")
        {
            if (element.argumentBinding.Length > 0 || !fields.TryGetValue(element.argument, out var target)
                || element.action == "page.toggle" && target.initialValue.kind != NovelValueKind.Boolean)
                throw new InvalidOperationException("Page actions require a fixed declared target of the correct type: " + element.id + "/" + element.argument);
        }

        VariableDefinition? Field(string path) => !path.StartsWith("page.", StringComparison.Ordinal) ? null
            : fields.TryGetValue(path[5..], out var field) ? field : throw new InvalidOperationException("Undeclared page field at " + element.id + ": " + path);
        void Check(string path, NovelValueKind? required)
        {
            var field = Field(path);
            if (field is not null && required is { } kind && field.initialValue.kind != kind
                && !(kind == NovelValueKind.Number && field.initialValue.kind == NovelValueKind.Integer))
                throw new InvalidOperationException("Incompatible page binding at " + element.id + ": " + path + " requires " + kind);
        }
    }
}
