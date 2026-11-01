using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Text;
using Inno.VisualNovel.Authoring.Diagnostics;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.ThemeEditing;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Text;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ThemeEditingContracts
{
    internal static void TokensAndOwnership()
    {
        ThemeAsset theme = ShellThemeTemplates.Create("Clear", new FontAsset());
        var before = new UiThemeResolver(theme).GetStyle("button");
        ThemeEditService.RenameToken(theme, "control.hover", "control.hover.renamed");
        Check(new UiThemeResolver(theme).GetStyle("button").hover == before.hover, "Token rename lost bound state values.");
        Reject(() => ThemeEditService.RemoveToken(theme, "control.hover.renamed"));
        Reject(() => ThemeEditService.RenameToken(theme, "control.hover.renamed", "font.body"));
        string color = ThemeEditService.AddToken(theme, true), metric = ThemeEditService.AddToken(theme, false), style = ThemeEditService.AddStyle(theme);
        var added = theme.styles.Single(item => item.key == style); added.fields = UiStyleProperty.FontSize;
        Reject(() => ThemeEditService.Bind(theme, added, UiStyleProperty.FontSize, color));
        ThemeEditService.Bind(theme, added, UiStyleProperty.FontSize, metric);
        theme.metrics.Single(item => item.key == metric).value = 42;
        Check(new UiThemeResolver(theme).GetStyle(style).fontSize == 42, "Typed visual binding did not use its metric token.");
        ThemeEditService.Bind(theme, added, UiStyleProperty.FontSize, ""); ThemeEditService.RemoveToken(theme, metric);
        ThemeEditService.RemoveToken(theme, color);
        _ = new UiThemeResolver(theme);
        var frozen = ThemeAssetCopy.Copy(theme); frozen.styles[0].values.foreground = 0;
        Check(theme.styles[0].values.foreground != 0, "Theme editing copy exposed shared mutable literal values.");
    }
    internal static void CompleteFamilies(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var themes = ShellThemeTemplates.names.Select(name => ShellThemeTemplates.Create(name, new FontAsset())).ToArray();
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, screens = themes[0].screens, themes = themes, defaultTheme = themes[0].resourceId };
        ThemeProjectValidator.Validate(game);
        var texts = LocalizationExtractionService.Extract(game);
        Check(texts.Any(static row => row.source == "界面主题"), "Theme-only page text was omitted from native localization extraction.");
        var titleLayouts = new HashSet<string>(StringComparer.Ordinal);
        foreach (ThemeAsset theme in themes)
        {
            Check(theme.screens.Length == 18 && theme.screens.Select(static binding => binding.role).Distinct().Count() == 18, "A theme omitted or duplicated a shell role.");
            var title = theme.screens.Single(static binding => binding.role == "title").screen!;
            var composition = ScreenEditService.Find(title, "title-composition");
            titleLayouts.Add(composition.layout.width + "/" + ScreenEditService.Find(title, "menu").kind);
            foreach (var binding in theme.screens)
            {
                var page = new UiDocumentInstance(binding.screen!, static (_, _) => (0, 0), Measure, theme); page.SetFontScale(2);
                UiFrame frame = page.BuildFrame(new Data());
                Check(frame.items.All(static item => float.IsFinite(item.bounds.width) && float.IsFinite(item.bounds.height)), "Large-font themed layout produced invalid geometry.");
                if (binding.role == "title")
                {
                    var menu = frame.items.Single(static item => item.id == "new");
                    Check(menu.bounds.height >= 108 && menu.bounds.width > 200, "The themed primary action clipped two-times text instead of growing with content.");
                }
                if (binding.role == "reading")
                {
                    var option = frame.items.Single(static item => item.id.EndsWith("disabled/option", StringComparison.Ordinal));
                    var reason = frame.items.Single(static item => item.id.EndsWith("disabled/option-reason", StringComparison.Ordinal));
                    Check(option.bounds.height > 200 && reason.text.Length > 0 && reason.bounds.y >= option.bounds.y && reason.bounds.y + reason.bounds.height <= option.bounds.y + option.bounds.height + 1,
                        "Long disabled choice text or its explanation escaped the actual content-sized button.");
                    UiInputResult input = page.HandleInput(new() { pointerInside = true, x = option.bounds.x + 30, y = option.bounds.y + 30, released = true, confirm = true }, new Data());
                    Check(input.actions.All(static action => action.action != "choice"), "A disabled choice accepted mixed pointer and keyboard activation.");
                }
            }
        }
        Check(titleLayouts.Count == 3, "Complete theme families share only one unchanged title composition.");
    }
    private static (float width, float height, float visibleHeight) Measure(
        UiElementDefinition element,
        UiRichText content,
        UiRichTextResources resources,
        float width,
        float scale
    ) {
        float glyph = element.style.fontSize * .7f;
        float height = (float)Math.Max(1, Math.Ceiling(content.document.text.Length * glyph / Math.Max(1, width))) * element.style.fontSize * element.style.lineSpacing;
        return (Math.Min(width, content.document.text.Length * glyph), height, height);
    }
    private sealed class Data : UiDataSource
    {
        public override bool TryWrite(string path, NovelValue value) => false;
        public override bool TryRead(string path, out NovelValue value)
        {
            value = path switch { "game.title" => NovelValue.String("一个需要检查长标题的故事"), "choice.visible" => NovelValue.Boolean(true), _ => NovelValue.Boolean(false) };
            return path is "game.title" or "choice.visible";
        }
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => path == "choices" ? [new("disabled", new Dictionary<string, NovelValue>
        { ["id"] = NovelValue.String("disabled"), ["text"] = NovelValue.String(new string('长', 130)), ["enabled"] = NovelValue.Boolean(false),
            ["reason"] = NovelValue.String("需要先解锁对应路线才能选择。"), ["hasReason"] = NovelValue.Boolean(true), ["selected"] = NovelValue.Boolean(false) })] : [];
    }
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An unsafe theme edit was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
