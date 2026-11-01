using System;
using System.Collections.Generic;
using System.IO;
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
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PersistenceContracts
{
    internal static void ContinueFromTitle(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var project = Project(codec, compiler);
        project.screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(static role => new NovelScreenBinding
            { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray();
        var storage = new MemoryStorage();
        var nativeCodec = new EditorNovelStateCodec(native);
        using var source = new NovelSession(project); source.StartNew();
        var service = new SaveService(storage, nativeCodec, source.gameId, source.buildFingerprint);
        service.SaveAsync(new(SaveSlotKind.Manual, 0), source).AsTask().GetAwaiter().GetResult();
        source.Advance(); source.Advance();
        service.SaveAsync(new(SaveSlotKind.Automatic, 0), source).AsTask().GetAwaiter().GetResult();
        service.SaveAsync(new(SaveSlotKind.Manual, 1), source).AsTask().GetAwaiter().GetResult();
        var wrong = source.Capture(); wrong.buildFingerprint = "other-build";
        new SaveService(storage, nativeCodec, source.gameId, "other-build").SaveSnapshotAsync(new(SaveSlotKind.Quick, 0), wrong).AsTask().GetAwaiter().GetResult();
        foreach (var key in storage.ListAsync().Result)
        {
            if (key.value.EndsWith("/manual/1", StringComparison.Ordinal)) { storage.WriteAsync(key, new byte[] { 1, 2, 3 }).GetAwaiter().GetResult(); continue; }
            var packet = native.Deserialize<SaveEnvelope>(storage.ReadAsync(key).Result!);
            packet.utcTicks = key.value.EndsWith("/automatic/0", StringComparison.Ordinal) ? 200 : key.value.EndsWith("/quick/0", StringComparison.Ordinal) ? 300 : 100;
            storage.WriteAsync(key, native.Serialize(packet)).GetAwaiter().GetResult();
        }
        using var target = new NovelSession(project);
        using var view = new NovelGameView(project, target, static (_, text) => (text.Length * 10, 28), service);
        Poll(view.persistence!);
        Check(view.persistence!.canContinue, "Existing compatible saves did not enable title continuation.");
        var button = view.BuildFrame().items.Single(static item => item.id == "continue");
        float x = button.bounds.x + 30, y = button.bounds.y + 30;
        view.HandleInput(new() { x = x, y = y, pointerInside = true, pressed = true, held = true });
        view.HandleInput(new() { x = x, y = y, pointerInside = true, released = true }); Poll(view.persistence);
        Check(target.page == "reading" && target.story?.status == StoryStatus.Choice, "Title continuation did not restore the newest compatible packet or loaded a newer incompatible/corrupt slot.");
        using var empty = new SaveSlotController(target, new SaveService(new MemoryStorage(), nativeCodec, target.gameId, target.buildFingerprint));
        empty.Refresh(); Poll(empty); Check(!empty.canContinue && !empty.ContinueLatest(), "Empty storage advertised a valid continuation.");
    }
    internal static void ProjectCapacity(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var project = Project(codec, compiler); project.manualSaveSlots = 23; project.automaticSaveSlots = 5;
        var copied = NovelProjectCopy.Copy(project);
        Check(copied.manualSaveSlots == 23 && copied.automaticSaveSlots == 5, "Game definition copying lost configured slot capacities.");
        var fallback = new Inno.VisualNovel.Player.Binding.SavePageDataSource(null, copied.manualSaveSlots, copied.automaticSaveSlots);
        fallback.Move(999);
        Check(fallback.ReadRows("saves.slots").Count == 3 && fallback.TryRead("saves.page", out var page) && page.text == "3 / 3", "Unavailable persistence displayed hard-coded manual slot capacity or an empty final page.");
        fallback.Select(SaveSlotKind.Automatic); Check(fallback.ReadRows("saves.slots").Count == 5, "Automatic category ignored its configured independent capacity.");
        fallback.Select(SaveSlotKind.Quick); Check(fallback.ReadRows("saves.slots").Count == 1, "Changing other capacities enlarged or merged the dedicated quick slot.");
        using var session = new NovelSession(copied); session.StartNew();
        var service = new SaveService(new MemoryStorage(), new EditorNovelStateCodec(native), session.gameId, session.buildFingerprint, copied.manualSaveSlots, copied.automaticSaveSlots);
        service.SaveAsync(new(SaveSlotKind.Manual, 22), session).AsTask().GetAwaiter().GetResult();
        service.SaveAsync(new(SaveSlotKind.Automatic, 4), session).AsTask().GetAwaiter().GetResult();
        bool rejected = false;
        try { service.SaveAsync(new(SaveSlotKind.Manual, 23), session).AsTask().GetAwaiter().GetResult(); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Configured manual bounds allowed an out-of-range write.");
        using var controller = new SaveSlotController(session, service);
        Check(controller.manualSlotCount == 23 && controller.automaticSlotCount == 5, "The actual controller disagreed with native save-service configuration.");
    }
    internal static void ExactThumbnails(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var project = Project(codec, compiler); var storage = new MemoryStorage();
        using var session = new NovelSession(project); session.StartNew();
        var service = new SaveService(storage, new EditorNovelStateCodec(native), session.gameId, session.buildFingerprint);
        var capture = new DeferredThumbnail(); using var slots = new SaveSlotController(session, service, capture);
        byte[] png = Inno.VisualNovel.UI.Imaging.UiPngImage.Encode(new(1, 1, new byte[] { 32, 64, 128, 255 }));
        Check(slots.Save(new(SaveSlotKind.Manual, 0)) && slots.busy && capture.boundary!.narrative.status == StoryStatus.Dialogue, "Save did not freeze narrative before requesting its image.");
        session.Advance(); session.Advance();
        Check(session.story!.status == StoryStatus.Choice && storage.ListAsync().Result.Count == 0, "An incomplete screenshot published a partial save packet.");
        capture.completion.SetResult(png); Poll(slots);
        var saved = service.ReadAsync(new(SaveSlotKind.Manual, 0)).AsTask().GetAwaiter().GetResult();
        Check(saved.narrative.status == StoryStatus.Dialogue && slots.slots.Single().thumbnail.AsSpan().SequenceEqual(png), "Thumbnail completion captured a later narrative state.");
        var data = new Inno.VisualNovel.Player.Binding.SavePageDataSource(slots); var row = data.ReadRows("saves.slots")[0];
        var bitmap = data.ReadBitmap("item.thumbnail", row);
        Check(bitmap is { width: 1, height: 1 } && ReferenceEquals(bitmap, data.ReadBitmap("item.thumbnail", row)), "Stored image did not enter the actual cached save-page binding.");
        capture.Reset(); Check(slots.Save(new(SaveSlotKind.Manual, 0)), "Replacement save did not start.");
        capture.completion.SetException(new IOException("Injected screenshot failure.")); Poll(slots);
        Check(slots.error.Length > 0 && slots.slots.Single().thumbnail.AsSpan().SequenceEqual(png), "A screenshot failure overwrote the previous complete packet.");
        capture.Reset(); Check(slots.Save(new(SaveSlotKind.Manual, 1)), "Delayed save did not start."); session.StartNew(); slots.Poll();
        capture.completion.TrySetResult(png);
        Check(!slots.busy && slots.error.Contains("discarded", StringComparison.Ordinal) && slots.slots.Count == 1, "A delayed image saved into a replaced game.");
    }

    private sealed class DeferredThumbnail : Inno.VisualNovel.Player.Ports.INovelSaveThumbnail
    {
        internal NovelSessionState? boundary;
        internal TaskCompletionSource<byte[]> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Reset() { boundary = null; completion = new(TaskCreationOptions.RunContinuationsAsynchronously); }
        public ValueTask<byte[]> CaptureAsync(NovelSessionState state, CancellationToken cancellationToken = default) { boundary = state; return new(completion.Task); }
    }

    internal static void AutomaticBoundaries(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var disk = new MemoryStorage(); var storage = new FailingStorage(disk); var project = Project(codec, compiler);
        using var session = new NovelSession(project); session.StartNew();
        var service = new SaveService(storage, new EditorNovelStateCodec(native), session.gameId, session.buildFingerprint);
        using var slots = new SaveSlotController(session, service); var policy = new AutomaticSaveController(session, slots, true);
        Check(slots.Refresh(), "Initial automatic metadata refresh did not start.");
        session.Advance(); session.Advance(); policy.Observe();
        Check(slots.automaticPending, "Entering a choice during existing IO did not retain an automatic boundary.");
        session.Choose("sea"); Poll(slots);
        var automatic = slots.slots.Single(); var saved = service.ReadAsync(automatic.slot).AsTask().GetAwaiter().GetResult();
        Check(automatic.slot.kind == SaveSlotKind.Automatic && saved.narrative.status == StoryStatus.Choice && session.story!.status == StoryStatus.Dialogue,
            "Deferred automatic IO captured a later game state or replaced the active game.");
        policy.Observe(); Check(!slots.busy && !slots.automaticPending, "Ordinary reading repeated the automatic request.");
        Check(slots.Load(automatic.slot), "Automatic slot could not be loaded."); Poll(slots); policy.Observe();
        Check(!slots.busy && !slots.automaticPending && session.lastReplacement == SessionReplacement.Load, "Loading a choice immediately wrote a new automatic save.");
        for (int iteration = 0; iteration < 4; iteration++)
        { session.StartNew(); session.Advance(); session.Advance(); policy.Observe(); Poll(slots); }
        Check(slots.slots.Count == 3 && slots.slots.All(static item => item.slot.kind == SaveSlotKind.Automatic), "Automatic rotation exceeded its independent three-slot capacity.");
        Check(slots.Save(new(SaveSlotKind.Manual, 0)), "Manual slot became unavailable after automatic saves."); Poll(slots);
        storage.failWrites = true; session.StartNew(); session.Advance(); session.Advance(); policy.Observe(); Poll(slots);
        Check(slots.error.Length > 0 && slots.slots.Count == 4 && session.story!.status == StoryStatus.Choice, "Automatic write failure damaged valid slots or the running decision.");
        storage.failWrites = false; slots.Refresh(); Check(slots.RequestAutomatic(), "Explicit automatic request was rejected during refresh.");
        session.StartNew(); Poll(slots);
        Check(!slots.automaticPending && session.story!.status == StoryStatus.Dialogue, "An old queued boundary survived game replacement.");
    }

    internal static void OwnerThreadSlots(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var disk = new MemoryStorage(); var storage = new FailingStorage(disk);
        var project = Project(codec, compiler);
        using var session = new NovelSession(project); session.StartNew();
        var service = new SaveService(storage, new EditorNovelStateCodec(native), session.gameId, session.buildFingerprint);
        using var controller = new SaveSlotController(session, service);
        var slot = new SaveSlot(SaveSlotKind.Manual, 0);
        Check(controller.Save(slot) && controller.busy && !controller.Save(slot), "An in-flight operation was not serialized.");
        Poll(controller);
        Check(controller.slots.Count == 1 && controller.error.Length == 0, "A durable save did not publish slot metadata.");
        session.Advance(); session.Advance();
        Check(controller.Load(slot) && session.story!.status == StoryStatus.Choice, "A load continuation replaced the game outside owner-thread polling.");
        Poll(controller);
        Check(session.story!.status == StoryStatus.Dialogue, "Owner-thread commit did not restore the candidate.");
        Check(controller.Load(slot), "Second load did not start."); session.StartNew();
        StoryMachine replacement = session.story!; Poll(controller);
        Check(ReferenceEquals(session.story, replacement) && controller.error.Contains("discarded", StringComparison.Ordinal), "A delayed load replaced a newer game.");
        storage.failWrites = true; Check(controller.Save(slot), "Failure fixture did not start."); Poll(controller);
        Check(controller.error.Length > 0 && controller.slots.Count == 1, "Failed IO hid its diagnostic or erased valid metadata.");
        var pages = new Inno.VisualNovel.Player.Binding.SavePageDataSource(controller); pages.Move(100);
        Check(pages.page == 9 && pages.ReadRows("saves.slots").Count == 10, "Manual slots were not bounded to ten pages.");
        Check(!pages.TrySlot("0:100", out _) && !pages.TrySlot("2:1", out _) && !pages.TrySlot("-1:0", out _), "Invalid action keys escaped configured slot bounds.");
        pages.Select(SaveSlotKind.Quick);
        Check(pages.ReadRows("saves.slots").Count == 1 && !pages.ReadRows("saves.slots")[0].values["canSave"].boolean, "Numbered UI writes could overwrite the dedicated quick slot.");
    }

    private static void Poll(SaveSlotController controller)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (controller.busy)
        {
            controller.Poll();
            if (timeout.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Slot operation did not complete.");
            if (controller.busy) Thread.Yield();
        }
    }

    internal static void EditableSaveShell(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var project = Project(codec, compiler);
        var font = new Inno.Text.FontAsset();
        project.screens = new[] { "title", "reading", "pause", "settings", "history", "credits", "save", "load", "confirm" }
            .Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, font, "Clear") }).ToArray();
        using var session = new NovelSession(project); session.StartNew();
        var service = new SaveService(new MemoryStorage(), new EditorNovelStateCodec(native), session.gameId, session.buildFingerprint);
        using var view = new NovelGameView(project, session, static (element, text) => (text.Length * 8, element.style.fontSize), service);
        Poll(view.persistence!); session.OpenPage("save");
        Click("写入槽位"); Poll(view.persistence!);
        Check(view.persistence!.slots.Count == 1, "The actual save page did not commit its bound slot.");
        Click("写入槽位");
        Check(session.page == "confirm" && !view.persistence.busy, "An occupied slot was overwritten before confirmation.");
        Click("取消"); Check(session.page == "save", "Cancelling overwrite did not restore the previous page.");
        Click("写入槽位"); Click("确认"); Poll(view.persistence);
        session.Advance(); session.Advance(); session.OpenPage("load");
        Click("读取"); Check(session.page == "confirm", "Loading replaced progress before confirmation.");
        Click("确认");
        Check(session.page == "load", "The load mutated session state inside its async continuation.");
        Poll(view.persistence); view.BuildFrame();
        Check(session.page == "reading" && session.story!.status == StoryStatus.Dialogue && !session.story.isTextComplete, "Confirmed UI load did not restore a fresh interactive boundary.");

        void Click(string text)
        {
            var item = view.BuildFrame().items.First(item => item.kind == Model.Interface.UiElementKind.Button && item.text == text && item.bounds.width > 0);
            float x = item.bounds.x + item.bounds.width / 2, y = item.bounds.y + item.bounds.height / 2;
            view.HandleInput(new() { pointerInside = true, x = x, y = y, pressed = true });
            view.HandleInput(new() { pointerInside = true, x = x, y = y, released = true });
        }
    }

    internal static void SeparateProgressAndPreferences(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) => SeparateProgressAndPreferencesAsync(codec, compiler, native).GetAwaiter().GetResult();

    private static async Task SeparateProgressAndPreferencesAsync(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var disk = new MemoryStorage(); var storage = new FailingStorage(disk);
        var project = Project(codec, compiler);
        using var session = new NovelSession(project); session.StartNew(); session.Advance(); session.Tick(0);
        var store = new GlobalProgressStore(session.gameId, storage, new EditorNovelStateCodec(native));
        Check(await store.ReadProgressAsync() is null && await store.ReadPreferencesAsync() is null, "First-run global data did not remain absent.");
        await store.SaveProgressAsync(session);
        var preferences = new PlayerPreferences { textSpeed = 2, reducedMotion = true };
        await store.SavePreferencesAsync(preferences);
        var loaded = await store.ReadPreferencesAsync() ?? throw new InvalidOperationException("Preferences were not committed.");
        Check(loaded.textSpeed == 2 && loaded.reducedMotion && (await store.ReadProgressAsync())!.readContent.Length == 1, "Independent global packets overwrote each other.");
        session.ApplyPreferences(loaded); loaded.textSpeed = 3;
        Check(session.preferences.textSpeed == 2, "Applied preferences retained a mutable candidate alias.");
        double[] gain = preferences.channelVolumes; gain[0] = 99;
        Check(preferences.channelVolumes[0] == 1, "Settings exposed mutable gain storage.");
        Throws<ArgumentOutOfRangeException>(() => preferences.masterVolume = double.NaN);
        Throws<ArgumentException>(() => preferences.channelVolumes = [1, 1]);
        preferences.textSpeed = 3; storage.failWrites = true;
        bool rejected = false;
        try { await store.SavePreferencesAsync(preferences); } catch (IOException) { rejected = true; }
        Check(rejected && (await store.ReadPreferencesAsync())!.textSpeed == 2, "A failed settings write destroyed the previous native packet.");
        storage.failWrites = false;
        var ordinary = new SaveService(storage, new EditorNovelStateCodec(native), session.gameId, session.buildFingerprint);
        await ordinary.SaveAsync(new(SaveSlotKind.Manual, 0), session);
        Check((await disk.ListAsync()).Count == 3 && (await store.ReadProgressAsync())!.readContent.Length == 1, "Ordinary saves overlapped global progress or preferences.");
        using var other = new NovelSession(project); other.RestoreProgress((await store.ReadProgressAsync())!); other.StartNew(); other.Advance();
        Check(other.IsCurrentRead(), "Read progress did not survive an independently created game session.");
    }
    internal static void SessionCandidates(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        NovelSession session = Create(codec, compiler);
        session.StartNew(); session.Tick(0.1);
        NovelSessionState saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        string text = session.story!.visibleText;
        session.Advance(); session.Advance(); session.Choose("sea");
        session.Restore(saved);
        Check(session.story.status == StoryStatus.Dialogue && session.story.visibleText == text, "Native session restore changed partial reveal.");
        saved.stage.cameraZoom = 0;
        StoryMachine before = session.story;
        Throws<InvalidOperationException>(() => session.Restore(saved));
        Check(ReferenceEquals(before, session.story) && session.story.visibleText == text, "Invalid stage data replaced the active interpreter.");
        saved.stage.cameraZoom = 1; saved.buildFingerprint = "other";
        Throws<InvalidOperationException>(() => session.Restore(saved));
        Check(ReferenceEquals(before, session.story), "An incompatible build replaced the active game.");
        session.Advance(); session.Tick(0);
        var progress = session.CaptureProgress();
        session.Restore(session.Capture());
        Check(session.IsCurrentRead(), "Ordinary save restore erased read progress.");
        progress.variables = [new() { id = "unknown", value = NovelValue.Boolean(false) }];
        Throws<InvalidOperationException>(() => session.RestoreProgress(progress));
        Check(session.IsCurrentRead(), "Rejected progress changed the read set.");
    }

    internal static void AtomicSlots(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native,
        string projectRoot
    ) => AtomicSlotsAsync(codec, compiler, native, projectRoot).GetAwaiter().GetResult();

    private static async Task AtomicSlotsAsync(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native,
        string projectRoot
    ) {
        var disk = new MemoryStorage();
        var storage = new FailingStorage(disk);
        var session = Create(codec, compiler); session.StartNew(); session.Tick(0.1);
        var service = new SaveService(storage, new EditorNovelStateCodec(native), session.gameId, session.buildFingerprint, 2, 2);
        var manual = new SaveSlot(SaveSlotKind.Manual, 0);
        await service.SaveAsync(manual, session, new byte[] { 1, 2, 3 });
        NovelSessionState original = await service.ReadAsync(manual);
        session.Advance(); session.Advance(); storage.failWrites = true;
        bool failed = false;
        try { await service.SaveAsync(manual, session); } catch (IOException) { failed = true; }
        Check(failed && (await service.ReadAsync(manual)).narrative.nodeId == original.narrative.nodeId, "Failed write destroyed the previous packet.");
        storage.failWrites = false;
        var quick = new SaveSlot(SaveSlotKind.Quick, 0);
        await service.SaveAsync(quick, session);
        Check((await service.ReadAsync(manual)).narrative.status == StoryStatus.Dialogue && (await service.ReadAsync(quick)).narrative.status == StoryStatus.Choice, "Quick save overlaps manual storage.");
        Check((await service.NextAutomaticAsync()).index == 0, "Automatic rotation did not select the first empty slot.");
        await service.SaveAsync(new(SaveSlotKind.Automatic, 0), session);
        Check((await service.NextAutomaticAsync()).index == 1, "Automatic rotation ignored an occupied slot.");
        Check((await service.ListAsync()).Count == 3 && (await service.ListAsync()).Single(item => item.slot == manual).thumbnail.SequenceEqual(new byte[] { 1, 2, 3 }), "Packet metadata and thumbnail did not remain atomic.");
        StorageKey manualKey = (await disk.ListAsync()).Single(key => key.value.EndsWith("/manual/0", StringComparison.Ordinal));
        await disk.WriteAsync(manualKey, new byte[] { 0, 1, 2 });
        Check((await service.ListAsync()).Single(item => item.slot == manual).error.Length > 0, "Corrupt slots were silently hidden.");
        StoryMachine current = session.story!;
        try { session.Restore(await service.ReadAsync(manual)); } catch (Exception error) when (error is not OutOfMemoryException) { }
        Check(ReferenceEquals(current, session.story), "Corrupt loading changed the active game.");
        Check(await service.DeleteAsync(manual) && !(await service.DeleteAsync(manual)), "Slot deletion did not report existence accurately.");
    }

    private static NovelSession Create(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) => new(Project(codec, compiler));

    private static NovelProjectAsset Project(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        return new NovelProjectAsset { gameId = "storage-contract", buildFingerprint = "complete-build", chapters = [chapter], startChapter = chapter.chapterId };
    }

    private sealed class FailingStorage(IApplicationStorage inner) : IApplicationStorage
    {
        internal bool failWrites;
        public ValueTask<bool> ExistsAsync(StorageKey key, CancellationToken cancellationToken = default) => inner.ExistsAsync(key, cancellationToken);
        public ValueTask<byte[]?> ReadAsync(StorageKey key, CancellationToken cancellationToken = default) => inner.ReadAsync(key, cancellationToken);
        public ValueTask WriteAsync(StorageKey key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
            => failWrites ? ValueTask.FromException(new IOException("Injected precommit failure.")) : inner.WriteAsync(key, value, cancellationToken);
        public ValueTask<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default) => inner.DeleteAsync(key, cancellationToken);
        public ValueTask<IReadOnlyList<StorageKey>> ListAsync(StorageKey? prefix = null, CancellationToken cancellationToken = default) => inner.ListAsync(prefix, cancellationToken);
    }

    private sealed class MemoryStorage : IApplicationStorage
    {
        private readonly Dictionary<StorageKey, byte[]> m_packets = [];
        public ValueTask<bool> ExistsAsync(StorageKey key, CancellationToken cancellationToken = default) => ValueTask.FromResult(m_packets.ContainsKey(key));
        public ValueTask<byte[]?> ReadAsync(StorageKey key, CancellationToken cancellationToken = default) => ValueTask.FromResult(m_packets.TryGetValue(key, out byte[]? value) ? value.ToArray() : null);
        public ValueTask WriteAsync(StorageKey key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            m_packets[key] = value.ToArray(); return ValueTask.CompletedTask;
        }
        public ValueTask<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default) => ValueTask.FromResult(m_packets.Remove(key));
        public ValueTask<IReadOnlyList<StorageKey>> ListAsync(StorageKey? prefix = null, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<StorageKey>>(m_packets.Keys.Where(key => prefix is null || key.value.StartsWith(prefix.Value.value, StringComparison.Ordinal)).OrderBy(static key => key.value, StringComparer.Ordinal).ToArray());
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
