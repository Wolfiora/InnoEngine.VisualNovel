using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Builds the beginner story with ordinary visible character and scenery instructions.</summary>
public static class StarterPresentationTemplate
{
    /// <summary>Creates a writable branching chapter whose presentation uses explicit packaged resource keys.</summary>
    /// <param name="codec">The native node-settings codec shared with graph and script editing.</param>
    /// <returns>One authoritative editable graph; no sample subtree or hidden runtime setup is referenced.</returns>
    public static StoryDraftState Create(StoryNodeDataCodec codec)
    {
        StoryDraftState draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var hello = draft.graph.document.FindNode(new("hello"))!; var line = codec.Read<Model.Story.Nodes.Reading.DialogueNode>(hello);
        line.characterId = "builtin.character.cheng"; line.speaker = string.Empty; codec.Write(hello, line);
        GraphNodeId actor = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode
        { operation = StageOperation.ShowCharacter, resourceId = "builtin.character.cheng", target = "guide", pose = "wave", expression = "smile", transform = new() { x = 660, y = 130 } }, new(140, 160));
        edits.Insert(draft, draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").id, actor);
        foreach (string route in new[] { "sea", "forest" })
        {
            GraphNodeId scenery = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode
            { operation = StageOperation.SetStage, resourceId = route == "sea" ? "builtin.stage.seafront" : "builtin.stage.woodland" }, new(700, route == "sea" ? -150 : 180));
            edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId.value == "choice" && edge.output.portId.value == route).id, scenery);
        }
        return draft;
    }
}
