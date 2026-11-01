using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ReadIdentityContracts
{
    internal static void ParametersSkipAndNativeRestore(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { gameId = "stable-reading", chapters = [chapter], startChapter = chapter.chapterId };
        using var session = new NovelSession(game);
        session.StartAt(chapter.chapterId, "start", [new() { id = "player", value = NovelValue.String("Alpha") }]);
        session.Advance(); session.Tick(0); Check(session.IsCurrentRead(), "A completed line was not recorded as read.");
        var progress = native.Deserialize<Player.Persistence.GlobalProgressState>(native.Serialize(session.CaptureProgress()));
        session.StartAt(chapter.chapterId, "start", [new() { id = "player", value = NovelValue.String("Beta") }]);
        Check(session.story!.dialogueText.Contains("Beta", StringComparison.Ordinal) && session.IsCurrentRead(), "Changing a frozen player substitution incorrectly reset the same authored line's read identity.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); session.story!.SetVariable("player", NovelValue.String("Later"));
        session.Restore(saved); Check(session.IsCurrentRead() && session.story!.dialogueText.Contains("Beta", StringComparison.Ordinal), "Native restoration lost reading identity or reread live substitutions.");
        session.preferences.skipReadOnly = true; session.SetSkippingHeld(false, true); session.SetSkippingHeld(true, true); session.Tick(0);
        Check(session.story!.status == StoryStatus.Choice && !session.skipping, "Read-only skipping stopped at a substituted value or crossed the next decision.");
        Check(session.CaptureProgress().readContent.SequenceEqual(progress.readContent), "An interpolation variant created another global read record.");
    }
    internal static void AuthoredChangesAndLanguageContent(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { gameId = "localized-reading", chapters = [chapter], startChapter = chapter.chapterId };
        var line = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("hello"))!);
        string originalText = line.text;
        var table = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English" }] };
        LocalizationEditService.Synchronize(table, LocalizationExtractionService.Extract(game));
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Dialogue(line.textId), "en", "Hello, {player}!", TranslationStatus.Reviewed); game.localization = table;
        using var session = new NovelSession(game); session.StartNew(); session.Advance(); session.Tick(0);
        var progress = session.CaptureProgress(); Check(session.IsCurrentRead(), "The original-language source was not recorded.");
        session.SetLanguage("en"); Check(!session.IsCurrentRead() && session.story!.dialogueSourceText == "Hello, {player}!", "Different translated content inherited the original language's read record or exposed substituted text as its source.");
        session.Tick(0); Check(session.IsCurrentRead(), "The translated completed line did not create its own content identity.");
        session.SetLanguage("zh-CN"); Check(session.IsCurrentRead(), "Returning to a previously read authored language lost its record.");
        line.text += " 新增的作者内容。"; codec.Write(draft.graph.document.FindNode(new("hello"))!, line);
        var changed = draft.ToAsset(); changed.program = compiler.Compile(changed).program!;
        using var changedSession = new NovelSession(new() { gameId = game.gameId, chapters = [changed], startChapter = changed.chapterId });
        changedSession.RestoreProgress(progress); changedSession.StartNew(); Check(!changedSession.IsCurrentRead(), "An edited source template retained its old read identity.");
        line.text = originalText; line.textId = Guid.NewGuid().ToString("N");
        codec.Write(draft.graph.document.FindNode(new("hello"))!, line); var replaced = draft.ToAsset(); replaced.program = compiler.Compile(replaced).program!;
        using var replacement = new NovelSession(new() { gameId = game.gameId, chapters = [replaced], startChapter = replaced.chapterId });
        replacement.RestoreProgress(progress); replacement.StartNew(); Check(!replacement.IsCurrentRead(), "A distinct dialogue identity inherited another line's progress merely because its visible text matched.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
