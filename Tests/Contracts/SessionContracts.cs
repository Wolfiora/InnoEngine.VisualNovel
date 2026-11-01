using System;
using System.Linq;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class SessionContracts
{
    internal static void ReadingAndRollback(StoryNodeDataCodec codec, StoryCompiler compiler)
    {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset();
        chapter.variables = [.. chapter.variables, new() { id = "unlocked", scope = VariableScope.Global, initialValue = NovelValue.Boolean(false) }];
        chapter.program = compiler.Compile(chapter).program!;
        var session = new NovelSession(new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId });
        session.StartNew();
        session.Advance();
        Check(session.story!.status == StoryStatus.Dialogue && session.story.isTextComplete, "First advance skipped a dialogue.");
        session.story.SetVariable("unlocked", NovelValue.Boolean(true));
        session.Advance();
        Check(session.story.status == StoryStatus.Choice && session.canRollback, "Choice boundary was not checkpointed.");
        session.Choose("sea");
        Check(session.history.Count == 2 && session.story.status == StoryStatus.Dialogue, "Selected branch was not recorded.");
        Check(session.Rollback() && session.story.status == StoryStatus.Choice, "Rollback did not restore the decision.");
        session.Choose("forest");
        Check(session.history.Count == 2 && session.history.Last().nodeId == "forest", "Rollback retained the abandoned future.");
        Check(session.story.GetVariable("unlocked").boolean, "Rollback revoked global progress.");
        session.OpenPage("settings");
        var before = session.story.Capture(); session.Tick(4);
        Check(session.story.Capture().revealProgress == before.revealProgress, "Modal system page advanced story time.");
        session.OpenPage("reading"); session.Tick(20);
        Check(session.story.isTextComplete, "Resumed game did not update reading.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
