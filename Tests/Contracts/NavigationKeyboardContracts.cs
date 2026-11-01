using System;
using System.Linq;
using Inno.Core.Input;
using Inno.Core.Serialization;
using Inno.Rendering;
using Inno.Rendering.Runtime;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NavigationKeyboardContracts
{
    internal static void NativeMappingsAndPhysicalText(SerializationRegistry native)
    {
        var preferences = new PlayerPreferences(); preferences.SetKeyboardShortcut("automatic", KeyCode.Q);
        preferences.SetNavigationShortcut("confirm", KeyCode.A); preferences.SetNavigationShortcut("next", KeyCode.J);
        Reject(() => preferences.SetNavigationShortcut("cancel", KeyCode.S));
        Reject(() => preferences.SetKeyboardShortcut("skip", KeyCode.J));
        var restored = native.Deserialize<PlayerPreferencesState>(native.Serialize(new PlayerPreferencesState { gameId = "navigation", preferences = preferences })).preferences.Copy();
        Check(restored.ResolveNavigationShortcut([KeyCode.A]) == "confirm" && restored.ResolveKeyboardShortcut([KeyCode.Q]) == "automatic", "Native preference materialization rejected a valid exchanged default key or omitted navigation.");
        var source = new KeyboardSettingsDataSource(() => restored, static (_, text) => text);
        var row = source.ReadRows("settings.navigationShortcuts").Single(static item => item.key == "cancel");
        Check(source.TryWrite("item.shortcutKey", NovelValue.Enum("Control+X"), row) && restored.ResolveNavigationShortcut([KeyCode.X], KeyModifier.Control) == "cancel",
            "A typed navigation row did not retain its distinct owner or exact chord.");
        var adapter = new InnoNovelInput(); var data = new TextSource();
        var page = new UiDocumentInstance(new ScreenAsset { width = 500, height = 200, root = new() { id = "input", kind = UiElementKind.Input, binding = "name", action = "submit",
            layout = new() { width = 500, height = 100 } } }, static (_, text) => (text.Length * 10, 28));
        page.HandleInput(Map([KeyCode.A], text: ["a"]), data);
        page.HandleInput(Map([KeyCode.J], text: ["j"]), data);
        Check(data.value.text == "aj" && page.focusedElement == "input", "A remapped printable navigation key submitted or moved focus instead of typing.");
        page.HandleInput(Map([KeyCode.LeftArrow]), data); page.HandleInput(Map([KeyCode.T], text: ["t"]), data);
        Check(data.value.text == "atj", "Physical text cursor movement was replaced by menu mapping.");
        Check(page.HandleInput(Map([KeyCode.Enter]), data).actions.Single().action == "submit", "Physical Enter could not submit a text field after remapping menu confirmation.");
        UiInputSnapshot navigation = Map([KeyCode.J]);
        Check(navigation.next && !navigation.textRight && navigation.InCanvas(Inno.VisualNovel.UI.Layout.UiCanvasFit.Create(500, 200, new(0, 0, 500, 200), true)).next,
            "Navigation mapping or nested canvas projection lost action ownership.");
        restored.ResetKeyboardShortcuts(); Check(restored.ResolveNavigationShortcut([KeyCode.Enter]) == "confirm" && restored.ResolveKeyboardShortcut([KeyCode.A]) == "automatic", "Reset did not restore both mapping tables atomically.");
        UiInputSnapshot Map(
            KeyCode[] keys,
            KeyModifier modifiers = KeyModifier.None,
            string[]? text = null
        ) => adapter.Map(new(default, false, default, modifiers, keys, [], [], [], text ?? []), new(0, 0, 500, 200), 500, 200, restored);
    }

    internal static void ActualSettingsAndTextCancellation(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var game = KeyboardSettingsContracts.Game(codec, compiler);
        using var session = new NovelSession(game); session.StartNew(); session.OpenPage("settings");
        using var view = new NovelGameView(game, session, static (_, text) => (text.Length * 14, 28));
        for (int count = 0; count < 100 && !view.BuildFrame().items.Any(static item => item.focused && item.id.EndsWith("confirm/navigation-key", StringComparison.Ordinal)); count++) view.HandleInput(new() { next = true });
        Check(view.BuildFrame().items.Any(static item => item.focused && item.id.EndsWith("confirm/navigation-key", StringComparison.Ordinal)), "Actual settings did not expose navigation rows.");
        view.HandleInput(new() { confirm = true }); view.HandleInput(new() { text = ["Ctrl+Q"] }); view.HandleInput(new() { confirm = true });
        Check(session.preferences.ResolveNavigationShortcut([KeyCode.Q], KeyModifier.Control) == "confirm" && session.page == "settings", "A searched typed navigation chord changed the wrong table or escaped its modal page.");
        view.HandleInput(new() { confirm = true });
        view.HandleInput(new() { selectAll = true, text = ["Ctrl+T"] }); view.ReleaseInput(preserveTextEntry: true);
        Check(view.TryGetFocusedInputText(out string query) && query == "Ctrl+T", "A native text bridge lost its explicitly owned selector query.");
        view.ReleaseInput();
        Check(view.BuildFrame().items.All(static item => !item.id.Contains("/option/", StringComparison.Ordinal)) && session.page == "settings", "Host focus loss retained a control popup or closed the page itself.");
        var page = new UiDocumentInstance(new ScreenAsset { width = 500, height = 200, root = new() { id = "text", kind = UiElementKind.Input, binding = "name", layout = new() { width = 500, height = 80 } } }, static (_, _) => (80, 28));
        var data = new TextSource();
        var result = page.HandleInput(new() { text = ["x"], cancel = true }, data);
        Check(result.cancelConsumed && result.consumed && data.value.text == "x", "Typing a remapped cancel key escaped the text owner.");
        var selector = new UiDocumentInstance(new ScreenAsset { width = 500, height = 200, root = new() { id = "select", kind = UiElementKind.Selector, binding = "name",
            options = [new() { id = "x", text = "X option", value = NovelValue.String("x") }], layout = new() { width = 500, height = 80 } } }, static (_, _) => (80, 28));
        selector.HandleInput(new() { confirm = true }, data);
        Check(selector.HandleInput(new() { text = ["x"], cancel = true }, data).cancelConsumed && selector.popupOpen,
            "A printable remapped cancel key escaped a selector's search owner.");
    }

    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("An occupied cross-table navigation chord was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class TextSource : UiDataSource
    {
        internal NovelValue value = NovelValue.String("");
        public override bool TryRead(
            string path,
            out NovelValue result
        ) { result = value.Copy(); return path == "name"; }
        public override bool TryWrite(
            string path,
            NovelValue result
        ) { if (path != "name") return false; value = result.Copy(); return true; }
    }
}
