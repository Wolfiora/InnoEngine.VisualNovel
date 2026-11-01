using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Interface.Data;

/// <summary>Validates and freezes typed source contracts without adding another value or persistence system.</summary>
public static class UiDataSourceContract
{
    /// <summary>Checks bounded source identities, field uniqueness and synchronous lifecycle guarantees.</summary>
    /// <param name="definition">The complete proposed source schema.</param>
    public static void Validate(UiDataSourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Segment(definition.id) || definition.title.Length is < 1 or > 256 || definition.help.Length > 4096 || definition.collections.Length > 16
            || definition.fields.Length + definition.images.Length + definition.collections.Sum(static item => item.fields.Length) > 64)
            throw new InvalidOperationException("A UI data source requires bounded identities and at most 64 typed/image fields.");
        UiActionContract.Validate(new() { id = "custom.source." + definition.id, title = definition.title, help = definition.help, capabilities = definition.capabilities });
        Fields(definition.fields);
        var names = definition.fields.Select(static item => item.schema.id).Concat(definition.collections.Select(static item => item.id)).Concat(definition.images).ToArray();
        if (names.Any(static id => !Segment(id)) || names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw new InvalidOperationException("Source field, collection and image names must be unique single segments.");
        foreach (var collection in definition.collections)
        { if (collection.title.Length > 256 || collection.fields.Length == 0) throw new InvalidOperationException("Collections need a bounded title and at least one declared field."); Fields(collection.fields); }
    }
    /// <summary>Checks a complete game-owned catalog before constructing implementations.</summary>
    /// <param name="definitions">All source declarations.</param>
    public static void ValidateCatalog(UiDataSourceDefinition[] definitions)
    {
        if (definitions.Length > 64) throw new InvalidOperationException("A game supports at most 64 custom UI data sources.");
        foreach (var item in definitions) Validate(item);
        if (definitions.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != definitions.Length) throw new InvalidOperationException("UI data source identities must be unique.");
    }
    /// <summary>Copies all mutable nested field and enum metadata.</summary>
    /// <param name="source">The source declaration.</param>
    /// <returns>A detached independently editable schema.</returns>
    public static UiDataSourceDefinition Copy(UiDataSourceDefinition source) => new()
    {
        id = source.id, title = source.title, help = source.help, capabilities = source.capabilities,
        fields = source.fields.Select(CopyField).ToArray(), images = source.images.ToArray(),
        collections = source.collections.Select(static item => new UiDataCollectionDefinition { id = item.id, title = item.title, fields = item.fields.Select(CopyField).ToArray() }).ToArray()
    };
    /// <summary>Uses the existing exact command schema fingerprint to compare catalog and registered metadata.</summary>
    /// <param name="definition">The complete source schema.</param>
    /// <returns>A deterministic neutral contract digest.</returns>
    public static string Fingerprint(UiDataSourceDefinition definition)
    {
        Validate(definition);
        var parameters = new List<CommandParameter>();
        foreach (var field in definition.fields) parameters.Add(Parameter("root", field));
        foreach (var collection in definition.collections)
            foreach (var field in collection.fields) { var parameter = Parameter("collection/" + collection.id, field); parameter.help = collection.title + "\n" + parameter.help; parameters.Add(parameter); }
        foreach (string image in definition.images) parameters.Add(new() { id = "image/" + image, defaultValue = NovelValue.String("") });
        return CommandContract.Fingerprint(new() { id = "custom.source." + definition.id, title = definition.title, help = definition.help, capabilities = definition.capabilities, parameters = parameters.ToArray() });
    }
    private static UiDataFieldDefinition CopyField(UiDataFieldDefinition field) => new() { writable = field.writable,
        schema = new() { id = field.schema.id, label = field.schema.label, help = field.schema.help, defaultValue = field.schema.defaultValue.Copy(), members = field.schema.members.ToArray() } };
    private static CommandParameter Parameter(
        string owner,
        UiDataFieldDefinition field
    ) { var result = CopyField(field).schema; result.id = owner + "/" + result.id + (field.writable ? "/write" : "/read"); return result; }
    private static bool Segment(string id) => id.Length <= 64 && !id.Contains('.') && SystemNodeRules.IsPageRole(id);
    private static void Fields(UiDataFieldDefinition[] fields)
    {
        if (fields.Any(static item => !Segment(item.schema.id) || item.schema.label.Length > 256 || item.schema.help.Length > 4096)
            || fields.Select(static item => item.schema.id).Distinct(StringComparer.Ordinal).Count() != fields.Length) throw new InvalidOperationException("Data fields require unique bounded typed identities.");
        foreach (var field in fields) { field.schema.Validate(field.schema.defaultValue); UiStateRules.ValidateValue(field.schema.defaultValue); }
    }
}
