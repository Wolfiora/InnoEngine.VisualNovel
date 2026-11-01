using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ChoiceCountdownContracts
{
    internal static void ActualPagesNativeClockAndRollback(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var choice = codec.Read<ChoiceNode>(draft.graph.document.FindNode(new("choice"))!);
        choice.timeout = 10; choice.defaultOption = "sea"; codec.Write(draft.graph.document.FindNode(new("choice"))!, choice);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        Check(BuiltInUiChoices.NumericValues([]).Any(static item => item.id == "choice.timeRemainingRatio")
            && !BuiltInUiChoices.Numbers([]).Any(static item => item.id.StartsWith("choice.", StringComparison.Ordinal))
            && !BuiltInUiChoices.TextValues([]).Any(static item => item.id == "choice.remainingSeconds"), "The visual editor exposed an observation-only timer as writable or as a String field.");
        foreach (string family in ShellThemeTemplates.names)
        {
            var theme = ShellThemeTemplates.Create(family, new FontAsset());
            var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, themes = [theme], defaultTheme = theme.resourceId, screens = theme.screens };
            using var session = new NovelSession(game); session.StartNew(); session.Advance(); session.Advance();
            using var view = new NovelGameView(game, session, static (element, text) => (text.Length * 14, element.style.fontSize));
            var source = new NovelSessionDataSource(session);
            UiTimer(1, 10); session.Tick(3.25); UiTimer(.675, 7);
            var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
            session.OpenPage("settings"); session.Tick(30); session.OpenPage("reading"); UiTimer(.675, 7);
            Check(!source.TryWrite("choice.timeRemainingRatio", NovelValue.Number(1)) && !source.TryWrite("choice.remainingSeconds", NovelValue.Integer(20)), "Game UI changed the story's countdown clock.");
            session.Tick(2); session.Restore(saved); UiTimer(.675, 7);
            session.Tick(6.76);
            Check(session.interactiveStory?.status == StoryStatus.Dialogue && session.interactiveStory.nodeId == "sea"
                && view.BuildFrame().items.All(static item => !item.id.StartsWith("choice-timer-", StringComparison.Ordinal)), family + " did not retire the timer with its exact default branch.");
            Check(session.Rollback() && session.interactiveStory?.status == StoryStatus.Choice, family + " lost the timed choice checkpoint.");
            UiTimer(.675, 7);

            void UiTimer(
                double ratio,
                long seconds
            ) {
                var frame = view.BuildFrame(); var progress = frame.items.Single(static item => item.id == "choice-timer-progress");
                Check(progress.kind == UiElementKind.Progress && Math.Abs(progress.progress - ratio) < .000001
                    && frame.items.Single(static item => item.id == "choice-timer-seconds").text == seconds.ToString()
                    && source.TryRead("choice.timed", out var timed) && timed.boolean, family + " countdown UI diverged from the active interpreter clock: ratio=" + progress.progress
                    + "; seconds=" + frame.items.Single(static item => item.id == "choice-timer-seconds").text + ".");
            }
        }
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
