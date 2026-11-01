using System;
using System.Collections.Generic;
using InnoEngine.Graphs;
using InnoEngine.Serialization;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Templates;

internal sealed class StoryTemplateBuilder(
    StoryNodeDataCodec codec,
    string title
)
{
    private readonly StoryDraftState m_draft = new() { chapterId = Guid.NewGuid().ToString("N"), title = title };
    private readonly List<string> m_order = [];
    internal void Add(
        string id,
        string kind
    ) => AddNode(id, kind);
    internal void Add<T>(
        string id,
        string kind,
        T settings
    ) where T : class, ISerializable => codec.Write(AddNode(id, kind), settings);
    private GraphNodeRecord AddNode(
        string id,
        string kind
    ) {
        var node = new GraphNodeRecord(new(id), kind) { position = new((m_order.Count % 5) * 320, (m_order.Count / 5) * 190) };
        m_draft.graph.document.AddNode(node); m_order.Add(id);
        return node;
    }
    internal void Link(
        string from,
        string to,
        string port = "next"
    ) => m_draft.graph.document.AddEdge(new(new(Guid.NewGuid().ToString("N")), new(new(from), new(port)), new(new(to), new("in"))));
    internal StoryDraftState Build(
        VariableDefinition[]? variables = null,
        StoryEntry[]? entries = null
    ) {
        m_draft.variables = variables ?? [];
        m_draft.entries = entries ?? [new() { id = "start", nodeId = "entry" }];
        m_draft.fragments = [new() { id = "opening", name = "教学流程", nodes = m_order.ToArray() }]; return m_draft;
    }
}
