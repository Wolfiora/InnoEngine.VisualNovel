using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Text;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ThemeReflowContracts
{
    internal static void NestedSettingsAndScrollablePause()
    {
        var source = new Source();
        foreach (string name in ShellThemeTemplates.names)
        {
            var theme = ShellThemeTemplates.Create(name, new FontAsset());
            var settings = new UiDocumentInstance(theme.screens.Single(static page => page.role == "settings").screen!, Plain, theme: theme, measureRichText: Wrapped);
            settings.SetFontScale(2); UiFrame frame = settings.BuildFrame(source);
            var rows = frame.items.Where(static item => item.id.EndsWith("/keyboard-row", StringComparison.Ordinal)).ToArray();
            Check(rows.Length == 7, name + " lost editable shortcut rows.");
            foreach (UiDrawItem row in rows)
            {
                string prefix = row.id[..^"keyboard-row".Length];
                foreach (var child in frame.items.Where(item => item.id == prefix + "keyboard-name" || item.id == prefix + "keyboard-key"))
                    Check(child.bounds.y >= row.bounds.y && child.bounds.y + child.bounds.height <= row.bounds.y + row.bounds.height + .01f,
                        name + " clipped a nested 200% label or selector inside a fixed-height structural row.");
            }
            Check(rows[0].bounds.height > 100 && rows.Zip(rows.Skip(1)).All(static pair => pair.Second.bounds.y >= pair.First.bounds.y + pair.First.bounds.height),
                name + " did not expand a long label or let it overlap the following row.");
            string last = frame.items.Single(static item => item.id.EndsWith("quickLoad/keyboard-key", StringComparison.Ordinal)).id;
            Check(settings.Focus(last), name + " could not focus the last offscreen setting.");
            UiDrawItem revealed = settings.BuildFrame(source).items.Single(item => item.id == last);
            Check(revealed.clip.height >= revealed.bounds.height - .01f, name + " could not scroll the final selector fully into view.");

            var pause = new UiDocumentInstance(theme.screens.Single(static page => page.role == "pause").screen!, Plain, theme: theme, measureRichText: Wrapped);
            pause.SetFontScale(2); pause.BuildFrame(source);
            Check(pause.Focus("title"), name + " lost title navigation in its pause menu.");
            UiFrame paused = pause.BuildFrame(source); UiDrawItem menu = paused.items.Single(static item => item.id == "pause-menu"), title = paused.items.Single(static item => item.id == "title");
            Check(menu.kind == UiElementKind.Scroll && title.clip.height >= title.bounds.height - .01f && menu.bounds.y + menu.bounds.height <= 930,
                name + " could not reveal a tall pause menu without colliding with the footer: menu=" + menu.bounds + "; title=" + title.bounds + "; clip=" + title.clip + ".");
            Check(pause.HandleInput(new() { confirm = true }, source).actions.Single().action == "title", name + " changed focus geometry without preserving real button activation.");
        }
    }
    private static (float width, float height) Plain(
        UiElementDefinition element,
        string text
    ) => (text.Length * element.style.fontSize * .55f, element.style.fontSize * element.style.lineSpacing);
    private static (float width, float height, float visibleHeight) Wrapped(
        UiElementDefinition element,
        UiRichText text,
        UiRichTextResources resources,
        float width,
        float scale
    ) {
        var measured = Plain(element, text.document.text);
        float height = Math.Max(1, MathF.Ceiling(measured.width / Math.Max(1, width))) * measured.height;
        return (Math.Min(width, measured.width), height, height);
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Source : UiDataSource
    {
        private readonly KeyboardSettingsDataSource m_keys = new(static () => new PlayerPreferences(), static (_, source) => "Long translated action: " + source + " with an explanation");
        public override bool TryRead(
            string path,
            out NovelValue value
        ) {
            if (m_keys.TryRead(path, out value)) return true;
            value = NovelValue.Boolean(true); return path is "game.normal" or "saves.ready" or "saves.canSave";
        }
        public override bool TryWrite(
            string path,
            NovelValue value
        ) => false;
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => m_keys.ReadRows(path);
        public override string Localize(
            string key,
            string fallback
        ) => key.Contains("pause", StringComparison.Ordinal) || key.Contains("title", StringComparison.Ordinal)
            ? "An expanded translated menu label for " + fallback : fallback;
    }
}
