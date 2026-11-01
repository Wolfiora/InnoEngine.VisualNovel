using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Extensions;
using Inno.VisualNovel.Player.Interface.Actions;
using Inno.VisualNovel.Player.Interface.Data;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Samples.ScriptExtension;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CodeFingerprintContracts
{
    internal static void FrozenRegistrationsAndNativeRestoration(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Game(codec, compiler, false);
        var originalCommands = Commands(false); var changedCommands = Commands(true);
        Check(originalCommands.codeFingerprint != changedCommands.codeFingerprint, "Changing the concrete implementation under the same schema retained its code identity.");
        var actions = new NovelUiActionRegistry(); actions.Register(static () => new SetPageCountAction());
        var sources = new NovelUiDataSourceRegistry(); sources.Register(PageCounterDataSource.schema, static context => new PageCounterDataSource(context));
        var options = new NovelSessionOptions { uiActions = actions, uiDataSources = sources, hostCodeFingerprint = "host-build-a" };
        using var original = new NovelSession(game, commands: originalCommands, options: options); original.StartNew();
        string fingerprint = original.buildFingerprint;
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(original.Capture()));
        using (var same = new NovelSession(game, commands: originalCommands, options: options))
        {
            same.StartNew(); same.Restore(saved);
            Check(same.buildFingerprint == fingerprint && same.story!.nodeId == original.story!.nodeId, "The same published code and frozen registrations did not restore through the native codec.");
        }
        using (var changed = new NovelSession(game, commands: changedCommands, options: options))
        {
            changed.StartNew(); var active = changed.story; Reject(() => changed.Restore(saved));
            Check(changed.story == active && changed.buildFingerprint != fingerprint, "An incompatible implementation replaced the active game during rejected load.");
        }
        using (var changedHost = new NovelSession(game, commands: originalCommands, options: new() { uiActions = actions, uiDataSources = sources, hostCodeFingerprint = "host-build-b" }))
        { changedHost.StartNew(); Reject(() => changedHost.Restore(saved)); }
        var extra = PageCounterDataSource.schema; extra.id = "second-counter";
        sources.Register(extra, static context => new PageCounterDataSource(context));
        using var later = new NovelSession(game, commands: originalCommands, options: options);
        Check(original.buildFingerprint == fingerprint && later.buildFingerprint != fingerprint, "Mutable caller registrations changed an existing session or were omitted from the next session's code identity.");
        int factories = 0;
        Reject(() => sources.Register<UiDataSource>(PageCounterDataSource.schema, context => { factories++; return new PageCounterDataSource(context); }));
        Check(factories == 0, "Data-source code identification invoked a source factory without an actual native context.");
        Check(NovelImplementationFingerprint.TypeCode(typeof(CodeFingerprintContracts)) != NovelImplementationFingerprint.TypeCode(typeof(NovelSession)),
            "Published implementation metadata did not distinguish explicit code modules and types.");
    }

    internal static void ChangedFactoryFailsBeforeExecution(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        int calls = 0; var registry = new NovelCommandRegistry();
        registry.Register(() => calls++ == 0 ? new DelayScoreCommand() : new AlternateDelay());
        using var session = new NovelSession(Game(codec, compiler, true), commands: registry);
        session.StartNew(); session.Advance(); session.Advance();
        Check(session.story!.status == StoryStatus.Faulted && session.story.error.Contains("implementation", StringComparison.OrdinalIgnoreCase),
            "A factory substituted unregistered code under an unchanged schema or escaped the normal source-addressed fault path.");
    }
    private static NovelCommandRegistry Commands(bool alternate)
    {
        var registry = new NovelCommandRegistry(); registry.Register(() => alternate ? new AlternateDelay() : new DelayScoreCommand()); return registry;
    }
    private static NovelProjectAsset Game(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        bool invoke
    ) {
        var draft = FirstStoryTemplate.Create(codec); using var command = new DelayScoreCommand();
        if (invoke)
        {
            var edits = new StoryEditService(codec); var node = edits.Add(draft, "opening", StoryNodeIds.command,
                new CommandNode { definition = command.definition }, new(0, 0));
            edits.Connect(draft, new(new("hello"), new("next")), node); edits.Connect(draft, new(node, new("next")), new("choice"));
            edits.Connect(draft, new(node, new("cancel")), new("end"));
        }
        var chapter = draft.ToAsset(); var result = compiler.Compile(chapter);
        Check(result.success, string.Join("; ", result.diagnostics.Select(static diagnostic => diagnostic.message))); chapter.program = result.program;
        return new() { gameId = "code-contract", buildFingerprint = "same-authored-content", chapters = [chapter], startChapter = chapter.chapterId, commandDefinitions = [command.definition] };
    }
    private sealed class AlternateDelay : NovelCommand
    {
        private readonly DelayScoreCommand m_inner = new();
        public override NovelCommandDefinition definition => m_inner.definition;
        public override bool Execute(NovelCommandContext context, NovelCommandState state, double seconds) => m_inner.Execute(context, state, seconds);
        public override void PrepareRestore(NovelCommandState state) => m_inner.PrepareRestore(state);
        public override void Cancel(NovelCommandState state) => m_inner.Cancel(state);
        public override void Dispose() => m_inner.Dispose();
    }
    private static void Reject(Action operation)
    {
        try { operation(); } catch (InvalidOperationException) { return; } catch (ArgumentException) { return; }
        throw new InvalidOperationException("An incompatible code candidate was accepted.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
