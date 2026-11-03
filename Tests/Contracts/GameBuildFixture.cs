using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Identity;
using Inno.Core.Serialization;
using Inno.Editor.Assets;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class GameBuildFixture
{
    internal static void Write(
        string repository,
        string root,
        NovelProjectAsset project,
        IdentityAllocator identities,
        TypeCatalog types,
        SerializationRegistry native,
        AssetPipeline assets
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = new GameScene("VisualNovelAcceptance");
        var player = scene.AddSystem<NovelSceneSystem>(); player.project = project;
        var captured = SceneAsset.Capture(scene, native, assets);
        var path = AssetPath.Project("Game/BuildStart.iscene");
        if (!EditorAssets.Save(path, captured)) throw new InvalidOperationException("The official scene importer did not save the build fixture.");
        var imported = Assets.Assets.Load<SceneAsset>(path);
        var restored = imported.Instantiate(native, assets);
        try
        {
            var component = restored.GetSystems().OfType<NovelSceneSystem>().Single();
            if (component.project?.identity.persistentId != project.identity.persistentId || component.owningScene != restored)
                throw new InvalidOperationException("The native build scene lost its native Scene -> VN System and game asset reference.");
        }
        finally
        {
            foreach (var item in restored.GetObjects().ToArray()) restored.DestroyObject(item);
            scene.RemoveSystem(player);
        }
        string report = Path.Combine(repository, "artifacts", "Reports", "GameBuildFixturePath.txt");
        File.WriteAllText(report, root);
        Console.WriteLine("Headless build fixture: " + root + " / " + path);
    }
}
