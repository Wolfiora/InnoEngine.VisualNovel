using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Checks typed source paths and repeated row ownership before a game is imported or published.</summary>
public static class UiDataBindingValidator
{
    /// <summary>Validates all source-backed values, conditions, images and row controls in fallback and themed pages.</summary>
    /// <param name="project">The complete candidate game.</param>
    public static void Validate(NovelProjectAsset project)
    {
        UiDataSourceContract.ValidateCatalog(project.uiDataSources);
        foreach (var page in project.screens.Concat(project.themes.SelectMany(static theme => theme.screens)))
            if (page.screen is { } screen) Visit(screen.root, null);
        void Visit(UiElementDefinition element, UiDataCollectionDefinition? row)
        {
            var field = Field(project.uiDataSources, element.binding);
            if (element.binding.StartsWith("item.", StringComparison.Ordinal) && row is not null)
                field = row.fields.SingleOrDefault(item => "item." + item.schema.id == element.binding) ?? throw new InvalidOperationException("Repeated source field is undeclared: " + element.id + "/" + element.binding);
            if (field is not null)
            {
                var kind = field.schema.defaultValue.kind;
                if (element.kind == UiElementKind.Selector && kind == NovelValueKind.List) throw new InvalidOperationException("Selectors require a scalar source field: " + element.id);
                if (element.kind == UiElementKind.Toggle && kind != NovelValueKind.Boolean || element.kind == UiElementKind.Input && kind != NovelValueKind.String
                    || element.kind is UiElementKind.Slider or UiElementKind.Progress && kind is not (NovelValueKind.Integer or NovelValueKind.Number))
                    throw new InvalidOperationException("Source field type is incompatible with its control: " + element.id + "/" + element.binding);
                if (element.kind is UiElementKind.Toggle or UiElementKind.Input or UiElementKind.Slider or UiElementKind.Selector && !field.writable)
                    throw new InvalidOperationException("An editable control requires a writable source field: " + element.id + "/" + element.binding);
            }
            foreach (string condition in new[] { element.visibleBinding, element.enabledBinding, element.followEndBinding, element.animation.waitingBinding })
            {
                var flag = Field(project.uiDataSources, condition);
                if (condition.StartsWith("item.", StringComparison.Ordinal) && row is not null) flag = row.fields.SingleOrDefault(item => "item." + item.schema.id == condition)
                    ?? throw new InvalidOperationException("Repeated condition field is undeclared: " + element.id + "/" + condition);
                if (flag is not null && flag.schema.defaultValue.kind != NovelValueKind.Boolean) throw new InvalidOperationException("Source condition is not Boolean: " + element.id + "/" + condition);
            }
            var help = Field(project.uiDataSources, element.tooltipBinding);
            if (element.tooltipBinding.StartsWith("item.", StringComparison.Ordinal) && row is not null)
                help = row.fields.SingleOrDefault(item => "item." + item.schema.id == element.tooltipBinding)
                    ?? throw new InvalidOperationException("Repeated tooltip field is undeclared: " + element.id + "/" + element.tooltipBinding);
            if (help is not null && help.schema.defaultValue.kind != NovelValueKind.String)
                throw new InvalidOperationException("Tooltip bindings require a String field: " + element.id + "/" + element.tooltipBinding);
            foreach (string geometry in new[] { element.geometryBinding.x, element.geometryBinding.y, element.geometryBinding.width, element.geometryBinding.height })
            {
                var number = Field(project.uiDataSources, geometry);
                if (geometry.StartsWith("item.", StringComparison.Ordinal) && row is not null) number = row.fields.SingleOrDefault(item => "item." + item.schema.id == geometry)
                    ?? throw new InvalidOperationException("Repeated geometry field is undeclared: " + element.id + "/" + geometry);
                if (number is not null && number.schema.defaultValue.kind is not (NovelValueKind.Number or NovelValueKind.Integer)) throw new InvalidOperationException("Source geometry requires a numeric field: " + element.id + "/" + geometry);
            }
            if (element.imageBinding.StartsWith("custom.", StringComparison.Ordinal))
            {
                var (source, name) = Resolve(project.uiDataSources, element.imageBinding);
                if (!source.images.Contains(name, StringComparer.Ordinal)) throw new InvalidOperationException("Custom image binding is undeclared: " + element.imageBinding);
            }
            if (element.action.StartsWith("custom.", StringComparison.Ordinal) && element.argumentBinding.StartsWith("item.", StringComparison.Ordinal) && row is not null)
            {
                var argument = row.fields.SingleOrDefault(item => "item." + item.schema.id == element.argumentBinding) ?? throw new InvalidOperationException("Custom action row argument is undeclared: " + element.id);
                var action = project.uiActions.SingleOrDefault(item => item.id == element.action) ?? throw new InvalidOperationException("Custom row action is undeclared: " + element.action);
                action.argument.Validate(argument.schema.defaultValue);
                if (argument.schema.members.Except(action.argument.members, StringComparer.Ordinal).Any()) throw new InvalidOperationException("Custom row argument includes unsupported enum members: " + element.id);
            }
            var childRow = element.repeatBinding.Length == 0 ? row : element.repeatBinding.StartsWith("custom.", StringComparison.Ordinal) ? Collection(project.uiDataSources, element.repeatBinding) : null;
            foreach (var child in element.children) Visit(child, childRow);
        }
    }
    /// <summary>Resolves a custom root value schema, returning null for ordinary native binding paths.</summary>
    /// <param name="sources">The selected game's source declarations.</param>
    /// <param name="path">The full authored binding.</param>
    /// <returns>The declared root field, or null outside the custom namespace.</returns>
    public static UiDataFieldDefinition? Field(
        UiDataSourceDefinition[] sources,
        string path
    ) {
        if (!path.StartsWith("custom.", StringComparison.Ordinal)) return null;
        var (source, name) = Resolve(sources, path);
        return source.fields.SingleOrDefault(item => item.schema.id == name) ?? throw new InvalidOperationException("Custom value binding is undeclared: " + path);
    }
    /// <summary>Resolves the exact schema of a custom repeated collection.</summary>
    /// <param name="sources">The selected game's source declarations.</param>
    /// <param name="path">The complete collection binding.</param>
    /// <returns>The declared keyed row schema.</returns>
    public static UiDataCollectionDefinition Collection(
        UiDataSourceDefinition[] sources,
        string path
    ) {
        var (source, name) = Resolve(sources, path);
        return source.collections.SingleOrDefault(item => item.id == name) ?? throw new InvalidOperationException("Custom collection binding is undeclared: " + path);
    }
    /// <summary>Finds the nearest repeated owner without combining unrelated item schemas.</summary>
    /// <param name="screen">The authored page tree.</param>
    /// <param name="elementId">The selected stable control identity.</param>
    /// <param name="sources">The selected game metadata.</param>
    /// <returns>The exact custom collection, or null inside a native or nonrepeated scope.</returns>
    public static UiDataCollectionDefinition? RowOwner(
        ScreenAsset screen,
        string elementId,
        UiDataSourceDefinition[] sources
    ) {
        string path = RepeatBinding(screen, elementId);
        return path.StartsWith("custom.", StringComparison.Ordinal) ? Collection(sources, path) : null;
    }
    /// <summary>Finds a control's actual inherited row path; a repeater's own declaration applies to its children.</summary>
    /// <param name="screen">The authored page tree.</param>
    /// <param name="elementId">The exact control identity.</param>
    /// <returns>The nearest ancestor repeat path, or empty outside repeated rows.</returns>
    public static string RepeatBinding(
        ScreenAsset screen,
        string elementId
    ) {
        var rows = ScreenEditService.Flatten(screen).ToDictionary(static item => item.element.id, StringComparer.Ordinal);
        var current = rows[elementId].parent;
        while (current is not null)
        {
            if (current.repeatBinding.Length > 0) return current.repeatBinding;
            current = rows[current.id].parent;
        }
        return string.Empty;
    }
    private static (UiDataSourceDefinition source, string name) Resolve(
        UiDataSourceDefinition[] sources,
        string path
    ) {
        string[] parts = path.Split('.');
        if (parts.Length != 3 || !sources.Any(item => item.id == parts[1])) throw new InvalidOperationException("Custom source binding is undeclared: " + path);
        return (sources.Single(item => item.id == parts[1]), parts[2]);
    }
}
