using System;
using System.Diagnostics;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ScriptViewportContracts
{
    internal static void MeasuredRowsAndStableIdentity()
    {
        string[] ids = Enumerable.Range(0, 10000).Select(static index => "line-" + index).ToArray();
        var viewport = new ScriptViewportLayout(); viewport.SetRows(ids, 100);
        var heights = ids.Select(static (_, index) => 20d + index % 7 * 3).ToArray();
        for (int index = 0; index < ids.Length; index++) viewport.Measure(ids[index], heights[index]);
        Near(viewport.height, heights.Sum());
        foreach (double top in new[] { -100d, 0, 400.25, 90000.25, viewport.height - 720, viewport.height + 1000 })
        {
            var visible = viewport.Visible(top, 720, 120);
            double firstY = 0; var expected = Enumerable.Range(0, ids.Length).Where(index =>
            {
                double y = firstY; firstY += heights[index]; return firstY > Math.Max(0, top - 120) && y <= Math.Max(0, top + 840);
            }).ToArray();
            Check(visible.first >= 0 && visible.end <= ids.Length && visible.end >= visible.first, "Script virtualization returned an invalid source interval.");
            Check(Enumerable.Range(visible.first, visible.end - visible.first).SequenceEqual(expected), "Measured script rows skipped or duplicated a visible source row.");
            Check(visible.end - visible.first < 60, "A small script viewport enumerated the complete 10000-line chapter.");
            Near(visible.before, heights.Take(visible.first).Sum()); Near(visible.after, heights.Skip(visible.end).Sum());
        }
        double middle = viewport.OffsetOf(ids[5000])!.Value;
        viewport.SetRows(ids.Reverse(), 300);
        Near(viewport.OffsetOf(ids[5000])!.Value, heights.Skip(5001).Sum()); Near(viewport.height, heights.Sum());
        viewport.SetRows([ids[5000], "new-row"], 99); Near(viewport.height, heights[5000] + 99);
        Reject(() => viewport.SetRows(["duplicate", "duplicate"], 100));
        Check(viewport.count == 2 && viewport.OffsetOf(ids[5000]) == 0 && viewport.OffsetOf("removed") is null && middle > 0,
            "Invalid row replacement mutated the existing view or row ownership.");
        viewport.ResetMeasurements(120); Near(viewport.height, 240); viewport.Measure("new-row", 250); Near(viewport.height, 370);
        Reject(() => viewport.Measure("new-row", double.NaN)); Reject(() => viewport.Measure("missing", 20));
        Reject(() => viewport.Visible(double.PositiveInfinity, 100)); Reject(() => viewport.Visible(0, -1)); Reject(() => viewport.EndOf(3));
        viewport.SetRows([], 100); Check(viewport.Visible(0, 100).first == 0 && viewport.Visible(0, 100).end == 0 && viewport.height == 0, "An empty chapter left phantom script rows.");
        viewport.SetRows(Enumerable.Range(0, 200).Select(static index => "large-" + index), 500000);
        Reject(() => viewport.Measure("large-0", 600000)); Reject(() => viewport.ResetMeasurements(600000));
        Reject(() => viewport.SetRows(Enumerable.Range(0, 201).Select(static index => "large-" + index), 500000));
        Check(viewport.count == 200 && viewport.height == 100000000, "A rejected geometry-budget change corrupted the active viewport.");
    }

    internal static void LargeGraphProjectionAndCompilation(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        const int dialogueCount = 9998;
        string[] ids = new[] { "entry" }.Concat(Enumerable.Range(0, dialogueCount).Select(static index => "line-" + index.ToString("D5"))).Append("end").ToArray();
        var draft = new StoryDraftState { chapterId = "large-chapter", fragments = [new() { id = "chapter", name = "Large chapter", nodes = ids }], entries = [new() { id = "start", nodeId = "entry" }] };
        for (int index = 0; index < ids.Length; index++)
        {
            string definition = index == 0 ? StoryNodeIds.entry : index == ids.Length - 1 ? StoryNodeIds.end : StoryNodeIds.dialogue;
            var node = new GraphNodeRecord(new(ids[index]), definition) { position = new(index % 100 * 320, index / 100 * 140) };
            if (definition == StoryNodeIds.dialogue) codec.Write(node, new DialogueNode { textId = "text-" + ids[index], speaker = "Author", text = "第 " + index + " 句台词。" });
            draft.graph.document.AddNode(node);
            if (index > 0) draft.graph.document.AddEdge(new(new("edge-" + index), new(new(ids[index - 1]), new("next")), new(node.id, new("in"))));
        }
        byte[] before = native.Serialize(draft);
        var clock = Stopwatch.StartNew(); var rows = ScriptProjectionBuilder.Build(draft); clock.Stop();
        Console.WriteLine("SCALE EVIDENCE: one 10000-node chapter projection took " + clock.Elapsed.TotalMilliseconds.ToString("0.0") + " ms on this headless host; no GUI frame-time claim.");
        Check(rows.Count == 10000 && rows.Select(static row => row.nodeId).SequenceEqual(ids), "The large script projection changed actual execution order or source identity.");
        var viewport = new ScriptViewportLayout(); viewport.SetRows(rows.Select(static row => row.nodeId), 180);
        var range = viewport.Visible(900000, 720, 180);
        Check(range.end - range.first < 10 && range.first == 4999, "The large graph was not reduced to its visible script interval.");
        Check(native.Serialize(draft).SequenceEqual(before), "Projection or virtualization changed the authoritative graph.");
        clock.Restart(); var compiled = compiler.Compile(draft.ToAsset()); clock.Stop();
        Console.WriteLine("SCALE EVIDENCE: one 10000-node chapter compilation took " + clock.Elapsed.TotalMilliseconds.ToString("0.0") + " ms; includes native program serialization, excludes GUI and cold project import.");
        Check(compiled.success && compiled.program!.instructions.Length == 10000, "The actual compiler rejected the 10000-node chapter: " + string.Join("; ", compiled.diagnostics.Select(static item => item.message)));
        Check(compiled.program!.instructions.Single(static item => item.nodeId == "line-05000").targets.Single().nodeId == "line-05001", "Indexed target lowering changed a large chapter's edges.");
        using var machine = new StoryMachine(compiled.program); machine.Start(); Check(machine.nodeId == "line-00000", "The compiled large chapter did not execute its real graph entry.");
        var moves = new StoryReorderService(native, codec, compiler); clock.Restart();
        var plan = moves.Preview(draft, new(ids[^2]), draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").id); clock.Stop();
        Console.WriteLine("SCALE EVIDENCE: a reviewed move across the 10000-node linear segment took " + clock.Elapsed.TotalMilliseconds.ToString("0.0") + " ms, including detached native copy and production compilation; no GUI responsiveness claim.");
        Check(!plan.crossesControlBoundary && native.Serialize(draft).SequenceEqual(before), "A large linear move lost its segment classification or mutated its preview source.");
        moves.Apply(draft, plan);
        Check(draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").input.nodeId.value == ids[^2]
            && draft.graph.document.nodes.Count == 10000, "An indexed large card move lost its reviewed order or node ownership.");
    }
    private static void Near(
        double actual,
        double expected
    ) => Check(Math.Abs(actual - expected) < .001, $"Script viewport expected {expected}, got {actual}.");
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid script viewport input was accepted.");
    }
}
