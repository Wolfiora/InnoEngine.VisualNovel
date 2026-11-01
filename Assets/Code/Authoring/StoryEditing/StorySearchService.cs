using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Describes one stable source match without retaining graph or editor instances.</summary>
/// <param name="nodeId">The existing node identity.</param>
/// <param name="fragmentId">The owning fragment identity.</param>
/// <param name="title">The node's author-facing category.</param>
/// <param name="excerpt">A bounded literal preview of matching source content.</param>
/// <param name="bookmarked">Whether the source node is bookmarked.</param>
public sealed record StorySearchResult(string nodeId, string fragmentId, string title, string excerpt, bool bookmarked);

/// <summary>Searches authored content and annotations on demand without another content database or execution order.</summary>
public sealed class StorySearchService(
    StoryNodeDataCodec codec,
    StoryNotesEditService notes
)
{
    /// <summary>Finds literal case-insensitive matches in text, identifiers, resource keys, tags and notes.</summary>
    /// <param name="draft">The current authoritative chapter snapshot.</param>
    /// <param name="query">A plain search string, not an executable expression.</param>
    /// <param name="bookmarksOnly">Whether to include only bookmarked nodes.</param>
    /// <returns>Detached matches in author fragment order; cycles are not expanded.</returns>
    public IReadOnlyList<StorySearchResult> Find(
        StoryDraftState draft,
        string query,
        bool bookmarksOnly = false
    ) {
        return Array.AsReadOnly(Scan(draft, query, bookmarksOnly).Where(static result => result is not null).Select(static result => result!).ToArray());
    }

    internal IEnumerable<StorySearchResult?> Scan(
        StoryDraftState draft,
        string query,
        bool bookmarksOnly
    ) {
        if (query.Length > 1024) throw new ArgumentException("Search text is limited to 1024 characters.", nameof(query));
        var catalog = new StoryNodeCatalog(codec);
        foreach (var row in ScriptProjection.ScriptProjectionBuilder.Build(draft))
        {
            var node = draft.graph.document.FindNode(new(row.nodeId))!; var annotation = notes.Read(node);
            if (bookmarksOnly && !annotation.bookmarked) { yield return null; continue; }
            string title = catalog.TryResolve(node.definitionId, out var definition) ? definition!.displayName : "Missing · " + node.definitionId;
            string[] fields = [node.id.value, node.definitionId, title, row.fragmentId, annotation.note, .. annotation.tags, .. Content()];
            if (!fields.Any(field => field.Contains(query, StringComparison.OrdinalIgnoreCase))) { yield return null; continue; }
            string excerpt = fields.Skip(4).FirstOrDefault(field => field.Length > 0 && field.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? node.definitionId;
            if (excerpt.Length > 200) excerpt = excerpt[..200] + "…";
            yield return new(node.id.value, row.fragmentId, title, excerpt, annotation.bookmarked);

            IEnumerable<string> Content()
            {
                if (node.definitionId == StoryNodeIds.log) return [codec.Read<Model.Story.Nodes.Debugging.LogNode>(node).message];
                if (node.definitionId == StoryNodeIds.dialogue) { var line = codec.Read<DialogueNode>(node); return [line.text, line.speaker, line.characterId, line.textId, line.voiceCueId]; }
                if (node.definitionId == StoryNodeIds.choice) { var choice = codec.Read<ChoiceNode>(node); return new[] { choice.prompt }.Concat(choice.options.SelectMany(static option => new[] { option.id, option.text, option.disabledReason })); }
                if (node.definitionId == StoryNodeIds.input) { var input = codec.Read<InputNode>(node); return [input.prompt, input.defaultText, input.variableId]; }
                if (node.definitionId == StoryNodeIds.stage) { var stage = codec.Read<StageNode>(node); return [stage.resourceId, stage.target, stage.pose, stage.expression]; }
                if (node.definitionId == StoryNodeIds.audio) { var audio = codec.Read<AudioNode>(node); return [audio.cueId, audio.slot]; }
                if (node.definitionId == StoryNodeIds.performance) { var clip = codec.Read<PerformanceNode>(node); return [clip.clipId, clip.instanceId, clip.markerId]; }
                if (node.definitionId == StoryNodeIds.screenEffect) { var effect = codec.Read<ScreenEffectNode>(node); return [effect.resourceId, effect.instanceId]; }
                if (node.definitionId == StoryNodeIds.transition) { var transition = codec.Read<TransitionNode>(node); return [transition.resourceId, transition.nextStage, transition.target]; }
                if (node.definitionId == StoryNodeIds.system) return [codec.Read<SystemNode>(node).argument];
                if (node.definitionId == StoryNodeIds.unlock) return [codec.Read<UnlockNode>(node).entryId];
                if (node.definitionId == StoryNodeIds.call) { var call = codec.Read<CallNode>(node); return [call.chapterId, call.entryId]; }
                if (node.definitionId == StoryNodeIds.assign) { var assignment = codec.Read<AssignmentNode>(node); return new[] { assignment.variableId }.Concat(ExpressionText(assignment.value)); }
                if (node.definitionId is StoryNodeIds.condition or StoryNodeIds.assertion) return ExpressionText(codec.Read<ConditionNode>(node).condition);
                if (node.definitionId == StoryNodeIds.command) { var command = codec.Read<CommandNode>(node); return [command.definition.id, command.definition.title, command.definition.help, command.resultVariable]; }
                return [];
            }
        }
    }
    private static IEnumerable<string> ExpressionText(ExpressionDocument expression)
    {
        var pending = new Stack<(ExpressionDocument expression, int depth)>(); pending.Push((expression, 0)); int count = 0;
        while (pending.TryPop(out var item))
        {
            if (item.depth > 64 || ++count > 10000) throw new InvalidOperationException("Search encountered an unbounded expression; repair its authored tree first.");
            yield return item.expression.variableId; yield return item.expression.literal.ToString();
            foreach (var child in item.expression.operands) pending.Push((child, item.depth + 1));
        }
    }
}
