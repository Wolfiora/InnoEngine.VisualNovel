using InnoEngine.Assets;
using System;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Resolves packaged resources through declared native asset mounts.</summary>
public static class BuiltInNovelAssets
{
    /// <summary>Loads an installed built-in, or the same project's source during plugin development.</summary>
    /// <typeparam name="T">The expected imported resource contract.</typeparam>
    /// <param name="path">The plugin-local resource path.</param>
    /// <returns>The canonical imported resource; missing resources produce native diagnostics.</returns>
    public static T Load<T>(string path) where T : AssetObject
    {
        T resource = Assets.Load<T>(Assets.LocalPath(path));
        if (resource.isMissing || resource.assetPath.localPath.Length == 0)
            throw new InvalidOperationException("Built-in resource was not imported by the active host: " + path + ". Check the native importer capabilities.");
        return resource;
    }
}
