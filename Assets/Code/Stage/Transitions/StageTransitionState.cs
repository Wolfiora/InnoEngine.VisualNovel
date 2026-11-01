using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Stage.Transitions;

/// <summary>Captures one transition boundary without retaining a GPU image or live UI object.</summary>
[GenerateSerializationConverter]
public sealed class StageTransitionState : ISerializable
{
    /// <summary>Gets or sets the owner-local boundary sequence.</summary>
    [SerializableProperty] public long sequence { get; set; }
    /// <summary>Gets or sets the declared native preset.</summary>
    [SerializableProperty] public string resourceId { get; set; } = string.Empty;
    /// <summary>Gets or sets elapsed logical transition time.</summary>
    [SerializableProperty] public double elapsed { get; set; }
    /// <summary>Gets or sets whether time is paused.</summary>
    [SerializableProperty] public bool paused { get; set; }
    /// <summary>Gets or sets the affected image scope.</summary>
    [SerializableProperty] public TransitionScope scope { get; set; }
    /// <summary>Gets or sets the selected composition band.</summary>
    [SerializableProperty] public StageLayerGroup group { get; set; }
    /// <summary>Gets or sets an optional exact owner filter.</summary>
    [SerializableProperty] public string target { get; set; } = string.Empty;
    /// <summary>Gets or sets the previous neutral composition; it must not contain another transition.</summary>
    [SerializableProperty] public StageState before { get; set; } = new();
}
