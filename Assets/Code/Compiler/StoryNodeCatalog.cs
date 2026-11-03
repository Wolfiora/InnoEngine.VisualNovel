using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Compiler;

/// <summary>Resolves built-in control-flow ports from current typed node settings.</summary>
public sealed class StoryNodeCatalog : IGraphNodeDefinitionResolver
{
    private readonly Dictionary<string, GraphNodeDefinition> m_definitions = new(StringComparer.Ordinal);

    /// <summary>Creates a generation-scoped catalog.</summary>
    /// <param name="codec">The native typed node settings owner.</param>
    public StoryNodeCatalog(StoryNodeDataCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Add(StoryNodeIds.entry, "Entry", "Flow", static _ => [Output("next")]);
        Add(StoryNodeIds.end, "End", "Flow", static _ => [Input()]);
        Add(StoryNodeIds.returnFromCall, "Return", "Flow", static _ => [Input()]);
        Add(StoryNodeIds.dialogue, "Dialogue", "Reading", Linear);
        Add(StoryNodeIds.assign, "Set variable", "Variables", Linear);
        Add(StoryNodeIds.wait, "Wait", "Flow", Linear);
        Add(StoryNodeIds.call, "Call or jump", "Flow", node => codec.Read<CallNode>(node).jump ? [Input()] : Linear(node));
        Add(StoryNodeIds.assertion, "Assert", "Debugging", Linear);
        Add(StoryNodeIds.log, "Log message", "Debugging", Linear);
        Add(StoryNodeIds.stage, "Stage and characters", "Presentation", Linear);
        Add(StoryNodeIds.screenEffect, "Screen effect", "Presentation", Linear);
        Add(StoryNodeIds.transition, "Image transition", "Presentation", Linear);
        Add(StoryNodeIds.audio, "Audio", "Sound", Linear);
        Add(StoryNodeIds.unlock, "Unlock collection content", "Game system", Linear);
        Add(StoryNodeIds.system, "Screens and themes", "Game system", Linear);
        Add(StoryNodeIds.parallel, "Parallel presentation", "Flow", node => [Input(), .. codec.Read<ParallelNode>(node).branches.Select(Output)]);
        Add(StoryNodeIds.join, "Parallel join", "Flow", Linear);
        Add(StoryNodeIds.command, "Custom C# command", "Extensions", node => codec.Read<CommandNode>(node).definition.capabilities.HasFlag(CommandCapabilities.Cancel) ? [Input(), Output("next"), Output("cancel")] : Linear(node));
        Add(StoryNodeIds.performance, "Performance clip", "Presentation", Linear);
        Add(StoryNodeIds.input, "Player text input", "Reading", node => codec.Read<InputNode>(node).allowCancel ? [Input(), Output("next"), Output("cancel")] : Linear(node));
        Add(StoryNodeIds.condition, "Condition", "Flow", static _ => [Input(), Output("true"), Output("false")]);
        Add(StoryNodeIds.choice, "Choices", "Reading", node => [Input(), .. codec.Read<ChoiceNode>(node).options.Select(item => Output(item.id))]);
        Add(StoryNodeIds.random, "Random branch", "Flow", node => [Input(), .. codec.Read<RandomNode>(node).branches.Select(item => Output(item.id))]);
    }

    /// <summary>Gets registered definitions in stable menu order.</summary>
    public IReadOnlyList<GraphNodeDefinition> definitions => m_definitions.Values.ToArray();

    /// <inheritdoc />
    public bool TryResolve(
        string definitionId,
        out GraphNodeDefinition? definition
    ) => m_definitions.TryGetValue(definitionId, out definition);

    private void Add(
        string id,
        string label,
        string category,
        Func<GraphNodeRecord, IReadOnlyList<GraphPortDefinition>> ports
    ) => m_definitions.Add(id, new Definition(id, label, category, ports));

    private static IReadOnlyList<GraphPortDefinition> Linear(GraphNodeRecord _) => [Input(), Output("next")];
    private static GraphPortDefinition Input() => new(new("in"), "In", "inno.vn.flow", GraphPortDirection.Input, GraphPortCapacity.Multiple);
    private static GraphPortDefinition Output(string id) => new(new(id), id, "inno.vn.flow", GraphPortDirection.Output);

    private sealed class Definition(
        string id,
        string label,
        string category,
        Func<GraphNodeRecord, IReadOnlyList<GraphPortDefinition>> ports
    ) : GraphNodeDefinition(id, label, category)
    {
        public override IReadOnlyList<GraphPortDefinition> GetPorts(GraphNodeRecord node) => ports(node);
    }
}
