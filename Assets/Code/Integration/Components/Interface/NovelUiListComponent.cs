using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Materializes a bounded collection window as actual native Prefab instances during an explicit update, never while rendering.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c2e")]
public sealed class NovelUiListComponent : GameBehavior
{
    /// <summary>Gets or sets the keyed collection exposed by the game's data source.</summary>
    [SerializableProperty] public string collection { get; set; } = string.Empty;
    /// <summary>Gets or sets the native Prefab used for every visible row.</summary>
    [SerializableProperty] public PrefabAsset? itemPrefab { get; set; }
    /// <summary>Gets or sets the first requested row; values beyond the collection show an empty window.</summary>
    [SerializableProperty] public int first { get; set; }
    /// <summary>Gets or sets the maximum instantiated rows, between one and 512.</summary>
    [SerializableProperty] public int capacity { get; set; } = 32;
    /// <summary>Gets the last successfully observed total count for page navigation.</summary>
    public int total { get; private set; }
    private PrefabAsset? m_template;
    private long m_templateVersion;

    /// <summary>Reconciles keyed rows with real scene children after validating the entire input window. Existing keys preserve object identity.</summary>
    /// <param name="source">The explicit session or preview data owner.</param>
    public void Refresh(UiDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (isDestroyed || !isActiveAndEnabled) return;
        if (string.IsNullOrWhiteSpace(collection) || first < 0 || capacity is < 1 or > 512)
            throw new InvalidOperationException("A native UI list requires a collection, nonnegative offset and capacity between 1 and 512.");
        var template = itemPrefab ?? throw new InvalidOperationException("Assign a native item Prefab to the UI list.");
        if (template.isMissing) throw new InvalidOperationException("The native UI list item Prefab is missing.");
        var rows = source.ReadRows(collection);
        if (rows.Count > 100000) throw new InvalidOperationException("The UI list collection exceeds 100000 rows.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
            if (row is null || string.IsNullOrWhiteSpace(row.key) || !keys.Add(row.key))
                throw new InvalidOperationException("Native UI list rows require unique nonempty keys.");
        var window = rows.Skip(first).Take(capacity).Select(row => new UiDataRow(row.key,
            new ReadOnlyDictionary<string, Model.Values.NovelValue>(row.values.ToDictionary(static pair => pair.Key,
                static pair => pair.Value.Copy(), StringComparer.Ordinal)), collection)).ToArray();
        var existing = transform.children.SelectMany(static child => child.gameObject.GetComponents<NovelUiRowComponent>())
            .Where(marker => ReferenceEquals(marker.owner, this)).ToArray();
        if (existing.Any(static marker => marker.row is null) || existing.Select(static marker => marker.row!.key).Distinct(StringComparer.Ordinal).Count() != existing.Length)
            throw new InvalidOperationException("The list contains invalid or duplicate native row ownership.");
        bool replace = !ReferenceEquals(template, m_template) || template.contentVersion != m_templateVersion;
        var available = replace ? new Dictionary<string, NovelUiRowComponent>(StringComparer.Ordinal)
            : existing.ToDictionary(static marker => marker.row!.key, StringComparer.Ordinal);
        int required = window.Count(row => !available.ContainsKey(row.key));
        int liveRows = gameObject.scene.GetObjects().Sum(static item => item.GetComponents<NovelUiRowComponent>().Count);
        if (liveRows + required > 4096) throw new InvalidOperationException("The scene's native UI row budget of 4096 would be exceeded.");
        var created = new List<GameObject>();
        var resolved = new List<(NovelUiRowComponent marker, UiDataRow row)>();
        try
        {
            foreach (var row in window)
            {
                if (!available.TryGetValue(row.key, out var marker))
                {
                    var instance = gameObject.scene.InstantiatePrefab(template, transform); created.Add(instance);
                    if (instance.TryGetComponent<NovelUiRootComponent>(out _) || instance.TryGetComponent<NovelUiRowComponent>(out _))
                        throw new InvalidOperationException("An item Prefab root must not contain a Canvas or runtime row marker.");
                    marker = instance.AddComponent<NovelUiRowComponent>();
                }
                resolved.Add((marker, row));
            }
        }
        catch
        {
            foreach (var instance in created) if (!instance.isDestroyed) gameObject.scene.DestroyObject(instance);
            throw;
        }
        foreach (var obsolete in existing.Where(marker => !resolved.Any(item => ReferenceEquals(item.marker, marker))))
            gameObject.scene.DestroyObject(obsolete.gameObject);
        int index = 0;
        foreach (var item in resolved)
        {
            item.marker.owner = this; item.marker.row = item.row;
            item.marker.transform.SetSiblingIndex(index++);
        }
        total = rows.Count; m_template = template; m_templateVersion = template.contentVersion;
    }

    /// <summary>Removes only rows owned by this component; authored decorations and unrelated children survive.</summary>
    public void Clear()
    {
        if (isDestroyed || !gameObject.isRuntimeValid) { total = 0; m_template = null; m_templateVersion = 0; return; }
        foreach (var child in transform.children.ToArray())
            if (child.gameObject.TryGetComponent<NovelUiRowComponent>(out var marker) && ReferenceEquals(marker?.owner, this))
                gameObject.scene.DestroyObject(child.gameObject);
        total = 0; m_template = null; m_templateVersion = 0;
    }

    /// <inheritdoc />
    protected override void OnDisable() => Clear();
    /// <inheritdoc />
    protected override void OnDestroy() => Clear();
}
