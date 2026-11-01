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
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ReadingPresentationContracts
{
    internal static void NvlSaveLanguageAndRollback(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        DialogueNode first = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("hello"))!); first.presentation = DialoguePresentation.Nvl; first.text = "[b]第一页，{player}。[/b]";
        codec.Write(draft.graph.document.FindNode(new("hello"))!, first);
        DialogueNode second = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("sea"))!); second.presentation = DialoguePresentation.Nvl; second.text = "海边的{player}。";
        codec.Write(draft.graph.document.FindNode(new("sea"))!, second);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }
            .Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Clear") }).ToArray() };
        var table = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English" }] };
        LocalizationEditService.Synchronize(table, LocalizationExtractionService.Extract(game));
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Dialogue(first.textId), "en", "First page, {player}.", TranslationStatus.Reviewed);
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Dialogue(second.textId), "en", "By the sea, {player}.", TranslationStatus.Reviewed); game.localization = table;
        using var session = new NovelSession(game); session.StartNew(); session.Advance(); session.Advance();
        session.story!.SetVariable("player", NovelValue.String("第二人")); session.Choose("sea");
        Check(session.story.readingPage.text.Contains("第一页，旅人。", StringComparison.Ordinal) && session.story.readingPage.text.Contains("海边的第二人。", StringComparison.Ordinal), "NVL did not append frozen per-line substitutions.");
        session.Tick(0.05); var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture())); int visible = session.story.visiblePageGraphemes;
        session.story.SetVariable("player", NovelValue.String("后来改变")); session.SetLanguage("en");
        Check(session.story!.readingPage.text.Contains("First page, 旅人.", StringComparison.Ordinal) && session.story.readingPage.text.Contains("By the sea, 第二人.", StringComparison.Ordinal), "Language reflow re-evaluated old page variables.");
        session.SetLanguage("zh-CN"); session.Restore(saved);
        Check(session.story!.visiblePageGraphemes == visible && session.story.readingPage.text.Contains("海边的第二人。", StringComparison.Ordinal), "Native restore lost NVL page or its reveal boundary.");
        Check(session.Rollback() == false, "Load retained a stale pre-load rollback journal.");
        session.StartNew(); session.Advance(); session.Advance(); session.Choose("sea");
        Check(session.Rollback() && session.Choose("forest") && session.story!.readingPage.text.Length == 0, "Rollback or switching from NVL to ADV retained the abandoned page.");

        second.clearPage = true; codec.Write(draft.graph.document.FindNode(new("sea"))!, second); chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        using var fresh = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId }); fresh.StartNew(); fresh.Advance(); fresh.Advance(); fresh.Choose("sea");
        Check(!fresh.story!.readingPage.text.Contains("第一页", StringComparison.Ordinal), "An explicit page break retained the previous page.");
    }

    internal static void LayoutsAndGeometry(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var node = draft.graph.document.FindNode(new("hello"))!;
        var line = codec.Read<DialogueNode>(node); line.presentation = DialoguePresentation.Bubble; line.anchorX = 0.1; line.anchorY = 0.2; line.relativeWidth = 0.4; codec.Write(node, line);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { width = 1280, height = 720, chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Paper") }).ToArray() };
        using var session = new NovelSession(game); session.StartNew();
        using var view = new NovelGameView(game, session, static (_, text) => (Math.Min(600, text.Length * 10), 40));
        var frame = view.BuildFrame(); var bubble = frame.items.Single(static item => item.id == "bubble-panel");
        Check(Math.Abs(bubble.bounds.x - 192) < 0.01 && Math.Abs(bubble.bounds.y - 216) < 0.01 && Math.Abs(bubble.bounds.width - 768) < 0.01
            && !frame.items.Any(static item => item.id == "dialogue" || item.id == "nvl-page"), "Actual editable shell ignored selected reading mode or numeric placement bindings.");
        var fit = Inno.VisualNovel.UI.Layout.UiCanvasFit.Create(frame.width, frame.height, new(0, 0, game.width, game.height));
        Check(Math.Abs(fit.Project(bubble.bounds).x - 128) < 0.01 && Math.Abs(fit.Project(bubble.bounds).width - 512) < 0.01,
            "Positioned reading geometry was scaled twice when the game and page use different design resolutions.");
        foreach (DialoguePresentation mode in new[] { DialoguePresentation.Overlay, DialoguePresentation.Letter })
        {
            line.presentation = mode; codec.Write(node, line); var source = draft.ToAsset(); source.program = compiler.Compile(source).program!;
            using var owner = new NovelSession(new() { chapters = [source], startChapter = source.chapterId, screens = game.screens }); owner.StartNew();
            using var ui = new NovelGameView(new() { screens = game.screens }, owner, static (_, _) => (200, 40));
            Check(ui.BuildFrame().items.Any(item => item.id == (mode == DialoguePresentation.Letter ? "letter-page" : "overlay-panel")), "A declared reading layout has no actual runtime page.");
        }
        var root = new UiElementDefinition { id = "root", layout = new() { width = 400, height = 100 }, children = [new()
        { id = "rows", kind = UiElementKind.Horizontal, repeatBinding = "rows", layout = new() { widthMode = UiSizeMode.Fill, height = 50 }, children = [new()
        { id = "row", kind = UiElementKind.Text, geometryBinding = new() { width = "item.width" }, layout = new() { width = 50, height = 40 } }] }] };
        root = native.Deserialize<UiElementDefinition>(native.Serialize(root));
        var document = new UiDocumentInstance(new() { width = 400, height = 100, root = root }, static (_, _) => (0, 0));
        var rows = document.BuildFrame(new Rows()).items.Where(static item => item.id.EndsWith("/row", StringComparison.Ordinal)).ToArray();
        Check(rows.Length == 2 && rows[0].bounds.width == 100 && rows[1].bounds.width == 200, "Repeated rows shared mutable resolved geometry.");
    }
    private sealed class Rows : UiDataSource
    {
        public override bool TryRead(string path, out NovelValue value) { value = NovelValue.Boolean(false); return false; }
        public override bool TryWrite(string path, NovelValue value) => false;
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => [new("a", new Dictionary<string, NovelValue> { ["width"] = NovelValue.Number(100) }), new("b", new Dictionary<string, NovelValue> { ["width"] = NovelValue.Number(200) })];
    }
    internal static void RevealTailFollowing()
    {
        var source = new Reveal();
        var root = new UiElementDefinition { id = "root", kind = UiElementKind.Panel, layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Fill },
            children = [new() { id = "scroll", kind = UiElementKind.Scroll, followEndBinding = "follow", layout = new() { width = 100, height = 50, clip = true, gap = 0 },
            children = [new() { id = "line", kind = UiElementKind.Text, binding = "text", layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content }, style = new() { textPadding = 0 } }] }] };
        var page = new UiDocumentInstance(new() { width = 100, height = 50, root = root }, static (_, _) => (100, 200),
            static (_, content, _, _, _) => (100, 200, content.visibleGraphemes * 20));
        var frame = page.BuildFrame(source); Check(frame.items.Single(static item => item.id == "line").bounds.y == 0, "Empty reveal jumped to the end of unrevealed text.");
        source.visible = 5; frame = page.BuildFrame(source); Check(frame.items.Single(static item => item.id == "line").bounds.y == -50, "Growing reveal did not follow the current visible baseline.");
        page.HandleInput(new() { pointerInside = true, x = 10, y = 10, scroll = 50 }, source);
        source.visible = 7; frame = page.BuildFrame(source); Check(frame.items.Single(static item => item.id == "line").bounds.y == 0, "Following overrode a player's manual review position.");
        page.HandleInput(new() { pointerInside = true, x = 10, y = 10, scroll = -1000 }, source);
        source.visible = 8; frame = page.BuildFrame(source); Check(frame.items.Single(static item => item.id == "line").bounds.y == -110, "Returning to the tail did not resume reveal following.");
    }
    private sealed class Reveal : UiDataSource
    {
        internal int visible;
        private readonly Inno.VisualNovel.Model.Text.NovelTextDocument m_text = Inno.VisualNovel.Model.Text.NovelTextDocument.Literal("0123456789");
        public override Inno.VisualNovel.UI.Text.UiRichText? ReadRichText(string path, UiDataRow? row) => path == "text" ? new(m_text, visible) : null;
        public override bool TryRead(string path, out NovelValue value) { value = path == "follow" ? NovelValue.Boolean(true) : NovelValue.String(m_text.Prefix(visible)); return true; }
        public override bool TryWrite(string path, NovelValue value) => false;
    }
    internal static void LongReadingAndKeyboardPages(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var node = draft.graph.document.FindNode(new("hello"))!;
        DialogueNode line = codec.Read<DialogueNode>(node); line.text = string.Join('\n', Enumerable.Range(1, 24).Select(static index => "第" + index + "行 👩🏽‍💻")); codec.Write(node, line);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }
            .Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Clear") }).ToArray() };
        using var session = new NovelSession(game); session.preferences.fontScale = 2; session.StartNew();
        using var view = new NovelGameView(game, session, static (_, _) => (100, 40), measureRichText: static (definition, content, _, width, _) =>
        {
            float height = content.document.text.Split('\n').Length * definition.style.fontSize * definition.style.lineSpacing;
            float visible = content.visibleGraphemes == 0 ? 0 : content.document.Prefix(content.visibleGraphemes).Split('\n').Length * definition.style.fontSize * definition.style.lineSpacing;
            return (width, height, visible);
        });
        session.Advance(); var frame = view.BuildFrame(); var text = frame.items.Single(static item => item.id == "line"); var scroll = frame.items.Single(static item => item.id == "dialogue-scroll");
        Check(text.bounds.height > scroll.bounds.height && text.bounds.y < scroll.bounds.y && text.clip.height <= scroll.bounds.height,
            "The actual large-font ADV page clipped long text without a scrollable revealed tail.");
        float tail = text.bounds.y; long interaction = session.story!.interactionSequence;
        var input = new UI.Input.UiInputSnapshot { pageUp = true, confirm = true }.InCanvas(UI.Layout.UiCanvasFit.Create(1920, 1080, new(0, 0, 1920, 1080)));
        view.HandleInput(input); frame = view.BuildFrame();
        Check(session.story.status == Narrative.Execution.StoryStatus.Dialogue && session.story.interactionSequence == interaction
            && frame.items.Single(static item => item.id == "line").bounds.y > tail, "A keyboard page-review action advanced dialogue or failed to move the visible reading range.");
        view.HandleInput(new() { pageDown = true }); frame = view.BuildFrame();
        Check(Math.Abs(frame.items.Single(static item => item.id == "line").bounds.y - tail) < 0.01, "Page-down failed to return to the revealed text tail.");
        view.HandleInput(new() { pageUp = true, composing = true });
        Check(Math.Abs(view.BuildFrame().items.Single(static item => item.id == "line").bounds.y - tail) < 0.01, "IME composition triggered a reading page shortcut.");
        var source = new Reveal { visible = 5 };
        var page = new UiDocumentInstance(new() { width = 100, height = 50, root = new() { id = "root", layout = new() { width = 100, height = 50 }, children = [new()
        { id = "scroll", kind = UiElementKind.Scroll, followEndBinding = "follow", layout = new() { width = 100, height = 50, clip = true }, children = [new()
        { id = "line", kind = UiElementKind.Text, binding = "text", layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content }, style = new() { textPadding = 0 } }] }] } },
            static (_, _) => (100, 200), static (_, content, _, _, _) => (100, 200, content.visibleGraphemes * 20));
        page.BuildFrame(source); for (int index = 0; index < 20; index++) page.HandleInput(new() { pageDown = true }, source);
        Check(page.BuildFrame(source).items.Single(static item => item.id == "line").bounds.y >= -50, "Keyboard review scrolled into unrevealed blank future text.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
