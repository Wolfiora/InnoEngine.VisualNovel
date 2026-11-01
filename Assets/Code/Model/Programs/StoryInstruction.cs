using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Interface;

namespace Inno.VisualNovel.Model.Programs;

/// <summary>Maps an instruction output to a stable destination node.</summary>
[GenerateSerializationConverter]
public sealed class InstructionTarget : ISerializable
{
    /// <summary>Gets or sets the output port identity.</summary>
    [SerializableProperty]
    public string port { get; set; } = string.Empty;
    /// <summary>Gets or sets the destination node identity.</summary>
    [SerializableProperty]
    public string nodeId { get; set; } = string.Empty;
}

/// <summary>Contains a lowered instruction without graph layout or native editor values.</summary>
[RequiresSerializationConverter]
public sealed class StoryInstruction : ISerializable
{
    /// <summary>Gets or sets a source-qualified engine log instruction.</summary>
    [SerializableProperty] public Story.Nodes.Debugging.LogNode? log { get; set; }
    /// <summary>Gets or sets the original source chapter independently of runtime linking.</summary>
    [SerializableProperty] public string sourceChapterId { get; set; } = string.Empty;
    /// <summary>Gets or sets the original graph node independently of runtime linking.</summary>
    [SerializableProperty] public string sourceNodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the stable source node.</summary>
    [SerializableProperty]
    public string nodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the open instruction definition identity.</summary>
    [SerializableProperty]
    public string definitionId { get; set; } = string.Empty;
    /// <summary>Gets or sets resolved outgoing control-flow targets.</summary>
    [SerializableProperty]
    public InstructionTarget[] targets { get; set; } = [];
    /// <summary>Gets or sets dialogue data when this is a dialogue instruction.</summary>
    [SerializableProperty]
    public DialogueNode? dialogue { get; set; }
    /// <summary>Gets or sets choice data when this is a decision instruction.</summary>
    [SerializableProperty]
    public ChoiceNode? choice { get; set; }
    /// <summary>Gets or sets assignment data when this is a variable instruction.</summary>
    [SerializableProperty]
    public AssignmentNode? assignment { get; set; }
    /// <summary>Gets or sets condition data when this is a conditional instruction.</summary>
    [SerializableProperty]
    public ConditionNode? condition { get; set; }
    /// <summary>Gets or sets timed wait data.</summary>
    [SerializableProperty]
    public WaitNode? wait { get; set; }
    /// <summary>Gets or sets invocation data.</summary>
    [SerializableProperty]
    public CallNode? call { get; set; }
    /// <summary>Gets or sets weighted branch data.</summary>
    [SerializableProperty]
    public RandomNode? random { get; set; }
    /// <summary>Gets or sets a stage request handled by the session coordinator.</summary>
    [SerializableProperty] public StageNode? stage { get; set; }
    /// <summary>Gets or sets a declarative audio request.</summary>
    [SerializableProperty] public AudioNode? audio { get; set; }
    /// <summary>Gets or sets a reusable performance lifecycle request.</summary>
    [SerializableProperty] public PerformanceNode? performance { get; set; }
    /// <summary>Gets or sets bounded player input data.</summary>
    [SerializableProperty] public InputNode? input { get; set; }
    /// <summary>Gets or sets an explicit monotonic collection unlock.</summary>
    [SerializableProperty] public UnlockNode? unlock { get; set; }
    /// <summary>Gets or sets a typed extension invocation.</summary>
    [SerializableProperty] public CommandNode? command { get; set; }
    /// <summary>Gets or sets a bounded parallel fork.</summary>
    [SerializableProperty] public ParallelNode? parallel { get; set; }
    /// <summary>Gets or sets a task completion boundary.</summary>
    [SerializableProperty] public JoinNode? join { get; set; }
    /// <summary>Gets or sets a screen-effect instance lifecycle request.</summary>
    [SerializableProperty] public ScreenEffectNode? screenEffect { get; set; }
    /// <summary>Gets or sets an image-transition boundary request.</summary>
    [SerializableProperty] public TransitionNode? transition { get; set; }
    /// <summary>Gets or sets a session page, theme or title action.</summary>
    [SerializableProperty] public SystemNode? system { get; set; }
}
