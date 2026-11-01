using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Debugging;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class DebuggerContracts
{
    internal static void BreakpointAndStep(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var condition = new ExpressionDocument { operation = ExpressionOperation.Equal, operands = [new() { operation = ExpressionOperation.Variable, variableId = "player" }, new() { literal = NovelValue.String("旅人") }] };
        var debugger = new StoryDebugger(8); debugger.SetBreakpoints([new(chapter.chapterId, "hello", condition)]);
        condition.operands[1].literal = NovelValue.String("caller changed");
        using var machine = new StoryMachine(chapter.program, seed: 23); machine.AttachDebugger(debugger); machine.Start();
        Check(machine.debugPaused && machine.status == StoryStatus.Running && debugger.location?.node == "hello" && !machine.canSave,
            "Source breakpoint did not hold immediately before entering its instruction or exposed an unstable save.");
        ulong random = machine.Capture().randomState; double elapsed = machine.Capture().waitElapsed;
        machine.Tick(90);
        Check(machine.Capture().randomState == random && machine.Capture().waitElapsed == elapsed && !machine.Advance() && !machine.Choose("sea"),
            "Paused debugging consumed logical time, randomness or player input.");
        var values = machine.InspectVariables(); values.Single(item => item.id == "player").value = NovelValue.String("alias");
        Check(machine.GetVariable("player").text == "旅人", "Debug observations mutated live values through aliases.");
        debugger.Continue(true); machine.Tick(0);
        Check(debugger.paused && machine.status == StoryStatus.Dialogue && machine.visibleGraphemes == 0 && debugger.HasVisited(chapter.chapterId, "hello"),
            "Single stepping skipped a breakpoint repeatedly, revealed text or ran beyond the natural dialogue wait.");
        var state = native.Deserialize<StoryExecutionState>(native.Serialize(machine.Capture()));
        using var restored = new StoryMachine(chapter.program); restored.Restore(state);
        Check(!restored.debugPaused && restored.status == StoryStatus.Dialogue, "Debug ownership leaked into ordinary native game state.");
        bool rejected = false; try { restored.AttachDebugger(debugger); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Two live sessions shared a debugger and cross-paused each other.");
        debugger.Continue(); machine.Advance(); machine.Advance(); machine.Choose("sea"); machine.Advance(); machine.Advance();
        Check(machine.status == StoryStatus.Completed && debugger.trace.Count <= 8 && debugger.coveredNodes == 5
            && debugger.trace.Any(static entry => entry.output == "sea"), "Bounded source and branch coverage lost actual graph execution.");
        machine.Dispose(); restored.AttachDebugger(debugger);
        Check(!debugger.paused && debugger.location is null, "Disposal retained a stopped task or prevented owner reuse.");
    }

    internal static void ConditionFailureAndCalls(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var invoke = edits.Add(draft, "opening", StoryNodeIds.call, new CallNode { entryId = "visit" }, new(10, 20));
        draft.graph.document.AddNode(new(new("return"), StoryNodeIds.returnFromCall)); draft.fragments[0].nodes = [.. draft.fragments[0].nodes, "return"];
        draft.entries = [.. draft.entries, new() { id = "visit", nodeId = "sea" }];
        edits.Connect(draft, new(new("hello"), new("next")), invoke); edits.Connect(draft, new(invoke, new("next")), new("end"));
        edits.Connect(draft, new(new("sea"), new("next")), new("return"));
        var result = compiler.Compile(draft.ToAsset()); Check(result.success, string.Join(";", result.diagnostics.Select(static item => item.message)));
        using var machine = new StoryMachine(result.program!); var debugger = new StoryDebugger();
        debugger.SetBreakpoints([new(draft.chapterId, "hello", new() { operation = ExpressionOperation.Variable, variableId = "unavailable" })]); machine.AttachDebugger(debugger); machine.Start();
        Check(debugger.paused && debugger.reason.Contains("unavailable", StringComparison.Ordinal) && machine.status != StoryStatus.Faulted,
            "A debug condition error faulted the story or failed to retain its original position.");
        debugger.SetBreakpoints([new(draft.chapterId, "sea")]); debugger.Continue(); machine.Tick(0); machine.Advance(); machine.Advance();
        Check(debugger.paused && debugger.location?.node == "sea" && machine.InspectCalls().Count == 1,
            "Callee source breakpoints or invocation inspection depended on an active coroutine stack.");
        var calls = machine.InspectCalls(); calls[0].returnNode = "corrupted";
        Check(machine.InspectCalls()[0].returnNode == "end", "Debug call inspection aliased the interpreter continuation.");
        debugger.Continue(true); machine.Tick(0); debugger.Continue(); machine.Advance(); machine.Advance();
        Check(machine.status == StoryStatus.Completed && machine.InspectCalls().Count == 0 && debugger.trace.Any(static item => item.output == "return"),
            "Debug stepping changed call/return behavior or omitted return trace.");
        bool rejected = false;
        try { _ = new StoryBreakpoint(draft.chapterId, "hello", new() { operation = ExpressionOperation.RandomNumber, operands = [new(), new()] }); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "A breakpoint condition was allowed to consume narrative random state.");
    }

    internal static void ParallelAndSessionClocks(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec); var groups = new ParallelEditService(edits, codec);
        var fork = groups.Create(draft, "opening", new(10, 20)); var definition = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!);
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(definition.joinId), new("next")), new("hello"));
        string first = draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == definition.branches[0]).input.nodeId.value;
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var debugger = new StoryDebugger(); debugger.SetBreakpoints([new(chapter.chapterId, first)]);
        using var session = new NovelSession(new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId }, debugger: debugger); session.StartNew();
        Check(debugger.paused && session.story!.status == StoryStatus.Parallel && session.story.InspectTasks().Count == 3
            && debugger.stoppedTask?.sourceNodeId == first && debugger.location!.task != "main", "Task debugging lost the bounded branch tree or stopped the wrong owner.");
        double clock = session.stage.Capture().time;
        session.Tick(10);
        Check(session.stage.Capture().time == clock && session.story!.InspectTasks().Skip(1).All(static task => task.Capture().waitElapsed == 0),
            "A branch breakpoint allowed another task or presentation time to continue.");
        debugger.Continue(true); session.Tick(0);
        Check(debugger.paused && debugger.stoppedTask?.status == StoryStatus.Timer && !session.story!.canSave,
            "Stepping a task did not hold the other unentered branch or exposed an incomplete task snapshot as savable.");
        debugger.Continue(); session.Tick(0); session.Tick(2);
        Check(!debugger.paused && session.story!.status == StoryStatus.Dialogue, "Resumed parallel waits did not rejoin normal session execution.");
        debugger.Pause(session.story!); string text = session.story!.visibleText; session.Tick(10);
        Check(session.story.visibleText == text && !session.Advance(), "Manual debug pause allowed automatic or manual reading progress.");
        debugger.Continue(); session.Tick(0.2); Check(session.story.visibleText.Length > text.Length, "Continuing a paused natural wait failed to restore ordinary reading time.");
    }
    private static void Check(
        bool condition,
        string message
    ) {
        if (!condition) throw new InvalidOperationException(message);
    }
}
