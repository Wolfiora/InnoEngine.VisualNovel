using System;
using System.Linq;
using InnoEngine.Input;
using Inno.VisualNovel.Player.Shell.Keyboard;
using Inno.VisualNovel.Authoring.ProjectEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Checks selector option types against declared game variables and implemented writable settings.</summary>
public static class UiSelectorValidator
{
    /// <summary>Rejects known incompatible selector bindings before publishing the game content closure.</summary>
    /// <param name="project">The complete candidate game.</param>
    public static void Validate(NovelProjectAsset project)
    {
        var variables = NovelProjectVariables.Read(project).ToDictionary(static variable => variable.id, StringComparer.Ordinal);
        foreach (var page in project.screens.Concat(project.themes.SelectMany(static theme => theme.screens)))
            foreach (var row in ScreenEditService.Flatten(page.screen ?? throw new InvalidOperationException("System page is missing: " + page.role)))
            {
                var selector = row.element; if (selector.kind != UiElementKind.Selector) continue;
                string location = page.role + "/" + selector.id + " → " + selector.binding;
                VariableDefinition? variable = null;
                if (UiDataBindingValidator.Field(project.uiDataSources, selector.binding) is { } custom)
                    variable = new() { id = selector.binding, initialValue = custom.schema.defaultValue, members = custom.schema.members };
                if (selector.binding.StartsWith("item.", StringComparison.Ordinal) && UiDataBindingValidator.RowOwner(page.screen!, selector.id, project.uiDataSources) is { } rowOwner)
                {
                    var rowField = rowOwner.fields.SingleOrDefault(item => "item." + item.schema.id == selector.binding) ?? throw new InvalidOperationException("Selector row field is undeclared: " + location);
                    variable = new() { id = selector.binding, initialValue = rowField.schema.defaultValue, members = rowField.schema.members };
                }
                if (selector.binding == "item.shortcutKey" && variable is null)
                {
                    string owner = UiDataBindingValidator.RepeatBinding(page.screen!, selector.id);
                    if (owner is not ("settings.shortcuts" or "settings.navigationShortcuts"))
                        throw new InvalidOperationException("Shortcut key selectors require their own reading or navigation settings row: " + location);
                    variable = new() { id = selector.binding, initialValue = NovelValue.Enum(KeyCode.Unknown.ToString()),
                        members = (owner == "settings.shortcuts" ? NovelKeyboardShortcuts.choices : NovelNavigationShortcuts.choices).Select(static choice => choice.id).ToArray() };
                }
                if (selector.binding == "settings.holdSkipModifiers")
                    variable = new() { id = selector.binding, initialValue = NovelValue.Enum(KeyModifier.Control.ToString()), members = NovelKeyboardShortcuts.modifiers.Select(static modifier => modifier.ToString()).ToArray() };
                if (selector.binding.StartsWith("variables.", StringComparison.Ordinal) && !variables.TryGetValue(selector.binding[10..], out variable))
                    throw new InvalidOperationException("Selector variable is undeclared: " + location);
                if (selector.binding.StartsWith("page.", StringComparison.Ordinal))
                    variable = page.screen!.stateVariables.SingleOrDefault(field => "page." + field.id == selector.binding)
                        ?? throw new InvalidOperationException("Selector page field is undeclared: " + location);
                if (selector.binding.Length == 0 || variable is null && !BuiltInUiChoices.SelectorValues([]).Any(option => option.id == selector.binding))
                    throw new InvalidOperationException("Selector requires an implemented writable binding: " + location);
                NovelValueKind kind = variable?.initialValue.kind ?? (BuiltInUiChoices.WritableBooleans([]).Any(option => option.id == selector.binding) ? NovelValueKind.Boolean
                    : BuiltInUiChoices.Numbers([]).Any(option => option.id == selector.binding) ? NovelValueKind.Number : NovelValueKind.String);
                if (selector.optionsBinding.Length > 0)
                {
                    if (selector.optionsBinding is "settings.shortcutKeys" or "settings.navigationKeys")
                    {
                        var choices = selector.optionsBinding == "settings.shortcutKeys" ? NovelKeyboardShortcuts.choices : NovelNavigationShortcuts.choices;
                        if (kind != NovelValueKind.Enum || variable is null || choices.Any(choice => !variable.members.Contains(choice.id, StringComparer.Ordinal)))
                            throw new InvalidOperationException("Shortcut key options require a matching bounded enum destination: " + location);
                        continue;
                    }
                    if (selector.optionsBinding == "settings.holdSkipChoices")
                    {
                        if (kind != NovelValueKind.Enum || variable is null || NovelKeyboardShortcuts.modifiers.Any(modifier => !variable.members.Contains(modifier.ToString(), StringComparer.Ordinal)))
                            throw new InvalidOperationException("Skip-hold choices require a matching modifier enum destination: " + location);
                        continue;
                    }
                    if (selector.optionsBinding.StartsWith("custom.", StringComparison.Ordinal))
                    {
                        var collection = UiDataBindingValidator.Collection(project.uiDataSources, selector.optionsBinding);
                        var valueField = collection.fields.SingleOrDefault(static field => field.schema.id == "value") ?? throw new InvalidOperationException("Selector source rows require a typed value field: " + location);
                        if (variable is not null) VariableValueRules.Validate(variable, valueField.schema.defaultValue);
                        else if (valueField.schema.defaultValue.kind != kind) throw new InvalidOperationException("Selector source value type differs from its binding: " + location);
                        if (variable is not null && valueField.schema.members.Except(variable.members, StringComparer.Ordinal).Any()) throw new InvalidOperationException("Selector source includes unsupported enum members: " + location);
                        continue;
                    }
                    if (selector.optionsBinding.StartsWith("page.", StringComparison.Ordinal))
                    {
                        var list = page.screen!.stateVariables.SingleOrDefault(field => "page." + field.id == selector.optionsBinding);
                        if (list?.initialValue.kind != NovelValueKind.List || list.initialValue.elementKind != kind)
                            throw new InvalidOperationException("Selector page list has an incompatible element type: " + location);
                        if (variable is not null) foreach (var value in list.initialValue.items) VariableValueRules.Validate(variable, value);
                        continue;
                    }
                    if (selector.optionsBinding is not ("languages" or "themes" or "checkpoints") || kind != NovelValueKind.String
                        || selector.binding == "settings.language" && selector.optionsBinding != "languages"
                        || selector.binding == "game.theme" && selector.optionsBinding != "themes")
                        throw new InvalidOperationException("This game selector requires a compatible typed collection: " + location + "/" + selector.optionsBinding);
                    continue;
                }
                foreach (var option in selector.options)
                {
                    if (variable is not null) VariableValueRules.Validate(variable, option.value);
                    else if (option.value.kind != kind && !(kind == NovelValueKind.Number && option.value.kind == NovelValueKind.Integer))
                        throw new InvalidOperationException("Selector option has an incompatible value type: " + location + "/" + option.id);
                    if (selector.binding == "settings.language" && (option.value.kind != NovelValueKind.String
                        || !(project.localization?.languages.Select(static language => language.id) ?? [project.defaultLanguage]).Contains(option.value.text, StringComparer.Ordinal)))
                        throw new InvalidOperationException("Selector language is unavailable: " + location + "/" + option.id);
                    if (selector.binding == "game.theme" && (option.value.kind != NovelValueKind.String
                        || option.value.text.Length > 0 && !project.themes.Any(theme => theme.resourceId == option.value.text)))
                        throw new InvalidOperationException("Selector theme is unavailable: " + location + "/" + option.id);
                }
            }
    }
}
