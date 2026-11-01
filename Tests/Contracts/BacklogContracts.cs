using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class BacklogContracts
{
    internal static void PagedBacklogAndExactRollback(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); edits.Connect(draft, new(new("sea"), new("next")), new("sea"));
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var project = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(static role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, new Inno.Text.FontAsset(), "Clear") }).ToArray() };
        using var session = new NovelSession(project); session.StartNew(); session.Advance(); session.Advance(); session.Choose("sea");
        for (int index = 0; index < 2300; index++) { session.Advance(); session.Advance(); }
        Check(session.historyCount > 2000, "History fixture did not exceed the old eager UI row limit.");
        var pages = new HistoryPageDataSource(session); pages.Open(); var latest = pages.ReadRows("history");
        Check(latest.Count is > 0 and <= 100 && latest[^1].values["index"].text == (session.historyCount - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "Paged history renumbered original voice/rollback row identities.");
        pages.Move(-1); Check(pages.ReadRows("history").Count == 100 && pages.TryRead("history.next", out NovelValue next) && next.boolean, "Backlog pages lost older rows or forward navigation.");
        pages.Move(int.MinValue); Check(pages.ReadRows("history")[0].key == "0", "History navigation escaped its first page.");
        pages.Move(int.MaxValue); Check(pages.ReadRows("history")[^1].key == (session.historyCount - 1).ToString(System.Globalization.CultureInfo.InvariantCulture), "History navigation escaped its last page.");
        using var view = new NovelGameView(project, session, static (_, _) => (100, 30)); session.OpenPage("history");
        var frame = view.BuildFrame();
        Check(frame.items.Count < 1000 && frame.items.Any(static item => item.id == "history-pagination"), "Actual history UI eagerly instantiated the complete backlog.");
        int target = session.historyCount - 10, abandoned = session.historyCount - 1;
        Check(!session.CanRollbackToHistory(0) && session.CanRollbackToHistory(target), "History did not distinguish expired checkpoints from retained positions.");
        var read = session.CaptureProgress().readContent;
        Check(session.RollbackToHistory(target) && session.historyCount == target + 1 && session.story!.nodeId.EndsWith("sea", StringComparison.Ordinal), "Backlog rollback did not restore the selected exact interactive boundary.");
        Check(!session.CanRollbackToHistory(abandoned) && read.All(value => session.CaptureProgress().readContent.Contains(value)), "Backlog rollback retained its abandoned future or reverted global read progress.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); session.Restore(saved);
        Check(!session.CanRollbackToHistory(target - 1), "Loading manufactured missing older rollback checkpoints.");
        bool rejected = false; try { session.ReadHistoryRange(-1, 100); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "A negative history range escaped bounds.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
