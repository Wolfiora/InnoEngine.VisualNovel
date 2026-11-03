using System;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CompilerDiagnosticContracts
{
    internal static void NativeIdentityAndOwnerFormatting(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        string custom = "custom/标题{0}##原始名称";
        var draft = FirstStoryTemplate.Create(codec);
        draft.graph.document.AddNode(new(new("missing"), custom));
        draft.fragments[0].nodes = [.. draft.fragments[0].nodes, "missing"];
        byte[] before = native.Serialize(draft);
        var result = compiler.Compile(draft.ToAsset());
        GraphDiagnostic issue = result.diagnostics.Single(static item => item.code == "VN_NODE_MISSING");
        Check(!result.success && !result.validation.isValid && issue.nodeId?.value == "missing" && issue.edgeId is null
            && issue.message == "Node implementation is unavailable: " + custom + ". Original data is preserved."
            && native.Serialize(draft).SequenceEqual(before), "Native diagnostics lost identity, literal user values or source isolation.");
        draft = FirstStoryTemplate.Create(codec);
        draft.graph.document.AddEdge(new(new("broken-edge"), new(new("hello"), new("inactive-port")), new(new("choice"), new("in"))));
        var invalid = compiler.Compile(draft.ToAsset());
        GraphDiagnostic edge = invalid.diagnostics.Single(static item => item.code == "GRAPH_MISSING_ENDPOINT_PORT");
        Check(edge.edgeId?.value == "broken-edge" && edge.nodeId is null, "The compiler flattened native edge identity.");
        draft = FirstStoryTemplate.Create(codec);
        draft.graph.document.AddNode(new(new("orphan"), StoryNodeIds.end));
        draft.fragments[0].nodes = [.. draft.fragments[0].nodes, "orphan"];
        var warning = compiler.Compile(draft.ToAsset());
        Check(warning.success && warning.validation.isValid
            && warning.diagnostics.Single(static item => item.code == "VN_UNREACHABLE").message == "This node cannot be reached from any entry.",
            "The English diagnostic treated an unreachable-node warning as a compile failure.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}