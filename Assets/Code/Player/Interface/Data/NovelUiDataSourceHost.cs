using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Player.Interface.Data;

internal sealed class NovelUiDataSourceHost : IDisposable
{
    private sealed record Owner(UiDataSource source, NovelUiDataSourceContext context);
    private readonly Dictionary<string, UiDataSourceDefinition> m_definitions;
    private readonly Dictionary<string, Owner> m_owners = new(StringComparer.Ordinal);
    private readonly NovelUiDataSourceRegistry m_registry;
    private readonly NovelSession m_session;
    private readonly UiDataSource m_native;
    internal string error { get; private set; } = string.Empty;
    internal NovelUiDataSourceHost(
        UiDataSourceDefinition[] definitions,
        NovelUiDataSourceRegistry? registry,
        NovelSession session,
        UiDataSource native
    ) {
        UiDataSourceContract.ValidateCatalog(definitions);
        m_definitions = definitions.ToDictionary(static item => item.id, UiDataSourceContract.Copy, StringComparer.Ordinal);
        m_registry = (registry ?? new()).Snapshot(); m_session = session; m_native = native;
    }
    internal bool TryRead(
        string path,
        out NovelValue value
    ) {
        value = NovelValue.Boolean(false);
        try
        {
            var (definition, local) = Resolve(path);
            var field = definition.fields.SingleOrDefault(item => item.schema.id == local) ?? throw new InvalidOperationException("Root field is undeclared.");
            if (!OwnerFor(definition).source.TryRead(local, out var found)) throw new InvalidOperationException("Declared source field is unavailable.");
            Validate(field, found); value = found.Copy(); return true;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { Fail(path, failure); return false; }
    }
    internal IReadOnlyList<UiDataRow> ReadRows(string path)
    {
        try
        {
            var (definition, local) = Resolve(path);
            var collection = definition.collections.SingleOrDefault(item => item.id == local) ?? throw new InvalidOperationException("Collection is undeclared.");
            var rows = OwnerFor(definition).source.ReadRows(local);
            if (rows.Count > 4096) throw new InvalidOperationException("A source page exceeds 4096 rows; provide explicit pagination.");
            var keys = new HashSet<string>(StringComparer.Ordinal); var result = new List<UiDataRow>();
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.key) || row.key.Length > 256 || !keys.Add(row.key) || row.values.Count != collection.fields.Length)
                    throw new InvalidOperationException("Source rows require unique bounded keys and the exact declared fields.");
                var values = new Dictionary<string, NovelValue>(StringComparer.Ordinal);
                foreach (var field in collection.fields)
                {
                    if (!row.values.TryGetValue(field.schema.id, out var value)) throw new InvalidOperationException("A declared row field is missing: " + field.schema.id);
                    Validate(field, value); values.Add(field.schema.id, value.Copy());
                }
                result.Add(new(row.key, values, path));
            }
            return result.AsReadOnly();
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { Fail(path, failure); return []; }
    }
    internal bool TryWrite(
        string path,
        NovelValue value,
        UiDataRow? row = null
    ) {
        Owner? owner = null;
        try
        {
            var (definition, local) = Resolve(row?.sourcePath ?? path);
            UiDataFieldDefinition? field;
            if (row is null) field = definition.fields.SingleOrDefault(item => item.schema.id == local);
            else
            {
                if (!path.StartsWith("item.", StringComparison.Ordinal)) throw new InvalidOperationException("Row writes require a declared item field.");
                field = definition.collections.SingleOrDefault(item => item.id == local)?.fields.SingleOrDefault(item => "item." + item.schema.id == path);
                string key = row.key;
                row = ReadRows(row.sourcePath).SingleOrDefault(item => item.key == key) ?? throw new InvalidOperationException("The submitted source row no longer exists.");
            }
            if (field is not { writable: true }) throw new InvalidOperationException("The source field is read-only or undeclared.");
            Validate(field, value); owner = OwnerFor(definition);
            if (!definition.capabilities.HasFlag(CommandCapabilities.Rollback)) m_session.story?.BlockRollback("UI source write: " + definition.id);
            owner.context.writing = true;
            bool written = row is null ? owner.source.TryWrite(local, value.Copy()) : owner.source.TryWrite(path, value.Copy(), row);
            if (!written) throw new InvalidOperationException("The source rejected its declared writable field.");
            return true;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { Fail(path, failure); return false; }
        finally { if (owner is not null) owner.context.writing = false; }
    }
    internal TextureAsset? ReadImage(
        string path,
        UiDataRow? row
    )
    {
        try
        {
            var (definition, local) = Resolve(path);
            if (!definition.images.Contains(local, StringComparer.Ordinal)) throw new InvalidOperationException("Image field is undeclared.");
            if (row is not null && row.sourcePath.Length > 0)
            {
                var (rowOwner, _) = Resolve(row.sourcePath);
                if (rowOwner.id != definition.id) throw new InvalidOperationException("Image rows must belong to the same custom source.");
            }
            var image = OwnerFor(definition).source.ReadImage(local, row);
            if (image is { isMissing: true }) throw new InvalidOperationException("The source image is missing.");
            return image;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { Fail(path, failure); return null; }
    }
    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var owner in m_owners.Values)
            try { (owner.source as IDisposable)?.Dispose(); } catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; }
        m_owners.Clear();
    }
    private Owner OwnerFor(UiDataSourceDefinition definition)
    {
        if (m_session.isPreview && (!definition.capabilities.HasFlag(CommandCapabilities.Preview) || definition.capabilities.HasFlag(CommandCapabilities.ExternalEffects)))
            throw new InvalidOperationException("UI data source is disabled in preview.");
        if (!m_owners.TryGetValue(definition.id, out var owner))
        { var context = new NovelUiDataSourceContext(m_session, m_native); owner = new(m_registry.Create(definition, context), context); m_owners.Add(definition.id, owner); }
        return owner;
    }
    private (UiDataSourceDefinition definition, string local) Resolve(string path)
    {
        string[] parts = path.Split('.');
        if (parts.Length != 3 || parts[0] != "custom" || !m_definitions.TryGetValue(parts[1], out var definition)) throw new InvalidOperationException("The custom source path is undeclared.");
        return (definition, parts[2]);
    }
    private static void Validate(
        UiDataFieldDefinition field,
        NovelValue value
    ) {
        field.schema.Validate(value);
        Model.Interface.UiStateRules.ValidateValue(value);
    }
    private void Fail(
        string path,
        Exception failure
    ) => error = path + ": " + failure.Message;
}
