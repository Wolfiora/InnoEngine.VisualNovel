using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Interface.Actions;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiActionContracts
{
    internal static void ActualControlsAndLifetime(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var definition = Definition("custom.count"); var game = Game(codec, compiler, definition);
        var tally = new Tally(); var registry = new NovelUiActionRegistry(); registry.Register(() => new ActionHandler(definition, tally));
        var snapshot = registry.Snapshot(); Reject(() => snapshot.Register(() => new ActionHandler(definition, tally)));
        using var session = new NovelSession(game, options: new() { uiActions = registry }); session.StartNew();
        long sequence = session.story!.interactionSequence;
        using (var view = new NovelGameView(game, session, static (_, text) => (text.Length * 10, 26)))
        {
            var initial = view.BuildFrame().items.Single(static item => item.id == "set");
            Check(initial.background == 0x123456FF, "Action selection changed focus or the unselected fill.");
            Click(view, "set");
            Check(Read(session, "count").integer == 7 && tally.executions == 1 && session.story.interactionSequence == sequence, "Typed UI action was lost, duplicated or advanced narrative.");
            view.HandleInput(new() { x = 750, y = 500, pointerInside = true });
            Check(view.BuildFrame().items.Single(static item => item.id == "set").background == 0xFEDCBAFF, "IsSelected did not paint independently of pointer focus.");
            var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
            session.Advance(); session.Advance(); session.pageState.TryWrite("reading", "count", NovelValue.Integer(99)); session.Choose("sea");
            Check(session.Rollback() && Read(session, "count").integer == 7, "A custom action state write was not captured at the native interaction checkpoint.");
            session.pageState.TryWrite("reading", "count", NovelValue.Integer(99)); session.Restore(saved);
            Check(Read(session, "count").integer == 7, "Custom action state bypassed ordinary native persistence.");
            session.pageState.TryWrite("reading", "allowed", NovelValue.Boolean(false)); Click(view, "set");
            Check(tally.executions == 1 && view.BuildFrame().items.Single(static item => item.id == "set").background == 0x111111FF, "CanExecute did not disable the actual button.");
            session.pageState.TryWrite("reading", "allowed", NovelValue.Boolean(true));
            definition.argument.defaultValue = NovelValue.Integer(222);
            Click(view, "set"); Check(tally.executions == 1 && view.actionError.Contains("contract", StringComparison.OrdinalIgnoreCase), "A changed factory contract silently executed.");
        }
        Check(tally.created == tally.disposed, "Generation-owned action handlers leaked after the game view closed.");
    }

    internal static void PoliciesAndDiagnostics(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var definition = Definition("custom.external"); definition.capabilities = CommandCapabilities.Save | CommandCapabilities.ExternalEffects;
        var tally = new Tally(); var registry = new NovelUiActionRegistry(); registry.Register(() => new ActionHandler(definition, tally));
        var game = Game(codec, compiler, definition);
        using (var preview = new NovelSession(game, preview: true, options: new() { uiActions = registry }))
        {
            preview.StartNew(); using var view = new NovelGameView(game, preview, static (_, _) => (10, 26));
            Click(view, "set"); Check(tally.executions == 0 && view.actionError.Contains("preview", StringComparison.OrdinalIgnoreCase), "External UI actions executed during preview.");
        }
        using (var session = new NovelSession(game, options: new() { uiActions = registry }))
        {
            session.StartNew(); using var view = new NovelGameView(game, session, static (_, _) => (10, 26));
            Click(view, "set"); Check(tally.executions == 1 && session.story!.rollbackBarrierReason.Contains("custom.external", StringComparison.Ordinal), "An irreversible UI action failed to establish its narrative rollback barrier.");
        }
        var pure = Definition("custom.impure"); var pureGame = Game(codec, compiler, pure); var impure = new NovelUiActionRegistry();
        impure.Register(() => new ActionHandler(pure, tally, true));
        using (var session = new NovelSession(pureGame, options: new() { uiActions = impure }))
        {
            session.StartNew(); using var view = new NovelGameView(pureGame, session, static (_, _) => (10, 26));
            Click(view, "set"); Check(Read(session, "count").integer == 0 && view.actionError.Contains("cannot modify", StringComparison.Ordinal), "An availability query mutated game state.");
        }
        using (var session = new NovelSession(pureGame))
        {
            session.StartNew(); using var view = new NovelGameView(pureGame, session, static (_, _) => (10, 26));
            Click(view, "set"); Check(view.actionError.Contains("custom.impure @ set", StringComparison.Ordinal), "Missing actions did not identify their control and stable action ID.");
        }
    }

    internal static void SchemasAndAuthoring(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var declaration = Definition("custom.authoring"); var game = Game(codec, compiler, declaration);
        UiActionBindingValidator.Validate(game);
        var catalog = new UiActionCatalogAsset { definitions = [declaration] };
        var copied = native.Deserialize<UiActionDefinition>(native.Serialize(declaration));
        Check(UiActionContract.Fingerprint(copied) == UiActionContract.Fingerprint(declaration), "Native action schema lost its typed contract.");
        var invalid = UiActionContract.Copy(declaration); invalid.capabilities |= CommandCapabilities.Cancel;
        Reject(() => UiActionCatalogEditService.Set(catalog, invalid)); Check(catalog.definitions.Length == 1, "Rejected catalog changes partially committed.");
        var button = game.screens.Single(static page => page.role == "reading").screen!.root.children[0];
        button.argumentBinding = "page.missing"; Reject(() => UiActionBindingValidator.Validate(game));
        button.argumentBinding = "page.allowed"; Reject(() => UiActionBindingValidator.Validate(game));
        button.argumentBinding = string.Empty; button.argument = "123"; UiActionBindingValidator.Validate(game);
        button.argument = "not-a-number"; Reject(() => UiActionBindingValidator.Validate(game));
        var list = UiActionContract.Copy(declaration); list.argument.defaultValue = NovelValue.List(NovelValueKind.Integer, [NovelValue.Integer(4)]);
        Check(UiActionContract.ResolveArgument(list, "").items[0].integer == 4, "Default typed lists were not supported.");
        Reject(() => UiActionContract.ResolveArgument(list, "[4]"));
        list.argument.defaultValue = NovelValue.Enum("A"); list.argument.members = ["A", "B"];
        Reject(() => UiActionContract.ResolveArgument(list, "C"));
        Reject(() => UiActionContract.ValidateCatalog([declaration, copied]));
    }

    private sealed class Tally { internal int created, disposed, executions; }
    private sealed class ActionHandler : NovelUiAction
    {
        private readonly UiActionDefinition m_definition;
        private readonly Tally m_tally;
        private readonly bool m_impure;
        internal ActionHandler(
            UiActionDefinition definition,
            Tally tally,
            bool impure = false
        ) { m_definition = definition; m_tally = tally; m_impure = impure; tally.created++; }
        public override UiActionDefinition definition => m_definition;
        public override bool CanExecute(NovelUiActionContext context, NovelValue argument)
        { if (m_impure) context.Set("page.count", NovelValue.Integer(888)); return context.Get("page.allowed").boolean; }
        public override bool IsSelected(NovelUiActionContext context, NovelValue argument) => context.Get("page.count").Equals(argument);
        public override void Execute(NovelUiActionContext context, NovelValue argument) { m_tally.executions++; context.Set("page.count", argument); }
        public override void Dispose() => m_tally.disposed++;
    }
    private static UiActionDefinition Definition(string id) => new() { id = id, title = "Set count", argument = new() { id = "argument", label = "Count", defaultValue = NovelValue.Integer(1) } };
    private static NovelProjectAsset Game(StoryNodeDataCodec codec, StoryCompiler compiler, UiActionDefinition definition)
    {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var reading = new ScreenAsset { width = 800, height = 600, root = new() { id = "root", kind = UiElementKind.Vertical, layout = new() { width = 800, height = 600 }, children =
            [new() { id = "set", kind = UiElementKind.Button, text = "Set", action = definition.id, argumentBinding = "page.delta", layout = new() { width = 500, height = 80 },
                style = new() { background = 0x123456FF, pressed = 0xFEDCBAFF, disabled = 0x111111FF } }] },
            stateVariables = [new() { id = "count", scope = VariableScope.Game, initialValue = NovelValue.Integer(0) },
                new() { id = "delta", scope = VariableScope.Local, initialValue = NovelValue.Integer(7) }, new() { id = "allowed", scope = VariableScope.Local, initialValue = NovelValue.Boolean(true) }] };
        return new() { gameId = "ui-actions", chapters = [chapter], startChapter = chapter.chapterId, uiActions = [UiActionContract.Copy(definition)],
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = role == "reading" ? reading : ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }
    private static NovelValue Read(NovelSession session, string id) => session.pageState.TryRead("reading", id, out var value) ? value : throw new InvalidOperationException("Missing page field.");
    private static void Click(NovelGameView view, string id)
    {
        var button = view.BuildFrame().items.Single(item => item.id == id); float x = button.bounds.x + 30, y = button.bounds.y + 30;
        view.HandleInput(new() { x = x, y = y, pointerInside = true, pressed = true, held = true }); view.HandleInput(new() { x = x, y = y, pointerInside = true, released = true });
    }
    private static void Reject(Action operation)
    { try { operation(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or OverflowException) { return; } throw new InvalidOperationException("Invalid UI action contract was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
