using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Hosting;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeSceneMaterialContracts
{
    internal static void BareSceneUsesNativePackagedDefaults(
        string repository,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry serialization,
        IdentityAllocator identities
    ) {
        string root = Path.Combine(repository, "artifacts", "AcceptanceProjects", "NativeSceneMaterials-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(repository, "Assets", "BuiltIn", "Shaders");
        string target = Path.Combine(root, "Assets", "BuiltIn", "Shaders");
        foreach (string path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(source, path));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(path, destination);
        }
        using var logs = new LogRouter();
        using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library")) with { enableFileSystemWatcher = false });
        using var assetsScope = AssetExecutionContext.EnterScope(assets);
        using var world = new SceneWorld(identities, types); using var sceneScope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Bare VN scene");
        var system = scene.AddSystem<NovelSceneSystem>();
        var image = scene.CreateObject("Image").AddComponent<NovelImageComponent>();
        image.width = 100; image.height = 80;
        Check(system.surfaceMaterial is { isMissing: false, shader: not null } && system.imageEffectsMaterial is { isMissing: false, shader: not null },
            "The bare native System did not load its packaged default materials.");
        var dependencies = new AssetDependencyCollection();
        var context = AssetSerializationContext.Create(assets, dependencies);
        byte[] before = serialization.Serialize(scene, context);
        var frame = system.CaptureScene();
        Check(frame.quads.Count == 1 && system.project is null && !system.hasPresentation
            && before.SequenceEqual(serialization.Serialize(scene, context)), "Bare-scene capture required Play or mutated the document.");
        Check(dependencies.dependencies.Any(item => item.persistentId == system.surfaceMaterial!.identity.persistentId)
            && dependencies.dependencies.Any(item => item.persistentId == system.imageEffectsMaterial!.identity.persistentId),
            "Implicit default loading omitted the serialized material dependencies from the native build closure.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
