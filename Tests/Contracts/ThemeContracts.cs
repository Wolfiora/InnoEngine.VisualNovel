using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ThemeContracts
{
    internal static void InheritanceAndInputStates(SerializationRegistry native)
    {
        ThemeAsset theme = Create(); var source = Screen();
        source = native.Deserialize<UiElementDefinition>(native.Serialize(source.root)) is { } root ? new() { width = 400, height = 200, root = root } : throw new InvalidOperationException();
        var resolver = new UiThemeResolver(theme); var resolved = resolver.Apply(source);
        Check(resolved.root.children[0].style.fontSize == 36 && resolved.root.children[0].style.foreground == 0x123456FF
            && source.root.children[0].style.fontSize == 12 && source.root.children[0].styleClass == "button.primary",
            "Style inheritance ignored metric tokens or page-local field ownership, or mutated the source page.");
        var copy = resolver.GetStyle("button.primary"); copy.fontSize = 999;
        Check(resolver.GetStyle("button.primary").fontSize == 36, "Resolved style lookup exposed mutable aliases.");
        var page = new UiDocumentInstance(source, static (_, _) => (100, 30), theme: theme);
        theme.colors[0].value = 0; theme.styles[0].values.pressed = 0;
        var normal = page.BuildFrame(new Empty()).items.Single(static item => item.id == "button");
        Check(normal.background == 0x336699FF && normal.foreground == 0x123456FF && normal.fontSize == 36, "The UI runtime did not freeze the resolved theme.");
        page.HandleInput(new() { pointerInside = true, x = 10, y = 10 }, new Empty());
        Check(page.BuildFrame(new Empty()).items.Single(static item => item.id == "button").background == 0x5577AAFF, "Hover did not use the inherited theme state.");
        page.HandleInput(new() { pointerInside = true, x = 10, y = 10, pressed = true }, new Empty());
        Check(page.BuildFrame(new Empty()).items.Single(static item => item.id == "button").background == 0x223344FF, "Pressed state lost its inherited literal value.");
        page.SetFontScale(1.5f); Check(page.BuildFrame(new Empty()).items.Single(static item => item.id == "button").fontSize == 54, "Accessibility size scaling used the pre-theme font metric.");
        source.root.children[0].interactable = false; source.root.children[0].styleOverrides = UiStyleProperty.None;
        var disabled = new UiDocumentInstance(source, static (_, _) => (0, 30), theme: Create()).BuildFrame(new Empty()).items.Single(static item => item.id == "button");
        Check(disabled.background == 0xCCDDEEFF && disabled.foreground == 0x665544FF, "Disabled text and fill could not be styled independently.");
    }
    internal static void RejectInvalidGraphs()
    {
        ThemeAsset theme = Create(); theme.styles[0].parent = "button.primary"; Reject(() => new UiThemeResolver(theme));
        theme = Create(); theme.styles[1].bindings[0].token = "size.body"; Reject(() => new UiThemeResolver(theme));
        theme = Create(); theme.styles[1].bindings = [theme.styles[1].bindings[0], theme.styles[1].bindings[0]]; Reject(() => new UiThemeResolver(theme));
        theme = Create(); theme.metrics[0].value = float.NaN; Reject(() => new UiThemeResolver(theme));
        theme = Create(); theme.styles[1].fields = (UiStyleProperty)(1 << 20); Reject(() => new UiThemeResolver(theme));
        var page = Screen(); page.root.children[0].styleClass = "unavailable"; Reject(() => new UiThemeResolver(Create()).Apply(page));
        page = Screen(); page.root.children[0].styleOverrides = (UiStyleProperty)(1 << 20); Reject(() => new UiDocumentInstance(page, static (_, _) => (0, 0)));
        theme = Create(); theme.colors = [theme.colors[0], theme.colors[0]]; Reject(() => new UiThemeResolver(theme));
    }
    internal static ThemeAsset Create() => new()
    {
        resourceId = "clear", title = "Clear",
        colors = [new() { key = "color.primary", value = 0x336699FF }, new() { key = "color.hover", value = 0x5577AAFF }],
        metrics = [new() { key = "size.body", value = 36 }],
        styles = [
            new() { key = "control", fields = UiStyleProperty.All, values = new() { pressed = 0x223344FF, disabled = 0xCCDDEEFF, disabledForeground = 0x665544FF, foreground = 0xFFFFFFFF },
                bindings = [new() { property = UiStyleProperty.FontSize, token = "size.body" }] },
            new() { key = "button.primary", parent = "control", fields = UiStyleProperty.Background | UiStyleProperty.Hover,
                bindings = [new() { property = UiStyleProperty.Background, token = "color.primary" }, new() { property = UiStyleProperty.Hover, token = "color.hover" }] }
        ]
    };
    internal static ScreenAsset Screen() => new() { width = 400, height = 200, root = new() { id = "root", kind = UiElementKind.Panel, layout = new() { width = 400, height = 200 }, children = [new()
    {
        id = "button", kind = UiElementKind.Button, text = "开始", styleClass = "button.primary", styleOverrides = UiStyleProperty.Foreground,
        style = new() { fontSize = 12, foreground = 0x123456FF }, layout = new() { width = 200, height = 80 }
    }] } };
    private sealed class Empty : UI.Binding.UiDataSource
    {
        public override bool TryRead(string path, out Model.Values.NovelValue value) { value = Model.Values.NovelValue.String(""); return false; }
        public override bool TryWrite(string path, Model.Values.NovelValue value) => false;
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException or ArgumentException) { return; }
        throw new InvalidOperationException("Invalid theme data was accepted.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
