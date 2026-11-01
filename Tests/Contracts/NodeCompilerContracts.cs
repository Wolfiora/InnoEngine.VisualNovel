using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Graphs;
using Inno.Core.Serialization;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Compiler.Extensions;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NodeCompilerContracts
{
    internal static void NativeLoweringRestoreAndMissing(
        IStorySerialization native,
        SerializationRegistry serialization
    ) {
        var codec = new StoryNodeDataCodec(native); var source = Create(codec); byte[] original = native.Serialize(source.graph);
        var registrations = new StoryCompilerRegistry(); registrations.Register(static () => new PauseCompiler());
        var compiler = new StoryCompiler(native, registrations); var result = compiler.Compile(source);
        Check(result.success, string.Join("; ", result.diagnostics.Select(static issue => issue.message)));
        Check(native.Serialize(source.graph).SequenceEqual(original) && source.variables[0].name == "Score", "A compiler extension mutated the authoritative graph or variable declarations.");
        StoryProgram program = serialization.Deserialize<StoryProgram>(serialization.Serialize(result.program!));
        var pause = program.instructions.Single(static item => item.nodeId == "pause");
        Check(pause.definitionId == StoryNodeIds.wait && pause.sourceNodeId == "pause" && pause.sourceChapterId == "extensions" && pause.wait!.seconds == .5,
            "The extension leaked a custom opcode or lost its original source cursor.");
        using var player = new StoryMachine(program); player.Start("start"); player.Tick(.2); var state = player.Capture();
        using var restored = new StoryMachine(program); restored.Restore(state); restored.Tick(.3);
        Check(restored.status == StoryStatus.Completed, "A lowered wait did not use the ordinary restorable runtime instruction.");
        registrations.Clear(); var missing = compiler.Compile(source);
        Check(!missing.success && missing.diagnostics.Any(static issue => issue.nodeId?.value == "pause") && native.Serialize(source.graph).SequenceEqual(original),
            "Removing a compiler silently dropped the node or changed its neutral native payload.");
        registrations.Register(static () => new PauseCompiler());
        Check(compiler.Compile(source).program!.fingerprint == result.program!.fingerprint, "Re-registering the same compiler changed the restored program.");
        source.graph.document.AddNode(new(new("unreached"), StoryNodeIds.end));
        source.fragments[0].nodes = [.. source.fragments[0].nodes, "unreached"];
        var annotated = compiler.Compile(source);
        var warning = annotated.diagnostics.Single(static issue => issue.code == "VN_UNREACHABLE");
        Check(annotated.FormatDiagnostic(warning, static message => message.Format == "该节点无法从任何入口到达。" ? "Translated warning" : "Unexpected") == "Translated warning",
            "Custom node lowering discarded native diagnostic presentation annotations.");
    }
    internal static void InvalidExtensionsRemainAtomic(IStorySerialization native)
    {
        var source = Create(new(native)); byte[] original = native.Serialize(source.graph);
        foreach (string mode in new[] { "ports", "identity", "recursive", "exception", "blank-exception", "invalid" })
        {
            var registrations = new StoryCompilerRegistry(); registrations.Register(() => new PauseCompiler(mode));
            var result = new StoryCompiler(native, registrations).Compile(source);
            Check(!result.success && result.program is null && result.diagnostics.Any(static issue => issue.nodeId?.value == "pause"), "Invalid compiler behavior escaped source diagnostics: " + mode);
            Check(native.Serialize(source.graph).SequenceEqual(original), "Failed lowering modified the source: " + mode);
        }
        var duplicate = new StoryCompilerRegistry(); duplicate.Register(static () => new PauseCompiler());
        ExpectFailure(() => duplicate.Register(static () => new PauseCompiler()));
        ExpectFailure(() => new StoryCompilerRegistry().Register(static () => new PauseCompiler(id: StoryNodeIds.wait)));
        int calls = 0; var unstable = new StoryCompilerRegistry(); unstable.Register(() => new PauseCompiler(id: calls++ == 0 ? "example.pause" : "example.changed"));
        Check(new StoryCompiler(native, unstable).Compile(source).diagnostics.Any(static issue => issue.code == "VN_NODE_COMPILER"), "A factory changed the stable registered identity.");
        var codec = new StoryNodeDataCodec(native); var parallel = new StoryAsset
        {
            chapterId = "parallel-extensions", entries = [new() { id = "start", nodeId = "fork" }],
            variables = [new() { id = "score", name = "Score", initialValue = NovelValue.Integer(0) }],
            fragments = [new() { id = "main", nodes = ["fork", "a", "b", "join", "end"] }]
        };
        Add("fork", StoryNodeIds.parallel, new ParallelNode { branches = ["left", "right"], joinId = "join" });
        Add("a", "example.write", new Model.Story.Nodes.Variables.AssignmentNode { variableId = "score", value = new() { literal = NovelValue.Integer(1) } });
        Add("b", "example.write", new Model.Story.Nodes.Variables.AssignmentNode { variableId = "score", value = new() { literal = NovelValue.Integer(2) } });
        Add("join", StoryNodeIds.join, new JoinNode { forkId = "fork" }); parallel.graph.document.AddNode(new(new("end"), StoryNodeIds.end));
        Connect("fork", "left", "a"); Connect("fork", "right", "b"); Connect("a", "next", "join"); Connect("b", "next", "join"); Connect("join", "next", "end");
        var writers = new StoryCompilerRegistry(); writers.Register(static () => new WriteCompiler());
        var collision = new StoryCompiler(native, writers).Compile(parallel);
        Check(!collision.success && collision.diagnostics.Any(static issue => issue.code == "VN_PARALLEL_SCOPE" && issue.nodeId?.value is "a" or "b"),
            "Custom lowering bypassed the shared parallel variable-writer analysis.");
        void Add<T>(
            string id,
            string definition,
            T settings
        ) where T : class, ISerializable { var node = new GraphNodeRecord(new(id), definition); codec.Write(node, settings); parallel.graph.document.AddNode(node); }
        void Connect(
            string from,
            string port,
            string to
        )
            => parallel.graph.document.AddEdge(new(new(from + "/" + port), new(new(from), new(port)), new(new(to), new("in"))));
    }
    private static StoryAsset Create(StoryNodeDataCodec codec)
    {
        var source = new StoryAsset
        {
            chapterId = "extensions", entries = [new() { id = "start", nodeId = "entry" }],
            variables = [new() { id = "score", name = "Score", initialValue = NovelValue.Integer(0) }],
            fragments = [new() { id = "main", nodes = ["entry", "pause", "end"] }]
        };
        source.graph.document.AddNode(new(new("entry"), StoryNodeIds.entry));
        var pause = new GraphNodeRecord(new("pause"), "example.pause"); codec.Write(pause, new WaitNode { seconds = .5 });
        source.graph.document.AddNode(pause); source.graph.document.AddNode(new(new("end"), StoryNodeIds.end));
        source.graph.document.AddEdge(new(new("e1"), new(new("entry"), new("next")), new(new("pause"), new("in"))));
        source.graph.document.AddEdge(new(new("e2"), new(new("pause"), new("next")), new(new("end"), new("in")))); return source;
    }
    private sealed class PauseCompiler(
        string mode = "valid",
        string id = "example.pause"
    ) : StoryNodeCompiler(id, "参数化停顿", "测试")
    {
        public override IReadOnlyList<GraphPortDefinition> GetPorts(GraphNodeRecord node) =>
        [new(new("in"), "In", "inno.vn.flow", GraphPortDirection.Input, GraphPortCapacity.Multiple), new(new("next"), "Next", "inno.vn.flow", GraphPortDirection.Output)];
        public override GraphNodeRecord Lower(StoryNodeCompilationContext context)
        {
            context.variables[0].name = "Changed detached declaration";
            var wait = context.Read<WaitNode>();
            if (mode == "exception") throw new InvalidOperationException("Extension rejected the source.");
            if (mode == "blank-exception") throw new InvalidOperationException(" ");
            if (mode == "invalid") wait.seconds = -1;
            if (mode == "identity") return new(new("different"), StoryNodeIds.wait);
            return context.Create(mode == "ports" ? StoryNodeIds.end : mode == "recursive" ? id : StoryNodeIds.wait, wait);
        }
    }
    private sealed class WriteCompiler() : StoryNodeCompiler("example.write", "写分数", "测试")
    {
        public override IReadOnlyList<GraphPortDefinition> GetPorts(GraphNodeRecord node) =>
        [new(new("in"), "In", "inno.vn.flow", GraphPortDirection.Input, GraphPortCapacity.Multiple), new(new("next"), "Next", "inno.vn.flow", GraphPortDirection.Output)];
        public override GraphNodeRecord Lower(StoryNodeCompilationContext context)
            => context.Create(StoryNodeIds.assign, context.Read<Model.Story.Nodes.Variables.AssignmentNode>());
    }
    private static void ExpectFailure(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("An invalid compiler registration was accepted.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
