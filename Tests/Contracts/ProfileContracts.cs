using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inno.Core.Serialization;
using Inno.Storage;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ProfileContracts
{
    internal static void RestoreAutosaveAndFailure(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Project(codec, compiler); var storage = new ProfileStorage(); var nativeCodec = new CountingCodec(new EditorNovelStateCodec(native));
        var store = new GlobalProgressStore(game.gameId, storage, nativeCodec);
        using (var session = new NovelSession(game))
        using (var profile = new PlayerProfileController(session, store))
        {
            Poll(profile); Check(profile.ready && profile.error.Length == 0, "A first-run profile did not become ready.");
            session.preferences.allowFlashing = false; session.preferences.textSpeed = 2;
            session.StartNew(); session.story!.SetVariable("shared", NovelValue.Boolean(true)); session.Advance(); session.Tick(0);
            profile.Poll(1); Poll(profile);
            Check(storage.packets.Count == 2 && storage.packets.Keys.All(static key => !key.value.Contains("manual", StringComparison.Ordinal)), "Profile writes were not independent of save slots.");
            var before = storage.packets.ToDictionary(static item => item.Key, static item => item.Value.ToArray());
            storage.failWrites = true; session.preferences.fontScale = 1.5; profile.Poll(1); Poll(profile);
            Check(profile.ready && profile.error.Length > 0 && before.All(pair => storage.packets[pair.Key].AsSpan().SequenceEqual(pair.Value)), "A failed atomic profile write damaged a valid profile or blocked gameplay.");
            int writes = storage.writes; profile.Poll(10); Check(storage.writes == writes, "A failed profile write retried continuously without player intent.");
            storage.failWrites = false; profile.Retry(); profile.Poll(); Poll(profile); Check(profile.error.Length == 0, "Explicit profile retry did not recover.");
        }
        using var restored = new NovelSession(game); using var loaded = new PlayerProfileController(restored, store); Poll(loaded);
        Check(loaded.ready && !restored.preferences.allowFlashing && restored.preferences.textSpeed == 2 && restored.preferences.fontScale == 1.5
            && restored.CaptureProgress().variables.Single().value.boolean && restored.CaptureProgress().readContent.Length == 1, "Global progress and preferences did not survive independent native persistence.");
        Check(nativeCodec.ownerViolations == 0, "Profile serialization ran outside its owner thread.");
    }

    internal static void StartupGateAndLateRead(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Project(codec, compiler); var storage = new ProfileStorage(); var nativeCodec = new CountingCodec(new EditorNovelStateCodec(native));
        var store = new GlobalProgressStore(game.gameId, storage, nativeCodec);
        storage.packets[new(Root(game.gameId) + "/progress")] = native.Serialize(new GlobalProgressState { gameId = game.gameId, variables = [new() { id = "shared", value = NovelValue.Boolean(true) }] });
        storage.packets[new(Root(game.gameId) + "/preferences")] = native.Serialize(new PlayerPreferencesState { gameId = "foreign" });
        using (var session = new NovelSession(game))
        using (var view = new NovelGameView(game, session, static (_, _) => (0, 20), profileStore: store))
        {
            view.Tick(0); var frame = view.BuildFrame();
            Check(!view.profile!.ready && view.profile.error.Length > 0 && !session.CaptureProgress().variables.Single().value.boolean
                && frame.items.Any(static item => item.id == "profile-error"), "Corrupt preferences partially applied global progress or hid the loading failure.");
            view.HandleInput(new() { confirm = true }); Check(session.story is null, "A loading failure allowed a title action to start a game.");
            view.Tick(0); view.profile.UseDefaults(); view.Tick(1); Poll(view.profile);
            Check(view.profile.ready && session.story is null, "Explicit defaults accidentally advanced narrative.");
        }
        var delayed = new ProfileStorage { delayReads = true }; var retiringCodec = new CountingCodec(new EditorNovelStateCodec(native));
        using var candidate = new NovelSession(game); var retiring = new PlayerProfileController(candidate, new(game.gameId, delayed, retiringCodec));
        retiring.Dispose(); retiringCodec.retired = true;
        delayed.read.TrySetResult(native.Serialize(new GlobalProgressState { gameId = game.gameId }));
        Thread.Sleep(20);
        Check(retiringCodec.decodeCalls == 0 && retiringCodec.retiredCalls == 0 && candidate.story is null, "A late profile read invoked a retired codec or changed the game.");
    }

    private static NovelProjectAsset Project(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.variables = [.. chapter.variables, new() { id = "shared", scope = VariableScope.Global, initialValue = NovelValue.Boolean(false) }];
        chapter.program = compiler.Compile(chapter).program!;
        return new() { gameId = "profile-contract", chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "loading", "title", "reading", "pause", "settings", "history", "credits" }.Select(static role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }
    private static void Poll(PlayerProfileController profile)
    {
        for (int index = 0; index < 500; index++) { profile.Poll(); if (!profile.loading && !profile.saving) return; Thread.Sleep(1); }
        throw new InvalidOperationException("Profile IO exceeded its deterministic test deadline.");
    }
    private static string Root(string game) => "visualnovel/" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(game))).ToLowerInvariant();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class CountingCodec(INovelStateCodec inner) : INovelStateCodec
    {
        private readonly int m_owner = Environment.CurrentManagedThreadId;
        internal bool retired; internal int retiredCalls; internal int decodeCalls; internal int ownerViolations;
        public byte[] Encode<T>(T value) where T : class, ISerializable { CheckOwner(); return inner.Encode(value); }
        public T Decode<T>(ReadOnlySpan<byte> bytes) where T : class, ISerializable { CheckOwner(); decodeCalls++; return inner.Decode<T>(bytes); }
        private void CheckOwner() { if (retired) { retiredCalls++; throw new InvalidOperationException("Native codec is retired."); } if (Environment.CurrentManagedThreadId != m_owner) ownerViolations++; }
    }
    private sealed class ProfileStorage : IApplicationStorage
    {
        internal readonly Dictionary<StorageKey, byte[]> packets = [];
        internal bool failWrites; internal bool delayReads; internal int writes;
        internal readonly TaskCompletionSource<byte[]?> read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<bool> ExistsAsync(StorageKey key, CancellationToken cancellationToken = default) => ValueTask.FromResult(packets.ContainsKey(key));
        public ValueTask<byte[]?> ReadAsync(StorageKey key, CancellationToken cancellationToken = default) => delayReads ? new(read.Task) : ValueTask.FromResult(packets.TryGetValue(key, out var bytes) ? bytes.ToArray() : null);
        public ValueTask WriteAsync(StorageKey key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); writes++;
            if (failWrites) return ValueTask.FromException(new System.IO.IOException("Injected profile precommit failure."));
            packets[key] = value.ToArray(); return ValueTask.CompletedTask;
        }
        public ValueTask<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default) => ValueTask.FromResult(packets.Remove(key));
        public ValueTask<IReadOnlyList<StorageKey>> ListAsync(StorageKey? prefix = null, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<StorageKey>>(packets.Keys.ToArray());
    }
}
