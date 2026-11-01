using System;
using System.Linq;
using System.Threading;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CheckpointContracts
{
    internal static void NamedBoundariesAndBarriers(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        StoryDraftState draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var checkpoint = edits.Add(draft, "opening", StoryNodeIds.system, new SystemNode { operation = SystemOperation.Checkpoint, argument = "decision" }, new());
        edits.Insert(draft, draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "hello").id, checkpoint);
        var chapter = Compile(draft, compiler);
        using (var session = new NovelSession(Game(chapter)))
        {
            session.StartNew(); session.Advance(); session.Advance();
            Check(session.story!.status == StoryStatus.Suspended && session.namedCheckpoints.SequenceEqual(new[] { "decision" }), "Named checkpoint did not stop at a stable post-action cursor.");
            var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
            Check(new NovelSessionDataSource(session).ReadRows("checkpoints").Single().key == "decision", "The visual checkpoint collection omitted its retained stable name.");
            session.Tick(0); session.Choose("sea");
            Check(session.RollbackToCheckpoint("decision") && session.story!.status == StoryStatus.Suspended && session.story.nodeId == "choice", "Named restore moved a pointer without restoring the exact logical boundary.");
            session.Tick(0); Check(session.Rollback() && session.story!.nodeId == "hello" && session.namedCheckpoints.Count == 0,
                "Ordinary rollback became trapped on a named pre-line boundary or retained its abandoned future.");
            session.Restore(saved); session.Tick(0);
            Check(session.story!.status == StoryStatus.Choice && !session.RollbackToCheckpoint("decision"), "Loading repeated the checkpoint effect or retained a pre-load rollback journal.");
        }
        var barrier = edits.Add(draft, "opening", StoryNodeIds.system, new SystemNode { operation = SystemOperation.RollbackBarrier, argument = "已确认不可撤销的分支" }, new());
        edits.Insert(draft, draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "choice" && edge.output.portId.value == "sea").id, barrier);
        using var locked = new NovelSession(Game(Compile(draft, compiler))); locked.StartNew(); locked.Advance(); locked.Advance(); locked.Tick(0); locked.Choose("sea");
        Check(!locked.canRollback && !locked.RollbackToCheckpoint("decision") && locked.namedCheckpoints.Count == 0 && locked.story!.rollbackBarrierReason == "已确认不可撤销的分支",
            "An explicit barrier allowed rollback through an older interactive or named checkpoint.");
        var packet = native.Deserialize<NovelSessionState>(native.Serialize(locked.Capture())); locked.StartNew(); locked.Restore(packet);
        var source = new NovelSessionDataSource(locked);
        Check(locked.story!.rollbackEpoch == packet.narrative.rollbackEpoch && source.TryRead("history.barrierReason", out var reason) && reason.text == "已确认不可撤销的分支",
            "Native restore lost the current barrier explanation or epoch.");
    }

    internal static void ExplicitSavesAndPreviewIsolation(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var automatic = edits.Add(draft, "opening", StoryNodeIds.system, new SystemNode { operation = SystemOperation.AutoSave, argument = string.Empty }, new());
        edits.Insert(draft, draft.graph.document.edges.Single(static edge => edge.output.nodeId.value == "entry").id, automatic);
        var game = Game(Compile(draft, compiler)); game.autoSaveOnChoices = false;
        using var first = new NovelPreviewSession(game, draft, compiler, new(), static (_, _) => (100, 40), stateCodec: new EditorNovelStateCodec(native));
        var slots = first.view.persistence ?? throw new InvalidOperationException("Editor-native preview persistence was not attached.");
        Check(first.session.story!.status == StoryStatus.Suspended && first.session.automaticSaveSequence == 1, "Explicit automatic save did not freeze its immediate continuation.");
        first.StepTime(0); Poll(slots);
        var saved = slots.slots.Single();
        Check(saved.slot.kind == SaveSlotKind.Automatic && first.session.story.status == StoryStatus.Dialogue, "Explicit saving was disabled with the optional choice policy or failed to resume after capture.");
        Check(slots.Load(saved.slot), "The exact automatic packet was not loadable."); Poll(slots); first.view.BuildFrame();
        Check(first.session.story!.status == StoryStatus.Suspended && first.session.automaticSaveSequence == 0, "Loaded automatic state pointed at the saving instruction and replayed its side effect.");
        first.StepTime(0); Poll(slots); Check(slots.slots.Count == 1 && first.session.story!.nodeId == "hello", "Load or resume generated a duplicate automatic packet.");
        Check(slots.Save(new(SaveSlotKind.Manual, 3)), "Preview manual saving was unavailable."); Poll(slots);
        using var second = new NovelPreviewSession(game, draft, compiler, new(), static (_, _) => (100, 40), stateCodec: new EditorNovelStateCodec(native));
        Poll(second.view.persistence!);
        Check(!second.view.persistence!.slots.Any(static slot => slot.slot.kind == SaveSlotKind.Manual), "Independent previews shared player packets or another preview's manual saves.");
        first.Dispose(); second.StepTime(0);
        Check(second.session.story!.status == StoryStatus.Dialogue, "Disposing one preview retired another preview's storage or native continuation.");
        using var missing = new NovelPreviewSession(game, draft, compiler, new(), static (_, _) => (100, 40));
        Check(missing.session.story!.status == StoryStatus.Faulted && missing.session.story.error.Contains(automatic.value, StringComparison.Ordinal)
            && missing.session.story.error.Contains("native persistence", StringComparison.Ordinal), "Explicit autosave silently succeeded without a native persistence owner.");
    }
    private static StoryAsset Compile(
        StoryDraftState draft,
        StoryCompiler compiler
    ) {
        StoryAsset chapter = draft.ToAsset(); var result = compiler.Compile(chapter);
        Check(result.success, string.Join("; ", result.diagnostics.Select(static issue => issue.message))); chapter.program = result.program; return chapter;
    }
    private static NovelProjectAsset Game(StoryAsset chapter)
    {
        var font = new FontAsset(); return new() { chapters = [chapter], startChapter = chapter.chapterId,
            screens = ShellThemeTemplates.roles.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, font, "Clear") }).ToArray() };
    }
    private static void Poll(SaveSlotController slots)
    {
        for (int count = 0; count < 1000 && (slots.busy || slots.automaticPending); count++) { slots.Poll(); if (slots.busy) Thread.Sleep(1); }
        Check(!slots.busy && !slots.automaticPending && slots.error.Length == 0, "Preview persistence did not settle: " + slots.error);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
