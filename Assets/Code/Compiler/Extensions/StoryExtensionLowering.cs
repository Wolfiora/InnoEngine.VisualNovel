using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Diagnostics;
using InnoEngine.Graphs;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Compiler.Extensions;

internal static class StoryExtensionLowering
{
    internal static StoryCompilationResult Compile(
        StoryAsset source,
        IStorySerialization serialization,
        StoryCompilerRegistry registry
    ) {
        var diagnostics = new List<GraphDiagnostic>(); string current = string.Empty;
        try
        {
            var extensions = registry.Create(); var codec = new StoryNodeDataCodec(serialization);
            var builtin = new StoryNodeCatalog(codec);
            var graph = serialization.Deserialize<StoryGraph>(serialization.Serialize(source.graph)).document;
            var resolver = new ExtensionResolver(builtin, extensions);
            foreach (var issue in GraphValidator.Validate(graph, resolver, allowCycles: true).diagnostics)
                diagnostics.Add(issue);
            if (diagnostics.Any(static issue => issue.severity == DiagnosticSeverity.Error)) return new(null, diagnostics);
            VariableDefinition[] variables = StoryVariableDeclarations.Merge(source.variables, source.variableTable);
            var lowered = new StoryGraph();
            foreach (var node in graph.nodes)
            {
                current = node.id.value;
                GraphNodeRecord value = node;
                if (extensions.TryGetValue(node.definitionId, out var extension))
                {
                    var before = extension.GetPorts(node).Select(PortKey).Order(StringComparer.Ordinal).ToArray();
                    value = extension.Lower(new(source.chapterId, node, codec, variables))
                        ?? throw new InvalidOperationException("The extension produced no instruction.");
                    if (value.id != node.id || !builtin.TryResolve(value.definitionId, out var definition) || definition is null)
                        throw new InvalidOperationException("An extension must preserve its node identity and lower to a built-in instruction.");
                    var after = definition.GetPorts(value).Select(PortKey).Order(StringComparer.Ordinal).ToArray();
                    if (!before.SequenceEqual(after)) throw new InvalidOperationException("Lowering changed the authored control-flow port contract.");
                }
                lowered.document.AddNode(value);
            }
            foreach (var edge in graph.edges) lowered.document.AddEdge(edge);
            foreach (var item in graph.metadata) lowered.document.SetMetadata(item.Key, item.Value);
            var candidate = new StoryAsset
            {
                chapterId = source.chapterId, title = source.title, graph = lowered, variables = variables,
                entries = source.entries.Select(static item => new StoryEntry { id = item.id, nodeId = item.nodeId }).ToArray(),
                fragments = source.fragments.Select(static item => new StoryFragment { id = item.id, name = item.name, nodes = item.nodes.ToArray() }).ToArray()
            };
            var compiled = new StoryCompiler(serialization).Compile(candidate);
            return new(compiled.program, diagnostics.Concat(compiled.diagnostics));
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            diagnostics.Add(new("VN_NODE_COMPILER", StoryCompiler.Explain(failure), DiagnosticSeverity.Error, current.Length > 0 ? new GraphNodeId(current) : null)); return new(null, diagnostics);
        }
    }
    private static string PortKey(GraphPortDefinition port)
        => string.Join("/", port.direction, port.id.value.Length, port.id.value, port.valueTypeId.Length, port.valueTypeId, port.capacity, port.required);
    private sealed class ExtensionResolver(
        StoryNodeCatalog builtin,
        IReadOnlyDictionary<string, StoryNodeCompiler> extensions
    ) : IGraphNodeDefinitionResolver
    {
        public bool TryResolve(
            string definitionId,
            out GraphNodeDefinition? definition
        ) {
            if (builtin.TryResolve(definitionId, out definition)) return true;
            if (!extensions.TryGetValue(definitionId, out var extension)) return false;
            definition = extension; return true;
        }
    }
}
