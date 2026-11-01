using System;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Creates a complete branching story with ordinary editable graph data.</summary>
public static class FirstStoryTemplate
{
    /// <summary>Builds a fresh sample without shared asset or node identities.</summary>
    /// <param name="codec">The native node settings codec.</param>
    /// <returns>A valid detached chapter with two endings.</returns>
    public static StoryDraftState Create(StoryNodeDataCodec codec)
    {
        var state = new StoryDraftState
        {
            chapterId = Guid.NewGuid().ToString("N"), title = "第一段故事 · First Story",
            variables = [new() { id = "player", name = "玩家姓名", initialValue = NovelValue.String("旅人") }]
        };
        var graph = state.graph.document;
        Add("entry", StoryNodeIds.entry, 0, 0);
        Add("hello", StoryNodeIds.dialogue, 280, 0);
        codec.Write(graph.FindNode(new("hello"))!, new DialogueNode
        {
            textId = Guid.NewGuid().ToString("N"), speaker = "澄", text = "你好，{player}。从这里开始写下你的故事。"
        });
        Add("choice", StoryNodeIds.choice, 560, 0);
        codec.Write(graph.FindNode(new("choice"))!, new ChoiceNode
        {
            prompt = "你想走向哪里？",
            options = [new() { id = "sea", text = "去看海" }, new() { id = "forest", text = "沿林间小径前行" }]
        });
        Add("sea", StoryNodeIds.dialogue, 840, -110);
        codec.Write(graph.FindNode(new("sea"))!, new DialogueNode { textId = Guid.NewGuid().ToString("N"), text = "海风送来了远方的消息。" });
        Add("forest", StoryNodeIds.dialogue, 840, 150);
        codec.Write(graph.FindNode(new("forest"))!, new DialogueNode { textId = Guid.NewGuid().ToString("N"), text = "树叶间的光点，像还没写完的句子。" });
        Add("end", StoryNodeIds.end, 1140, 30);
        Link("entry", "next", "hello");
        Link("hello", "next", "choice");
        Link("choice", "sea", "sea");
        Link("choice", "forest", "forest");
        Link("sea", "next", "end");
        Link("forest", "next", "end");
        state.entries = [new() { id = "start", nodeId = "entry" }];
        state.fragments = [new() { id = "opening", name = "序章", nodes = ["entry", "hello", "choice", "sea", "forest", "end"] }];
        return state;

        void Add(
            string id,
            string kind,
            float x,
            float y
        ) => graph.AddNode(new(new(id), kind) { position = new(x, y) });
        void Link(
            string from,
            string port,
            string to
        ) => graph.AddEdge(new(new(Guid.NewGuid().ToString("N")), new(new(from), new(port)), new(new(to), new("in"))));
    }
}
