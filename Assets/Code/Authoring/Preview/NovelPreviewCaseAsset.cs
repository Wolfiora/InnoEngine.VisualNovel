using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;
using Inno.VisualNovel.Narrative.Values;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Stores editor-only preview preconditions and recorded route inputs as a native asset.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f183")]
public sealed class NovelPreviewCaseAsset : AssetObject
{
    /// <summary>Gets or sets the author-facing test name.</summary>
    [SerializableProperty] public string title { get; set; } = "Preview case";
    /// <summary>Gets or sets explicit complete, local or route semantics.</summary>
    [SerializableProperty] public PreviewRunMode mode { get; set; }
    /// <summary>Gets or sets the entry chapter, empty for the project's initial chapter.</summary>
    [SerializableProperty] public string chapterId { get; set; } = string.Empty;
    /// <summary>Gets or sets the public starting entry, empty for the project's configured entry.</summary>
    [SerializableProperty] public string entryId { get; set; } = string.Empty;
    /// <summary>Gets or sets a local start or interactive route target in the draft chapter.</summary>
    [SerializableProperty] public string nodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact recorded interaction ordinal, or zero for the first matching target.</summary>
    [SerializableProperty] public long targetInteraction { get; set; }
    /// <summary>Gets or sets explicit typed test values; globals remain isolated from real player progress.</summary>
    [SerializableProperty] public VariableState[] initialValues { get; set; } = [];
    /// <summary>Gets or sets whether the neutral stage snapshot replaces the project's initial scenery.</summary>
    [SerializableProperty] public bool useStageSnapshot { get; set; }
    /// <summary>Gets or sets explicit scenery, actors, camera and performance state for local preview.</summary>
    [SerializableProperty] public StageState stage { get; set; } = new();
    /// <summary>Gets or sets ordered stable decision option identities for a route.</summary>
    [SerializableProperty] public string[] choices { get; set; } = [];
    /// <summary>Gets or sets explicit input values for route playback.</summary>
    [SerializableProperty] public PreviewInput[] inputs { get; set; } = [];
    /// <summary>Gets or sets stable source breakpoints owned by this editor-only test case.</summary>
    [SerializableProperty] public PreviewBreakpoint[] breakpoints { get; set; } = [];
    /// <summary>Gets or sets the maximum logical route duration before a diagnostic stops replay.</summary>
    [SerializableProperty] public double maximumSeconds { get; set; } = 120;
    /// <summary>Gets or sets the fixed logical step used by route playback.</summary>
    [SerializableProperty] public double stepSeconds { get; set; } = 0.05;
}
