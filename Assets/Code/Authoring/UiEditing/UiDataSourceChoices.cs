using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Builds no-code binding menus directly from the selected game's typed source contracts.</summary>
public static class UiDataSourceChoices
{
    /// <summary>Lists compatible root values and optionally fields of the current repeated row owner.</summary>
    /// <param name="sources">The selected game metadata.</param>
    /// <param name="kind">Optional required kind; Number also accepts Integer.</param>
    /// <param name="writable">Whether only editable fields are suitable.</param>
    /// <param name="row">The selected element's exact repeated source schema.</param>
    /// <returns>Stable field paths and author-facing names.</returns>
    public static IEnumerable<(string id, string name)> Values(
        UiDataSourceDefinition[] sources,
        NovelValueKind? kind = null,
        bool writable = false,
        UiDataCollectionDefinition? row = null
    ) => sources.SelectMany(source => source.fields.Where(field => Accept(field, kind, writable)).Select(field => ("custom." + source.id + "." + field.schema.id, "扩展 · " + source.title + " / " + field.schema.label)))
        .Concat(row?.fields.Where(field => Accept(field, kind, writable)).Select(static field => ("item." + field.schema.id, "当前列表项 · " + field.schema.label)) ?? []);
    /// <summary>Lists declared custom collections.</summary>
    /// <param name="sources">The selected game metadata.</param>
    /// <returns>Collection paths and author-facing names.</returns>
    public static IEnumerable<(string id, string name)> Rows(UiDataSourceDefinition[] sources) => sources.SelectMany(source => source.collections.Select(collection => ("custom." + source.id + "." + collection.id, "扩展 · " + source.title + " / " + collection.title)));
    /// <summary>Lists declared custom image bindings.</summary>
    /// <param name="sources">The selected game metadata.</param>
    /// <returns>Image paths and labels.</returns>
    public static IEnumerable<(string id, string name)> Images(UiDataSourceDefinition[] sources) => sources.SelectMany(source => source.images.Select(image => ("custom." + source.id + "." + image, "扩展 · " + source.title + " / " + image)));
    private static bool Accept(
        UiDataFieldDefinition field,
        NovelValueKind? kind,
        bool writable
    ) => (!writable || field.writable)
        && (kind is null || field.schema.defaultValue.kind == kind || kind == NovelValueKind.Number && field.schema.defaultValue.kind == NovelValueKind.Integer);
}
