using System;
using System.Linq;
using System.Text;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class RichTextContracts
{
    internal static void ReadingAndRestoration(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var node = draft.graph.document.FindNode(new("hello"))!;
        DialogueNode line = codec.Read<DialogueNode>(node);
        line.text = "[b]A[pause=0.5]\u0301[/b][speed=2]👩🏽‍💻[/speed]![pause=1]"; line.charactersPerSecond = 1; line.punctuationPause = 0.25;
        codec.Write(node, line); var result = compiler.Compile(draft.ToAsset()); Check(result.success, "Valid rich dialogue did not compile.");
        using var machine = new StoryMachine(result.program!); machine.Start(); machine.Tick(1.4);
        Check(machine.visibleText == "", "A pause inside a combining sequence was not normalized before that grapheme.");
        machine.Tick(0.1); Check(machine.visibleText == "A\u0301", "Styled reveal split a grapheme or missed its absolute boundary.");
        var state = native.Deserialize<StoryExecutionState>(native.Serialize(machine.Capture())); machine.Tick(0.5);
        Check(machine.visibleText == "A\u0301👩🏽‍💻", "Local speed split a composed emoji.");
        machine.Restore(state); machine.Tick(0.5); Check(machine.visibleText == "A\u0301👩🏽‍💻", "Native restore lost local speed or pause progress.");
        machine.Tick(1); Check(machine.visibleText.EndsWith('!') && !machine.isTextComplete, "Trailing author and punctuation pauses were ignored.");
        machine.Advance(); Check(machine.status == StoryStatus.Dialogue && machine.isTextComplete, "First action escaped the line while a trailing pause remained.");
        machine.Advance(); Check(machine.status == StoryStatus.Choice, "Second action did not advance the completed line.");

        line.text = TextFormattingEditService.ApplyStyle("[color=#80FFAA]{player}[/color]", 0, 8, new(bold: true));
        codec.Write(node, line); var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        using var session = new NovelSession(new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId }); session.StartNew();
        session.story!.SetVariable("player", Inno.VisualNovel.Model.Values.NovelValue.String("后来改变"));
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); session.Restore(saved);
        Check(session.story!.dialogueText == "旅人", "Same-language restoration re-interpolated an already resolved saved line.");
        using var injected = new StoryMachine(chapter.program); injected.SetVariable("player", Inno.VisualNovel.Model.Values.NovelValue.String("[pause=60][b]名字")); injected.Start();
        Check(injected.dialogueText == "[pause=60][b]名字" && injected.dialogueDocument.beats.Count == 0, "Player data injected author markup.");
        line.text = "[b]未闭合"; codec.Write(node, line);
        Check(compiler.Compile(draft.ToAsset()).diagnostics.Any(static item => item.code == "VN_TEXT_MARKUP"), "Malformed markup did not produce a source-addressed compile diagnostic.");
    }

    internal static void FormattingAndStableLayout()
    {
        string source = "[b]甲[/b][pause=0.2]A\u0301👩🏽‍💻{player}";
        string styled = TextFormattingEditService.ApplyStyle(source, 1, 2, new(italic: true, color: 0xAAEEFFFF));
        Check(NovelTextMarkup.Parse(styled).text == "甲A\u0301👩🏽‍💻{player}" && NovelTextMarkup.Parse(styled).beats.Sum(static beat => beat.pause) == 0.2, "Visual formatting changed text or pacing.");
        string ruby = TextFormattingEditService.ApplyRuby(styled, 1, 1, "えー");
        Check(NovelTextMarkup.Parse(ruby).runs.Any(static run => run.ruby == "えー"), "Visual ruby editing did not retain an annotation.");
        string faster = TextFormattingEditService.ApplySpeed(ruby, 0, 1, 2);
        Check(NovelTextMarkup.Parse(faster).text == NovelTextMarkup.Parse(source).text, "Pacing edit changed the canonical plain text.");
        bool rejected = false; try { TextFormattingEditService.ApplyStyle(source, 4, 1, new()); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Visual formatting split a variable placeholder.");
        var builder = new NovelTextLayoutBuilder(Shape); var font = new FontAsset();
        NovelTextDocument document = NovelTextMarkup.Parse("中A\u0301👩🏽‍💻[size=30]大[/size][ruby=かん]漢[/ruby]");
        var hidden = builder.Build(new(document, 0), font, 20, 60); var partial = builder.Build(new(document, 1), font, 20, 60); var full = builder.Build(new(document), font, 20, 60);
        Check(hidden.height == full.height && hidden.width == full.width && hidden.glyphs.Count == 0, "Unrevealed text changed wrapping or measured bounds.");
        Check(partial.glyphs.Count == 1 && partial.glyphs[0] == full.glyphs[0], "UTF-8 shaping clusters were compared as UTF-16 or reveal moved existing glyphs.");
        Check(full.height > 40 && full.glyphs.Any(static item => item.size == 10) && full.glyphs.Any(static item => item.size == 30), "Wrapped large text or ruby was not placed by the production layout.");
        var outline = Inno.VisualNovel.Rendering.Text.GlyphOutline.Build(new GlyphBitmap(1, 1, 0, 1, 1, [255]), 1);
        Check(outline.width == 3 && outline.bearingX == -1 && outline.bearingY == 2 && outline.pixels.Span[0] == 0 && outline.pixels.Span[1] == 255, "Circular glyph outline lost coverage or baseline bearings.");
    }

    private static TextLayout Shape(FontAsset font, string text, TextStyle style)
    {
        var glyphs = new System.Collections.Generic.List<TextGlyph>(); int offset = 0;
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        { string cluster = elements.GetTextElement(); glyphs.Add(new((uint)cluster[0], (uint)offset, style.fontSize, 0, 0, 0)); offset += Encoding.UTF8.GetByteCount(cluster); }
        return new(glyphs, new(style.fontSize * 0.8f, -style.fontSize * 0.2f, style.fontSize, 0, 0), glyphs.Count * style.fontSize, style.fontSize);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
