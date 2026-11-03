using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Maintains typed selector options and their localization identities independently of the editor presentation.</summary>
public static class UiOptionEditService
{
    /// <summary>Adds an independent static option without modifying the bound game value.</summary>
    /// <param name="element">The detached selector declaration.</param>
    /// <param name="defaultValue">An optional typed game default for the new option.</param>
    public static void Add(
        UiElementDefinition element,
        NovelValue? defaultValue = null
    ) {
        if (element.kind != UiElementKind.Selector || element.options.Length >= 1000) throw new AuthoringValidationException($"A selector supports at most 1,000 options.");
        element.options = [.. element.options, new() { id = Guid.NewGuid().ToString("N"), textId = Guid.NewGuid().ToString("N"), text = "新选项", value = defaultValue?.Copy() ?? NovelValue.String("新值") }];
    }
    /// <summary>Replaces the option set from a declared enum while retaining unchanged members' text identities.</summary>
    /// <param name="element">The detached selector.</param>
    /// <param name="variable">The explicit enum declaration.</param>
    public static void FromEnum(
        UiElementDefinition element,
        VariableDefinition variable
    ) {
        if (element.kind != UiElementKind.Selector || variable.initialValue.kind != NovelValueKind.Enum) throw new AuthoringValidationException($"Select a selector control and an Enum variable.");
        element.options = variable.members.Select(member =>
        {
            var previous = element.options.FirstOrDefault(option => option.value.kind == NovelValueKind.Enum && option.value.text == member);
            return new UiOptionDefinition { id = previous?.id ?? Guid.NewGuid().ToString("N"), textId = previous?.textId ?? Guid.NewGuid().ToString("N"),
                text = previous?.text ?? member, enabled = previous?.enabled ?? true, value = NovelValue.Enum(member) };
        }).ToArray();
        element.optionsBinding = string.Empty;
    }
}
