using System;
using System.Linq;
using Inno.Core.Identity;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class SceneOwnershipContracts
{
    internal static void NativeActivationAndDestruction(
        IdentityAllocator identities,
        TypeCatalog types,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        using var world = new SceneWorld(identities, types);
        using IDisposable scope = world.EnterScope();
        NovelProjectAsset game = Game("scene-owned", codec, compiler);
        RejectSession(new NovelSceneSystem { project = game });
        var first = new GameScene("First VN scene");
        NovelSceneSystem player = first.AddSystem<NovelSceneSystem>(); player.project = game;
        Check(first.GetObjects().Count == 0 && first.GetSystems().Single() == player && player.owningScene == first,
            "The system introduced a holder object or a parallel scene.");
        RejectSession(player);
        SceneManager.LoadScene(first);
        var second = SceneManager.LoadNewSceneAdditive("Second VN scene");
        NovelSceneSystem other = second.AddSystem<NovelSceneSystem>(); other.project = game;
        world.FixedUpdate(0);
        var original = player.session; var independent = other.session;
        original.StartNew(); independent.StartNew();
        Check(!ReferenceEquals(original, independent) && original.story!.status == StoryStatus.Dialogue
            && independent.story!.status == StoryStatus.Dialogue, "Native systems shared a live story.");
        player.rollbackBudgetMiB = 0;
        Check(original.rollbackByteBudget == 0 && independent.rollbackByteBudget == 64L * 1024 * 1024,
            "A system changed another scene's retention policy.");
        player.enabled = false;
        Check(original.story!.status == StoryStatus.Stopped && independent.story!.status == StoryStatus.Dialogue,
            "Disabling a system did not retire exactly its own narrative.");
        RejectSession(player);
        player.enabled = true;
        var enabled = player.session; enabled.StartNew();
        Check(!ReferenceEquals(enabled, original), "Re-enabling a system reused its disposed session.");
        player.project = game;
        Check(ReferenceEquals(enabled, player.session), "Reassigning the same game restarted the story.");
        player.project = Game("replacement", codec, compiler);
        Check(enabled.story!.status == StoryStatus.Stopped && player.session.gameId == "replacement", "Changing the game retained the old session.");
        var replacement = player.session; replacement.StartNew();
        Check(first.RemoveSystem(player) && player.isDestroyed && replacement.story!.status == StoryStatus.Stopped,
            "Removing a native system leaked its story.");
        RejectSession(player);
        Check(SceneManager.UnloadScene(second) && other.isDestroyed && independent.story!.status == StoryStatus.Stopped,
            "Unloading a native scene leaked its system-owned story.");
        RejectSession(other);
        var stopped = first.AddSystem<NovelSceneSystem>(); stopped.project = game;
        world.FixedUpdate(0);
        stopped.StartGame(); var previous = stopped.session;
        var animated = first.CreateObject("Stopped animation").AddComponent<Integration.Components.NovelCharacterComponent>();
        stopped.StopGame();
        world.Update(.25f);
        Check(animated.elapsed == 0 && previous.story!.status == StoryStatus.Stopped && !stopped.hasPresentation,
            "The native update recreated a stopped game or advanced stopped scene components.");
        RejectSession(stopped);
        stopped.StartGame();
        Check(!ReferenceEquals(previous, stopped.session) && stopped.session.story!.status == StoryStatus.Dialogue,
            "An explicit restart did not create a fresh story.");
    }
    private static NovelProjectAsset Game(
        string id,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset();
        chapter.program = compiler.Compile(chapter).program!;
        return new() { gameId = id, chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }
                .Select(static role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }
    private static void RejectSession(NovelSceneSystem system)
    {
        try { _ = system.session; } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("An inactive or detached system created a VN session.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
