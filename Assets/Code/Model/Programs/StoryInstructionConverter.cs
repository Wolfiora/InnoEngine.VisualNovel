using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Interface;

namespace Inno.VisualNovel.Model.Programs;

/// <summary>Preserves the nullable instruction payload union through native serialization.</summary>
public sealed class StoryInstructionConverter : SerializationConverter<StoryInstruction>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        StoryInstruction value
    ) {
        writer.Write("nodeId", value.nodeId);
        writer.Write("sourceChapterId", value.sourceChapterId);
        writer.Write("sourceNodeId", value.sourceNodeId);
        writer.Write("definitionId", value.definitionId);
        writer.Write<Story.Nodes.Debugging.LogNode?>("log", value.log);
        writer.Write("targets", value.targets);
        writer.Write<DialogueNode?>("dialogue", value.dialogue);
        writer.Write<ChoiceNode?>("choice", value.choice);
        writer.Write<AssignmentNode?>("assignment", value.assignment);
        writer.Write<ConditionNode?>("condition", value.condition);
        writer.Write<WaitNode?>("wait", value.wait);
        writer.Write<CallNode?>("call", value.call);
        writer.Write<RandomNode?>("random", value.random);
        writer.Write<StageNode?>("stage", value.stage);
        writer.Write<AudioNode?>("audio", value.audio);
        writer.Write<PerformanceNode?>("performance", value.performance);
        writer.Write<InputNode?>("input", value.input);
        writer.Write<UnlockNode?>("unlock", value.unlock);
        writer.Write<CommandNode?>("command", value.command);
        writer.Write<ParallelNode?>("parallel", value.parallel); writer.Write<JoinNode?>("join", value.join);
        writer.Write<ScreenEffectNode?>("screenEffect", value.screenEffect); writer.Write<TransitionNode?>("transition", value.transition);
        writer.Write<SystemNode?>("system", value.system);
    }

    /// <inheritdoc />
    public override StoryInstruction Read(SerializationReader reader) => new()
    {
        nodeId = reader.Read<string>("nodeId"),
        sourceChapterId = reader.Read<string>("sourceChapterId"),
        sourceNodeId = reader.Read<string>("sourceNodeId"),
        definitionId = reader.Read<string>("definitionId"),
        log = reader.Read<Story.Nodes.Debugging.LogNode?>("log"),
        targets = reader.Read<InstructionTarget[]>("targets"),
        dialogue = reader.Read<DialogueNode?>("dialogue"),
        choice = reader.Read<ChoiceNode?>("choice"),
        assignment = reader.Read<AssignmentNode?>("assignment"),
        condition = reader.Read<ConditionNode?>("condition"),
        wait = reader.Read<WaitNode?>("wait"),
        call = reader.Read<CallNode?>("call"),
        random = reader.Read<RandomNode?>("random"),
        stage = reader.Read<StageNode?>("stage"),
        audio = reader.Read<AudioNode?>("audio"),
        performance = reader.Read<PerformanceNode?>("performance"),
        input = reader.Read<InputNode?>("input"),
        unlock = reader.Read<UnlockNode?>("unlock"),
        command = reader.Read<CommandNode?>("command"), parallel = reader.Read<ParallelNode?>("parallel"), join = reader.Read<JoinNode?>("join"),
        screenEffect = reader.Read<ScreenEffectNode?>("screenEffect"), transition = reader.Read<TransitionNode?>("transition"), system = reader.Read<SystemNode?>("system")
    };
}
