using System;
using System.Linq;
using System.Text;
using Inno.Assets;
using Inno.Text;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class GlyphCoverageContracts
{
    internal static void BudgetsContextsAndUnavailableHost(StoryNodeDataCodec codec, StoryCompiler compiler)
    {
        var font = new FontAsset(); var path = AssetPath.Project("Story.ivnstory");
        var sources = new[]
        {
            new GlyphCoverageDocument("line", "chapter/hello", "A{name}中中", false, true, font, 20, UiRichTextResources.empty, path, "hello"),
            new GlyphCoverageDocument("plain", "page/plain", "ASCII", false, false, font, 20, UiRichTextResources.empty, AssetPath.Project("Page.ivnui")),
            new GlyphCoverageDocument("fontless", "page/fontless", "text", false, false, null, 20, UiRichTextResources.empty, AssetPath.Project("Page.ivnui"))
        };
        TextLayout Shape(FontAsset _, string text, TextStyle style)
        {
            var glyphs = text.Select((letter, index) => new TextGlyph(letter < 128 ? 1u : 0u, (uint)Encoding.UTF8.GetByteCount(text[..index]), 10, 0, 0, 0)).ToArray();
            return new(glyphs, new(16, -4, 20, 0, 0), glyphs.Length * 10, 20);
        }
        using (var audit = new GlyphCoverageAudit(sources, Shape))
        {
            audit.Step(1);
            Check(audit.processed == 1 && !audit.completed && audit.dynamicSources == 1 && audit.issues.Single().text == "中" && audit.issues.Single().count == 2
                && audit.issues.Single().nodeId == "hello" && audit.issues.Single().assetPath == path, "Incremental glyph checks lost their budget, static template grammar, grouping or native source owner.");
            audit.Cancel(); audit.Step(100); Check(audit.processed == 1 && audit.cancelled && audit.issues.Count == 1, "Cancelling coverage continued shaping or discarded earlier source-located results.");
        }
        using (var audit = new GlyphCoverageAudit(sources, Shape))
        {
            audit.Step(8); Check(audit.completed && audit.issues.Count == 2 && audit.issues.Single(static issue => issue.failed).textId == "fontless", "Missing fonts were mistaken for a successful no-missing-glyph result.");
        }
        using (var audit = new GlyphCoverageAudit(sources.Take(1), static (_, _, _) => throw new InvalidOperationException("Host text unavailable")))
        { audit.Step(8); Check(audit.completed && audit.issues.Single().failed && audit.issues.Single().message.Contains("unavailable", StringComparison.Ordinal), "An unavailable native host was reported as verified font coverage."); }

        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var reading = new ScreenAsset { font = font, root = new() { id = "root", kind = UiElementKind.Vertical, children =
            [new() { id = "body", kind = UiElementKind.Text, binding = "dialogue.text" }, new() { id = "speaker", kind = UiElementKind.Text, binding = "dialogue.speaker" }] } };
        var game = new NovelProjectAsset { font = font, chapters = [chapter], themes = [new ThemeAsset { resourceId = "test", font = font }], screens = [new() { role = "reading", screen = reading }] };
        var captured = GlyphCoverageSources.Enumerate(game, "zh-CN");
        string before = chapter.program.instructions.Single(static instruction => instruction.nodeId == "hello").dialogue!.text;
        chapter.program.instructions.Single(static instruction => instruction.nodeId == "hello").dialogue!.text = "Changed after audit began";
        var contexts = captured.ToArray();
        string id = StoryTextKeys.Dialogue(chapter.program.instructions.Single(static instruction => instruction.nodeId == "hello").dialogue!.textId);
        var hello = contexts.Where(source => source.textId == id).ToArray();
        Check(hello.Length == 2 && hello.All(source => source.nodeId == "hello" && source.font == font && source.rich && source.text == before)
            && hello.Select(static source => source.location.Split('/')[0]).Distinct().Count() == 2,
            "Coverage source enumeration skipped theme or page contexts, lost graph identity or used a different default font policy from the player.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
