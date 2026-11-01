using System;
using System.Linq;
using Inno.Core.Input;
using Inno.Core.Serialization;
using Inno.Rendering;
using Inno.Rendering.Runtime;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Player.Shell.Keyboard;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class KeyboardChordContracts
{
    internal static void ExactModifiersPersistenceAndHoldOwnership(SerializationRegistry native)
    {
        var preferences = new PlayerPreferences(); preferences.SetKeyboardShortcut("automatic", KeyCode.Q, KeyModifier.Control);
        preferences.SetKeyboardShortcut("hide", KeyCode.Q, KeyModifier.Alt);
        Reject(() => preferences.SetKeyboardShortcut("skip", KeyCode.Q, KeyModifier.Control));
        Reject(() => preferences.SetKeyboardShortcut("automatic", KeyCode.Unknown, KeyModifier.Control));
        Check(preferences.ResolveKeyboardShortcut([KeyCode.Q], KeyModifier.Control) == "automatic" && preferences.ResolveKeyboardShortcut([KeyCode.Q], KeyModifier.Alt) == "hide"
            && preferences.ResolveKeyboardShortcut([KeyCode.Q]) == "" && preferences.ResolveKeyboardShortcut([KeyCode.Q], KeyModifier.Control | KeyModifier.Alt) == "",
            "Chord matching ignored exact modifier ownership, modified another action or treated AltGr as Ctrl.");
        preferences.holdSkipModifiers = KeyModifier.Shift;
        var packet = native.Deserialize<PlayerPreferencesState>(native.Serialize(new PlayerPreferencesState { gameId = "chords", preferences = preferences }));
        Check(packet.preferences.holdSkipModifiers == KeyModifier.Shift && packet.preferences.ResolveKeyboardShortcut([KeyCode.Q], KeyModifier.Control) == "automatic"
            && packet.preferences.Copy().keyboardShortcuts.Single(static item => item.action == "automatic").modifiers == KeyModifier.Control, "Native profile capture or copying erased exact chords or the hold policy.");
        var source = new KeyboardSettingsDataSource(() => preferences, static (_, text) => text);
        var row = source.ReadRows("settings.shortcuts").Single(static item => item.key == "automatic");
        string choice = NovelKeyboardShortcuts.ChoiceId(KeyCode.T, KeyModifier.Control | KeyModifier.Shift);
        Check(source.ReadRows("settings.shortcutKeys").Any(item => item.values["value"].text == choice)
            && source.TryWrite("item.shortcutKey", NovelValue.Enum(choice), row) && preferences.ResolveKeyboardShortcut([KeyCode.T], KeyModifier.Control | KeyModifier.Shift) == "automatic",
            "An atomic typed selector choice lost modifiers or required an invalid intermediate assignment.");
        var adapter = new InnoNovelInput(); Map(KeyModifier.None);
        Check(Map(KeyModifier.Shift, [KeyCode.LeftShift]).skipHeld, "A configured hold modifier did not start skipping.");
        Check(!Map(KeyModifier.Shift, [KeyCode.T]).skipHeld && !Map(KeyModifier.Shift).skipHeld, "A modifier chord rearmed held skipping before release.");
        Map(KeyModifier.None); Check(Map(KeyModifier.Shift).skipHeld, "Releasing the configured chord did not permit a new hold.");
        Check(Map(KeyModifier.Control | KeyModifier.Shift, [KeyCode.T]).readingShortcut == "automatic", "Formal viewport mapping discarded a configured chord.");
        preferences.holdSkipModifiers = KeyModifier.Alt; Check(!Map(KeyModifier.Alt).skipHeld, "Changing hold policy armed already held modifiers.");
        Map(KeyModifier.None); Check(Map(KeyModifier.Alt).skipHeld, "A changed hold binding did not rearm after release.");
        adapter.Map(RenderOutputInput.suspended, new(0, 0, 800, 600), 800, 600, preferences);
        Check(!Map(KeyModifier.Alt).skipHeld, "Resuming a suspended output inherited an old hold.");
        preferences.holdSkipModifiers = KeyModifier.None; Check(!Map(KeyModifier.Alt).skipHeld, "An explicitly disabled hold policy remained active.");
        preferences.ResetKeyboardShortcuts(); Check(preferences.holdSkipModifiers == KeyModifier.Control && preferences.ResolveKeyboardShortcut([KeyCode.A]) == "automatic", "Reset omitted hold policy or default key bindings.");
        UiInputSnapshot Map(KeyModifier modifiers, KeyCode[]? pressed = null) => adapter.Map(new(default, false, default, modifiers, pressed ?? [], [], [], [], []), new(0, 0, 800, 600), 800, 600, preferences);
    }
    internal static void ActualHoldSelectorAndModalReading(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var game = KeyboardSettingsContracts.Game(codec, compiler);
        using var session = new NovelSession(game); session.StartNew(); session.OpenPage("settings");
        using var view = new NovelGameView(game, session, static (_, text) => (text.Length * 16, 28));
        Focus("hold-skip-choice"); view.HandleInput(new() { confirm = true }); view.HandleInput(new() { next = true }); view.HandleInput(new() { confirm = true });
        Check(session.preferences.holdSkipModifiers == KeyModifier.Shift && session.page == "settings", "The actual editable hold selector changed the wrong value or leaked confirmation.");
        session.preferences.SetKeyboardShortcut("automatic", KeyCode.Q, KeyModifier.Control);
        view.HandleInput(new() { readingShortcut = "automatic" }); Check(!session.automatic, "A configured chord escaped modal ownership.");
        view.HandleInput(new() { cancel = true }); view.HandleInput(new() { readingShortcut = "automatic" });
        Check(session.page == "reading" && session.automatic && !session.story!.isTextComplete, "A chord failed after menu retirement or also advanced the story.");
        void Focus(string id)
        {
            for (int count = 0; count < 100; count++)
            { if (view.BuildFrame().items.Any(item => item.focused && item.id == id)) return; view.HandleInput(new() { next = true }); }
            throw new InvalidOperationException("The native focus route could not reach " + id);
        }
    }
    private static void Reject(Action operation)
    { try { operation(); } catch (ArgumentException) { return; } throw new InvalidOperationException("An invalid chord was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
