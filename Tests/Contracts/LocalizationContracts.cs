using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Player.Localization;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class LocalizationContracts
{
    internal static void RuntimeAndRestore(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var font = new FontAsset();
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits", "save", "load", "confirm" }.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, font, "Clear") }).ToArray() };
        var table = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English" }, new() { id = "ja", name = "日本語" }] };
        LocalizationEditService.Synchronize(table, LocalizationExtractionService.Extract(game));
        DialogueNode line = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("hello"))!);
        string textId = StoryTextKeys.Dialogue(line.textId);
        LocalizationEditService.SetTranslation(table, textId, "en", "Hello, {player}!", TranslationStatus.Reviewed);
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Speaker(line.textId), "en", "Cheng", TranslationStatus.Translated);
        LocalizationEditService.SetTranslation(table, textId, "ja", "こんにちは、{player}。", TranslationStatus.Reviewed);
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Ui("title", "new"), "en", "New game", TranslationStatus.Reviewed);
        game.localization = table;
        using var session = new NovelSession(game); session.StartNew(); session.Tick(0.1);
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        session.SetLanguage("en");
        Check(session.story!.dialogueText == "Hello, 旅人!" && session.story.speaker == "Cheng" && session.history.Count == 1, "Language change missed interpolation or duplicated the line boundary.");
        session.Restore(saved);
        Check(session.story!.dialogueText == "Hello, 旅人!" && session.localization.language == "en" && session.history.Count == 1, "Loading an older-language snapshot reset preferences or duplicated history.");
        session.SetLanguage("ja"); Check(session.story.dialogueText == "こんにちは、旅人。", "Japanese text did not resolve through the same interpreter.");
        string before = session.story.dialogueText;
        try { session.SetLanguage("missing"); } catch (ArgumentException) { }
        Check(session.story.dialogueText == before && session.localization.language == "ja", "Rejected language changed the running session.");
        table.entries.Single(row => row.id == textId).translations[0].text = "MUTATED";
        session.SetLanguage("en"); Check(session.story.dialogueText == "Hello, 旅人!", "Runtime retained editable translation aliases.");
        using var view = new NovelGameView(game, session, static (_, text) => (text.Length * 10, 30)); session.OpenPage("title");
        Check(view.BuildFrame().items.Any(item => item.text == "New game"), "Actual game UI literals did not use the selected language.");
        session.OpenPage("settings");
        var selector = view.BuildFrame().items.Single(item => item.id == "language-selector");
        float x = selector.bounds.x + selector.bounds.width / 2, y = selector.bounds.y + selector.bounds.height / 2;
        view.HandleInput(new() { pointerInside = true, x = x, y = y, pressed = true });
        view.HandleInput(new() { pointerInside = true, x = x, y = y, released = true });
        view.HandleInput(new() { next = true }); view.HandleInput(new() { confirm = true });
        Check(session.localization.language == "ja" && session.page == "settings" && session.history.Count == 1, "Language dropdown changed shell navigation or replayed the active line.");
        view.HandleInput(new() { confirm = true }); view.HandleInput(new() { cancel = true });
        Check(session.page == "settings", "Dropdown cancellation also closed the settings page.");
        var stale = new LocalizationTableAsset { languages = table.languages, entries = [new() { id = "x", source = "new {player}", translations = [new() { language = "en", text = "old {name}", status = TranslationStatus.Reviewed, sourceFingerprint = NovelLocalizationService.Fingerprint("old {name}") }] }] };
        Check(new NovelLocalizationService(stale, "en").Resolve("x", "new {player}") == "new {player}", "Stale translations did not fall back safely.");
    }
    internal static void Exchange()
    {
        var table = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English" }], entries = [new() { id = "line", source = "名字 {name},\n第二行\"" }] };
        LocalizationEditService.SetTranslation(table, "line", "en", "Name {name},\nsecond \"line\"", TranslationStatus.Reviewed);
        foreach (char separator in new[] { ',', '\t' })
        {
            string exported = LocalizationDelimitedText.Export(table, separator);
            var destination = new LocalizationTableAsset { languages = table.languages, entries = [new() { id = "line", source = table.entries[0].source }] };
            var preview = LocalizationDelimitedText.Preview(destination, exported, separator);
            Check(preview.rows.Count == 1 && preview.rows[0].applicable, "Quoted multiline Unicode exchange did not preview correctly.");
            LocalizationDelimitedText.Apply(destination, preview);
            Check(destination.entries[0].translations[0].text == table.entries[0].translations[0].text, "CSV/TSV changed translated text.");
            bool rejected = false; try { LocalizationDelimitedText.Apply(destination, preview); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "A stale import preview overwrote newer translation work.");
        }
        var unknown = LocalizationDelimitedText.Export(table).Replace("\"en\"", "\"xx\"", StringComparison.Ordinal);
        Check(!LocalizationDelimitedText.Preview(table, unknown).rows[0].applicable, "Unknown exchange language was accepted.");
        bool mismatch = false; try { LocalizationEditService.SetTranslation(table, "line", "en", "Missing variable", TranslationStatus.Reviewed); } catch (InvalidOperationException) { mismatch = true; }
        Check(mismatch, "Translation review removed an interpolated game variable.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
