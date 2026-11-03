using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Identity;
using Inno.Runtime;
using Inno.Scene;

namespace Inno.VisualNovel.Tools.Bootstrap;

internal static class HeadlessSceneAudit
{
    internal static void Run(
        EngineHost engine,
        AssetPipeline assets,
        string output
    ) {
        using var world = new SceneWorld(new IdentityAllocator(), engine.types);
        using var scope = world.EnterScope();
        var lines = new List<string>();
        int missing = 0, count = 0;
        foreach (var entry in assets.GetFileSystemEntries(includeDirectories: false)
            .Where(static entry => entry.source == AssetSourceId.project && !AssetSample.IsRuntimeExcluded(entry.assetPath, false))
            .OrderBy(static entry => entry.assetPath.localPath, StringComparer.Ordinal))
        {
            if (!assets.TryGetAssetType(entry.assetPath, out var type) || type != typeof(SceneAsset)) continue;
            var scene = assets.Load<SceneAsset>(entry.assetPath).Instantiate(engine.serialization, assets);
            SceneManager.LoadSceneAdditive(scene);
            count++;
            lines.Add(entry.assetPath + ": " + scene.GetObjects().Count + " objects; " + scene.GetSystems().Count + " systems");
            foreach (var system in scene.GetSystems())
            {
                lines.Add("  System: " + system.GetType().FullName);
                if (system is MissingGameSystem unavailable) { lines.Add("  MISSING: " + unavailable.missingTypeName); missing++; }
            }
            foreach (var owner in scene.GetObjects())
                foreach (var component in owner.GetComponents<GameComponent>())
                    if (component is MissingGameComponent unavailable)
                    { lines.Add("  MISSING on " + owner.name + ": " + unavailable.missingTypeName); missing++; }
            SceneManager.UnloadScene(scene);
        }
        lines.Add("Scene count: " + count + "; missing types: " + missing + "; no runtime update, GPU or desktop input was executed.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllLines(output, lines);
        foreach (string line in lines) Console.WriteLine(line);
        if (missing > 0) throw new InvalidOperationException("The candidate package cannot preserve the consumer's existing scene types; rebuild the content before replacing its installed package.");
    }
}
