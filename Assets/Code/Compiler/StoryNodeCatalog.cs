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
        Add(StoryNodeIds.entry, "入口", "流程", static _ => [Output("next")]);
        Add(StoryNodeIds.end, "结束", "流程", static _ => [Input()]);
        Add(StoryNodeIds.returnFromCall, "返回", "流程", static _ => [Input()]);
        Add(StoryNodeIds.dialogue, "对白", "阅读", Linear);
        Add(StoryNodeIds.assign, "设置变量", "变量", Linear);
        Add(StoryNodeIds.wait, "等待", "流程", Linear);
        Add(StoryNodeIds.call, "调用或跳转", "流程", node => codec.Read<CallNode>(node).jump ? [Input()] : Linear(node));
        Add(StoryNodeIds.assertion, "断言", "调试", Linear);
        Add(StoryNodeIds.log, "输出日志", "调试", Linear);
        Add(StoryNodeIds.stage, "舞台与角色", "演出", Linear);
        Add(StoryNodeIds.screenEffect, "屏幕效果", "演出", Linear);
        Add(StoryNodeIds.transition, "画面转场", "演出", Linear);
        Add(StoryNodeIds.audio, "音频", "声音", Linear);
        Add(StoryNodeIds.unlock, "解锁鉴赏内容", "游戏系统", Linear);
        Add(StoryNodeIds.system, "页面与主题", "游戏系统", Linear);
        Add(StoryNodeIds.parallel, "并行演出", "流程", node => [Input(), .. codec.Read<ParallelNode>(node).branches.Select(Output)]);
        Add(StoryNodeIds.join, "并行汇合", "流程", Linear);
        Add(StoryNodeIds.command, "自定义 C# 指令", "扩展", node => codec.Read<CommandNode>(node).definition.capabilities.HasFlag(CommandCapabilities.Cancel) ? [Input(), Output("next"), Output("cancel")] : Linear(node));
        Add(StoryNodeIds.performance, "演出片段", "演出", Linear);
        Add(StoryNodeIds.input, "玩家文字输入", "阅读", node => codec.Read<InputNode>(node).allowCancel ? [Input(), Output("next"), Output("cancel")] : Linear(node));
        Add(StoryNodeIds.condition, "条件", "流程", static _ => [Input(), Output("true"), Output("false")]);
        Add(StoryNodeIds.choice, "选项", "阅读", node => [Input(), .. codec.Read<ChoiceNode>(node).options.Select(item => Output(item.id))]);
        Add(StoryNodeIds.random, "随机分支", "流程", node => [Input(), .. codec.Read<RandomNode>(node).branches.Select(item => Output(item.id))]);
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
