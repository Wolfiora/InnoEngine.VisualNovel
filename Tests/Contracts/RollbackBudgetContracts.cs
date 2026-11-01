using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class RollbackBudgetContracts
{
    internal static void NativeRetentionAndDiscardedFutures(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Game(codec, compiler); var stateCodec = new Codec(native);
        using var session = new NovelSession(game, options: new() { checkpointCodec = stateCodec }); session.StartNew();
        long first = stateCodec.lastLength, budget = first * 5;
        Check(first > 0 && session.rollbackStoredBytes == first, "Native rollback did not account its actual encoded payload.");
        session.SetRollbackBudget(budget); session.Advance(); session.Advance(); session.Choose("sea");
        for (int index = 0; index < 25; index++)
        {
            session.story!.SetVariable("player", NovelValue.String("value-" + index)); session.Advance(); session.Advance();
            Check(session.rollbackStoredBytes is > 0 && session.rollbackStoredBytes <= budget, "Retained native rollback buffers exceeded the configured budget.");
        }
        Check(!session.CanRollbackToHistory(0) && session.canRollback, "Budget/count eviction did not distinguish expired backlog from retained checkpoints.");
        int abandoned = session.historyCount - 1, reads = stateCodec.decodes;
        Check(session.Rollback() && stateCodec.decodes == reads + 1 && session.story!.GetVariable("player").text == "value-23",
            "Rollback did not decode its native snapshot or restored the latest live variable instead of the retained boundary.");
        Check(!session.CanRollbackToHistory(abandoned) && session.historyCount == abandoned, "Native rollback retained its abandoned future.");
        long retained = session.rollbackStoredBytes!.Value; Reject(() => session.SetRollbackBudget(-1)); Reject(() => session.SetRollbackBudget(1073741825));
        Check(session.rollbackStoredBytes == retained && session.rollbackByteBudget == budget, "Invalid retention policy partially evicted the journal.");
        session.SetRollbackBudget(0);
        Check(session.rollbackStoredBytes == 0 && !session.canRollback && session.namedCheckpoints.Count == 0, "Disabling rollback retained native buffers or exposed unavailable boundaries.");
        session.SetRollbackBudget(budget); Check(!session.canRollback, "Increasing a budget fabricated previously evicted history.");
        session.Advance(); session.Advance(); session.Advance(); session.Advance(); Check(session.canRollback, "Future interactions failed to create new bounded checkpoints after enabling retention.");
        session.Dispose(); Check(session.rollbackStoredBytes == 0 && !session.canRollback, "Disposing a session retained its native journal payloads.");
    }
    internal static void OversizeCodecFailuresAndUnknownHost(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Game(codec, compiler); var stateCodec = new Codec(native);
        using var session = new NovelSession(game, options: new() { checkpointCodec = stateCodec }); session.StartNew();
        long budget = stateCodec.lastLength * 5; session.SetRollbackBudget(budget); session.Advance(); session.Advance();
        session.story!.SetVariable("player", NovelValue.String(new string('大', 200000))); session.Choose("sea");
        Check(stateCodec.lastLength > budget && session.rollbackStoredBytes <= budget && session.rollbackDiagnostic.Length > 0 && session.canRollback,
            "An oversized snapshot exceeded retention or hid all earlier valid boundaries.");
        Check(session.Rollback() && session.story!.status == StoryStatus.Choice && session.story.GetVariable("player").text == "旅人",
            "Omitting a large current boundary prevented recovery to an older small native checkpoint.");
        stateCodec.failEncode = true; session.Choose("forest");
        Check(session.story!.status == StoryStatus.Dialogue && session.rollbackDiagnostic.Contains("编码失败", StringComparison.Ordinal) && session.canRollback,
            "An encoder failure stopped the game or presented an unavailable checkpoint as retained.");
        byte[] before = native.Serialize(session.Capture()); long bytes = session.rollbackStoredBytes!.Value; stateCodec.failDecode = true;
        Reject(() => session.Rollback());
        Check(native.Serialize(session.Capture()).SequenceEqual(before) && session.rollbackStoredBytes == bytes, "A failed native checkpoint read partially replaced the active game.");
        stateCodec.failDecode = stateCodec.failEncode = false; Check(session.Rollback(), "A failed decode discarded the last valid rollback candidate.");
        using var unconfigured = new NovelSession(game); unconfigured.StartNew(); unconfigured.Advance(); unconfigured.Advance();
        Check(unconfigured.rollbackStoredBytes is null && unconfigured.canRollback, "A codec-less host fabricated byte accounting or lost its existing count-bounded rollback.");
        unconfigured.SetRollbackBudget(0); Check(!unconfigured.canRollback, "An explicit disable was ignored by the count-only fallback.");
        Reject(() => new NovelSession(game, options: new() { rollbackByteBudget = -1 }));
    }
    private static NovelProjectAsset Game(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); new StoryEditService(codec).Connect(draft, new(new("sea"), new("next")), new("sea"));
        var chapter = draft.ToAsset(); var result = compiler.Compile(chapter);
        Check(result.success, string.Join("; ", result.diagnostics.Select(static item => item.message))); chapter.program = result.program;
        return new() { chapters = [chapter], startChapter = chapter.chapterId, rollbackLimit = 4 };
    }
    private sealed class Codec(SerializationRegistry native) : INovelStateCodec
    {
        internal int decodes, lastLength;
        internal bool failEncode, failDecode;
        public byte[] Encode<T>(T value) where T : class, ISerializable
        {
            if (failEncode) throw new InvalidOperationException("Injected native encoder failure.");
            byte[] bytes = native.Serialize(value); lastLength = bytes.Length; return bytes;
        }
        public T Decode<T>(ReadOnlySpan<byte> bytes) where T : class, ISerializable
        {
            if (failDecode) throw new InvalidOperationException("Injected native decoder failure.");
            decodes++; return native.Deserialize<T>(bytes);
        }
    }
    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid rollback policy or failed codec was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
