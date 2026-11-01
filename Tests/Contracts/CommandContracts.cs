using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Extensions;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CommandContracts
{
    internal static void SaveRestoreAndCancellation(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var owner = new Owner(); var registry = new NovelCommandRegistry(); registry.Register(() => new Delay(owner, Safe));
        var game = Game(codec, compiler, Definition(Safe));
        using var session = new NovelSession(game, commands: registry); Enter(session);
        Check(session.story!.status == StoryStatus.Command && session.canSave && session.story.canCancelCommand, "Typed command did not enter a savable explicit wait.");
        session.Tick(0.4); session.story.SetVariable("duration", NovelValue.Number(100));
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        var bad = native.Deserialize<NovelSessionState>(native.Serialize(saved)); bad.narrative.commands[0].contract = "invalid";
        var before = session.story; int cancelled = owner.cancelled;
        bool rejected = false; try { session.Restore(bad); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && session.story == before && owner.cancelled == cancelled, "Rejected command restoration cancelled or replaced the active game.");
        session.Restore(saved); Check(owner.restored == 1, "Command did not prepare a neutral restored continuation.");
        session.Tick(1.6);
        Check(session.story!.status == StoryStatus.Choice && session.story.GetVariable("result").integer == 7, "Restored arguments were re-evaluated against changed variables or result did not reach its declared destination.");
        session.StartNew(); EnterFromDialogue(session); session.Tick(2);
        Check(session.Rollback() && session.story.GetVariable("result").integer == 0, "Pure command result was not rolled back with game state.");
        EnterFromDialogue(session); NovelCommandContext escaped = owner.context!;
        Check(session.CancelCommand() && session.story.status == StoryStatus.Completed, "Cancellation did not follow the explicit cancel output.");
        Check(owner.cancelled > cancelled && session.story.GetVariable("result").integer == 0, "Cancelled command wrote a result or leaked external work.");
        bool expired = false; try { escaped.Argument("delay"); } catch (InvalidOperationException) { expired = true; }
        Check(expired, "An escaped command context remained usable after its owner-thread step.");
        var missing = new NovelSession(game); Enter(missing);
        Check(missing.story!.status == StoryStatus.Faulted && missing.story.error.Contains("contract.delay", StringComparison.Ordinal), "Missing implementation did not retain a source-addressed diagnostic."); missing.Dispose();
    }
    internal static void SideEffectPolicies(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var owner = new Owner(); var registry = new NovelCommandRegistry(); registry.Register(() => new Delay(owner, CommandCapabilities.Cancel | CommandCapabilities.ExternalEffects));
        var game = Game(codec, compiler, Definition(CommandCapabilities.Cancel | CommandCapabilities.ExternalEffects));
        game.localization = new() { languages = [new(), new() { id = "en", name = "English" }] };
        using (var session = new NovelSession(game, commands: registry))
        {
            session.StartNew(); session.preferences.skipReadOnly = false; session.ToggleSkipping(); EnterFromDialogue(session);
            Check(!session.canSave && !session.skipping, "Unsafe active command exposed saving or allowed skipping across its barrier.");
            bool denied = false; try { session.Capture(); } catch (InvalidOperationException) { denied = true; }
            var machine = session.story; int executions = owner.executed;
            session.SetLanguage("en");
            Check(denied && session.story == machine && owner.executed == executions, "Language change captured or recreated unsavable external work.");
            session.Tick(2); Check(!session.canRollback && session.story!.status == StoryStatus.Choice, "External command did not establish a rollback barrier.");
        }
        int before = owner.executed;
        using (var preview = new NovelSession(game, commands: registry, preview: true))
        {
            Enter(preview);
            Check(preview.story!.status == StoryStatus.Faulted && owner.executed == before, "Preview executed prohibited external side effects.");
        }
        var schema = Definition(Safe); var copy = CommandContract.Copy(schema); copy.parameters[0].defaultValue = NovelValue.Number(3);
        Check(CommandContract.Fingerprint(schema) != CommandContract.Fingerprint(copy), "Command contract comparison ignored typed defaults.");
    }
    internal static NovelCommandDefinition Definition(CommandCapabilities capabilities) => new()
    {
        id = "contract.delay", title = "延时返回结果", capabilities = capabilities, hasResult = true, resultDefault = NovelValue.Integer(0),
        parameters = [new() { id = "delay", label = "等待", defaultValue = NovelValue.Number(2) }, new() { id = "value", label = "结果", defaultValue = NovelValue.Integer(7) }]
    };
    private const CommandCapabilities Safe = CommandCapabilities.Save | CommandCapabilities.Preview | CommandCapabilities.Rollback | CommandCapabilities.Cancel;
    private static NovelProjectAsset Game(StoryNodeDataCodec codec, StoryCompiler compiler, NovelCommandDefinition definition)
    {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        draft.variables = [.. draft.variables, new() { id = "result", initialValue = NovelValue.Integer(0) }, new() { id = "duration", initialValue = NovelValue.Number(2) }];
        var command = edits.Add(draft, "opening", StoryNodeIds.command, new CommandNode { definition = CommandContract.Copy(definition), resultVariable = "result", arguments = [new() { id = "delay", value = new() { operation = ExpressionOperation.Variable, variableId = "duration" } }] }, new(0, 0));
        edits.Connect(draft, new(new("hello"), new("next")), command); edits.Connect(draft, new(command, new("next")), new("choice"));
        if (definition.capabilities.HasFlag(CommandCapabilities.Cancel)) edits.Connect(draft, new(command, new("cancel")), new("end"));
        var chapter = draft.ToAsset(); var compiled = compiler.Compile(chapter);
        Check(compiled.success, string.Join("; ", compiled.diagnostics.Select(static item => item.message))); chapter.program = compiled.program;
        return new() { chapters = [chapter], startChapter = chapter.chapterId, commandDefinitions = [definition] };
    }
    private static void Enter(NovelSession session) { session.StartNew(); EnterFromDialogue(session); }
    private static void EnterFromDialogue(NovelSession session) { session.Advance(); session.Advance(); }
    private sealed class Owner
    {
        internal int cancelled, restored, executed;
        internal NovelCommandContext? context;
    }
    private sealed class Delay(Owner owner, CommandCapabilities capabilities) : NovelCommand
    {
        public override NovelCommandDefinition definition => Definition(capabilities);
        public override bool Execute(NovelCommandContext context, NovelCommandState state, double seconds)
        {
            owner.context = context; owner.executed++;
            if (state.elapsed < context.Argument("delay").number) return false;
            context.Return(context.Argument("value")); return true;
        }
        public override void PrepareRestore(NovelCommandState state)
        { if (!state.started || state.elapsed < 0) throw new InvalidOperationException("Delay continuation is invalid."); owner.restored++; }
        public override void Cancel(NovelCommandState state) { owner.cancelled++; }
        public override void Dispose() { owner.context = null; }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
