using System;
using System.Linq;
using Inno.Core.Identity;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Performance;

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
        var detached = new NovelPlayerComponent { project = game };
        RejectSession(detached);

        var first = new GameScene("First VN scene");
        GameObject owner = first.CreateObject("VN player");
        NovelPlayerComponent player = owner.AddComponent<NovelPlayerComponent>();
        player.project = game;
        Check(ReferenceEquals(player.gameObject, owner) && ReferenceEquals(player.transform, owner.transform)
            && owner.GetComponents<NovelPlayerComponent>().Single() == player && first.GetObjects().Single() == owner,
            "The player did not use the native scene/object/component ownership tree.");
        RejectSession(player);
        SceneManager.LoadScene(first);
        var second = SceneManager.LoadNewSceneAdditive("Second VN scene");
        GameObject otherOwner = second.CreateObject("Another VN player");
        NovelPlayerComponent other = otherOwner.AddComponent<NovelPlayerComponent>();
        other.project = game;
        var performanceTargets = new NovelPerformanceTargets(new System.Collections.Generic.Dictionary<string, PerformanceProperty[]> { ["extension/native"] = [PerformanceProperty.X] });
        player.ConfigurePerformanceTargets(performanceTargets);
        NovelSession original = player.session, independent = other.session;
        Check(original.buildFingerprint != independent.buildFingerprint, "Native component configuration did not pass its extension contract into the owned session.");
        bool lateTargets = false;
        try { player.ConfigurePerformanceTargets(performanceTargets); } catch (InvalidOperationException) { lateTargets = true; }
        Check(lateTargets, "An active native component changed its frozen extension contract after computing save identity.");
        original.StartNew(); independent.StartNew();
        Check(!ReferenceEquals(original, independent) && original.story!.status == StoryStatus.Dialogue
            && independent.story!.status == StoryStatus.Dialogue, "Native players shared a live narrative session.");
        player.rollbackBudgetMiB = 0;
        Check(original.rollbackByteBudget == 0 && independent.rollbackByteBudget == 64L * 1024 * 1024 && original.rollbackStoredBytes is null,
            "The native component budget failed to update its own session or fabricated a codec capability.");
        bool badBudget = false; try { player.rollbackBudgetMiB = -1; } catch (ArgumentOutOfRangeException) { badBudget = true; }
        Check(badBudget && player.rollbackBudgetMiB == 0, "The native component accepted an invalid retention policy.");

        player.enabled = false;
        Check(original.story!.status == StoryStatus.Stopped && independent.story!.status == StoryStatus.Dialogue,
            "Native component disable failed to retire exactly its own narrative.");
        RejectSession(player);
        player.enabled = true;
        NovelSession enabled = player.session; enabled.StartNew();
        Check(!ReferenceEquals(enabled, original) && enabled.buildFingerprint == original.buildFingerprint, "Re-enabled native component retained its retired session or lost its explicit extension contract.");
        owner.SetActive(false);
        Check(enabled.story!.status == StoryStatus.Stopped, "Native object deactivation did not stop its player.");
        RejectSession(player);
        owner.SetActive(true);
        NovelSession active = player.session; active.StartNew();
        player.project = game;
        Check(ReferenceEquals(active, player.session), "Reassigning the same game unexpectedly restarted its session.");
        player.project = Game("replacement", codec, compiler);
        Check(active.story!.status == StoryStatus.Stopped && player.session.gameId == "replacement",
            "Replacing the game binding retained the old content session.");
        NovelSession replacement = player.session; replacement.StartNew();
        first.DestroyObject(owner);
        Check(player.isDestroyed && replacement.story!.status == StoryStatus.Stopped && first.GetObjects().Count == 0,
            "Native object destruction leaked its component-owned narrative.");
        RejectSession(player);

        Check(otherOwner.RemoveComponent(other) && other.isDestroyed && independent.story!.status == StoryStatus.Stopped
            && otherOwner.GetComponents<NovelPlayerComponent>().Count == 0 && otherOwner.transform is not null,
            "Native component removal did not retire the narrative while preserving the ordinary object.");
        RejectSession(other);
        NovelPlayerComponent last = otherOwner.AddComponent<NovelPlayerComponent>(); last.project = game;
        NovelSession unloading = last.session; unloading.StartNew();
        Check(SceneManager.UnloadScene(second) && last.isDestroyed && unloading.story!.status == StoryStatus.Stopped,
            "Native scene unload did not release its attached player.");
        RejectSession(last);
    }

    private static NovelProjectAsset Game(
        string id,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset();
        var result = compiler.Compile(chapter);
        Check(result.success, "The native scene fixture story did not compile.");
        chapter.program = result.program!;
        return new() { gameId = id, chapters = [chapter], startChapter = chapter.chapterId,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }
                .Select(static role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }

    private static void RejectSession(NovelPlayerComponent component)
    {
        try { _ = component.session; }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("An inactive, unloaded, detached or destroyed component created a live VN session.");
    }

    private static void Check(
        bool condition,
        string message
    ) {
        if (!condition) throw new InvalidOperationException(message);
    }
}
