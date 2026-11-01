using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Story;

/// <summary>Stores one chapter and its importer-produced runtime program.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f100")]
public sealed class StoryAsset : AssetObject
{
    /// <summary>Gets or sets the persistent chapter semantic identity.</summary>
    [SerializableProperty]
    public string chapterId { get; set; } = "chapter";
    /// <summary>Gets or sets the displayed title.</summary>
    [SerializableProperty]
    public string title { get; set; } = "New chapter";
    /// <summary>Gets or sets the authoritative native graph.</summary>
    [SerializableProperty]
    public StoryGraph graph { get; set; } = new();
    /// <summary>Gets or sets node ownership and author-facing organization.</summary>
    [SerializableProperty]
    public StoryFragment[] fragments { get; set; } = [];
    /// <summary>Gets or sets named chapter entries.</summary>
    [SerializableProperty]
    public StoryEntry[] entries { get; set; } = [];
    /// <summary>Gets or sets variables declared by this chapter.</summary>
    [SerializableProperty]
    public VariableDefinition[] variables { get; set; } = [];
    /// <summary>Gets or sets an explicit native shared declaration library; runtime execution consumes its compiled values.</summary>
    [SerializableProperty] public VariableTableAsset? variableTable { get; set; }
    /// <summary>Gets or sets the importer-produced program; source authors do not edit this value.</summary>
    [SerializableProperty(PropertyVisibility.Hide)]
    public StoryProgram? program { get; set; }
}
