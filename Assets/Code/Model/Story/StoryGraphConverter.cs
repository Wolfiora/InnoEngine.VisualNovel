using System.Linq;
using InnoEngine.Graphs;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Story;

/// <summary>Persists the owned graph using native structured serialization and neutral graph records.</summary>
public sealed class StoryGraphConverter : SerializationConverter<StoryGraph>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        StoryGraph value
    ) {
        writer.WriteObjectArray("nodes", value.document.nodes, static (
            output,
            node
        ) =>
        {
            output.Write("id", node.id.value);
            output.Write("definition", node.definitionId);
            output.Write("x", node.position.x);
            output.Write("y", node.position.y);
            output.WriteObjectArray("values", node.values.OrderBy(static item => item.Key, System.StringComparer.Ordinal), static (
                property,
                item
            ) =>
            {
                property.Write("key", item.Key);
                property.Write("bytes", item.Value.ToArray());
            });
        });
        writer.WriteObjectArray("edges", value.document.edges, static (
            output,
            edge
        ) =>
        {
            output.Write("id", edge.id.value);
            output.Write("from", edge.output.nodeId.value);
            output.Write("out", edge.output.portId.value);
            output.Write("to", edge.input.nodeId.value);
            output.Write("in", edge.input.portId.value);
        });
        writer.WriteObjectArray("metadata", value.document.metadata.OrderBy(static item => item.Key, System.StringComparer.Ordinal), static (
            output,
            item
        ) =>
        {
            output.Write("key", item.Key);
            output.Write("bytes", item.Value.ToArray());
        });
    }

    /// <inheritdoc />
    public override StoryGraph Read(SerializationReader reader)
    {
        var graph = new StoryGraph();
        Restore(reader, graph);
        return graph;
    }

    /// <inheritdoc />
    public override void Restore(
        SerializationReader reader,
        StoryGraph target
    ) {
        var document = new GraphDocument();
        foreach (SerializationReader input in reader.ReadObjectArray("nodes"))
        {
            var node = new GraphNodeRecord(new(input.Read<string>("id")), input.Read<string>("definition"))
            {
                position = new(input.Read<float>("x"), input.Read<float>("y"))
            };
            foreach (SerializationReader property in input.ReadObjectArray("values"))
                node.SetValue(property.Read<string>("key"), new GraphSerializedValue(property.Read<byte[]>("bytes")));
            document.AddNode(node);
        }
        foreach (SerializationReader input in reader.ReadObjectArray("edges"))
            document.AddEdge(new GraphEdgeRecord(
                new(input.Read<string>("id")),
                new(new(input.Read<string>("from")), new(input.Read<string>("out"))),
                new(new(input.Read<string>("to")), new(input.Read<string>("in")))));
        foreach (SerializationReader item in reader.ReadObjectArray("metadata"))
            document.SetMetadata(item.Read<string>("key"), new GraphSerializedValue(item.Read<byte[]>("bytes")));
        target.document.ReplaceContents(document);
    }
}
