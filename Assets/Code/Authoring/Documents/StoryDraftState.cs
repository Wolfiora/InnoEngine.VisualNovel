using InnoEngine.Serialization;
using System;
using InnoEngine.Assets;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Documents;

/// <summary>Stores reference-free authored chapter state independently of canonical imported assets.</summary>
[GenerateSerializationConverter]
public sealed class StoryDraftState : ISerializable
{
    /// <summary>Gets or sets the stable chapter identity.</summary>
    [SerializableProperty]
    public string chapterId { get; set; } = "chapter";
    /// <summary>Gets or sets the chapter title.</summary>
    [SerializableProperty]
    public string title { get; set; } = "New chapter";
    /// <summary>Gets or sets the authoritative neutral graph.</summary>
    [SerializableProperty]
    public StoryGraph graph { get; set; } = new();
    /// <summary>Gets or sets node ownership.</summary>
    [SerializableProperty]
    public StoryFragment[] fragments { get; set; } = [];
    /// <summary>Gets or sets public entry mappings.</summary>
    [SerializableProperty]
    public StoryEntry[] entries { get; set; } = [];
    /// <summary>Gets or sets typed variable declarations.</summary>
    [SerializableProperty]
    public VariableDefinition[] variables { get; set; } = [];
    /// <summary>Gets or sets the shared library's engine-owned persistent identity, empty when unassigned.</summary>
    [SerializableProperty] public Guid variableTableId { get; set; }
    /// <summary>Gets or sets the last known native path for author-facing missing-reference diagnostics, never a resolution alias.</summary>
    [SerializableProperty] public string variableTablePath { get; set; } = string.Empty;

    /// <summary>Resolves the shared authority through native asset identity and returns detached typed declarations.</summary>
    /// <returns>The library and chapter's effective declarations without modifying either source.</returns>
    public VariableDefinition[] GetVariables() => StoryVariableDeclarations.Merge(variables, ResolveVariableTable());
    /// <summary>Resolves only the persistent native library identity without creating another asset registry.</summary>
    /// <returns>The canonical library, its native missing representation, or null when unassigned.</returns>
    public VariableTableAsset? ResolveVariableTable() => variableTableId == Guid.Empty ? null : Assets.Load<VariableTableAsset>(variableTableId);

    /// <summary>Creates an asset view of this draft for synchronous compilation or export.</summary>
    /// <returns>A detached asset shell; its nested state is borrowed until the call completes.</returns>
    public StoryAsset ToAsset() => new()
    {
        chapterId = chapterId, title = title, graph = graph,
        fragments = fragments, entries = entries, variables = variables, variableTable = ResolveVariableTable()
    };

    /// <summary>Creates a temporary authored-state view over an asset.</summary>
    /// <param name="asset">The source asset; no mutation is performed.</param>
    /// <returns>A borrowed state view to be deep-copied through the native serializer.</returns>
    public static StoryDraftState FromAsset(StoryAsset asset) => new()
    {
        chapterId = asset.chapterId, title = asset.title, graph = asset.graph,
        fragments = asset.fragments, entries = asset.entries, variables = asset.variables,
        variableTableId = asset.variableTable?.identity.persistentId ?? Guid.Empty, variableTablePath = asset.variableTable?.assetPath.ToString() ?? string.Empty
    };
}
