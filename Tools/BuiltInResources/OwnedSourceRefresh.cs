using System;
using System.IO;
using Inno.Assets;
using Inno.Assets.Pipeline;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal sealed class OwnedSourceRefresh(
    string root,
    string subtree,
    bool enabled
)
{
    private readonly string m_backup = Path.Combine(root, "artifacts", "SourceBackups", subtree.Replace('/', '-') + "-" + Guid.NewGuid().ToString("N"));
    internal void Save<T>(
        string relative,
        T source
    ) where T : AssetObject
    {
        string boundary = Path.GetFullPath(Path.Combine(root, "Assets", subtree)) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, "Assets", relative));
        if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Native source regeneration escaped its owned subtree: " + relative);
        if (File.Exists(path))
        {
            if (!enabled) return;
            string backup = Path.Combine(m_backup, Path.GetRelativePath(boundary, path));
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!); File.Copy(path, backup);
        }
        if (!EditorAssets.Save(AssetPath.Project(relative), source)) throw new InvalidOperationException("Native owned source did not import: " + relative);
    }
}
