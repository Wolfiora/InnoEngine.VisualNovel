using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Interface.Data;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiDataSourceContracts
{
    internal static void ActualBindingsAndRows(StoryNodeDataCodec codec, StoryCompiler compiler, SerializationRegistry native)
    {
        var metadata = Definition(); var game = Game(codec, compiler, metadata); var tally = new Tally();
        var registry = Registry(metadata, tally);
        using var session = new NovelSession(game, options: new() { uiDataSources = registry }); session.StartNew();
        using (var view = new NovelGameView(game, session, static (_, text) => (text.Length * 10, 24)))
        {
            Check(view.BuildFrame().items.Any(static item => item.id == "label" && item.text == "derived"), "An actual authored custom value was not resolved.");
            Click(view, "root-flag"); Check(Read(session, "flag"), "An actual custom root toggle did not write its native context.");
            string second = view.BuildFrame().items.Single(static item => item.kind == UiElementKind.Toggle && item.id.Contains("second", StringComparison.Ordinal)).id;
            Click(view, second); Check(!Read(session, "first") && Read(session, "second"), "A custom row write lost its exact stable owner.");
            tally.reverse = true;
            Check(view.BuildFrame().items.First(static item => item.kind == UiElementKind.Toggle && item.id != "root-flag").id.Contains("second", StringComparison.Ordinal), "Custom source reordering changed row ownership into display indices.");
            var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
            session.pageState.TryWrite("reading", "second", NovelValue.Boolean(false)); session.Restore(saved);
            Check(Read(session, "second"), "Custom derived source state bypassed native save restoration.");
            Reject(() => tally.context!.Set("page.flag", NovelValue.Boolean(false)));
            tally.duplicate = true; view.BuildFrame(); Check(view.dataSourceError.Contains("unique", StringComparison.Ordinal), "Duplicate source keys were silently assigned unstable identity.");
        }
        Check(tally.created == 1 && tally.disposed == 1, "A view did not release its single lazy source owner exactly once.");
    }
    internal static void PoliciesAndMismatch(StoryNodeDataCodec codec, StoryCompiler compiler)
    {
        var metadata = Definition(); metadata.capabilities = CommandCapabilities.Save | CommandCapabilities.ExternalEffects;
        var tally = new Tally(); var game = Game(codec, compiler, metadata);
        using (var session = new NovelSession(game, preview: true, options: new() { uiDataSources = Registry(metadata, tally) }))
        {
            session.StartNew(); using var view = new NovelGameView(game, session, static (_, _) => (20, 24));
            view.BuildFrame(); Check(tally.created == 0 && view.dataSourceError.Contains("preview", StringComparison.OrdinalIgnoreCase), "External source queries executed in preview.");
        }
        using (var session = new NovelSession(game, options: new() { uiDataSources = Registry(metadata, tally) }))
        {
            session.StartNew(); using var view = new NovelGameView(game, session, static (_, _) => (20, 24));
            Click(view, "root-flag"); Check(Read(session, "flag") && session.story!.rollbackBarrierReason.Contains("stats", StringComparison.Ordinal), "An irreversible source write did not establish a rollback barrier.");
        }
        metadata = Definition(); tally = new Tally { impure = true }; game = Game(codec, compiler, metadata);
        using (var session = new NovelSession(game, options: new() { uiDataSources = Registry(metadata, tally) }))
        {
            session.StartNew(); using var view = new NovelGameView(game, session, static (_, _) => (20, 24));
            view.BuildFrame(); Check(!Read(session, "flag") && view.dataSourceError.Contains("cannot modify", StringComparison.Ordinal), "A source query modified ordinary native state.");
        }
        tally = new Tally(); var registry = Registry(metadata, tally); metadata.fields[0].writable = false; game = Game(codec, compiler, metadata);
        using (var session = new NovelSession(game, options: new() { uiDataSources = registry }))
        {
            session.StartNew(); using var view = new NovelGameView(game, session, static (_, _) => (20, 24));
            view.BuildFrame(); Check(tally.created == 0 && view.dataSourceError.Contains("differs", StringComparison.Ordinal), "Source registration metadata mismatch created an incompatible provider.");
        }
        var frozen = registry.Snapshot(); Reject(() => frozen.Register<Source>(Definition(), _ => throw new InvalidOperationException("Must not instantiate.")));
    }
    internal static void AuthoringAndNeutralSchemas(StoryNodeDataCodec codec, StoryCompiler compiler, SerializationRegistry native)
    {
        var definition = Definition(); var restored = native.Deserialize<UiDataSourceDefinition>(native.Serialize(definition));
        Check(UiDataSourceContract.Fingerprint(restored) == UiDataSourceContract.Fingerprint(definition), "Native typed UI source schema changed its exact contract.");
        var game = Game(codec, compiler, definition); UiDataBindingValidator.Validate(game);
        var page = game.screens.Single(static item => item.role == "reading").screen!;
        Check(UiDataBindingValidator.RowOwner(page, "row-flag", game.uiDataSources)?.id == "items", "Designer row choices mixed unrelated source schemas.");
        Check(UiDataSourceChoices.Values(game.uiDataSources, NovelValueKind.Boolean, true).Single().id == "custom.stats.flag", "Typed menus exposed read-only or incompatible bindings.");
        definition.fields[0].writable = false; game.uiDataSources = [definition]; Reject(() => UiDataBindingValidator.Validate(game));
        var catalog = new UiDataSourceCatalogAsset { definitions = [restored] }; var invalid = UiDataSourceContract.Copy(restored); invalid.collections[0].fields = [];
        Reject(() => UiDataSourceCatalogEditService.Set(catalog, invalid)); Check(catalog.definitions.Single().collections[0].fields.Length == 2, "Rejected UI source edits partially committed.");
        invalid = UiDataSourceContract.Copy(restored); invalid.fields[0].schema.defaultValue = NovelValue.Boolean(true);
        Check(UiDataSourceContract.Fingerprint(invalid) != UiDataSourceContract.Fingerprint(restored), "Different source defaults shared a contract fingerprint.");
    }
    private sealed class Tally { internal int created, disposed; internal bool reverse, duplicate, impure; internal NovelUiDataSourceContext? context; }
    private sealed class Source(NovelUiDataSourceContext context, Tally tally) : UiDataSource, IDisposable
    {
        public override bool TryRead(string path, out NovelValue value)
        {
            if (tally.impure) context.Set("page.flag", NovelValue.Boolean(true));
            value = path == "flag" ? context.Get("page.flag") : NovelValue.String("derived"); return path is "flag" or "label";
        }
        public override bool TryWrite(string path, NovelValue value) { if (path != "flag") return false; context.Set("page.flag", value); return true; }
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => (tally.duplicate ? new[] { "first", "first" } : tally.reverse ? new[] { "second", "first" } : new[] { "first", "second" })
            .Select(key => new UiDataRow(key, new Dictionary<string, NovelValue> { ["value"] = context.Get("page." + key), ["text"] = NovelValue.String(key) })).ToArray();
        public override bool TryWrite(string path, NovelValue value, UiDataRow? row)
        { if (path != "item.value" || row is null) return false; context.Set("page." + row.key, value); return true; }
        public void Dispose() => tally.disposed++;
    }
    private static NovelUiDataSourceRegistry Registry(UiDataSourceDefinition definition, Tally tally)
    {
        var result = new NovelUiDataSourceRegistry(); result.Register(definition, context => { tally.context = context; tally.created++; return new Source(context, tally); }); return result;
    }
    private static UiDataSourceDefinition Definition() => new()
    {
        id = "stats", title = "Statistics", fields = [new() { writable = true, schema = new() { id = "flag", defaultValue = NovelValue.Boolean(false) } },
            new() { schema = new() { id = "label", defaultValue = NovelValue.String("") } }],
        collections = [new() { id = "items", title = "Items", fields = [new() { writable = true, schema = new() { id = "value", defaultValue = NovelValue.Boolean(false) } }, new() { schema = new() { id = "text", defaultValue = NovelValue.String("") } }] }]
    };
    private static NovelProjectAsset Game(StoryNodeDataCodec codec, StoryCompiler compiler, UiDataSourceDefinition definition)
    {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var page = new ScreenAsset { width = 800, height = 650, root = new() { id = "root", kind = UiElementKind.Vertical, layout = new() { width = 800, height = 650 }, children =
            [new() { id = "label", kind = UiElementKind.Text, binding = "custom.stats.label", layout = new() { width = 500, height = 60 } },
                new() { id = "root-flag", kind = UiElementKind.Toggle, binding = "custom.stats.flag", layout = new() { width = 500, height = 60 } },
                new() { id = "rows", kind = UiElementKind.Vertical, repeatBinding = "custom.stats.items", layout = new() { width = 500, height = 300 }, children =
                    [new() { id = "row-flag", kind = UiElementKind.Toggle, binding = "item.value", layout = new() { width = 500, height = 60 } }] }] },
            stateVariables = new[] { "flag", "first", "second" }.Select(static id => new VariableDefinition { id = id, scope = VariableScope.Game, initialValue = NovelValue.Boolean(false) }).ToArray() };
        return new() { gameId = "ui-data", chapters = [chapter], startChapter = chapter.chapterId, uiDataSources = [UiDataSourceContract.Copy(definition)],
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = role == "reading" ? page : ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }
    private static bool Read(NovelSession session, string id) => session.pageState.TryRead("reading", id, out var value) && value.boolean;
    private static void Click(NovelGameView view, string id)
    { var item = view.BuildFrame().items.Single(item => item.id == id); float x = item.bounds.x + 30, y = item.bounds.y + 30; view.HandleInput(new() { x = x, y = y, pointerInside = true, pressed = true, held = true }); view.HandleInput(new() { x = x, y = y, pointerInside = true, released = true }); }
    private static void Reject(Action operation)
    { try { operation(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; } throw new InvalidOperationException("Invalid custom source operation was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
