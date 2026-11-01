using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Rendering.Assets;
using Inno.Text;
using Inno.VisualNovel.Authoring.Publishing;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class SystemActionContracts
{
    internal static void ModalAndNativeResume(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var theme = edits.Add(draft, "opening", StoryNodeIds.system, new SystemNode { operation = SystemOperation.SetTheme, argument = "night" }, new());
        var open = edits.Add(draft, "opening", StoryNodeIds.system, new SystemNode { argument = "custom.notes" }, new());
        var assign = edits.Add(draft, "opening", StoryNodeIds.assign, new AssignmentNode { variableId = "player", value = new() { literal = NovelValue.String("页面关闭后") } }, new());
        edits.Connect(draft, new(new("entry"), new("next")), theme); edits.Connect(draft, new(theme, new("next")), open);
        edits.Connect(draft, new(open, new("next")), assign); edits.Connect(draft, new(assign, new("next")), new("hello"));
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        NovelProjectAsset game = Game(chapter); game.themes = [new ThemeAsset { resourceId = "day" }, new ThemeAsset { resourceId = "night" }]; game.defaultTheme = "day";
        game.screens = [.. game.screens, new() { role = "custom.notes", screen = new() { width = 400, height = 200, initialFocus = "close", root = new() { id = "root", layout = new() { width = 400, height = 200 },
            children = [new() { id = "close", kind = UiElementKind.Button, action = "back", text = "关闭", layout = new() { x = 20, y = 20, width = 300, height = 100 } }] } } }];
        using var session = new NovelSession(game); using var view = new NovelGameView(game, session, static (_, _) => (100, 40));
        session.StartNew();
        Check(session.themeId == "night" && session.page == "custom.notes" && session.story!.status == StoryStatus.Suspended
            && session.story.GetVariable("player").text == "旅人", "Opening an authored page executed the following assignment before the page closed.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); session.Tick(20);
        Check(session.story!.status == StoryStatus.Suspended && session.playTime == 0, "An open custom modal advanced the hidden narrative.");
        view.BuildFrame(); view.HandleInput(new() { confirm = true });
        Check(session.page == "reading" && session.story.status == StoryStatus.Suspended, "Closing a page reused the confirming input in the following instruction.");
        session.Tick(0);
        Check(session.story.status == StoryStatus.Dialogue && session.story.GetVariable("player").text == "页面关闭后" && !session.story.isTextComplete,
            "Returning to reading did not resume the exact stored continuation once.");
        session.SetTheme("day"); session.Restore(saved); session.Tick(0);
        Check(session.page == "reading" && session.themeId == "night" && session.story!.status == StoryStatus.Dialogue,
            "Loading a suspended continuation repeated its page action or lost the selected theme.");
        var current = session.story; saved.narrative.nodeId = "hello";
        Reject(() => session.Restore(saved)); Check(ReferenceEquals(current, session.story), "Invalid suspended cursor replaced the current session.");
    }
    internal static void ValidationAndTitle(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var system = edits.Add(draft, "opening", StoryNodeIds.system, new SystemNode { operation = SystemOperation.ReturnTitle, argument = string.Empty }, new());
        edits.Insert(draft, draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").id, system);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!; var game = Game(chapter);
        using var session = new NovelSession(game); session.StartNew(); session.Tick(2);
        Check(session.page == "title" && session.story!.status == StoryStatus.Suspended, "ReturnTitle executed hidden narrative behind the title page.");
        session.OpenPage("reading"); session.Tick(0); Check(session.story!.nodeId == "hello", "Title continuation lost its next source node.");
        edits.SetSettings(draft, system, new SystemNode { operation = SystemOperation.SetTheme, argument = "missing-theme" });
        chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!; game = Game(chapter);
        Reject(() => NovelProjectValidator.Validate(game));
        using var unavailable = new NovelSession(game); unavailable.StartNew();
        Check(unavailable.story!.status == StoryStatus.Faulted && unavailable.story.error.Contains(system.value, StringComparison.Ordinal) && unavailable.themeId.Length == 0,
            "An unavailable system resource failed without a source location or partially changed theme.");
        edits.SetSettings(draft, system, new SystemNode { operation = (SystemOperation)99 });
        Check(!compiler.Compile(draft.ToAsset()).success, "Unknown system operation compiled successfully.");
        draft = FirstStoryTemplate.Create(codec); var fork = edits.AddParallel(draft, "opening", new());
        var parallel = codec.Read<Model.Story.Nodes.Flow.ParallelNode>(draft.graph.document.FindNode(fork)!);
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(parallel.joinId), new("next")), new("hello"));
        Check(compiler.Compile(draft.ToAsset()).success, "The parallel ownership fixture was invalid before inserting a system action.");
        var branch = draft.graph.document.edges.First(edge => edge.output.nodeId == fork);
        var blocked = edits.Add(draft, "opening", StoryNodeIds.system, new SystemNode(), new()); edits.Insert(draft, branch.id, blocked);
        var rejected = compiler.Compile(draft.ToAsset());
        Check(!rejected.success && rejected.diagnostics.Any(issue => issue.nodeId?.value == blocked.value), "Parallel presentation branch acquired the session's modal input or theme owner.");
    }
    private static NovelProjectAsset Game(Model.Story.StoryAsset chapter)
    {
        var font = new FontAsset(); return new() { chapters = [chapter], startChapter = chapter.chapterId, font = font, material = new MaterialAsset(),
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, font, "Clear") }).ToArray() };
    }
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Invalid system state or resource was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
