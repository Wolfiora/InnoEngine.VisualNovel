using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Interface;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PageStateContracts
{
    internal static void ActualControlsAndPersistence(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        NovelProjectAsset game = Game(codec, compiler);
        using var session = new NovelSession(game); session.StartNew();
        using var view = new NovelGameView(game, session, static (_, text) => (text.Length * 10, 28));
        long interaction = session.story!.interactionSequence;
        Click(view, "expand");
        Check(Read(session, "expanded").boolean && view.BuildFrame().items.Any(static item => item.id == "mark"), "An actual page action did not reveal its dependent control.");
        Click(view, "mark");
        Check(Read(session, "marked").boolean && session.story.interactionSequence == interaction, "A UI state write advanced narrative or missed its own page field.");
        session.pageState.TryWrite("reading", "count", NovelValue.Integer(7));
        session.pageState.TryWrite("reading", "player", NovelValue.String("UI only"));
        Click(view, "selector");
        view.HandleInput(new() { next = true }); view.HandleInput(new() { confirm = true });
        Check(Read(session, "selected").text == "B", "A typed selector did not write its page-owned enum from page list rows.");
        session.OpenPage("pause"); view.BuildFrame(); session.OpenPage("reading"); view.BuildFrame();
        Check(!Read(session, "expanded").boolean && Read(session, "marked").boolean && Read(session, "count").integer == 7,
            "Closing a page failed to reset Local fields or erased saved Game fields.");
        Check(session.story.GetVariable("player").text != "UI only", "A page field overwrote an equally named narrative variable.");
        NovelSessionState saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        Check(saved.pageState.Single().values.All(static field => field.id != "expanded") && saved.pageState.Single().values.Length == 6,
            "Native saves included transient UI state or omitted a declared persistent field.");
        session.pageState.TryWrite("reading", "count", NovelValue.Integer(99));
        var damaged = native.Deserialize<NovelSessionState>(native.Serialize(saved));
        damaged.pageState[0].values.Single(static field => field.id == "count").value = NovelValue.String("wrong type");
        var originalStory = session.story; var originalPages = session.pageState;
        Reject(() => session.Restore(damaged));
        Check(ReferenceEquals(session.story, originalStory) && ReferenceEquals(session.pageState, originalPages) && Read(session, "count").integer == 99,
            "A rejected UI snapshot partially replaced the running session.");
        session.Restore(saved);
        Check(Read(session, "count").integer == 7 && Read(session, "selected").text == "B" && !Read(session, "expanded").boolean,
            "Native restoration lost persistent page values or restored transient state.");
        saved.pageState[0].values.Single(static field => field.id == "count").value = NovelValue.Integer(-123);
        Check(Read(session, "count").integer == 7, "The restored UI retained caller-owned snapshot arrays.");
        Click(view, "reset-count"); Check(Read(session, "count").integer == 0, "The actual reset action did not restore the authored default.");

        session.StartNew();
        session.pageState.TryWrite("reading", "count", NovelValue.Integer(11));
        session.Advance(); session.Advance();
        session.pageState.TryWrite("reading", "count", NovelValue.Integer(22));
        session.Choose("sea");
        Check(session.Rollback() && Read(session, "count").integer == 11, "Rollback did not restore UI state at the exact previous interaction boundary.");
        session.StartNew(); Check(Read(session, "count").integer == 0, "New game retained prior persistent page values.");
    }

    internal static void SchemasEditingAndComponents(SerializationRegistry native)
    {
        var source = UiWidgetTemplates.Create("disclosure", null!);
        var detached = UiDefinitionCopy.Copy(source);
        detached.stateVariables[0].initialValue = NovelValue.Boolean(true);
        Check(!source.stateVariables[0].initialValue.boolean, "UI declaration copying retained mutable state metadata.");
        var sameTheme = UiDefinitionCopy.Copy(source); sameTheme.stateVariables[0].name = "Translated name";
        var store = new NovelPageStateStore([new() { role = "reading", screen = source }, new() { role = "reading", screen = sameTheme }]);
        source.stateVariables[0].initialValue = NovelValue.Boolean(true);
        store.Enter("reading"); Check(store.TryRead("reading", "expanded", out var frozen) && !frozen.boolean, "The page state schema was not frozen at session creation.");
        sameTheme.stateVariables[0].initialValue = NovelValue.Boolean(false);
        Reject(() => new NovelPageStateStore([new() { role = "reading", screen = source }, new() { role = "reading", screen = sameTheme }]));

        var widget = UiWidgetTemplates.Create("disclosure", null!);
        var before = native.Serialize(widget.stateVariables[0]);
        var invalid = VariableValueRules.Copy(widget.stateVariables[0]); invalid.initialValue = NovelValue.Integer(3);
        Reject(() => UiStateEditService.Set(widget, invalid));
        Check(native.Serialize(widget.stateVariables[0]).AsSpan().SequenceEqual(before), "A failed page type change partially edited its declaration.");
        Reject(() => UiStateEditService.Remove(widget, "expanded"));
        var clear = UiDefinitionCopy.Copy(widget); UiStateEditService.Remove(clear, "expanded", true);
        Check(clear.stateVariables.Length == 1 && UiStateEditService.References(clear).All(static item => item.fieldId != "expanded"), "Explicit field deletion left unresolved page bindings.");
        invalid = VariableValueRules.Copy(widget.stateVariables[0]); invalid.scope = VariableScope.Global;
        Reject(() => UiStateEditService.Set(widget, invalid));
        var malformed = UiDefinitionCopy.Copy(widget); malformed.root.children[0].argument = "missing";
        Reject(() => new UiDocumentInstance(malformed, static (_, _) => (0, 0)));

        var destination = new ScreenAsset { width = 1280, height = 720, root = new() { id = "root", kind = UiElementKind.Vertical, layout = new() { width = 1280, height = 720 } } };
        string first = UiWidgetEditService.Insert(destination, "root", widget);
        string second = UiWidgetEditService.Insert(destination, "root", widget);
        var firstWidget = UiWidgetEditService.Extract(destination, first, "First");
        var secondWidget = UiWidgetEditService.Extract(destination, second, "Second");
        Check(firstWidget.stateVariables.Length == 2 && secondWidget.stateVariables.Length == 2
            && firstWidget.stateVariables.Select(static field => field.id).Intersect(secondWidget.stateVariables.Select(static field => field.id), StringComparer.Ordinal).Count() == 0,
            "Independent component instances shared page field identities or captured unrelated state.");
        _ = new UiDocumentInstance(destination, static (_, _) => (0, 0));
        var instances = new NovelPageStateStore([new() { role = "reading", screen = destination }]);
        string firstExpanded = firstWidget.stateVariables.Single(static field => field.name == "显示详情").id;
        string secondExpanded = secondWidget.stateVariables.Single(static field => field.name == "显示详情").id;
        Check(instances.TryWrite("reading", firstExpanded, NovelValue.Boolean(true)) && instances.TryRead("reading", secondExpanded, out var other) && !other.boolean,
            "Writing one copied widget changed a different instance.");
        var roundtrip = native.Deserialize<VariableDefinition>(native.Serialize(firstWidget.stateVariables[0]));
        Check(roundtrip.id == firstWidget.stateVariables[0].id && roundtrip.scope == firstWidget.stateVariables[0].scope, "Native field metadata lost its stable copied lifetime contract.");
    }

    private static NovelProjectAsset Game(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var page = UiWidgetTemplates.Create("disclosure", null!); page.width = 800; page.height = 650; page.root.layout.height = 650;
        page.stateVariables = [.. page.stateVariables,
            new() { id = "count", scope = VariableScope.Game, initialValue = NovelValue.Integer(0) },
            new() { id = "player", scope = VariableScope.Game, initialValue = NovelValue.String("UI") },
            new() { id = "selected", scope = VariableScope.Game, initialValue = NovelValue.Enum("A"), members = ["A", "B"] },
            new() { id = "options", scope = VariableScope.Game, initialValue = NovelValue.List(NovelValueKind.Enum, [NovelValue.Enum("A"), NovelValue.Enum("B")]), members = ["A", "B"] },
            new() { id = "message", scope = VariableScope.Game, initialValue = NovelValue.String("saved page text") }];
        page.root.children = [.. page.root.children,
            new() { id = "selector", kind = UiElementKind.Selector, binding = "page.selected", optionsBinding = "page.options", layout = new() { width = 560, height = 60 } },
            new() { id = "reset-count", kind = UiElementKind.Button, text = "Reset", action = "page.reset", argument = "count", layout = new() { width = 560, height = 60 } }];
        return new() { gameId = "page-state-contract", chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }
                .Select(role => new NovelScreenBinding { role = role, screen = role == "reading" ? page : ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }

    private static NovelValue Read(
        NovelSession session,
        string id
    ) => session.pageState.TryRead("reading", id, out var value) ? value : throw new InvalidOperationException("Missing page field: " + id);

    private static void Click(
        NovelGameView view,
        string id
    ) {
        var button = view.BuildFrame().items.Single(item => item.id == id);
        float x = button.bounds.x + button.bounds.width / 2, y = button.bounds.y + button.bounds.height / 2;
        view.HandleInput(new() { x = x, y = y, pointerInside = true, pressed = true, held = true });
        view.HandleInput(new() { x = x, y = y, pointerInside = true, released = true });
    }

    private static void Reject(Action operation)
    {
        try { operation(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; }
        throw new InvalidOperationException("An invalid page state candidate was accepted.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
