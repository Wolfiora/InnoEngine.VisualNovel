using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Model.Story.Nodes.Stage;

/// <summary>Declares a transition boundary and optional common scenery replacement.</summary>
[GenerateSerializationConverter]
public sealed class TransitionNode : ISerializable
{
    /// <summary>Gets or sets the explicit transition lifecycle operation.</summary>
    [SerializableProperty] public TransitionOperation operation { get; set; }
    /// <summary>Gets or sets the native transition preset key.</summary>
    [SerializableProperty] public string resourceId { get; set; } = string.Empty;
    /// <summary>Gets or sets optional scenery applied after capturing the previous composition.</summary>
    [SerializableProperty] public string nextStage { get; set; } = string.Empty;
    /// <summary>Gets or sets the affected composition scope.</summary>
    [SerializableProperty] public TransitionScope scope { get; set; } = TransitionScope.Stage;
    /// <summary>Gets or sets the stage composition band when scope is Group.</summary>
    [SerializableProperty] public StageLayerGroup group { get; set; }
    /// <summary>Gets or sets an optional exact layer owner filter within the selected band.</summary>
    [SerializableProperty] public string target { get; set; } = string.Empty;
    /// <summary>Gets or sets whether Begin also inserts a completion wait after replacing scenery.</summary>
    [SerializableProperty] public bool waitForCompletion { get; set; }
}

/// <summary>Identifies transition lifecycle operations on the stage's single composition boundary.</summary>
public enum TransitionOperation
{
    /// <summary>Captures previous state and begins a new transition, explicitly replacing any older boundary.</summary>
    Begin,
    /// <summary>Waits for the active transition to complete.</summary>
    Wait,
    /// <summary>Finishes the active transition at its new composition.</summary>
    Complete,
    /// <summary>Restores the captured previous stage and discards the transition.</summary>
    Cancel,
    /// <summary>Pauses the active transition's logical time.</summary>
    Pause,
    /// <summary>Resumes the active transition's logical time.</summary>
    Resume
}

/// <summary>Selects the image scope captured by a transition.</summary>
public enum TransitionScope
{
    /// <summary>Transitions the selected stage band or exact owner.</summary>
    Group,
    /// <summary>Transitions the entire stage while leaving game UI live.</summary>
    Stage,
    /// <summary>Transitions the complete game output including UI.</summary>
    Output
}
