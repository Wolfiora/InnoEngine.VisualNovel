using System;
using System.Linq;
using Inno.VisualNovel.Authoring.ProjectEditing;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Checks custom UI actions against their game-owned typed metadata before publication.</summary>
public static class UiActionBindingValidator
{
    /// <summary>Validates declared actions, fixed arguments and statically known variable bindings in every theme.</summary>
    /// <param name="project">The candidate game with complete page and action libraries.</param>
    public static void Validate(NovelProjectAsset project)
    {
        UiActionContract.ValidateCatalog(project.uiActions);
        var definitions = project.uiActions.ToDictionary(static item => item.id, StringComparer.Ordinal);
        var variables = NovelProjectVariables.Read(project);
        foreach (var page in project.screens.Concat(project.themes.SelectMany(static theme => theme.screens)))
            foreach (var row in ScreenEditService.Flatten(page.screen ?? throw new InvalidOperationException("Page is missing: " + page.role)))
            {
                var element = row.element;
                if (!element.action.StartsWith("custom.", StringComparison.Ordinal)) continue;
                string location = page.role + "/" + element.id + " → " + element.action;
                if (!definitions.TryGetValue(element.action, out var definition)) throw new InvalidOperationException("UI action metadata is undeclared: " + location);
                if (element.argumentBinding.Length == 0) { UiActionContract.ResolveArgument(definition, element.argument); continue; }
                string path = element.argumentBinding;
                VariableDefinition? field = path.StartsWith("page.", StringComparison.Ordinal) ? page.screen!.stateVariables.SingleOrDefault(item => "page." + item.id == path)
                    : path.StartsWith("variables.", StringComparison.Ordinal) ? variables.SingleOrDefault(item => "variables." + item.id == path) : null;
                if (field is null && (path.StartsWith("page.", StringComparison.Ordinal) || path.StartsWith("variables.", StringComparison.Ordinal)))
                    throw new InvalidOperationException("UI action argument field is undeclared: " + location + "/" + path);
                if (field is null)
                {
                    if (UiDataBindingValidator.Field(project.uiDataSources, path) is { } custom)
                    {
                        definition.argument.Validate(custom.schema.defaultValue);
                        if (custom.schema.members.Except(definition.argument.members, StringComparer.Ordinal).Any()) throw new InvalidOperationException("UI action source includes unsupported enum members: " + location);
                    }
                    continue;
                }
                definition.argument.Validate(field.initialValue);
                if (field.initialValue.kind == NovelValueKind.Enum || field.initialValue.kind == NovelValueKind.List && field.initialValue.elementKind == NovelValueKind.Enum)
                    if (field.members.Except(definition.argument.members, StringComparer.Ordinal).Any()) throw new InvalidOperationException("UI action argument enum includes unsupported members: " + location);
            }
    }
}
