using System;
using Inno.Core.Identity;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Hosting;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeTargetBindingContracts
{
    internal static void ExplicitReferencesDoNotRepairByName(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Explicit bindings");
        var system = scene.AddSystem<NovelSceneSystem>();
        var first = scene.CreateObject("Same name"); var firstTarget = first.AddComponent<NovelTargetComponent>(); firstTarget.targetId = "actor/first";
        var firstCharacter = first.AddComponent<NovelCharacterComponent>();
        var second = scene.CreateObject("Same name"); var secondTarget = second.AddComponent<NovelTargetComponent>(); secondTarget.targetId = "actor/second";
        var secondCharacter = second.AddComponent<NovelCharacterComponent>();
        system.targets = [firstTarget, secondTarget];
        Check(ReferenceEquals(system.ResolveTarget<NovelCharacterComponent>("actor/first"), firstCharacter)
            && ReferenceEquals(system.ResolveTarget<NovelCharacterComponent>("actor/second"), secondCharacter), "Two instances resolved through their names rather than exact bindings.");
        first.SetActive(false);
        Check(ReferenceEquals(system.ResolveTarget<NovelCharacterComponent>("actor/first"), firstCharacter) && !first.activeInHierarchy,
            "Resolving an inactive show target changed activation or discarded its reference.");
        Reject(() => system.ResolveTarget<NovelCameraComponent>("actor/first"));
        Reject(() => system.ResolveTarget<NovelCharacterComponent>("not-bound"));
        secondTarget.targetId = firstTarget.targetId; Reject(() => NovelSceneTargets.Validate(system)); secondTarget.targetId = "actor/second";
        var otherScene = SceneManager.LoadNewSceneAdditive("Other scope");
        var foreign = otherScene.CreateObject("Foreign").AddComponent<NovelTargetComponent>(); foreign.targetId = "foreign";
        system.targets = [foreign]; Reject(() => NovelSceneTargets.Validate(system));
        system.targets = [firstTarget]; scene.DestroyObject(first);
        var replacement = scene.CreateObject("Same name"); replacement.AddComponent<NovelTargetComponent>().targetId = "actor/first";
        replacement.AddComponent<NovelCharacterComponent>();
        Reject(() => system.ResolveTarget<NovelCharacterComponent>("actor/first"));
        system.targets = [replacement.GetComponent<NovelTargetComponent>()];
        Check(ReferenceEquals(system.ResolveTarget<NovelCharacterComponent>("actor/first").gameObject, replacement), "An explicit reassignment did not restore the requested target.");
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("An invalid native binding was silently accepted.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
