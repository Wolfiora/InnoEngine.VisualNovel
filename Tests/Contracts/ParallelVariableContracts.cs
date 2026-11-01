using System;
using System.Linq;
using Inno.Core.Graphs;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ParallelVariableContracts
{
    internal static void SharedScopesAndCandidateRestore(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var fixture = Create(codec); var chapter = fixture.draft.ToAsset(); chapter.program = Compile(compiler, chapter);
        using var session = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId });
        session.StartNew(); session.Tick(.3);
        Check(session.story!.status == StoryStatus.Parallel && Number(session, "x") == 1 && Number(session, "g") == 1 && Number(session, "y") == 0,
            "A completed branch lost a shared Game/Global write while another branch remained active.");
        var snapshot = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        Check(snapshot.narrative.tasks.All(task => task.execution.gameVariables.Single(static item => item.id == "x").value.number == 1),
            "Parallel snapshots retained stale fork-local copies of shared variables.");
        session.Tick(.3); Check(session.story.status == StoryStatus.Dialogue && Number(session, "y") == 11, "A later branch did not observe the earlier committed write or preserve its Local result at join.");
        session.Restore(snapshot); Check(Number(session, "y") == 0 && Number(session, "g") == 1, "Restoring game/local scopes incorrectly rewound global progress.");
        session.Tick(.3); Check(Number(session, "x") == 1 && Number(session, "y") == 11 && Number(session, "g") == 1,
            "Restored child scopes became detached, restarted completed assignments, or duplicated a global write.");
        var damaged = native.Deserialize<NovelSessionState>(native.Serialize(snapshot));
        damaged.narrative.tasks[1].execution.gameVariables.Single(static item => item.id == "x").value = NovelValue.Integer(999);
        byte[] before = native.Serialize(session.Capture());
        Reject(() => session.Restore(damaged));
        Check(native.Serialize(session.Capture()).SequenceEqual(before), "A mismatching child scope partially replaced the live session.");
        session.Advance(); session.Advance(); session.Choose("sea");
        Check(session.Rollback() && Number(session, "x") == 1 && Number(session, "y") == 11 && Number(session, "g") == 1,
            "Rollback discarded joined branch results or rewound global progress.");
    }

    internal static void OrderConflictsAndWaitAny(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        IStorySerialization native
    ) {
        var fixture = Create(codec); var edits = new StoryEditService(codec); var groups = new ParallelEditService(edits, codec);
        var definition = codec.Read<ParallelNode>(fixture.draft.graph.document.FindNode(fixture.fork)!);
        foreach (var output in fixture.draft.graph.document.edges.Where(edge => edge.output.nodeId == fixture.fork)) edits.SetSettings(fixture.draft, output.input.nodeId, new WaitNode { seconds = 0 });
        var program = Compile(compiler, fixture.draft.ToAsset());
        using (var machine = new StoryMachine(program)) { machine.Start(); Check(machine.GetVariable("y").number == 11, "Stable branch order failed to expose an earlier same-update assignment."); }
        var edges = fixture.draft.graph.document.edges.Select(static edge => (edge.id, edge.output, edge.input)).ToArray();
        groups.MoveBranch(fixture.draft, fixture.fork, definition.branches[1], 0);
        Check(fixture.draft.graph.document.edges.Select(static edge => (edge.id, edge.output, edge.input)).SequenceEqual(edges), "Changing scheduling order rewired execution edges.");
        using (var machine = new StoryMachine(Compile(compiler, fixture.draft.ToAsset())))
        { machine.Start(); Check(machine.GetVariable("y").number == 10 && machine.GetVariable("x").number == 1, "Explicit scheduling order was ignored or a later task reset shared variables."); }
        byte[] before = native.Serialize(fixture.draft); Reject(() => groups.MoveBranch(fixture.draft, fixture.fork, definition.branches[0], 99));
        Check(native.Serialize(fixture.draft).SequenceEqual(before), "Invalid scheduling edits partially changed the graph.");

        var assignment = codec.Read<AssignmentNode>(fixture.draft.graph.document.FindNode(fixture.second)!); assignment.variableId = "x";
        edits.SetSettings(fixture.draft, fixture.second, assignment);
        var rejected = compiler.Compile(fixture.draft.ToAsset());
        Check(!rejected.success && rejected.diagnostics.Any(static item => item.code == "VN_PARALLEL_SCOPE" && item.message.Contains("variable/x", StringComparison.Ordinal)),
            "Competing variable writers were not located by the real compiler.");
        var forged = Model.Programs.StoryProgramCopy.Copy(program); forged.instructions.Single(item => item.nodeId == fixture.second.value).assignment!.variableId = "x";
        using (var machine = new StoryMachine(forged))
        {
            machine.Start(); Check(machine.status == StoryStatus.Faulted && machine.error.Contains("x", StringComparison.Ordinal) && machine.GetVariable("x").number == 0,
                "Runtime accepted competing writers or executed one before validating the group.");
        }
        fixture = Create(codec); definition = codec.Read<ParallelNode>(fixture.draft.graph.document.FindNode(fixture.fork)!); definition.waitAny = true;
        edits.SetSettings(fixture.draft, fixture.fork, definition);
        using (var machine = new StoryMachine(Compile(compiler, fixture.draft.ToAsset())))
        {
            machine.Start(); machine.Tick(.3);
            Check(machine.status == StoryStatus.Dialogue && machine.GetVariable("x").number == 1 && machine.GetVariable("y").number == 0,
                "Wait-any undid committed writes or ran a cancelled branch's future assignment.");
        }
    }

    private static (StoryDraftState draft, GraphNodeId fork, GraphNodeId second) Create(StoryNodeDataCodec codec)
    {
        var edits = new StoryEditService(codec); var draft = FirstStoryTemplate.Create(codec);
        draft.variables = [.. draft.variables, new() { id = "x", name = "Shared X", scope = VariableScope.Game, initialValue = NovelValue.Integer(0) },
            new() { id = "y", name = "Shared local Y", scope = VariableScope.Local, initialValue = NovelValue.Integer(0) },
            new() { id = "g", name = "Global", scope = VariableScope.Global, initialValue = NovelValue.Integer(0) }];
        var fork = edits.AddParallel(draft, "opening", new(100, 100)); var definition = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!);
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(definition.joinId), new("next")), new("hello"));
        var waits = definition.branches.Select(branch => draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == branch).input.nodeId).ToArray();
        edits.SetSettings(draft, waits[0], new WaitNode { seconds = .25 }); edits.SetSettings(draft, waits[1], new WaitNode { seconds = .5 });
        var first = edits.Add(draft, "opening", StoryNodeIds.assign, new AssignmentNode { variableId = "x", value = Add("x", 1) }, new(400, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId == waits[0]).id, first);
        var global = edits.Add(draft, "opening", StoryNodeIds.assign, new AssignmentNode { variableId = "g", value = Add("g", 1) }, new(600, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId == first).id, global);
        var second = edits.Add(draft, "opening", StoryNodeIds.assign, new AssignmentNode { variableId = "y", value = Add("x", 10) }, new(400, 200));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId == waits[1]).id, second);
        return (draft, fork, second);
    }
    private static ExpressionDocument Add(
        string variable,
        int number
    ) => new() { operation = ExpressionOperation.Add, operands = [new() { operation = ExpressionOperation.Variable, variableId = variable }, new() { literal = NovelValue.Integer(number) }] };
    private static Model.Programs.StoryProgram Compile(
        StoryCompiler compiler,
        StoryAsset chapter
    ) {
        var result = compiler.Compile(chapter); Check(result.success, string.Join("; ", result.diagnostics.Select(static item => item.message))); return result.program!;
    }
    private static double Number(
        NovelSession session,
        string id
    ) => session.story!.GetVariable(id).number;
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Invalid parallel-variable operation was accepted."); }
}
