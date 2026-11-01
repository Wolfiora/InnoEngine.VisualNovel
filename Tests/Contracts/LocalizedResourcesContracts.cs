using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
using Inno.Core.Serialization;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Editor.Assets;
using Inno.Rendering;
using Inno.Rendering.Assets;
using Inno.Text;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Player.Localization;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class LocalizedResourcesContracts
{
    internal static void NativeResourceReferences(
        string project,
        string source,
        AssetPipeline assets,
        TextureAsset original,
        TextureAsset translated
    ) {
        File.Copy(Path.Combine(project, "Assets", "BuiltIn", "Fonts", "NotoSansCJKsc-Regular.otf"), Path.Combine(source, "locale-font.otf"));
        Check(assets.Import(AssetPath.Project("locale-font.otf")), "Native language font failed to import.");
        var font = Inno.Assets.Assets.Load<FontAsset>(AssetPath.Project("locale-font.otf"));
        var definition = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English", font = font, fallbackFonts = [font], images = [new() { source = original, image = translated }] }] };
        Check(EditorAssets.Save(AssetPath.Project("Localized.ivnlocale"), definition), "Native language presentation failed to save/import.");
        var imported = Inno.Assets.Assets.Load<LocalizationTableAsset>(AssetPath.Project("Localized.ivnlocale"));
        var language = imported.languages.Single(static item => item.id == "en");
        Check(language.font?.identity.persistentId == font.identity.persistentId && language.fallbackFonts.Single().assetPath == font.assetPath
            && language.images.Single().source?.identity.persistentId == original.identity.persistentId && language.images.Single().image?.assetPath == translated.assetPath,
            "Native localization decoding lost resource identities or source ownership.");
        var dependencies = assets.CaptureProperties(imported).dependencies.Select(static item => item.persistentId).ToHashSet();
        Check(dependencies.Contains(font.identity.persistentId) && dependencies.Contains(original.identity.persistentId) && dependencies.Contains(translated.identity.persistentId), "Language resources were absent from the native build dependency closure.");
    }
    internal static void FallbackGraphemesAndRuby()
    {
        var latin = new FontAsset(); var cjk = new FontAsset(); var emoji = new FontAsset();
        var calls = new List<string>();
        TextLayout Shape(FontAsset font, string text, TextStyle style)
        {
            calls.Add(text);
            if (ReferenceEquals(font, latin) && text == "fi") return new([new(99, 0, 14, 0, 0, 0)], new(16, -4, 20, 0, 0), 14, 20);
            int bytes = 0; var glyphs = new List<TextGlyph>();
            var elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                string value = elements.GetTextElement();
                bool covered = ReferenceEquals(font, latin) ? value == "A\u0301" || value.All(static letter => letter < 128)
                    : ReferenceEquals(font, cjk) ? value is "中" or "ル" or "ビ" : value == "👩‍💻";
                glyphs.Add(new(covered ? 1u : 0u, (uint)bytes, 10, 0, 0, 0)); bytes += Encoding.UTF8.GetByteCount(value);
            }
            return new(glyphs, new(16, -4, 20, 0, 0), glyphs.Count * 10, 20);
        }
        const string sample = "A\u0301中👩‍💻fi";
        var shape = new FontFallbackResolver(Shape).Shape(latin, sample, new(20), [cjk, emoji]);
        Check(shape.fonts.Count == shape.layout.glyphs.Count && ReferenceEquals(shape.fonts[1], cjk) && ReferenceEquals(shape.fonts[2], emoji)
            && shape.layout.glyphs.Any(static glyph => glyph.glyphId == 99), "Fallback lost exact glyph fonts or reshaped a contiguous primary-font ligature as isolated characters.");
        var unresolved = new FontFallbackResolver(Shape).Shape(latin, "X𝄞", new(20), [cjk, emoji]);
        Check(shape.missing.Count == 0 && unresolved.missing.Count == 1 && unresolved.missing[0].text == "𝄞" && unresolved.missing[0].start == 1, "Missing font diagnostics split or mislocated a supplementary grapheme.");
        Check(!calls.Contains("👩", StringComparer.Ordinal) && !calls.Contains("💻", StringComparer.Ordinal), "Font selection cut a joined emoji into individual scalars.");
        var resources = new UiRichTextResources([], [], [cjk, emoji]);
        var document = NovelTextMarkup.Parse("[ruby=ルビ]A中[/ruby]👩‍💻𝄞");
        var builder = new NovelTextLayoutBuilder(Shape);
        var full = builder.Build(new(document), latin, 20, 500, resources: resources);
        var hidden = builder.Build(new(document, 0), latin, 20, 500, resources: resources);
        Check(full.glyphs.Any(item => ReferenceEquals(item.font, cjk) && item.size == 10) && full.glyphs.Any(item => ReferenceEquals(item.font, emoji))
            && full.missingGlyphs.Count == 1 && hidden.missingGlyphs.Count == 1 && full.width == hidden.width && full.height == hidden.height,
            "Production text layout lost mixed-font ruby, full-text missing diagnostics or reveal-stable bounds.");
    }

    internal static void ActualLanguageResources(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var original = new TextureAsset(200, 100, TextureColorSpace.Srgb, "source"); var translated = new TextureAsset(100, 200, TextureColorSpace.Srgb, "translated"); var font = new FontAsset();
        var table = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English", font = font,
            images = [new() { source = original, image = translated }] }] };
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var screen = new ScreenAsset { width = 800, height = 600, textIcons = [new() { key = "mark", image = original }],
            root = new() { id = "root", kind = UiElementKind.Vertical, layout = new() { width = 800, height = 600 }, children =
                [new() { id = "picture", kind = UiElementKind.Image, image = original, layout = new() { width = 300, height = 100 } },
                    new() { id = "line", kind = UiElementKind.Text, text = "[icon=mark]", richText = true, layout = new() { width = 300, height = 70 } }] } };
        var game = new NovelProjectAsset { gameId = "locale-resources", chapters = [chapter], startChapter = chapter.chapterId, localization = table,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = role == "reading" ? screen : ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
        using var session = new NovelSession(game); session.StartNew();
        using var view = new NovelGameView(game, session, static (_, text) => (text.Length * 10, 24));
        Check(view.BuildFrame().items.Single(static item => item.id == "picture").image == original, "An unconfigured language replaced original images.");
        view.HandleInput(new() { x = 500, y = 200, pointerInside = true, pressed = true, held = true });
        session.SetLanguage("en"); var frame = view.BuildFrame();
        long sequence = session.story!.interactionSequence; int revealed = session.story.visibleGraphemes;
        view.HandleInput(new() { x = 500, y = 200, pointerInside = true, released = true });
        var picture = frame.items.Single(static item => item.id == "picture"); var line = frame.items.Single(static item => item.id == "line");
        Check(picture.image == translated && line.font == font && line.textResources is { } richResources && richResources.Icon("mark") == translated && richResources.fallbackFonts.Single() == font,
            "A real language switch failed to resolve static images, default fonts, inline icons or named fallback resources.");
        Check(session.story.interactionSequence == sequence && session.story.visibleGraphemes == revealed, "Retiring language UI leaked the held pointer into narrative advancement.");
        var snapshot = native.Deserialize<UiFrameSnapshot>(native.Serialize(UiFrameSnapshot.Capture(frame)));
        var restored = snapshot.Restore(_ => font, _ => translated);
        Check(restored.items.Single(static item => item.id == "line").textResources!.fallbackFonts.Single() == font, "Native paint capture omitted its resolved fallback font chain.");
        table.languages[1].images[0].image = original;
        Check(view.BuildFrame().items.Single(static item => item.id == "picture").image == translated, "The language resource policy retained mutable authoring mappings.");
        var stage = new StageFrame([new("background", Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY, original, 0xFFFFFFFF)]);
        var localized = session.localization.resources.Stage(stage);
        Check(localized.items.Single().texture == translated && stage.items.Single().texture == original && localized.items.Single().topRight == stage.items.Single().topRight,
            "Localized stage mapping mutated source state or changed authored composition geometry.");
        LocalizationEditService.SetLanguage(table, "en", "Renamed English");
        Check(table.languages.Single(static item => item.id == "en").font == font && table.languages.Single(static item => item.id == "en").images.Length == 1, "Renaming a language discarded its presentation references.");
        var invalid = table.languages.Single(static item => item.id == "en").Copy(); invalid.images = [new() { source = original, image = translated }, new() { source = original, image = original }];
        Reject(() => LocalizationEditService.SetPresentation(table, invalid)); Check(table.languages.Single(static item => item.id == "en").images.Length == 1, "Rejected language mappings partially committed.");
    }
    private static void Reject(Action operation)
    { try { operation(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; } throw new InvalidOperationException("Invalid language resources were accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
