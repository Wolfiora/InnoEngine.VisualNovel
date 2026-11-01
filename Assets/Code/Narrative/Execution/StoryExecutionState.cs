using InnoEngine.Serialization;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Narrative.Execution;

/// <summary>Captures interpreter state without tasks, delegates or live asset references.</summary>
[GenerateSerializationConverter]
public sealed class StoryExecutionState : ISerializable
{
    /// <summary>Gets or sets the exact executable fingerprint.</summary>
    [SerializableProperty]
    public string fingerprint { get; set; } = string.Empty;
    /// <summary>Gets or sets the stable current instruction identity.</summary>
    [SerializableProperty]
    public string nodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the lifecycle state.</summary>
    [SerializableProperty]
    public StoryStatus status { get; set; }
    /// <summary>Gets or sets elapsed time in the current wait.</summary>
    [SerializableProperty]
    public double waitElapsed { get; set; }
    /// <summary>Gets or sets the dialogue text after variable interpolation.</summary>
    [SerializableProperty]
    public string dialogueText { get; set; } = string.Empty;
    /// <summary>Gets or sets resolved rich markup with literal interpolation values already escaped.</summary>
    [SerializableProperty]
    public string dialogueMarkup { get; set; } = string.Empty;
    /// <summary>Gets or sets frozen textual substitutions used by the current resolved line.</summary>
    [SerializableProperty] public Reading.DialogueParameter[] dialogueParameters { get; set; } = [];
    /// <summary>Gets or sets the current NVL page independently of historical and global read progress.</summary>
    [SerializableProperty] public Reading.ReadingPageLine[] nvlLines { get; set; } = [];
    /// <summary>Gets or sets selected option marks owned by this ordinary game and rollback branch.</summary>
    [SerializableProperty] public Reading.ChoiceSelection[] selectedChoices { get; set; } = [];
    /// <summary>Gets or sets reveal progress in grapheme clusters.</summary>
    [SerializableProperty]
    public double revealProgress { get; set; }
    /// <summary>Gets or sets game-scoped variable values.</summary>
    [SerializableProperty]
    public VariableState[] gameVariables { get; set; } = [];
    /// <summary>Gets or sets invocation-local values.</summary>
    [SerializableProperty]
    public VariableState[] localVariables { get; set; } = [];
    /// <summary>Gets or sets active invocation frames, oldest first.</summary>
    [SerializableProperty]
    public StoryCallFrame[] calls { get; set; } = [];
    /// <summary>Gets or sets the complete deterministic random state.</summary>
    [SerializableProperty]
    public ulong randomState { get; set; }
    /// <summary>Gets or sets the pending input draft without modifying its destination variable.</summary>
    [SerializableProperty] public string inputText { get; set; } = string.Empty;
    /// <summary>Gets or sets the resolved active dialogue speaker.</summary>
    [SerializableProperty] public string dialogueSpeaker { get; set; } = string.Empty;
    /// <summary>Gets or sets the language in which the captured line was resolved.</summary>
    [SerializableProperty] public string textLocale { get; set; } = string.Empty;
    /// <summary>Gets or sets zero or one active typed command continuations.</summary>
    [SerializableProperty] public Extensions.CommandInvocationState[] commands { get; set; } = [];
    /// <summary>Gets or sets the monotonic barrier identity used to prevent unsafe rollback across external effects.</summary>
    [SerializableProperty] public long rollbackEpoch { get; set; }
    /// <summary>Gets or sets the explanation for the most recent explicit or extension rollback barrier.</summary>
    [SerializableProperty] public string rollbackBarrierReason { get; set; } = string.Empty;
    /// <summary>Gets or sets active bounded parallel branch continuations.</summary>
    [SerializableProperty] public StoryTaskState[] tasks { get; set; } = [];
    /// <summary>Gets or sets the task-local join boundary, empty for the root interpreter.</summary>
    [SerializableProperty] public string taskJoin { get; set; } = string.Empty;
    /// <summary>Gets or sets resource identities started by this task.</summary>
    [SerializableProperty] public StoryOwnedResource[] ownedResources { get; set; } = [];
    /// <summary>Gets or sets deferred cancellation cleanup not yet consumed by the presentation host.</summary>
    [SerializableProperty] public StoryOwnedResource[] cleanup { get; set; } = [];
}
