using System;
using System.Linq;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ChapterContracts
{
    internal static void LinkCallAndJump(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits
    ) {
        var first = FirstStoryTemplate.Create(codec); first.chapterId = "a:chapter";
        var second = FirstStoryTemplate.Create(codec); second.chapterId = "b:chapter";
        second.graph.document.AddNode(new(new("return"), StoryNodeIds.returnFromCall));
        second.fragments[0].nodes = [.. second.fragments[0].nodes, "return"];
        edits.Connect(second, new(new("hello"), new("next")), new("return"));
        var invoke = edits.Add(first, "opening", StoryNodeIds.call, new CallNode { chapterId = second.chapterId }, new(0, 300));
        edits.Connect(first, new(new("entry"), new("next")), invoke);
        edits.Connect(first, new(invoke, new("next")), new("hello"));
        var a = first.ToAsset(); a.program = compiler.Compile(a).program!;
        var b = second.ToAsset(); b.program = compiler.Compile(b).program!;
        var game = new NovelProjectAsset { chapters = [a, b], startChapter = a.chapterId };
        using var session = new NovelSession(game); session.StartNew();
        Check(session.story!.sourceChapterId == second.chapterId && session.story.sourceNodeId == "hello", "Cross-chapter source mapping was lost.");
        var saved = session.Capture();
        session.Advance(); session.Advance();
        Check(session.story.status == StoryStatus.Dialogue && session.story.sourceChapterId == first.chapterId, "Cross-chapter return did not restore its continuation.");
        session.Restore(saved); session.Advance(); session.Advance();
        Check(session.story.sourceChapterId == first.chapterId, "Saved cross-chapter call stack did not restore.");
        CallNode jump = codec.Read<CallNode>(first.graph.document.FindNode(invoke)!); jump.jump = true;
        edits.SetSettings(first, invoke, jump, true);
        a.program = compiler.Compile(first.ToAsset()).program!;
        using var jumped = new NovelSession(game); jumped.StartNew(); jumped.Advance(); jumped.Advance();
        Check(jumped.story!.status == StoryStatus.Faulted && jumped.story.error.Contains("active invocation", StringComparison.Ordinal), "A jump incorrectly added a return continuation.");
        b.program!.variables.Single().initialValue = NovelValue.String("incompatible default");
        bool rejected = false;
        try { _ = new NovelSession(game); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Inconsistent shared variable declarations were silently accepted.");
        Check(a.program.instructions.Single(item => item.nodeId == invoke.value).call!.chapterId == second.chapterId, "Runtime linking mutated imported chapter assets.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
